// Controllers/WikiController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;
using GameRealmAPI.DTOs;
using GameRealmAPI.Services;

namespace GameRealmAPI.Controllers;

[ApiController]
[Route("api/wiki")]
[Produces("application/json")]
public class WikiController : ControllerBase
{
    private readonly WikiService _wiki;
    public WikiController(WikiService wiki) { _wiki = wiki; }

    // ── PUBLIC ──────────────────────────────────────────────────────

    [HttpGet("sidebar")]
    [OutputCache(Duration = 300)] // 5 min — sidebar rarely changes
    public async Task<IActionResult> GetSidebar()
        => Ok(await _wiki.GetSidebarAsync());

    [HttpGet("category/{slug}")]
    [OutputCache(Duration = 120)]
    public async Task<IActionResult> GetCategory(string slug)
        => Ok(await _wiki.GetPagesByCategoryAsync(slug));

    [HttpGet("page/{slug}")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> GetPage(string slug)
    {
        var page = await _wiki.GetPageBySlugAsync(slug, isAdmin: false);
        if (page == null) return NotFound(new { success = false, message = "Page not found" });
        return Ok(page);
    }

    [HttpGet("page/{slug}/edit")]
    [Authorize]
    public async Task<IActionResult> GetPageForEdit(string slug)
    {
        if (!IsAdminOrGM()) return Forbid();
        var page = await _wiki.GetPageBySlugAsync(slug, isAdmin: true);
        if (page == null) return NotFound(new { success = false, message = "Page not found" });
        return Ok(page);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<object>());
        return Ok(await _wiki.SearchAsync(q, limit));
    }

    [HttpGet("page/{pageId:int}/revisions")]
    [Authorize]
    public async Task<IActionResult> GetRevisions(int pageId)
    {
        if (!IsAdminOrGM()) return Forbid();
        return Ok(await _wiki.GetRevisionsAsync(pageId));
    }

    [HttpGet("management-tree")]
    [Authorize]
    public async Task<IActionResult> GetManagementTree()
    {
        if (!IsAdminOrGM()) return Forbid();
        return Ok(await _wiki.GetManagementTreeAsync());
    }

    [HttpGet("all-pages")]
    [Authorize]
    public async Task<IActionResult> GetAllPages()
    {
        if (!IsAdminOrGM()) return Forbid();
        return Ok(await _wiki.GetAllPagesAsync());
    }

    // ── INTERACTIONS ──────────────────────────────────────────────

    [HttpGet("page/{pageId:int}/comments")]
    public async Task<IActionResult> GetComments(int pageId)
        => Ok(await _wiki.GetCommentsAsync(pageId));

    [HttpPost("page/{pageId:int}/comments")]
    [Authorize]
    public async Task<IActionResult> PostComment(int pageId, [FromBody] PostCommentRequest req)
        => Ok(await _wiki.PostCommentAsync(pageId, GetUserId(), req));

    [HttpPost("comments/{commentId:int}/replies")]
    [Authorize]
    public async Task<IActionResult> PostReply(int commentId, [FromBody] PostCommentRequest req)
    {
        req = req with { ParentId = commentId };
        // We can reuse PostCommentAsync logic but ensure pageId is correct.
        // Actually, let's look up the pageId from the comment.
        var pageId = await _wiki.GetPageIdFromCommentAsync(commentId);
        if (pageId == 0) return NotFound("Comment not found");
        return Ok(await _wiki.PostCommentAsync(pageId, GetUserId(), req));
    }

