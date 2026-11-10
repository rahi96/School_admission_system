using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UserApi.Tests;

public class AdmissionApiTests : IClassFixture<AdmissionApiFactory>
{
    private readonly AdmissionApiFactory _factory;

    public AdmissionApiTests(AdmissionApiFactory factory)
    {
        _factory = factory;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpClient NewClient() => _factory.CreateClient();

    private static HttpRequestMessage WithStudent(HttpMethod method, string url, string studentId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Student-Id", studentId);
        return req;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    private async Task<string> CreateClassAsync(HttpClient client, string name, int totalSeats)
    {
        var resp = await client.PostAsJsonAsync("/classes", new { name, totalSeats });
        resp.EnsureSuccessStatusCode();
        var created = await ReadAsync<JsonElement>(resp);
        return created.GetProperty("id").GetString()!;
    }

    // ---- Test 1: happy path ----
    [Fact]
    public async Task Apply_Then_Confirm_UpdatesSeatCounts()
    {
        var client = NewClient();
        var classId = await CreateClassAsync(client, "Class 6 - Morning Shift", 10);
        const string studentId = "10000000000000001";

        // Apply (hold).
        var holdResp = await client.SendAsync(WithStudent(HttpMethod.Post, $"/classes/{classId}/holds", studentId));
        Assert.Equal(HttpStatusCode.Created, holdResp.StatusCode);
        var hold = await ReadAsync<JsonElement>(holdResp);
        var holdId = hold.GetProperty("holdId").GetString()!;
        Assert.Equal("HELD", hold.GetProperty("status").GetString());

        // After the hold: 1 held, 0 confirmed, 9 available.
        var afterHold = await ReadAsync<JsonElement>(await client.GetAsync($"/classes/{classId}"));
        Assert.Equal(1, afterHold.GetProperty("heldSeats").GetInt32());
        Assert.Equal(0, afterHold.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(9, afterHold.GetProperty("availableSeats").GetInt32());

        // Confirm (pay fee).
        var confirmResp = await client.SendAsync(WithStudent(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId));
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);
        var confirm = await ReadAsync<JsonElement>(confirmResp);
        Assert.Equal("CONFIRMED", confirm.GetProperty("status").GetString());

        // After confirm: 0 held, 1 confirmed, 9 available.
        var afterConfirm = await ReadAsync<JsonElement>(await client.GetAsync($"/classes/{classId}"));
        Assert.Equal(0, afterConfirm.GetProperty("heldSeats").GetInt32());
        Assert.Equal(1, afterConfirm.GetProperty("confirmedSeats").GetInt32());
    }

    // ---- Test 2: business rules ----
    [Fact]
    public async Task Confirm_IsIdempotent_And_DuplicateHold_Is409()
    {
        var client = NewClient();
        var classId = await CreateClassAsync(client, "Idempotency Class", 10);
        const string studentId = "20000000000000002";

        var holdResp = await client.SendAsync(WithStudent(HttpMethod.Post, $"/classes/{classId}/holds", studentId));
        var hold = await ReadAsync<JsonElement>(holdResp);
        var holdId = hold.GetProperty("holdId").GetString()!;

        // A second active application for the same student+class is rejected.
        var dupResp = await client.SendAsync(WithStudent(HttpMethod.Post, $"/classes/{classId}/holds", studentId));
        Assert.Equal(HttpStatusCode.Conflict, dupResp.StatusCode);

        // Confirming three times only counts once.
        for (var i = 0; i < 3; i++)
        {
            var r = await client.SendAsync(WithStudent(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId));
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        var status = await ReadAsync<JsonElement>(await client.GetAsync($"/classes/{classId}"));
        Assert.Equal(1, status.GetProperty("confirmedSeats").GetInt32());
    }

    // ---- Test 3: validation never returns 500 ----
    [Fact]
    public async Task InvalidInput_Returns4xx_Never500()
    {
        var client = NewClient();

        // Missing name.
        var noName = await client.PostAsJsonAsync("/classes", new { totalSeats = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);

        // Non-positive seats.
        var badSeats = await client.PostAsJsonAsync("/classes", new { name = "X", totalSeats = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, badSeats.StatusCode);

        var classId = await CreateClassAsync(client, "Validation Class", 5);

        // Hold without the X-Student-Id header.
        var noHeader = await client.PostAsync($"/classes/{classId}/holds", null);
        Assert.Equal(HttpStatusCode.BadRequest, noHeader.StatusCode);

        // Hold on a non-existent class.
        var missingClass = await client.SendAsync(WithStudent(HttpMethod.Post, "/classes/does-not-exist/holds", "30000000000000003"));
        Assert.Equal(HttpStatusCode.NotFound, missingClass.StatusCode);

        // Get a non-existent hold.
        var missingHold = await client.GetAsync("/holds/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missingHold.StatusCode);
    }

    // ---- Test 4: MANDATORY concurrency test ----
    [Fact]
    public async Task Concurrent_Holds_NeverExceed_TotalSeats()
    {
        var client = NewClient();
        const int totalSeats = 10;
        const int attempts = 200;
        var classId = await CreateClassAsync(client, "Concurrency Class", totalSeats);

        // Fire many simultaneous hold requests, each from a distinct student.
        var tasks = Enumerable.Range(0, attempts).Select(async i =>
        {
            var studentId = (40000000000000000L + i).ToString("D17");
            var resp = await client.SendAsync(WithStudent(HttpMethod.Post, $"/classes/{classId}/holds", studentId));
            return resp.StatusCode;
        });

        var results = await Task.WhenAll(tasks);

        var created = results.Count(s => s == HttpStatusCode.Created);
        var conflicts = results.Count(s => s == HttpStatusCode.Conflict);
        var busy = results.Count(s => s == HttpStatusCode.ServiceUnavailable);

        // The core guarantee: exactly the seat count are granted, no more.
        Assert.Equal(totalSeats, created);

        // No request may ever fail with a 500 (bad input / overload must be 4xx or 503).
        Assert.DoesNotContain(HttpStatusCode.InternalServerError, results);

        // Every non-granted request is cleanly rejected as 409 (no seat / duplicate)
        // or, under heavy burst, 503 (busy, retry) — never anything else.
        Assert.Equal(attempts, created + conflicts + busy);

        // The database must agree: held + confirmed exactly equals totalSeats and
        // never exceeds it, no matter how many requests raced.
        var status = await ReadAsync<JsonElement>(await client.GetAsync($"/classes/{classId}"));
        var held = status.GetProperty("heldSeats").GetInt32();
        var confirmed = status.GetProperty("confirmedSeats").GetInt32();
        Assert.True(held + confirmed <= totalSeats, $"Oversold: held={held} confirmed={confirmed}");
        Assert.Equal(totalSeats, held + confirmed);
    }
}
