using News.Publishing.Publication;
using static News.Publishing.Publication.MediaPlatformAction;
using static News.Publishing.Publication.MediaPlatformPublicationStatus;
using static News.Publishing.Publication.PublicationMediaPlatform;

namespace News.Publishing.Features;

public static partial class PublicationService
{
    public static PublishRequested PublishRequest(Publication.Publication current, PublishRequest command)
    {
        if (current is { Status : PublicationStatus.PublishedAndClosed })
            throw new InvalidOperationException(
                $"Publication `{command.StreamId}` already published and closed. Request not allowed");

        var (streamId, toPlatform, when) = command;

        return (current.Publications.TryGetValue(toPlatform, out var publications), publications) switch
        {
            (false, _) => new PublishRequested(streamId, toPlatform, when),
            (true, [.., { Status: MediaPlatformPublicationStatus.PublishRequested, When: var at }]) =>
                throw new InvalidOperationException(
                    $"Publish already has been requested for platform `{toPlatform}` at `{at}`"),
            (true, [.., { Status: MediaPlatformPublicationStatus.Published, When: var at }]) =>
                throw new InvalidOperationException(
                    $"Publication already has been published for platform `{toPlatform}` at `{at}`"),
            (true, [.., { Status: PublishingInProgress }]) =>
                throw new InvalidOperationException($"Publication for platform `{toPlatform}` in progress"),
            (_, _) => new PublishRequested(streamId, toPlatform, when),
        };
    }
}
