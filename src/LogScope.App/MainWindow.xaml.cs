using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LogScope.App.ViewModels;

namespace LogScope.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        ApplyWindowSettings();
        Closing += OnClosing;
    }

    private void ApplyWindowSettings()
    {
        var s = _vm.Settings;
        if (s.WindowWidth > 200) Width = s.WindowWidth;
        if (s.WindowHeight > 200) Height = s.WindowHeight;
        if (s.WorkspacePanelWidth > 26) WorkspaceColumn.Width = new GridLength(s.WorkspacePanelWidth);

        if (s.WindowLeft.HasValue && s.WindowTop.HasValue)
        {
            // Clamp to virtual screen so the window isn't off-screen after a monitor layout change.
            var screen = System.Windows.SystemParameters.VirtualScreenLeft;
            var screenRight = screen + System.Windows.SystemParameters.VirtualScreenWidth;
            var screenTop = System.Windows.SystemParameters.VirtualScreenTop;
            var screenBottom = screenTop + System.Windows.SystemParameters.VirtualScreenHeight;

            double left = Math.Max(screen, Math.Min(s.WindowLeft.Value, screenRight - 200));
            double top = Math.Max(screenTop, Math.Min(s.WindowTop.Value, screenBottom - 100));
            Left = left;
            Top = top;
        }

        WindowState = s.WindowMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var s = _vm.Settings;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            s.WindowLeft = Left;
            s.WindowTop = Top;
        }
        if (WorkspaceColumn.Width.IsAbsolute && WorkspaceColumn.Width.Value > 26)
            s.WorkspacePanelWidth = WorkspaceColumn.Width.Value;
        _vm.SaveSettings();
        _vm.Dispose();
    }

    /// <summary>Open a file or directory path (from the command line / Explorer).</summary>
    public void OpenPath(string path)
    {
        if (Directory.Exists(path))
            _vm.LoadWorkspace(path);
        else if (File.Exists(path))
        {
            _vm.OpenSingleFile(path);
        }
    }

    private void OnNodeClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 &&
            sender is FrameworkElement { DataContext: WorkspaceNodeViewModel { IsFile: true } node })
        {
            _vm.OpenLog(node.FilePath!);
            e.Handled = true;
        }
    }

    private void OnCloseTab(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LogTabViewModel tab })
            _vm.CloseTab(tab);
    }

    private bool _workspaceCollapsed;
    private GridLength _lastWorkspaceWidth = new(260);

    /// <summary>Collapse/expand the workspace panel accordion-style.</summary>
    private void OnToggleWorkspace(object sender, RoutedEventArgs e)
    {
        _workspaceCollapsed = !_workspaceCollapsed;
        if (_workspaceCollapsed)
        {
            _lastWorkspaceWidth = WorkspaceColumn.Width;
            WorkspaceColumn.Width = new GridLength(26);
            WorkspaceHeaderDetails.Visibility = Visibility.Collapsed;
            Tree.Visibility = Visibility.Collapsed;
            WorkspaceSplitter.Visibility = Visibility.Collapsed;
            WorkspaceToggle.Content = "▶";
        }
        else
        {
            WorkspaceColumn.Width = _lastWorkspaceWidth.Value > 26 ? _lastWorkspaceWidth : new GridLength(260);
            WorkspaceHeaderDetails.Visibility = Visibility.Visible;
            Tree.Visibility = Visibility.Visible;
            WorkspaceSplitter.Visibility = Visibility.Visible;
            WorkspaceToggle.Content = "◀";
        }
    }
}
