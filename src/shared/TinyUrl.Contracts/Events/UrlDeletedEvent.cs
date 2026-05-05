namespace TinyUrl.Contracts.Events;

public record UrlDeletedEvent(
    Guid UrlId,
    string ShortCode,
    DateTimeOffset OccurredAt
);
