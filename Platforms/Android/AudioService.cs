using Android.Media;
using Android.Content;
using Android.OS;
using Android.App;
using Android.Graphics;
// Dodane dla obsługi sesji i powiadomień
using Android.Media.Session;
using Android.Graphics.Drawables;

namespace RadioVolna;

public class AudioService : IAudioService
{
    private MediaPlayer? _player;

    // --- NOWE ZMIENNE DLA POWIADOMIEŃ ---
    private MediaSession? _mediaSession;
    private NotificationManager? _notificationManager;
    private Context _context;
    private NotificationReceiver? _receiver;

    private const int NotificationId = 1001;
    private const string ChannelId = "radio_volna_channel";
    private const string ActionPlay = "com.radiovolna.ACTION_PLAY";
    private const string ActionPause = "com.radiovolna.ACTION_PAUSE";
    private const string ActionStop = "com.radiovolna.ACTION_STOP";
    // -------------------------------------

    public event EventHandler<bool>? IsPlayingChanged;
    public event EventHandler<string>? StatusChanged;

    public AudioService()
    {
        _context = Android.App.Application.Context;

        // Inicjalizacja sesji i kanału powiadomień
        InitializeMediaSession();
        CreateNotificationChannel();
        RegisterNotificationReceiver();
    }

    private void InitializeMediaSession()
    {
        _mediaSession = new MediaSession(_context, "RadioVolnaSession");
        _mediaSession.SetCallback(new MediaSessionCallback(this));
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
            _notificationManager = (NotificationManager)_context.GetSystemService(Context.NotificationService)!;
            _notificationManager.CreateNotificationChannel(channel);
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

    public void Play(string url)
    {
        // Używamy metody wewnętrznej, żeby nie kasować powiadomienia przy zmianie stacji
        StopPlayerOnly();

        _player = new MediaPlayer();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            _player.SetAudioAttributes(new AudioAttributes.Builder()
                .SetContentType(AudioContentType.Music)
                .SetUsage(AudioUsageKind.Media)
                .Build());
        }

        try
        {
            _player.SetDataSource(url);

            _player.Prepared += (s, e) =>
            {
                _player.Start();
                IsPlayingChanged?.Invoke(this, true);
                StatusChanged?.Invoke(this, "Połączenie nawiązane");

                // Pokaż powiadomienie (stan: GRA)
                UpdateNotification(true);
            };

            _player.Error += (s, e) =>
            {
                StatusChanged?.Invoke(this, $"Błąd strumienia: {e.What}");
                IsPlayingChanged?.Invoke(this, false);
                UpdateNotification(false);
            };

            StatusChanged?.Invoke(this, "Łączenie...");
            _player.PrepareAsync();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Błąd: {ex.Message}");
        }
    }

    public void Pause()
    {
        if (_player != null && _player.IsPlaying)
        {
            _player.Pause();
            IsPlayingChanged?.Invoke(this, false);
            StatusChanged?.Invoke(this, "Wstrzymano");

            // Zaktualizuj powiadomienie (stan: PAUZA)
            UpdateNotification(false);
        }
    }

    public void Resume()
    {
        if (_player != null && !_player.IsPlaying)
        {
            _player.Start();
            IsPlayingChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, "Połączenie nawiązane");

            // Zaktualizuj powiadomienie (stan: GRA)
            UpdateNotification(true);
        }
    }

    public void Stop()
    {
        StopPlayerOnly();

        IsPlayingChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, "Zatrzymano");

        // Usuń powiadomienie całkowicie
        _notificationManager?.Cancel(NotificationId);
    }

    private void StopPlayerOnly()
    {
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

    // --- LOGIKA BUDOWANIA POWIADOMIENIA ---
    private void UpdateNotification(bool isPlaying)
    {
        if (_notificationManager == null) return;

        // 1. Ustal akcję (Graj czy Pauza)
        string action = isPlaying ? ActionPause : ActionPlay;
        var intent = new Intent(action);
        // Ważne: PendingIntentFlags.Immutable jest wymagane w nowych Androidach
        var pendingIntent = PendingIntent.GetBroadcast(_context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        // 2. Wybierz ikonę i tekst przycisku
        int icon = isPlaying ? Android.Resource.Drawable.IcMediaPause : Android.Resource.Drawable.IcMediaPlay;
        string title = isPlaying ? "Pauza" : "Graj";

        // 3. Kliknięcie w treść powiadomienia otwiera aplikację
        var openAppIntent = _context.PackageManager?.GetLaunchIntentForPackage(_context.PackageName!);
        var contentIntent = PendingIntent.GetActivity(_context, 0, openAppIntent!, PendingIntentFlags.Immutable);

        // 4. Styl MediaStyle (To sprawia, że wygląda jak odtwarzacz)
        var mediaStyle = new Notification.MediaStyle();
        mediaStyle.SetMediaSession(_mediaSession!.SessionToken);
        // .SetShowActionsInCompactView(0) oznacza: pokaż pierwszy dodany przycisk (czyli nasz Play/Pause)
        mediaStyle.SetShowActionsInCompactView(0);

        // 5. Budowanie
        var builder = new Notification.Builder(_context, ChannelId)
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay) // Ikona systemowa w pasku statusu
            .SetContentTitle("Radio Volna")
            .SetContentText(isPlaying ? "Na antenie" : "Wstrzymano")
            .SetContentIntent(contentIntent)
            .SetStyle(mediaStyle)
            .SetOngoing(isPlaying) // Jeśli gra, nie da się go łatwo usunąć gestem
            .AddAction(new Notification.Action(icon, title, pendingIntent)); // Dodajemy TYLKO JEDEN przycisk

        _notificationManager.Notify(NotificationId, builder.Build());
    }

    // --- KLASY POMOCNICZE ---

    // Odbiera kliknięcia w przyciski powiadomienia
    [BroadcastReceiver(Enabled = true, Exported = true)]
    private class NotificationReceiver : BroadcastReceiver
    {
        private readonly AudioService _service;
        public NotificationReceiver() { } // Wymagany pusty konstruktor
        public NotificationReceiver(AudioService service) => _service = service;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (_service == null) return;

            if (intent?.Action == ActionPlay) _service.Resume();
            else if (intent?.Action == ActionPause) _service.Pause();
            else if (intent?.Action == ActionStop) _service.Stop();
        }
    }

    // Obsługuje sterowanie z np. słuchawek Bluetooth
    private class MediaSessionCallback : MediaSession.Callback
    {
        private readonly AudioService _service;
        public MediaSessionCallback(AudioService service) => _service = service;

        public override void OnPlay() => _service.Resume();
        public override void OnPause() => _service.Pause();
        public override void OnStop() => _service.Stop();
    }
}