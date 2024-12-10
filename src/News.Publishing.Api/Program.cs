using System.Text.Json.Serialization;
using Marten;
using Marten.Schema.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using News.Publishing.Api.Infrastructure;
using News.Publishing.Api.Infrastructure.Http;
using News.Publishing.Api.Infrastructure.Marten;
using News.Publishing.Features;
using News.Publishing.Publication;
using News.Publishing.Videos;
using Oakton;
using static System.DateTimeOffset;
using static Microsoft.AspNetCore.Http.TypedResults;
using static News.Publishing.Api.Infrastructure.Http.ETagExtensions;
using static News.Publishing.Publication.MediaPlatformAction;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

var builder = WebApplication.CreateBuilder(args);
var appName = "news-publishing";

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    //.AddDefaultExceptionHandler()
    .ConfigureMassTransit(appName)
    .ConfigureMarten(builder.Configuration);

builder.Services
    .Configure<JsonOptions>(o =>
    {
        o.SerializerOptions.PropertyNamingPolicy = null;
        o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Header forwarding to enable Swagger in Nginx
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Host.ApplyOaktonExtensions();

var app = builder.Build();

var publicationGroup = app.MapGroup("api/publications").WithTags("Publications");
var videoGroup = app.MapGroup("api/videos").WithTags("Videos");
var maintenanceGroup = app.MapGroup("api/maintenance").WithTags("Maintenance");

publicationGroup.MapPost("",
    async (
        [FromServices] IDocumentSession documentSession,
        CreatePublicationRequest body,
        CancellationToken ct) =>
    {
        // Validate
        var (publicationId, title, synopsis, articleRequests, videoRequests, createdAt) = body;

        if (await documentSession.Query<PublicationDetails>()
                .AnyAsync(p => p.PublicationId == publicationId, ct))
        {
            return Results.Conflict($"Publication with provided id {publicationId} already exists");
        }

        // Map & handle
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
            new CreatePublication(streamId, publicationId, title, synopsis, articles, articleVideos, videos, createdAt,
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
            .SingleOrDefaultAsync();

        if (publication is null)
            return Results.NotFound($"Publication with id {publicationId} not found");

        var article = new Article(
            body.Article.ArticleId,
            body.Article.Title,
            body.Article.Text,
            body.Article.CreatedAt) { VideoIds = (body.Article.Videos ?? []).Select(v => v.VideoId).ToList(), };

        await documentSession.GetAndUpdate<Publication>(publication.Id, ToExpectedVersion(eTag),
            state => PublicationService.LinkArticle(state,
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
            .SingleOrDefaultAsync();

        if (publication is null)
            return Results.NotFound($"Publication with id {publicationId} not found");

        var video = await documentSession.Query<VideoDetails>().Where(x => x.VideoId == body.VideoId)
            .SingleOrDefaultAsync();

        if (video is null)
            return Results.NotFound($"Video with id {body.VideoId} not found");

        await documentSession.GetAndUpdate<Publication>(publication.Id, ToExpectedVersion(eTag),
            state => PublicationService.LinkVideo(state,
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
            .SingleOrDefaultAsync();

        if (publication is null)
            return Results.NotFound($"Publication with id {publicationId} not found");

        await documentSession.GetAndUpdate<Publication>(publication.Id, ToExpectedVersion(eTag),
            state => PublicationService.PublishRequest(state,
                new PublishRequest(publication.Id, body.Platform, Now)), ct);

        return Ok();
    });

publicationGroup.MapGet("{publicationId}",
    async (HttpContext context, IQuerySession querySession, string publicationId) =>
    {
        var publication = await querySession.Query<PublicationDetails>().Where(p => p.PublicationId == publicationId)
            .SingleOrDefaultAsync();

        return publication is not null ? Results.Ok(publication) : Results.NotFound();
    }
);

videoGroup.MapPost("",
    async (
        [FromServices] IDocumentSession documentSession,
        CreateVideoRequest body,
        CancellationToken ct
    ) =>
    {
        // Validate
        if (await documentSession.Query<VideoDetails>().AnyAsync(v => v.VideoId == body.VideoId, ct))
            return Results.Conflict($"Video with id {body.VideoId} already exists");

        var publication = await documentSession.Query<PublicationDetails>()
            .Where(x => x.PublicationId == body.PublicationId)
            .SingleOrDefaultAsync(ct);

        if (publication is null)
            return Results.NotFound($"Publication with id {body.PublicationId} not found");

        // Handle
        var streamId = CombGuidIdGeneration.NewGuid();

        await documentSession.Add<Video>(streamId, VideoService.Create(
            new CreateVideo(streamId, body.VideoId, publication.Id, body.PublicationId, body.MediaType, body.Origin,
                body.Url, body.CreatedAt, Now)), ct);

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

maintenanceGroup.MapPost("projections/rebuild",
    async (
        [FromServices] IDocumentStore documentStore,
        [FromBody] RebuildProjectionRequest request,
        CancellationToken ct) =>
    {
        if (request.ProjectionName is null)
            throw new ArgumentNullException(nameof(request.ProjectionName));
        
        using var daemon = await documentStore.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync(request.ProjectionName, ct);
        
        return Results.Accepted();
    });

// Configure the HTTP request pipeline.
app
    .UseSwagger()
    .UseSwaggerUI()
    .UseForwardedHeaders(); // Header forwarding to enable Swagger in Nginx;

//app.UseHttpsRedirection();

// app.Run();
return await app.RunOaktonCommands(args);

public record ArticleRequest(
    Guid ArticleId,
    string Title,
    string Text,
    DateTimeOffset CreatedAt
)
{
    public IReadOnlyList<VideoRequest>? Videos { get; init; } = [];
}

public record VideoRequest(
    string VideoId,
    string MediaType,
    VideoOrigin Origin,
    string Url,
    DateTimeOffset CreatedAt);

public record CreateVideoRequest(
    string VideoId,
    string PublicationId,
    string MediaType,
    VideoOrigin Origin,
    string Url,
    DateTimeOffset CreatedAt) : VideoRequest(VideoId, MediaType, Origin, Url, CreatedAt);

public record CreatePublicationRequest(
    string PublicationId,
    string Title,
    string Synopsis,
    List<ArticleRequest>? Articles,
    List<VideoRequest>? Videos,
    DateTimeOffset CreatedAt
);

public record RebuildProjectionRequest(
    string? ProjectionName
);

public record AddArticleToPublicationRequest(ArticleRequest Article);

public record AddVideoToPublicationRequest(string VideoId);

public record PublicationRequest(MediaPlatform Platform);

public partial class Program;
