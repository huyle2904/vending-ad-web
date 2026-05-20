namespace VendingAdWorker;

public class RabbitMqWorkerOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "vendingad";
    public string Password { get; set; } = "vendingad@123";
    public string ExchangeName { get; set; } = "vendingad.events";
    public string ScheduleChangedQueueName { get; set; } = "vendingad.worker.schedule-changed";
}
