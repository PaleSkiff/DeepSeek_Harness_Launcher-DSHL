namespace DeepSeekHarnessLauncher.Tests;

/// <summary>
/// UI 资源验证：纯黑主题（#000000/#121216）、无毛玻璃、导航指示条、关于页、弹窗圆角、logo。
/// </summary>
public sealed class UiResourceTests
{
    [Fact]
    public void MainViewModel_NavItems_NoOrdinalPrefixes()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "ViewModels", "MainViewModel.cs");

        Assert.DoesNotContain("①", content);
        Assert.DoesNotContain("②", content);
        Assert.DoesNotContain("③", content);
        Assert.DoesNotContain("④", content);
        Assert.DoesNotContain("⑤", content);
        // 导航文案已迁移到本地化资源字典（Strings.zh-CN.xaml / Strings.en-US.xaml）。
        Assert.Contains("Nav.Service", content);
        Assert.Contains("Nav.About", content);
    }

    [Fact]
    public void AppResources_ContainsPureBlackTheme()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "App.xaml");

        Assert.Contains("#000000", content);   // 全局纯黑底层
        Assert.Contains("#121216", content);   // 功能卡片深灰
        Assert.Contains("#165DFF", content);   // 主色深蓝
        Assert.Contains("CornerRadius", content); // 圆角
        Assert.Contains("DoubleAnimation", content); // hover 动画
        Assert.DoesNotContain("SurfaceTranslucentBrush", content); // 无半透明毛玻璃
        Assert.DoesNotContain("LinearGradientBrush", content);      // 无渐变毛玻璃
    }

    [Fact]
    public void LogView_ComboBox_NoInlineWhiteBackground()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "LogView.xaml");

        // 级别下拉框不应内联半透明白背景（避免原生白色样式）
        Assert.DoesNotContain("#14FFFFFF", content);
    }

    [Fact]
    public void AppResources_ComboBox_DarkDropdown()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "App.xaml");

        Assert.Contains("ComboBox", content);
        Assert.Contains("Popup", content);              // 下拉面板
        Assert.Contains("DropDownBorder", content);     // 深色下拉边框
    }

    [Fact]
    public void AppResources_HasDarkScrollbar()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "App.xaml");

        Assert.Contains("ScrollBar", content);
        Assert.Contains("PART_Track", content);
        Assert.Contains("Thumb", content);
    }

    [Fact]
    public void MainWindow_ContainsLogoAndNavIndicator()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml");

        Assert.Contains("Assets/logo.png", content);  // logo
        Assert.Contains("ListBox", content);          // 侧边导航
        Assert.Contains("Indicator", content);        // 选中项左侧蓝色指示条
        Assert.Contains("SelectedNavKey", content);   // 选中状态绑定
    }

    [Fact]
    public void MainWindow_NoTopWhiteBorderOrGap()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml");

        // 无外层 1px 浅灰边框（避免顶部白色分割线）
        Assert.DoesNotContain("CardBorderBrush\" BorderThickness=\"1\"", content);
        // 底部状态栏无顶部 1px 分割线
        Assert.DoesNotContain("BorderThickness=\"0,1,0,0\"", content);
    }

    [Fact]
    public void MainWindow_NoAcrylicOrBlur()
    {
        var code = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml.cs");

        Assert.DoesNotContain("EnableAcrylic", code);
        Assert.DoesNotContain("WindowBlurHelper", code);
        Assert.False(File.Exists(FindFilePath("src", "DeepSeekHarnessLauncher", "Helpers", "WindowBlurHelper.cs")));
    }

    [Fact]
    public void AboutView_ContainsContactInfo()
    {
        var vm = ReadProjectFile("src", "DeepSeekHarnessLauncher", "ViewModels", "AboutViewModel.cs");

        Assert.Contains("小舟 Superboy", vm);
        Assert.Contains("701206818", vm);
        Assert.Contains("1001747300", vm);
    }

    [Fact]
    public void Dialogs_UseRoundedCorners16()
    {
        var first = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "FirstCloseDialog.xaml");
        var port = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "PortOccupiedDialog.xaml");

        Assert.Contains("CornerRadius=\"16\"", first);
        Assert.Contains("CornerRadius=\"16\"", port);
    }

    [Fact]
    public void TrayService_UsesLogoIcon()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "TrayService.cs");

        Assert.Contains("LogoIconHelper.GetTrayIcon", content);
    }

    [Fact]
    public void MainWindow_UsesWindowIcon()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml.cs");

        Assert.Contains("LogoIconHelper.GetWindowIcon", content);
    }

    [Fact]
    public void Csproj_ReferencesNewLogo()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "DeepSeekHarnessLauncher.csproj");

        Assert.Contains("deepseeknew.png", content);   // 新 logo 资源引用
        Assert.Contains("Assets/logo.png", content);   // LogicalName 保持不变，代码引用不变
    }

    [Fact]
    public void MainWindow_TitleBarShowsLogoIcon()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml");

        Assert.Contains("Assets/logo.png", content);   // 标题栏左上角 logo 图标
    }

    [Fact]
    public void Csproj_HasApplicationIcon()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "DeepSeekHarnessLauncher.csproj");

        Assert.Contains("ApplicationIcon", content);
        Assert.Contains("app.ico", content);
    }

    [Fact]
    public void AppIco_FileExists()
    {
        var path = FindFilePath("src", "DeepSeekHarnessLauncher", "Resources", "app.ico");

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void AppIco_IsValidMultiSizeIcon()
    {
        var path = FindFilePath("src", "DeepSeekHarnessLauncher", "Resources", "app.ico");

        var info = new FileInfo(path);
        // 多尺寸 ICO（含 16/32/48/256 的 PNG 嵌入）应明显大于单尺寸空图标（126 字节）。
        Assert.True(info.Exists);
        Assert.True(info.Length > 1000, $"app.ico 过小（{info.Length} 字节），可能未内置有效图标。");
    }

    [Fact]
    public void AppResources_TitleBarAndSidebar_ArePureBlack()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "App.xaml");

        // 标题栏与侧边栏均为纯黑，与全局背景完全融合（消除顶部白色分割线）。
        Assert.Contains("TitleBarBrush\" Color=\"#000000\"", content);
        Assert.Contains("SidebarBrush\" Color=\"#000000\"", content);
    }

    [Fact]
    public void ComboBox_ContentPresenter_HasForeground()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "App.xaml");

        // 下拉框选中项文字与下拉项文字都显式绑定前景色，避免深色背景上无法显示。
        Assert.Contains("TextElement.Foreground=\"", content);
    }

    [Fact]
    public void ComboBox_ContentPresenter_BindsSelectionBoxItem()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "App.xaml");

        // 未展开时下拉框应显示选中项文字。
        Assert.Contains("SelectionBoxItem", content);
    }

    [Fact]
    public void MainWindow_UsesWindowChrome_ToRemoveSystemBorder()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml");

        // 使用 WindowChrome 移除系统 resize 边框（消除顶部/四周白线）。
        Assert.Contains("WindowChrome", content);
        Assert.Contains("GlassFrameThickness=\"0\"", content);
    }

    [Fact]
    public void TrayNotifyWindow_IsCustomFrostedCard_NotSystemToast()
    {
        var xaml = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "TrayNotifyWindow.xaml");
        var code = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "TrayNotifyWindow.xaml.cs");

        // 自定义无边框、透明底、置顶、不进入任务栏的提示卡片（非系统 Toast）。
        Assert.Contains("WindowStyle=\"None\"", xaml);
        Assert.Contains("AllowsTransparency=\"True\"", xaml);
        Assert.Contains("Background=\"Transparent\"", xaml);
        Assert.Contains("ShowInTaskbar=\"False\"", xaml);
        Assert.Contains("Topmost=\"True\"", xaml);

        // 圆角 + 柔光阴影（深色卡片）。
        Assert.Contains("CornerRadius=\"12\"", xaml);
        Assert.Contains("DropShadowEffect", xaml);

        // 不使用系统托盘气泡通知（NotifyIcon.ShowBalloonTip）。
        Assert.DoesNotContain("ShowBalloonTip", code);
    }

    [Fact]
    public void TrayNotifyWindow_SlidesInFadesIn_ThenOut()
    {
        var code = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "TrayNotifyWindow.xaml.cs");

        // 滑入 + 淡入 → 停留 2 秒 → 滑出 + 淡出。
        Assert.Contains("ShowAndDismissAsync", code);
        Assert.Contains("AnimateAsync", code);
        Assert.Contains("Task.Delay(2000)", code);
        Assert.Contains("Close()", code);

        // 动画同时驱动 Top（滑动）与 Opacity（淡入淡出）。
        Assert.Contains("Top =", code);
        Assert.Contains("Opacity =", code);
    }

    [Fact]
    public void MainWindow_MinimizeAndCloseToTray_ShowNotifyCard()
    {
        var code = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml.cs");

        // 最小化与“最小化到托盘”关闭都触发自定义提示卡片。
        Assert.Contains("HideToTray()", code);
        Assert.Contains("ShowTrayNotifyAsync", code);
        Assert.Contains("new TrayNotifyWindow()", code);
    }

    [Fact]
    public void MainWindow_TitleBarTooltips_AreLocalized()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml");

        // 标题栏按钮提示应走本地化资源，避免英文模式下仍显示中文。
        Assert.Contains("ToolTip=\"{DynamicResource TitleBar.MinimizeTip}\"", content);
        Assert.Contains("ToolTip=\"{DynamicResource TitleBar.CloseTip}\"", content);
        Assert.DoesNotContain("ToolTip=\"最小化到托盘\"", content);
        Assert.DoesNotContain("ToolTip=\"关闭\"", content);
    }

    [Fact]
    public void EnvironmentService_RefreshesProcessPath_AtAllDetectionAndInstallEntries()
    {
        // 回归（问题 1/2）：msiexec 装完 node.js 后进程 PATH 仍是旧快照，
        // 检测与安装入口（CheckAsync / InstallNodeAsync / InstallNodeOnlineAsync / PrefetchDshAsync）
        // 都必须先刷新进程 PATH，否则重新检测找不到 node、直接安装 DSH 找不到 npx/npm。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "EnvironmentService.cs");

        var count = System.Text.RegularExpressions.Regex.Matches(
            content, "ProcessPathHelper.RefreshPathFromRegistry").Count;

        Assert.True(count >= 4, $"检测与安装入口都应调用 PATH 刷新，实际只有 {count} 处");
    }

    [Fact]
    public void EnvironmentService_DshWebInstall_HasNoHardTimeout()
    {
        // DSH 首次安装（npx web）不得有等待上限：慢速/低配机器上首次安装可能耗时很久，
        // 有上限会在安装中途被强杀导致失败；成功与否只由"包可用性验证"决定。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "EnvironmentService.cs");

        Assert.DoesNotContain("DshWebInstallTimeout", content);
        Assert.DoesNotContain("deadline", content);
        Assert.Contains("VerifyDshAvailableAsync", content); // 成功判据：包可用性验证
    }

    [Fact]
    public void EnvironmentService_NodeInstall_ShowsElevationAndHeartbeatProgress()
    {
        // 问题 1：node.js 安装进度不得"消失"——提权前提示 UAC，静默安装期间心跳上报。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "EnvironmentService.cs");

        Assert.Contains("RunElevatedWithProgressAsync", content);
        Assert.Contains("Log.RequestingElevation", content);
        Assert.Contains("Log.SilentInstallWaitFmt", content);
    }

    [Fact]
    public void EnvironmentViewModel_ReactivateWindow_AfterInstall()
    {
        // 问题 1：UAC 提权会把主窗口挤到后台，安装结束后必须重新激活窗口让用户看到结果。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "ViewModels", "EnvironmentViewModel.cs");

        Assert.Contains("Application.Current?.MainWindow?.Activate()", content);
    }

    [Fact]
    public void EnvironmentView_InstallButtons_DisabledWhenInstalled()
    {
        // 问题 3：检测到已安装时，对应的安装按钮必须不可点击。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "EnvironmentView.xaml");

        Assert.Contains("IsEnabled=\"{Binding NodeReady, Converter={StaticResource InverseBool}}\"", content);
        Assert.Contains("IsEnabled=\"{Binding DshReady, Converter={StaticResource InverseBool}}\"", content);
    }

    [Fact]
    public void ProcessService_ErrorStrings_UseLocalization()
    {
        // 问题 5：进程服务错误文案（无法启动/超时）在英文模式下不能显示中文。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "ProcessService.cs");

        Assert.Contains("GetText(\"Err.ProcessStartFailed\")", content);
        Assert.Contains("GetText(\"Err.CommandStartFailed\")", content);
        Assert.Contains("GetText(\"Err.CommandTimeout\")", content);
        Assert.DoesNotContain("\"无法启动进程", content);
        Assert.DoesNotContain("\"无法启动命令", content);
        Assert.DoesNotContain("\"命令执行超时", content);
    }

    [Fact]
    public void TrayService_StateSeparator_IsLocalized()
    {
        // 问题 5：托盘状态行的分隔符不能硬编码中文全角冒号。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "TrayService.cs");

        Assert.Contains("GetText(\"Tray.StateSeparator\")", content);
        Assert.DoesNotContain("State\")}：", content);
    }

    [Fact]
    public void EnvironmentView_InstallLog_AutoScrollsToEnd()
    {
        // 功能：安装日志必须有自动滚轮效果——新日志到达时自动滚动到底部。
        var xaml = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "EnvironmentView.xaml");
        var code = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "EnvironmentView.xaml.cs");

        Assert.Contains("x:Name=\"InstallLogBox\"", xaml);
        Assert.Contains("TextChanged=\"InstallLogBox_TextChanged\"", xaml);
        Assert.Contains("ScrollToEnd", code);
    }

    [Fact]
    public void TrayService_RefreshesAllTexts_OnLanguageChanged()
    {
        // 问题 2：托盘菜单文字只在初始化时设置一次，运行时切换语言后必须刷新全部文案，
        // 否则英文模式下托盘仍残留中文。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "TrayService.cs");

        Assert.Contains("_localization.LanguageChanged += ", content); // 订阅语言切换
        Assert.Contains("public void RefreshTexts()", content);        // 刷新方法
        Assert.Contains("_startItem.Text = GetText(\"Tray.Start\")", content);
        Assert.Contains("_stopItem.Text = GetText(\"Tray.Stop\")", content);
        Assert.Contains("_restartItem.Text = GetText(\"Tray.Restart\")", content);
        Assert.Contains("_openItem.Text = GetText(\"Tray.Open\")", content);
        Assert.Contains("_exitItem.Text = GetText(\"Tray.Exit\")", content);
        Assert.Contains("_stateItem.Text = $\"{GetText(\"Tray.State\")}", content);
    }

    [Fact]
    public void FirstRunGuideDialog_ContainsStepsAndStart()
    {
        // 问题 2：首次启动引导对话框必须包含介绍、三步使用说明与开始按钮。
        var xaml = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "FirstRunGuideDialog.xaml");

        Assert.Contains("Guide.FirstRunTitle", xaml);
        Assert.Contains("Guide.StepsTitle", xaml);
        Assert.Contains("Guide.Step1", xaml);
        Assert.Contains("Guide.Step2", xaml);
        Assert.Contains("Guide.Step3", xaml);
        Assert.Contains("Guide.Start", xaml);
        Assert.Contains("Assets/logo.png", xaml);
        Assert.Contains("NodeStatusText", xaml);
        Assert.Contains("DshStatusText", xaml);
    }

    [Fact]
    public void MainWindow_FirstRunGuide_ShowsOnboardingDialog()
    {
        // 首次启动（config.json 不存在）时弹出完整引导，而非仅缺依赖时的提示。
        var code = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml.cs");

        Assert.Contains("FirstRunGuideDialog.Show(this, result)", code);
        Assert.Contains("ShowFirstRunGuideIfNeededAsync", code);
        Assert.DoesNotContain("BuildMissingMessage(result)", code);
    }

    [Fact]
    public void ProcessService_ElevatedRuns_HiddenWindow()
    {
        // 提权执行的命令（如 cmd.exe /c npm install）不得弹出黑色控制台窗口。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "ProcessService.cs");

        Assert.Contains("WindowStyle = ProcessWindowStyle.Hidden", content);
    }

    [Fact]
    public void EnvironmentService_CheckAsync_RunsInParallel()
    {
        // 问题 2：检测慢导致提示慢——node/npx/npm 三项必须并行执行（Task.WhenAll）。
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "EnvironmentService.cs");

        Assert.Contains("Task.WhenAll(nodeTask, dshTask, versionTask)", content);
    }

    [Fact]
    public void Localization_ContainsTrayNotifyMessage()
    {
        var zh = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Localization", "Strings.zh-CN.xaml");
        var en = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Localization", "Strings.en-US.xaml");

        // 中文提示文案（问题 3 指定）。
        Assert.Contains("程序已最小化至系统托盘，可点击托盘图标恢复窗口。", zh);
        // 英文提示文案。
        Assert.Contains("The app has been minimized to the system tray. Click the tray icon to restore the window.", en);
    }

    private static string FindFilePath(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var parts = new[] { dir.FullName }.Concat(relativeParts).ToArray();
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return string.Join("\\", relativeParts);
    }

    private static string ReadProjectFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var parts = new[] { dir.FullName }.Concat(relativeParts).ToArray();
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"未找到项目文件：{string.Join("\\", relativeParts)}");
    }

    [Fact]
    public void EnglishDictionary_CoversAllChineseKeys()
    {
        var zh = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Localization", "Strings.zh-CN.xaml");
        var en = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Localization", "Strings.en-US.xaml");

        var zhKeys = ExtractKeys(zh);
        var enKeys = ExtractKeys(en);

        var missing = zhKeys.Where(k => !enKeys.Contains(k)).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, $"英文字典缺少以下 key：{string.Join(", ", missing)}");
    }

    [Fact]
    public void AboutView_VersionRunBinding_IsOneWay()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "AboutView.xaml");

        // Run.Text 默认 TwoWay，只读属性 Version 必须显式 Mode=OneWay（避免运行时绑定错误）。
        Assert.Contains("{Binding Version, Mode=OneWay}", content);
    }

    [Fact]
    public void PrimaryButtonStyle_NoScaleOnHover()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "App.xaml");

        var primaryBlock = ExtractBlock(content, "x:Key=\"PrimaryButtonStyle\"", "x:Key=\"SecondaryButtonStyle\"");

        // 蓝色主按钮悬浮不应再有放大缩放动画。
        Assert.DoesNotContain("ScaleTransform", primaryBlock);
        Assert.DoesNotContain("DoubleAnimation", primaryBlock);
    }

    [Fact]
    public void EnvironmentView_DshInstallButton_IsOnLeft()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "EnvironmentView.xaml");

        var buttonIndex = content.IndexOf("Btn.InstallDsh", StringComparison.Ordinal);
        var statusIndex = content.IndexOf("DshStatusText", StringComparison.Ordinal);

        // “安装 DeepSeek Harness”按钮应位于状态文字之前（左侧）。
        Assert.True(buttonIndex >= 0, "未找到 DSH 安装按钮");
        Assert.True(buttonIndex < statusIndex, "DSH 安装按钮应位于状态指示左侧");
    }

    [Fact]
    public void MainWindow_HasRoundedCorners()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml");

        // 圆润圆角：WindowChrome 与内容 Border 均为 16。
        Assert.Contains("CornerRadius=\"16\"", content);
        Assert.Contains("ClipToBounds=\"True\"", content);
    }

    [Fact]
    public void MainWindow_CannotBeMaximized()
    {
        // 应用禁止最大化：双击标题栏不触发 DragMove（避免最大化），
        // 且 StateChanged 中任何最大化都会立即恢复为正常窗口。
        var code = ReadProjectFile("src", "DeepSeekHarnessLauncher", "MainWindow.xaml.cs");

        Assert.Contains("StateChanged += OnStateChanged", code);
        Assert.Contains("if (WindowState == WindowState.Maximized)", code);
        Assert.Contains("WindowState = WindowState.Normal", code);
        Assert.Contains("e.ClickCount != 1", code); // 双击标题栏不再触发拖拽/最大化
    }

    [Fact]
    public void DshDownloadFailedDialog_ContainsCommandAndOptions()
    {
        var code = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "DshDownloadFailedDialog.xaml.cs");

        Assert.Contains("npx --verbose @deepseek-ai/dsh web", code); // 手动安装命令
        Assert.Contains("OpenTerminal", code);                        // 打开终端
        Assert.Contains("CopyCommand", code);                         // 复制命令
    }

    [Fact]
    public void ServiceController_HealthMessages_UseLocalization()
    {
        var content = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Services", "ServiceController.cs");

        // 健康消息应通过 GetText 走本地化，而非硬编码中文。
        Assert.Contains("GetText", content);
        Assert.DoesNotContain("\"正在启动", content);
        Assert.DoesNotContain("\"已停止\"", content);
        Assert.DoesNotContain("\"健康检查通过\"", content);
    }

    [Fact]
    public void Dialogs_AreCenteredOnScreen()
    {
        var msg = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "MessageDialog.xaml");
        var dsh = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "DshDownloadFailedDialog.xaml");
        var first = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "FirstCloseDialog.xaml");
        var port = ReadProjectFile("src", "DeepSeekHarnessLauncher", "Views", "PortOccupiedDialog.xaml");

        Assert.Contains("CenterScreen", msg);
        Assert.Contains("CenterScreen", dsh);
        Assert.Contains("CenterScreen", first);
        Assert.Contains("CenterScreen", port);
    }

    private static HashSet<string> ExtractKeys(string content)
    {
        var keys = new HashSet<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(content, "x:Key=\"([^\"]+)\""))
            keys.Add(m.Groups[1].Value);
        return keys;
    }

    private static string ExtractBlock(string content, string startMarker, string endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        var end = content.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
            return string.Empty;
        return content[start..end];
    }
}
