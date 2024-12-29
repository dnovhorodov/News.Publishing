using System.Text.Json.Serialization;
using Marten.Exceptions;
using Microsoft.AspNetCore.HttpOverrides;
using News.Publishing.Api.Endpoints;
using News.Publishing.Api.Infrastructure;
using News.Publishing.Api.Infrastructure.Http.Middlewares.ExceptionHandling;
using Oakton;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

var builder = WebApplication.CreateBuilder(args);

// Register the NpgsqlDataSource in the IoC container using
// connection string named "marten" from IConfiguration
builder.AddNpgsqlDataSource("marten");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddDefaultExceptionHandler(
        (exception, _) => exception switch
        {
            ConcurrencyException =>
                exception.MapToProblemDetails(StatusCodes.Status412PreconditionFailed),
            ExistingStreamIdCollisionException =>
                exception.MapToProblemDetails(StatusCodes.Status412PreconditionFailed),
            _ => null,
        })
    .ConfigureMassTransit()
    .ConfigureMarten(builder.Configuration);

builder.Services
    .Configure<JsonOptions>(o =>
    {
        o.SerializerOptions.PropertyNamingPolicy = null;
        o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Header forwarding to enable Swagger in Nginx
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Host.ApplyOaktonExtensions();

var app = builder.Build();

var publicationGroup = app.MapGroup("api/publications").WithTags("Publications");
var videoGroup = app.MapGroup("api/videos").WithTags("Videos");
var maintenanceGroup = app.MapGroup("api/maintenance").WithTags("Maintenance");

publicationGroup.MapPublicationEndpoints();
videoGroup.MapVideoEndpoints();
maintenanceGroup.MapMaintenanceEndpoints();

// Configure the HTTP request pipeline.
app
    .UseSwagger()
    .UseSwaggerUI()
    .UseForwardedHeaders(); // Header forwarding to enable Swagger in Nginx;

app.UseHttpsRedirection();

// app.Run();
return await app.RunOaktonCommands(args);

public partial class Program;
