using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyUrl.UrlService.Application.Interfaces;
using TinyUrl.UrlService.Infrastructure.Data;
using TinyUrl.UrlService.Infrastructure.Repositories;
using TinyUrl.UrlService.Infrastructure.Services;

namespace TinyUrl.UrlService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<UrlServiceDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("urlservice-db")));

        services.AddStackExchangeRedisCache(opts =>
            opts.Configuration = config.GetConnectionString("tinyurl-redis"));

        var managedIdentity = new DefaultAzureCredential();

        services.AddSingleton(_ => new ServiceBusClient(
            config["Azure:ServiceBus:Namespace"] ?? "tinyurl-servicebus.servicebus.windows.net",
            managedIdentity));

        services.AddScoped<IUrlRepository, UrlRepository>();
        services.AddScoped<IEventPublisher, ServiceBusEventPublisher>();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddSingleton<IShortCodeGenerator, Base62ShortCodeGenerator>();

        return services;
    }
}
