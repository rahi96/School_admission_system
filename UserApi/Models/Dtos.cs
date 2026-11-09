namespace UserApi.Models;

public record CreateClassRequest(string? Name, int? TotalSeats);

public record ClassResponse(string Id, string Name, int TotalSeats);

public record ClassStatusResponse(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);

public record HoldResponse(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt);

public record HoldStatusResponse(
    string HoldId,
    string ClassId,
    string Status,
    DateTimeOffset ExpiresAt);

public record ConfirmResponse(string HoldId, string Status);

public record MyHoldResponse(
    string HoldId,
    string ClassId,
    string ClassName,
    string Status,
    DateTimeOffset ExpiresAt);

public record ErrorResponse(string Error, string Message);
