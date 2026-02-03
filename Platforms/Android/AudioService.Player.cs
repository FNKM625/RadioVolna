// Plik: Platforms/Android/AudioService.Player.cs
using Android.Content;
using Android.Media;
using Android.Net.Wifi;
using Android.OS;

namespace RadioVolna;

public partial class AudioService
{
    private void InitializePlayer(string url)
    {
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
                RegisterNoisyReceiver();
                IsPlayingChanged?.Invoke(this, true);
                StatusChanged?.Invoke(this, $"Gra: {_currentStationName}");
                UpdateSystemMediaInfo(true);
            };

            _player.Error += (s, e) =>
            {
                StatusChanged?.Invoke(this, $"Błąd radia: {e.What}");
                IsPlayingChanged?.Invoke(this, false);
                UpdateSystemMediaInfo(false);
                ReleaseLocks();
            };

            _player.Completion += (s, e) =>
            {
                IsPlayingChanged?.Invoke(this, false);
                StatusChanged?.Invoke(this, "Zakończono");
                UpdateSystemMediaInfo(false);
                ReleaseLocks();
            };

            StatusChanged?.Invoke(this, "Łączenie...");
            _player.PrepareAsync();
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Błąd: {ex.Message}");
            ReleaseLocks();
        }
    }

    private void PausePlayerInternal()
    {
        if (_player != null)
        {
            try { if (_player.IsPlaying) _player.Pause(); } catch { }
        }
    }

    private bool ResumePlayerInternal()
    {
        if (_player != null)
        {
            try
            {
                if (!_player.IsPlaying) _player.Start();
                return true;
            }
            catch { return false; }
        }
        return false;
    }

    private void StopPlayerOnly()
    {
        ReleaseLocks();
        UnregisterNoisyReceiver();
        if (_player != null)
        {
            try { if (_player.IsPlaying) _player.Stop(); } catch { }
            try { _player.Release(); } catch { }
            _player = null;
        }
    }

    // --- ZARZĄDZANIE BLOKADAMI (BATERIA / WIFI) ---
    private void AcquireLocks()
    {
        try
        {
            if (_wifiLock == null)
            {
                var wm = _context.GetSystemService(Context.WifiService) as WifiManager;
                if (wm != null) _wifiLock = wm.CreateWifiLock(Android.Net.WifiMode.FullHighPerf, "RadioVolnaWifiLock");
            }
            if (_wifiLock != null && !_wifiLock.IsHeld) _wifiLock.Acquire();
        }
        catch { }

        try
        {
            if (_powerWakeLock == null)
            {
                var pm = _context.GetSystemService(Context.PowerService) as PowerManager;
                if (pm != null) _powerWakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "RadioVolnaPowerLock");
            }
            if (_powerWakeLock != null && !_powerWakeLock.IsHeld) _powerWakeLock.Acquire();
        }
        catch { }
    }

    private void ReleaseLocks()
    {
        try { if (_wifiLock != null && _wifiLock.IsHeld) _wifiLock.Release(); } catch { }
        try { if (_powerWakeLock != null && _powerWakeLock.IsHeld) _powerWakeLock.Release(); } catch { }
    }
}