// DTOs/MarketDTOs.cs
namespace GameRealmAPI.DTOs;

public record MarketListingDto(
    int Id,
    string ItemName,
    string Category,
    string Quality,
    int Plus,
    int Bless,
    string Gem1,
    string Gem2,
    string SellerName,
    long Price,
    string Currency,
    DateTime ListedAt,
    int? MapX,        // pixel X on map (null if no stall assigned yet)
    int? MapY,        // pixel Y on map
    int? StallNumber  // stall number 1-32
);

public record MarketFilterRequest(
    string? Category,
    string? Quality,
    int? Plus,
    int? Bless,
    string? Name,
    string? SortBy,
    int Page = 1,
    int PageSize = 50
);

public record CreateListingRequest(
    string ItemName,
    string Category,
    string Quality,
    int Plus,
    int Bless,
    string Gem1,
    string Gem2,
    long Price,
    string Currency
);

public record MarketPagedResponse(
    List<MarketListingDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    DateTime LastRefreshed
);

// For the map overlay — returns all active stalls with positions
public record StallMapDto(
    string SellerName,
    int StallNumber,
    int MapX,
    int MapY
);
