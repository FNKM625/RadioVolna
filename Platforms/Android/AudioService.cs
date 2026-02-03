// Plik: Platforms/Android/AudioService.cs
using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Session;
using Android.Net.Wifi;
using Android.OS;

namespace RadioVolna;

public partial class AudioService : Java.Lang.Object, IAudioService, AudioManager.IOnAudioFocusChangeListener
{
    private Context _context;
    private MediaPlayer? _player;
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

        InitializePlayer(url);
    }

    public void Pause()
    {
        PausePlayerInternal();

        IsPlayingChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, "Wstrzymano");
        UpdateSystemMediaInfo(false);

        ReleaseLocks();
        UnregisterNoisyReceiver();
    }

    public void Resume()
    {
        if (!RequestAudioFocus()) return;

        if (ResumePlayerInternal())
        {
            AcquireLocks();
            RegisterNoisyReceiver();
            IsPlayingChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, "Gra");
            UpdateSystemMediaInfo(true);
        }
        else
        {
            Play("", _currentStationName);
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
}