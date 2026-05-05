using FluentValidation;
using MediatR;
using Serilog;
using TinyUrl.UrlService.Api.Middleware;
using TinyUrl.UrlService.Application.Behaviors;
using TinyUrl.UrlService.Infrastructure;
using TinyUrl.UrlService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "TinyURL URL Service", Version = "v1" }));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(TinyUrl.UrlService.Application.Commands.CreateUrl.CreateUrlHandler).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(TinyUrl.UrlService.Application.Commands.CreateUrl.CreateUrlValidator).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<UrlServiceDbContext>().Database.Migrate();

app.UseMiddleware<ExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapDefaultEndpoints();
app.Run();
