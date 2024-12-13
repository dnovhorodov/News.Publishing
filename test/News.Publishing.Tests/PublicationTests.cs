using Bogus;
using FluentAssertions;
using News.Publishing.Publication;
using static System.DateTimeOffset;
using static News.Publishing.Publication.Publication;

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
            Faker.Lorem.Words(5).ToList(),
            Faker.Date.RecentOffset(),
            Now
        );

        var publication = Create(@event);

        publication.Id.Should().Be(@event.Id);
        publication.PublicationId.Should().Be(@event.PublicationId);
        publication.Title.Should().Be(@event.Title);
        publication.Synopsis.Should().Be(@event.Synopsis);
        publication.Articles.Should().NotBeNull().And.HaveCount(1);
        publication.VideoIds.Should().NotBeNull().And.HaveCount(5);
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
}
