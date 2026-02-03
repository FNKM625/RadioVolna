// Plik: Platforms/Android/AudioService.Notifications.cs
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;

namespace RadioVolna;

public partial class AudioService
{
    private const int NotificationId = 1001;
    private const string ChannelId = "radio_volna_channel";
    private const string ActionPlay = "com.radiovolna.ACTION_PLAY";
    private const string ActionPause = "com.radiovolna.ACTION_PAUSE";
    private const string ActionStop = "com.radiovolna.ACTION_STOP";

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
        if (_mediaSession == null || _notificationManager == null) return;

        Bitmap? largeIcon = null;
        try
        {
            largeIcon = BitmapFactory.DecodeResource(_context.Resources, Resource.Drawable.radio_logo);
        }
        catch { }

        var metadataBuilder = new MediaMetadata.Builder();
        metadataBuilder.PutString(MediaMetadata.MetadataKeyTitle, isPlaying ? _currentStationName : "Wstrzymano");
        metadataBuilder.PutString(MediaMetadata.MetadataKeyArtist, "Radio Volna");

        if (largeIcon != null)
        {
            metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, largeIcon);
            metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyArt, largeIcon);
        }

        _mediaSession.SetMetadata(metadataBuilder.Build());

        var stateBuilder = new PlaybackState.Builder();
        var actions = PlaybackState.ActionPlay | PlaybackState.ActionPause | PlaybackState.ActionStop;
        stateBuilder.SetActions(actions);
        stateBuilder.SetState(isPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused, PlaybackState.PlaybackPositionUnknown, 1.0f);
        _mediaSession.SetPlaybackState(stateBuilder.Build());
        _mediaSession.Active = true;

        int icon = isPlaying ? Android.Resource.Drawable.IcMediaPause : Android.Resource.Drawable.IcMediaPlay;
        string text = isPlaying ? "Pauza" : "Graj";
        string actionToken = isPlaying ? ActionPause : ActionPlay;

        var intent = new Intent(actionToken);
        var pendingIntent = PendingIntent.GetBroadcast(_context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new Notification.Builder(_context, ChannelId)
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle("Radio Volna")
            .SetContentText(isPlaying ? _currentStationName : "Wstrzymano")
            .SetStyle(new Notification.MediaStyle().SetMediaSession(_mediaSession.SessionToken))
            .SetOngoing(isPlaying)
            .AddAction(icon, text, pendingIntent);

        if (largeIcon != null)
        {
            builder.SetLargeIcon(largeIcon);
        }

        var openIntent = _context.PackageManager?.GetLaunchIntentForPackage(_context.PackageName ?? "");
        if (openIntent != null)
        {
            builder.SetContentIntent(PendingIntent.GetActivity(_context, 0, openIntent, PendingIntentFlags.Immutable));
        }

        _notificationManager.Notify(NotificationId, builder.Build());
    }

    // --- KLASY WEWNĘTRZNE DO OBSŁUGI ZDARZEŃ ---

    [BroadcastReceiver(Enabled = true, Exported = true)]
    private class NotificationReceiver : BroadcastReceiver
    {
        private readonly AudioService _service;
        public NotificationReceiver() { }
        public NotificationReceiver(AudioService service) => _service = service;

        public override void OnReceive(Context? context, Intent? intent)
        {
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
}