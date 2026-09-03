// Services/MarketService.cs
using Microsoft.EntityFrameworkCore;
using GameRealmAPI.Data;
using GameRealmAPI.DTOs;
using GameRealmAPI.Models;

namespace GameRealmAPI.Services;

public class MarketService
{
    private readonly AppDbContext _db;

    private static readonly (int X, int Y)[] StallPositions =
   [
       (112, 155), (168, 175), (224, 155),   // top-left row
        (290, 155), (346, 175), (402, 155),   // top-right row
        (112, 230), (112, 295),               // left column
        (402, 230), (402, 295),               // right column
        (112, 340), (168, 355), (224, 340),   // bottom-left row
        (290, 340), (346, 355), (402, 340),   // bottom-right row
        (185, 230), (185, 295),               // inner-left
        (325, 230), (325, 295),               // inner-right
        (145, 215), (145, 265), (145, 315),   // extra left
        (255, 195), (255, 245), (255, 315),   // extra center
        (365, 215), (365, 265), (365, 315),   // extra right
        (210, 355), (305, 355), (258, 370),   // extra bottom
    ];


    public MarketService(AppDbContext db) { _db = db; }


    // ===== GET LISTINGS WITH FILTERS =====
    public async Task<MarketPagedResponse> GetListingsAsync(MarketFilterRequest filter)
    {
        var query = _db.MarketListings.Where(l => l.IsActive);

        if (!string.IsNullOrEmpty(filter.Category))
            query = query.Where(l => l.Category == filter.Category);

        if (!string.IsNullOrEmpty(filter.Name))
            query = query.Where(l => l.ItemName.Contains(filter.Name));

        if (!string.IsNullOrEmpty(filter.Quality))
            query = query.Where(l => l.Quality == filter.Quality);

        if (filter.Plus.HasValue)
            query = query.Where(l => l.Plus == filter.Plus.Value);

        if (filter.Bless.HasValue)
            query = query.Where(l => l.Bless == filter.Bless.Value);

        query = filter.SortBy switch
        {
            "price_asc" => query.OrderBy(l => l.Price),
            "price_desc" => query.OrderByDescending(l => l.Price),
            _ => query.OrderByDescending(l => l.ListedAt)
        };

        var total = await query.CountAsync();
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Max(filter.Page, 1);

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get stall positions for all unique sellers in this result
        var sellerNames = rawItems.Select(l => l.SellerName).Distinct().ToList();
        var stallsRaw = await _db.SellerStalls
            .Where(s => sellerNames.Contains(s.SellerName))
            .ToListAsync();

        // Use a safe dictionary creation to avoid duplicate name crashes (500 error)
        var stalls = stallsRaw
            .GroupBy(s => s.SellerName)
            .ToDictionary(g => g.Key, g => g.First());

        var items = rawItems.Select(l =>
        {
            stalls.TryGetValue(l.SellerName, out var stall);
            return new MarketListingDto(
                l.Id, l.ItemName, l.Category, l.Quality,
                l.Plus, l.Bless, l.Gem1, l.Gem2,
                l.SellerName, l.Price, l.Currency, l.ListedAt,
                stall?.MapX, stall?.MapY, stall?.StallNumber
            );
        }).ToList();

        return new MarketPagedResponse(items, total, page, pageSize, DateTime.UtcNow);
    }
    // ===== GET OR ASSIGN STALL FOR SELLER =====
    public async Task<(int X, int Y, int StallNum)> GetOrAssignStallAsync(string sellerName)
    {
        // Check if seller already has a stall
        var existing = await _db.SellerStalls
            .FirstOrDefaultAsync(s => s.SellerName == sellerName);

        if (existing != null)
            return (existing.MapX, existing.MapY, existing.StallNumber);

        // Find next available stall number
        var usedNumbers = await _db.SellerStalls
            .Select(s => s.StallNumber)
            .ToListAsync();

        var nextStall = 1;
        while (usedNumbers.Contains(nextStall) && nextStall <= StallPositions.Length)
            nextStall++;

        // Wrap around if all stalls taken (multiple sellers share position)
        var idx = (nextStall - 1) % StallPositions.Length;
        var (x, y) = StallPositions[idx];

        var stallRecord = new SellerStall
        {
            SellerName = sellerName,
            StallNumber = nextStall,
            MapX = x,
            MapY = y,
            AssignedAt = DateTime.UtcNow
        };

        _db.SellerStalls.Add(stallRecord);
        await _db.SaveChangesAsync();

        return (x, y, nextStall);
    }
    // ===== CREATE LISTING =====
    public async Task<ApiResponse<MarketListingDto>> CreateListingAsync(int userId, CreateListingRequest req)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return new ApiResponse<MarketListingDto>(false, "User not found");

