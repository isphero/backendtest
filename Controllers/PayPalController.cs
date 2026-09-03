// Controllers/PayPalController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GameRealmAPI.Services;

namespace GameRealmAPI.Controllers;

[ApiController]
[Route("api/paypal")]
[Produces("application/json")]
public class PayPalController : ControllerBase
{
    private readonly PayPalService _paypal;
    private readonly IConfiguration _config;
    private readonly ILogger<PayPalController> _logger;

    public PayPalController(PayPalService paypal, IConfiguration config,
        ILogger<PayPalController> logger)
    {
        _paypal = paypal;
        _config = config;
        _logger = logger;
    }

    /// <summary>Step 1 — اعمل PayPal order وارجع الـ approval URL</summary>
    [HttpPost("create-order")]
    [Authorize]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _paypal.CreateOrderAsync(userId, req.PackageId);

        if (!result.Success)
            return BadRequest(new { success = false, message = result.Message });

        return Ok(new
        {
            success = true,
            orderId = result.OrderId,
            approvalUrl = result.ApprovalUrl
        });
    }

    /// <summary>Step 2 — PayPal بيرجع هنا بعد الدفع (Capture + إضافة Coins)</summary>
    [HttpGet("capture")]
    public async Task<IActionResult> Capture([FromQuery] string token, [FromQuery] string? PayerID)
    {
        _logger.LogInformation("PayPal capture called. token={token} PayerID={payerId}", token, PayerID);

        var storeUrl = _config["PayPal:StoreUrl"]!;   // e.g. http://localhost:5173/store
        var result = await _paypal.CapturePaymentAsync(token);

        if (!result.Success)
        {
            _logger.LogError("Capture failed: {msg}", result.Message);
            return Redirect($"{storeUrl}?payment=failed");
        }

        return Redirect($"{storeUrl}?payment=success&coins={result.CoinsAdded}");
    }

    /// <summary>Cancel — اللاعب ضغط cancel في PayPal</summary>
    [HttpGet("cancel")]
    public IActionResult Cancel()
    {
        var storeUrl = _config["PayPal:StoreUrl"]!;
        return Redirect($"{storeUrl}?payment=cancelled");
    }
}

public record CreateOrderRequest(int PackageId);