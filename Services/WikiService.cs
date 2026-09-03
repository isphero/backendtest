// Services/WikiService.cs
using Microsoft.EntityFrameworkCore;
using GameRealmAPI.Data;
using GameRealmAPI.DTOs;
using GameRealmAPI.Models;

namespace GameRealmAPI.Services;

public class WikiService
{
    private readonly AppDbContext _db;

    public WikiService(AppDbContext db) { _db = db; }

    // ===== GET FULL SIDEBAR (recursive tree for public view) =====
    public async Task<List<WikiTreeItemDto>> GetSidebarAsync()
    {
        var allCats = await _db.WikiCategories.AsNoTracking().Where(c => c.IsVisible == true).OrderBy(c => c.SortOrder).ToListAsync();
        var allPages = await _db.WikiPages.AsNoTracking().Where(p => p.IsPublished == true).OrderBy(p => p.SortOrder).ToListAsync();
        return BuildPublicTree(null, null, allCats, allPages);
    }

    private List<WikiTreeItemDto> BuildPublicTree(int? parentCatId, int? parentPageId, List<WikiCategory> allCats, List<WikiPage> allPages)
    {
        var items = new List<WikiTreeItemDto>();
        if (parentPageId == null) {
            foreach (var cat in allCats.Where(c => c.ParentId == parentCatId)) {
                items.Add(new WikiTreeItemDto(cat.Id, cat.Name, "category", cat.Slug, cat.Icon ?? "📁", cat.SortOrder, cat.ParentId, BuildPublicTree(cat.Id, null, allCats, allPages)));
            }
        }
        var pagesInThisLevel = allPages.Where(p => p.ParentPageId == parentPageId);
        if (parentPageId == null) pagesInThisLevel = pagesInThisLevel.Where(p => p.CategoryId == parentCatId);
        foreach (var page in pagesInThisLevel) {
            items.Add(new WikiTreeItemDto(page.Id, page.Title, "page", page.Slug, "📄", page.SortOrder, page.CategoryId, BuildPublicTree(null, page.Id, allCats, allPages)));
        }
        return items;
    }

    // ===== GET MANAGEMENT TREE =====
    public async Task<List<WikiTreeItemDto>> GetManagementTreeAsync()
    {
        var allCats = await _db.WikiCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync();
        var allPages = await _db.WikiPages.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        return BuildManagementTree(null, null, allCats, allPages);
    }

    private List<WikiTreeItemDto> BuildManagementTree(int? parentCatId, int? parentPageId, List<WikiCategory> allCats, List<WikiPage> allPages)
    {
        var items = new List<WikiTreeItemDto>();
        if (parentPageId == null) {
            foreach (var cat in allCats.Where(c => c.ParentId == parentCatId)) {
                var title = cat.IsVisible == true ? cat.Name : $"{cat.Name} (Hidden)";
                items.Add(new WikiTreeItemDto(cat.Id, title, "category", cat.Slug, cat.Icon ?? "📁", cat.SortOrder, cat.ParentId, BuildManagementTree(cat.Id, null, allCats, allPages)));
            }
        }
        var pagesInThisLevel = allPages.Where(p => p.ParentPageId == parentPageId);
        if (parentPageId == null) pagesInThisLevel = pagesInThisLevel.Where(p => p.CategoryId == parentCatId);
        foreach (var page in pagesInThisLevel) {
            var title = page.IsPublished == true ? page.Title : $"{page.Title} [DRAFT]";
            items.Add(new WikiTreeItemDto(page.Id, title, "page", page.Slug, "📄", page.SortOrder, page.CategoryId, BuildManagementTree(null, page.Id, allCats, allPages)));
        }
        return items;
    }

    public async Task<WikiCategoryDto?> GetPagesByCategoryAsync(string categorySlug)
    {
        var cat = await _db.WikiCategories.Include(c => c.Children).Include(c => c.Pages).FirstOrDefaultAsync(c => c.Slug == categorySlug);
        if (cat == null) return null;
        var pages = cat.Pages.Where(p => p.IsPublished == true).Select(p => new WikiPageListDto(p.Id, p.Title, p.Slug, cat.Slug, cat.Name, p.LastEditedBy ?? "Unknown", p.UpdatedAt, p.ViewCount, p.SortOrder)).ToList();
        return new WikiCategoryDto(cat.Id, cat.Name, cat.Slug, cat.Icon, cat.Description, cat.SortOrder, cat.ParentId, cat.Children.Select(c => new WikiCategoryDto(c.Id, c.Name, c.Slug, c.Icon, null, c.SortOrder, c.ParentId, new(), new(), 0)).ToList(), pages, pages.Count);
    }

