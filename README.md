# ❤️ HeartRateWidget - 实时心率游戏小组件

一个与 [HeartRateMonitor](https://github.com/ccc007ccc/HeartRateMonitor) 桌面主程序或 [HeartRateMonitorMobile](https://github.com/ccc007ccc/HeartRateMonitorMobile) 手机App配套使用的 Xbox Game Bar 小组件。

它专为解决在**独占全屏 (Exclusive Fullscreen)** 游戏中查看实时心率的问题而设计。当游戏以独占全屏模式运行时，常规的桌面悬浮窗将无法显示。本小组件利用 Xbox Game Bar 的原生叠加层，让您即使在最沉浸的游戏环境中，也能时刻关注自己的心率。

<img src="https://github.com/user-attachments/assets/19963a1b-763b-42ef-85df-8a5773f18265" width="450"/>

## 📋 使用前提

在使用本小组件前，请确保您已满足以下条件：

1.  拥有一台运行 Windows 10 或 11 的电脑，并已启用 Xbox Game Bar。
2.  **二选一**：
    * 已在电脑上下载并运行 [**HeartRateMonitor 桌面主程序**](https://github.com/ccc007ccc/HeartRateMonitor)。
    * 已在手机上安装并运行 [**HeartRateMonitorMobile App**](https://github.com/ccc007ccc/HeartRateMonitorMobile)。
3.  已在 `HeartRateMonitor` 主程序或手机 App 中连接上您的心率设备，并**启用了API服务**。

## 🚀 安装与设置

### 步骤一：安装小组件

1.  前往 [Releases 页面](https://github.com/ccc007ccc/HeartRateWidget/releases) 下载最新的 `HeartRateWidget.zip` 压缩包。
2.  将压缩包解压到一个独立的文件夹中。
3.  右键点击 `Install.ps1` 安装脚本，并选择 **“使用 PowerShell 运行”**。

    <img src="https://github.com/user-attachments/assets/f6e678ab-82f6-4a04-b2be-05fde29b5d52" width="450"/>

4.  根据 PowerShell 窗口的提示，**启用开发者模式** 并允许脚本执行，以完成安装。

    <img src="https://github.com/user-attachments/assets/55921401-8023-4e46-a773-7f72fdcdfa5b" width="450"/>

### 步骤二：允许小组件访问数据（仅需一次）

为了让 Game Bar 小组件能够从主程序获取心率数据，您需要进行一次性的网络环回设置。

1.  下载并运行 [EnableLoopback 工具](https://github.com/Kuingsmile/uwp-tool/releases/download/latest/enableLoopback.exe)。
2.  在程序列表中找到并勾选 `HeartRateWidget`。
3.  点击 “**Save Changes**” 保存设置。此后即可关闭该工具。

    <img src="https://github.com/user-attachments/assets/1af9ea49-b616-4789-b672-b584810ca24d" width="450"/>

## 🎮 如何使用

1.  确保 `HeartRateMonitor` 桌面程序或手机 App 正在运行，且 **API 服务** 已被启用。
2.  在游戏中或桌面上，按 `Win + G` 快捷键打开 Xbox Game Bar。
3.  在顶部的小组件菜单中，找到并点击 **HeartRateWidget**。
4.  小组件将出现在屏幕上，初次显示为 `❤️N/A`。它会自动连接并开始显示心率。
5.  点击小组件右上角的 **图钉图标**，将其固定在屏幕上。这样即使关闭 Game Bar，心率显示也会保留在游戏画面之上。

### ⚙️ 自定义数据源 (IP和端口)

默认情况下，小组件会尝试从 `http://127.0.0.1:8000` 获取数据，这是桌面主程序的默认地址。如果您需要连接到 **手机App** 或 **自定义了桌面程序的IP/端口**，请按以下步骤操作：

1.  在小组件上**单击右键**，打开设置菜单。
2.  在输入框中填入新的地址（例如，您手机的局域网IP地址和端口 `http://192.168.1.100:8000`）。
3.  点击“保存”，小组件将立即尝试从新地址获取数据。

## ❓ 常见问题

**Q: 小组件一直显示 `❤️N/A` 怎么办？**
A: 请按以下步骤排查：
1.  确认 `HeartRateMonitor` 桌面程序或手机 App 是否已启动并成功连接到您的心率设备。
2.  确认程序或App中的 **API 服务** 是否已启用。
3.  **（最常见原因）** 确认您是否已按照 **“步骤二：允许小组件访问数据”** 的说明正确配置了环回。
4.  **（连接手机时）** 确认电脑和手机是否连接在**同一个局域网**下，并且已在小组件的右键菜单中正确设置了手机的IP地址和端口。
5.  尝试重启 `HeartRateMonitor` 程序/App 和游戏。
