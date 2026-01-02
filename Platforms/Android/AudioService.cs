using Android.Media;
using Android.Content;
using Android.OS;

namespace RadioVolna;

public class AudioService : IAudioService
{
    private MediaPlayer? _player;

    public event EventHandler<bool>? IsPlayingChanged;
    public event EventHandler<string>? StatusChanged;

    public void Play(string url)
    {
        Stop();

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
            };

            _player.Error += (s, e) =>
            {
                StatusChanged?.Invoke(this, $"Błąd strumienia: {e.What}");
                IsPlayingChanged?.Invoke(this, false);
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
        }
    }

    public void Resume()
    {
        if (_player != null && !_player.IsPlaying)
        {
            _player.Start();
            IsPlayingChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, "Połączenie nawiązane");
        }
    }

    public void Stop()
    {
        if (_player != null)
        {
            try
            {
                if (_player.IsPlaying)
                {
                    _player.Stop();
                }
                _player.Release();
            }
            catch { }

            _player = null;
        }

        IsPlayingChanged?.Invoke(this, false);
        StatusChanged?.Invoke(this, "Zatrzymano");
    }
}