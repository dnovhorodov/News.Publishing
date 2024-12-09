using News.Publishing.Publication;

namespace News.Publishing.Features;

public record UnlinkVideoFromPublication(
    Guid StreamId,
    string VideoId,
    DateTimeOffset UnlinkedAt
);

public static partial class PublicationService
{
    public static VideoRemovedFromPublication UnlinkVideo(Publication.Publication current, UnlinkVideoFromPublication command)
    {
        if (current is not { Status : PublicationStatus.Pending })
            throw new InvalidOperationException(
                $"Can't modify publication `{command.StreamId}`. It is not in a pending state");

        var (streamId, videoId, now) = command;

        if (current.VideoIds?.SingleOrDefault(id =>
                string.Equals(id, videoId, StringComparison.InvariantCultureIgnoreCase)) is null)
            throw new InvalidOperationException($"The video with id `{videoId}` was not found");

        return new VideoRemovedFromPublication(streamId, videoId, now);
    }
}
