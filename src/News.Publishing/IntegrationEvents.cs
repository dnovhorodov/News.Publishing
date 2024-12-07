using News.Publishing.Videos;

namespace News.Publishing;

public abstract record IntegrationEvent
{
    public record VideoUploaded(
        string VideoId,
        string MediaType,
        VideoOrigin Origin,
        string Url,
        DateTimeOffset Timestamp) : IntegrationEvent();

    public record VideoIngested(
        string VideoId,
        DateTimeOffset Timestamp
    ) : IntegrationEvent;

    public record PublicationReady(string PublicationUrl) : IntegrationEvent;

    public record VideoRevoked(string VideoId) : IntegrationEvent;

    internal IntegrationEvent() { }
}
