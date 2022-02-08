using Eto.Forms;
using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using log4net.Repository.Hierarchy;
using Application = Eto.Forms.Application;
using FileInfo = System.IO.FileInfo;

namespace AM2RLauncher.Gtk;

/// <summary>
/// The main class for the GTK project.
/// </summary>
internal static class MainClass
{
    /// <summary>
    /// The logger for <see cref="MainForm"/>, used to write any caught exceptions.
    /// </summary>
    private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));
    /// <summary>
    /// The main method for the GTK project.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        string launcherDataPath = GenerateCurrentPath();

        // Make sure first, ~/.local/share/AM2RLauncher exists
        if (!Directory.Exists(launcherDataPath))
            Directory.CreateDirectory(launcherDataPath);

        // Now, see if log4netConfig exists, if not write it again.
        if (!File.Exists(launcherDataPath + "/log4net.config"))
            File.WriteAllText(launcherDataPath + "/log4net.config", Properties.Resources.log4netContents.Replace("${DATADIR}", launcherDataPath));

        // Configure logger
        XmlConfigurator.Configure(new FileInfo(launcherDataPath + "/log4net.config"));

        // if we're on debug, always set loglevel to debug
        #if DEBUG
        ((Logger)log.Logger).Level = log4net.Core.Level.Debug;
        #endif

        // Log distro and version (if it exists)
        if (File.Exists("/etc/os-release"))
        {
            string osRelease = File.ReadAllText("/etc/os-release");
            Regex lineRegex = new Regex(".*=.*");
            var results = lineRegex.Matches(osRelease).Cast<Match>().ToList();
            var version = results.FirstOrDefault(x => x.Value.Contains("VERSION"));
            log.Info("Current Distro: " + results.FirstOrDefault(x => x.Value.Contains("NAME"))?.Value.Substring(5).Replace("\"", "") +
                     (version == null ? "" : " " + version.Value.Substring(8).Replace("\"", "")));
        }
        else
            log.Error("Couldn't determine the currently running distro!");


        try
        {
            Application gtkLauncher = new Application(Eto.Platforms.Gtk);
            LauncherUpdater.Main();
            gtkLauncher.UnhandledException += GTKLauncher_UnhandledException;
            gtkLauncher.Run(new MainForm());
        }
        catch (Exception e)
        {
            log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.StackTrace);
            Console.WriteLine(Language.Text.UnhandledException + "\n" + e.Message + "\n*****Stack Trace*****\n\n" + e.StackTrace);
            Console.WriteLine("Check the logs at " + launcherDataPath + " for more info!");
        }
    }

    /// <summary>
    /// This method gets fired when an unhandled excpetion occurs in <see cref="MainForm"/>.
    /// </summary>
    private static void GTKLauncher_UnhandledException(object sender, Eto.UnhandledExceptionEventArgs e)
    {
        log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.ExceptionObject);
        Application.Instance.Invoke(() =>
        {
            MessageBox.Show(Language.Text.UnhandledException + "\n*****Stack Trace*****\n\n" + e.ExceptionObject, "GTK", MessageBoxType.Error);
        });
    }

    // This is a duplicate of CrossPlatformOperations.GenerateCurrentPath, because trying to invoke that would cause a crash due to currentPlatform not being initialized.
    private static string GenerateCurrentPath()
    {
        string nixHome = Environment.GetEnvironmentVariable("HOME");
        // First, we check if the user has a custom AM2RLAUNCHERDATA env var
        string am2rLauncherDataEnvVar = Environment.GetEnvironmentVariable("AM2RLAUNCHERDATA");
        if (!String.IsNullOrWhiteSpace(am2rLauncherDataEnvVar))
        {
            try
            {
                // This will create the directories recursively if they don't exist
                Directory.CreateDirectory(am2rLauncherDataEnvVar);

                // Our env var is now set and directories exist
                return am2rLauncherDataEnvVar;
            }
            catch { }
        }

        // First check if XDG_DATA_HOME is set, if not we'll use ~/.local/share
        string xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (String.IsNullOrWhiteSpace(xdgDataHome))
            xdgDataHome = nixHome + "/.local/share";

        // Add AM2RLauncher to the end of the dataPath
        xdgDataHome += "/AM2RLauncher";

        try
        {
            // This will create the directories recursively if they don't exist
            Directory.CreateDirectory(xdgDataHome);

            // Our env var is now set and directories exist
            return xdgDataHome;
        }
        catch { }

        return Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
    }
}