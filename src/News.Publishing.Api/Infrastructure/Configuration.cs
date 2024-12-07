using System.Text.Json.Serialization;
using JasperFx.CodeGeneration;
using Marten;
using Marten.Events;
using Marten.Events.Daemon.Resiliency;
using Marten.Storage;
using MassTransit;
using News.Publishing.Api.Infrastructure.RabbitMQ;
using Weasel.Core;

namespace News.Publishing.Api.Infrastructure;

public static class Configuration
{
    public static IServiceCollection ConfigureMassTransit(this IServiceCollection services, string appName)
    {
        return services.AddMassTransit(bus =>
        {
            bus.UsingRabbitMq((context, cfg) =>
            {
                var configuration = context.GetRequiredService<IConfiguration>();
                var rabbitMqSettings = configuration.GetSection("RabbitMQ").Get<RabbitMqSettings>()
                                       ?? throw new ArgumentNullException(nameof(RabbitMqSettings), "RabbitMq settings cannot be null. Ensure they are defined in appsettings.json.");
                
                cfg.Host(rabbitMqSettings.Host, rabbitMqSettings.VirtualHost, h =>
                {
                    h.Username(rabbitMqSettings.Username);
                    h.Password(rabbitMqSettings.Password);
                });

                cfg.ConfigureJsonSerializerOptions(jsonOptions =>
                {
                    jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    jsonOptions.Converters.Add(new JsonStringEnumConverter());
                    return jsonOptions;
                });

                cfg.Publish<IntegrationEvent.VideoUploaded>(x =>
                {
                    x.Durable = true;
                    x.ExchangeType = "fanout";
                });
                cfg.Publish<IntegrationEvent.PublicationReady>(x =>
                {
                    x.Durable = true;
                    x.ExchangeType = "fanout";
                });
                cfg.ReceiveEndpoint("video-ingested", e =>
                {
                    e.ConfigureConsumer<VideoIngestedConsumer>(context);
                });
            });
            
            bus.AddConsumer<VideoIngestedConsumer>();
        });
    }

    public static IServiceCollection ConfigureMarten(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMarten(provider =>
            {
                var options = new StoreOptions();

                var schemaName = Environment.GetEnvironmentVariable("SchemaName") ?? "Publishing";
                options.Events.DatabaseSchemaName = schemaName;
                options.DatabaseSchemaName = schemaName;
                options.Connection(configuration.GetConnectionString("Publications") ??
                                   throw new InvalidOperationException());

                options.UseSystemTextJsonForSerialization(EnumStorage.AsString);

                options.Events.TenancyStyle = TenancyStyle.Conjoined;
                options.Policies.AllDocumentsAreMultiTenanted();

                options.Events.StreamIdentity = StreamIdentity.AsGuid;
                options.Events.MetadataConfig.HeadersEnabled = true;
                options.Events.MetadataConfig.CausationIdEnabled = true;
                options.Events.MetadataConfig.CorrelationIdEnabled = true;
                options.Events.UseIdentityMapForInlineAggregates = true;
                options.Events.MessageOutbox = new RabbitMqMessageOutbox(provider);

                options.Projections.Errors.SkipApplyErrors = false;
                options.Projections.Errors.SkipSerializationErrors = false;
                options.Projections.Errors.SkipUnknownEvents = false;

                options.ConfigurePublications();

                return options;
            })
            .OptimizeArtifactWorkflow(TypeLoadMode.Static)
            .ApplyAllDatabaseChangesOnStartup()
            .UseLightweightSessions()
            .AddAsyncDaemon(DaemonMode.HotCold);

        return services;
    }
}
