using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace TinyUrl.UrlService.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var failures = validators.Select(v => v.Validate(request)).SelectMany(r => r.Errors).Where(f => f != null).ToList();
        if (failures.Any()) throw new ValidationException(failures);
        return await next(ct);
    }
}

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next(ct);
        logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}
