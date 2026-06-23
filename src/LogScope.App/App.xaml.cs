using System.IO;
using System.Windows;
using LogScope.App.ViewModels;

namespace LogScope.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        window.Show();

        // Optional: open a file or folder passed on the command line
        // (e.g. via Explorer "Open with", or `LogScope.exe path\to\file.log`).
        if (e.Args.Length > 0)
            window.OpenPath(e.Args[0]);
    }
}
