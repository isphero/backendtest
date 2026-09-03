// Controllers/RanksController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using GameRealmAPI.Services;

namespace GameRealmAPI.Controllers;

[ApiController]
[Route("api/ranks")]
[Produces("application/json")]
public class RanksController : ControllerBase
{
    private readonly GameService _game;

    public RanksController(GameService game)
    {
        _game = game;
    }

    /// <summary>Get top players leaderboard (cached 10 minutes)</summary>
    [HttpGet("players")]
    [OutputCache(Duration = 600)] // 10 minutes
    public async Task<IActionResult> GetTopPlayers(
        [FromQuery] int limit = 100,
        [FromQuery] int page = 1)
    {
        limit = Math.Clamp(limit, 1, 500);
        var result = await _game.GetTopPlayersAsync(page, limit);
        return Ok(result);
    }

    /// <summary>Get top guilds leaderboard (cached 10 minutes)</summary>
    [HttpGet("guilds")]
    [OutputCache(Duration = 600)] // 10 minutes
    public async Task<IActionResult> GetTopGuilds(
        [FromQuery] int limit = 50,
        [FromQuery] int page = 1)
    {
        limit = Math.Clamp(limit, 1, 200);
        var result = await _game.GetTopGuildsAsync(page, limit);
        return Ok(result);
    }
}
