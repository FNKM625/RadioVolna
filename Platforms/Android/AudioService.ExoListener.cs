using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;

namespace RadioVolna;

public partial class AudioService
{
    private class ExoPlayerListener : Java.Lang.Object, IPlayerListener
    {
        private readonly AudioService _service;

        public ExoPlayerListener(AudioService service)
        {
            _service = service;
        }

        public void OnPlaybackStateChanged(int playbackState)
        {
            switch (playbackState)
            {
                case 2:
                    _service.StatusChanged?.Invoke(_service, "Buforowanie...");
                    break;
                case 3:
                    _service.StatusChanged?.Invoke(_service, $"Gra: {_service._currentStationName}");
                    _service.IsPlayingChanged?.Invoke(_service, true);
                    break;
                case 4:
                    _service.IsPlayingChanged?.Invoke(_service, false);
                    break;
            }
        }

        public void OnPlayerError(PlaybackException? error)
        {
            _service.Log($"Exo Error: {error?.ErrorCodeName}");
            if (error?.ErrorCodeName != "ERROR_CODE_IO_NETWORK_CONNECTION_FAILED")
            {
                _service.StatusChanged?.Invoke(_service, "Błąd strumienia");
            }
            _service.IsPlayingChanged?.Invoke(_service, false);
        }

        public void OnMetadata(Metadata metadata)
        {
            _service.Log("Otrzymano metadane (tytuł/artysta).");
        }

        public void OnTracksChanged(Tracks tracks)
        {
            _service.Log("Zmieniono ścieżki audio.");
        }

        public void OnTimelineChanged(Timeline timeline, int reason) { }
        public void OnMediaItemTransition(MediaItem? mediaItem, int reason) { }
        public void OnAvailableCommandsChanged(PlayerCommands commands) { }
        public void OnPlayerStateChanged(bool playWhenReady, int playbackState) { }
        public void OnPlayWhenReadyChanged(bool playWhenReady, int reason) { }
        public void OnLoadingChanged(bool isLoading) { }
        public void OnIsLoadingChanged(bool isLoading) { }
        public void OnPositionDiscontinuity(PlayerPositionInfo oldPosition, PlayerPositionInfo newPosition, int reason) { }
        public void OnRepeatModeChanged(int repeatMode) { }
        public void OnShuffleModeEnabledChanged(bool shuffleModeEnabled) { }
        public void OnPlaybackSuppressionReasonChanged(int reason) { }
        public void OnIsPlayingChanged(bool isPlaying) { }
        public void OnMediaMetadataChanged(MediaMetadata mediaMetadata) { }
        public void OnPlaylistMetadataChanged(MediaMetadata mediaMetadata) { }
        public void OnEvents(IPlayer player, PlayerEvents playerEvents) { }
        public void OnCues(AndroidX.Media3.Common.Text.CueGroup cueGroup) { }
        public void OnCues(System.Collections.Generic.IList<AndroidX.Media3.Common.Text.Cue> cues) { }
        public void OnVideoSizeChanged(VideoSize videoSize) { }
        public void OnVolumeChanged(float volume) { }
        public void OnDeviceInfoChanged(AndroidX.Media3.Common.DeviceInfo deviceInfo) { }
        public void OnDeviceVolumeChanged(int volume, bool muted) { }
        public void OnRenderedFirstFrame() { }
        public void OnSurfaceSizeChanged(int width, int height) { }
    }
}