using Bogus;
using News.Publishing.Api.Endpoints;
using News.Publishing.Videos;

namespace News.Publishing.Api.Tests.Helpers;

public static class FakerHelpers
{
    internal static readonly Faker<ArticleRequest> ArticleRequestFaker = new Faker<ArticleRequest>().CustomInstantiator(
        f => new ArticleRequest(
            f.Random.Guid(),
            f.Lorem.Sentence(5),
            f.Lorem.Paragraphs(2),
            f.Date.PastOffset()));

    internal static readonly Faker<VideoRequest> VideoRequestFaker = new Faker<VideoRequest>().CustomInstantiator(
        f => new VideoRequest(
            f.Random.Guid().ToString(),
            f.PickRandom("mp4", "avi", "mkv"),
            f.PickRandom<VideoOrigin>(),
            f.Internet.Url(),
            f.Date.PastOffset()));
}
