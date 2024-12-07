using Marten;
using Marten.Events.Aggregation;
using Marten.Internal.Sessions;
using Marten.Services;
using MassTransit;

namespace News.Publishing.Api.Infrastructure.RabbitMQ;

public class RabbitMqMessageOutbox : IMessageOutbox
{
    private readonly IServiceProvider _serviceProvider;

    public RabbitMqMessageOutbox(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public ValueTask<IMessageBatch> CreateBatch(DocumentSessionBase session)
    {
        var batch = new RabbitMqMessageBatch(_serviceProvider);
        return new ValueTask<IMessageBatch>(batch);
    }
}

public class RabbitMqMessageBatch : IMessageBatch
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<object?> _messages = new();

    public RabbitMqMessageBatch(IServiceProvider provider)
        => _serviceProvider = provider;

    public ValueTask PublishAsync<T>(T message)
    {
        _messages.Add(message);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Used to carry out actions on potentially changed projected documents generated and updated
    /// during the execution of asynchronous projections. This will give you "at most once" delivery guarantees
    /// </summary>
    /// <param name="session"></param>
    /// <param name="commit"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task AfterCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Used to carry out actions on potentially changed projected documents generated and updated
    /// during the execution of asynchronous projections. This will execute *before* database changes
    /// are committed. Use this for "at least once" delivery guarantees.
    /// </summary>
    /// <param name="session"></param>
    /// <param name="commit"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task BeforeCommitAsync(IDocumentSession session, IChangeSet commit, CancellationToken token)
    {
        using var scope = _serviceProvider.CreateScope();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        
        foreach (var message in _messages)
        {
            await publishEndpoint.Publish(message!);
        }
    }
}
