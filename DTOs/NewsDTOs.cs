// DTOs/NewsDTOs.cs
namespace GameRealmAPI.DTOs;

public record NewsArticleDto(
    int Id,
    string Title,
    string Category,
    string Excerpt,
    string Content,
    string Author,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record NewsListItemDto(
    int Id,
    string Title,
    string Category,
    string Excerpt,
    string Author,
    bool IsPublished,
    DateTime CreatedAt
);

public record CreateNewsRequest(
    string Title,
    string Category,
    string Excerpt,
    string Content,
    string Author
);

public record UpdateNewsRequest(
    string Title,
    string Category,
    string Excerpt,
    string Content,
    string Author,
    bool IsPublished
);

public record NewsPagedResponse(
    List<NewsListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
