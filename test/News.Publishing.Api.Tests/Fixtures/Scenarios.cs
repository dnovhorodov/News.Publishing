using Bogus;
using News.Publishing.Publication;
using News.Publishing.Videos;
using Newtonsoft.Json;
using Ogooreck.API;
using Ogooreck.Newtonsoft;
using static Ogooreck.API.ApiSpecification;

namespace News.Publishing.Api.Tests.Fixtures;

public static class Scenarios
{
    private static readonly Faker Faker = new();
    private static readonly JsonSerializerSettings JsonSerializerSettings = new();

    public static async Task<PublicationDetails> CreatedPublication(this ApiSpecification<Program> api,
        List<ArticleRequest> articleRequests, List<VideoRequest> videoRequests)
    {
        var request = new CreatePublicationRequest(
            Faker.Random.Guid().ToString(),
            Faker.Lorem.Sentence(3),
            Faker.Lorem.Paragraph(),
            articleRequests,
            videoRequests,
            Faker.Date.PastOffset(1));

        var response = await api.Scenario(
            api.CreatePublication(request),
            r => api.GetPublicationDetails(r.GetCreatedId<string>())
        );

        return await response.GetResultFromJson<PublicationDetails>();
    }

    public static async Task<VideoDetails> CreatedVideo(this PublishingTestFixture fixture,
        string publicationId)
    {
        var request = new CreateVideoRequest(
            Faker.Random.Guid().ToString(),
            publicationId,
            Faker.PickRandom("mp4", "avi", "mkv"),
            Faker.PickRandom<VideoOrigin>(),
            Faker.Internet.Url(),
            Faker.Date.PastOffset(1));

        var result = await fixture.ApiSpec.CreateVideo(request);
        // Here I'm going to completely rewind the async projections, then
        // rebuild from 0 to the very end of the event store so we know
        // we got our new stream completely processed
        await fixture.RebuildAsyncProjections();
        var response = await fixture.ApiSpec.GetVideoDetails(result.Response.GetCreatedId<string>());

        return await response.FromResponse<VideoDetails>();
    }

    public static async Task<(PublicationDetails, VideoDetails)> LinkedVideo(this PublishingTestFixture fixture)
    {
        var publication = await fixture.ApiSpec.CreatedPublication(ArticleRequestFaker.Generate(1), []);
        return (publication, await fixture.LinkedVideo(publication.PublicationId));
    }

    public static async Task<VideoDetails> LinkedVideo(this PublishingTestFixture fixture,
        string publicationId)
    {
        var videoId = Faker.Random.Guid().ToString();

        var request = new CreateVideoRequest(
            videoId,
            publicationId,
            Faker.PickRandom("mp4", "avi", "mkv"),
            Faker.PickRandom<VideoOrigin>(),
            Faker.Internet.Url(),
            Faker.Date.PastOffset(1));

        await fixture.ApiSpec.CreateVideo(request);
        // Here I'm going to completely rewind the async projections, then
        // rebuild from 0 to the very end of the event store so we know
        // we got our new stream completely processed
        await fixture.RebuildAsyncProjections();

        var response = await fixture.ApiSpec.GetVideoDetails(videoId);

        return await response.FromResponse<VideoDetails>();
    }

    public static async Task<PublicationDetails> PublishedRequest(this ApiSpecification<Program> api,
        MediaPlatform mediaPlatform = MediaPlatform.GeekNews)
    {
        var publication = await api.CreatedPublication(ArticleRequestFaker.Generate(1), []);

        await api.PublishedRequest(publication.PublicationId, mediaPlatform);

        return publication;
    }

    public static async Task PublishedRequest(this ApiSpecification<Program> api, string publicationId,
        MediaPlatform mediaPlatform = MediaPlatform.GeekNews) =>
        await api.PublishRequest(publicationId, mediaPlatform);

    private static Task<Result> CreatePublication(
        this ApiSpecification<Program> api,
        CreatePublicationRequest request) =>
        api.Given()
            .When(POST, URI("api/publications"), BODY(request))
            .Then(CREATED_WITH_DEFAULT_HEADERS(locationHeaderPrefix: "/api/publications/"));

    private static Task<Result> GetPublicationDetails(
        this ApiSpecification<Program> api,
        string publicationId) => api.Given().When(GET, URI($"api/publications/{publicationId}")).Then(OK);

    private static Task<Result> CreateVideo(
        this ApiSpecification<Program> api,
        CreateVideoRequest request) =>
        api.Given()
            .When(POST, URI("api/videos"), BODY(request))
            .Then(CREATED_WITH_DEFAULT_HEADERS(locationHeaderPrefix: "/api/videos/"));

    private static Task<Result> LinkVideo(
        this ApiSpecification<Program> api,
        string publicationId,
        string videoId,
        int version = 1) =>
        api.Given()
            .When(PUT, URI($"api/publications/{publicationId}/videos/"),
                BODY(new AddVideoToPublicationRequest(videoId)),
                HEADERS(IF_MATCH(version)))
            .Then(OK);

    private static Task<Result> GetVideoDetails(
        this ApiSpecification<Program> api,
        string videoId) => api.Given().When(GET, URI($"api/videos/{videoId}")).Then(OK);

    private static Task<Result> PublishRequest(
        this ApiSpecification<Program> api,
        string publicationId,
        MediaPlatform mediaPlatform,
        int version = 1) =>
        api.Given()
            .When(PUT, URI($"api/publications/{publicationId}/publication-requests/"),
                BODY(new PublicationRequest(mediaPlatform)),
                HEADERS(IF_MATCH(version)))
            .Then(OK);

    private static async Task<T> FromResponse<T>(this Result result)
    {
        var content = await result.Response.Content.ReadAsStringAsync();
        return content.FromJson<T>(JsonSerializerSettings);
    }
}
