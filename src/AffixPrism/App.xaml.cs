using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Threading;

namespace AffixPrism;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool smoke = Array.IndexOf(e.Args, "--smoke-test") >= 0;
        WindowPlacement.Disabled = smoke;
        if (!smoke)
        {
            instance = new Mutex(true, @"Local\AffixPrism-Overlay", out bool created);
            if (!created)
            {
                MessageBox.Show("AffixPrism is already running. Use its notification-tray icon to reopen it, or exit the old instance before updating.", "AffixPrism");
                instance.Dispose(); instance = null; Shutdown(); return;
            }
        }
        var window = new MainWindow(smoke);
        MainWindow = window;
        window.Show();
        if (smoke)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try { window.RunSmokeTest(); File.WriteAllText("artifacts/smoke-result.txt", "PASS: Item check first/default, separate Expedition show/hide, configurable shortcut, title drag, clipboard fixture, watchlist, market changes, local filter presets/validation/draft restoration, UI price matching and stale-result invalidation, click-through and renders"); }
                catch (Exception ex) { Directory.CreateDirectory("artifacts"); File.WriteAllText("artifacts/smoke-result.txt", ex.ToString()); Environment.ExitCode = 1; }
                finally { window.ExitApplication(); }
            }));
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.ReleaseMutex(); instance?.Dispose();
        base.OnExit(e);
    }
}
