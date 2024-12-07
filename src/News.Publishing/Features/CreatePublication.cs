using System.Collections.Immutable;
using Marten;
using News.Publishing.Publication;
using News.Publishing.Videos;
using PublicationAggregate = News.Publishing.Publication.Publication;
using VideoAddedToPublication = News.Publishing.Publication.VideoAddedToPublication;

namespace News.Publishing.Features;

public delegate PublicationCreated CreatePublicationDelegate(CreatePublication command);

public record CreatePublication(
    Guid StreamId,
    string PublicationId,
    string Title,
    string Synopsis,
    IReadOnlyList<Article>? Articles,
    IReadOnlyList<(Guid ArticleId, IEnumerable<Video.Created> Videos)>? ArticleVideos,
    IReadOnlyList<Video.Created>? Videos,
    DateTimeOffset CreatedAt,
    DateTimeOffset Now
);

public static partial class PublicationService
{
    public static readonly CreatePublicationDelegate Create = command =>
    {
        var (streamId, publicationId, title, synopsis, articles, _, videos, createdAt, now) = command;
        var videoIds = videos?.Select(v => v.VideoId).ToList();

        return new PublicationCreated(streamId, publicationId, title, synopsis, articles, videoIds, createdAt, now);
    };

    public static async Task CreatePublication(
        this IDocumentSession session,
        CreatePublication command,
        CancellationToken ct)
    {
        var combinedVideoIds = GetCombinedVideoIds(command);
        var existingVideos = (await session.GetExistingVideos(combinedVideoIds, ct)).ToImmutableList();
        var missingVideoIds = GetMissingVideoIds(combinedVideoIds, existingVideos);

        session.CreatePublicationWithVideos(command, existingVideos);
        session.CreateMissingVideos(command, missingVideoIds);

        await session.SaveChangesAsync(token: ct);
    }

    private static HashSet<string> GetCombinedVideoIds(CreatePublication command)
    {
        var (_, _, _, _, articles, _, videos, _, _) = command;

        return (videos?.Select(v => v.VideoId) ?? [])
            .Union(articles?.SelectMany(a => a.VideoIds) ?? [])
            .ToHashSet();
    }

    private static async Task<IReadOnlyList<VideoDetails>> GetExistingVideos(this IDocumentSession session,
        HashSet<string> combinedVideoIds, CancellationToken ct)
    {
        return await session.Query<VideoDetails>()
            .Where(x => combinedVideoIds.Contains(x.VideoId))
            .ToListAsync(ct);
    }

    private static IEnumerable<string> GetMissingVideoIds(HashSet<string> combinedVideoIds,
        IReadOnlyList<VideoDetails> existingVideos)
    {
        var existingVideoIdsSet = existingVideos.Select(v => v.VideoId).ToHashSet();
        return combinedVideoIds.Where(id => !existingVideoIdsSet.Contains(id));
    }

    private static void CreatePublicationWithVideos(
        this IDocumentSession session,
        CreatePublication command,
        IEnumerable<VideoDetails> existingVideos)
    {
        var (streamId, _, _, _, _, _, _, _, now) = command;

        var createdEvent = Create(command);
        var initialPublication = PublicationAggregate.Create(createdEvent);

        var (_, videoAddedToPublicationEvents) = existingVideos
            .Select(video => new LinkVideoToPublication(streamId, video.Id, video.VideoId, now))
            .Aggregate(
                (Publication: initialPublication, Events: new List<VideoAddedToPublication>()),
                (acc, linkVideoCommand) =>
                {
                    var @event = LinkVideo(acc.Publication, linkVideoCommand);
                    var newPublication = acc.Publication.Apply(@event);
                    acc.Events.Add(@event);
                    return (newPublication, acc.Events);
                });

        session.Events.StartStream<PublicationAggregate>(streamId, [createdEvent, ..videoAddedToPublicationEvents]);
    }

    private static void CreateMissingVideos(
        this IDocumentSession session,
        CreatePublication command,
        IEnumerable<string> missingVideoIds)
    {
        var (streamId, publicationId, _, _, _, articleVideos, videos, _, now) = command;

        var allVideos = (articleVideos?
            .SelectMany(a => a.Videos) ?? [])
            .Union(videos?.Select(v => v) ?? []);

        var videoCreatedEvents = missingVideoIds
            .Select(videoId =>
                allVideos?.FirstOrDefault(video =>
                        video.VideoId.Equals(videoId, StringComparison.InvariantCultureIgnoreCase)) switch
                    {
                        { } video => new VideoEvent.VideoCreated(video.Id, video.VideoId, streamId,
                            publicationId, video.MediaType, video.Origin, video.Url, video.CreatedAt, now),
                        _ => null
                    })
            .OfType<VideoEvent.VideoCreated>()
            .ToList();

        videoCreatedEvents.ForEach(video => session.Events.StartStream<Video>(video.Id, video));
    }
}
