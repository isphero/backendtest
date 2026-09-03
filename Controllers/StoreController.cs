// Controllers/StoreController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GameRealmAPI.DTOs;
using GameRealmAPI.Services;

namespace GameRealmAPI.Controllers;

[ApiController]
[Route("api/store")]
[Produces("application/json")]
public class StoreController : ControllerBase
{
    private readonly StoreService _store;
    public StoreController(StoreService store) { _store = store; }

    /// <summary>Get all store items</summary>
    [HttpGet("items")]
    public async Task<IActionResult> GetItems([FromQuery] string? category = null)
    {
        var items = await _store.GetItemsAsync(category);
        return Ok(items);
    }

    /// <summary>Get coin packages</summary>
    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages()
    {
        var packages = await _store.GetPackagesAsync();
        return Ok(packages);
    }

    /// <summary>Get my wallet balance (requires login)</summary>
    [HttpGet("wallet")]
    [Authorize]
    public async Task<IActionResult> GetWallet()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var wallet = await _store.GetWalletAsync(userId);
        return Ok(wallet);
    }

    /// <summary>Buy an item with coins (requires login)</summary>
    [HttpPost("buy")]
    [Authorize]
    public async Task<IActionResult> BuyItem([FromBody] BuyItemRequest req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _store.BuyItemAsync(userId, req.ItemId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Initiate payment for coin top-up (requires login)</summary>
    [HttpPost("pay")]
    [Authorize]
    public async Task<IActionResult> InitiatePayment([FromBody] InitiatePaymentRequest req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _store.InitiatePaymentAsync(userId, req);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Get my purchase history (requires login)</summary>
    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetHistory()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var history = await _store.GetHistoryAsync(userId);
        return Ok(history);
    }
}
