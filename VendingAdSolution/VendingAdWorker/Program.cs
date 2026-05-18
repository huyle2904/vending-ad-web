using VendingAdSystem.Application.Messaging;
using VendingAdWorker;
using VendingAdSystem.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMQ"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.HostName), "RabbitMQ:HostName is required.")
    .Validate(options => options.Port > 0, "RabbitMQ:Port must be greater than 0.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.UserName), "RabbitMQ:UserName is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "RabbitMQ:Password is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ExchangeName), "RabbitMQ:ExchangeName is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ScheduleChangedQueueName), "RabbitMQ:ScheduleChangedQueueName is required.")
    .ValidateOnStart();
builder.Services.AddWorkerInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
