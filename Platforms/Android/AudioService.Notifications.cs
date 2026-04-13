using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using RadioVolna.Resources;

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
        _mediaSession.SetFlags(
            MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls
        );
        _mediaSession.Active = true;
    }

    private void CreateNotificationChannel()
    {
        if (!IsAndroid26OrHigher())
            return;

        CreateNotificationChannelAndroid26();
    }

    [SupportedOSPlatform("android26.0")]
    private void CreateNotificationChannelAndroid26()
    {
        string channelName = LocalizationResourceManager.Instance["NotifChannelName"] ?? "Playback";
        string channelDesc =
            LocalizationResourceManager.Instance["NotifChannelDesc"] ?? "Radio playback controls";

        var channel = new NotificationChannel(ChannelId, channelName, NotificationImportance.Low);

        channel.Description = channelDesc;

        _notificationManager =
            _context.GetSystemService(Context.NotificationService) as NotificationManager;

        _notificationManager?.CreateNotificationChannel(channel);
    }

    private void RegisterNotificationReceiver()
    {
        _notificationReceiver = new NotificationReceiver(this);

        var filter = new IntentFilter();
        filter.AddAction(ActionPlay);
        filter.AddAction(ActionPause);
        filter.AddAction(ActionStop);

        if (IsAndroid33OrHigher())
        {
            RegisterNotificationReceiverAndroid33(_notificationReceiver, filter);
            return;
        }

        _context.RegisterReceiver(_notificationReceiver, filter);
    }

    [SupportedOSPlatform("android33.0")]
    private void RegisterNotificationReceiverAndroid33(
        BroadcastReceiver receiver,
        IntentFilter filter
    )
    {
        _context.RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
    }

    private void UpdateSystemMediaInfo(bool isPlaying)
    {
        if (_mediaSession == null)
            return;

        if (_notificationManager == null)
        {
            _notificationManager =
                _context.GetSystemService(Context.NotificationService) as NotificationManager;

            if (_notificationManager == null)
                return;
        }

        Bitmap? largeIcon = null;
        try
        {
            largeIcon = BitmapFactory.DecodeResource(
                _context.Resources,
                Resource.Drawable.radio_logo
            );
        }
        catch { }

        string textPaused = LocalizationResourceManager.Instance["NotifPaused"] ?? "Paused";
        string textPlay = LocalizationResourceManager.Instance["NotifActionPlay"] ?? "Play";
        string textPause = LocalizationResourceManager.Instance["NotifActionPause"] ?? "Pause";

        var metadataBuilder = new MediaMetadata.Builder();
        metadataBuilder.PutString(
            MediaMetadata.MetadataKeyTitle,
            isPlaying ? _currentStationName : textPaused
        );
        metadataBuilder.PutString(MediaMetadata.MetadataKeyArtist, "Radio Volna");

        if (largeIcon != null)
        {
            metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, largeIcon);
            metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyArt, largeIcon);
        }

        _mediaSession.SetMetadata(metadataBuilder.Build());

        var stateBuilder = new PlaybackState.Builder();
        var actions =
            PlaybackState.ActionPlay | PlaybackState.ActionPause | PlaybackState.ActionStop;
        stateBuilder.SetActions(actions);
        stateBuilder.SetState(
            isPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused,
            PlaybackState.PlaybackPositionUnknown,
            1.0f
        );
        _mediaSession.SetPlaybackState(stateBuilder.Build());
        _mediaSession.Active = true;

        int icon = isPlaying
            ? Android.Resource.Drawable.IcMediaPause
            : Android.Resource.Drawable.IcMediaPlay;
        string text = isPlaying ? textPause : textPlay;
        string actionToken = isPlaying ? ActionPause : ActionPlay;

        var intent = new Intent(actionToken);
        var pendingIntent = CreateBroadcastPendingIntent(intent);
        if (pendingIntent == null)
            return;

        Notification.Builder builder;
        if (IsAndroid26OrHigher())
            builder = CreateNotificationBuilderAndroid26();
        else
            builder = CreateNotificationBuilderLegacy();

        builder
            .SetSmallIcon(Android.Resource.Drawable.IcMediaPlay)
            .SetContentTitle("Radio Volna")
            .SetContentText(isPlaying ? _currentStationName : "Wstrzymano")
            .SetStyle(new Notification.MediaStyle().SetMediaSession(_mediaSession.SessionToken))
            .SetOngoing(isPlaying);

        if (IsAndroid26OrHigher())
        {
            AddNotificationActionAndroid26(builder, icon, text, pendingIntent);
        }
        else
        {
            AddNotificationActionLegacy(builder, icon, text, pendingIntent);
        }

        if (largeIcon != null)
            builder.SetLargeIcon(largeIcon);

        var packageName = _context.PackageName;
        if (!string.IsNullOrWhiteSpace(packageName))
        {
            var openIntent = _context.PackageManager?.GetLaunchIntentForPackage(packageName);
            if (openIntent != null)
            {
                var contentIntent = CreateActivityPendingIntent(openIntent);
                if (contentIntent != null)
                    builder.SetContentIntent(contentIntent);
            }
        }

        _notificationManager.Notify(NotificationId, builder.Build());
    }

