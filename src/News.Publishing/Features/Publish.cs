using News.Publishing.Publication;
using static News.Publishing.Publication.MediaPlatformAction;
using static News.Publishing.Publication.MediaPlatformPublicationStatus;
using static News.Publishing.Publication.PublicationMediaPlatform;

namespace News.Publishing.Features;

public delegate Published PublishDelegate(Publication.Publication current, Publish command);

public static partial class PublicationService
{
    public static readonly PublishDelegate Publish = (current, command) =>
    {
        var (streamId, platform, when) = command;

        var (lastStatus, _) = (current.Publications.TryGetValue(platform, out var statuses)
            ? statuses.LastOrDefault()
            : default)!;

        return lastStatus switch
        {
            None => throw new InvalidOperationException(
                $"Publication request for platform `{platform}` not found"),
            PublishingInProgress => throw new InvalidOperationException(
                $"Publication for platform `{platform}` is already in progress"),
            MediaPlatformPublicationStatus.PublishRequested or MediaPlatformPublicationStatus.UnPublished =>
                new Published(streamId, platform, when),
            _ => throw new InvalidOperationException(
                $"Publication for platform `{platform}` has invalid status `{lastStatus}`"),
        };
    };
}
