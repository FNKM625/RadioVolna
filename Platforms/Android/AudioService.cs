#nullable enable
#pragma warning disable CS8618, CS8602, CS8600, CS0168, CA1416

using Android.Media;
using Android.Content;
using Android.OS;
using Android.App;
using Android.Graphics;
using Android.Media.Session;
using Android.Net.Wifi;
using System.Collections.Generic;

namespace RadioVolna;

public class AudioService : Java.Lang.Object, IAudioService, AudioManager.IOnAudioFocusChangeListener
{
    private MediaPlayer? _player;
    private MediaSession? _mediaSession;
    private NotificationManager? _notificationManager;
    private AudioManager? _audioManager;
    private Context _context;
    private NotificationReceiver? _receiver;
    private AudioFocusRequestClass? _focusRequest;
    private bool _resumeOnFocusGain = false;
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
        _audioManager = _context.GetSystemService(Context.AudioService) as AudioManager;

        InitializeMediaSession();
        CreateNotificationChannel();
        RegisterNotificationReceiver();
    }

    private bool RequestAudioFocus()
    {
        if (_audioManager == null) return false;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var attributes = new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media).SetContentType(AudioContentType.Music).Build();
            _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain).SetAudioAttributes(attributes).SetAcceptsDelayedFocusGain(true).SetOnAudioFocusChangeListener(this).Build();
            var result = _audioManager.RequestAudioFocus(_focusRequest);
            return (int)result == (int)AudioFocusRequest.Granted;
        }
        else
        {
            var result = _audioManager.RequestAudioFocus(this, Android.Media.Stream.Music, AudioFocus.Gain);
            return (int)result == (int)AudioFocus.Gain;
        }
    }

    private void AbandonAudioFocus()
    {
        if (_audioManager == null) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O && _focusRequest != null) _audioManager.AbandonAudioFocusRequest(_focusRequest);
        else _audioManager.AbandonAudioFocus(this);
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Gain: if (_resumeOnFocusGain) { Resume(); _resumeOnFocusGain = false; } _player?.SetVolume(1.0f, 1.0f); break;
            case AudioFocus.Loss: Stop(); break;
            case AudioFocus.LossTransient: if (_player != null && _player.IsPlaying) { _resumeOnFocusGain = true; Pause(); } break;
            case AudioFocus.LossTransientCanDuck: if (_player != null && _player.IsPlaying) _player.SetVolume(0.1f, 0.1f); break;
        }
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
            var channel = new NotificationChannel(ChannelId, "Radio Volna Sterowanie", NotificationImportance.Low) { Description = "Pasek sterowania radiem" };
            _notificationManager = _context.GetSystemService(Context.NotificationService) as NotificationManager;
            if (_notificationManager != null) _notificationManager.CreateNotificationChannel(channel);
        }
    }

    private void RegisterNotificationReceiver()
    {
        _receiver = new NotificationReceiver(this);
        var filter = new IntentFilter();
        filter.AddAction(ActionPlay); filter.AddAction(ActionPause); filter.AddAction(ActionStop);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) _context.RegisterReceiver(_receiver, filter, ReceiverFlags.Exported);
        else _context.RegisterReceiver(_receiver, filter);
    }

    private void ReleaseWifiLock() { if (_wifiLock != null && _wifiLock.IsHeld) { _wifiLock.Release(); _wifiLock = null; } }

    public void Play(string url, string stationName)
    {
        if (!RequestAudioFocus()) { StatusChanged?.Invoke(this, "Błąd: Audio zajęte"); return; }
        StopPlayerOnly();
        _currentStationName = stationName;

        _player = new MediaPlayer();
        _player.SetWakeMode(_context, WakeLockFlags.Partial);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            _player.SetAudioAttributes(new AudioAttributes.Builder().SetContentType(AudioContentType.Music).SetUsage(AudioUsageKind.Media).Build());
        }

        try { var wm = _context.GetSystemService(Context.WifiService) as WifiManager; if (wm != null) { _wifiLock = wm.CreateWifiLock(Android.Net.WifiMode.FullHighPerf, "RadioVolnaLock"); _wifiLock.Acquire(); } } catch { }

        try
        {
            var uri = Android.Net.Uri.Parse(url);
            var headers = new Dictionary<string, string>();
            headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

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
                StatusChanged?.Invoke(this, $"Błąd radia: {e.What}");
                IsPlayingChanged?.Invoke(this, false);
                UpdateSystemMediaInfo(false);
                ReleaseWifiLock();
            };

            StatusChanged?.Invoke(this, "Łączenie...");
            _player.PrepareAsync();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Błąd połączenia: {ex.Message}");
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
            ReleaseWifiLock();
        }
    }

    public void Resume()
    {
        if (!RequestAudioFocus()) return;
        if (_player != null && !_player.IsPlaying)
        {
            _player.Start();
            IsPlayingChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, $"Gra: {_currentStationName}");
            UpdateSystemMediaInfo(true);
            try { var wm = _context.GetSystemService(Context.WifiService) as WifiManager; if (wm != null && (_wifiLock == null || !_wifiLock.IsHeld)) { _wifiLock = wm.CreateWifiLock(Android.Net.WifiMode.FullHighPerf, "RadioVolnaLock"); _wifiLock.Acquire(); } } catch { }
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
        ReleaseWifiLock();
        if (_player != null) { try { if (_player.IsPlaying) _player.Stop(); } catch { } try { _player.Release(); } catch { } _player = null; }
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

        var builder = new Notification.Builder(_context, ChannelId)
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay)
            .SetLargeIcon(largeIcon)
            .SetContentTitle("Radio Volna")
            .SetContentText(isPlaying ? _currentStationName : "Wstrzymano")
            .SetStyle(new Notification.MediaStyle().SetMediaSession(_mediaSession.SessionToken).SetShowActionsInCompactView(0))
            .SetOngoing(isPlaying)
            .AddAction(new Notification.Action(iconBtn, isPlaying ? "Pauza" : "Graj", pendingIntent));

        _notificationManager.Notify(NotificationId, builder.Build());
    }

    [BroadcastReceiver(Enabled = true, Exported = true)]
    private class NotificationReceiver : BroadcastReceiver
    {
        private readonly AudioService _service;
        public NotificationReceiver() { }
        public NotificationReceiver(AudioService service) => _service = service;
        public override void OnReceive(Context? context, Intent? intent) { if (_service == null) return; if (intent?.Action == ActionPlay) _service.Resume(); else if (intent?.Action == ActionPause) _service.Pause(); else if (intent?.Action == ActionStop) _service.Stop(); }
    }
    private class MediaSessionCallback : MediaSession.Callback { private AudioService _s; public MediaSessionCallback(AudioService s) => _s = s; public override void OnPlay() => _s.Resume(); public override void OnPause() => _s.Pause(); public override void OnStop() => _s.Stop(); }
}