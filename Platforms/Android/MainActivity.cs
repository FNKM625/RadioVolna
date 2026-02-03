using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;

namespace RadioVolna
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            CheckBatteryOptimizations();
        }

        private void CheckBatteryOptimizations()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var packageName = ApplicationContext.PackageName;
                var pm = (PowerManager)GetSystemService(Context.PowerService);

                if (pm != null && !pm.IsIgnoringBatteryOptimizations(packageName))
                {
                    var intent = new Intent();
                    intent.SetAction(Settings.ActionRequestIgnoreBatteryOptimizations);
                    intent.SetData(Android.Net.Uri.Parse("package:" + packageName));
                    StartActivity(intent);
                }
            }
        }
    }
}