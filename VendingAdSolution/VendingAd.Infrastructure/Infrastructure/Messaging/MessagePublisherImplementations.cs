using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using VendingAdSystem.Application.Messaging;

namespace VendingAdSystem.Infrastructure.Messaging;

public class NullMessagePublisher : IMessagePublisher
{
    public Task PublishAsync<TEvent>(TEvent eventMessage, CancellationToken cancellationToken = default) where TEvent : class
    {
        return Task.CompletedTask;
    }
}

public class RabbitMqMessagePublisher : IMessagePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqMessagePublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync<TEvent>(TEvent eventMessage, CancellationToken cancellationToken = default) where TEvent : class
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                DispatchConsumersAsync = true
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);

            var eventName = typeof(TEvent).Name;
            var routingKey = ToRoutingKey(eventName);
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventMessage, JsonOptions));
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = eventName;
            properties.MessageId = Guid.NewGuid().ToString("N");
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(_options.ExchangeName, routingKey, mandatory: false, basicProperties: properties, body: body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish RabbitMQ event {EventType}.", typeof(TEvent).Name);
        }

        return Task.CompletedTask;
    }

    private static string ToRoutingKey(string eventName)
    {
        return eventName
            .Replace("Event", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Changed", ".changed", StringComparison.OrdinalIgnoreCase)
            .Replace("Uploaded", ".uploaded", StringComparison.OrdinalIgnoreCase)
            .Trim('.')
            .ToLowerInvariant();
    }
}
