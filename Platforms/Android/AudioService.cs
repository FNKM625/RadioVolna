// --- SEKCJA KONFIGURACJI KOMPILATORA ---
#nullable enable
#pragma warning disable CS8618
#pragma warning disable CS8602
#pragma warning disable CS8600
#pragma warning disable CS0168
#pragma warning disable CA1416

using Android.Media;
using Android.Content;
using Android.OS;
using Android.App;
using Android.Graphics;
using Android.Media.Session;
using Android.Graphics.Drawables;
using Android.Net.Wifi;
using Android.Net;

namespace RadioVolna;

public class AudioService : IAudioService
{
    private MediaPlayer? _player;
    private MediaSession? _mediaSession;
    private NotificationManager? _notificationManager;
    private Context _context;
    private NotificationReceiver? _receiver;
    private WifiManager.WifiLock? _wifiLock;

    private string _currentStationName = "Radio Volna";
    private const int NotificationId = 1001;
    private const string ChannelId = "radio_volna_channel";
    private const string ActionPlay = "com.radiovolna.ACTION_PLAY";
    private const string ActionPause = "com.radiovolna.ACTION_PAUSE";
    private const string ActionStop = "com.radiovolna.ACTION_STOP";

    public event EventHandler<bool>? IsPlayingChanged;
    public event EventHandler<string>? StatusChanged;

    public AudioService()
    {
        _context = Android.App.Application.Context;
        InitializeMediaSession();
        CreateNotificationChannel();
        RegisterNotificationReceiver();
    }

