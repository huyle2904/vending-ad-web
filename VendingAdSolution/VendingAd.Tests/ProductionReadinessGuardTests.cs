using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VendingAdSystem.Infrastructure;
using Xunit;

namespace VendingAd.Tests;

public class ProductionReadinessGuardTests
{
    [Fact]
    public void AddInfrastructure_WhenProductionRequiresDistributedCacheWithoutRedis_ThrowsClearError()
    {
        using var environment = new TemporaryEnvironment("ASPNETCORE_ENVIRONMENT", "Production");
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=vendingad;Username=test;Password=test",
            ["ProductionReadiness:RequireDistributedCache"] = "true",
            ["Redis:Enabled"] = "false",
            ["Redis:ConnectionString"] = "localhost:6379"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(configuration));

        Assert.Contains("Redis must be enabled", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_WhenProductionRequiresMessageBusWithoutRabbitMq_ThrowsClearError()
    {
        using var environment = new TemporaryEnvironment("ASPNETCORE_ENVIRONMENT", "Production");
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=vendingad;Username=test;Password=test",
            ["ProductionReadiness:RequireMessageBus"] = "true",
            ["RabbitMQ:Enabled"] = "false",
            ["RabbitMQ:HostName"] = "localhost",
            ["RabbitMQ:UserName"] = "",
            ["RabbitMQ:Password"] = ""
        });

        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(configuration));

        Assert.Contains("RabbitMQ must be enabled", exception.Message);
    }

    [Fact]
    public void AddInfrastructure_WhenDevelopmentRequiresDependencies_AllowsLocalFallbacks()
    {
        using var environment = new TemporaryEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=vendingad;Username=test;Password=test",
            ["ProductionReadiness:RequireDistributedCache"] = "true",
            ["ProductionReadiness:RequireMessageBus"] = "true",
            ["Redis:Enabled"] = "false",
            ["RabbitMQ:Enabled"] = "false"
        });

        var exception = Record.Exception(() => new ServiceCollection().AddInfrastructure(configuration));

        Assert.Null(exception);
    }

    [Fact]
    public void AddWorkerInfrastructure_WhenProductionWithoutRedisOrRabbitMq_ThrowsClearError()
    {
        using var environment = new TemporaryEnvironment("DOTNET_ENVIRONMENT", "Production");
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=vendingad;Username=test;Password=test",
            ["Redis:Enabled"] = "false",
            ["RabbitMQ:Enabled"] = "false"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddWorkerInfrastructure(configuration));

        Assert.Contains("Redis must be enabled", exception.Message);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TemporaryEnvironment : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public TemporaryEnvironment(string name, string value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }
}
