using Npgsql;
using NpgsqlTypes;
using UserApi.Models;

namespace UserApi.Services;

public enum ResultStatus
{
    Ok,
    NotFound,
    Conflict,
    BadRequest,
    Forbidden,
    Unavailable
}

public record ServiceResult<T>(ResultStatus Status, T? Value, string? Error = null, string? Message = null)
{
    public static ServiceResult<T> Ok(T value) => new(ResultStatus.Ok, value);
    public static ServiceResult<T> NotFound(string message) => new(ResultStatus.NotFound, default, "NOT_FOUND", message);
    public static ServiceResult<T> Conflict(string code, string message) => new(ResultStatus.Conflict, default, code, message);
    public static ServiceResult<T> BadRequest(string message) => new(ResultStatus.BadRequest, default, "BAD_REQUEST", message);
    public static ServiceResult<T> Forbidden(string message) => new(ResultStatus.Forbidden, default, "FORBIDDEN", message);
    public static ServiceResult<T> Unavailable(string message) => new(ResultStatus.Unavailable, default, "UNAVAILABLE", message);
}

public class AdmissionService
{
    private const string Held = "HELD";
    private const string Confirmed = "CONFIRMED";
    private const string Expired = "EXPIRED";
    private const string Cancelled = "CANCELLED";

    private readonly NpgsqlDataSource _dataSource;
    private readonly int _holdDurationSeconds;
    private readonly ILogger<AdmissionService> _logger;