#pragma warning disable CA1422
    [SupportedOSPlatform("android26.0")]
    private void AddNotificationActionAndroid26(
        Notification.Builder builder,
        int icon,
        string text,
        PendingIntent pendingIntent
    )
    {
        builder.AddAction(icon, text, pendingIntent);
    }

#pragma warning disable CS0618
    private void AddNotificationActionLegacy(
        Notification.Builder builder,
        int icon,
        string text,
        PendingIntent pendingIntent
    )
    {
        builder.AddAction(icon, text, pendingIntent);
    }
#pragma warning restore CS0618
#pragma warning restore CA1422

    [SupportedOSPlatform("android26.0")]
    private Notification.Builder CreateNotificationBuilderAndroid26()
    {
        return new Notification.Builder(_context, ChannelId);
    }

#pragma warning disable CS0618
#pragma warning disable CA1422
    private Notification.Builder CreateNotificationBuilderLegacy()
    {
        return new Notification.Builder(_context);
    }
#pragma warning restore CA1422
#pragma warning restore CS0618

    private PendingIntent? CreateBroadcastPendingIntent(Intent intent)
    {
        if (IsAndroid23OrHigher())
            return CreateBroadcastPendingIntentAndroid23(intent);

#pragma warning disable CS0618
        return PendingIntent.GetBroadcast(_context, 0, intent, PendingIntentFlags.UpdateCurrent);
#pragma warning restore CS0618
    }

    [SupportedOSPlatform("android23.0")]
    private PendingIntent? CreateBroadcastPendingIntentAndroid23(Intent intent)
    {
        return PendingIntent.GetBroadcast(
            _context,
            0,
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
        );
    }

    private PendingIntent? CreateActivityPendingIntent(Intent intent)
    {
        if (IsAndroid23OrHigher())
            return CreateActivityPendingIntentAndroid23(intent);

#pragma warning disable CS0618
        return PendingIntent.GetActivity(_context, 0, intent, PendingIntentFlags.UpdateCurrent);
#pragma warning restore CS0618
    }

    [SupportedOSPlatform("android23.0")]
    private PendingIntent? CreateActivityPendingIntentAndroid23(Intent intent)
    {
        return PendingIntent.GetActivity(
            _context,
            0,
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
        );
    }

    [BroadcastReceiver(Enabled = true, Exported = true)]
    private class NotificationReceiver : BroadcastReceiver
    {
        private readonly AudioService? _service;

        public NotificationReceiver() { }

        public NotificationReceiver(AudioService service)
        {
            _service = service;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (_service == null)
                return;

            switch (intent?.Action)
            {
                case ActionPlay:
                    _service.Resume();
                    break;
                case ActionPause:
                    _service.Pause();
                    break;
                case ActionStop:
                    _service.Stop();
                    break;
            }
        }
    }

    private class MediaSessionCallback : MediaSession.Callback
    {
        private readonly AudioService _service;

        public MediaSessionCallback(AudioService service)
        {
            _service = service;
        }

        public override void OnPlay()
        {
            _service.Resume();
        }

        public override void OnPause()
        {
            _service.Pause();
        }

        public override void OnStop()
        {
            _service.Stop();
        }

        public override void OnPlayFromMediaId(string? mediaId, Bundle? extras)
        {
            if (string.IsNullOrWhiteSpace(mediaId))
                return;

            string name = "Radio Volna";
            var station = RadioVolna.AudioService._autoStations.FirstOrDefault(s =>
                s.Url == mediaId
            );
            if (station != null)
                name = station.DisplayName;

            _service.Play(mediaId, name);
        }
    }
}
