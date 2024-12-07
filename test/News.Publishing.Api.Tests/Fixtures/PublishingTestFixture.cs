using Marten;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using News.Publishing.Api.Infrastructure.RabbitMQ;
using Ogooreck.API;

namespace News.Publishing.Api.Tests.Fixtures;

public class PublishingTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public ApiSpecification<Program> ApiSpec { get; private set; }
    public ITestHarness TestHarness { get; private set; } = default!;
    
    public IDocumentStore Store { get; private set; } = default!;
    public IConsumerTestHarness<VideoIngestedConsumer> VideoIngestedTestHarness { get; private set; } = default!;
    
    public PublishingTestFixture() => ApiSpec = ApiSpecification<Program>.Setup(this);

    public async Task ForceAsyncDaemonRebuild<TProjection>()
    {
        using var daemon = await Store.BuildProjectionDaemonAsync();
        await daemon.RebuildProjectionAsync<TProjection>(CancellationToken.None);
    }
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddMassTransitTestHarness(cfg =>
            {
                cfg.SetTestTimeouts(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
                cfg.AddConsumer<VideoIngestedConsumer>();
                cfg.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            });
        });
    }
    
    async Task IAsyncLifetime.InitializeAsync()
    {
        TestHarness = this.Services.GetRequiredService<ITestHarness>();
        Store = this.Services.GetRequiredService<IDocumentStore>();
        VideoIngestedTestHarness = this.TestHarness.GetConsumerHarness<VideoIngestedConsumer>();
        
        await TestHarness.Start();
    }

    async Task IAsyncLifetime.DisposeAsync() => await TestHarness.Stop();
}
