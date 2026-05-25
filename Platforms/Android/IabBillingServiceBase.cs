using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using SpyGame.Models;
using SpyGame.Services;
using System.Security.Cryptography;

namespace SpyGame.Platforms.Android;

internal abstract class IabBillingServiceBase : Java.Lang.Object, IServiceConnection, IBillingService
{
    protected abstract string ServiceAction { get; }
    protected abstract string ServicePackage { get; }
    protected abstract string InterfaceDescriptor { get; }
    protected abstract string PublicKey { get; }
    protected abstract int PurchaseRequestCode { get; }
    public abstract string MarketName { get; }

    private IBinder? _binder;
    private TaskCompletionSource<IBinder>? _bindTcs;
    private TaskCompletionSource<PurchaseResult>? _purchaseTcs;
    protected readonly PremiumManager Premium;

    protected IabBillingServiceBase(PremiumManager premium)
    {
        Premium = premium;
    }

    public void OnServiceConnected(ComponentName? name, IBinder? service)
    {
        _binder = service;
        _bindTcs?.TrySetResult(service!);
    }

    public void OnServiceDisconnected(ComponentName? name)
    {
        _binder = null;
    }

    protected bool IsMarketInstalled()
    {
        try
        {
#pragma warning disable CA1422
            global::Android.App.Application.Context
                .PackageManager!.GetPackageInfo(ServicePackage, 0);
#pragma warning restore CA1422
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> EnsureBoundAsync()
    {
        if (_binder != null) return true;
        if (!IsMarketInstalled()) return false;

        _bindTcs = new TaskCompletionSource<IBinder>();
        var ctx = global::Android.App.Application.Context;
        var intent = new Intent(ServiceAction).SetPackage(ServicePackage);
        bool bound = ctx.BindService(intent, this, Bind.AutoCreate);
        if (!bound) return false;

        await Task.WhenAny(_bindTcs.Task, Task.Delay(5_000));
        return _binder != null;
    }

    public async Task<PurchaseResult> LaunchPurchaseAsync(string sku)
    {
        if (!await EnsureBoundAsync())
            return new PurchaseResult(
                BillingResultCode.BillingUnavailable,
                $"{MarketName} روی دستگاه نصب نیست.\nلطفاً ابتدا اپ {MarketName} را نصب کنید.");

        _purchaseTcs = new TaskCompletionSource<PurchaseResult>();

        var data = Parcel.Obtain()!;
        var reply = Parcel.Obtain()!;
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity!;
            var packageName = activity.PackageName!;

            data.WriteInterfaceToken(InterfaceDescriptor);
            data.WriteInt(3);       // apiVersion
            data.WriteString(packageName);
            data.WriteString(sku);
            data.WriteString("inapp");
            data.WriteString(""); // developerPayload

            // transact 3 = getBuyIntent
            _binder!.Transact(3, data, reply, TransactionFlags.None);
            reply.ReadException();

            Bundle? responseBundle = null;
            if (reply.ReadInt() != 0)
                responseBundle = reply.ReadBundle();

            if (responseBundle == null)
                return new PurchaseResult(BillingResultCode.Error, "پاسخ نامعتبر");

            int responseCode = responseBundle.GetInt("RESPONSE_CODE");
            if (responseCode != 0)
                return new PurchaseResult((BillingResultCode)responseCode,
                    $"کد خطا: {responseCode}");

#pragma warning disable CA1422
            var pendingIntent = responseBundle.GetParcelable("BUY_INTENT") as PendingIntent;
#pragma warning restore CA1422
            if (pendingIntent == null)
                return new PurchaseResult(BillingResultCode.Error, "لینک خرید دریافت نشد");

            activity.StartIntentSenderForResult(
                pendingIntent.IntentSender, PurchaseRequestCode, new Intent(), 0, 0, 0);
        }
        catch (Exception ex)
        {
            _purchaseTcs.TrySetResult(new PurchaseResult(BillingResultCode.Error, ex.Message));
        }
        finally
        {
            data.Recycle();
            reply.Recycle();
        }

        return await _purchaseTcs.Task;
    }

    // فراخوانی از MainActivity.OnActivityResult
    internal void ForwardActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != PurchaseRequestCode || _purchaseTcs == null) return;

        if (resultCode == Result.Canceled)
        {
            _purchaseTcs.TrySetResult(
                new PurchaseResult(BillingResultCode.UserCancelled, "خرید لغو شد"));
            return;
        }

        if (data == null)
        {
            _purchaseTcs.TrySetResult(
                new PurchaseResult(BillingResultCode.Error, "داده‌ای دریافت نشد"));
            return;
        }

        int responseCode = data.GetIntExtra("RESPONSE_CODE", -1);
        string? purchaseData = data.GetStringExtra("INAPP_PURCHASE_DATA");
        string? signature = data.GetStringExtra("INAPP_DATA_SIGNATURE");

        if (responseCode != 0)
        {
            _purchaseTcs.TrySetResult(
                new PurchaseResult((BillingResultCode)responseCode, $"کد خطا: {responseCode}"));
            return;
        }

        if (!VerifySignature(purchaseData, signature))
        {
            _purchaseTcs.TrySetResult(
                new PurchaseResult(BillingResultCode.Error, "امضای خرید نامعتبر است"));
            return;
        }

        Task.Run(async () =>
        {
            await Premium.UnlockAsync(PremiumFeature.Premium);
            _purchaseTcs.TrySetResult(new PurchaseResult(BillingResultCode.Ok, null));
        });
    }

    private bool VerifySignature(string? data, string? signature)
    {
        if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(signature))
            return false;
        try
        {
            byte[] keyBytes = Convert.FromBase64String(PublicKey);
            byte[] sigBytes = Convert.FromBase64String(signature);
            byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(data);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
            return rsa.VerifyData(dataBytes, sigBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }
}
