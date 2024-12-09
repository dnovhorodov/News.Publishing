using News.Publishing.Videos;

namespace News.Publishing.Features;

public record IngestVideo(
    Guid StreamId,
    string VideoId,
    DateTimeOffset IngestedAt
);

public static partial class VideoService
{
    public static VideoEvent.VideoIngested IngestVideo(Video current, IngestVideo command)
    {
        if (current is Video.Ingested)
            throw new InvalidOperationException(
                $"Video `{command.VideoId}` was already ingested.");
        
        var (streamId, videoId, ingestedAt) = command;

        return new VideoEvent.VideoIngested(streamId, videoId, ingestedAt);
    }
}
