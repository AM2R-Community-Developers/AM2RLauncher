using Eto.Forms;
using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Reflection;

namespace AM2RLauncher.Wpf;

/// <summary>
/// The main class for the WinForms project.
/// </summary>
internal static class MainClass
{
    /// <summary>
    /// The logger for <see cref="MainForm"/>, used to write any caught exceptions.
    /// </summary>
    private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));
    /// <summary>
    /// The main method for the WinForms project.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        string launcherDataPath = GenerateCurrentPath();

        // Make sure first, that the path exists
        if (!Directory.Exists(launcherDataPath))
            Directory.CreateDirectory(launcherDataPath);

        // Now, see if log4netConfig exists, if not write it again.
        if (!File.Exists(launcherDataPath + "/log4net.config"))
            File.WriteAllText(launcherDataPath + "/log4net.config", Properties.Resources.log4netContents.Replace("${DATADIR}", launcherDataPath));

        // Configure logger
        XmlConfigurator.Configure(new FileInfo(launcherDataPath + "/log4net.config"));

        //Log Wine
        if (Core.Core.IsThisRunningFromWine)
            log.Info("Currently running from WINE!");

        // Try catch in case it ever crashes before actually getting to the Eto application
        try
        {
            Application winLauncher = new Application(Eto.Platforms.WinForms);
            LauncherUpdater.Main();
            winLauncher.UnhandledException += WinLauncher_UnhandledException;
            winLauncher.Run(new MainForm());
        }
        catch (Exception e)
        {
            log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.StackTrace);
            System.Windows.Forms.MessageBox.Show(Language.Text.UnhandledException + "\n" + e.Message + "\n*****Stack Trace*****\n\n" + e.StackTrace, "Microsoft .NET Framework",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// This method gets fired when an unhandled excpetion occurs in <see cref="MainForm"/>.
    /// </summary>
    private static void WinLauncher_UnhandledException(object sender, Eto.UnhandledExceptionEventArgs e)
    {
        log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.ExceptionObject);
        MessageBox.Show(Language.Text.UnhandledException + "\n*****Stack Trace*****\n\n" + e.ExceptionObject, "Microsoft .NET Framework", MessageBoxType.Error);
    }

    // This is a duplicate of CrossPlatformOperations.GenerateCurrentPath, because trying to invoke that would cause a crash due to currentPlatform not being initialized.
    private static string GenerateCurrentPath()
    {
        // First, we check if the user has a custom AM2RLAUNCHERDATA env var
        string am2rLauncherDataEnvVar = Environment.GetEnvironmentVariable("AM2RLAUNCHERDATA");
        // Windows has the path where the exe is located as default
        string defaultPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
        if (String.IsNullOrWhiteSpace(am2rLauncherDataEnvVar))
            return defaultPath;
        
        try
        {
            // This will create the directories recursively if they don't exist
            Directory.CreateDirectory(am2rLauncherDataEnvVar);

            // Our env var is now set and directories exist
            return am2rLauncherDataEnvVar;
        }
        catch
        {
            return defaultPath;
        }
    }
}