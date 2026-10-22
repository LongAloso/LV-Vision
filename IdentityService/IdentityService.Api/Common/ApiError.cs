using System.Collections.Generic;

namespace IdentityService.Api.Common;

public sealed class ApiError
{
    public string TraceId { get; set; } = default!;
    public int Status { get; set; }
    public string Code { get; set; } = default!;
    public string Message { get; set; } = default!;
    public IDictionary<string, string[]>? Errors { get; set; }
}