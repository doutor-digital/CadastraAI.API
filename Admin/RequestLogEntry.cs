namespace CadastraAI.API.Admin;

public record RequestLogEntry(
    DateTime At,
    string Method,
    string Path,
    string? Query,
    int Status,
    long DurationMs,
    string? UserId,
    string? UserEmail,
    string? Ip,
    string? UserAgent,
    string? RequestBody,
    string? ErrorMessage);
