using News.Publishing.Publication;

namespace News.Publishing.Features;

public delegate ArticleRemovedFromPublication UnlinkArticleDelegate(Publication.Publication current, UnlinkArticleFromPublication command);
public record UnlinkArticleFromPublication(
    Guid StreamId,
    Guid ArticleId,
    DateTimeOffset UnLinkedAt
);

public static partial class PublicationService
{
    public static readonly UnlinkArticleDelegate UnlinkArticle = (current, command) =>
    {
        if (current is not { Status : PublicationStatus.Pending })
            throw new InvalidOperationException(
                $"Can't modify publication `{command.StreamId}`. It is not in a pending state");

        var (streamId, articleId, now) = command;

        if (current.Articles?.SingleOrDefault(a => a.ArticleId == articleId) is null)
            throw new InvalidOperationException($"The article with id `{articleId}` was not found");

        return new ArticleRemovedFromPublication(streamId, articleId, now);
    };
}
