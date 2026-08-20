using System.Windows;

namespace DeepSeekHarnessLauncher.Services;

public interface ILocalizationService
{
    /// <summary>当前语言："zh-CN" 或 "en-US"。</summary>
    string CurrentLanguage { get; }
    void Initialize(string language);
    void SetLanguage(string language);
    event EventHandler? LanguageChanged;
    string Get(string key);
}

/// <summary>应用语言切换：通过替换资源字典实现界面中英文切换。</summary>
public sealed class LocalizationService : ILocalizationService
{
    private const string ZhCn = "zh-CN";
    private const string EnUs = "en-US";

    public string CurrentLanguage { get; private set; } = ZhCn;

    public event EventHandler? LanguageChanged;

    public void SetLanguage(string language)
    {
        var target = language == EnUs ? EnUs : ZhCn;
        if (target == CurrentLanguage)
            return;

        CurrentLanguage = target;
        ApplyLanguage(target);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>启动时初始化语言（不触发切换事件）。</summary>
    public void Initialize(string language)
    {
        CurrentLanguage = language == EnUs ? EnUs : ZhCn;
        ApplyLanguage(CurrentLanguage);
    }

    public string Get(string key)
    {
        var app = Application.Current;
        if (app is not null && app.Resources[key] is string s)
            return s;
        return key;
    }

    private static void ApplyLanguage(string language)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var uri = new Uri(
            language == EnUs
                ? "pack://application:,,,/Localization/Strings.en-US.xaml"
                : "pack://application:,,,/Localization/Strings.zh-CN.xaml");

        var dict = new ResourceDictionary { Source = uri };

        var merged = app.Resources.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var src = merged[i].Source?.ToString() ?? string.Empty;
            if (src.Contains("Strings."))
                merged.RemoveAt(i);
        }
        merged.Add(dict);
    }
}
