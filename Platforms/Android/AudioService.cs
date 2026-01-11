#nullable enable
#pragma warning disable CS8618, CS8602, CS8600, CS0168, CA1416

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;

// Używamy nowych bibliotek AndroidX Media3
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer.Source;

namespace RadioVolna;

public class AudioService : Java.Lang.Object, IAudioService, IPlayerListener, Android.Media.AudioManager.IOnAudioFocusChangeListener
{
    private IExoPlayer? _exoPlayer;
    private Android.Media.AudioManager? _audioManager;
    private Context _context;

    private Android.Media.AudioFocusRequestClass? _focusRequest;
    private bool _resumeOnFocusGain = false;

    // Zmienne stacji
    private string _currentStationName = "Radio Volna";
    private string _currentUrl = "";

    public event EventHandler<bool>? IsPlayingChanged;
    public event EventHandler<string>? StatusChanged;

    public AudioService()
    {
        _context = Android.App.Application.Context;
        _audioManager = _context.GetSystemService(Context.AudioService) as Android.Media.AudioManager;
    }

    public void Play(string url, string stationName)
    {
        // 1. Audio Focus (Wyciszenie innych apek)
        if (!RequestAudioFocus())
        {
            StatusChanged?.Invoke(this, "Błąd: Audio zajęte");
            return;
        }

        _currentStationName = stationName;
        _currentUrl = url;
        ReleasePlayer();

        try
        {
            // 2. Konfiguracja User-Agent (Kluczowe dla RMF FM)
            var httpDataSourceFactory = new DefaultHttpDataSource.Factory()
                .SetAllowCrossProtocolRedirects(true)
                .SetUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // 3. Fabryka mediów
            var mediaSourceFactory = new DefaultMediaSourceFactory(_context)
                .SetDataSourceFactory(httpDataSourceFactory);

            // 4. Budujemy ExoPlayera (Poprawka: ExoPlayerBuilder)
            _exoPlayer = new AndroidX.Media3.ExoPlayer.ExoPlayerBuilder(_context)
                .SetMediaSourceFactory((IMediaSourceFactory)mediaSourceFactory) // Rzutowanie naprawia błąd CS1503
                .Build();

            _exoPlayer.AddListener(this);

            // 5. Ładujemy i gramy
            var mediaItem = MediaItem.FromUri(Android.Net.Uri.Parse(url));
            _exoPlayer.SetMediaItem(mediaItem);
            _exoPlayer.Prepare();
            _exoPlayer.PlayWhenReady = true;

            StatusChanged?.Invoke(this, "Łączenie...");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Błąd Playera: {ex.Message}");
        }
    }

    public void Pause()
    {
        if (_exoPlayer != null)
        {
            _exoPlayer.PlayWhenReady = false;
            StatusChanged?.Invoke(this, "Wstrzymano");
            IsPlayingChanged?.Invoke(this, false);
        }
    }

    public void Resume()
    {
        if (!RequestAudioFocus()) return;

        if (_exoPlayer != null)
        {
            _exoPlayer.PlayWhenReady = true;
            StatusChanged?.Invoke(this, "Gra");
            IsPlayingChanged?.Invoke(this, true);
        }
        else if (!string.IsNullOrEmpty(_currentUrl))
        {
            Play(_currentUrl, _currentStationName);
        }
    }

    public void Stop()
    {
        AbandonAudioFocus();
        ReleasePlayer();
        StatusChanged?.Invoke(this, "Zatrzymano");
        IsPlayingChanged?.Invoke(this, false);
    }

    private void ReleasePlayer()
    {
        if (_exoPlayer != null)
        {
            _exoPlayer.RemoveListener(this);
            _exoPlayer.Stop();
            _exoPlayer.Release();
            _exoPlayer = null;
        }
    }

    // --- OBSŁUGA ZDARZEŃ (Poprawiona obsługa nulli) ---
    public void OnPlaybackStateChanged(int playbackState)
    {
        // 2=Buffering, 3=Ready, 4=Ended
        switch (playbackState)
        {
            case 2:
                StatusChanged?.Invoke(this, "Buforowanie...");
                break;
            case 3:
                if (_exoPlayer != null && _exoPlayer.PlayWhenReady)
                {
                    StatusChanged?.Invoke(this, $"Gra: {_currentStationName}");
                    IsPlayingChanged?.Invoke(this, true);
                }
                break;
            case 4:
                IsPlayingChanged?.Invoke(this, false);
                break;
        }
    }

    public void OnPlayerError(PlaybackException? error)
    {
        string msg = error != null ? error.ErrorCodeName : "Nieznany błąd";
        StatusChanged?.Invoke(this, $"Błąd: {msg}");
        IsPlayingChanged?.Invoke(this, false);
    }

    public void OnIsPlayingChanged(bool isPlaying) => IsPlayingChanged?.Invoke(this, isPlaying);

    // Puste metody wymagane przez interfejs (z dodanymi znakami zapytania dla bezpieczeństwa)
    public void OnLoadingChanged(bool isLoading) { }
    public void OnPositionDiscontinuity(int reason) { }
    public void OnRepeatModeChanged(int repeatMode) { }
    public void OnShuffleModeEnabledChanged(bool shuffleModeEnabled) { }
    public void OnTimelineChanged(Timeline? timeline, int reason) { }
    public void OnTracksChanged(Tracks? tracks) { }


    // --- AUDIO FOCUS ---
    private bool RequestAudioFocus()
    {
        if (_audioManager == null) return true;
        int resultInt = 0;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var attr = new Android.Media.AudioAttributes.Builder()
                .SetUsage(Android.Media.AudioUsageKind.Media)
                .SetContentType(Android.Media.AudioContentType.Music)
                .Build();

            _focusRequest = new Android.Media.AudioFocusRequestClass.Builder(Android.Media.AudioFocus.Gain)
                .SetAudioAttributes(attr)
                .SetOnAudioFocusChangeListener(this)
                .Build();

            var resObj = _audioManager.RequestAudioFocus(_focusRequest);
            resultInt = (int)resObj; // Rzutowanie na int
        }
        else
        {
#pragma warning disable CS0618
            var resObj = _audioManager.RequestAudioFocus(this, Android.Media.Stream.Music, Android.Media.AudioFocus.Gain);
            resultInt = (int)resObj;
#pragma warning restore CS0618
        }

        return resultInt == 1; // 1 = GRANTED
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

    public void OnAudioFocusChange(Android.Media.AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case Android.Media.AudioFocus.Gain:
                if (_resumeOnFocusGain) { Resume(); _resumeOnFocusGain = false; }
                if (_exoPlayer != null) _exoPlayer.Volume = 1.0f;
                break;
            case Android.Media.AudioFocus.Loss:
                Stop();
                break;
            case Android.Media.AudioFocus.LossTransient:
                Pause();
                _resumeOnFocusGain = true;
                break;
            case Android.Media.AudioFocus.LossTransientCanDuck:
                if (_exoPlayer != null) _exoPlayer.Volume = 0.2f;
                break;
        }
    }
}