namespace RadioVolna
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            string savedTheme = Preferences.Get("AppTheme", "Dark");
            if (Enum.TryParse(typeof(AppTheme), savedTheme, out var theme))
            {
                UserAppTheme = (AppTheme)theme;
            }

            MainPage = new AppShell();
        }
    }
}
