// Models/StoreModels.cs — normal int Id
namespace GameRealmAPI.Models;

public class StoreItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "🎁";
    public string Category { get; set; } = "general";
    public int PriceCoins { get; set; }
    public string? Badge { get; set; }
    public string? BadgeType { get; set; }
    public bool IsActive { get; set; } = true;
    public int Stock { get; set; } = -1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CoinPackage
{
    public int Id { get; set; }
    public int Coins { get; set; }
    public decimal Price { get; set; }
    public int BonusPercent { get; set; } = 0;
    public bool IsPopular { get; set; } = false;
    public bool IsActive { get; set; } = true;
}

public class PlayerWallet
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Coins { get; set; } = 0;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}

public class PurchaseLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? StoreItemId { get; set; }
    public int? CoinPackageId { get; set; }
    public string Type { get; set; } = "item";
    public int CoinsSpent { get; set; }
    public int CoinsReceived { get; set; }
    public string Status { get; set; } = "completed";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public StoreItem? StoreItem { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CoinPackageId { get; set; }
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "pending";
    public string? ExternalTransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public User User { get; set; } = null!;
    public CoinPackage CoinPackage { get; set; } = null!;
}

public class SeedVersion
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}
