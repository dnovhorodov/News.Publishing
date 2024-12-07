using News.Publishing.Publication;
using static News.Publishing.Publication.MediaPlatformAction;
using static News.Publishing.Publication.MediaPlatformPublicationStatus;
using static News.Publishing.Publication.PublicationMediaPlatform;

namespace News.Publishing.Features;

public delegate UnPublished UnPublishDelegate(Publication.Publication current, UnPublish command);

public static partial class PublicationService
{
    public static readonly UnPublishDelegate UnPublish = (current, command) =>
    {
        var (streamId, platform, when) = command;

        var (lastStatus, _) = (current.Publications.TryGetValue(platform, out var statuses)
            ? statuses.LastOrDefault()
            : default)!;

        return lastStatus switch
        {
            None => throw new InvalidOperationException(
                $"Publication for platform `{platform}` is not published"),
            PublishingInProgress => throw new InvalidOperationException(
                $"Publication for platform `{platform}` is in publishing progress"),
            MediaPlatformPublicationStatus.Published => new UnPublished(streamId,
                platform, when),
            _ => throw new InvalidOperationException(
                $"Publication for platform `{platform}` has invalid status `{lastStatus}`"),
        };
    };
}
