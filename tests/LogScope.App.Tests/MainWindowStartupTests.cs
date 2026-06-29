using System.Windows;
using FluentAssertions;

namespace LogScope.App.Tests;

/// <summary>
/// Smoke test: MainWindow must initialize and lay out without throwing.
/// This catches XAML binding errors such as TwoWay/OneWayToSource bindings on
/// read-only ViewModel properties (the v0.4.0 startup crash class).
/// </summary>
public class MainWindowStartupTests
{
    [Fact]
    public void MainWindow_LayoutPass_DoesNotThrow()
    {
        Exception? caught = null;

        var thread = new Thread(() =>
        {
            try
            {
                // Application.Current must exist for some WPF initialisation paths.
                if (Application.Current == null)
                    new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

                var vm = new LogScope.App.ViewModels.MainViewModel();
                var window = new MainWindow { DataContext = vm };

                // Measure+Arrange triggers the binding engine — this is where
                // invalid binding modes (TwoWay on a read-only property) would throw.
                window.Measure(new Size(1280, 800));
                window.Arrange(new Rect(0, 0, 1280, 800));
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(15));

        caught.Should().BeNull(
            "MainWindow must not throw during initialization — check for TwoWay bindings on read-only properties");
    }
}
