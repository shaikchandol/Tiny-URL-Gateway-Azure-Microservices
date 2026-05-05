namespace TinyUrl.Contracts.Events;

public record UrlCreatedEvent(
    Guid UrlId,
    string ShortCode,
    string LongUrl,
    string? CustomAlias,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset OccurredAt
);
