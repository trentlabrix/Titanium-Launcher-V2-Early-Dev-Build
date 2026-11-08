using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TitaniumLauncher.Services;

public static class ModuleStartup
{
    public const string MenuModule = "TitaniumMenu.dll";
    public static IReadOnlyList<string> RequiredModules { get; } = Array.AsReadOnly(new[]
        { "memory.dll", "starfall.dll", "console.dll", "Titanium1040.dll", "TitaniumConsoleRecovery.dll", MenuModule });

    public static async Task InitializeAsync(bool host, bool enableMenu, Func<string, Task> load, Func<Task> waitForEngine, Func<Task> waitForLogin)
    {
        await load("starfall.dll");
        await waitForEngine();
        // Reboot only applies this legacy memory helper to versions below 10.
        // This launcher exclusively supports 10.40, where that helper has no match.
        await waitForLogin();
        await load(host ? "Titanium1040.dll" : "console.dll");
        if(!host)
        {
            await load("TitaniumConsoleRecovery.dll");
            // The menu is optional and client-only. It starts closed; the
            // player opens it with Insert after the game has loaded.
            if(enableMenu) await load(MenuModule);
        }
    }
}
