#nullable enable
#pragma warning disable CS8618, CS8602, CS8600, CS0168, CA1416

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.Net.Wifi;
using Android.OS;
using Android.Runtime;

namespace RadioVolna;

public class AudioService : Java.Lang.Object, IAudioService, AudioManager.IOnAudioFocusChangeListener
{
    private MediaPlayer? _player;
    private AudioManager? _audioManager;
    private Context _context;

    // --- POWIADOMIENIA I MEDIA SESSION ---
    private MediaSession? _mediaSession;
    private NotificationManager? _notificationManager;
    private const int NotificationId = 1001;
    private const string ChannelId = "radio_volna_channel";
    private const string ActionPlay = "com.radiovolna.ACTION_PLAY";
    private const string ActionPause = "com.radiovolna.ACTION_PAUSE";
    private const string ActionStop = "com.radiovolna.ACTION_STOP";
    private NotificationReceiver? _notificationReceiver;

    // Blokady
    private WifiManager.WifiLock? _wifiLock;
    private PowerManager.WakeLock? _powerWakeLock;

    // Audio Focus i Słuchawki
    private AudioFocusRequestClass? _focusRequest;
    private bool _resumeOnFocusGain = false;
    private NoisyAudioReceiver? _noisyReceiver;
    private bool _isNoisyReceiverRegistered = false;

    private string _currentStationName = "Radio Volna";

    public event EventHandler<bool>? IsPlayingChanged;
    public event EventHandler<string>? StatusChanged;

    public AudioService()
    {
        _context = Android.App.Application.Context;
        _audioManager = _context.GetSystemService(Context.AudioService) as AudioManager;

        InitializeMediaSession();
        CreateNotificationChannel();
        RegisterNotificationReceiver();
    }

    public void Play(string url, string stationName)
    {
        if (!RequestAudioFocus())
        {
            StatusChanged?.Invoke(this, "Błąd: Audio zajęte");
            return;
        }

        _currentStationName = stationName;
        StopPlayerOnly();
        AcquireLocks();

        try
        {
            _player = new MediaPlayer();
            _player.SetWakeMode(_context, WakeLockFlags.Partial);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                _player.SetAudioAttributes(new AudioAttributes.Builder()
                    .SetContentType(AudioContentType.Music)
                    .SetUsage(AudioUsageKind.Media)
                    .Build());
            }

            var uri = Android.Net.Uri.Parse(url);
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" }
            };

            _player.SetDataSource(_context, uri, headers);

            _player.Prepared += (s, e) =>
            {
                _player.Start();
                RegisterNoisyReceiver();
                IsPlayingChanged?.Invoke(this, true);
                StatusChanged?.Invoke(this, $"Gra: {_currentStationName}");
                UpdateSystemMediaInfo(true);
            };

            _player.Error += (s, e) =>
            {
                StatusChanged?.Invoke(this, $"Błąd radia: {e.What}");
                IsPlayingChanged?.Invoke(this, false);
                UpdateSystemMediaInfo(false);
                ReleaseLocks();
            };

            _player.Completion += (s, e) =>
            {
                IsPlayingChanged?.Invoke(this, false);
                StatusChanged?.Invoke(this, "Zakończono");
                UpdateSystemMediaInfo(false);
                ReleaseLocks();
            };

