// DTOs/WikiDTOs.cs
namespace GameRealmAPI.DTOs;

public record WikiCategoryDto(
    int Id,
    string Name,
    string Slug,
    string? Icon,
    string? Description,
    int SortOrder,
    int? ParentId,
    List<WikiCategoryDto> Children,
    List<WikiPageListDto> Pages,
    int PageCount
);

public record WikiPageDto(
    int Id,
    string Title,
    string Slug,
    string Content,
    int? CategoryId,
    string? CategoryName,
    string? CategorySlug,
    int? ParentPageId,
    List<WikiPageListDto> ChildPages,
    string LastEditedBy,
    bool? IsPublished,
    int ViewCount,
    int SortOrder,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

public record WikiPageListDto(
    int Id,
    string Title,
    string Slug,
    string? CategorySlug,
    string? CategoryName,
    string LastEditedBy,
    DateTime? UpdatedAt,
    int ViewCount,
    int SortOrder
);

public record WikiRevisionDto(
    int Id,
    string EditedBy,
    string EditSummary,
    DateTime? CreatedAt
);

public record WikiTreeItemDto(
    int Id,
    string Title,
    string Type, // "category" or "page"
    string Slug,
    string Icon,
    int SortOrder,
    int? ParentId,
    List<WikiTreeItemDto> Children
);

// Requests
public record CreateWikiPageRequest(
    string Title,
    string Content,
    int? CategoryId,
    int? ParentPageId,
    int SortOrder,
    string? EditSummary = null,
    string? Slug = null
);

public record UpdateWikiPageRequest(
    string Title,
    string Content,
    int? CategoryId,
    int? ParentPageId,
    int SortOrder,
    bool IsPublished,
    string? EditSummary = null,
    string? Slug = null,
    List<WikiReorderRequest>? ReorderItems = null
);

public record CreateWikiCategoryRequest(
    string Name,
    string Slug,
    string? Icon,
    string? Description,
    int? ParentId,
    int SortOrder
);

public record UpdateWikiCategoryRequest(
    string Name,
    string Slug,
    string? Icon,
    string? Description,
    int? ParentId,
    int SortOrder,
    List<WikiReorderRequest>? ReorderItems = null
);

public record WikiReorderRequest(int Id, int SortOrder);

// Interactions
public record WikiCommentDto(
    int Id,
    int PageId,
    int? ParentId,
    int UserId,
    string AuthorName,
    string? AuthorAvatar,
    string Content,
    DateTime? CreatedAt,
    List<WikiCommentDto> Replies
);

public record PostCommentRequest(string Content, int? ParentId = null);

public record WikiReviewDto(
    int Id,
    int PageId,
    int UserId,
    string AuthorName,
    string? AuthorAvatar,
    string Content,
    int Rating,
    DateTime? CreatedAt
);

public record PostReviewRequest(string Content, int Rating);

public record WikiReactionDto(
    string Type,
    int Count,
    bool UserHasReacted
);

public record ToggleReactionRequest(string Type);

public record WikiReactionsResponse(
    Dictionary<string, int> Reactions,
    List<string> UserReactions
);
