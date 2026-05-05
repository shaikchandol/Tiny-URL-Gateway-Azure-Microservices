using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TinyUrl.AnalyticsService.Infrastructure.Consumers;
using TinyUrl.AnalyticsService.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "TinyURL Analytics Service", Version = "v1" }));

builder.Services.AddDbContext<AnalyticsDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("analytics-db")));

builder.Services.AddSingleton(_ =>
    new ServiceBusClient(
        builder.Configuration["Azure:ServiceBus:Namespace"] ?? "tinyurl-servicebus.servicebus.windows.net",
        new DefaultAzureCredential()));

builder.Services.AddHostedService<ClickEventConsumer>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>().Database.Migrate();

app.UseSerilogRequestLogging();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapDefaultEndpoints();
app.Run();
