using System;
using Galdr;
using Scum_Bag.Commands;
using Scum_Bag.Services;

namespace Scum_Bag;

internal class Program
{
    [STAThread]
    static void Main()
    {
        using Galdr.Galdr galdr = new GaldrBuilder()
            .SetTitle("Scum Bag")
            .SetSize(1000, 775)
            .SetMinSize(800, 600)
            .AddSingleton<Config>()
            .AddSingleton<SaveGameCommands>()
            .AddSingleton<BackupService>()
            .AddSingleton<GameService>()
            .AddSingleton<SaveService>()
            .AddSingleton<ScreenshotService>()
            .AddSingleton<LoggingService>()
#if DEBUG
            .SetDebug(true)
            .SetPort(1314)
#endif
            .Build()
            .Run();
    }
}