        var validCategories = new[] { "Weapon", "Armor", "Necklace", "Ring", "Headgear", "Boots", "Garment", "Others" };
        var validQualities = new[] { "Normal", "Refined", "Unique", "Elite", "Super", "Legendary" };

        if (!validCategories.Contains(req.Category))
            return new ApiResponse<MarketListingDto>(false, "Invalid category");

        if (!validQualities.Contains(req.Quality))
            return new ApiResponse<MarketListingDto>(false, "Invalid quality");

        if (req.Plus < 0 || req.Plus > 9)
            return new ApiResponse<MarketListingDto>(false, "Plus must be between 0 and 9");

        if (req.Price <= 0)
            return new ApiResponse<MarketListingDto>(false, "Price must be greater than 0");

        // Assign stall position to seller
        var (stallX, stallY, stallNum) = await GetOrAssignStallAsync(user.Username);

        var listing = new MarketListing
        {
            SellerUserId = userId,
            SellerName = user.Username,
            ItemName = req.ItemName.Trim(),
            Category = req.Category,
            Quality = req.Quality,
            Plus = req.Plus,
            Bless = req.Bless,
            Gem1 = req.Gem1 ?? "NoSocket",
            Gem2 = req.Gem2 ?? "NoSocket",
            Price = req.Price,
            Currency = req.Currency == "coins" ? "coins" : "gold",
            IsActive = true,
            ListedAt = DateTime.UtcNow
        };

        _db.MarketListings.Add(listing);
        await _db.SaveChangesAsync();

