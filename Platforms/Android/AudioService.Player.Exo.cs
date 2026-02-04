// Plik: Platforms/Android/AudioService.Player.Exo.cs
using Android.Runtime;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer.Source;
using RadioVolna.Resources;

namespace RadioVolna;

public partial class AudioService
{
    private IExoPlayer? _exoPlayer;

    private void InitializeExoPlayer(string url)
    {
        Log("Inicjalizacja: ExoPlayer (Silnik zaawansowany)");
        try
        {
            var httpDataSourceFactory = new DefaultHttpDataSource.Factory()
                .SetAllowCrossProtocolRedirects(true)
                .SetUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var mediaSourceFactory = new DefaultMediaSourceFactory(_context)
                .SetDataSourceFactory(httpDataSourceFactory);

            _exoPlayer = new AndroidX.Media3.ExoPlayer.ExoPlayerBuilder(_context)
                .SetMediaSourceFactory(mediaSourceFactory.JavaCast<IMediaSourceFactory>())
                .Build();

            _exoPlayer.AddListener(new ExoPlayerListener(this));

            var mediaItem = AndroidX.Media3.Common.MediaItem.FromUri(Android.Net.Uri.Parse(url));

            _exoPlayer.SetMediaItem(mediaItem);
            _exoPlayer.Prepare();
            _exoPlayer.PlayWhenReady = true;

            IsPlayingChanged?.Invoke(this, true);
            string statusPrefix = LocalizationResourceManager.Instance["StatusPlayingExo"];
            StatusChanged?.Invoke(this, $"{statusPrefix} {_currentStationName}");
            UpdateSystemMediaInfo(true);
        }
        catch (Exception ex)
        {
            Log($"ExoPlayer Błąd: {ex.Message}");
            string errorMsg = LocalizationResourceManager.Instance["StatusErrorExo"];
            StatusChanged?.Invoke(this, errorMsg);
        }
    }

    private void StopExoPlayer()
    {
        if (_exoPlayer != null)
        {
            _exoPlayer.Stop();
            _exoPlayer.Release();
            _exoPlayer = null;
        }
    }

    private void PauseExoPlayer()
    {
        if (_exoPlayer != null) _exoPlayer.PlayWhenReady = false;
    }

    private void ResumeExoPlayer()
    {
        if (_exoPlayer != null) _exoPlayer.PlayWhenReady = true;
    }
}
