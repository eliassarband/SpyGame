namespace SpyGame.Services;

internal class NullBillingService : IBillingService
{
    public string MarketName => string.Empty;

    public Task<PurchaseResult> LaunchPurchaseAsync(string sku) =>
        Task.FromResult(new PurchaseResult(
            BillingResultCode.BillingUnavailable,
            "پرداخت درون‌برنامه‌ای فقط روی اندروید پشتیبانی می‌شود."));
}
