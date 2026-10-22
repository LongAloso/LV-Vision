namespace IdentityService.Application.DTOs.Common;

public class PagedQuery
{
    private const int MaxPageSize = 300;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Tên field để sort: userName, email, createdAt, name, ...</summary>
    public string? SortBy { get; set; }

    /// <summary>"asc" hoặc "desc"</summary>
    public string? SortDirection { get; set; } = "asc";

    /// <summary>Chuỗi tìm kiếm (search text)</summary>
    public string? Search { get; set; }

    public void Normalize()
    {
        if (Page <= 0) Page = 1;
        if (PageSize <= 0) PageSize = 20;
        if (PageSize > MaxPageSize) PageSize = MaxPageSize;

        SortDirection = string.IsNullOrWhiteSpace(SortDirection)
            ? "asc"
            : SortDirection.Trim().ToLowerInvariant() switch
            {
                "desc" or "descending" or "-" => "desc",
                _ => "asc"
            };
    }
}