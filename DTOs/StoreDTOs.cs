// DTOs/StoreDTOs.cs
namespace GameRealmAPI.DTOs;

public record StoreItemDto(
    int Id,
    string Name,
    string Description,
    string Icon,
    string Category,
    int PriceCoins,
    string? Badge,
    string? BadgeType,
    int Stock
);

public record CoinPackageDto(
    int Id,
    int Coins,
    decimal Price,
    int BonusPercent,
    bool IsPopular
);

public record WalletDto(
    int UserId,
    int Coins
);

public record BuyItemRequest(int ItemId);

public record PurchaseLogDto(
    int Id,
    string Type,
    string ItemName,
    int CoinsSpent,
    int CoinsReceived,
    string Status,
    DateTime CreatedAt
);

public record InitiatePaymentRequest(
    int PackageId,
    string Method  // paypal, card, arcen
);

public record PaymentDto(
    int Id,
    int PackageId,
    string PackageName,
    string Method,
    decimal Amount,
    string Status,
    DateTime CreatedAt
);
