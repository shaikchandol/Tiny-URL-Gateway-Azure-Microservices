namespace TinyUrl.Contracts.Events;

public record UrlClickedEvent(
    Guid UrlId,
    string ShortCode,
    string? UserAgent,
    string? IpAddress,
    DateTimeOffset ClickedAt
);
