using FluentAssertions;
using Marten;
using News.Publishing.Api.Tests.Fixtures;
using News.Publishing.Publication;
using News.Publishing.Videos;
using static System.DateTimeOffset;
using static News.Publishing.IntegrationEvent;

namespace News.Publishing.Api.Tests;

[Collection(nameof(PublishingCollectionFixture))]
public class IngestVideoTests(PublishingTestFixture fixture)
{
    [Fact]
    [Trait("Category", "Acceptance")]
    public async Task WhenPublishRequested_And_AllVideosBeingIngested_ShouldPublishPublicationReadyEvent()
    {
        var fakeVideoRequests = VideoRequestFaker.Generate(3);
        var publication = await fixture.ApiSpec.CreatedPublication([], fakeVideoRequests);
        await fixture.RebuildAsyncProjections();
        await fixture.ApiSpec.PublishedRequest(publication.PublicationId);
        
        var videoIngestedEvents = fakeVideoRequests
            .Select(r => new VideoIngested(r.VideoId, Now))
            .ToList();
        await fixture.ConsumeEvents(videoIngestedEvents);
        
        await fixture.AssertEventPublished<PublicationReady>(e =>
            e.PublicationUrl.Should().NotBeEmpty());
    }

    [Fact]
    [Trait("Category", "Acceptance")]
    public async Task WhenIngestedVideoEventReceived_ShouldUpdateIntestedAt()
    {
        var publication = await fixture.ApiSpec.CreatedPublication(ArticleRequestFaker.Generate(1), []);
        var video = await fixture.CreatedVideo(publication.PublicationId);

        await fixture.ConsumeEvents([new VideoIngested(video.VideoId, Now)]);
        await fixture.RebuildAsyncProjections();
        
        await using var session = fixture.Store.LightweightSession();
        var videoDetails = await session.LoadAsync<VideoDetails>(video.Id);
        videoDetails!.IngestedAt.Should().NotBeNull();
        videoDetails.IsIngested.Should().BeTrue();
    }

    [Fact]
    public async Task IngestingVideo_ShouldMoveVideoToIngestedCollection()
    {
        var (publication, linkedVideo) = await fixture.LinkedVideo();

        await fixture.ConsumeEvents([new VideoIngested(linkedVideo.VideoId, Now)]);
        await fixture.RebuildAsyncProjections();

        await using var session = fixture.Store.QuerySession();
        var publicationVideos = await session.Query<PublicationVideos>()
            .Where(pv => pv.PublicationId == publication.PublicationId)
            .SingleOrDefaultAsync();
        publicationVideos.Should().NotBeNull();
        publicationVideos!.RemainingVideoIds.Should().BeEmpty();
        publicationVideos.IngestedVideos.Should().ContainSingle(x => x.VideoId == linkedVideo.VideoId);
    }
}
