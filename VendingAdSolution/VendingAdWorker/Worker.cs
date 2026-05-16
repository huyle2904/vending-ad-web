using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VendingAd.Contracts;

namespace VendingAdWorker;

public class Worker : BackgroundService
{
    private const string ScheduleChangedRoutingKey = "schedule.changed";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<Worker> _logger;
    private readonly RabbitMqWorkerOptions _options;
    private IConnection? _connection;
    private IModel? _channel;

    public Worker(ILogger<Worker> logger, IOptions<RabbitMqWorkerOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        _channel.QueueDeclare(_options.ScheduleChangedQueueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(_options.ScheduleChangedQueueName, _options.ExchangeName, ScheduleChangedRoutingKey);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleScheduleChangedAsync;

        _channel.BasicConsume(_options.ScheduleChangedQueueName, autoAck: false, consumer);
        _logger.LogInformation("Worker listening to queue {QueueName} with routing key {RoutingKey}.", _options.ScheduleChangedQueueName, ScheduleChangedRoutingKey);

        stoppingToken.Register(() => _logger.LogInformation("Worker stopping."));
        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private Task HandleScheduleChangedAsync(object sender, BasicDeliverEventArgs args)
    {
        if (_channel == null)
            return Task.CompletedTask;

        try
        {
            var payload = Encoding.UTF8.GetString(args.Body.ToArray());
            var message = JsonSerializer.Deserialize<ScheduleChangedEvent>(payload, JsonOptions);
            if (message == null)
            {
                _logger.LogWarning("Received empty ScheduleChangedEvent payload.");
                _channel.BasicReject(args.DeliveryTag, requeue: false);
                return Task.CompletedTask;
            }

            _logger.LogInformation(
                "Consumed ScheduleChangedEvent {EventId}: ScheduleId={ScheduleId}, ChangeType={ChangeType}, Devices={Devices}",
                message.EventId,
                message.ScheduleId,
                message.ChangeType,
                string.Join(",", message.AffectedDeviceCodes));

            _channel.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process ScheduleChangedEvent.");
            _channel.BasicReject(args.DeliveryTag, requeue: false);
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
