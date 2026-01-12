#nullable enable
#pragma warning disable CS8618, CS8602, CS8600, CS0168, CA1416

using Android.Media;
using Android.Content;
using Android.OS;
using Android.App;
using Android.Graphics;
using Android.Media.Session;
using Android.Net.Wifi;

namespace RadioVolna;

public class AudioService : Java.Lang.Object, IAudioService, AudioManager.IOnAudioFocusChangeListener
{
    private MediaPlayer? _player;
    private MediaSession? _mediaSession;
    private NotificationManager? _notificationManager;
    private AudioManager? _audioManager;
    private Context _context;

    // Blokady systemowe (To one utrzymują radio przy życiu w tle)
    private WifiManager.WifiLock? _wifiLock;
    private PowerManager.WakeLock? _powerWakeLock; // <--- NOWOŚĆ: Twarda blokada CPU

    private NotificationReceiver? _receiver;
    private AudioFocusRequestClass? _focusRequest;
    private bool _resumeOnFocusGain = false;

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
        _audioManager = _context.GetSystemService(Context.AudioService) as AudioManager;

        InitializeMediaSession();
        CreateNotificationChannel();
        RegisterNotificationReceiver();
    }

    // --- LOGIKA AUDIO FOCUS ---
    private bool RequestAudioFocus()
    {
        if (_audioManager == null) return false;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var attributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)
                .SetContentType(AudioContentType.Music)
                .Build();

            _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                .SetAudioAttributes(attributes)
                .SetAcceptsDelayedFocusGain(true)
                .SetOnAudioFocusChangeListener(this)
                .Build();

            var result = _audioManager.RequestAudioFocus(_focusRequest);
            return (int)result == (int)AudioFocusRequest.Granted;
        }
        else
        {
#pragma warning disable CS0618
            var result = _audioManager.RequestAudioFocus(this, Android.Media.Stream.Music, AudioFocus.Gain);
            return (int)result == (int)AudioFocus.Gain;
#pragma warning restore CS0618
        }
    }

    private void AbandonAudioFocus()
    {
        if (_audioManager == null) return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O && _focusRequest != null)
            _audioManager.AbandonAudioFocusRequest(_focusRequest);
        else
#pragma warning disable CS0618
            _audioManager.AbandonAudioFocus(this);
