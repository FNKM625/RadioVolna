// Plik: Platforms/Android/AudioService.Player.cs
// (To jest ten "Native Player" - MediaPlayer)
using Android.Content;
using Android.Media;
using Android.OS;
using System.Diagnostics;
using System.Net.Http;

namespace RadioVolna;

public partial class AudioService
{
    // Logowanie
    private void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[RADIO_LOG] {message}");
        Android.Util.Log.Info("RADIO_LOG", message);
    }

    // ZMIANA NAZWY: InitializePlayer -> InitializeNativePlayer
    private void InitializeNativePlayer(string url)
    {
        Log("Inicjalizacja: Native MediaPlayer (Silnik lekki)");
        try
        {
            _player = new MediaPlayer();
            _player.SetWakeMode(_context, WakeLockFlags.Partial);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                _player.SetAudioAttributes(new AudioAttributes.Builder()
                    .SetContentType(AudioContentType.Music)
                    .SetUsage(AudioUsageKind.Media)
                    .Build());
            }

            var uri = Android.Net.Uri.Parse(url);
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" }
            };

            _player.SetDataSource(_context, uri, headers);

            _player.Prepared += (s, e) =>
            {
                _player.Start();
                _player.SetVolume(1.0f, 1.0f);
                IsPlayingChanged?.Invoke(this, true);
                StatusChanged?.Invoke(this, $"Gra: {_currentStationName}");
                UpdateSystemMediaInfo(true);
            };

            _player.Info += (s, e) =>
            {
                if (e.What == MediaInfo.BufferingStart) StatusChanged?.Invoke(this, "Buforowanie...");
                else if (e.What == MediaInfo.BufferingEnd) StatusChanged?.Invoke(this, $"Gra: {_currentStationName}");
            };

            _player.Error += (s, e) =>
            {
                if ((int)e.What != -38)
                {
                    Log($"Native Error: {(int)e.What}");
                    StatusChanged?.Invoke(this, $"Błąd Native: {(int)e.What}");
                    // W razie błędu native'a, można by tu dodać fallback do Exo, ale na razie tylko stop
                    IsPlayingChanged?.Invoke(this, false);
                }
            };

            _player.PrepareAsync();
        }
        catch (Exception ex)
        {
            Log($"Native Exception: {ex.Message}");
        }
    }

    private void StopNativePlayer()
    {
        if (_player != null)
        {
            try { if (_player.IsPlaying) _player.Stop(); } catch { }
            try { _player.Release(); } catch { }
            _player = null;
        }
    }

    // Metoda sprawdzająca format (zwraca teraz Task<string>)
    private async Task<string> CheckStreamFormatAsync(string url)
    {
        Log($"Sprawdzam nagłówki: {url}");
        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5); // Szybki timeout
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await client.SendAsync(request);

                if (response.Content.Headers.ContentType != null)
                {
                    string mime = response.Content.Headers.ContentType.MediaType;
                    Log($"Wykryto format: {mime}");
                    return mime;
                }
            }
        }
        catch (Exception ex) { Log($"Błąd sprawdzania: {ex.Message}"); }
        return "unknown";
    }
}