    [HttpDelete("comments/{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(int id)
        => Ok(await _wiki.DeleteCommentAsync(id, GetUserId(), GetRole()));

    [HttpGet("page/{pageId:int}/reviews")]
    public async Task<IActionResult> GetReviews(int pageId)
        => Ok(await _wiki.GetReviewsAsync(pageId));

    [HttpPost("page/{pageId:int}/reviews")]
    [Authorize]
    public async Task<IActionResult> PostReview(int pageId, [FromBody] PostReviewRequest req)
        => Ok(await _wiki.PostReviewAsync(pageId, GetUserId(), req));

    // ── REACTIONS ──────────────────────────────────────────────────

    [HttpGet("page/{pageId:int}/reactions")]
    public async Task<IActionResult> GetReactions(int pageId)
        => Ok(await _wiki.GetReactionsAsync(pageId, GetUserId()));

    [HttpPost("page/{pageId:int}/reactions/toggle")]
    [Authorize]
    public async Task<IActionResult> ToggleReaction(int pageId, [FromBody] ToggleReactionRequest req)
    {
        try
        {
            var userId = GetUserId();
            if (userId <= 0) return Unauthorized("Security token missing valid user signature.");
            
            return Ok(await _wiki.ToggleReactionAsync(pageId, userId, req.Type, GetRole()));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ── ADMIN/GM ────────────────────────────────────────────────────

    [HttpPost("page")]
    [Authorize]
    public async Task<IActionResult> CreatePage([FromBody] CreateWikiPageRequest req)
    {
        if (!IsAdminOrGM()) return Forbid();
        var result = await _wiki.CreatePageAsync(GetUserId(), GetUsername(), req);
        return result.Success ? Created("", result) : BadRequest(result);
    }

    [HttpPut("page/{id:int}")]
    [Authorize]
    public async Task<IActionResult> UpdatePage(int id, [FromBody] UpdateWikiPageRequest req)
    {
        if (!IsAdminOrGM()) return Forbid();
        var result = await _wiki.UpdatePageAsync(id, GetUserId(), GetUsername(), GetRole(), req, req.ReorderItems);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("page/{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeletePage(int id)
    {
        if (!IsAdminOrGM()) return Forbid();
        var result = await _wiki.DeletePageAsync(id, GetUserId(), GetRole());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("category")]
    [Authorize]
    public async Task<IActionResult> CreateCategory([FromBody] CreateWikiCategoryRequest req)
    {
        if (!IsAdminOrGM()) return Forbid();
        var result = await _wiki.CreateCategoryAsync(req);
        return result.Success ? Created("", result) : BadRequest(result);
    }

    [HttpGet("category/{slug}/edit")]
    [Authorize]
    public async Task<IActionResult> GetCategoryForEdit(string slug)
    {
        if (!IsAdminOrGM()) return Forbid();
        var cat = await _wiki.GetCategoryBySlugAsync(slug);
        if (cat == null) return NotFound(new { success = false, message = "Category not found" });
        return Ok(cat);
    }

    [HttpPut("category/{id:int}")]
    [Authorize]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateWikiCategoryRequest req)
    {
        if (!IsAdminOrGM()) return Forbid();
        var result = await _wiki.UpdateCategoryAsync(id, req, req.ReorderItems);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("reorder-categories")]
    [Authorize]
    public async Task<IActionResult> ReorderCategories([FromBody] List<WikiReorderRequest> items)
    {
        if (!IsAdminOrGM()) return Forbid();
        var result = await _wiki.ReorderCategoriesAsync(items);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("reorder-pages")]
    [Authorize]
    public async Task<IActionResult> ReorderPages([FromBody] List<WikiReorderRequest> items)
    {
        if (!IsAdminOrGM()) return Forbid();
        var result = await _wiki.ReorderPagesAsync(items);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("category/{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        if (!IsAdminOrGM()) return Forbid();
        var result = await _wiki.DeleteCategoryAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ── HELPERS ─────────────────────────────────────────────────────
    private int GetUserId()
    {
        // 1. Try common claim types first (fast path)
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                     ?? User.FindFirstValue("sub") 
                     ?? User.FindFirstValue("id");

        if (int.TryParse(userIdStr, out var id) && id > 0) return id;

        // 2. Fallback: Search ALL claims for anything that looks like a valid user ID (int > 0)
        // This handles cases where the claim name might be different due to middleware mapping
        foreach (var claim in User.Claims)
        {
            if (int.TryParse(claim.Value, out var val) && val > 0)
            {
                // Heuristic: User IDs in this system are typically large (like 100+) 
                // but we accept any positive int.
                return val;
            }
        }

        return 0;
    }
    private string GetUsername() => User.FindFirstValue(ClaimTypes.Name) ?? "Unknown";
    private string GetRole() => User.FindFirstValue(ClaimTypes.Role) ?? "Player";
    private bool IsAdminOrGM() => GetRole() is "Admin" or "Moderator";
}
