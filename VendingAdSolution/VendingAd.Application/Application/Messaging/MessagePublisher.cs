namespace VendingAdSystem.Application.Messaging;

public class RabbitMqOptions
{
    public bool Enabled { get; set; }
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "vendingad.events";
    public string ScheduleChangedQueueName { get; set; } = "vendingad.worker.schedule-changed";
}

public interface IMessagePublisher
{
    Task PublishAsync<TEvent>(TEvent eventMessage, CancellationToken cancellationToken = default) where TEvent : class;
}
