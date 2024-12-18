using Marten;
using Microsoft.AspNetCore.Mvc;

namespace News.Publishing.Api.Endpoints;

public static class MaintenanceEndpoints
{
    public static void MapMaintenanceEndpoints(this RouteGroupBuilder maintenanceGroup)
    {
        maintenanceGroup.MapPost("projections/rebuild",
            async (
                [FromServices] IDocumentStore documentStore,
                [FromBody] RebuildProjectionRequest request,
                CancellationToken ct) =>
            {
                if (request.ProjectionName is null)
                    throw new ArgumentNullException(nameof(request.ProjectionName));

                using var daemon = await documentStore.BuildProjectionDaemonAsync();
                await daemon.RebuildProjectionAsync(request.ProjectionName, ct);

                return Results.Accepted();
            });
    }
}

public record RebuildProjectionRequest(
    string? ProjectionName
);