    public AdmissionService(NpgsqlDataSource dataSource, IConfiguration configuration, ILogger<AdmissionService> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
        var raw = Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS")
                  ?? configuration["HOLD_DURATION_SECONDS"];
        _holdDurationSeconds = int.TryParse(raw, out var parsed) && parsed > 0 ? parsed : 600;
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    public async Task<ClassResponse> CreateClassAsync(string name, int totalSeats)
    {
        var id = NewId("c");
        const string sql = "INSERT INTO classes (id, name, total_seats) VALUES (@id, @name, @seats)";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("seats", totalSeats);
        await cmd.ExecuteNonQueryAsync();
        return new ClassResponse(id, name, totalSeats);
    }

    public async Task<List<ClassStatusResponse>> ListClassesAsync()
    {
        const string sql = """
            SELECT c.id, c.name, c.total_seats,
                   COALESCE(SUM(CASE WHEN h.status = 'HELD' AND h.expires_at > now() THEN 1 ELSE 0 END), 0) AS held,
                   COALESCE(SUM(CASE WHEN h.status = 'CONFIRMED' THEN 1 ELSE 0 END), 0) AS confirmed
            FROM classes c
            LEFT JOIN holds h ON h.class_id = c.id
            GROUP BY c.id, c.name, c.total_seats
            ORDER BY c.created_at
            """;
        var list = new List<ClassStatusResponse>();
        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapClassStatus(reader));
        }
        return list;
    }

    public async Task<ClassStatusResponse?> GetClassAsync(string classId)
    {
        const string sql = """
            SELECT c.id, c.name, c.total_seats,
                   COALESCE(SUM(CASE WHEN h.status = 'HELD' AND h.expires_at > now() THEN 1 ELSE 0 END), 0) AS held,
                   COALESCE(SUM(CASE WHEN h.status = 'CONFIRMED' THEN 1 ELSE 0 END), 0) AS confirmed
            FROM classes c
            LEFT JOIN holds h ON h.class_id = c.id
            WHERE c.id = @id
            GROUP BY c.id, c.name, c.total_seats
            """;
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", classId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapClassStatus(reader);
        }
        return null;
    }

    private static ClassStatusResponse MapClassStatus(NpgsqlDataReader reader)
    {
        var id = reader.GetString(0);
        var name = reader.GetString(1);
        var total = reader.GetInt32(2);
        var held = (int)reader.GetInt64(3);
        var confirmed = (int)reader.GetInt64(4);
        var available = total - held - confirmed;
        if (available < 0) available = 0;
        return new ClassStatusResponse(id, name, total, held, confirmed, available);
    }

    public async Task<ServiceResult<HoldResponse>> CreateHoldAsync(string classId, string studentId)
    {
        var holdId = NewId("h");
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(_holdDurationSeconds);

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            // Step 1: lock the class row so concurrent holds for the same class are
            // serialized. This also confirms the class exists and gives us capacity.
            int totalSeats;
            await using (var lockCmd = new NpgsqlCommand(
                "SELECT total_seats FROM classes WHERE id = @cid FOR UPDATE", conn, tx))
            {
                lockCmd.Parameters.AddWithValue("cid", classId);
                var totalObj = await lockCmd.ExecuteScalarAsync();
                if (totalObj is null)
                {
                    await tx.RollbackAsync();
                    return ServiceResult<HoldResponse>.NotFound("Class not found.");
                }
                totalSeats = (int)totalObj;
            }

            // Step 2: a single atomic statement that re-reads under a fresh snapshot
            // (taken after the lock was acquired, so it sees every committed hold),
            // enforces the one-active-application rule, checks capacity, and inserts
            // the hold only when both guards pass. Running this as one statement keeps
            // the lock critical-section to a single round-trip.
            await using (var cmd = new NpgsqlCommand(
                """
                WITH occupied AS (
                    SELECT COUNT(*) AS cnt FROM holds
                    WHERE class_id = @cid
                      AND (status = 'CONFIRMED' OR (status = 'HELD' AND expires_at > now()))
                ),
                dup AS (
                    SELECT COUNT(*) AS cnt FROM holds
                    WHERE class_id = @cid AND student_id = @sid
                      AND (status = 'CONFIRMED' OR (status = 'HELD' AND expires_at > now()))
                ),
                ins AS (
                    INSERT INTO holds (id, class_id, student_id, status, expires_at)
                    SELECT @hid, @cid, @sid, 'HELD', @exp
                    WHERE (SELECT cnt FROM dup) = 0
                      AND (SELECT cnt FROM occupied) < @total
                    RETURNING id
                )
                SELECT (SELECT cnt FROM dup)            AS dup_cnt,
                       (SELECT COUNT(*) FROM ins)       AS inserted
                """, conn, tx))
            {
                cmd.Parameters.AddWithValue("cid", classId);
                cmd.Parameters.AddWithValue("sid", studentId);
                cmd.Parameters.AddWithValue("hid", holdId);
                cmd.Parameters.AddWithValue("exp", NpgsqlDbType.TimestampTz, expiresAt);
                cmd.Parameters.AddWithValue("total", totalSeats);

                long dupCnt;
                long inserted;
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    await reader.ReadAsync();
                    dupCnt = reader.GetInt64(0);
                    inserted = reader.GetInt64(1);
                }

                if (dupCnt > 0)
                {
                    await tx.RollbackAsync();
                    return ServiceResult<HoldResponse>.Conflict(
                        "DUPLICATE_HOLD",
                        "Student already has an active application for this class.");
                }

                if (inserted == 0)
                {
                    await tx.RollbackAsync();
                    return ServiceResult<HoldResponse>.Conflict("NO_SEATS", "No seats available for this class.");
                }
            }

            await tx.CommitAsync();
            return ServiceResult<HoldResponse>.Ok(
                new HoldResponse(holdId, classId, studentId, Held, expiresAt));
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            // Transient DB overload (e.g. connection-pool/lock-wait timeout under a
            // heavy burst). Never surface a 500 for this; ask the caller to retry.
            _logger.LogWarning(ex, "Transient database error while creating a hold for class {ClassId}.", classId);
            return ServiceResult<HoldResponse>.Unavailable("The system is busy. Please try again in a moment.");
        }
    }

    public async Task<HoldStatusResponse?> GetHoldAsync(string holdId)
    {
        const string sql = "SELECT id, class_id, status, expires_at FROM holds WHERE id = @id";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", holdId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var id = reader.GetString(0);
        var classId = reader.GetString(1);
        var status = reader.GetString(2);
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(3);
        if (status == Held && expiresAt <= DateTimeOffset.UtcNow)
        {
            status = Expired;
        }
        return new HoldStatusResponse(id, classId, status, expiresAt);
    }

    public async Task<ServiceResult<ConfirmResponse>> ConfirmHoldAsync(string holdId, string studentId)
    {
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await using var selectCmd = new NpgsqlCommand(
                "SELECT student_id, status, expires_at FROM holds WHERE id = @id FOR UPDATE", conn, tx);
            selectCmd.Parameters.AddWithValue("id", holdId);
            await using var reader = await selectCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                await reader.DisposeAsync();
                await tx.RollbackAsync();
                return ServiceResult<ConfirmResponse>.NotFound("Hold not found.");
            }

            var ownerId = reader.GetString(0);
            var status = reader.GetString(1);
            var expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
            await reader.DisposeAsync();

            if (ownerId != studentId)
            {
                await tx.RollbackAsync();
                return ServiceResult<ConfirmResponse>.Forbidden("This hold belongs to another student.");
            }

            // Idempotent: confirming an already-confirmed hold succeeds without double counting.
            if (status == Confirmed)
            {
                await tx.RollbackAsync();
                return ServiceResult<ConfirmResponse>.Ok(new ConfirmResponse(holdId, Confirmed));
            }

            if (status == Cancelled)
            {
                await tx.RollbackAsync();
                return ServiceResult<ConfirmResponse>.Conflict("HOLD_CANCELLED", "This application was cancelled.");
            }

            if (status == Expired || (status == Held && expiresAt <= DateTimeOffset.UtcNow))
            {
                await tx.RollbackAsync();
                return ServiceResult<ConfirmResponse>.Conflict("HOLD_EXPIRED", "This application has expired.");
            }

            await using (var updateCmd = new NpgsqlCommand(
                "UPDATE holds SET status = 'CONFIRMED' WHERE id = @id", conn, tx))
            {
                updateCmd.Parameters.AddWithValue("id", holdId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return ServiceResult<ConfirmResponse>.Ok(new ConfirmResponse(holdId, Confirmed));
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            _logger.LogWarning(ex, "Transient database error while confirming hold {HoldId}.", holdId);
            return ServiceResult<ConfirmResponse>.Unavailable("The system is busy. Please try again in a moment.");
        }
    }

    public async Task<ServiceResult<bool>> CancelHoldAsync(string holdId, string studentId)
    {
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await using var selectCmd = new NpgsqlCommand(
                "SELECT student_id, status FROM holds WHERE id = @id FOR UPDATE", conn, tx);
            selectCmd.Parameters.AddWithValue("id", holdId);
            await using var reader = await selectCmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                await reader.DisposeAsync();
                await tx.RollbackAsync();
                return ServiceResult<bool>.NotFound("Hold not found.");
            }

            var ownerId = reader.GetString(0);
            var status = reader.GetString(1);
            await reader.DisposeAsync();

            if (ownerId != studentId)
            {
                await tx.RollbackAsync();
                return ServiceResult<bool>.Forbidden("This hold belongs to another student.");
            }

            if (status is Expired or Cancelled)
            {
                // Already released; treat as success (idempotent cancel).
                await tx.RollbackAsync();
                return ServiceResult<bool>.Ok(true);
            }

            await using (var updateCmd = new NpgsqlCommand(
                "UPDATE holds SET status = 'CANCELLED' WHERE id = @id", conn, tx))
            {
                updateCmd.Parameters.AddWithValue("id", holdId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            _logger.LogWarning(ex, "Transient database error while cancelling hold {HoldId}.", holdId);
            return ServiceResult<bool>.Unavailable("The system is busy. Please try again in a moment.");
        }
    }

    public async Task<List<MyHoldResponse>> GetMyHoldsAsync(string studentId)
    {
        const string sql = """
            SELECT h.id, h.class_id, c.name, h.status, h.expires_at
            FROM holds h
            JOIN classes c ON c.id = h.class_id
            WHERE h.student_id = @sid
            ORDER BY h.created_at DESC
            """;
        var list = new List<MyHoldResponse>();
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("sid", studentId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var classId = reader.GetString(1);
            var className = reader.GetString(2);
            var status = reader.GetString(3);
            var expiresAt = reader.GetFieldValue<DateTimeOffset>(4);
            if (status == Held && expiresAt <= DateTimeOffset.UtcNow)
            {
                status = Expired;
            }
            list.Add(new MyHoldResponse(id, classId, className, status, expiresAt));
        }
        return list;
    }

    public async Task<int> ExpireHoldsAsync()
    {
        const string sql = "UPDATE holds SET status = 'EXPIRED' WHERE status = 'HELD' AND expires_at <= now()";
        await using var cmd = _dataSource.CreateCommand(sql);
        return await cmd.ExecuteNonQueryAsync();
    }
}
