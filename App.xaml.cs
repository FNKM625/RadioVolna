using RadioVolna.Resources;
using System.Globalization;

namespace RadioVolna
{
    public partial class App : Application
    {
        public App(IAudioService audioService)
        {
            InitializeComponent();

            string savedLang = Preferences.Get("Language", "en");
            LocalizationResourceManager.Instance.SetCulture(new CultureInfo(savedLang));

            string savedTheme = Preferences.Get("AppTheme", "Dark");
            if (Enum.TryParse(typeof(AppTheme), savedTheme, out var theme))
            {
                UserAppTheme = (AppTheme)theme;
            }

            MainPage = new AppShell();
            MainPage = new MainPage(audioService);
        }
    }
}
