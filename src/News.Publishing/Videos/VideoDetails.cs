using System.Collections.Immutable;
using Marten;
using Marten.Events.Projections;
using News.Publishing.Publication;
using static News.Publishing.Videos.VideoEvent;

namespace News.Publishing.Videos;

public record VideoDetails
{
    public required Guid Id { get; set; }
    public required string VideoId { get; set; }
    public required string MediaType { get; set; }
    public VideoOrigin Origin { get; set; }
    public required string Url { get; set; }
    public ImmutableDictionary<Guid, PublicationInfo>? Publications { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? IngestedAt { get; set; }
    public bool IsIngested => IngestedAt.HasValue;
}

public record PublicationInfo(
    string PublicationId,
    string Title,
    DateTimeOffset AddedAt
);

public class VideoDetailsProjection : MultiStreamProjection<VideoDetails, Guid>
{
    public VideoDetailsProjection()
    {
        Identity<VideoCreated>(e => e.Id);
        Identity<VideoIngested>(e => e.Id);
        Identity<VideoAddedToPublication>(e => e.VideoStreamId);
    }

    public async Task Apply(VideoDetails video, VideoCreated created, IQuerySession session)
    {
        var result = await GetPublicationInfo(session, created.PublicationStreamId);
        var (publicationId, title, addedAt) = (created.PublicationId, result?.Title, created.CreatedAt);

        video.Id = created.Id;
        video.VideoId = created.VideoId;
        video.MediaType = created.MediaType;
        video.Origin = created.Origin;
        video.Url = created.Url;
        video.CreatedAt = created.CreatedAt;
        video.Publications = result is null
            ? ImmutableDictionary<Guid, PublicationInfo>.Empty
            : ImmutableDictionary<Guid, PublicationInfo>.Empty
                .Add(created.PublicationStreamId, new PublicationInfo(publicationId, title ?? string.Empty, addedAt));
    }

    public void Apply(VideoIngested ingested, VideoDetails video) => video.IngestedAt = ingested.IngestedAt;

    public async Task<VideoDetails> Apply(VideoAddedToPublication videoAddedToPublication, VideoDetails current,
        IQuerySession session)
    {
        var result = await GetPublicationInfo(session, videoAddedToPublication.Id);
        var (publicationId, title, addedAt) = (result?.PublicationId, result?.Title, videoAddedToPublication.AddedAt);

        return current with
        {
            Publications = current.Publications?.TryGetValue(videoAddedToPublication.Id, out _) switch
            {
                true when result is not null => current.Publications.SetItem(
                    videoAddedToPublication.Id,
                    new PublicationInfo(publicationId!, title ?? string.Empty, addedAt)),
                false when result is not null => current.Publications.Add(
                    videoAddedToPublication.Id,
                    new PublicationInfo(publicationId!, title ?? string.Empty, addedAt)),
                _ => current.Publications,
            }
        };
    }

    public override ValueTask RaiseSideEffects(IDocumentOperations operations, IEventSlice<VideoDetails> slice)
    {
        if (slice.Aggregate is not null && slice
                .Events()
                .Any(e => e.Data is VideoCreated))
        {
            var video = slice.Aggregate;
            var videoUploaded =
                new IntegrationEvent.VideoUploaded(video!.VideoId, video.MediaType, video.Origin, video.Url,
                    video.CreatedAt);

            slice.PublishMessage(videoUploaded);
        }

        return new ValueTask();
    }

    private static async Task<PublicationInfo?> GetPublicationInfo(IQuerySession session, Guid streamId) =>
        await session.Events.QueryRawEventDataOnly<PublicationCreated>()
            .Where(e => e.Id == streamId)
            .Select(p => new PublicationInfo(p.PublicationId, p.Title, p.CreatedAt))
            .SingleOrDefaultAsync();
}
