// Controllers/NewsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;
using GameRealmAPI.DTOs;
using GameRealmAPI.Services;

namespace GameRealmAPI.Controllers;

[ApiController]
[Route("api/news")]
[Produces("application/json")]
public class NewsController : ControllerBase
{
    private readonly NewsService _news;
    public NewsController(NewsService news) { _news = news; }

    // ===== PUBLIC ENDPOINTS =====

    /// <summary>Get published articles (public, cached 2 min)</summary>
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(Duration = 120)]
    public async Task<IActionResult> GetPublished(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? category = null)
    {
        var result = await _news.GetPublishedAsync(page, pageSize, category);
        return Ok(result);
    }

    /// <summary>Get single article by ID (public)</summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [OutputCache(Duration = 120)]
    public async Task<IActionResult> GetById(int id)
    {
        var article = await _news.GetByIdAsync(id);
        if (article == null) return NotFound(new { success = false, message = "Article not found" });
        return Ok(article);
    }

    // ===== ADMIN ENDPOINTS (Admin + GM only) =====

    /// <summary>Get ALL articles including unpublished (Admin/GM)</summary>
    [HttpGet("admin/all")]
    [Authorize]
    public async Task<IActionResult> GetAllAdmin(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null)
    {
        if (!IsAdminOrGM()) return Forbid();
        var result = await _news.GetAllAdminAsync(page, pageSize, search);
        return Ok(result);
    }

    /// <summary>Get single article (admin view - includes unpublished)</summary>
    [HttpGet("admin/{id}")]
    [Authorize]
    public async Task<IActionResult> GetByIdAdmin(int id)
    {
        if (!IsAdminOrGM()) return Forbid();
        var article = await _news.GetByIdAsync(id, adminView: true);
        if (article == null) return NotFound();
        return Ok(article);
    }

    /// <summary>Create article (Admin/GM)</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateNewsRequest req)
    {
        if (!IsAdminOrGM()) return Forbid();
        var userId = GetUserId();
        var result = await _news.CreateAsync(userId, req);
        return result.Success ? Created("", result) : BadRequest(result);
    }

    /// <summary>Update article (Admin/GM/Author)</summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateNewsRequest req)
    {
        if (!IsAdminOrGM()) return Forbid();
        var userId = GetUserId();
        var role = GetRole();
        var result = await _news.UpdateAsync(id, userId, role, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Delete article (Admin/GM)</summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        if (!IsAdminOrGM()) return Forbid();
        var userId = GetUserId();
        var role = GetRole();
        var result = await _news.DeleteAsync(id, userId, role);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ===== HELPERS =====
    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    private string GetRole() =>
        User.FindFirstValue(ClaimTypes.Role) ?? "Player";

    private bool IsAdminOrGM()
    {
        var role = GetRole();
        return role == "Admin" || role == "Moderator";
    }
}
