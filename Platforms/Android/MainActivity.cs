using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android;

namespace SpyGame
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges =
            ConfigChanges.ScreenSize |
            ConfigChanges.Orientation |
            ConfigChanges.UiMode |
            ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize |
            ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        const int ReqPostNotif = 1001;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                if (CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
                    RequestPermissions(new[] { Manifest.Permission.PostNotifications }, ReqPostNotif);
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == Platforms.Android.MyketBillingService.RC_PURCHASE)
                Platforms.Android.MyketBillingService.Instance?.HandleActivityResult(resultCode, data);
        }
    }
}
