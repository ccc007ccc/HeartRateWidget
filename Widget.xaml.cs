using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace HeartRateWidget
{
    public class HeartRateData
    {
        public int heart_rate { get; set; }
        public bool connected { get; set; }
    }

    public sealed partial class Widget : Page
    {
        private HttpClient httpClient;
        private DispatcherTimer timer;
        private readonly string apiPort = "8000";
        private string apiUrl;

        // 新增：定義基礎尺寸
        private const double baseWidth = 200.0;
        private const double baseHeight = 100.0;


        public Widget()
        {
            this.InitializeComponent();
            this.apiUrl = $"http://127.0.0.1:{apiPort}/heartrate";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            // 加載並應用已保存的設置
            LoadAndApplyOpacitySetting();
            LoadAndApplySizeSetting(); // 新增：加載尺寸設置

            Task.Run(() => UpdateHeartRate());
        }

        private async void Timer_Tick(object sender, object e)
        {
            await UpdateHeartRate();
        }

        private async Task UpdateHeartRate()
        {
            string displayText;
            string foregroundColor = "White";

            try
            {
                string jsonResponse = await httpClient.GetStringAsync(apiUrl);
                var data = JsonSerializer.Deserialize<HeartRateData>(jsonResponse);

                if (data.connected && data.heart_rate > 0)
                {
                    displayText = data.heart_rate.ToString();
                    if (data.heart_rate > 100) foregroundColor = "OrangeRed";
                    else if (data.heart_rate > 60) foregroundColor = "#33CC33";
                    else foregroundColor = "White";
                }
                else
                {
                    displayText = "--";
                    foregroundColor = "Gray";
                }
            }
            catch (Exception)
            {
                displayText = "N/A";
                foregroundColor = "Red";
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                HeartRateTextBlock.Text = displayText;
                HeartRateTextBlock.Foreground = new SolidColorBrush(
                    (Windows.UI.Color)Windows.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Windows.UI.Color), foregroundColor)
                );
            });
        }

        private void Grid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        // --- 不透明度調整代碼 (無變化) ---
        private void LoadAndApplyOpacitySetting()
        {
            object savedValue = ApplicationData.Current.LocalSettings.Values["backgroundOpacity"];
            double opacityValue = (savedValue is double) ? (double)savedValue : 10.0;
            OpacitySlider.Value = opacityValue;
            BackgroundBrush.Opacity = opacityValue / 100.0;
        }

        private void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (BackgroundBrush != null)
            {
                double newOpacity = e.NewValue / 100.0;
                BackgroundBrush.Opacity = newOpacity;
                ApplicationData.Current.LocalSettings.Values["backgroundOpacity"] = e.NewValue;
            }
        }

        // --- 新增代碼：用於尺寸調整 ---

        /// <summary>
        /// 加載並應用已保存的尺寸設置
        /// </summary>
        private void LoadAndApplySizeSetting()
        {
            // 嘗試讀取保存的值，如果不存在則使用默認值 100 (%)
            object savedValue = ApplicationData.Current.LocalSettings.Values["widgetSize"];
            double sizePercentage = (savedValue is double) ? (double)savedValue : 100.0;

            // 將值應用到滑塊
            SizeSlider.Value = sizePercentage;

            // 根據百分比計算並應用新的尺寸
            double scale = sizePercentage / 100.0;
            ContentGrid.Width = baseWidth * scale;
            ContentGrid.Height = baseHeight * scale;
        }

        /// <summary>
        /// 當尺寸滑塊的值改變時觸發
        /// </summary>
        private void SizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (ContentGrid != null)
            {
                // 計算縮放比例 (滑塊值 50-200 -> 比例 0.5-2.0)
                double scale = e.NewValue / 100.0;

                // 應用新的尺寸
                ContentGrid.Width = baseWidth * scale;
                ContentGrid.Height = baseHeight * scale;

                // 保存當前設置
                ApplicationData.Current.LocalSettings.Values["widgetSize"] = e.NewValue;
            }
        }
    }
}