            StatusChanged?.Invoke(this, "Łączenie...");
            _player.PrepareAsync();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Błąd: {ex.Message}");
            ReleaseLocks();
        }
    }

    public void Pause()
    {
        if (_player != null)
        {
            try { if (_player.IsPlaying) _player.Pause(); } catch { }
        }

        IsPlayingChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, "Wstrzymano");
        UpdateSystemMediaInfo(false);

        ReleaseLocks();
        UnregisterNoisyReceiver();
    }

    public void Resume()
    {
        if (!RequestAudioFocus()) return;

        if (_player != null)
        {
            try
            {
                if (!_player.IsPlaying) _player.Start();
            }
            catch { Play("", _currentStationName); return; }

            AcquireLocks();
            RegisterNoisyReceiver();

            IsPlayingChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, "Gra");
            UpdateSystemMediaInfo(true);
        }
    }

    public void Stop()
    {
        AbandonAudioFocus();
        StopPlayerOnly();
        IsPlayingChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, "Zatrzymano");

        _notificationManager?.Cancel(NotificationId);
        if (_mediaSession != null) _mediaSession.Active = false;
    }

    private void StopPlayerOnly()
    {
        ReleaseLocks();
        UnregisterNoisyReceiver();
        if (_player != null)
        {
            try { if (_player.IsPlaying) _player.Stop(); } catch { }
            try { _player.Release(); } catch { }
            _player = null;
        }
    }

    // --- MEDIA SESSION & NOTIFICATION LOGIC ---

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
            var channel = new NotificationChannel(ChannelId, "Radio Volna Player", NotificationImportance.Low)
            {
                Description = "Sterowanie radiem"
            };
            _notificationManager = _context.GetSystemService(Context.NotificationService) as NotificationManager;
            if (_notificationManager != null) _notificationManager.CreateNotificationChannel(channel);
        }
    }

    private void RegisterNotificationReceiver()
    {
        _notificationReceiver = new NotificationReceiver(this);
        var filter = new IntentFilter();
        filter.AddAction(ActionPlay);
        filter.AddAction(ActionPause);
        filter.AddAction(ActionStop);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            _context.RegisterReceiver(_notificationReceiver, filter, ReceiverFlags.Exported);
        else
            _context.RegisterReceiver(_notificationReceiver, filter);
    }

    private void UpdateSystemMediaInfo(bool isPlaying)
    {
        if (_mediaSession == null) return;

        var metadataBuilder = new MediaMetadata.Builder();
        metadataBuilder.PutString(MediaMetadata.MetadataKeyTitle, isPlaying ? _currentStationName : "Wstrzymano");
        metadataBuilder.PutString(MediaMetadata.MetadataKeyArtist, "Radio Volna");

        try
        {
            // Bitmap largeIcon = BitmapFactory.DecodeResource(_context.Resources, Resource.Drawable.radio_logo);
            // metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, largeIcon);
        }
        catch { }
        _mediaSession.SetMetadata(metadataBuilder.Build());

        var stateBuilder = new PlaybackState.Builder();
        var actions = PlaybackState.ActionPlay | PlaybackState.ActionPause | PlaybackState.ActionStop;
        var state = isPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused;
        stateBuilder.SetActions(actions);
        stateBuilder.SetState(state, PlaybackState.PlaybackPositionUnknown, 1.0f);
        _mediaSession.SetPlaybackState(stateBuilder.Build());
        _mediaSession.Active = true;

        if (_notificationManager == null) return;

        string actionToken = isPlaying ? ActionPause : ActionPlay;
        int icon = isPlaying ? Android.Resource.Drawable.IcMediaPause : Android.Resource.Drawable.IcMediaPlay;
        string text = isPlaying ? "Pauza" : "Graj";

        var intent = new Intent(actionToken);
        var pendingIntent = PendingIntent.GetBroadcast(_context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new Notification.Builder(_context, ChannelId)
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle("Radio Volna")
            .SetContentText(isPlaying ? _currentStationName : "Wstrzymano")
            .SetStyle(new Notification.MediaStyle().SetMediaSession(_mediaSession.SessionToken))
            .SetOngoing(isPlaying)
            .AddAction(icon, text, pendingIntent);

        var openIntent = _context.PackageManager?.GetLaunchIntentForPackage(_context.PackageName ?? "");
        if (openIntent != null)
        {
            var pIntent = PendingIntent.GetActivity(_context, 0, openIntent, PendingIntentFlags.Immutable);
            builder.SetContentIntent(pIntent);
        }

        _notificationManager.Notify(NotificationId, builder.Build());
    }

    // --- KLASY WEWNĘTRZNE (Callbacki) ---

    [BroadcastReceiver(Enabled = true, Exported = true)]
    private class NotificationReceiver : BroadcastReceiver
    {
        private readonly AudioService _service;
        public NotificationReceiver() { }
        public NotificationReceiver(AudioService service) => _service = service;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (_service == null) return;
            switch (intent?.Action)
            {
                case ActionPlay: _service.Resume(); break;
                case ActionPause: _service.Pause(); break;
                case ActionStop: _service.Stop(); break;
            }
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

    private void RegisterNoisyReceiver()
    {
        if (!_isNoisyReceiverRegistered)
        {
            _noisyReceiver = new NoisyAudioReceiver(this);
            var filter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
            _context.RegisterReceiver(_noisyReceiver, filter);
            _isNoisyReceiverRegistered = true;
        }
    }

    private void UnregisterNoisyReceiver()
    {
        if (_isNoisyReceiverRegistered && _noisyReceiver != null)
        {
            try { _context.UnregisterReceiver(_noisyReceiver); } catch { }
            _isNoisyReceiverRegistered = false;
            _noisyReceiver = null;
        }
    }

    private class NoisyAudioReceiver : BroadcastReceiver
    {
        private readonly AudioService _service;
        public NoisyAudioReceiver(AudioService service) => _service = service;
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy)
            {
                _service.Pause();
            }
        }
    }

    // --- ZARZĄDZANIE BLOKADAMI I AUDIO FOCUS ---
    private void AcquireLocks()
    {
        try
        {
            if (_wifiLock == null)
            {
                var wm = _context.GetSystemService(Context.WifiService) as WifiManager;
                if (wm != null) _wifiLock = wm.CreateWifiLock(Android.Net.WifiMode.FullHighPerf, "RadioVolnaWifiLock");
            }
            if (_wifiLock != null && !_wifiLock.IsHeld) _wifiLock.Acquire();
        }
        catch { }

        try
        {
            if (_powerWakeLock == null)
            {
                var pm = _context.GetSystemService(Context.PowerService) as PowerManager;
                if (pm != null) _powerWakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "RadioVolnaPowerLock");
            }
            if (_powerWakeLock != null && !_powerWakeLock.IsHeld) _powerWakeLock.Acquire();
        }
        catch { }
    }

    private void ReleaseLocks()
    {
        try { if (_wifiLock != null && _wifiLock.IsHeld) _wifiLock.Release(); } catch { }
        try { if (_powerWakeLock != null && _powerWakeLock.IsHeld) _powerWakeLock.Release(); } catch { }
    }

    private bool RequestAudioFocus()
    {
        if (_audioManager == null) return true;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var attr = new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media).SetContentType(AudioContentType.Music).Build();
            _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain).SetAudioAttributes(attr).SetOnAudioFocusChangeListener(this).Build();
            return (int)_audioManager.RequestAudioFocus(_focusRequest) == (int)AudioFocusRequest.Granted;
        }
#pragma warning disable CS0618
        return (int)_audioManager.RequestAudioFocus(this, Android.Media.Stream.Music, AudioFocus.Gain) == (int)AudioFocus.Gain;
#pragma warning restore CS0618
    }

    private void AbandonAudioFocus()
    {
        if (_audioManager == null) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O && _focusRequest != null) _audioManager.AbandonAudioFocusRequest(_focusRequest);
#pragma warning disable CS0618
        else _audioManager.AbandonAudioFocus(this);
#pragma warning restore CS0618
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Gain: if (_resumeOnFocusGain) { Resume(); _resumeOnFocusGain = false; } if (_player != null) _player.SetVolume(1.0f, 1.0f); break;
            case AudioFocus.Loss: Stop(); break;
            case AudioFocus.LossTransient: Pause(); _resumeOnFocusGain = true; break;
            case AudioFocus.LossTransientCanDuck: if (_player != null) _player.SetVolume(0.1f, 0.1f); break;
        }
    }
}