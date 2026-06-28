using RobsWindSim.Models;

namespace RobsWindSim;

static class Program
{
    [STAThread]
    static void Main()
    {
        if (!SingleInstance.TryAcquire(out var mutex))
            return;

        ApplicationConfiguration.Initialize();

        using (mutex!)
        {
            var settings = AppSettings.Load();
            var context = new AppTrayContext(settings);
            using var activateListener = SingleInstance.ListenForActivate(context.ShowSettings);
            Application.Run(context);
        }
    }
}
