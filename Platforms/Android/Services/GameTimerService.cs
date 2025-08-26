#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;          // ⬅️ برای ForegroundService.TypeMediaPlayback
using Android.OS;
using AndroidX.Core.App;
using Plugin.Maui.Audio;
using System;
using System.Timers;              // ⬅️ Timer از اینجا

using NotificationCompat = AndroidX.Core.App.NotificationCompat;

namespace SpyGame.Platforms.Android.Services
{
    [Service(ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
    public class GameTimerService : Service
    {
        public const int NOTIFICATION_ID = 1001;
        public const string CHANNEL_ID = "spy_timer_channel";
        public const string ACTION_START = "SPY_TIMER_START";
        public const string ACTION_STOP = "SPY_TIMER_STOP";
        public const string EXTRA_SECONDS = "SECONDS";

        private System.Timers.Timer? _timer;
        private int _remaining;
        private IAudioPlayer? _endPlayer;

        public override void OnCreate()
        {
            base.OnCreate();
            CreateChannel();

            try
            {
                var stream = FileSystem.OpenAppPackageFileAsync("beep_end.wav").Result;
                _endPlayer = AudioManager.Current.CreatePlayer(stream);
            }
            catch { /* ignore */ }
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            if (intent == null) return StartCommandResult.NotSticky;

            var action = intent.Action;
            if (action == ACTION_START)
            {
                _remaining = intent.GetIntExtra(EXTRA_SECONDS, 60);
                StartInForeground();
                StartTicking();
            }
            else if (action == ACTION_STOP)
            {
                StopForeground(true);
                StopSelf();
            }

            return StartCommandResult.Sticky;
        }

        public override IBinder? OnBind(Intent? intent) => null;

        private void StartInForeground()
        {
            var notification = BuildNotification(TimeSpan.FromSeconds(_remaining));
            StartForeground(NOTIFICATION_ID, notification);
        }

        private void StartTicking()
        {
            _timer?.Stop();
            _timer?.Dispose();

            _timer = new System.Timers.Timer(1000);   // ⬅️ 1000 میلی‌ثانیه، نه TimeSpan
            _timer.AutoReset = true;
            _timer.Elapsed += (_, __) =>
            {
                _remaining--;
                UpdateNotification();

                if (_remaining <= 0)
                {
                    _timer!.Stop();
                    try { _endPlayer?.Play(); } catch { }
                    StopForeground(true);
                    StopSelf();
                }
            };
            _timer.Start();
        }

        private void UpdateNotification()
        {
            var notif = BuildNotification(TimeSpan.FromSeconds(Math.Max(0, _remaining)));
            var nm = NotificationManagerCompat.From(this);
            nm.Notify(NOTIFICATION_ID, notif);
        }

        private Notification BuildNotification(TimeSpan remain)
        {
            var time = $"{remain.Minutes:00}:{remain.Seconds:00}";

            var stopIntent = new Intent(this, typeof(GameTimerService));
            stopIntent.SetAction(ACTION_STOP);
            var stopPending = PendingIntent.GetService(
                this, 0, stopIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle("تایمر بازی جاسوس")
                .SetContentText($"زمان باقی‌مانده: {time}")
                .SetSmallIcon(Resource.Drawable.ic_stat_name)   // ⬅️ حتماً این آیکن را در drawable بگذار
                .SetOngoing(true)
                .AddAction(new NotificationCompat.Action(0, "توقف", stopPending))
                .SetVisibility((int)NotificationVisibility.Public)
                .SetOnlyAlertOnce(true);

            return builder.Build();
        }

        private void CreateChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(CHANNEL_ID, "Spy Timer", NotificationImportance.Low)
                {
                    LockscreenVisibility = NotificationVisibility.Public
                };
                var nm = (NotificationManager)GetSystemService(NotificationService)!;
                nm.CreateNotificationChannel(channel);
            }
        }

        public override void OnDestroy()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _endPlayer?.Dispose();
            base.OnDestroy();
        }
    }
}
#endif