    public async Task<WikiPageDto?> GetPageBySlugAsync(string slug, bool isAdmin = false)
    {
        var query = _db.WikiPages.Include(p => p.Category).Include(p => p.ChildPages).Where(p => p.Slug == slug);
        if (!isAdmin) query = query.Where(p => p.IsPublished == true);
        var page = await query.FirstOrDefaultAsync();
        if (page == null) return null;
        await IncrementViewCountAsync(page.Id);
        return ToDto(page);
    }

    public async Task<List<WikiPageListDto>> GetAllPagesAsync()
    {
        return await _db.WikiPages.AsNoTracking().Include(p => p.Category).OrderBy(p => p.SortOrder).Select(p => new WikiPageListDto(p.Id, p.Title, p.Slug, p.Category != null ? p.Category.Slug : null, p.Category != null ? p.Category.Name : null, p.LastEditedBy ?? "Unknown", p.UpdatedAt, p.ViewCount, p.SortOrder)).ToListAsync();
    }

    public async Task<List<WikiPageListDto>> SearchAsync(string query, int limit = 20)
    {
        var q = query.ToLower();
        return await _db.WikiPages.Include(p => p.Category).Where(p => p.IsPublished == true && (p.Title.ToLower().Contains(q) || p.Content.ToLower().Contains(q))).OrderByDescending(p => p.Title.ToLower().StartsWith(q)).ThenBy(p => p.Title).Take(limit).Select(p => new WikiPageListDto(p.Id, p.Title, p.Slug, p.Category != null ? p.Category.Slug : null, p.Category != null ? p.Category.Name : null, p.LastEditedBy ?? "Unknown", p.UpdatedAt, p.ViewCount, p.SortOrder)).ToListAsync();
    }

