using News.Publishing.Videos;

namespace News.Publishing.Features;

public delegate VideoEvent.VideoIngested VideoIngestedDelegate(Video current, IngestVideo command);

public record IngestVideo(
    Guid StreamId,
    string VideoId,
    DateTimeOffset IngestedAt
);

public static partial class VideoService
{
    public static readonly VideoIngestedDelegate IngestVideo = (current, command) =>
    {
        if (current is Video.Ingested)
            throw new InvalidOperationException(
                $"Video `{command.VideoId}` was already ingested.");
        
        var (streamId, videoId, ingestedAt) = command;

        return new VideoEvent.VideoIngested(streamId, videoId, ingestedAt);
    };
}
