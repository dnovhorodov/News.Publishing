using News.Publishing.Publication;

namespace News.Publishing.Features;

public record UnlinkArticleFromPublication(
    Guid StreamId,
    Guid ArticleId,
    DateTimeOffset UnLinkedAt
);

public static partial class PublicationService
{
    public static ArticleRemovedFromPublication UnlinkArticle(Publication.Publication current, UnlinkArticleFromPublication command)
    {
        if (current is not { Status : PublicationStatus.Pending })
            throw new InvalidOperationException(
                $"Can't modify publication `{command.StreamId}`. It is not in a pending state");

        var (streamId, articleId, now) = command;

        if (current.Articles?.SingleOrDefault(a => a.ArticleId == articleId) is null)
            throw new InvalidOperationException($"The article with id `{articleId}` was not found");

        return new ArticleRemovedFromPublication(streamId, articleId, now);
    }
}
