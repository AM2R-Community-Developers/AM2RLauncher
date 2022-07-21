using Eto.Forms;
using log4net;
using log4net.Config;
using System;
using System.IO;
using AM2RLauncherLib;
using log4net.Repository.Hierarchy;

// ReSharper disable LocalizableElement - we want hardcoded strings for console writes.

namespace AM2RLauncher.Mac;

/// <summary>
/// The main class for the Mac project.
/// </summary>
internal static class MainClass
{
    /// <summary>
    /// The logger for <see cref="MainForm"/>, used to write any caught exceptions.
    /// </summary>
    private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));

    /// <summary>
    /// The main method for the Mac project.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        string launcherDataPath = CrossPlatformOperations.CurrentPath;

        // Make sure first, ~/.local/share/AM2RLauncher exists
        if (!Directory.Exists(launcherDataPath))
            Directory.CreateDirectory(launcherDataPath);

        // Now, see if log4netConfig exists, if not write it again.
        if (!File.Exists($"{launcherDataPath}/log4net.config"))
            File.WriteAllText($"{launcherDataPath}/log4net.config", Properties.Resources.log4netContents.Replace("${DATADIR}", launcherDataPath));

        // Configure logger
        XmlConfigurator.Configure(new FileInfo(launcherDataPath + "/log4net.config"));

        // if we're on debug, always set logLevel to debug
        #if DEBUG
        ((Logger)log.Logger).Level = log4net.Core.Level.Debug;
        #endif

        try
        {
            Application macLauncher = new Application(Eto.Platforms.macOS);
            LauncherUpdater.Main();
            macLauncher.UnhandledException += MacLauncher_UnhandledException;
            macLauncher.Run(new MainForm());
        }
        catch (Exception e)
        {
            log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.StackTrace);
            Console.WriteLine($"{Language.Text.UnhandledException}\n{e.Message}\n*****Stack Trace*****\n\n{e.StackTrace}");
            Console.WriteLine($"Check the logs at {launcherDataPath} for more info!");
        }
    }

    /// <summary>
    /// This method gets fired when an unhandled exception occurs in <see cref="MainForm"/>.
    /// </summary>
    private static void MacLauncher_UnhandledException(object sender, Eto.UnhandledExceptionEventArgs e)
    {
        log.Error($"An unhandled exception has occurred: \n*****Stack Trace*****\n\n{e.ExceptionObject}");
        Application.Instance.Invoke(() =>
        {
            MessageBox.Show($"{Language.Text.UnhandledException}\n*****Stack Trace*****\n\n{e.ExceptionObject}", "Mac", MessageBoxType.Error);
        });
    }
}