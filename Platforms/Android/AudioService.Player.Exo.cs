// Plik: Platforms/Android/AudioService.Player.Exo.cs
using Android.Runtime; // <--- WAŻNE: To pozwala na użycie JavaCast
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer.Source;

namespace RadioVolna;

public partial class AudioService
{
    private IExoPlayer? _exoPlayer;

    private void InitializeExoPlayer(string url)
    {
        Log("Inicjalizacja: ExoPlayer (Silnik zaawansowany)");
        try
        {
            // 1. Konfiguracja User-Agent
            var httpDataSourceFactory = new DefaultHttpDataSource.Factory()
                .SetAllowCrossProtocolRedirects(true)
                .SetUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var mediaSourceFactory = new DefaultMediaSourceFactory(_context)
                .SetDataSourceFactory(httpDataSourceFactory);

            // 2. Budowa playera (NAPRAWIONE JavaCast)
            _exoPlayer = new AndroidX.Media3.ExoPlayer.ExoPlayerBuilder(_context)
                // Tutaj używamy JavaCast, żeby naprawić błąd CS1503/CS0030
                .SetMediaSourceFactory(mediaSourceFactory.JavaCast<IMediaSourceFactory>())
                .Build();

            // 3. Listenery
            _exoPlayer.AddListener(new ExoPlayerListener(this));

            // 4. Start
            var mediaItem = AndroidX.Media3.Common.MediaItem.FromUri(Android.Net.Uri.Parse(url));

            _exoPlayer.SetMediaItem(mediaItem);
            _exoPlayer.Prepare();
            _exoPlayer.PlayWhenReady = true;

            // Logika UI
            IsPlayingChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, $"Gra (Exo): {_currentStationName}");
            UpdateSystemMediaInfo(true);
        }
        catch (Exception ex)
        {
            Log($"ExoPlayer Błąd: {ex.Message}");
            StatusChanged?.Invoke(this, "Błąd ExoPlayera");
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
