// Services/PayPalService.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using GameRealmAPI.Data;
using GameRealmAPI.Models;

namespace GameRealmAPI.Services;

public class PayPalService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<PayPalService> _logger;

    private const string SandboxUrl = "https://api-m.sandbox.paypal.com";
    private const string LiveUrl = "https://api-m.paypal.com";

    private string BaseUrl => _config["PayPal:Mode"] == "live" ? LiveUrl : SandboxUrl;

    public PayPalService(IConfiguration config, AppDbContext db,
        IHttpClientFactory httpFactory, ILogger<PayPalService> logger)
    {
        _config = config;
        _db = db;
        _http = httpFactory.CreateClient("paypal");
        _logger = logger;
    }

    // ===== Get Access Token =====
    private async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var clientId = _config["PayPal:ClientId"];
            var secret = _config["PayPal:Secret"];
            var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));

            var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/oauth2/token");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
            req.Content = new StringContent("grant_type=client_credentials",
                Encoding.UTF8, "application/x-www-form-urlencoded");

            var res = await _http.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("PayPal token failed: {json}", json);
                return null;
            }

            var data = JsonSerializer.Deserialize<JsonElement>(json);
            return data.GetProperty("access_token").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAccessToken exception");
            return null;
        }
    }

    // ===== Create Order =====
    public async Task<CreateOrderResult> CreateOrderAsync(int userId, int packageId)
    {
        var package = await _db.CoinPackages.FindAsync(packageId);
        if (package == null)
            return new CreateOrderResult(false, "Package not found", null, null);

        var token = await GetAccessTokenAsync();
        if (token == null)
            return new CreateOrderResult(false, "PayPal authentication failed", null, null);

        var captureUrl = _config["PayPal:CaptureUrl"]; // API endpoint for capture
        var cancelUrl = _config["PayPal:CancelUrl"];

        var body = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    amount = new
                    {
                        currency_code = "USD",
                        value = package.Price.ToString("F2")
                    },
                    description = $"{package.Coins} GameRealm Coins",
                    custom_id   = $"{userId}:{packageId}"
                }
            },
            application_context = new
            {
                return_url = captureUrl,
                cancel_url = cancelUrl,
                brand_name = "GameRealm",
                user_action = "PAY_NOW",
                shipping_preference = "NO_SHIPPING"
            }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/checkout/orders");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();

        _logger.LogInformation("PayPal CreateOrder response: {json}", json);

        if (!res.IsSuccessStatusCode)
            return new CreateOrderResult(false, "PayPal error creating order", null, null);

        var data = JsonSerializer.Deserialize<JsonElement>(json);
        var orderId = data.GetProperty("id").GetString()!;
        var approvalUrl = data.GetProperty("links")
            .EnumerateArray()
            .First(l => l.GetProperty("rel").GetString() == "approve")
            .GetProperty("href").GetString()!;

        // Save pending payment — نحفظ الـ orderId عشان نعرفه وقت الـ capture
        var payment = new Payment
        {
            UserId = userId,
            CoinPackageId = packageId,
            Method = "paypal",
            Amount = package.Price,
            Currency = "USD",
            Status = "pending",
            ExternalTransactionId = orderId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        return new CreateOrderResult(true, "Order created", orderId, approvalUrl);
    }

    // ===== Capture Payment =====
    public async Task<CaptureResult> CapturePaymentAsync(string orderId)
    {
        // 1. جيب الـ payment record من الـ DB بالـ orderId
        //    ده أضمن من قراءة custom_id من PayPal response
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.ExternalTransactionId == orderId);

        if (payment == null)
        {
            _logger.LogError("Payment not found for orderId: {orderId}", orderId);
            return new CaptureResult(false, "Payment record not found", 0, 0);
        }

        // 2. تأكد مش اتعملت Capture قبل كده
        if (payment.Status == "completed")
        {
            _logger.LogWarning("Payment already completed: {orderId}", orderId);
            return new CaptureResult(true, "Already completed", payment.UserId, 0);
        }

        // 3. Capture من PayPal
        var token = await GetAccessTokenAsync();
        if (token == null)
            return new CaptureResult(false, "PayPal auth failed", 0, 0);

        var req = new HttpRequestMessage(
            HttpMethod.Post, $"{BaseUrl}/v2/checkout/orders/{orderId}/capture");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        var json = await res.Content.ReadAsStringAsync();

        _logger.LogInformation("PayPal Capture response: {json}", json);

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal Capture failed: {json}", json);
            payment.Status = "failed";
            await _db.SaveChangesAsync();
            return new CaptureResult(false, "Capture failed", 0, 0);
        }

        var data = JsonSerializer.Deserialize<JsonElement>(json);
        var status = data.GetProperty("status").GetString();

        if (status != "COMPLETED")
        {
            _logger.LogError("PayPal status not COMPLETED: {status}", status);
            return new CaptureResult(false, $"PayPal status: {status}", 0, 0);
        }

        // 4. اضيف الـ Coins للاعب
        var coinsAdded = await AddCoinsAsync(payment);
        return new CaptureResult(true, "Payment completed!", payment.UserId, coinsAdded);
    }

    // ===== Add Coins to Wallet =====
    private async Task<int> AddCoinsAsync(Payment payment)
    {
        var package = await _db.CoinPackages.FindAsync(payment.CoinPackageId);
        if (package == null) return 0;

        // احسب الـ Coins مع الـ Bonus
        var coins = package.Coins;
        if (package.BonusPercent > 0)
            coins += (int)(package.Coins * package.BonusPercent / 100.0);

        // جيب أو اعمل Wallet
        var wallet = await _db.PlayerWallets
            .FirstOrDefaultAsync(w => w.UserId == payment.UserId);

        if (wallet == null)
        {
            wallet = new PlayerWallet { UserId = payment.UserId, Coins = 0 };
            _db.PlayerWallets.Add(wallet);
        }

        wallet.Coins += coins;
        wallet.UpdatedAt = DateTime.UtcNow;

        // حدّث الـ Payment
        payment.Status = "completed";
        payment.CompletedAt = DateTime.UtcNow;

        // سجّل في Purchase Log
        _db.PurchaseLogs.Add(new PurchaseLog
        {
            UserId = payment.UserId,
            CoinPackageId = payment.CoinPackageId,
            Type = "topup",
            CoinsSpent = 0,
            CoinsReceived = coins,
            Status = "completed",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "✅ Added {coins} coins to userId {userId}", coins, payment.UserId);

        return coins;
    }
}

public record CreateOrderResult(bool Success, string Message, string? OrderId, string? ApprovalUrl);
public record CaptureResult(bool Success, string Message, int UserId, int CoinsAdded);