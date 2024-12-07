using System.Collections.Immutable;

namespace News.Publishing.Publication;

public static class PublicationExtensions
{
    public static ImmutableDictionary<MediaPlatform, List<PublicationRecord>>
        AppendStatus(
            this ImmutableDictionary<MediaPlatform, List<PublicationRecord>>
                publications,
            MediaPlatform platform,
            MediaPlatformPublicationStatus status,
            DateTimeOffset when)
    {
        var updatedList = publications.TryGetValue(platform, out var existingList)
            ? existingList
            : new List<PublicationRecord>();

        updatedList.Add(new PublicationRecord(status, when));

        return publications.SetItem(platform, updatedList);
    }
}
