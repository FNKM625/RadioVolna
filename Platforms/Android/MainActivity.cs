// Plik: Platforms/Android/MainActivity.cs
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using RadioVolna.Resources;

namespace RadioVolna
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const string PrefKeyDontAskBattery = "BatteryOpt_DontAskAgain";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CheckBatteryOptimizations();
            });
        }

        private void CheckBatteryOptimizations()
        {
            bool dontAskAgain = Preferences.Get(PrefKeyDontAskBattery, false);
            if (dontAskAgain)
            {
                return;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var packageName = ApplicationContext.PackageName;
                var pm = (PowerManager)GetSystemService(Context.PowerService);

                if (pm != null && pm.IsIgnoringBatteryOptimizations(packageName))
                {
                    return;
                }
                ShowExplanationDialog(packageName);
            }
        }

        private void ShowExplanationDialog(string packageName)
        {
            var builder = new Android.App.AlertDialog.Builder(this);
            builder.SetTitle(LocalizationResourceManager.Instance["TitleBatteryOpt"]);
            builder.SetMessage(LocalizationResourceManager.Instance["MsgBatteryOpt"]);

            var container = new FrameLayout(this);
            var paramsF = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            int margin = (int)(20 * Resources.DisplayMetrics.Density);
            paramsF.SetMargins(margin, 0, margin, 0);

            var checkBox = new Android.Widget.CheckBox(this);
            checkBox.Text = LocalizationResourceManager.Instance["LabelDontAskAgain"];
            checkBox.LayoutParameters = paramsF;

            container.AddView(checkBox);
            builder.SetView(container);

            builder.SetPositiveButton(LocalizationResourceManager.Instance["BtnSettings"], (sender, args) =>
            {
                if (checkBox.Checked) Preferences.Set(PrefKeyDontAskBattery, true);
                OpenSettings(packageName);
            });

            builder.SetNegativeButton(LocalizationResourceManager.Instance["BtnSkip"], (sender, args) =>
            {
                if (checkBox.Checked) Preferences.Set(PrefKeyDontAskBattery, true);
                Toast.MakeText(this, LocalizationResourceManager.Instance["MsgBatteryOptToast"], ToastLength.Long)?.Show();
            });

            builder.SetCancelable(false);
            builder.Show();
        }

        private void OpenSettings(string packageName)
        {
            try
            {
                var intent = new Intent();
                intent.SetAction(Settings.ActionRequestIgnoreBatteryOptimizations);
                intent.SetData(Android.Net.Uri.Parse("package:" + packageName));
                StartActivity(intent);
            }
            catch
            {
                try
                {
                    StartActivity(new Intent(Settings.ActionIgnoreBatteryOptimizationSettings));
                }
                catch { }
            }
        }
    }
}