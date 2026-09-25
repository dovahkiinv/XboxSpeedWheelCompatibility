using System.Diagnostics;
using XboxWheelCompatibility.WheelTransformer;


namespace XboxWheelCompatibility.WheelCompatibilityService
{
    class Program
    {
        public static void Main(string[] Arguments)
        {
            // Written before anything else so a log exists even if startup fails.
            Console.WriteLine("Xbox Wheel Compatibility service starting...");
            Console.WriteLine("Output.log: " + DiagnosticsLog.OutputLogPath);
            Console.WriteLine("diagnostics.log: " + DiagnosticsLog.LogPath);
            DiagnosticsLog.Write("Service process started. Exe folder: " + AppContext.BaseDirectory
                + " | Admin: " + IsAdministrator() + " | Interactive console: " + Environment.UserInteractive);

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                DiagnosticsLog.Write("UNHANDLED EXCEPTION: " + e.ExceptionObject);

            try
            {
                Host.CreateDefaultBuilder(Arguments)
                    .UseWindowsService()
                    .ConfigureServices(Services =>
                        {
                            Services.AddHostedService<WheelCompatibilityWorker>();
                        }
                    )
                    .Build()
                    .Run();
                DiagnosticsLog.Write("Service host stopped normally.");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("FATAL: " + ex);
                Console.WriteLine("FATAL: " + ex);
                try { EventLog.WriteEntry(".NET Runtime", ex.ToString(), EventLogEntryType.Error, 1000); } catch { }
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }
}
