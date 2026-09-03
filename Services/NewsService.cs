// Services/NewsService.cs
using Microsoft.EntityFrameworkCore;
using GameRealmAPI.Data;
using GameRealmAPI.DTOs;
using GameRealmAPI.Models;

namespace GameRealmAPI.Services;

public class NewsService
{
    private readonly AppDbContext _db;

    public NewsService(AppDbContext db) { _db = db; }

    // ===== GET ALL (public - published only, paginated) =====
    public async Task<NewsPagedResponse> GetPublishedAsync(int page = 1, int pageSize = 10, string? category = null)
    {
        var query = _db.NewsArticles
            .Where(n => n.IsPublished)
            .OrderByDescending(n => n.CreatedAt);

        if (!string.IsNullOrEmpty(category))
            query = (IOrderedQueryable<NewsArticle>)query.Where(n => n.Category == category);

        var total = await query.CountAsync();
        var pageSize2 = Math.Clamp(pageSize, 1, 50);
        var page2 = Math.Max(page, 1);

        var items = await query
            .Skip((page2 - 1) * pageSize2)
            .Take(pageSize2)
            .Select(n => new NewsListItemDto(
                n.Id, n.Title, n.Category, n.Excerpt,
                n.Author, n.IsPublished, n.CreatedAt))
            .ToListAsync();

        return new NewsPagedResponse(items, total, page2, pageSize2);
    }

    // ===== GET ALL (admin - includes unpublished) =====
    public async Task<NewsPagedResponse> GetAllAdminAsync(int page = 1, int pageSize = 50, string? search = null)
    {
        var query = _db.NewsArticles.OrderByDescending(n => n.CreatedAt);

        if (!string.IsNullOrEmpty(search))
        {
            var s = search.ToLower();
            query = (IOrderedQueryable<NewsArticle>)query
                .Where(n => n.Title.ToLower().Contains(s) || n.Category.ToLower().Contains(s));
        }

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NewsListItemDto(
                n.Id, n.Title, n.Category, n.Excerpt,
                n.Author, n.IsPublished, n.CreatedAt))
            .ToListAsync();

        return new NewsPagedResponse(items, total, page, pageSize);
    }

    // ===== GET SINGLE =====
    public async Task<NewsArticleDto?> GetByIdAsync(int id, bool adminView = false)
    {
        var query = _db.NewsArticles.Where(n => n.Id == id);
        if (!adminView) query = query.Where(n => n.IsPublished);

        var n = await query.FirstOrDefaultAsync();
        if (n == null) return null;

        return new NewsArticleDto(n.Id, n.Title, n.Category, n.Excerpt,
            n.Content, n.Author, n.IsPublished, n.CreatedAt, n.UpdatedAt);
    }

    // ===== CREATE =====
    public async Task<ApiResponse<NewsArticleDto>> CreateAsync(int userId, CreateNewsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return new ApiResponse<NewsArticleDto>(false, "Title is required");

        var validCategories = new[] { "PATCH", "NEWS", "EVENT", "SECURITY", "STORE" };
        if (!validCategories.Contains(req.Category.ToUpper()))
            return new ApiResponse<NewsArticleDto>(false, "Invalid category");

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return new ApiResponse<NewsArticleDto>(false, "User not found");

        var article = new NewsArticle
        {
            Title = req.Title.Trim(),
            Category = req.Category.ToUpper(),
            Excerpt = req.Excerpt.Trim(),
            Content = req.Content,
            Author = string.IsNullOrEmpty(req.Author) ? user.Username : req.Author.Trim(),
            AuthorUserId = userId,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.NewsArticles.Add(article);
        await _db.SaveChangesAsync();

        return new ApiResponse<NewsArticleDto>(true, "Article created",
            new NewsArticleDto(article.Id, article.Title, article.Category,
                article.Excerpt, article.Content, article.Author,
                article.IsPublished, article.CreatedAt, null));
    }

    // ===== UPDATE =====
    public async Task<ApiResponse<NewsArticleDto>> UpdateAsync(int id, int userId, string role, UpdateNewsRequest req)
    {
        var article = await _db.NewsArticles.FindAsync(id);
        if (article == null)
            return new ApiResponse<NewsArticleDto>(false, "Article not found");

        // Only author, Moderator or Admin can edit
        if (article.AuthorUserId != userId && role != "Admin" && role != "Moderator")
            return new ApiResponse<NewsArticleDto>(false, "Not authorized to edit this article");

        article.Title = req.Title.Trim();
        article.Category = req.Category.ToUpper();
        article.Excerpt = req.Excerpt.Trim();
        article.Content = req.Content;
        article.Author = req.Author.Trim();
        article.IsPublished = req.IsPublished;
        article.UpdatedAt = DateTime.UtcNow;

        _db.NewsArticles.Update(article);
        await _db.SaveChangesAsync();

        return new ApiResponse<NewsArticleDto>(true, "Article updated",
            new NewsArticleDto(article.Id, article.Title, article.Category,
                article.Excerpt, article.Content, article.Author,
                article.IsPublished, article.CreatedAt, article.UpdatedAt));
    }

    // ===== DELETE =====
    public async Task<ApiResponse> DeleteAsync(int id, int userId, string role)
    {
        var article = await _db.NewsArticles.FindAsync(id);
        if (article == null)
            return new ApiResponse(false, "Article not found");
        if (role == "Moderator")
            return new ApiResponse(false, "Moderators cannot delete articles. You can only toggle publishing it");

        if (article.AuthorUserId != userId && role != "Admin" && role != "Moderator")
            return new ApiResponse(false, "Not authorized to delete this article");

        _db.NewsArticles.Remove(article);
        await _db.SaveChangesAsync();

        return new ApiResponse(true, "Article deleted");
    }

    // ===== SEED =====
    public async Task SeedNewsAsync()
    {
        if (await _db.NewsArticles.AnyAsync()) return;

        var adminUser = await _db.Users.FirstOrDefaultAsync(u => u.Role == AccountRole.Admin || u.Role == AccountRole.Moderator)
                     ?? await _db.Users.FirstOrDefaultAsync();
        if (adminUser == null) return;

        _db.NewsArticles.AddRange(
            new NewsArticle
            {
                Title = "Release Notes: Patch 5.2 \"Awakening\"",
                Category = "PATCH",
                Excerpt = "Server maintenance has concluded. Version 5.2 introduces a new class balance and major bug fixes.",
                Content = "<p>Complete overhaul of the ranking calculation algorithms. New balance changes for all classes.</p>",
                Author = "ADMIN",
                AuthorUserId = adminUser.Id,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new NewsArticle
            {
                Title = "New Market District Now Open",
                Category = "NEWS",
                Excerpt = "The Twin City market district has been expanded to support up to 500 simultaneous stalls.",
                Content = "<p>The economy of the Realm is growing faster than ever. Visit the new market area!</p>",
                Author = "ADMIN",
                AuthorUserId = adminUser.Id,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow.AddDays(-4)
            },
            new NewsArticle
            {
                Title = "Easter Event: The Great Hunt",
                Category = "EVENT",
                Excerpt = "Special hidden items have appeared in Phoenix Castle! Find them all for unique rewards.",
                Content = "<p>Hunt for hidden eggs across all maps. Rewards include exclusive cosmetics!</p>",
                Author = "GM_X",
                AuthorUserId = adminUser.Id,
                IsPublished = true,
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            }
        );

        await _db.SaveChangesAsync();
    }
}
