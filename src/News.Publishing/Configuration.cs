using Marten;
using Marten.Events.Projections;
using News.Publishing.Publication;
using News.Publishing.Videos;

namespace News.Publishing;

public static class Configuration
{
    public static StoreOptions ConfigurePublications(this StoreOptions options)
    {
        options.Projections.LiveStreamAggregation<Video>();
        options.Projections.LiveStreamAggregation<Publication.Publication>();
        options.Projections.Add<PublicationDetailsProjection>(ProjectionLifecycle.Inline);
        options.Projections.Add<VideoDetailsProjection>(ProjectionLifecycle.Async);
        options.Projections.Add<PublicationVideosProjection>(ProjectionLifecycle.Async);

        return options;
    }
}
