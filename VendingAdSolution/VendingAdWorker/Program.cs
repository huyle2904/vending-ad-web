using VendingAdWorker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<RabbitMqWorkerOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
