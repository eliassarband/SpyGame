using SpyGame.Services;

namespace SpyGame.Platforms.Android;

internal sealed class BazaarBillingService : IabBillingServiceBase
{
    public override string MarketName => "Bazaar";

    protected override string ServiceAction =>
        "ir.cafebazaar.pardakht.InAppBillingService.BIND";
    protected override string ServicePackage =>
        "com.farsitel.bazaar";
    protected override string InterfaceDescriptor =>
        "ir.cafebazaar.pardakht.IInAppBillingService";
    protected override int PurchaseRequestCode => 10002;

    // ⚠️ TODO: کلید عمومی RSA از کنسول توسعه‌دهنده بازار
    // مسیر: cafebazaar.ir/developers → برنامه → اطلاعات مالی → کلید عمومی
    // پس از دریافت، این مقدار را جایگزین BAZAAR_RSA_KEY کن
    protected override string PublicKey =>
        "BAZAAR_RSA_KEY";

    internal static BazaarBillingService? Instance { get; private set; }

    public BazaarBillingService(PremiumManager premium) : base(premium)
    {
        Instance = this;
    }
}
