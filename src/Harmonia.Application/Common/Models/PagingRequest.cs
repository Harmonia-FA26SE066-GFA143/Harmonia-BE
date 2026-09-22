namespace Harmonia.Application.Common.Models;

/// <summary>
/// Page/size query parameters shared by every paged list endpoint. Values are clamped here
/// rather than in a validator, so a bad query string never reaches a Skip/Take.
/// </summary>
public class PagingRequest
{
    private const int DefaultPageSize = 20;

    private const int MaxPageSize = 100;

    private int _pageNumber = 1;

    private int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? DefaultPageSize : value > MaxPageSize ? MaxPageSize : value;
    }
}
