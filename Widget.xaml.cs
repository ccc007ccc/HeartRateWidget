using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace HeartRateWidget
{
    public sealed partial class Widget : Page
    {
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        private DispatcherTimer timer;
        private string apiPort = "8000";
        private string apiUrl;

        private const double baseWidth = 200.0;
        private const double baseHeight = 100.0;

        // 只保留需要的设置键
        private const string SettingApiPort = "apiPort";
        private const string SettingWidgetSize = "widgetSize";
        private const string SettingBgColorR = "bgColorR";
        private const string SettingBgColorG = "bgColorG";
        private const string SettingBgColorB = "bgColorB";
        private const string SettingBackgroundOpacity = "backgroundOpacity";
        private const string SettingIsBlurEffectEnabled = "isBlurEffectEnabled";

        public Widget()
        {
            this.InitializeComponent();
            this.Loaded += Widget_Loaded;
            this.Unloaded += Widget_Unloaded;
        }

        private void Widget_Loaded(object sender, RoutedEventArgs e)
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick;

            LoadAllSettings();
            SubscribeToSettingChanges();

            UpdateApiUrl();
            Task.Run(UpdateHeartRate);
        }

        private void Widget_Unloaded(object sender, RoutedEventArgs e)
        {
            timer?.Stop();
            timer = null;
        }

        private void UpdateApiUrl()
        {
            apiUrl = $"http://127.0.0.1:{apiPort}/heartrate";
        }

        private void LoadAllSettings()
        {
            object port = ApplicationData.Current.LocalSettings.Values[SettingApiPort];
            apiPort = port is string p && !string.IsNullOrWhiteSpace(p) ? p : "8000";
            ApiPortTextBox.Text = apiPort;

            object size = ApplicationData.Current.LocalSettings.Values[SettingWidgetSize]; SizeSlider.Value = (size is double s) ? s : 65.0;
            object r = ApplicationData.Current.LocalSettings.Values[SettingBgColorR]; object g = ApplicationData.Current.LocalSettings.Values[SettingBgColorG]; object b = ApplicationData.Current.LocalSettings.Values[SettingBgColorB]; RedSlider.Value = (r is double rd) ? rd : 0; GreenSlider.Value = (g is double gd) ? gd : 0; BlueSlider.Value = (b is double bd) ? bd : 0;
            object opacity = ApplicationData.Current.LocalSettings.Values[SettingBackgroundOpacity]; OpacitySlider.Value = (opacity is double o) ? o : 70.0;
            object blur = ApplicationData.Current.LocalSettings.Values[SettingIsBlurEffectEnabled]; BlurEffectToggle.IsOn = (blur is bool bl) ? bl : true;

            UpdateBackground();
            UpdateWidgetSize(SizeSlider.Value);
        }

        private void SubscribeToSettingChanges()
        {
            BlurEffectToggle.Toggled += BlurEffectToggle_Toggled;
            OpacitySlider.ValueChanged += OpacitySlider_ValueChanged;
            RedSlider.ValueChanged += ColorSliders_ValueChanged;
            GreenSlider.ValueChanged += ColorSliders_ValueChanged;
            BlueSlider.ValueChanged += ColorSliders_ValueChanged;
            SizeSlider.ValueChanged += SizeSlider_ValueChanged;
            ApiPortTextBox.LostFocus += ApiPortTextBox_LostFocus;
        }

        private void UnsubscribeFromSettingChanges()
        {
            BlurEffectToggle.Toggled -= BlurEffectToggle_Toggled;
            OpacitySlider.ValueChanged -= OpacitySlider_ValueChanged;
            RedSlider.ValueChanged -= ColorSliders_ValueChanged;
            GreenSlider.ValueChanged -= ColorSliders_ValueChanged;
            BlueSlider.ValueChanged -= ColorSliders_ValueChanged;
            SizeSlider.ValueChanged -= SizeSlider_ValueChanged;
            ApiPortTextBox.LostFocus -= ApiPortTextBox_LostFocus;
        }

        private void ApiPortTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var newPort = ApiPortTextBox.Text.Trim();
            if (int.TryParse(newPort, out int portNumber) && portNumber > 0 && portNumber <= 65535)
            {
                if (apiPort != newPort)
                {
                    apiPort = newPort;
                    ApplicationData.Current.LocalSettings.Values[SettingApiPort] = newPort;
                    UpdateApiUrl();
                    Task.Run(UpdateHeartRate); // 端口改变后立即刷新一次
                }
            }
            else
            {
                ApiPortTextBox.Text = apiPort;
            }
        }

        private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values.Clear();
            UnsubscribeFromSettingChanges();
            LoadAllSettings();
            SubscribeToSettingChanges();
            UpdateApiUrl();
            Task.Run(UpdateHeartRate);
        }

        private async void Timer_Tick(object sender, object e)
        {
            timer?.Stop();
            await UpdateHeartRate();
        }

        private async Task UpdateHeartRate()
        {
            try
            {
                string jsonResponse = await httpClient.GetStringAsync(apiUrl);
                await ProcessHeartRateJson(jsonResponse);
            }
            catch (Exception)
            {
                await UpdateDisplay("N/A", "Red");
            }
            finally
            {
                // 派发回UI线程以安全地启动定时器
                var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    timer?.Start();
                });
            }
        }

        private async Task ProcessHeartRateJson(string json)
        {
            try
            {
                var data = JsonSerializer.Deserialize<HeartRateData>(json);
                if (data.connected && data.heart_rate > 0)
                {
                    string foregroundColor;
                    if (data.heart_rate > 100) foregroundColor = "OrangeRed";
                    else if (data.heart_rate > 60) foregroundColor = "#33CC33";
                    else foregroundColor = "White";
                    await UpdateDisplay(data.heart_rate.ToString(), foregroundColor);
                }
                else
                {
                    await UpdateDisplay("--", "Gray");
                }
            }
            catch (Exception)
            {
                await UpdateDisplay("Err", "Yellow");
            }
        }

        private async Task UpdateDisplay(string text, string color)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                HeartRateTextBlock.Text = text;
                HeartRateTextBlock.Foreground = new SolidColorBrush((Color)Windows.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Color), color));
            });
        }

        private void UpdateBackground() { var acrylicBrush = (AcrylicBrush)this.Resources["AcrylicBackgroundBrush"]; var solidBrush = (SolidColorBrush)this.Resources["SolidBackgroundBrush"]; var color = Color.FromArgb(255, (byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value); double opacity = OpacitySlider.Value / 100.0; acrylicBrush.TintColor = color; acrylicBrush.TintOpacity = opacity; solidBrush.Color = color; solidBrush.Opacity = opacity; if (BlurEffectToggle.IsOn) { ContentGrid.Background = acrylicBrush; } else { ContentGrid.Background = solidBrush; } }
        private void UpdateWidgetSize(double newValue) { if (ContentGrid != null) { double scale = newValue / 100.0; ContentGrid.Width = baseWidth * scale; ContentGrid.Height = baseHeight * scale; } }
        private void BlurEffectToggle_Toggled(object sender, RoutedEventArgs e) { UpdateBackground(); ApplicationData.Current.LocalSettings.Values[SettingIsBlurEffectEnabled] = BlurEffectToggle.IsOn; }
        private void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) { UpdateBackground(); ApplicationData.Current.LocalSettings.Values[SettingBackgroundOpacity] = e.NewValue; }
        private void ColorSliders_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) { UpdateBackground(); ApplicationData.Current.LocalSettings.Values[SettingBgColorR] = RedSlider.Value; ApplicationData.Current.LocalSettings.Values[SettingBgColorG] = GreenSlider.Value; ApplicationData.Current.LocalSettings.Values[SettingBgColorB] = BlueSlider.Value; }
        private void SizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) { UpdateWidgetSize(e.NewValue); ApplicationData.Current.LocalSettings.Values[SettingWidgetSize] = e.NewValue; }
        private void Grid_RightTapped(object sender, RightTappedRoutedEventArgs e) { FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender); }
        public class HeartRateData { public int heart_rate { get; set; } public bool connected { get; set; } }
    }
}