using System.Text.Json;
using FluentValidation;

namespace TinyUrl.UrlService.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (KeyNotFoundException ex) { await WriteError(ctx, 404, "NOT_FOUND", ex.Message); }
        catch (InvalidOperationException ex) { await WriteError(ctx, ex.Message.Contains("expired") ? 410 : 409, "CONFLICT", ex.Message); }
        catch (ValidationException ex) { await WriteError(ctx, 400, "VALIDATION_ERROR", string.Join("; ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (Exception ex) { logger.LogError(ex, "Unhandled exception"); await WriteError(ctx, 500, "INTERNAL_ERROR", "An unexpected error occurred."); }
    }

    private static async Task WriteError(HttpContext ctx, int status, string error, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error, message }));
    }
}
