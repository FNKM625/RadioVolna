namespace RadioVolna;

public interface IAudioService
{
    void Play(string url);
    void Pause();
    void Resume();
    void Stop();
    event EventHandler<bool> IsPlayingChanged;
    event EventHandler<string> StatusChanged;
}