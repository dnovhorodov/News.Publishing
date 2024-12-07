using FluentAssertions;
using Marten;
using News.Publishing.Api.Tests.Fixtures;
using News.Publishing.Publication;
using static System.DateTimeOffset;
using static News.Publishing.IntegrationEvent;
using static Ogooreck.API.ApiSpecification;

namespace News.Publishing.Api.Tests;

[Collection(nameof(PublishingCollectionFixture))]
public class PublishRequestTests(PublishingTestFixture fixture)
{
    [Fact]
    [Trait("Category", "Acceptance")]
    public async Task PublishRequest_ShouldCreateEntryInPublicationHistory()
    {
        var publication = await fixture.ApiSpec.PublishedRequest();

        await fixture.ApiSpec.Given()
            .When(GET, URI(_ => $"/api/publications/{publication.PublicationId}"))
            .Then(OK,
                RESPONSE_BODY<PublicationDetails>(response =>
                {
                    response.PublicationHistory.Should().NotBeEmpty();
                    response.PublicationHistory.Should()
                        .ContainSingle(p => p.Key == MediaPlatform.BbcNews);
                    response.PublicationHistory[MediaPlatform.BbcNews].Should().ContainSingle(h =>
                        h.Status == MediaPlatformPublicationStatus.PublishRequested);
                    response.Version.Should().Be(2);
                }));
    }

    [Fact]
    [Trait("Category", "Acceptance")]
    public async Task WhenAllVideosIngested_And_PublishRequested_ShouldPublishPublicationReadyEvent()
    {
        var fakeVideoRequests = VideoRequestFaker.Generate(2);
        var publication = await fixture.ApiSpec.CreatedPublication([], fakeVideoRequests);

        var videoIngestedEvents = fakeVideoRequests
            .Select(r => new VideoIngested(r.VideoId, Now))
            .ToList();
        await fixture.ConsumeEvents(videoIngestedEvents);
        await fixture.RebuildAsyncProjections();
        await fixture.ApiSpec.PublishedRequest(publication.PublicationId);

        await fixture.AssertEventPublished<PublicationReady>(e =>
            e.PublicationUrl.Should().NotBeEmpty());
    }

    [Fact]
    public async Task PublishRequest_ShouldUpdatePublishRequestedAt()
    {
        var publication = await fixture.ApiSpec.PublishedRequest();
        await fixture.RebuildAsyncProjections();
        
        await using var session = fixture.Store.QuerySession();
        var publicationVideos = await session.Query<PublicationVideos>()
            .Where(pv => pv.PublicationId == publication.PublicationId)
            .SingleOrDefaultAsync();

        publicationVideos!.PublishRequestedAt.Should().NotBeNull();
    }
}
