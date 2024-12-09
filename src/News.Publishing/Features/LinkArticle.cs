using News.Publishing.Publication;

namespace News.Publishing.Features;

public record LinkArticleToPublication(
    Guid StreamId,
    Article Article,
    DateTimeOffset LinkedAt
);

public static partial class PublicationService
{
    public static ArticleAddedToPublication LinkArticle(Publication.Publication current, LinkArticleToPublication command)
    {
        if (current is not { Status : PublicationStatus.Pending })
            throw new InvalidOperationException(
                $"Can't modify publication `{command.StreamId}`. It is not in a pending state");

        var (streamId, article, now) = command;

        return new ArticleAddedToPublication(streamId, article, now);
    }
}
