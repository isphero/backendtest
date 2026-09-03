// Controllers/StatsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using GameRealmAPI.Services;

namespace GameRealmAPI.Controllers;

[ApiController]
[Route("api/stats")]
[Produces("application/json")]
public class StatsController : ControllerBase
{
    private readonly GameService _game;

    public StatsController(GameService game)
    {
        _game = game;
    }

    /// <summary>Get home page stats (cached 2 seconds)</summary>
    [HttpGet("home")]
    [OutputCache(Duration = 2)] // 2 seconds server-side cache
    public async Task<IActionResult> GetHomeStats()
    {
        var stats = await _game.GetHomeStatsAsync();
        return Ok(stats);
    }

    /// <summary>Get detailed server stats (cached 2 minutes)</summary>
    [HttpGet("server")]
    [OutputCache(Duration = 120)] // 2 minutes
    public async Task<IActionResult> GetServerStats()
    {
        var stats = await _game.GetServerStatsAsync();
        return Ok(stats);
    }

    /// <summary>Get current online player count (cached 1 minute)</summary>
    [HttpGet("online")]
    [OutputCache(Duration = 60)] // 1 minute
    public async Task<IActionResult> GetOnlineCount()
    {
        var count = await _game.GetOnlineCountAsync();
        return Ok(new { onlinePlayers = count });
    }
}
