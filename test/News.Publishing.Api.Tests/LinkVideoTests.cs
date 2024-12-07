using FluentAssertions;
using Marten;
using News.Publishing.Api.Tests.Fixtures;
using News.Publishing.Publication;
using static Ogooreck.API.ApiSpecification;

namespace News.Publishing.Api.Tests;

[Collection(nameof(PublishingCollectionFixture))]
public class LinkVideoTests(PublishingTestFixture fixture)
{
    [Fact]
    [Trait("Category", "Acceptance")]
    public async Task LinkingVideo_ShouldReturnPublicationWithVideo()
    {
        var (publication, linkedVideo) = await fixture.LinkedVideo();

        await fixture.ApiSpec.Given()
            .When(GET, URI(_ => $"/api/publications/{publication.PublicationId}"))
            .Then(OK,
                RESPONSE_BODY(
                    publication with
                    {
                        VideoIds = [linkedVideo.VideoId],
                        Version = 2
                    }));
    }
    
    [Fact]
    public async Task LinkingVideo_ShouldAddVideoToRemainingCollection()
    {
        var (publication, linkedVideo) = await fixture.LinkedVideo();

        await fixture.ForceAsyncDaemonRebuild<PublicationVideos>();

        await using var session = fixture.Store.QuerySession();
        var publicationVideos = await session.Query<PublicationVideos>()
            .Where(pv => pv.PublicationId == publication.PublicationId)
            .SingleOrDefaultAsync();
        
        publicationVideos.Should().NotBeNull();
        publicationVideos!.RemainingVideoIds.Should().ContainSingle(videoId => videoId == linkedVideo.VideoId);
        publicationVideos.IngestedVideos.Should().BeEmpty();
    }
}
