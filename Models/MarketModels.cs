// Models/MarketModels.cs — normal int Id
namespace GameRealmAPI.Models;

public class MarketListing
{
    public int Id { get; set; }
    public int SellerUserId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Quality { get; set; } = "Normal";
    public int Plus { get; set; } = 0;
    public int Bless { get; set; } = 0;
    public string Gem1 { get; set; } = "NoSocket";
    public string Gem2 { get; set; } = "NoSocket";
    public long Price { get; set; } = 0;
    public string Currency { get; set; } = "gold";
    public bool IsActive { get; set; } = true;
    public DateTime ListedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SoldAt { get; set; }
    public User Seller { get; set; } = null!;
}

public class SellerStall
{
    public int Id { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public int StallNumber { get; set; }
    public int MapX { get; set; }
    public int MapY { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