    private void InitializeMediaSession()
    {
        _mediaSession = new MediaSession(_context, "RadioVolnaSession");
        _mediaSession.SetCallback(new MediaSessionCallback(this));
        _mediaSession.SetFlags(MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls);
        _mediaSession.Active = true;
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Radio Volna Sterowanie", NotificationImportance.Low)
            {
                Description = "Pasek sterowania radiem"
            };

            _notificationManager = _context.GetSystemService(Context.NotificationService) as NotificationManager;
            if (_notificationManager != null)
            {
                _notificationManager.CreateNotificationChannel(channel);
            }
        }
    }

    private void RegisterNotificationReceiver()
    {
        _receiver = new NotificationReceiver(this);
        var filter = new IntentFilter();
        filter.AddAction(ActionPlay);
        filter.AddAction(ActionPause);
        filter.AddAction(ActionStop);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            _context.RegisterReceiver(_receiver, filter, ReceiverFlags.Exported);
        }
        else
        {
            _context.RegisterReceiver(_receiver, filter);
        }
    }

    // --- METODA POMOCNICZA: Zwalnianie blokad ---
    private void ReleaseWifiLock()
    {
        if (_wifiLock != null && _wifiLock.IsHeld)
        {
            _wifiLock.Release();
            _wifiLock = null;
        }
    }

    public void Play(string url, string stationName)
    {
        StopPlayerOnly();
        _currentStationName = stationName;

        _player = new MediaPlayer();

        // --- 1. BLOKADA PROCESORA (CPU WakeLock) ---
        // To mówi systemowi: "Nie usypiaj procesora, gdy gra muzyka"
        _player.SetWakeMode(_context, WakeLockFlags.Partial);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            _player.SetAudioAttributes(new AudioAttributes.Builder()
                .SetContentType(AudioContentType.Music)
                .SetUsage(AudioUsageKind.Media)
                .Build());
        }

        // --- 2. BLOKADA WIFI (WiFi Lock) ---
        // To mówi systemowi: "Nie rozłączaj internetu po wygaszeniu ekranu"
        try
        {
            var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
            if (wifiManager != null)
            {
                _wifiLock = wifiManager.CreateWifiLock(WifiMode.FullHighPerf, "RadioVolnaLock");
                _wifiLock.Acquire();
            }
        }
        catch { } // Ignorujemy błędy blokady, żeby nie wywaliło apki

        try
        {
            _player.SetDataSource(url);
            _player.Prepared += (s, e) =>
            {
                _player.Start();
                IsPlayingChanged?.Invoke(this, true);
                StatusChanged?.Invoke(this, $"Gra: {_currentStationName}");
                UpdateSystemMediaInfo(true);
            };

            _player.Error += (s, e) =>
            {
                StatusChanged?.Invoke(this, $"Błąd: {e.What}");
                IsPlayingChanged?.Invoke(this, false);
                UpdateSystemMediaInfo(false);
                ReleaseWifiLock(); // Zwolnij blokadę przy błędzie
            };

            StatusChanged?.Invoke(this, "Łączenie...");
            _player.PrepareAsync();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Błąd: {ex.Message}");
            ReleaseWifiLock();
        }
    }

    public void Pause()
    {
        if (_player != null && _player.IsPlaying)
        {
            _player.Pause();
            IsPlayingChanged?.Invoke(this, false);
            StatusChanged?.Invoke(this, "Wstrzymano");
            UpdateSystemMediaInfo(false);

            // Przy pauzie możemy zwolnić WiFi, żeby oszczędzać baterię
            ReleaseWifiLock();
        }
    }

    public void Resume()
    {
        if (_player != null && !_player.IsPlaying)
        {
            _player.Start();
            IsPlayingChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, $"Połączenie nawiązane");
            UpdateSystemMediaInfo(true);

            // Odnawiamy blokadę WiFi
            try
            {
                var wifiManager = _context.GetSystemService(Context.WifiService) as WifiManager;
                if (wifiManager != null && (_wifiLock == null || !_wifiLock.IsHeld))
                {
                    _wifiLock = wifiManager.CreateWifiLock(WifiMode.FullHighPerf, "RadioVolnaLock");
                    _wifiLock.Acquire();
                }
            }
            catch { }
        }
    }

    public void Stop()
    {
        StopPlayerOnly();
        IsPlayingChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, "Zatrzymano");

        _mediaSession!.Active = false;
        _mediaSession.SetMetadata(null);
        _mediaSession.SetPlaybackState(null);
        _notificationManager?.Cancel(NotificationId);
    }

    private void StopPlayerOnly()
    {
        // Zwalniamy blokady przy zatrzymaniu
        ReleaseWifiLock();

        if (_player != null)
        {
            try
            {
                if (_player.IsPlaying) _player.Stop();
                _player.Release();
            }
            catch { }
            _player = null;
        }
    }

    private void UpdateSystemMediaInfo(bool isPlaying)
    {
        Bitmap largeIcon = BitmapFactory.DecodeResource(_context.Resources, Resource.Drawable.radio_logo)!;

        var metadataBuilder = new MediaMetadata.Builder();
        metadataBuilder.PutString(MediaMetadata.MetadataKeyTitle, isPlaying ? _currentStationName : "Wstrzymano");
        metadataBuilder.PutString(MediaMetadata.MetadataKeyArtist, "Radio Volna");
        metadataBuilder.PutString(MediaMetadata.MetadataKeyAlbum, "Radio Internetowe");
        metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, largeIcon);
        metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyArt, largeIcon);
        _mediaSession!.SetMetadata(metadataBuilder.Build());

        var stateBuilder = new PlaybackState.Builder();
        var state = isPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused;

        stateBuilder.SetActions(PlaybackState.ActionPlay | PlaybackState.ActionPause | PlaybackState.ActionStop);
        stateBuilder.SetState(state, PlaybackState.PlaybackPositionUnknown, 1.0f);

        _mediaSession.SetPlaybackState(stateBuilder.Build());
        _mediaSession.Active = true;

        if (_notificationManager == null) return;

        string action = isPlaying ? ActionPause : ActionPlay;
        var intent = new Intent(action);
        var pendingIntent = PendingIntent.GetBroadcast(_context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        int iconBtn = isPlaying ? Android.Resource.Drawable.IcMediaPause : Android.Resource.Drawable.IcMediaPlay;
        string title = isPlaying ? "Pauza" : "Graj";

        var openAppIntent = _context.PackageManager?.GetLaunchIntentForPackage(_context.PackageName ?? "");
        PendingIntent? contentIntent = null;
        if (openAppIntent != null)
        {
            contentIntent = PendingIntent.GetActivity(_context, 0, openAppIntent, PendingIntentFlags.Immutable);
        }

        var mediaStyle = new Notification.MediaStyle();
        mediaStyle.SetMediaSession(_mediaSession.SessionToken);
        mediaStyle.SetShowActionsInCompactView(0);

        var builder = new Notification.Builder(_context, ChannelId)
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay)
            .SetLargeIcon(largeIcon)
            .SetContentTitle("Radio Volna")
            .SetContentText(isPlaying ? _currentStationName : "Wstrzymano")
            .SetStyle(mediaStyle)
            .SetOngoing(isPlaying)
            .AddAction(new Notification.Action(iconBtn, title, pendingIntent));

        if (contentIntent != null)
        {
            builder.SetContentIntent(contentIntent);
        }

        _notificationManager.Notify(NotificationId, builder.Build());
    }

    [BroadcastReceiver(Enabled = true, Exported = true)]
    private class NotificationReceiver : BroadcastReceiver
    {
        private readonly AudioService _service;
        public NotificationReceiver() { }
        public NotificationReceiver(AudioService service) => _service = service;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (_service == null) return;
            if (intent?.Action == ActionPlay) _service.Resume();
            else if (intent?.Action == ActionPause) _service.Pause();
            else if (intent?.Action == ActionStop) _service.Stop();
        }
    }

    private class MediaSessionCallback : MediaSession.Callback
    {
        private readonly AudioService _service;
        public MediaSessionCallback(AudioService service) => _service = service;
        public override void OnPlay() => _service.Resume();
        public override void OnPause() => _service.Pause();
        public override void OnStop() => _service.Stop();
    }
}