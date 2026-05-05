using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Serilog;
using TinyUrl.RedirectService.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "TinyURL Redirect Service", Version = "v1" }));

builder.Services.AddStackExchangeRedisCache(opts =>
    opts.Configuration = builder.Configuration.GetConnectionString("tinyurl-redis"));

builder.Services.AddSingleton(_ =>
    new ServiceBusClient(
        builder.Configuration["Azure:ServiceBus:Namespace"] ?? "tinyurl-servicebus.servicebus.windows.net",
        new DefaultAzureCredential()));

builder.Services.AddHttpClient("url-service")
    .AddStandardResilienceHandler();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapDefaultEndpoints();
app.Run();
