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
        Config config = new();
        using LoggingService loggingService = new(config);

        try
        {
            using Galdr.Galdr galdr = new GaldrBuilder()
                .SetTitle("Scum Bag - Save Manager")
                .SetSize(1100, 775)
                .SetMinSize(800, 600)
                .AddSingleton(config)
                .AddSingleton(loggingService)
                .AddSingleton<SaveGameCommands>()
                .AddSingleton<SettingsCommands>()
                .AddSingleton<BackupService>()
                .AddSingleton<GameService>()
                .AddSingleton<SaveService>()
                .AddSingleton<ScreenshotService>()
                .AddSingleton<SettingsService>()
                .AddSingleton<FileService>()
#if DEBUG
                .SetDebug(true)
                .SetPort(1314)
#endif
                .Build()
                .Run();
        }
        catch (Exception e)
        {
            loggingService.LogError($"{nameof(Program)}>{nameof(Main)} - {e}");
        }
    }
}
