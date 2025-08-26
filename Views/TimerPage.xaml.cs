#if ANDROID
using Android;
using Android.Content;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using SpyGame.Platforms.Android.Services;
#endif

using Microsoft.Maui.ApplicationModel;
using Plugin.Maui.Audio;
using SpyGame.Data;
using System.Timers;

namespace SpyGame.Views;

public partial class TimerPage : ContentPage
{
    private readonly AppDatabase _db;
    private System.Timers.Timer? _timer;
    private TimeSpan _remaining;
    private bool _running;

    private IAudioPlayer? _endBeep;
    private IAudioPlayer? _shortBeep;

    // DI سازنده
    public TimerPage(AppDatabase db)
    {
        InitializeComponent();
        _db = db;
    }

    //// سازندهٔ بدون پارامتر برای Shell (از DI می‌گیرد)
    //public TimerPage() : this(App.Current.Services.GetRequiredService<AppDatabase>()) { }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // مخفی کردن فلش Back (اضافی در کنار XAML)
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false, IsEnabled = false });

        var config = await _db.GetLastConfigAsync();
        if (config == null) return;

        PlayersInfoLabel.Text = $"بازیکنان: {config.Players}";
        SpiesInfoLabel.Text = $"جاسوس‌ها: {config.Spies}";

        _remaining = TimeSpan.FromMinutes(config.Minutes);
        UpdateLabel();

        WordHintLabel.Text = $"زمان بازی: {config.Minutes} دقیقه";

        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync("beep_end.wav");
            _endBeep = AudioManager.Current.CreatePlayer(stream);

        }
        catch { 
            _endBeep = null;
        }

        try
        {
            var streamS = await FileSystem.OpenAppPackageFileAsync("beep_short.wav");
            _shortBeep = AudioManager.Current.CreatePlayer(streamS);
        }
        catch
        {
            _shortBeep = null;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { _endBeep?.Dispose(); } catch { }
        _endBeep = null;

        try { _shortBeep?.Dispose(); } catch { }
        _shortBeep = null;
    }

    // دکمه Back سخت‌افزاری/سیستمی کار نکند
    protected override bool OnBackButtonPressed() => true;

    private void UpdateLabel()
    {
        TimerLabel.Text = $"{_remaining.Minutes:00}:{_remaining.Seconds:00}";
    }

    private void OnStartPause(object sender, EventArgs e)
    {
        if (_running) Pause();
        else
        {
#if ANDROID
            EnsureNotificationPermission();
#endif
            Start();
        }
    }

    private async void OnReset(object sender, EventArgs e)
    {
        Pause();
        var config = await _db.GetLastConfigAsync();
        if (config != null)
            _remaining = TimeSpan.FromMinutes(config.Minutes);
        UpdateLabel();
    }

    private async void OnFinish(object sender, EventArgs e)
    {
        Pause();
        await Shell.Current.GoToAsync($"{nameof(SetupPage)}"); // Root nav
    }

    private void Start()
    {
        if (_running) return;
        _running = true;

        _timer ??= new System.Timers.Timer(1000);
        _timer.Elapsed -= OnTick;
        _timer.Elapsed += OnTick;
        _timer.AutoReset = true;
        _timer.Start();

#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context;
            var intent = new Intent(ctx, typeof(GameTimerService));
            intent.SetAction(GameTimerService.ACTION_START);
            intent.PutExtra(GameTimerService.EXTRA_SECONDS, (int)_remaining.TotalSeconds);
            ctx.StartForegroundService(intent);
        }
        catch { }
#endif
    }

    private void Pause()
    {
        _running = false;
        _timer?.Stop();

#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context;
            var stopIntent = new Intent(ctx, typeof(GameTimerService));
            stopIntent.SetAction(GameTimerService.ACTION_STOP);
            ctx.StartService(stopIntent);
        }
        catch { }
#endif
    }

    private async void OnTick(object? sender, ElapsedEventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_remaining.TotalSeconds > 0)
            {
                _remaining = _remaining.Add(TimeSpan.FromSeconds(-1));
                UpdateLabel();

                if (_remaining.TotalSeconds <= 60 && _remaining.TotalSeconds >= 58)
                {
                    try { Vibration.Vibrate(TimeSpan.FromMilliseconds(100)); } catch { }
                    try { _shortBeep?.Play(); } catch { }
                }
                else if (_remaining.TotalSeconds <= 30 && _remaining.TotalSeconds >= 28)
                {
                    try { Vibration.Vibrate(TimeSpan.FromMilliseconds(300)); } catch { }
                    try { _endBeep?.Play(); } catch { }
                }

                if (_remaining.TotalSeconds <= 60 && _remaining.TotalSeconds >= 55)
                {
                    WarningLabel.Text = "60 ثانیه وقت مونده";
                }
                else if (_remaining.TotalSeconds <= 30 && _remaining.TotalSeconds >= 25)
                {
                    WarningLabel.Text = "فقط 30 ثانیه!!!!!!";
                }
                else
                {
                    WarningLabel.Text = "";
                }
            }
            else
            {
                Pause();
                UpdateLabel();
                WarningLabel.Text = "";
                _ = OnTimeFinished();
            }

        });
    }

    private async Task OnTimeFinished()
    {
        try { Vibration.Vibrate(TimeSpan.FromMilliseconds(600)); } catch { }
        try { _endBeep?.Play(); } catch { }

        await DisplayAlert("پایان زمان", "زمان بازی به پایان رسید.", "باشه");
        await Shell.Current.GoToAsync($"{nameof(SetupPage)}"); // Root nav
    }

#if ANDROID
    // درخواست Runtime Permission برای POST_NOTIFICATIONS روی Android 13+ (API 33)
    private static void EnsureNotificationPermission()
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (ContextCompat.CheckSelfPermission(activity, Manifest.Permission.PostNotifications) != (int)Android.Content.PM.Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(activity,
                        new string[] { Manifest.Permission.PostNotifications }, requestCode: 1001);
                }
            }
        }
        catch { }
    }
#endif
}
