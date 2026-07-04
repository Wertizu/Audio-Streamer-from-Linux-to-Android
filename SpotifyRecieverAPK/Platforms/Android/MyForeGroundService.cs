using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace SpotifyRecieverAPK.Platforms.Android
{
    [Service(Enabled = true, Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
    public class MyForegroundService : Service
    {
        
        private global::Android.Net.Wifi.WifiManager.WifiLock wifiLock;
        private global::Android.OS.PowerManager.WakeLock wakeLock;

        public override IBinder OnBind(Intent intent) => null;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            var channelId = "dummy_channel";
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(channelId, "Audio Service", NotificationImportance.High);
                ((NotificationManager)GetSystemService(NotificationService))?.CreateNotificationChannel(channel);
            }

            var notification = new NotificationCompat.Builder(this, channelId)
                .SetContentTitle("Spotify Receiver")
                .SetContentText("Streaming active...")
                .SetSmallIcon(Microsoft.Maui.Resource.Id.icon)
                .SetOngoing(true)
                .Build();

            StartForeground(1001, notification);

            

            // 1. CPU wachhalten (Wake-Lock)
            var powerManager = (global::Android.OS.PowerManager)GetSystemService(Context.PowerService);
            if (powerManager != null)
            {
                
                wakeLock = powerManager.NewWakeLock(global::Android.OS.WakeLockFlags.Partial, "SpotifyReceiver:WakeLockAPK");
                wakeLock.Acquire();
            }

            // 2. WLAN auf voller Leistung halten (Wifi-Lock)
            var wifiManager = (global::Android.Net.Wifi.WifiManager)GetSystemService(Context.WifiService);
            if (wifiManager != null)
            {
               
                wifiLock = wifiManager.CreateWifiLock((global::Android.Net.WifiMode.FullLowLatency), "SpotifyReceiverAPK:WifiLock");
                wifiLock.Acquire();
            }

            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            if (wakeLock != null && wakeLock.IsHeld)
            {
                wakeLock.Release();
            }

            if (wifiLock != null && wifiLock.IsHeld)
            {
                wifiLock.Release();
            }

            base.OnDestroy();
        }
    }
}