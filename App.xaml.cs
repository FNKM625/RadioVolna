using RadioVolna.Resources;
using System.Globalization;

namespace RadioVolna
{
    public partial class App : Application
    {
        public App(IAudioService audioService)
        {
            InitializeComponent();

            string savedLang = Preferences.Get("Language", null);

            CultureInfo cultureToSet;

            if (!string.IsNullOrEmpty(savedLang))
            {
                cultureToSet = new CultureInfo(savedLang);
            }
            else
            {
                var systemCulture = CultureInfo.CurrentCulture;
                string langCode = systemCulture.TwoLetterISOLanguageName.ToLower();

                if (langCode == "pl")
                {
                    cultureToSet = new CultureInfo("pl");
                    Preferences.Set("Language", "pl");
                }
                else if (langCode == "ru")
                {
                    cultureToSet = new CultureInfo("ru");
                    Preferences.Set("Language", "ru");
                }
                else
                {
                    cultureToSet = new CultureInfo("en");
                    Preferences.Set("Language", "en");
                }
            }

            LocalizationResourceManager.Instance.SetCulture(cultureToSet);

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
