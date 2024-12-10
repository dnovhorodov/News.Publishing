using Marten.Events;
using Marten.Events.Projections;
using Marten.Schema.Identity;
using static News.Publishing.Publication.PublicationMediaPlatform;

namespace News.Publishing.Publication;

public record PublicationHistory(
    Guid Id,
    Guid StreamId,
    string Description);

public class PublicationHistoryTransformation : EventProjection
{
    public PublicationHistory Transform(IEvent<PublicationCreated> input)
    {
        var (streamId, publicationId, title, synopsis, _, _, createdAt, loggedAt) = input.Data;

        return new PublicationHistory(
            CombGuidIdGeneration.NewGuid(),
            streamId,
            $"['{loggedAt}'] Publication created at '{createdAt}' with id: '{streamId}' with external id '{publicationId}' and title '{title}' and synopsis 'synopsis'");
    }
    
    public PublicationHistory Transform(IEvent<ArticleAddedToPublication> input)
    {
        var (streamId, article, addedAt) = input.Data;

        return new PublicationHistory(
            CombGuidIdGeneration.NewGuid(),
            streamId,
            $"['{addedAt}'] Article with id: {article.ArticleId} has been added to publication with id '{streamId}' and title '{article.Title}'");
    }
    
    public PublicationHistory Transform(IEvent<VideoAddedToPublication> input)
    {
        var (streamId, videoStreamId, videoId, addedAt) = input.Data;

        return new PublicationHistory(
            CombGuidIdGeneration.NewGuid(),
            streamId,
            $"['{addedAt}'] Video with id: {videoStreamId} and external id '{videoId}' has been added to publication with id '{streamId}'");
    }
    
    public PublicationHistory Transform(IEvent<ArticleRemovedFromPublication> input)
    {
        var (streamId, articleId, removedAt) = input.Data;

        return new PublicationHistory(
            CombGuidIdGeneration.NewGuid(),
            streamId,
            $"['{removedAt}'] Article with id: {articleId} has been removed from publication with id '{streamId}'");
    }
    
    public PublicationHistory Transform(IEvent<VideoRemovedFromPublication> input)
    {
        var (streamId, videoId, removedAt) = input.Data;

        return new PublicationHistory(
            CombGuidIdGeneration.NewGuid(),
            streamId,
            $"['{removedAt}'] Video with external id {videoId} has been added to publication with id '{streamId}'");
    }
    
    public PublicationHistory Transform(IEvent<PublishRequested> input)
    {
        var (streamId, platform, when) = input.Data;

        return new PublicationHistory(
            CombGuidIdGeneration.NewGuid(),
            streamId,
            $"['{when}'] Publish action has been requested for publication with id: {streamId} for platform '{platform}'");
    }
    
    public PublicationHistory Transform(IEvent<Published> input)
    {
        var (streamId, platform, when) = input.Data;

        return new PublicationHistory(
            CombGuidIdGeneration.NewGuid(),
            streamId,
            $"['{when}'] Publication with id: {streamId} has been successfully published on platform '{platform}'");
    }
    
    public PublicationHistory Transform(IEvent<UnPublishRequested> input)
    {
        var (streamId, platform, when) = input.Data;

        return new PublicationHistory(
            CombGuidIdGeneration.NewGuid(),
            streamId,
            $"['{when}'] Unpublishing action has been requested for publication with id: {streamId} for platform '{platform}'");
    }
    
    public PublicationHistory Transform(IEvent<UnPublished> input)
    {
        var (streamId, platform, when) = input.Data;

        return new PublicationHistory(
            CombGuidIdGeneration.NewGuid(),
            streamId,
            $"['{when}'] Publication with id: {streamId} has been successfully removed from platform '{platform}'");
    }
}
