using FluentAssertions;
using MassTransit.Testing;
using News.Publishing.Api.Tests.Fixtures;

namespace News.Publishing.Api.Tests.Helpers;

public static class TestHelper
{
    internal static async Task ConsumeEvents<TEvent>(this PublishingTestFixture fixture,
        IEnumerable<TEvent> events) where TEvent : IntegrationEvent.VideoIngested
    {
        foreach (var @event in events)
        {
            await fixture.TestHarness.Bus.Publish(@event);
            await fixture.VideoIngestedTestHarness.Consumed.Any<IntegrationEvent.VideoIngested>(e =>
                e.Context.Message.VideoId == @event.VideoId);
        }
    }

    internal static async Task AssertEventPublished<TEvent>(this PublishingTestFixture fixture, Action<TEvent> assert)
        where TEvent : class
    {
        const int maxRetryAttempts = 5;
        const int delayBetweenRetriesMs = 1000; // 1 second delay

        var fetchEvent = async () => await fixture.TestHarness.Published
            .SelectAsync<TEvent>()
            .FirstOrDefault();

        var publishedMessage = await RetryAsync<IPublishedMessage<TEvent>>(fetchEvent, maxRetryAttempts, delayBetweenRetriesMs);

        publishedMessage.Should().NotBeNull();
        assert(publishedMessage!.Context.Message);
    }

    private static async Task<T?> RetryAsync<T>(Func<Task<T?>> action, int maxAttempts, int delayMs) where T : class
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (await action() is { } result)
            {
                return result;
            }

            await Task.Delay(delayMs);
        }

        return null;
    }
}
