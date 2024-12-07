using News.Publishing.Api.Tests.Fixtures;
using News.Publishing.Publication;
using News.Publishing.Videos;

namespace News.Publishing.Api.Tests.Helpers;

public static class MartenHelper
{
    public static async Task RebuildAsyncProjections(this PublishingTestFixture fixture)
    {
        await fixture.ForceAsyncDaemonRebuild<VideoDetails>();
        await fixture.ForceAsyncDaemonRebuild<PublicationVideos>();
    }
}
