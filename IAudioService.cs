namespace RadioVolna;

public interface IAudioService
{
    void Play(string url, string stationName);
    void Pause();
    void Resume();
    void Stop();
    event EventHandler<bool> IsPlayingChanged;
    event EventHandler<string> StatusChanged;
}