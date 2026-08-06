using NTB.Toolbox.Services;

namespace NTB.Toolbox;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => AppErrorHandler.Handle(args.Exception, "Benutzeroberfläche");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                AppLog.Write($"Unbehandelter Hintergrundfehler: {exception}");
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Write($"Unbeobachteter Taskfehler: {args.Exception}");
            args.SetObserved();
        };
        Application.Run(new MainForm());
    }
}
