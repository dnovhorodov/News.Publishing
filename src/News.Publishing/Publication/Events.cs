namespace News.Publishing.Publication;

public record PublicationCreated(
    Guid Id,
    string PublicationId,
    string Title,
    string Synopsis,
    IReadOnlyList<Article>? Articles,
    IReadOnlyList<string>? VideoIds,
    DateTimeOffset PublicationCreatedAt,
    DateTimeOffset CreatedAt
);

public record ArticleAddedToPublication(
    Guid Id,
    Article Article,
    DateTimeOffset AddedAt
);

public record ArticleRemovedFromPublication(
    Guid Id,
    Guid ArticleId,
    DateTimeOffset RemovedAt
);

public record VideoAddedToPublication(
    Guid Id,
    Guid VideoStreamId,
    string VideoId,
    DateTimeOffset AddedAt
);

public record VideoRemovedFromPublication(
    Guid Id,
    string VideoId,
    DateTimeOffset RemovedAt
);

public abstract record PublicationMediaPlatform
{
    public record PublishRequested(Guid Id, MediaPlatform ToPlatform, DateTimeOffset When)
        : PublicationMediaPlatform(Id, When);

    public record Published(Guid Id, MediaPlatform ToPlatform, DateTimeOffset When)
        : PublicationMediaPlatform(Id, When);

    public record UnPublishRequested(Guid Id, MediaPlatform FromPlatform, DateTimeOffset When)
        : PublicationMediaPlatform(Id, When);

    public record UnPublished(Guid Id, MediaPlatform FromPlatform, DateTimeOffset When)
        : PublicationMediaPlatform(Id, When);

    public Guid Id { get; init; }
    public DateTimeOffset When { get; init; }

    private PublicationMediaPlatform(Guid id, DateTimeOffset when)
    {
        Id = id;
        When = when;
    }
}
