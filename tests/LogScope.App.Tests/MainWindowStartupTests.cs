using System.Windows;

namespace LogScope.App.Tests;

/// <summary>
/// Smoke test: MainWindow must initialize and lay out without throwing.
/// This catches XAML binding errors such as TwoWay/OneWayToSource bindings on
/// read-only ViewModel properties (the v0.4.0 startup crash class).
/// </summary>
[Xunit.Collection(WpfCollection.Name)]
public class MainWindowStartupTests
{
    private readonly WpfTestFixture _wpf;
    public MainWindowStartupTests(WpfTestFixture wpf) => _wpf = wpf;

    [Fact]
    public void MainWindow_LayoutPass_DoesNotThrow()
    {
        _wpf.Run(() =>
        {
            var vm = new LogScope.App.ViewModels.MainViewModel();
            var window = new MainWindow { DataContext = vm };

            // Measure+Arrange the window's Content (root DockPanel) — a Window's own template
            // isn't applied until shown, so measuring Content is what actually realizes the
            // element tree and triggers the binding engine. This is where invalid binding
            // modes (TwoWay on a read-only property) would throw.
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(1280, 800));
            root.Arrange(new Rect(0, 0, 1280, 800));
            root.UpdateLayout();
        });
    }
}
