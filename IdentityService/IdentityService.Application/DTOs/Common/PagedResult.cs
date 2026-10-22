namespace IdentityService.Application.DTOs.Common;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    // Tổng số bản ghi (để client tính total pages)
    public long TotalItems { get; set; }
    public int TotalPages =>
        PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalItems / PageSize);
}

