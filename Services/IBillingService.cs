namespace SpyGame.Services;

public enum BillingResultCode
{
    Ok = 0,
    UserCancelled = 1,
    BillingUnavailable = 3,
    ItemUnavailable = 4,
    DeveloperError = 5,
    Error = 6,
    ItemAlreadyOwned = 7,
    ItemNotOwned = 8
}

public record PurchaseResult(BillingResultCode Code, string? Message);

public interface IBillingService
{
    string MarketName { get; }
    Task<PurchaseResult> LaunchPurchaseAsync(string sku);
}
