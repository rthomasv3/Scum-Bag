using System;
using System.Diagnostics;
using Galdr;
using Scum_Bag.Commands;
using Scum_Bag.Services;

namespace Scum_Bag;

internal class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            using Galdr.Galdr galdr = new GaldrBuilder()
                .SetTitle("Scum Bag - Save Manager")
                .SetSize(1100, 775)
                .SetMinSize(800, 600)
                .AddSingleton<Config>()
                .AddSingleton<SaveGameCommands>()
                .AddSingleton<SettingsCommands>()
                .AddSingleton<BackupService>()
                .AddSingleton<GameService>()
                .AddSingleton<SaveService>()
                .AddSingleton<ScreenshotService>()
                .AddSingleton<LoggingService>()
                .AddSingleton<SettingsService>()
#if DEBUG
                .SetDebug(true)
                .SetPort(1314)
#endif
                .Build()
                .Run();
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.ToString());
        }
    }
}
