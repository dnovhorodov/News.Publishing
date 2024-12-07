using System.Collections.Immutable;
using static News.Publishing.Publication.PublicationMediaPlatform;

namespace News.Publishing.Publication;

public record Publication(
    Guid Id,
    string PublicationId,
    string Title,
    string Synopsis,
    IReadOnlyList<Article>? Articles,
    IReadOnlySet<string>? VideoIds,
    DateTimeOffset CreatedAt
)
{
    public required PublicationStatus Status { get; init; }
    public required PublicationType OfKind { get; init; }

    public ImmutableDictionary<MediaPlatform, List<PublicationRecord>> Publications
    {
        get;
        init;
    } = ImmutableDictionary<MediaPlatform, List<PublicationRecord>>.Empty;

    public static Publication Create(PublicationCreated @event)
        => new Publication(
            @event.Id,
            @event.PublicationId,
            @event.Title,
            @event.Synopsis,
            @event.Articles ?? [],
            new HashSet<string>(@event.VideoIds ?? []),
            @event.PublicationCreatedAt)
        {
            Status = PublicationStatus.Pending, OfKind = EvaluateType(@event.Articles, @event.VideoIds),
        };

    public Publication Apply(ArticleAddedToPublication @event) =>
        this with
        {
            Articles = Articles!.Any(a => a.ArticleId == @event.Article.ArticleId)
                ? Articles
                : Articles!.Append(@event.Article).ToList(),
            OfKind = EvaluateType(this),
        };

    public Publication Apply(ArticleRemovedFromPublication @event) =>
        this with
        {
            Articles = Articles!.Where(a => a.ArticleId != @event.ArticleId).ToList(), OfKind = EvaluateType(this),
        };

    public Publication Apply(VideoAddedToPublication @event) =>
        this with
        {
            VideoIds = VideoIds!.Contains(@event.VideoId)
                ? VideoIds
                : new HashSet<string>(VideoIds) { @event.VideoId },
            OfKind = EvaluateType(this),
        };

    public Publication Apply(VideoRemovedFromPublication @event) =>
        this with
        {
            VideoIds = new HashSet<string>(VideoIds!.Where(id => id != @event.VideoId)),
            OfKind = EvaluateType(this),
        };

    public Publication Apply(PublishRequested @event) =>
        this with
        {
            Publications = Publications.AppendStatus(
                @event.ToPlatform, MediaPlatformPublicationStatus.PublishRequested, @event.When),
        };

    public Publication Apply(UnPublishRequested @event) =>
        this with
        {
            Publications = Publications.AppendStatus(
                @event.FromPlatform, MediaPlatformPublicationStatus.UnPublishRequested, @event.When),
        };

    public Publication Apply(Published @event) =>
        this with
        {
            Publications = Publications.AppendStatus(
                @event.ToPlatform, MediaPlatformPublicationStatus.Published, @event.When),
            Status = EvaluateStatus(this),
        };

    public Publication Apply(UnPublished @event) =>
        this with
        {
            Publications = Publications.AppendStatus(
                @event.FromPlatform, MediaPlatformPublicationStatus.UnPublished, @event.When),
            Status = EvaluateStatus(this),
        };

    private static PublicationStatus EvaluateStatus(Publication publication)
    {
        var allPublished = publication.Publications
            .Select(p => p.Value.LastOrDefault()?.Status)
            .All(status => status == MediaPlatformPublicationStatus.Published);

        return allPublished ? PublicationStatus.PublishedAndClosed : PublicationStatus.Pending;
    }

    private static PublicationType EvaluateType(Publication publication)
        => EvaluateType(publication.Articles, publication.VideoIds);

    internal static PublicationType EvaluateType(IReadOnlyList<Article>? articles, IReadOnlyCollection<string>? videos)
        => (articles, videos) switch
        {
            { articles.Count: > 0 } when videos is { Count: > 0 } || articles.Any(article => article.VideoIds.Count > 0)
                =>
                PublicationType.Mixed,
            { articles.Count: > 0, videos: null or { Count: 0 } } => PublicationType.Article,
            { articles: null or { Count: 0 }, videos: { Count: > 0 } } => PublicationType.Video,
            _ => throw new InvalidOperationException("Can't evaluate permitted publication type"),
        };
}

public enum PublicationType
{
    Video,
    Article,
    Mixed,
}

public enum PublicationStatus
{
    Pending,
    PublishedAndClosed,
}

public enum MediaPlatformPublicationStatus
{
    None,
    PublishRequested,
    UnPublishRequested,
    PublishingInProgress,
    Published,
    UnPublished,
}

public enum MediaPlatform
{
    BbcNews,
    Netflix,
    Youtube,
}

public record PublicationRecord(MediaPlatformPublicationStatus Status, DateTimeOffset When);

public record Article(
    Guid ArticleId,
    string Title,
    string Text,
    DateTimeOffset CreatedAt
)
{
    public IReadOnlyList<string> VideoIds { get; init; } = [];
}
