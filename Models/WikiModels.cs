// Models/WikiModels.cs
namespace GameRealmAPI.Models;

public class WikiCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;     // e.g. "guides", "npcs"
    public string? Icon { get; set; }                    // emoji icon
    public string? Description { get; set; }
    public int SortOrder { get; set; } = 0;
    public int? ParentId { get; set; }                   // for sub-categories
    public bool? IsVisible { get; set; } = true;

    // Navigation
    public WikiCategory? Parent { get; set; }
    public ICollection<WikiCategory> Children { get; set; } = new List<WikiCategory>();
    public ICollection<WikiPage> Pages { get; set; } = new List<WikiPage>();
}

public class WikiPage
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;     // e.g. "getting-started"
    public string Content { get; set; } = string.Empty;  // HTML content
    public int? CategoryId { get; set; }
    public int? ParentPageId { get; set; }
    public int AuthorUserId { get; set; }
    public string? LastEditedBy { get; set; }
    public bool? IsPublished { get; set; } = true;
    public int ViewCount { get; set; } = 0;
    public int SortOrder { get; set; } = 0;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public WikiCategory? Category { get; set; }
    public WikiPage? ParentPage { get; set; }
    public ICollection<WikiPage> ChildPages { get; set; } = new List<WikiPage>();
    public User AuthorUser { get; set; } = null!;
    public ICollection<WikiRevision> Revisions { get; set; } = new List<WikiRevision>();
}

public class WikiRevision
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public string Content { get; set; } = string.Empty;  // snapshot of content at this revision
    public string EditedBy { get; set; } = string.Empty;
    public string? EditSummary { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public WikiPage Page { get; set; } = null!;
}

public class WikiComment
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public int? ParentId { get; set; } // for replies
    public int UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty; // Store character name or username
    public string Content { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public WikiPage Page { get; set; } = null!;
    public WikiComment? Parent { get; set; }
    public ICollection<WikiComment> Replies { get; set; } = new List<WikiComment>();
    public User User { get; set; } = null!;
}

public class WikiReview
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public int UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Rating { get; set; } // 1-5 stars
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public WikiPage Page { get; set; } = null!;
    public User User { get; set; } = null!;
}

public class WikiReaction
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public int UserId { get; set; }
    public string ReactionType { get; set; } = string.Empty; // e.g. "heart", "fire"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public WikiPage Page { get; set; } = null!;
    public User User { get; set; } = null!;
}
