namespace News.Publishing.Api.Infrastructure.RabbitMQ;

public record RabbitMqSettings
{
    public required string Host { get; init; }
    public string VirtualHost { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