    public async Task<ApiResponse<WikiPageDto>> CreatePageAsync(int userId, string username, CreateWikiPageRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return Fail<WikiPageDto>("Title is required");
        var slug = string.IsNullOrEmpty(req.Slug) ? Slugify(req.Title) : req.Slug.ToLower().Trim();
        if (await _db.WikiPages.AnyAsync(p => p.Slug == slug)) return Fail<WikiPageDto>("A page with this slug already exists");
        var page = new WikiPage { Title = req.Title.Trim(), Slug = slug, Content = req.Content, CategoryId = req.CategoryId, ParentPageId = req.ParentPageId, AuthorUserId = userId, LastEditedBy = username, IsPublished = true, SortOrder = req.SortOrder, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.WikiPages.Add(page);
        await _db.SaveChangesAsync();
        _db.WikiRevisions.Add(new WikiRevision { PageId = page.Id, Content = req.Content, EditedBy = username, EditSummary = req.EditSummary ?? "Page created", CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        var full = await _db.WikiPages.Include(p => p.Category).FirstAsync(p => p.Id == page.Id);
        return new ApiResponse<WikiPageDto>(true, "Page created", ToDto(full));
    }

    public async Task<ApiResponse<WikiPageDto>> UpdatePageAsync(int pageId, int userId, string username, string role, UpdateWikiPageRequest req, List<WikiReorderRequest>? reorderItems = null)
    {
        var page = await _db.WikiPages.AsTracking().Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == pageId);
        if (page == null) return Fail<WikiPageDto>("Page not found");
        if (page.AuthorUserId != userId && role != "Admin" && role != "Moderator") return Fail<WikiPageDto>("Not authorized to edit this page");
        _db.WikiRevisions.Add(new WikiRevision { PageId = page.Id, Content = page.Content, EditedBy = username, EditSummary = req.EditSummary ?? "Position/Meta updated", CreatedAt = DateTime.UtcNow });
        page.Title = req.Title.Trim();
        page.Slug = string.IsNullOrWhiteSpace(req.Slug) ? Slugify(req.Title) : req.Slug.ToLower().Trim();
        page.Content = req.Content;
        page.CategoryId = req.CategoryId;
        page.ParentPageId = req.ParentPageId;
        page.IsPublished = req.IsPublished;
        page.SortOrder = req.SortOrder;
        page.LastEditedBy = username;
        page.UpdatedAt = DateTime.UtcNow;
        if (reorderItems != null && reorderItems.Any()) {
            foreach (var item in reorderItems) await _db.Database.ExecuteSqlRawAsync("UPDATE WikiPages SET SortOrder = {0} WHERE Id = {1}", item.SortOrder, item.Id);
        }
        await _db.SaveChangesAsync();
        return new ApiResponse<WikiPageDto>(true, "Page updated", ToDto(page));
    }

    public async Task<ApiResponse> DeletePageAsync(int pageId, int userId, string role)
    {
        var page = await _db.WikiPages.AsTracking().FirstOrDefaultAsync(p => p.Id == pageId);
        if (page == null) return new ApiResponse(false, "Page not found");
        if (page.AuthorUserId != userId && role != "Admin" && role != "Moderator") return new ApiResponse(false, "Not authorized");
        _db.WikiPages.Remove(page);
        await _db.SaveChangesAsync();
        return new ApiResponse(true, "Page deleted");
    }

    public async Task<List<WikiRevisionDto>> GetRevisionsAsync(int pageId)
    {
        return await _db.WikiRevisions.Where(r => r.PageId == pageId).OrderByDescending(r => r.CreatedAt).Take(20).Select(r => new WikiRevisionDto(r.Id, r.EditedBy, r.EditSummary, r.CreatedAt)).ToListAsync();
    }

    public async Task<ApiResponse<WikiCategoryDto>> CreateCategoryAsync(CreateWikiCategoryRequest req)
    {
        var slug = req.Slug.ToLower().Trim();
        if (await _db.WikiCategories.AnyAsync(c => c.Slug == slug)) return Fail<WikiCategoryDto>("Slug already in use");
        var cat = new WikiCategory { Name = req.Name.Trim(), Slug = slug, Icon = req.Icon, Description = req.Description, ParentId = req.ParentId, SortOrder = req.SortOrder };
        _db.WikiCategories.Add(cat);
        await _db.SaveChangesAsync();
        return new ApiResponse<WikiCategoryDto>(true, "Category created", new WikiCategoryDto(cat.Id, cat.Name, cat.Slug, cat.Icon, cat.Description, cat.SortOrder, cat.ParentId, new List<WikiCategoryDto>(), new List<WikiPageListDto>(), 0));
    }

    public async Task<WikiCategoryDto?> GetCategoryBySlugAsync(string slug)
    {
        var cat = await _db.WikiCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug);
        if (cat == null) return null;
        return new WikiCategoryDto(cat.Id, cat.Name, cat.Slug, cat.Icon, cat.Description, cat.SortOrder, cat.ParentId, new List<WikiCategoryDto>(), new List<WikiPageListDto>(), 0);
    }

    public async Task<ApiResponse<WikiCategoryDto>> UpdateCategoryAsync(int id, UpdateWikiCategoryRequest req, List<WikiReorderRequest>? reorderItems = null)
    {
        var cat = await _db.WikiCategories.AsTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (cat == null) return Fail<WikiCategoryDto>("Category not found");
        var slug = req.Slug.ToLower().Trim();
        if (cat.Slug != slug && await _db.WikiCategories.AnyAsync(c => c.Slug == slug)) return Fail<WikiCategoryDto>("Slug already in use");
        cat.Name = req.Name.Trim();
        cat.Slug = slug;
        cat.Icon = req.Icon;
        cat.Description = req.Description;
        cat.ParentId = req.ParentId;
        cat.SortOrder = req.SortOrder;
        if (reorderItems != null && reorderItems.Any()) {
            foreach (var item in reorderItems) await _db.Database.ExecuteSqlRawAsync("UPDATE WikiCategories SET SortOrder = {0} WHERE Id = {1}", item.SortOrder, item.Id);
        }
        await _db.SaveChangesAsync();
        return new ApiResponse<WikiCategoryDto>(true, "Category updated", new WikiCategoryDto(cat.Id, cat.Name, cat.Slug, cat.Icon, cat.Description, cat.SortOrder, cat.ParentId, new List<WikiCategoryDto>(), new List<WikiPageListDto>(), 0));
    }

    public async Task<ApiResponse> ReorderCategoriesAsync(List<WikiReorderRequest> items)
    {
        foreach (var item in items) await _db.Database.ExecuteSqlRawAsync("UPDATE WikiCategories SET SortOrder = {0} WHERE Id = {1}", item.SortOrder, item.Id);
        return new ApiResponse(true, "Categories reordered");
    }

    public async Task<ApiResponse> ReorderPagesAsync(List<WikiReorderRequest> items)
    {
        foreach (var item in items) await _db.Database.ExecuteSqlRawAsync("UPDATE WikiPages SET SortOrder = {0} WHERE Id = {1}", item.SortOrder, item.Id);
        return new ApiResponse(true, "Pages reordered");
    }

    public async Task<ApiResponse> DeleteCategoryAsync(int id)
    {
        var cat = await _db.WikiCategories.AsTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (cat == null) return new ApiResponse(false, "Category not found");
        var hasPages = await _db.WikiPages.AnyAsync(p => p.CategoryId == id);
        if (hasPages) return new ApiResponse(false, "Cannot delete category that contains pages. Move them first.");
        _db.WikiCategories.Remove(cat);
        await _db.SaveChangesAsync();
        return new ApiResponse(true, "Category deleted");
    }

    public async Task SeedWikiAsync()
    {
        if (await _db.WikiCategories.AnyAsync()) return;
        var adminUser = await _db.Users.FirstOrDefaultAsync(u => u.Role == AccountRole.Admin) ?? await _db.Users.FirstOrDefaultAsync();
        if (adminUser == null) return;
        var categories = new List<WikiCategory> { new() { Name="Guides", Slug="guides", Icon="📖", SortOrder=1 }, new() { Name="Features", Slug="features", Icon="⭐", SortOrder=2 }, new() { Name="NPCs", Slug="npcs", Icon="👤", SortOrder=3 }, new() { Name="Quests", Slug="quests", Icon="📜", SortOrder=4 }, new() { Name="Events", Slug="events", Icon="🎉", SortOrder=5 }, new() { Name="Management", Slug="management", Icon="⚙️", SortOrder=6 } };
        _db.WikiCategories.AddRange(categories);
        await _db.SaveChangesAsync();
        var guidesId = categories.First(c => c.Slug == "guides").Id;
        var pages = new List<WikiPage> { new() { Title="Getting Started", Slug="getting-started", Content="<h2>Welcome to GameRealm!</h2><p>This guide will help you get started on your adventure.</p><h3>First Steps</h3><ul><li>Download the game client</li><li>Create your account</li><li>Choose your class</li></ul>", CategoryId=guidesId, AuthorUserId=adminUser.Id, LastEditedBy=adminUser.Username, IsPublished=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow }, new() { Title="Game Rules", Slug="game-rules", Content="<h2>Game Rules</h2><p>Please read and follow all server rules to ensure a fair experience for everyone.</p><h3>General Rules</h3><ol><li>Respect all players</li><li>No cheating or exploiting bugs</li><li>No offensive language</li></ol>", CategoryId=guidesId, AuthorUserId=adminUser.Id, LastEditedBy=adminUser.Username, IsPublished=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow }, new() { Title="FAQ", Slug="faq", Content="<h2>Frequently Asked Questions</h2><h3>How do I reset my password?</h3><p>Go to the login page and click 'Forgot Password'.</p><h3>How do I buy coins?</h3><p>Visit the Store page and choose a coin package.</p>", CategoryId=guidesId, AuthorUserId=adminUser.Id, LastEditedBy=adminUser.Username, IsPublished=true, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow } };
        _db.WikiPages.AddRange(pages);
        await _db.SaveChangesAsync();
    }

    private static WikiPageDto ToDto(WikiPage p) => new(p.Id, p.Title, p.Slug, p.Content, p.CategoryId, p.Category?.Name, p.Category?.Slug, p.ParentPageId, p.ChildPages.Select(c => new WikiPageListDto(c.Id, c.Title, c.Slug, null, null, c.LastEditedBy ?? "Unknown", c.UpdatedAt, c.ViewCount, c.SortOrder)).ToList(), p.LastEditedBy ?? "Unknown", p.IsPublished, p.ViewCount, p.SortOrder, p.CreatedAt, p.UpdatedAt);

    private static ApiResponse<T> Fail<T>(string msg) => new(false, msg);

    private static string Slugify(string title) => System.Text.RegularExpressions.Regex.Replace(title.ToLower().Trim(), @"[^a-z0-9]+", "-").Trim('-');

    public async Task<int> GetPageIdFromCommentAsync(int commentId) => await _db.WikiComments.Where(c => c.Id == commentId).Select(c => c.PageId).FirstOrDefaultAsync();

    private async Task IncrementViewCountAsync(int pageId) => await _db.WikiPages.Where(p => p.Id == pageId).ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, p => p.ViewCount + 1));

    // ── INTERACTIONS (Restricted to Players) ───────────────────────

    public async Task<List<WikiCommentDto>> GetCommentsAsync(int pageId)
    {
        var allComments = await _db.WikiComments.AsNoTracking().Where(c => c.PageId == pageId).OrderBy(c => c.CreatedAt).ToListAsync();
        var userIds = allComments.Select(c => c.UserId).Distinct().ToList();
        var players = await _db.Players.AsNoTracking().Where(p => userIds.Contains(p.UserId) && p.Id > 0).ToDictionaryAsync(p => p.UserId, p => p.Face);
        return BuildCommentTree(null, allComments, players);
    }

    private List<WikiCommentDto> BuildCommentTree(int? parentId, List<WikiComment> all, Dictionary<int, int> playerFaces)
    {
        return all.Where(c => c.ParentId == parentId).Select(c => new WikiCommentDto(
            c.Id, c.PageId, c.ParentId, c.UserId, c.AuthorName, 
            playerFaces.TryGetValue(c.UserId, out var face) ? $"/src/assets/{face}.jpg" : "/default-avatar.png",
            c.Content, c.CreatedAt, BuildCommentTree(c.Id, all, playerFaces)
        )).ToList();
    }

    public async Task<ApiResponse<WikiCommentDto>> PostCommentAsync(int pageId, int userId, PostCommentRequest req)
    {
        if (userId <= 0) return new ApiResponse<WikiCommentDto>(false, "Unauthorized signal origin.");
        var player = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.Id > 0);
        if (player == null) return new ApiResponse<WikiCommentDto>(false, "In-game character required to post comments.");
        var comment = new WikiComment { PageId = pageId, UserId = userId, AuthorName = player.Name, Content = req.Content, ParentId = req.ParentId, CreatedAt = DateTime.UtcNow };
        _db.WikiComments.Add(comment);
        await _db.SaveChangesAsync();
        return new ApiResponse<WikiCommentDto>(true, "Comment posted", new WikiCommentDto(comment.Id, comment.PageId, comment.ParentId, userId, player.Name, $"/src/assets/{player.Face}.jpg", comment.Content, comment.CreatedAt, new()));
    }

    public async Task<ApiResponse<WikiCommentDto>> PostReplyAsync(int commentId, int userId, PostCommentRequest req)
    {
        if (userId <= 0) return new ApiResponse<WikiCommentDto>(false, "Unauthorized signal origin.");
        var player = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.Id > 0);
        if (player == null) return new ApiResponse<WikiCommentDto>(false, "In-game character required to post replies.");
        var parent = await _db.WikiComments.FindAsync(commentId);
        if (parent == null) return new ApiResponse<WikiCommentDto>(false, "Original comment not found");
        var reply = new WikiComment { PageId = parent.PageId, UserId = userId, AuthorName = player.Name, Content = req.Content, ParentId = commentId, CreatedAt = DateTime.UtcNow };
        _db.WikiComments.Add(reply);
        await _db.SaveChangesAsync();
        return new ApiResponse<WikiCommentDto>(true, "Reply posted", new WikiCommentDto(reply.Id, reply.PageId, reply.ParentId, userId, player.Name, $"/src/assets/{player.Face}.jpg", reply.Content, reply.CreatedAt, new()));
    }

    public async Task<List<WikiReviewDto>> GetReviewsAsync(int pageId)
    {
        var reviews = await _db.WikiReviews.AsNoTracking().Where(r => r.PageId == pageId).OrderByDescending(r => r.CreatedAt).ToListAsync();
        var userIds = reviews.Select(r => r.UserId).Distinct().ToList();
        var players = await _db.Players.AsNoTracking().Where(p => userIds.Contains(p.UserId) && p.Id > 0).ToDictionaryAsync(p => p.UserId, p => p.Face);
        return reviews.Select(r => new WikiReviewDto(r.Id, r.PageId, r.UserId, r.AuthorName, players.TryGetValue(r.UserId, out var face) ? $"/src/assets/{face}.jpg" : "/default-avatar.png", r.Content, r.Rating, r.CreatedAt)).ToList();
    }

    public async Task<ApiResponse<WikiReviewDto>> PostReviewAsync(int pageId, int userId, PostReviewRequest req)
    {
        if (userId <= 0) return new ApiResponse<WikiReviewDto>(false, "Unauthorized signal origin.");
        var player = await _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.Id > 0);
        if (player == null) return new ApiResponse<WikiReviewDto>(false, "In-game character required to post reviews.");
        var existing = await _db.WikiReviews.FirstOrDefaultAsync(r => r.PageId == pageId && r.UserId == userId);
        if (existing != null) return new ApiResponse<WikiReviewDto>(false, "You have already reviewed this page");
        var review = new WikiReview { PageId = pageId, UserId = userId, AuthorName = player.Name, Content = req.Content, Rating = req.Rating, CreatedAt = DateTime.UtcNow };
        _db.WikiReviews.Add(review);
        await _db.SaveChangesAsync();
        return new ApiResponse<WikiReviewDto>(true, "Review posted", new WikiReviewDto(review.Id, review.PageId, userId, player.Name, $"/src/assets/{player.Face}.jpg", review.Content, review.Rating, review.CreatedAt));
    }

    public async Task<ApiResponse> DeleteCommentAsync(int commentId, int userId, string role)
    {
        var comment = await _db.WikiComments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment == null) return new ApiResponse(false, "Comment not found");
        if (comment.UserId != userId && role != "Admin" && role != "Moderator") return new ApiResponse(false, "Not authorized");
        _db.WikiComments.Remove(comment);
        await _db.SaveChangesAsync();
        return new ApiResponse(true, "Comment deleted");
    }

    // ── REACTIONS ────────────────────────────────────────────────────

    public async Task<WikiReactionsResponse> GetReactionsAsync(int pageId, int userId)
    {
        var allReactions = await _db.WikiReactions.AsNoTracking()
            .Where(r => r.PageId == pageId)
            .ToListAsync();

        // Match frontend types exactly
        var types = new[] { "heart", "fire", "like", "heart_eyes", "100", "party", "salute", "success" };
        
        var reactionCounts = types.ToDictionary(
            t => t,
            t => allReactions.Count(r => r.ReactionType == t)
        );

        // Also include any other types that might exist in DB but aren't in our "standard" list (for safety)
        var extraTypes = allReactions
            .Select(r => r.ReactionType)
            .Distinct()
            .Where(t => !types.Contains(t));
            
        foreach (var t in extraTypes)
        {
            reactionCounts[t] = allReactions.Count(r => r.ReactionType == t);
        }

        var userReactions = allReactions
            .Where(r => r.UserId == userId)
            .Select(r => r.ReactionType)
            .ToList();

        return new WikiReactionsResponse(reactionCounts, userReactions);
    }

    public async Task<WikiReactionsResponse> ToggleReactionAsync(int pageId, int userId, string type, string role)
    {
        if (userId <= 0) throw new UnauthorizedAccessException("User identification failed.");

        // Check if page exists first
        if (!await _db.WikiPages.AnyAsync(p => p.Id == pageId))
            throw new KeyNotFoundException("Archive node not found.");

        bool isStaff = role == "Admin" || role == "Moderator";

        // IMPORTANT: Must use AsTracking() because the DbContext has NoTracking as default
        var existingOfSameType = await _db.WikiReactions
            .AsTracking()
            .FirstOrDefaultAsync(r => r.PageId == pageId && r.UserId == userId && r.ReactionType == type);

        if (existingOfSameType != null)
        {
            // If it's the same type, always remove it (Toggle Off)
            _db.WikiReactions.Remove(existingOfSameType);
        }
        else
        {
            // If the user is NOT staff, they can only have ONE reaction per page.
            // Remove any other reactions they might have before adding the new one.
            if (!isStaff)
            {
                var otherReactions = await _db.WikiReactions
                    .AsTracking()
                    .Where(r => r.PageId == pageId && r.UserId == userId)
                    .ToListAsync();
                
                if (otherReactions.Any())
                {
                    _db.WikiReactions.RemoveRange(otherReactions);
                }
            }

            // Add the new reaction
            _db.WikiReactions.Add(new WikiReaction
            {
                PageId = pageId,
                UserId = userId,
                ReactionType = type,
                CreatedAt = DateTime.UtcNow
            });
        }
        
        // Ensure changes are detected even if AutoDetectChanges is off
        _db.ChangeTracker.DetectChanges();
        await _db.SaveChangesAsync();
        
        return await GetReactionsAsync(pageId, userId);
    }
}
