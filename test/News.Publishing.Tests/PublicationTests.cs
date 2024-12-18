using Bogus;
using FluentAssertions;
using News.Publishing.Publication;
using static System.DateTimeOffset;
using static News.Publishing.Publication.Publication;
using static News.Publishing.Publication.PublicationMediaPlatform;
using static News.Publishing.Publication.MediaPlatform;

namespace News.Publishing.Tests;

public class PublicationTests
{
    private static readonly Faker Faker = new();
    
    [Fact]
    public void CreatePublication_ShouldInitializeCorrectly()
    {
        var @event = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [
                new(
                    Guid.NewGuid(),
                    "Article Title",
                    "Article Text",
                    Faker.Date.PastOffset()
                )
            ],
            Faker.Lorem.Words(1).ToList(),
            Faker.Date.RecentOffset(),
            Now
        );

        var publication = Create(@event);

        publication.Id.Should().Be(@event.Id);
        publication.PublicationId.Should().Be(@event.PublicationId);
        publication.Title.Should().Be(@event.Title);
        publication.Synopsis.Should().Be(@event.Synopsis);
        publication.Articles.Should().NotBeNull().And.HaveCount(1);
        publication.VideoIds.Should().NotBeNull().And.HaveCount(1);
        publication.Status.Should().Be(PublicationStatus.Pending);
        publication.OfKind.Should().Be(PublicationType.Mixed);
        publication.Publications.Should().BeEmpty();
    }

    [Fact]
    public void CreatePublication_WithoutArticleOrVideo_ShouldThrow()
    {
        var initialEvent = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [],
            [],
            Faker.Date.RecentOffset(),
            Now
        );
        var act = () => Create(initialEvent);
        
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Can't evaluate permitted publication type");
    }
    
    [Fact]
    public void ArticleAddedToPublication_ShouldAddArticleIfNotPresent()
    {
        var initialEvent = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [],
            Faker.Lorem.Words(1).ToList(),
            Faker.Date.RecentOffset(),
            Now
        );
        var publication = Create(initialEvent);
        var articleToAdd = new Article(
            Faker.Random.Guid(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Paragraph(),
            Faker.Date.PastOffset()
        );
        var addEvent = new ArticleAddedToPublication(publication.Id, articleToAdd, Now);

        var updated = publication.Apply(addEvent);

        updated.Articles.Should().ContainSingle()
            .Which.ArticleId.Should().Be(articleToAdd.ArticleId);
        updated.OfKind.Should().Be(PublicationType.Mixed);
    }
    
    [Fact]
    public void ArticleAddedToPublication_ShouldNotDuplicateArticle()
    {
        var articleId = Faker.Random.Guid();
        var article = new Article(articleId, Faker.Lorem.Sentence(), Faker.Lorem.Paragraph(), Now);
        var initialEvent = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [article],
            [],
            Faker.Date.RecentOffset(),
            Now
        );
        var publication = Create(initialEvent);
        var addEvent = new ArticleAddedToPublication(publication.Id, article, Now);

        var updated = publication.Apply(addEvent);

        updated.Articles.Should().ContainSingle();
    }
    
    [Fact]
    public void ArticleRemovedFromPublication_ShouldRemoveArticle()
    {
        var articleId = Faker.Random.Guid();
        var article = new Article(articleId, Faker.Lorem.Sentence(), Faker.Lorem.Paragraph(), Now);
        var initialEvent = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [article],
            Faker.Lorem.Words(1).ToList(),
            Faker.Date.RecentOffset(),
            Now
        );
        var publication = Create(initialEvent);
        var removeEvent = new ArticleRemovedFromPublication(publication.Id, article.ArticleId, Now);

        var updated = publication.Apply(removeEvent);

        updated.Articles.Should().BeEmpty();
        updated.OfKind.Should().Be(PublicationType.Video);
    }
    
    [Fact]
    public void VideoAddedToPublication_ShouldAddVideoIfNotPresent()
    {
        var initialEvent = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [],
            ["existing-video-id"],
            Faker.Date.RecentOffset(),
            Now
        );
        var publication = Create(initialEvent);
        var addEvent = new VideoAddedToPublication(publication.Id, Faker.Random.Guid(), "new-video-id", Now);

        var updated = publication.Apply(addEvent);

        updated.VideoIds.Should().Contain(["existing-video-id", "new-video-id"]);
        updated.OfKind.Should().Be(PublicationType.Video);
    }
    
    [Fact]
    public void VideoRemovedFromPublication_ShouldRemoveVideo()
    {
        var initialEvent = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [],
            ["video-id-1", "video-id-2"],
            Faker.Date.RecentOffset(),
            Now
        );
        var publication = Create(initialEvent);
        var removeEvent = new VideoRemovedFromPublication(publication.Id, "video-id-1", Now);

        var updated = publication.Apply(removeEvent);

        updated.VideoIds.Should().ContainSingle().Which.Should().Be("video-id-2");
        updated.OfKind.Should().Be(PublicationType.Video);
    }
    
    [Fact]
    public void PublishRequested_ShouldAppendPublishRequestedStatus()
    {
        var initialEvent = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [],
            Faker.Lorem.Words(1).ToList(),
            Faker.Date.RecentOffset(),
            Now
        );
        var publication = Create(initialEvent);
        var requestedEvent = new PublishRequested(publication.Id, IntrovertTv, Now);

        var updated = publication.Apply(requestedEvent);

        updated.Publications.Should().ContainKey(IntrovertTv);
        updated.Publications[IntrovertTv].Should().ContainSingle()
            .Which.Status.Should().Be(MediaPlatformPublicationStatus.PublishRequested);
    }
    
    [Fact]
    public void Published_ShouldSetStatusToPublishedAndCloseWhenAllPlatformsPublished()
    {
        var initialEvent = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [],
            Faker.Lorem.Words(1).ToList(),
            Faker.Date.RecentOffset(),
            Now
        );
        var publication = Create(initialEvent);
        publication.Apply(new PublishRequested(publication.Id, Youtube, Now));
        var publishedEvent = new Published(publication.Id, Youtube, Now);

        var updated = publication.Apply(publishedEvent);

        updated.Publications[Youtube].Last().Status.Should().Be(MediaPlatformPublicationStatus.Published);
        updated.Status.Should().Be(PublicationStatus.PublishedAndClosed);
    }
    
    [Fact]
    public void UnPublished_ShouldRevertToPendingIfNotAllPlatformsArePublished()
    {
        var initialEvent = new PublicationCreated(
            Faker.Random.Guid(),
            Faker.Lorem.Word(),
            Faker.Lorem.Sentence(),
            Faker.Lorem.Sentence(),
            [],
            Faker.Lorem.Words(1).ToList(),
            Faker.Date.RecentOffset(),
            Now
        );
        var publication = Create(initialEvent)
            .Apply(new PublishRequested(initialEvent.Id, Youtube, Now))
            .Apply(new Published(initialEvent.Id, Youtube, Now))
            .Apply(new PublishRequested(initialEvent.Id, GeekNews, Now))
            .Apply(new Published(initialEvent.Id, IntrovertTv, Now));
        var unpublishEvent = new UnPublished(publication.Id, Youtube, Now);

        var updated = publication.Apply(unpublishEvent);

        updated.Status.Should().Be(PublicationStatus.Pending);
        updated.Publications[Youtube].Last().Status.Should().Be(MediaPlatformPublicationStatus.UnPublished);
    }
}
