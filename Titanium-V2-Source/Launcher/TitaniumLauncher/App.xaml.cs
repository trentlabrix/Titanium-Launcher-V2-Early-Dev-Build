using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
namespace TitaniumLauncher;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length == 2 && e.Args[0] == "--check-runtime")
        {
            try { File.WriteAllText(e.Args[1], Services.GameLauncher.CheckRuntime()); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(e.Args[1], ex.Message); Shutdown(1); }
            return;
        }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        Services.GameLauncher.StopOwned();
        base.OnExit(e);
    }
}
