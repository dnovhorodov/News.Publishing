using Marten;
using MassTransit;
using News.Publishing.Features;
using News.Publishing.Videos;
using static News.Publishing.IntegrationEvent;

namespace News.Publishing.Api.Infrastructure.RabbitMQ;

public class VideoIngestedConsumer : IConsumer<VideoIngested>
{
    private readonly IDocumentSession _session;

    public VideoIngestedConsumer(IDocumentSession session) => _session = session;

    public async Task Consume(ConsumeContext<VideoIngested> context)
    {
        var ct = context.CancellationToken;

        var created = await _session.Events.QueryRawEventDataOnly<VideoEvent.VideoCreated>()
            .Where(v => v.VideoId == context.Message.VideoId)
            .SingleOrDefaultAsync(ct);

        if (created is null)
            return;

        await _session.Events.WriteToAggregate<Video>(created.Id, stream =>
        {
            var video = stream.Aggregate;
            stream.AppendOne(VideoService.IngestVideo(video,
                new IngestVideo(video.Id, video.VideoId, context.Message.Timestamp)));
            
        }, ct);
    }
}
