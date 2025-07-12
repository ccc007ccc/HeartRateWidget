using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
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
        // --- WebSocket 客户端 ---
        private ClientWebSocket webSocket;
        private CancellationTokenSource cts;

        // --- HTTP 客户端 (保留作为备用) ---
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        private DispatcherTimer httpTimer;

        // --- 连接设置 ---
        private string httpApiIp = "127.0.0.1";
        private string httpApiPort = "8000";
        private string webSocketIp = "127.0.0.1";
        private string webSocketPort = "8001";

        // --- UI 基础尺寸 ---
        private const double baseWidth = 200.0;
        private const double baseHeight = 100.0;

        // --- 设置存储键 ---
        private const string SettingConnectionType = "connectionType";
        private const string SettingHttpApiIp = "httpApiIp";
        private const string SettingHttpApiPort = "httpApiPort";
        private const string SettingWebSocketIp = "webSocketIp";
        private const string SettingWebSocketPort = "webSocketPort";
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
            LoadAllSettings();
            SubscribeToSettingChanges();
            StartConnection();
        }

        private async void Widget_Unloaded(object sender, RoutedEventArgs e)
        {
            httpTimer?.Stop();
            await DisconnectWebSocketAsync();
        }

        #region Connection Management

        /// <summary>
        /// 根据用户选择，启动相应的连接任务 (WebSocket或HTTP)
        /// </summary>
        private void StartConnection()
        {
            // 确保先断开所有旧的连接
            httpTimer?.Stop();
            httpTimer = null;
            // 异步断开WebSocket，不阻塞UI线程
            Task.Run(DisconnectWebSocketAsync);

            // 根据下拉框选择的模式启动连接
            if (ConnectionTypeComboBox.SelectedIndex == 0) // WebSocket
            {
                Task.Run(ConnectWebSocketAsync);
            }
            else // HTTP
            {
                httpTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                httpTimer.Tick += HttpTimer_Tick;
                Task.Run(UpdateHeartRateViaHttp); // 立即执行一次
                httpTimer.Start();
            }
        }

        /// <summary>
        /// 异步连接到 WebSocket 服务器，包含重试逻辑。
        /// </summary>
        private async Task ConnectWebSocketAsync()
        {
            // 如果已经连接或正在连接，则直接返回
            if (webSocket != null && (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting))
            {
                return;
            }

            // UI提示正在连接
            await UpdateDisplay("...", "Gray");

            // 创建新的CancellationTokenSource和ClientWebSocket实例
            cts = new CancellationTokenSource();
            webSocket = new ClientWebSocket();
            var uri = new Uri($"ws://{webSocketIp}:{webSocketPort}");

            try
            {
                // 尝试连接
                await webSocket.ConnectAsync(uri, cts.Token);
                // 连接成功后，开始监听消息
                await ListenForMessagesAsync(webSocket, cts.Token);
            }
            catch (Exception)
            {
                // 如果连接失败，则触发断线处理逻辑
                await HandleDisconnection();
            }
        }

        /// <summary>
        /// 循环监听来自服务器的消息，直到连接关闭。
        /// </summary>
        private async Task ListenForMessagesAsync(ClientWebSocket ws, CancellationToken token)
        {
            var buffer = new byte[1024 * 4];
            while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                try
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessHeartRateJson(json);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleDisconnection();
                        break;
                    }
                }
                catch (Exception)
                {
                    await HandleDisconnection();
                    break;
                }
            }
        }

        /// <summary>
        /// 优雅地断开并清理 WebSocket 资源。
        /// </summary>
        private async Task DisconnectWebSocketAsync()
        {
            if (webSocket != null)
            {
                cts?.Cancel(); // 取消所有与此WebSocket相关的异步操作
                if (webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                    }
                    catch { /* 忽略关闭时可能发生的异常 */ }
                }
                webSocket.Dispose();
                webSocket = null;
                cts?.Dispose();
            }
        }

        /// <summary>
        /// 处理断线情况：更新UI，清理资源，并在延迟后尝试重连。
        /// </summary>
        private async Task HandleDisconnection()
        {
            // 确保断开并清理旧的连接资源
            await DisconnectWebSocketAsync();
            // 在UI上显示断线状态
            await UpdateDisplay("N/A", "Red");

            // 延迟5秒，避免过于频繁地重连
            await Task.Delay(5000);

            // 只有当用户仍然选择WebSocket模式时才重连
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (ConnectionTypeComboBox.SelectedIndex == 0)
                {
                    Task.Run(ConnectWebSocketAsync);
                }
            });
        }

        #endregion

        #region HTTP Polling (Legacy Mode)

        private async void HttpTimer_Tick(object sender, object e)
        {
            await UpdateHeartRateViaHttp();
        }

        private async Task UpdateHeartRateViaHttp()
        {
            try
            {
                var apiUrl = $"http://{httpApiIp}:{httpApiPort}/heartrate";
                string jsonResponse = await httpClient.GetStringAsync(apiUrl);
                await ProcessHeartRateJson(jsonResponse);
            }
            catch (Exception)
            {
                await UpdateDisplay("N/A", "Red");
            }
        }

        #endregion

        #region UI Update & Data Processing

        /// <summary>
        /// 解析服务器发来的JSON数据并更新UI显示。
        /// </summary>
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
            catch (JsonException)
            {
                await UpdateDisplay("Err", "Yellow"); // JSON格式错误
            }
        }

        /// <summary>
        /// 安全地在UI线程上更新心率文本和颜色。
        /// </summary>
        private async Task UpdateDisplay(string text, string color)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                HeartRateTextBlock.Text = text;
                HeartRateTextBlock.Foreground = new SolidColorBrush((Color)Windows.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Color), color));
            });
        }

        #endregion

        #region Settings Management

        private void LoadAllSettings()
        {
            ConnectionTypeComboBox.SelectedIndex = (ApplicationData.Current.LocalSettings.Values[SettingConnectionType] as int?) ?? 0;
            UpdateSettingsPanelVisibility();

            httpApiIp = (ApplicationData.Current.LocalSettings.Values[SettingHttpApiIp] as string) ?? "127.0.0.1";
            ApiIpTextBox.Text = httpApiIp;
            httpApiPort = (ApplicationData.Current.LocalSettings.Values[SettingHttpApiPort] as string) ?? "8000";
            ApiPortTextBox.Text = httpApiPort;

            webSocketIp = (ApplicationData.Current.LocalSettings.Values[SettingWebSocketIp] as string) ?? "127.0.0.1";
            WebSocketIpTextBox.Text = webSocketIp;
            webSocketPort = (ApplicationData.Current.LocalSettings.Values[SettingWebSocketPort] as string) ?? "8001";
            WebSocketPortTextBox.Text = webSocketPort;

            SizeSlider.Value = (ApplicationData.Current.LocalSettings.Values[SettingWidgetSize] as double?) ?? 65.0;
            RedSlider.Value = (ApplicationData.Current.LocalSettings.Values[SettingBgColorR] as double?) ?? 0;
            GreenSlider.Value = (ApplicationData.Current.LocalSettings.Values[SettingBgColorG] as double?) ?? 0;
            BlueSlider.Value = (ApplicationData.Current.LocalSettings.Values[SettingBgColorB] as double?) ?? 0;
            OpacitySlider.Value = (ApplicationData.Current.LocalSettings.Values[SettingBackgroundOpacity] as double?) ?? 70.0;
            BlurEffectToggle.IsOn = (ApplicationData.Current.LocalSettings.Values[SettingIsBlurEffectEnabled] as bool?) ?? true;

            UpdateBackground();
            UpdateWidgetSize(SizeSlider.Value);
        }

        private void SubscribeToSettingChanges()
        {
            ConnectionTypeComboBox.SelectionChanged += ConnectionTypeComboBox_SelectionChanged;
            ApiIpTextBox.LostFocus += ApiIpTextBox_LostFocus;
            ApiPortTextBox.LostFocus += ApiPortTextBox_LostFocus;
            WebSocketIpTextBox.LostFocus += WebSocketIpTextBox_LostFocus;
            WebSocketPortTextBox.LostFocus += WebSocketPortTextBox_LostFocus;
            BlurEffectToggle.Toggled += BlurEffectToggle_Toggled;
            OpacitySlider.ValueChanged += OpacitySlider_ValueChanged;
            RedSlider.ValueChanged += ColorSliders_ValueChanged;
            GreenSlider.ValueChanged += ColorSliders_ValueChanged;
            BlueSlider.ValueChanged += ColorSliders_ValueChanged;
            SizeSlider.ValueChanged += SizeSlider_ValueChanged;
        }

        private void UnsubscribeFromSettingChanges()
        {
            // 在重置等操作时取消订阅，防止意外触发事件
            ConnectionTypeComboBox.SelectionChanged -= ConnectionTypeComboBox_SelectionChanged;
            ApiIpTextBox.LostFocus -= ApiIpTextBox_LostFocus;
            ApiPortTextBox.LostFocus -= ApiPortTextBox_LostFocus;
            WebSocketIpTextBox.LostFocus -= WebSocketIpTextBox_LostFocus;
            WebSocketPortTextBox.LostFocus -= WebSocketPortTextBox_LostFocus;
            BlurEffectToggle.Toggled -= BlurEffectToggle_Toggled;
            OpacitySlider.ValueChanged -= OpacitySlider_ValueChanged;
            RedSlider.ValueChanged -= ColorSliders_ValueChanged;
            GreenSlider.ValueChanged -= ColorSliders_ValueChanged;
            BlueSlider.ValueChanged -= ColorSliders_ValueChanged;
            SizeSlider.ValueChanged -= SizeSlider_ValueChanged;
        }

        #endregion

        #region UI Event Handlers

        private void ConnectionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[SettingConnectionType] = ConnectionTypeComboBox.SelectedIndex;
            UpdateSettingsPanelVisibility();
            StartConnection(); // 切换模式后，立即重新连接
        }

        private void UpdateSettingsPanelVisibility()
        {
            if (ConnectionTypeComboBox.SelectedIndex == 0) // WebSocket
            {
                WebSocketSettingsPanel.Visibility = Visibility.Visible;
                HttpSettingsPanel.Visibility = Visibility.Collapsed;
            }
            else // HTTP
            {
                WebSocketSettingsPanel.Visibility = Visibility.Collapsed;
                HttpSettingsPanel.Visibility = Visibility.Visible;
            }
        }

        private void WebSocketIpTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var newIp = WebSocketIpTextBox.Text.Trim();
            if (webSocketIp != newIp)
            {
                webSocketIp = newIp;
                ApplicationData.Current.LocalSettings.Values[SettingWebSocketIp] = newIp;
                StartConnection();
            }
        }

        private void WebSocketPortTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var newPort = WebSocketPortTextBox.Text.Trim();
            if (webSocketPort != newPort)
            {
                webSocketPort = newPort;
                ApplicationData.Current.LocalSettings.Values[SettingWebSocketPort] = newPort;
                StartConnection();
            }
        }

        private void ApiIpTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var newIp = ApiIpTextBox.Text.Trim();
            if (httpApiIp != newIp)
            {
                httpApiIp = newIp;
                ApplicationData.Current.LocalSettings.Values[SettingHttpApiIp] = newIp;
                StartConnection();
            }
        }

        private void ApiPortTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var newPort = ApiPortTextBox.Text.Trim();
            if (httpApiPort != newPort)
            {
                httpApiPort = newPort;
                ApplicationData.Current.LocalSettings.Values[SettingHttpApiPort] = newPort;
                StartConnection();
            }
        }

        private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromSettingChanges();
            ApplicationData.Current.LocalSettings.Values.Clear();
            LoadAllSettings();
            SubscribeToSettingChanges();
            StartConnection();
        }

        // --- 外观设置的事件处理器 ---

        /// <summary>
        /// 更新背景（亚克力效果或纯色）。
        /// 这是修复编译错误的核心所在。
        /// </summary>
        private void UpdateBackground()
        {
            var acrylicBrush = (AcrylicBrush)this.Resources["AcrylicBackgroundBrush"];
            var solidBrush = (SolidColorBrush)this.Resources["SolidBackgroundBrush"];
            var color = Color.FromArgb(255, (byte)RedSlider.Value, (byte)GreenSlider.Value, (byte)BlueSlider.Value);
            double opacity = OpacitySlider.Value / 100.0;

            acrylicBrush.TintColor = color;
            acrylicBrush.TintOpacity = opacity;
            solidBrush.Color = color;
            solidBrush.Opacity = opacity;

            // **【错误修复】** 使用 if/else 替代三元表达式
            if (BlurEffectToggle.IsOn)
            {
                ContentGrid.Background = acrylicBrush;
            }
            else
            {
                ContentGrid.Background = solidBrush;
            }
        }

        private void UpdateWidgetSize(double newValue)
        {
            if (ContentGrid != null)
            {
                double scale = newValue / 100.0;
                ContentGrid.Width = baseWidth * scale;
                ContentGrid.Height = baseHeight * scale;
            }
        }

        private void BlurEffectToggle_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateBackground();
            ApplicationData.Current.LocalSettings.Values[SettingIsBlurEffectEnabled] = BlurEffectToggle.IsOn;
        }

        private void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            UpdateBackground();
            ApplicationData.Current.LocalSettings.Values[SettingBackgroundOpacity] = e.NewValue;
        }

        private void ColorSliders_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            UpdateBackground();
            ApplicationData.Current.LocalSettings.Values[SettingBgColorR] = RedSlider.Value;
            ApplicationData.Current.LocalSettings.Values[SettingBgColorG] = GreenSlider.Value;
            ApplicationData.Current.LocalSettings.Values[SettingBgColorB] = BlueSlider.Value;
        }

        private void SizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            UpdateWidgetSize(e.NewValue);
            ApplicationData.Current.LocalSettings.Values[SettingWidgetSize] = e.NewValue;
        }

        private void Grid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        #endregion

        /// <summary>
        /// 用于反序列化服务器JSON数据的数据模型类。
        /// </summary>
        public class HeartRateData
        {
            public int heart_rate { get; set; }
            public bool connected { get; set; }
            public string status { get; set; } // 新增字段，可用于未来显示更详细的状态
            public long timestamp { get; set; } // 新增字段
        }
    }
}