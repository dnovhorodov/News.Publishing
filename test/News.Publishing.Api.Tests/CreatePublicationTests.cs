using Bogus;
using FluentAssertions;
using Marten;
using News.Publishing.Api.Tests.Fixtures;
using News.Publishing.Publication;
using Ogooreck.API;
using static Ogooreck.API.ApiSpecification;

namespace News.Publishing.Api.Tests;

[Collection(nameof(PublishingCollectionFixture))]
public class CreatePublicationTests(PublishingTestFixture fixture)
{
    [Fact]
    [Trait("Category", "Acceptance")]
    public async Task CreatePublicationWithVideos_ShouldSucceed_And_PublishVideoUploadedEvent()
    {
        var publicationId = Guid.NewGuid().ToString();
        var fakeVideoRequest = VideoRequestFaker.Generate(1).Single();

        await fixture.ApiSpec.Given()
            .When(
                POST,
                URI("api/publications"),
                BODY(PublicationRequestFaker
                    .RuleFor(p => p.PublicationId, publicationId)
                    .RuleFor(p => p.Videos, [fakeVideoRequest])
                    .Generate()))
            .Then(CREATED_WITH_DEFAULT_HEADERS(locationHeaderPrefix: "/api/publications/"))
            .And()
            .When(GET, URI(ctx => $"/api/publications/{ctx.GetCreatedId()}"))
            .Then(OK,
                RESPONSE_BODY<PublicationDetails>(response =>
                {
                    response.Id.Should().NotBeEmpty();
                    response.PublicationId.Should().Be(publicationId);
                    response.Articles.Should().BeNullOrEmpty();
                    response.VideoIds.Should().NotBeNullOrEmpty();
                    response.PublicationHistory.Should().BeNullOrEmpty();
                    response.OfKind.Should().Be(PublicationType.Video);
                }))
            .And()
            .When(GET, URI(_ => $"/api/videos/{fakeVideoRequest.VideoId}"))
            .Until(RESPONSE_SUCCEEDED())
            .Then(OK);

        await fixture.AssertEventPublished<IntegrationEvent.VideoUploaded>(e =>
        {
            e.Should().NotBeNull();
            e.VideoId.Should().Be(fakeVideoRequest.VideoId);
        });
    }
    
    [Fact]
    public async Task CreatePublicationWithArticles_ShouldSucceed()
    {
        var publicationId = Guid.NewGuid().ToString();

        await fixture.ApiSpec.Given()
            .When(
                POST,
                URI("api/publications"),
                BODY(PublicationRequestFaker
                    .RuleFor(p => p.PublicationId, publicationId)
                    .RuleFor(p => p.Articles, (f, _) => ArticleRequestFaker.Generate(f.Random.Int(1, 3)))
                    .Generate()))
            .Then(CREATED_WITH_DEFAULT_HEADERS(locationHeaderPrefix: "/api/publications/"))
            .And()
            .When(GET, URI(ctx => $"/api/publications/{ctx.GetCreatedId()}"))
            .Then(OK,
                RESPONSE_BODY<PublicationDetails>(response =>
                {
                    response.Id.Should().NotBeEmpty();
                    response.PublicationId.Should().Be(publicationId);
                    response.Articles.Should().NotBeNullOrEmpty();
                    response.PublicationHistory.Should().BeNullOrEmpty();
                    response.OfKind.Should().Be(PublicationType.Article);
                }));
    }

    [Fact]
    public async Task CreatePublicationWithVideos_ShouldAddVideosToRemainingCollection()
    {
        var fakeVideoRequests = VideoRequestFaker.Generate(2);
        var publication = await fixture.ApiSpec.CreatedPublication(videoRequests: fakeVideoRequests, articleRequests: []);

        await fixture.ForceAsyncDaemonRebuild<PublicationVideos>();
        
        await using var session = fixture.Store.QuerySession();
        var publicationVideos = await session.Query<PublicationVideos>()
            .Where(pv => pv.PublicationId == publication.PublicationId)
            .SingleOrDefaultAsync();
        
        publicationVideos.Should().NotBeNull();
        publicationVideos!.RemainingVideoIds.Should().NotBeEmpty();
        publicationVideos.RemainingVideoIds!.Count.Should().Be(2);
    }

    private readonly Faker<CreatePublicationRequest> PublicationRequestFaker =
        new Faker<CreatePublicationRequest>().CustomInstantiator(
            f => new CreatePublicationRequest(
                f.Random.Guid().ToString(),
                f.Lorem.Sentence(3),
                f.Lorem.Paragraph(),
                [],
                [],
                f.Date.PastOffset(1)));
}
