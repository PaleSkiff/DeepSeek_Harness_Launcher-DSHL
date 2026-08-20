using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace DeepSeekHarnessLauncher.Helpers;

/// <summary>从嵌入式资源加载应用 logo 并转换为 WinForms 托盘图标。</summary>
public static class LogoIconHelper
{
    private static readonly Uri LogoUri = new("pack://application:,,,/Assets/logo.png");

    /// <summary>加载 logo PNG 并缩放为托盘图标（默认 32x32）。失败返回 null。</summary>
    public static Icon? GetTrayIcon(int size = 32)
    {
        try
        {
            var info = Application.GetResourceStream(LogoUri);
            if (info?.Stream is null)
                return null;

            using var stream = info.Stream;
            using var bitmap = new Bitmap(stream);
            using var resized = new Bitmap(bitmap, new System.Drawing.Size(size, size));

            var handle = resized.GetHicon();
            try
            {
                return (Icon)Icon.FromHandle(handle).Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>返回 WPF 窗口图标 ImageSource。</summary>
    public static System.Windows.Media.ImageSource? GetWindowIcon()
    {
        try
        {
            return new System.Windows.Media.Imaging.BitmapImage(LogoUri);
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
