using Marten;
using Marten.Schema.Identity;
using Microsoft.AspNetCore.Mvc;
using News.Publishing.Api.Infrastructure.Http;
using News.Publishing.Api.Infrastructure.Marten;
using News.Publishing.Features;
using News.Publishing.Publication;
using News.Publishing.Videos;
using static System.DateTimeOffset;
using static Microsoft.AspNetCore.Http.TypedResults;
using static News.Publishing.Api.Infrastructure.Http.ETagExtensions;
using static News.Publishing.Features.PublicationService;

namespace News.Publishing.Api.Endpoints;

public static class PublicationEndpoints
{
    public static void MapPublicationEndpoints(this RouteGroupBuilder publicationGroup)
    {
        publicationGroup.MapPost("",
            async (
                [FromServices] IDocumentSession documentSession,
                CreatePublicationRequest body,
                CancellationToken ct) =>
            {
                var (publicationId, title, synopsis, articleRequests, videoRequests, createdAt) = body;

                if (await documentSession.Query<PublicationDetails>()
                        .AnyAsync(p => p.PublicationId == publicationId, ct))
                {
                    return Results.Conflict($"Publication with provided id {publicationId} already exists");
                }

                var streamId = CombGuidIdGeneration.NewGuid();
                var articles =
                    articleRequests?.Select(a =>
                        new Article(a.ArticleId, a.Title, a.Text, a.CreatedAt)
                        {
                            VideoIds = (a.Videos?.Select(v => v.VideoId) ?? []).ToList()
                        }).ToList();

                var articleVideos = articleRequests?
                    .Where(article => article.Videos is not null && article.Videos.Any())
                    .Select(article => (
                        article.ArticleId,
                        Videos: article.Videos!.Select(video => new Video.Created(
                            CombGuidIdGeneration.NewGuid(),
                            video.VideoId,
                            video.MediaType,
                            video.Origin,
                            video.Url,
                            video.CreatedAt))
                    ))
                    .ToList();

                var videos = videoRequests
                    ?.Select(video =>
                        new Video.Created(CombGuidIdGeneration.NewGuid(), video.VideoId, video.MediaType, video.Origin,
                            video.Url, Now))
                    .ToList() ?? [];

                await documentSession.CreatePublication(
                    new CreatePublication(streamId, publicationId, title, synopsis, articles, articleVideos, videos,
                        createdAt,
                        Now),
                    ct);

                return Created($"/api/publications/{publicationId}", publicationId);
            }
        );

        publicationGroup.MapPost("{publicationId}/articles/",
            async (
                [FromServices] IDocumentSession documentSession,
                string publicationId,
                [FromIfMatchHeader] string eTag,
                AddArticleToPublicationRequest body,
                CancellationToken ct
            ) =>
            {
                var publication = await documentSession.Query<PublicationDetails>()
                    .Where(p => p.PublicationId == publicationId)
                    .SingleOrDefaultAsync(token: ct);

                if (publication is null)
                    return Results.NotFound($"Publication with id {publicationId} not found");

                var article = new Article(
                    body.Article.ArticleId,
                    body.Article.Title,
                    body.Article.Text,
                    body.Article.CreatedAt) { VideoIds = (body.Article.Videos ?? []).Select(v => v.VideoId).ToList(), };

                await documentSession.GetAndUpdate<Publication.Publication>(publication.Id, ToExpectedVersion(eTag),
                    state => LinkArticle(state,
                        new LinkArticleToPublication(publication.Id, article, Now)), ct);

                return Ok();
            });

        publicationGroup.MapPut("{publicationId}/videos/",
            async (
                [FromServices] IDocumentSession documentSession,
                string publicationId,
                AddVideoToPublicationRequest body,
                [FromIfMatchHeader] string eTag,
                CancellationToken ct
            ) =>
            {
                var publication = await documentSession.Query<PublicationDetails>()
                    .Where(p => p.PublicationId == publicationId)
                    .SingleOrDefaultAsync(token: ct);

                if (publication is null)
                    return Results.NotFound($"Publication with id {publicationId} not found");

                var video = await documentSession.Query<VideoDetails>().Where(x => x.VideoId == body.VideoId)
                    .SingleOrDefaultAsync(token: ct);

                if (video is null)
                    return Results.NotFound($"Video with id {body.VideoId} not found");

                await documentSession.GetAndUpdate<Publication.Publication>(publication.Id, ToExpectedVersion(eTag),
                    state => LinkVideo(state,
                        new LinkVideoToPublication(publication.Id, video.Id, body.VideoId, Now)), ct);

                return Ok();
            }
        );

        publicationGroup.MapPut("{publicationId}/publication-requests/",
            async (
                [FromServices] IDocumentSession documentSession,
                string publicationId,
                PublicationRequest body,
                [FromIfMatchHeader] string eTag,
                CancellationToken ct
            ) =>
            {
                var publication = await documentSession.Query<PublicationDetails>()
                    .Where(p => p.PublicationId == publicationId)
                    .SingleOrDefaultAsync(token: ct);

                if (publication is null)
                    return Results.NotFound($"Publication with id {publicationId} not found");

                await documentSession.GetAndUpdate<Publication.Publication>(publication.Id, ToExpectedVersion(eTag),
                    state => PublishRequest(state,
                        new MediaPlatformAction.PublishRequest(publication.Id, body.Platform, Now)), ct);

                return Ok();
            });

        publicationGroup.MapGet("{publicationId}",
            async (
                HttpContext context,
                IQuerySession querySession,
                string publicationId,
                CancellationToken ct) =>
            {
                var publication = await querySession.Query<PublicationDetails>()
                    .Where(p => p.PublicationId == publicationId)
                    .SingleOrDefaultAsync(token: ct);

                return publication is not null ? Results.Ok(publication) : Results.NotFound();
            }
        );
    }
}

public record ArticleRequest(
    Guid ArticleId,
    string Title,
    string Text,
    DateTimeOffset CreatedAt
)
{
    public IReadOnlyList<VideoRequest>? Videos { get; } = [];
}

public record VideoRequest(
    string VideoId,
    string MediaType,
    VideoOrigin Origin,
    string Url,
    DateTimeOffset CreatedAt);
public record CreatePublicationRequest(
    string PublicationId,
    string Title,
    string Synopsis,
    List<ArticleRequest>? Articles,
    List<VideoRequest>? Videos,
    DateTimeOffset CreatedAt
);

public record AddArticleToPublicationRequest(ArticleRequest Article);

public record AddVideoToPublicationRequest(string VideoId);

public record PublicationRequest(MediaPlatform Platform);
