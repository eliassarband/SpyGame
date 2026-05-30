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
        "com.android.vending.billing.IInAppBillingService";
    protected override int PurchaseRequestCode => 10002;

    // کلید عمومی RSA از کنسول توسعه‌دهنده بازار
    protected override string PublicKey =>
        "MIHNMA0GCSqGSIb3DQEBAQUAA4G7ADCBtwKBrwC15IJsijALGcV5nqcRjX6RCRc33jdJDULDSVIch03xr" +
        "8SzvRHqEqbVjrE0G4oyIETp9OHIekQtLFmN2EiWNY0Nz+8vrwLI++kOap9YPX95vdjADZgWv+58oIcWR" +
        "3b5q8/AsBIP4+Y5/RtucExv0C2pt3CXrVW+E+JeyNbZdyc4WHC95+FbTxjyv5PBGRQU7AVlP/pm6EuZN" +
        "xoCF+OuCAALTrirCjACNo3PSNJjFIkCAwEAAQ==";

    internal static BazaarBillingService? Instance { get; private set; }

    public BazaarBillingService(PremiumManager premium) : base(premium)
    {
        Instance = this;
    }
}
