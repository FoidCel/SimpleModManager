using System.Threading;
using System.Windows;

namespace SimpleModManager;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "Isolumia-SimpleModManager-SingleInstance-Mutex", out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show("Simple Mod Manager is already running.", "Already Running", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}
