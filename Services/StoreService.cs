// Services/StoreService.cs
using Microsoft.EntityFrameworkCore;
using GameRealmAPI.Data;
using GameRealmAPI.DTOs;
using GameRealmAPI.Models;

namespace GameRealmAPI.Services;

public class StoreService
{
    private readonly AppDbContext _db;

    public StoreService(AppDbContext db) { _db = db; }

    // ===== GET ALL ITEMS =====
    public async Task<List<StoreItemDto>> GetItemsAsync(string? category = null)
    {
        var query = _db.StoreItems.Where(i => i.IsActive);
        if (!string.IsNullOrEmpty(category) && category != "all")
            query = query.Where(i => i.Category == category);

        return await query
            .OrderBy(i => i.Category)
            .ThenBy(i => i.PriceCoins)
            .Select(i => new StoreItemDto(
                i.Id, i.Name, i.Description, i.Icon,
                i.Category, i.PriceCoins, i.Badge, i.BadgeType, i.Stock
            ))
            .ToListAsync();
    }

    // ===== GET COIN PACKAGES =====
    public async Task<List<CoinPackageDto>> GetPackagesAsync()
    {
        return await _db.CoinPackages
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .Select(p => new CoinPackageDto(
                p.Id, p.Coins, p.Price, p.BonusPercent, p.IsPopular
            ))
            .ToListAsync();
    }

    // ===== GET WALLET =====
    public async Task<WalletDto?> GetWalletAsync(int userId)
    {
        var wallet = await _db.PlayerWallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null)
        {
            // Create wallet if not exists
            wallet = new PlayerWallet { UserId = userId, Coins = 0 };
            _db.PlayerWallets.Add(wallet);
            await _db.SaveChangesAsync();
        }
        return new WalletDto(wallet.UserId, wallet.Coins);
    }

    // ===== BUY ITEM =====
    public async Task<ApiResponse> BuyItemAsync(int userId, int itemId)
    {
        var item = await _db.StoreItems.FirstOrDefaultAsync(i => i.Id == itemId && i.IsActive);
        if (item == null)
            return new ApiResponse(false, "Item not found");

        if (item.Stock == 0)
            return new ApiResponse(false, "Item is out of stock");

        var wallet = await _db.PlayerWallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null)
            return new ApiResponse(false, "Wallet not found");

        if (wallet.Coins < item.PriceCoins)
            return new ApiResponse(false, "Insufficient coins");

        // Deduct coins
        wallet.Coins -= item.PriceCoins;
        wallet.UpdatedAt = DateTime.UtcNow;

        // Reduce stock if limited
        if (item.Stock > 0) item.Stock--;

        // Log purchase
        _db.PurchaseLogs.Add(new PurchaseLog
        {
            UserId = userId,
            StoreItemId = itemId,
            Type = "item",
            CoinsSpent = item.PriceCoins,
            CoinsReceived = 0,
            Status = "completed"
        });

        _db.PlayerWallets.Update(wallet);
        _db.StoreItems.Update(item);
        await _db.SaveChangesAsync();
        return new ApiResponse(true, $"Successfully purchased {item.Name}! ({wallet.Coins} coins remaining)");
    }

    // ===== INITIATE PAYMENT (placeholder for PayPal/Card/Arcen) =====
    public async Task<ApiResponse<PaymentDto>> InitiatePaymentAsync(int userId, InitiatePaymentRequest req)
    {
        var package = await _db.CoinPackages.FindAsync(req.PackageId);
        if (package == null)
            return new ApiResponse<PaymentDto>(false, "Package not found");

        // Create pending payment record
        var payment = new Payment
        {
            UserId = userId,
            CoinPackageId = req.PackageId,
            Method = req.Method,
            Amount = package.Price,
            Currency = "USD",
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // TODO: Integrate payment gateway here
        // PayPal: redirect to PayPal checkout
        // Card: Stripe/PayTabs integration
        // Arcen: Arcen API call

        return new ApiResponse<PaymentDto>(true, "Payment initiated (coming soon)",
            new PaymentDto(
                payment.Id,
                package.Id,
                $"{package.Coins} Coins",
                req.Method,
                package.Price,
                "pending",
                payment.CreatedAt
            ));
    }

    // ===== GET PURCHASE HISTORY =====
    public async Task<List<PurchaseLogDto>> GetHistoryAsync(int userId)
    {
        return await _db.PurchaseLogs
            .Include(l => l.StoreItem)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(50)
            .Select(l => new PurchaseLogDto(
                l.Id,
                l.Type,
                l.StoreItem != null ? l.StoreItem.Name : "Coin Top-up",
                l.CoinsSpent,
                l.CoinsReceived,
                l.Status,
                l.CreatedAt
            ))
            .ToListAsync();
    }

    // ===== SEED DEFAULT DATA =====
    public async Task SeedDefaultDataAsync()
    {
        if (!await _db.StoreItems.AnyAsync())
        {
            _db.StoreItems.AddRange(
                new StoreItem { Name = "VIP Status", Description = "30 days VIP with exclusive benefits", Icon = "👑", Category = "premium", PriceCoins = 200, Badge = "Hot", BadgeType = "orange" },
                new StoreItem { Name = "EXP Boost x2", Description = "Double experience for 7 days", Icon = "⚡", Category = "boosts", PriceCoins = 100 },
                new StoreItem { Name = "Drop Rate +50%", Description = "Increase item drops for 3 days", Icon = "🎁", Category = "boosts", PriceCoins = 75 },
                new StoreItem { Name = "Name Color", Description = "Custom name color in chat", Icon = "🎨", Category = "cosmetics", PriceCoins = 50, Badge = "New", BadgeType = "green" },
                new StoreItem { Name = "Wing Set", Description = "Exclusive cosmetic wings", Icon = "🦋", Category = "cosmetics", PriceCoins = 300 },
                new StoreItem { Name = "Rare Sword", Description = "+100 Attack legendary weapon", Icon = "⚔️", Category = "equipment", PriceCoins = 500, Badge = "Rare", BadgeType = "orange" },
                new StoreItem { Name = "Dragon Shield", Description = "+200 Defense epic shield", Icon = "🛡️", Category = "equipment", PriceCoins = 450 },
                new StoreItem { Name = "Teleport Scroll x10", Description = "Instantly teleport anywhere", Icon = "📜", Category = "boosts", PriceCoins = 30 }
            );
        }

        if (!await _db.CoinPackages.AnyAsync())
        {
            _db.CoinPackages.AddRange(
                new CoinPackage { Coins = 100, Price = 1.00m, BonusPercent = 0 },
                new CoinPackage { Coins = 600, Price = 5.00m, BonusPercent = 20 },
                new CoinPackage { Coins = 1400, Price = 10.00m, BonusPercent = 40, IsPopular = true },
                new CoinPackage { Coins = 3000, Price = 20.00m, BonusPercent = 50 },
                new CoinPackage { Coins = 8000, Price = 50.00m, BonusPercent = 60 }
            );
        }

        await _db.SaveChangesAsync();
    }
}
