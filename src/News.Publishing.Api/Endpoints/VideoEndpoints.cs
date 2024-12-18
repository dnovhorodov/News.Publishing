using Marten;
using Marten.Schema.Identity;
using Microsoft.AspNetCore.Mvc;
using News.Publishing.Features;
using News.Publishing.Publication;
using News.Publishing.Videos;
using static System.DateTimeOffset;
using static Microsoft.AspNetCore.Http.TypedResults;
using static News.Publishing.Features.PublicationService;

namespace News.Publishing.Api.Endpoints;

public static class VideoEndpoints
{
    public static void MapVideoEndpoints(this RouteGroupBuilder videoGroup)
    {
        videoGroup.MapPost("",
            async (
                [FromServices] IDocumentSession documentSession,
                CreateVideoRequest body,
                CancellationToken ct
            ) =>
            {
                if (await documentSession.Query<VideoDetails>().AnyAsync(v => v.VideoId == body.VideoId, ct))
                    return Results.Conflict($"Video with id {body.VideoId} already exists");

                var publication = await documentSession.Query<PublicationDetails>()
                    .Where(x => x.PublicationId == body.PublicationId)
                    .SingleOrDefaultAsync(ct);

                if (publication is null)
                    return Results.NotFound($"Publication with id {body.PublicationId} not found");

                var streamId = CombGuidIdGeneration.NewGuid();

                documentSession.Events.StartStream<Video>(streamId, VideoService.Create(
                    new CreateVideo(streamId, body.VideoId, publication.Id, body.PublicationId, body.MediaType, body.Origin,
                        body.Url, body.CreatedAt, Now)));

                await documentSession.Events.WriteToAggregate<Publication.Publication>(publication.Id, stream =>
                {
                    stream.AppendOne(LinkVideo(stream.Aggregate,
                        new LinkVideoToPublication(stream.Id, streamId, body.VideoId, Now)));
                }, ct);

                await documentSession.SaveChangesAsync();

                return Created($"/api/videos/{body.VideoId}", body.VideoId);
            }
        );

        videoGroup.MapGet("{videoId}",
            async (HttpContext context, [FromServices] IQuerySession querySession, string videoId) =>
            {
                var video = await querySession.Query<VideoDetails>().Where(v => v.VideoId == videoId)
                    .SingleOrDefaultAsync();

                return video is not null ? Results.Ok(video) : Results.NotFound();
            }
        );
    }
}

public record CreateVideoRequest(
    string VideoId,
    string PublicationId,
    string MediaType,
    VideoOrigin Origin,
    string Url,
    DateTimeOffset CreatedAt) : VideoRequest(VideoId, MediaType, Origin, Url, CreatedAt);
