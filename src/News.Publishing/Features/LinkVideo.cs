﻿using News.Publishing.Publication;

namespace News.Publishing.Features;

public record LinkVideoToPublication(
    Guid StreamId,
    Guid VideoStreamId,
    string VideoId,
    DateTimeOffset LinkedAt
);

public static partial class PublicationService
{
    public static VideoAddedToPublication LinkVideo(Publication.Publication current, LinkVideoToPublication command)
    {
        if (current is not { Status : PublicationStatus.Pending })
            throw new InvalidOperationException(
                $"Can't modify publication `{command.StreamId}`. It is not in a pending state");

        var (streamId, videoStreamId, videoId, now) = command;

        return new VideoAddedToPublication(streamId, videoStreamId, videoId, now);
    }
}


