using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;
using GameRealmAPI.DTOs;
using GameRealmAPI.Services;

namespace GameRealmAPI.Controllers;

[ApiController]
[Route("api/market")]
[Produces("application/json")]
public class MarketController : ControllerBase
{
    private readonly MarketService _market;
    public MarketController(MarketService market) { _market = market; }

    /// <summary>Get listings with filters - 30s server cache</summary>
    [HttpGet("listings")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetListings([FromQuery] MarketFilterRequest filter)
        => Ok(await _market.GetListingsAsync(filter));

    /// <summary>Get all active stall positions for the map overlay</summary>
    [HttpGet("stalls")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetStalls()
        => Ok(await _market.GetStallMapAsync());

    /// <summary>Create a listing (auto-assigns stall to seller)</summary>
    [HttpPost("listings")]
    [Authorize]
    public async Task<IActionResult> CreateListing([FromBody] CreateListingRequest req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _market.CreateListingAsync(userId, req);
        return result.Success ? Created("", result) : BadRequest(result);
    }

    /// <summary>Remove a listing</summary>
    [HttpDelete("listings/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteListing(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "Player";
        var result = await _market.DeleteListingAsync(id, userId, role);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
