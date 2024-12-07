using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using News.Publishing.Videos;
using static News.Publishing.Publication.PublicationMediaPlatform;
using static News.Publishing.Videos.VideoEvent;

namespace News.Publishing.Publication;

public record PublicationVideos
{
    public required Guid Id { get; init; }
    public required string PublicationId { get; init; }
    public string? Title { get; set; }
    public HashSet<string>? RemainingVideoIds { get; set; }
    public HashSet<IngestedVideo>? IngestedVideos { get; set; }
    public DateTimeOffset? PublishRequestedAt { get; set; }
    public bool PublishRequested => PublishRequestedAt.HasValue;
}

public record IngestedVideo(string VideoId, DateTimeOffset IngestedAt);

public class PublicationVideosProjection : MultiStreamProjection<PublicationVideos, Guid>
{
    public PublicationVideosProjection()
    {
        Identity<PublicationCreated>(e => e.Id);
        Identity<VideoAddedToPublication>(e => e.Id);
        Identity<VideoRemovedFromPublication>(e => e.Id);
        Identity<VideoCreated>(e => e.PublicationStreamId);
        Identity<PublishRequested>(e => e.Id);

        CustomGrouping(new VideoIngestedEventGrouper());
    }

    public static PublicationVideos Create(PublicationCreated created) =>
        new()
        {
            Id = created.Id,
            PublicationId = created.PublicationId,
            Title = created.Title,
            RemainingVideoIds = new HashSet<string>(GetAllVideoIds(created)),
            IngestedVideos = new HashSet<IngestedVideo>(),
            PublishRequestedAt = default
        };

    public void Apply(VideoAddedToPublication videoAdded, PublicationVideos current)
        => current.RemainingVideoIds?.Add(videoAdded.VideoId);

    public void Apply(VideoRemovedFromPublication videoRemoved, PublicationVideos current)
        => current.RemainingVideoIds?.Remove(videoRemoved.VideoId);

    public void Apply(PublishRequested publishRequested, PublicationVideos current)
        => current.PublishRequestedAt = publishRequested.When;

    public void Apply(VideoCreated videoCreated, PublicationVideos current)
        => current.RemainingVideoIds?.Add(videoCreated.VideoId);

    public void Apply(VideoIngested videoIngested, PublicationVideos current)
    {
        current.RemainingVideoIds?.Remove(videoIngested.VideoId);
        current.IngestedVideos?.Add(new(videoIngested.VideoId, videoIngested.IngestedAt));
    }

    private static HashSet<string> GetAllVideoIds(PublicationCreated created)
    {
        return (created?.VideoIds?.Select(id => id) ?? [])
            .Union(created?.Articles?.SelectMany(a => a.VideoIds) ?? [])
            .ToHashSet();
    }

    public override ValueTask RaiseSideEffects(IDocumentOperations operations, IEventSlice<PublicationVideos> slice)
    {
        var publicationVideos = slice.Aggregate;

        if (publicationVideos is
                { RemainingVideoIds: { Count: 0 }, IngestedVideos: { Count: > 0 }, PublishRequested: true } &&
            slice.Events().Any(e => e.Data is VideoIngested or PublishRequested))
        {
            var publicationReady = new IntegrationEvent.PublicationReady(
                new Uri($"https://news.local.dev/publishing/api/publications/{publicationVideos.PublicationId}")
                    .ToString());

            slice.PublishMessage(publicationReady);
        }

        return new ValueTask();
    }
}

public class VideoIngestedEventGrouper : IAggregateGrouper<Guid>
{
    public async Task Group(IQuerySession session, IEnumerable<IEvent> events, ITenantSliceGroup<Guid> grouping)
    {
        var videoIngestedEvents = events
            .OfType<IEvent<VideoIngested>>()
            .ToList();

        if (!videoIngestedEvents.Any())
            return;

        var videoStreamIds = videoIngestedEvents
            .Select(e => e.Data.Id)
            .ToList();

        var videoDetails = await session.LoadManyAsync<VideoDetails>(videoIngestedEvents
            .Select(e => e.Data.Id));

        var streamIds = videoDetails
            .Where(video => video.Publications is not null)
            .SelectMany(video =>
                video.Publications?.Keys.Select(pubKey => (VideoId: video.Id, PublicationKey: pubKey)) ?? [])
            .GroupBy(pair => pair.VideoId, pair => pair.PublicationKey)
            .ToDictionary(g => g.Key, g => g.ToList());

        grouping.AddEvents<VideoIngested>(
            e => streamIds.TryGetValue(e.Id, out var pubKeys) ? pubKeys : Enumerable.Empty<Guid>(),
            videoIngestedEvents
        );
    }
}
