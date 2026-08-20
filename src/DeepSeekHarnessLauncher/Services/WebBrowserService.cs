using System.Diagnostics;

namespace DeepSeekHarnessLauncher.Services;

public interface IWebBrowserService
{
    void Open(string url);
}

/// <summary>用系统默认浏览器打开 URL。</summary>
public sealed class WebBrowserService : IWebBrowserService
{
    public void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 打开失败不影响主流程。
        }
    }
}
