// Plik: Platforms/Android/AudioService.Player.cs
using Android.Content;
using Android.Media;
using Android.OS;
using System.Diagnostics;
using System.Net.Http;
using RadioVolna.Resources;

namespace RadioVolna;

public partial class AudioService
{
    private CancellationTokenSource? _monitorCts;
    private bool _shouldBePlaying = false;

    private bool _isBuffering = false;
    private DateTime _bufferingStartTime;

    private void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[RADIO_LOG] {message}");
    }

    private void InitializeNativePlayer(string url)
    {
        Log("Inicjalizacja: Native MediaPlayer (Strażnik + ErrorHandler)");

        StopNativePlayer();

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
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" }
            };

            _player.SetDataSource(_context, uri, headers);

            _player.Prepared += (s, e) =>
            {
                _player.Start();
                _player.SetVolume(1.0f, 1.0f);

                _retryCount = 0;
                _shouldBePlaying = true;
                _isBuffering = false;

                IsPlayingChanged?.Invoke(this, true);

                string statusPlaying = LocalizationResourceManager.Instance["StatusPlaying"];
                StatusChanged?.Invoke(this, $"{statusPlaying} {_currentStationName}");

                UpdateSystemMediaInfo(true);

                StartMonitoring(url);
            };

            _player.Info += (s, e) =>
            {
                if (e.What == MediaInfo.BufferingStart)
                {
                    _isBuffering = true;
                    _bufferingStartTime = DateTime.Now;
                    StatusChanged?.Invoke(this, LocalizationResourceManager.Instance["StatusBuffering"]);
                }
                else if (e.What == MediaInfo.BufferingEnd)
                {
                    _isBuffering = false;
                    _retryCount = 0;
                    StatusChanged?.Invoke(this, $"{LocalizationResourceManager.Instance["StatusPlaying"]} {_currentStationName}");
                }
            };

            _player.Error += async (s, e) =>
            {
                Log($"[Native Error Callback] Code: {e.What}");
                if ((int)e.What == -38) return;

                if (_retryCount < MaxRetries)
                {
                    _retryCount++;
                    string weakSignal = LocalizationResourceManager.Instance["StatusWeakSignal"];
                    string msg = $"{weakSignal} ({_retryCount}/{MaxRetries})";
                    Log($"[Error Handler] Błąd playera. {msg}");

                    StatusChanged?.Invoke(this, msg);

                    await Task.Delay(3000);

                    InitializeNativePlayer(url);
                }
                else
                {
                    StatusChanged?.Invoke(this, LocalizationResourceManager.Instance["StatusConnectionError"]);
                    IsPlayingChanged?.Invoke(this, false);
                }
            };

            _player.PrepareAsync();
        }
        catch (Exception ex)
        {
            Log($"Native Exception: {ex.Message}");
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                if (_retryCount < MaxRetries) InitializeNativePlayer(url);
            });
        }
    }

    private void StopNativePlayer()
    {
        if (_monitorCts != null)
        {
            _monitorCts.Cancel();
            _monitorCts = null;
        }

        _shouldBePlaying = false;
        _isBuffering = false;

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

    private void StartMonitoring(string url)
    {
        if (_monitorCts != null) _monitorCts.Cancel();
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(2000, token);

                if (token.IsCancellationRequested) break;

                try
                {
                    if (_shouldBePlaying)
                    {
                        bool needsRestart = false;
                        string reason = "";

                        if (_player == null || (!_player.IsPlaying && !_isBuffering))
                        {
                            needsRestart = true;
                            reason = "Player zatrzymany";
                        }

                        if (_isBuffering && (DateTime.Now - _bufferingStartTime).TotalSeconds > 6)
                        {
                            needsRestart = true;
                            reason = "Zwiecha buforowania (>6s)";
                        }

                        if (needsRestart)
                        {
                            if (_retryCount < MaxRetries)
                            {
                                _retryCount++;
                                string weakSignal = LocalizationResourceManager.Instance["StatusWeakSignal"];
                                string msg = $"{weakSignal} ({_retryCount}/{MaxRetries})";
                                Log($"[Strażnik] Wykryto problem: {reason}. {msg}");

                                MainThread.BeginInvokeOnMainThread(() => StatusChanged?.Invoke(this, msg));

                                MainThread.BeginInvokeOnMainThread(() => InitializeNativePlayer(url));

                                break;
                            }
                            else
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    StatusChanged?.Invoke(this, LocalizationResourceManager.Instance["StatusNoNetwork"]);
                                    IsPlayingChanged?.Invoke(this, false);
                                    StopNativePlayer();
                                });
                                break;
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
        });
    }

    private async Task<string> CheckStreamFormatAsync(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request);
            if (response.Content.Headers.ContentType != null)
            return response.Content.Headers.ContentType.MediaType;
            
        }
        catch { return "unknown"; }
        return "unknown";
    }
}