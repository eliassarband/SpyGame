namespace SpyGame.Services;

internal class NullBillingService : IBillingService
{
    public Task<PurchaseResult> LaunchPurchaseAsync(string sku) =>
        Task.FromResult(new PurchaseResult(
            BillingResultCode.BillingUnavailable,
            "پرداخت درون‌برنامه‌ای فقط روی اندروید پشتیبانی می‌شود."));
}
