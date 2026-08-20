using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeepSeekHarnessLauncher.Models;
using DeepSeekHarnessLauncher.Services;

namespace DeepSeekHarnessLauncher.ViewModels;

/// <summary>② 日志页 ViewModel：实时收集 + 过滤 + 清空/导出/复制。</summary>
public partial class LogViewModel : ViewModelBase
{
    private readonly ILogService _logService;
    private readonly ObservableCollection<LogEntry> _entries = new();

    public string DisplayName => "日志";

    [ObservableProperty]
    private LogFilter _selectedFilter = LogFilter.All;

    [ObservableProperty]
    private bool _autoScroll = true;

    public ICollectionView EntriesView { get; }

    public LogViewModel(ILogService logService)
    {
        _logService = logService;
        EntriesView = CollectionViewSource.GetDefaultView(_entries);
        EntriesView.Filter = FilterEntry;

        foreach (var entry in logService.Entries)
            _entries.Add(entry);

        logService.EntryReceived += (_, e) => OnUi(() => _entries.Add(e));
    }

    partial void OnSelectedFilterChanged(LogFilter value) => EntriesView?.Refresh();

    private bool FilterEntry(object obj)
        => obj is LogEntry entry && MatchesFilter(entry, SelectedFilter);

    /// <summary>级别过滤：All 显示全部，否则精确匹配。纯函数，便于测试。</summary>
    public static bool MatchesFilter(LogEntry entry, LogFilter filter)
        => filter == LogFilter.All
           || entry.Level.ToString().Equals(filter.ToString(), StringComparison.OrdinalIgnoreCase);

    /// <summary>格式化一条日志为单行文本。纯函数，便于测试。</summary>
    public static string FormatEntry(LogEntry entry)
        => $"{entry.Timestamp:HH:mm:ss.fff}  {entry.Level.ToString().ToUpperInvariant(),-5}  {entry.Message}";

    [RelayCommand]
    private void ClearView()
    {
        _entries.Clear();
        _logService.ClearView();
    }

    [RelayCommand]
    private void Export()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = GetText("Dlg.LogFilter"),
            FileName = $"dsh-log-{DateTime.Now:yyyyMMdd-HHmmss}.log",
        };

        if (dialog.ShowDialog() == true)
            _logService.Export(dialog.FileName);
    }

    private static string GetText(string key)
        => System.Windows.Application.Current?.Resources[key] as string ?? key;

    [RelayCommand]
    private void CopyAll()
    {
        var text = string.Join(Environment.NewLine, _entries.Select(FormatEntry));
        Clipboard.SetText(text);
    }

    private static void OnUi(Action action)
    {
        var app = Application.Current;
        if (app is null || app.Dispatcher.CheckAccess())
        {
            action();
            return;
        }
        app.Dispatcher.BeginInvoke(action);
    }
}
