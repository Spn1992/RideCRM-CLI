using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AmusementRideCRM.Services;

public class AuditUserProvider : IAuditUserProvider
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AuditUserProvider(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? GetUserId()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext?.User != null)
        {
            var userIdClaim = httpContext.User.FindFirst("UserId")
                ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirst("sub");

            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }
        }
        return 0; // Default system user ID
    }

    public string GetUserName()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext?.User != null)
        {
            var name = httpContext.User.Identity?.Name
                ?? httpContext.User.FindFirst("UserName")?.Value
                ?? httpContext.User.FindFirst(ClaimTypes.Name)?.Value;

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        return "System";
    }

    public string? GetIpAddress()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            string? ip = httpContext.Request?.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(ip))
            {
                ip = httpContext.Connection?.RemoteIpAddress?.ToString();
            }
            if (!string.IsNullOrWhiteSpace(ip))
            {
                return ip;
            }
        }
        return "127.0.0.1";
    }

    public Guid GetCorrelationId()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext != null)
        {
            string? corrIdStr = httpContext.Request?.Headers["X-Correlation-ID"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(corrIdStr) && Guid.TryParse(corrIdStr, out var corrId))
            {
                return corrId;
            }
            if (!string.IsNullOrWhiteSpace(httpContext.TraceIdentifier) && Guid.TryParse(httpContext.TraceIdentifier, out var traceGuid))
            {
                return traceGuid;
            }
        }
        return Guid.NewGuid();
    }
}
