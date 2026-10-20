using System;

namespace AmusementRideCRM.Services;

public interface IAuditUserProvider
{
    int? GetUserId();
    string GetUserName();
    string? GetIpAddress();
    Guid GetCorrelationId();
}
