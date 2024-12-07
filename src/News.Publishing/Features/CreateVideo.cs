using News.Publishing.Videos;

namespace News.Publishing.Features;

using static VideoEvent;

public delegate VideoCreated CreateVideoDelegate(CreateVideo command);
public record CreateVideo(
    Guid StreamId,
    string VideoId,
    Guid PublicationStreamId,
    string PublicationId,
    string Type,
    VideoOrigin Origin,
    string Url,
    DateTimeOffset VideoCreatedAt,
    DateTimeOffset CreatedAt);

public static partial class VideoService
{
    public static readonly CreateVideoDelegate Create = command =>
    {
        var (streamId, videoId, publicationStreamId, publicationId, type, origin, url, createdAt, now) = command;

        return new VideoCreated(streamId, videoId, publicationStreamId, publicationId, type, origin, url, createdAt, now);
    };
}
