using System.Windows;
using System.Windows.Threading;

namespace LogScope.App.Tests;

/// <summary>
/// Hosts a single WPF <see cref="Application"/> on one dedicated STA thread with a running
/// Dispatcher, and marshals test work onto it. WPF has strict thread affinity — an
/// <see cref="Application"/> is a per-AppDomain singleton and its resources belong to the
/// thread that created it. Building windows on ad-hoc per-test STA threads races on that
/// singleton and throws cross-thread errors. Routing every UI test through this one thread
/// makes the suite deterministic.
/// </summary>
public sealed class WpfTestFixture : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher _dispatcher = null!;
    private readonly ManualResetEventSlim _ready = new(false);

    public WpfTestFixture()
    {
        _thread = new Thread(() =>
        {
            // Reuse an existing Application if a previous fixture/test already created one.
            _ = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            _dispatcher = Dispatcher.CurrentDispatcher;
            _ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WpfTestSTA",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(10));
    }

    /// <summary>Runs <paramref name="action"/> on the UI thread and rethrows any exception on the caller.</summary>
    public void Run(Action action)
    {
        Exception? caught = null;
        _dispatcher.Invoke(() =>
        {
            try { action(); }
            catch (Exception ex) { caught = ex; }
        });
        if (caught != null)
            throw new Xunit.Sdk.XunitException(caught.ToString());
    }

    public void Dispose() => _dispatcher.InvokeShutdown();
}

/// <summary>All WPF UI tests share one Application/STA thread and therefore run serialized.</summary>
[Xunit.CollectionDefinition(Name)]
public sealed class WpfCollection : Xunit.ICollectionFixture<WpfTestFixture>
{
    public const string Name = "WPF UI";
}