#pragma warning restore CS0618
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Gain:
                if (_resumeOnFocusGain)
                {
                    Resume();
                    _resumeOnFocusGain = false;
                }
                _player?.SetVolume(1.0f, 1.0f);
                break;
            case AudioFocus.Loss:
                Stop(); // Trwała utrata fokusu (np. inne radio włączyło się)
                break;
            case AudioFocus.LossTransient:
                if (_player != null && _player.IsPlaying)
                {
                    _resumeOnFocusGain = true;
                    Pause();
                }
                break;
            case AudioFocus.LossTransientCanDuck:
                _player?.SetVolume(0.1f, 0.1f); // Przycisz (np. nawigacja mówi)
                break;
        }
    }

    // --- ZARZĄDZANIE BLOKADAMI (Keep Alive) ---

    private void AcquireLocks()
    {
        // 1. Wifi Lock (żeby nie zrywało neta)
        try
        {
            if (_wifiLock == null)
            {
                var wm = _context.GetSystemService(Context.WifiService) as WifiManager;
                if (wm != null)
                    _wifiLock = wm.CreateWifiLock(Android.Net.WifiMode.FullHighPerf, "RadioVolnaWifiLock");
            }
            if (_wifiLock != null && !_wifiLock.IsHeld) _wifiLock.Acquire();
        }
        catch { }

        // 2. Power Lock (żeby CPU nie zasnął) - KLUCZOWE DLA TŁA
        try
        {
            if (_powerWakeLock == null)
            {
                var pm = _context.GetSystemService(Context.PowerService) as PowerManager;
                if (pm != null)
                {
                    // Partial Wake Lock pozwala wygasić ekran, ale CPU pracuje dalej
                    _powerWakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "RadioVolnaPowerLock");
                }
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

    // --- ODTWARZANIE ---

    public void Play(string url, string stationName)
    {
        if (!RequestAudioFocus())
        {
            StatusChanged?.Invoke(this, "Błąd: Audio zajęte");
            return;
        }

        StopPlayerOnly(); // Czyści stary player i zwalnia blokady na chwilę
        _currentStationName = stationName;

        // NATYCHMIAST pobierz blokady, zanim system pomyśli o uśpieniu
        AcquireLocks();

        _player = new MediaPlayer();

        // Wbudowana blokada playera (dodatkowe zabezpieczenie)
        _player.SetWakeMode(_context, WakeLockFlags.Partial);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            _player.SetAudioAttributes(new AudioAttributes.Builder()
                .SetContentType(AudioContentType.Music)
                .SetUsage(AudioUsageKind.Media)
                .Build());
        }

        try
        {
            var uri = Android.Net.Uri.Parse(url);
            // Dodajemy nagłówki, żeby serwery RMF nas nie odrzucały
            var headers = new Dictionary<string, string> {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" }
            };

            _player.SetDataSource(_context, uri, headers);

            _player.Prepared += (s, e) =>
            {
                _player.Start();
                IsPlayingChanged?.Invoke(this, true);
                StatusChanged?.Invoke(this, $"Gra: {_currentStationName}");
                UpdateSystemMediaInfo(true);
            };

            _player.Error += (s, e) =>
            {
                StatusChanged?.Invoke(this, $"Błąd radia ({e.What})");
                IsPlayingChanged?.Invoke(this, false);
                UpdateSystemMediaInfo(false);
                ReleaseLocks(); // Zwolnij blokady przy błędzie
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
        if (_player != null && _player.IsPlaying)
        {
            _player.Pause();
            IsPlayingChanged?.Invoke(this, false);
            StatusChanged?.Invoke(this, "Wstrzymano");
            UpdateSystemMediaInfo(false);
            ReleaseLocks(); // Nie trzymaj CPU włączonego na pauzie
        }
    }

    public void Resume()
    {
        if (!RequestAudioFocus()) return;

        if (_player != null && !_player.IsPlaying)
        {
            AcquireLocks(); // Ponownie włącz blokady
            _player.Start();
            IsPlayingChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, $"Gra: {_currentStationName}");
            UpdateSystemMediaInfo(true);
        }
        else if (_player == null)
        {
            // Jeśli playera nie ma (został ubity), spróbuj zagrać od nowa (wymagałoby zapamiętania URL, tutaj prosta obsługa)
            StatusChanged?.Invoke(this, "Wznowienie niemożliwe (Wybierz stację)");
        }
    }

    public void Stop()
    {
        AbandonAudioFocus();
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
        ReleaseLocks(); // ZAWSZE zwalniaj blokady przy stopie

        if (_player != null)
        {
            try
            {
                if (_player.IsPlaying) _player.Stop();
            }
            catch { }
            try
            {
                _player.Release();
            }
            catch { }
            _player = null;
        }
    }

    // --- NOTYFIKACJE I MEDIA SESSION ---

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
            if (_notificationManager != null) _notificationManager.CreateNotificationChannel(channel);
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
            _context.RegisterReceiver(_receiver, filter, ReceiverFlags.Exported);
        else
            _context.RegisterReceiver(_receiver, filter);
    }

    private void UpdateSystemMediaInfo(bool isPlaying)
    {
        Bitmap largeIcon = BitmapFactory.DecodeResource(_context.Resources, Resource.Drawable.radio_logo)!;

        var metadataBuilder = new MediaMetadata.Builder();
        metadataBuilder.PutString(MediaMetadata.MetadataKeyTitle, isPlaying ? _currentStationName : "Wstrzymano");
        metadataBuilder.PutString(MediaMetadata.MetadataKeyArtist, "Radio Volna");
        metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, largeIcon);
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
        string btnText = isPlaying ? "Pauza" : "Graj";

        // Dodajemy Intencję otwierania aplikacji po kliknięciu w powiadomienie
        var openAppIntent = _context.PackageManager?.GetLaunchIntentForPackage(_context.PackageName ?? "");
        PendingIntent? contentIntent = null;
        if (openAppIntent != null)
        {
            openAppIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            contentIntent = PendingIntent.GetActivity(_context, 0, openAppIntent, PendingIntentFlags.Immutable);
        }

        var builder = new Notification.Builder(_context, ChannelId)
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay)
            .SetLargeIcon(largeIcon)
            .SetContentTitle("Radio Volna")
            .SetContentText(isPlaying ? _currentStationName : "Wstrzymano")
            .SetStyle(new Notification.MediaStyle().SetMediaSession(_mediaSession.SessionToken).SetShowActionsInCompactView(0))
            .SetOngoing(isPlaying) // To sprawia, że powiadomienia nie da się usunąć gdy gra
            .AddAction(new Notification.Action(iconBtn, btnText, pendingIntent));

        if (contentIntent != null) builder.SetContentIntent(contentIntent);

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