// Plik: Platforms/Android/AudioService.Focus.cs
using Android.Content;
using Android.Media;
using Android.OS;

namespace RadioVolna;

public partial class AudioService
{
    // --- AUDIO FOCUS ---
    private bool RequestAudioFocus()
    {
        if (_audioManager == null) return true;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var attr = new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media).SetContentType(AudioContentType.Music).Build();
            _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain).SetAudioAttributes(attr).SetOnAudioFocusChangeListener(this).Build();
            return (int)_audioManager.RequestAudioFocus(_focusRequest) == (int)AudioFocusRequest.Granted;
        }
#pragma warning disable CS0618
        return (int)_audioManager.RequestAudioFocus(this, Android.Media.Stream.Music, AudioFocus.Gain) == (int)AudioFocus.Gain;
#pragma warning restore CS0618
    }

    private void AbandonAudioFocus()
    {
        if (_audioManager == null) return;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O && _focusRequest != null)
            _audioManager.AbandonAudioFocusRequest(_focusRequest);
#pragma warning disable CS0618
        else _audioManager.AbandonAudioFocus(this);
#pragma warning restore CS0618
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Gain:
                if (_resumeOnFocusGain) { Resume(); _resumeOnFocusGain = false; }
                if (_player != null) _player.SetVolume(1.0f, 1.0f);
                break;
            case AudioFocus.Loss: Stop(); break;
            case AudioFocus.LossTransient: Pause(); _resumeOnFocusGain = true; break;
            case AudioFocus.LossTransientCanDuck:
                if (_player != null) _player.SetVolume(0.1f, 0.1f);
                break;
        }
    }

    // --- NOISY AUDIO (ODŁĄCZENIE SŁUCHAWEK) ---

    private void RegisterNoisyReceiver()
    {
        if (!_isNoisyReceiverRegistered)
        {
            _noisyReceiver = new NoisyAudioReceiver(this);
            var filter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
            _context.RegisterReceiver(_noisyReceiver, filter);
            _isNoisyReceiverRegistered = true;
        }
    }

    private void UnregisterNoisyReceiver()
    {
        if (_isNoisyReceiverRegistered && _noisyReceiver != null)
        {
            try { _context.UnregisterReceiver(_noisyReceiver); } catch { }
            _isNoisyReceiverRegistered = false;
            _noisyReceiver = null;
        }
    }

    private class NoisyAudioReceiver : BroadcastReceiver
    {
        private readonly AudioService _service;
        public NoisyAudioReceiver(AudioService service) => _service = service;
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy)
            {
                _service.Pause();
            }
        }
    }
}