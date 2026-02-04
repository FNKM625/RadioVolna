// Plik: Platforms/Android/AudioService.cs
using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.Net.Wifi;
using Android.OS;
using RadioVolna.Resources;

namespace RadioVolna;

public partial class AudioService : Java.Lang.Object, IAudioService, AudioManager.IOnAudioFocusChangeListener
{
    private Context _context;
    private MediaPlayer? _player;

    private bool _isUsingExoPlayer = false;

    private AudioManager? _audioManager;
    private WifiManager.WifiLock? _wifiLock;
    private PowerManager.WakeLock? _powerWakeLock;
    private MediaSession? _mediaSession;
    private NotificationManager? _notificationManager;
    private NotificationReceiver? _notificationReceiver;
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

    // --- LOGIKA HYBRYDOWA ---
    public async void Play(string url, string stationName)
    {
        if (!RequestAudioFocus())
        {
            StatusChanged?.Invoke(this, LocalizationResourceManager.Instance["StatusAudioBusy"]);
            return;
        }

        _currentStationName = stationName;
        StatusChanged?.Invoke(this, LocalizationResourceManager.Instance["StatusCheckingStream"]);

        StopInternal();
        AcquireLocks();

        string mimeType = await CheckStreamFormatAsync(url);
        bool isMp3 = mimeType.Equals("audio/mpeg", StringComparison.OrdinalIgnoreCase) ||
                     mimeType.Equals("audio/mp3", StringComparison.OrdinalIgnoreCase) ||
                     mimeType.Equals("text/plain", StringComparison.OrdinalIgnoreCase);

        if (isMp3)
        {
            _isUsingExoPlayer = false;
            InitializeNativePlayer(url);
        }
        else
        {
            _isUsingExoPlayer = true;
            InitializeExoPlayer(url);
        }

        RegisterNoisyReceiver();
    }

    public void Pause()
    {
        if (_isUsingExoPlayer) PauseExoPlayer();
        else if (_player != null && _player.IsPlaying) _player.Pause();

        IsPlayingChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, LocalizationResourceManager.Instance["NotifPaused"]);

        UpdateSystemMediaInfo(false);
        ReleaseLocks();
        UnregisterNoisyReceiver();
    }

    public void Resume()
    {
        if (!RequestAudioFocus()) return;
        AcquireLocks();
        RegisterNoisyReceiver();

        if (_isUsingExoPlayer)
        {
            ResumeExoPlayer();
        }
        else
        {
            if (_player != null && !_player.IsPlaying) _player.Start();
            else Play("", _currentStationName);
        }

        IsPlayingChanged?.Invoke(this, true);
        StatusChanged?.Invoke(this, LocalizationResourceManager.Instance["NotifActionPlay"]);
        UpdateSystemMediaInfo(true);
    }

    public void Stop()
    {
        AbandonAudioFocus();
        StopInternal();
        IsPlayingChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, LocalizationResourceManager.Instance["StatusStopped"]);
        _notificationManager?.Cancel(NotificationId);
        if (_mediaSession != null) _mediaSession.Active = false;
    }

    private void StopInternal()
    {
        ReleaseLocks();
        UnregisterNoisyReceiver();

        StopNativePlayer();
        StopExoPlayer();

        _isUsingExoPlayer = false;
    }

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

    private int _retryCount = 0;
    private const int MaxRetries = 30;

    private async void AttemptReconnect()
    {
        _retryCount++;
        string logPrefix = LocalizationResourceManager.Instance["LogReconnectTry"];
        System.Diagnostics.Debug.WriteLine($"[AudioService] {logPrefix} {_retryCount}/{MaxRetries}...");

        string weakSignal = LocalizationResourceManager.Instance["StatusWeakSignal"];
        StatusChanged?.Invoke(this, $"{weakSignal} ({_retryCount}/{MaxRetries})");

        await Task.Delay(3000);

        if (_exoPlayer != null)
        {
            _exoPlayer.Prepare();
            _exoPlayer.PlayWhenReady = true;
        }
    }


}