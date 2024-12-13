using System.Collections.Immutable;
using Marten.Events.Aggregation;
using static News.Publishing.Publication.PublicationMediaPlatform;

namespace News.Publishing.Publication;

public record PublicationDetails(
    Guid Id,
    string PublicationId,
    string Title,
    string Synopsis,
    IEnumerable<Article> Articles,
    IEnumerable<string> VideoIds,
    ImmutableDictionary<MediaPlatform, List<PublicationRecord>> PublicationHistory,
    DateTimeOffset PublicationCreatedAt,
    DateTimeOffset OperationAt,
    int Version = 1
);

public class PublicationDetailsProjection : SingleStreamProjection<PublicationDetails>
{
    public static PublicationDetails Create(PublicationCreated created) =>
        new(created.Id,
            created.PublicationId,
            created.Title,
            created.Synopsis,
            created.Articles ?? [],
            created.VideoIds ?? [],
            ImmutableDictionary<MediaPlatform, List<PublicationRecord>>.Empty,
            created.PublicationCreatedAt,
            created.CreatedAt);

    public PublicationDetails Apply(ArticleAddedToPublication articleAddedToPublication, PublicationDetails current) =>
        current with
        {
            Articles = current.Articles.Union(
                new[]
                {
                    new Article(
                        articleAddedToPublication.Article.ArticleId,
                        articleAddedToPublication.Article.Title,
                        articleAddedToPublication.Article.Text,
                        articleAddedToPublication.Article.CreatedAt
                    ) { VideoIds = articleAddedToPublication.Article.VideoIds }
                }).ToList()
        };

    public PublicationDetails Apply(ArticleRemovedFromPublication articleRemovedFromPublication,
        PublicationDetails current) =>
        current with
        {
            Articles = current.Articles.Where(a => a.ArticleId != articleRemovedFromPublication.ArticleId).ToList()
        };

    public PublicationDetails Apply(VideoAddedToPublication videoAddedToPublication, PublicationDetails current) =>
        current with { VideoIds = current.VideoIds.Union(new[] { videoAddedToPublication.VideoId }).ToList() };

    public PublicationDetails Apply(VideoRemovedFromPublication videoRemovedFromPublication,
        PublicationDetails current) =>
        current with { VideoIds = current.VideoIds.Where(id => id != videoRemovedFromPublication.VideoId).ToList() };

    public PublicationDetails Apply(PublishRequested publishRequested, PublicationDetails current) =>
        current with
        {
            PublicationHistory = current.PublicationHistory.AppendStatus(
                publishRequested.ToPlatform, MediaPlatformPublicationStatus.PublishRequested, publishRequested.When)
        };

    public PublicationDetails Apply(Published published, PublicationDetails current) =>
        current with
        {
            PublicationHistory = current.PublicationHistory.AppendStatus(
                published.ToPlatform, MediaPlatformPublicationStatus.Published, published.When)
        };

    public PublicationDetails Apply(UnPublished unpublished, PublicationDetails current) =>
        current with
        {
            PublicationHistory = current.PublicationHistory.AppendStatus(
                unpublished.FromPlatform, MediaPlatformPublicationStatus.UnPublished,
                unpublished.When)
        };
}
