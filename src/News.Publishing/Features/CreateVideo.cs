using News.Publishing.Videos;

namespace News.Publishing.Features;

using static VideoEvent;

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
    public static VideoCreated Create(CreateVideo command)
    {
        var (streamId, videoId, publicationStreamId, publicationId, type, origin, url, createdAt, now) = command;

        return new VideoCreated(streamId, videoId, publicationStreamId, publicationId, type, origin, url, createdAt, now);
    }
}
