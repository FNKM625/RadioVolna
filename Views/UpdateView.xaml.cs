using RadioVolna.Resources;

namespace RadioVolna.Views;

public partial class UpdateView : ContentView
{
    private string? _downloadUrl;
    private string? _latestBuild;

    public UpdateView()
    {
        InitializeComponent();
    }

    public async Task ShowUpdateAsync(string version, string build, string downloadUrl)
    {
        _downloadUrl = downloadUrl;
        _latestBuild = build;

        TitleLabel.Text = LocalizationResourceManager.Instance["UpdateTitle"];
        MessageLabel.Text = string.Format(
            LocalizationResourceManager.Instance["UpdateMessage"],
            version
        );

        this.IsVisible = true;
        this.Opacity = 0;
        await this.FadeTo(1, 300);
    }

    private async void OnDownloadClicked(object sender, EventArgs e)
    {
        this.IsVisible = false;
        if (!string.IsNullOrEmpty(_downloadUrl))
            await Launcher.OpenAsync(_downloadUrl);
    }

    private void OnLaterClicked(object sender, EventArgs e)
    {
        this.IsVisible = false;
    }

    private void OnSkipClicked(object sender, EventArgs e)
    {
        this.IsVisible = false;
        if (!string.IsNullOrEmpty(_latestBuild))
        {
            Preferences.Default.Set("IgnoredUpdateBuild", _latestBuild);
        }
    }

    private void OnWindowContentTapped(object sender, EventArgs e) { }
}
