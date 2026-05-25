using SpyGame.Services;

namespace SpyGame.Platforms.Android;

internal sealed class MyketBillingService : IabBillingServiceBase
{
    public override string MarketName => "Myket";

    protected override string ServiceAction =>
        "ir.mservices.market.InAppBillingService.BIND";
    protected override string ServicePackage =>
        "ir.mservices.market";
    protected override string InterfaceDescriptor =>
        "com.android.vending.billing.IInAppBillingService";
    protected override int PurchaseRequestCode => 10001;

    // کلید عمومی RSA از کنسول توسعه‌دهنده مایکت
    protected override string PublicKey =>
        "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCR8jWHgb6mkQ9uriwo6my1oqprYoB+Y4uMlUh3s0bD" +
        "FEflQGqpkBh7ooTjhlqJgX83H0c27lE7PIZJ82MteBPYMIrYU2W3QfUqAyyQnGsna8wL7PZHU7qxrmap" +
        "fpfn9oYFNl9Oc0STLW7z+c4drXKMUuJ1Ps1zYvLQAyTYX33KTQIDAQAB";

    internal static MyketBillingService? Instance { get; private set; }

    public MyketBillingService(PremiumManager premium) : base(premium)
    {
        Instance = this;
    }
}