        return new ApiResponse<MarketListingDto>(true, "Listing created",
            new MarketListingDto(listing.Id, listing.ItemName, listing.Category,
                listing.Quality, listing.Plus, listing.Bless, listing.Gem1, listing.Gem2,
                listing.SellerName, listing.Price, listing.Currency, listing.ListedAt,
                stallX, stallY, stallNum));
    }
    // ===== DELETE LISTING (owner or admin) =====
    public async Task<ApiResponse> DeleteListingAsync(int listingId, int userId, string role)
    {
        var listing = await _db.MarketListings.FindAsync(listingId);
        if (listing == null)
            return new ApiResponse(false, "Listing not found");

        if (listing.SellerUserId != userId && role != "Admin" && role != "Moderator")
            return new ApiResponse(false, "You can only remove your own listings");

        listing.IsActive = false;
        listing.SoldAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new ApiResponse(true, "Listing removed");
    }
    // ===== GET STALL MAP (all active sellers + positions) =====
    public async Task<List<StallMapDto>> GetStallMapAsync()
    {
        var activeSellerNames = await _db.MarketListings
            .Where(l => l.IsActive)
            .Select(l => l.SellerName)
            .Distinct()
            .ToListAsync();

        return await _db.SellerStalls
            .Where(s => activeSellerNames.Contains(s.SellerName))
            .Select(s => new StallMapDto(s.SellerName, s.StallNumber, s.MapX, s.MapY))
            .ToListAsync();
    }
    // ===== SEED SAMPLE DATA =====
    public async Task SeedMarketDataAsync()
    {
        if (await _db.MarketListings.AnyAsync()) return;

        var sellers = new[] { "btats", "Crunchy" };
        foreach (var seller in sellers)
            await GetOrAssignStallAsync(seller);

        var seedUser = await _db.Users.FirstOrDefaultAsync();

        if (seedUser == null)
        {
            Console.WriteLine("[MarketService] Skipping seed: No users found in database to assign as sellers.");
            return;
        }

        var samples = new List<MarketListing>
    {
            new() { SellerName="btats",       ItemName="DragonBlade",     Category="Weapon",   Quality="Super",     Plus=5, Bless=1, Gem1="SuperDragonGem", Gem2="SuperDragonGem", Price=15_000_000,    Currency="coins" },
            new() { SellerName="btats",       ItemName="DragonArmor",     Category="Armor",    Quality="Super",     Plus=6, Bless=1, Gem1="SuperDragonGem", Gem2="Empty",          Price=50_000_000,    Currency="gold" },
            new() { SellerName="Crunchy",     ItemName="DragonRing",      Category="Ring",     Quality="Super",     Plus=4, Bless=3, Gem1="SuperDragonGem", Gem2="SuperDragonGem", Price=550_000_000,   Currency="gold" },
            new() { SellerName="btats",       ItemName="DragonNecklace",  Category="Necklace", Quality="Super",     Plus=7, Bless=5, Gem1="SuperDragonGem", Gem2="SuperDragonGem", Price=1_000_000_000, Currency="gold" },
            new() { SellerName="btats",       ItemName="ConquestArmor",   Category="Armor",    Quality="Super",     Plus=9, Bless=1, Gem1="SuperDragonGem", Gem2="SuperDragonGem", Price=1_000_000_000, Currency="gold" },
            new() { SellerName="btats",       ItemName="ConquestArmor",   Category="Armor",    Quality="Unique",    Plus=9, Bless=1, Gem1="SuperDragonGem", Gem2="SuperDragonGem", Price=1_000_000_000, Currency="gold" },
            new() { SellerName="btats",       ItemName="ConquestArmor",   Category="Armor",    Quality="Refined",   Plus=9, Bless=1, Gem1="SuperDragonGem", Gem2="SuperDragonGem", Price=1_000_000_000, Currency="gold" },
            new() { SellerName="Chloe04",     ItemName="KylinBoots",      Category="Boots",    Quality="Super",     Plus=0, Bless=0, Gem1="NoSocket",       Gem2="NoSocket",       Price=1_500_000,     Currency="gold" },
            new() { SellerName="Miagi",       ItemName="TortoiseGem",     Category="Others",   Quality="Normal",    Plus=0, Bless=0, Gem1="NoSocket",       Gem2="NoSocket",       Price=1_300_000_000, Currency="gold" },
            new() { SellerName="ChubbyCheeks",ItemName="CopperRing",      Category="Ring",     Quality="Super",     Plus=0, Bless=0, Gem1="SuperRainbowGem",Gem2="SuperRainbowGem",Price=800_000_000,   Currency="gold" },
            new() { SellerName="Miagi",       ItemName="RhinocoatArmor",  Category="Armor",    Quality="Elite",     Plus=2, Bless=0, Gem1="NoSocket",       Gem2="NoSocket",       Price=10_000_000,    Currency="gold" },
            new() { SellerName="Miagi",       ItemName="SwanPlume",       Category="Armor",    Quality="Super",     Plus=4, Bless=0, Gem1="NoSocket",       Gem2="NoSocket",       Price=25_000_000,    Currency="gold" },
        };

        foreach (var s in samples)
        {
            s.IsActive = true;
            s.ListedAt = DateTime.UtcNow.AddMinutes(-new Random().Next(1, 1440));

            s.SellerUserId = seedUser.Id;
        }

        _db.MarketListings.AddRange(samples);
        await _db.SaveChangesAsync();
    }
}
