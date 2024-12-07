namespace News.Publishing.Publication;

public abstract record MediaPlatformAction
{
    public record PublishRequest(Guid StreamId, MediaPlatform ToPlatform, DateTimeOffset When)
        : MediaPlatformAction(StreamId, ToPlatform, When);

    public record Publish(Guid StreamId, MediaPlatform ToPlatform, DateTimeOffset When)
        : MediaPlatformAction(StreamId, ToPlatform, When);

    public record UnPublishRequest(Guid StreamId, MediaPlatform FromPlatform, DateTimeOffset When)
        : MediaPlatformAction(StreamId, FromPlatform, When);

    public record UnPublish(Guid StreamId, MediaPlatform FromPlatform, DateTimeOffset When)
        : MediaPlatformAction(StreamId, FromPlatform, When);

    public Guid StreamId { get; init; }
    public MediaPlatform Platform { get; init; }
    public DateTimeOffset When { get; init; }

    private MediaPlatformAction(Guid streamId, MediaPlatform platform, DateTimeOffset when)
    {
        StreamId = streamId;
        Platform = platform;
        When = when;
    }
}
