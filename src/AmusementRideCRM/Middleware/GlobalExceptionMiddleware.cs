using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using AmusementRideCRM.Models;
using AmusementRideCRM.Services;

namespace AmusementRideCRM.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext, ApplicationDbContext dbContext, IAuditUserProvider userProvider)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception intercepted by GlobalExceptionMiddleware: {Message}", ex.Message);
            await LogExceptionToDatabaseAsync(httpContext, dbContext, userProvider, ex);

            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

                bool isJsonOrApi = httpContext.Request.Headers["Accept"].ToString().Contains("application/json") ||
                                   httpContext.Request.Path.StartsWithSegments("/api");

                if (isJsonOrApi)
                {
                    httpContext.Response.ContentType = "application/json";
                    var response = new
                    {
                        error = "An unhandled error occurred.",
                        message = ex.Message,
                        correlationId = userProvider.GetCorrelationId()
                    };
                    await httpContext.Response.WriteAsJsonAsync(response);
                    return;
                }
            }

            throw;
        }
    }

    private static async Task LogExceptionToDatabaseAsync(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IAuditUserProvider userProvider,
        Exception ex)
    {
        try
        {
            var errorLog = new ErrorLogging
            {
                ErrorLogId = Guid.NewGuid().ToString(),
                ErrorDate = DateTime.UtcNow,
                Severity = "Error",
                Source = ex.Source,
                MachineName = Environment.MachineName,
                UserName = userProvider.GetUserName(),
                RequestPath = httpContext.Request.Path.Value,
                RequestMethod = httpContext.Request.Method,
                QueryString = httpContext.Request.QueryString.HasValue ? httpContext.Request.QueryString.Value : null,
                StatusCode = StatusCodes.Status500InternalServerError,
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace,
                InnerException = ex.InnerException?.ToString(),
                AdditionalData = $"TargetSite: {ex.TargetSite}",
                CorrelationId = userProvider.GetCorrelationId()
            };

            dbContext.ErrorLogging.Add(errorLog);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception loggingEx)
        {
            System.Diagnostics.Trace.TraceError($"Failed to log exception to DB: {loggingEx}");
        }
    }
}
