namespace News.Publishing.Videos;

using static VideoEvent;

public record Video
{
    public record Created(
        Guid Id,
        string VideoId,
        string MediaType,
        VideoOrigin Origin,
        string Url,
        DateTimeOffset CreatedAt) : Video(Id, VideoId);

    public record Ingested(
        Guid Id,
        string VideoId,
        DateTimeOffset IngestedAt) : Video(Id, VideoId);

    public Guid Id { get; init; } = default!;
    public string VideoId { get; init; }
    public int Version { get; set; }
    public IReadOnlySet<string>? PublicationIds { get; init; } = new HashSet<string>();

    public Video Apply(VideoEvent @event) =>
        (this, @event) switch
        {
            (_, VideoCreated videoCreated) =>
                new Created(
                    videoCreated.Id,
                    videoCreated.VideoId,
                    videoCreated.MediaType,
                    videoCreated.Origin,
                    videoCreated.Url,
                    videoCreated.VideoCreatedAt)
                {
                    PublicationIds = new HashSet<string> { videoCreated.PublicationId }
                },
            
            (Created _, VideoIngested videoIngested) => 
                new Ingested(videoIngested.Id, videoIngested.VideoId, videoIngested.IngestedAt),

            _ => this
        };

    private Video(Guid id, string videoId)
    {
        Id = id;
        VideoId = videoId;
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private Video() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}

public enum VideoOrigin { S3, AzureBlob }

public abstract record VideoEvent
{
    public record VideoCreated(
        Guid Id,
        string VideoId,
        Guid PublicationStreamId,
        string PublicationId,
        string MediaType,
        VideoOrigin Origin,
        string Url,
        DateTimeOffset VideoCreatedAt,
        DateTimeOffset CreatedAt
    ) : VideoEvent;

    public record VideoIngested(
        Guid Id,
        string VideoId,
        DateTimeOffset IngestedAt
    ) : VideoEvent;

    public record VideoRevoked(
        Guid Id,
        string VideoId,
        string Reason,
        DateTimeOffset RevokedAt
    ) : VideoEvent;

    private VideoEvent() { }
}
