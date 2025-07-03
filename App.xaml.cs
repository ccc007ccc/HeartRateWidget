using Microsoft.Gaming.XboxGameBar;
using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace HeartRateWidget
{
    sealed partial class App : Application
    {
        private XboxGameBarWidget widget = null;

        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        // 错误修正 #1: 方法名从 Onactivated 改为 OnActivated
        // 错误修正 #2: 重写了整个方法的逻辑，使其符合Game Bar小组件的正确激活流程
        protected override void OnActivated(IActivatedEventArgs args)
        {
            base.OnActivated(args);

            XboxGameBarWidgetActivatedEventArgs widgetArgs = null;
            if (args.Kind == ActivationKind.Protocol)
            {
                var protocolArgs = args as IProtocolActivatedEventArgs;
                if (protocolArgs?.Uri.Scheme == "ms-gamebarwidget")
                {
                    widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                }
            }

            if (widgetArgs != null)
            {
                // 如果小组件是第一次被激活
                if (widget == null)
                {
                    // 创建一个 Frame 作为根导航容器
                    var rootFrame = new Frame();
                    // 导航到我们的小组件页面
                    rootFrame.Navigate(typeof(Widget), null);
                    // 将这个Frame设置为当前窗口的内容
                    Window.Current.Content = rootFrame;

                    // 创建XboxGameBarWidget实例来管理小组件
                    widget = new XboxGameBarWidget(
                        widgetArgs,
                        Window.Current.CoreWindow,
                        rootFrame); // 将Frame传递给小组件实例

                    // 激活窗口
                    Window.Current.Activate();
                }
            }
        }


        // 错误修正 #3: 移除了对 MainPage 的引用，避免找不到类型的错误
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // 对于小组件应用，正常启动时可以什么都不做，或者导航到小组件页面
                    // 这里我们选择导航到Widget页面，这样直接启动应用也能看到界面
                    rootFrame.Navigate(typeof(Widget), e.Arguments);
                }
                Window.Current.Activate();
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            widget = null; // 在挂起时清理小组件实例
            deferral.Complete();
        }
    }
}