using Eto.Forms;
using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Reflection;

namespace AM2RLauncher.Wpf
{
    /// <summary>
    /// The main class for the WinForms project.
    /// </summary>
    class MainClass
    {
        /// <summary>
        /// The logger for <see cref="MainForm"/>, used to write any caught exceptions.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));
        /// <summary>
        /// The main method for the WinForms project.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
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

            // Try catch in case it ever crashes before actually getting to the Eto application
            try
            {
                Application WinLauncher = new Application(Eto.Platforms.WinForms);
                LauncherUpdater.Main();
                WinLauncher.UnhandledException += WinLauncher_UnhandledException;
                WinLauncher.Run(new MainForm());
            }
            catch (Exception e)
            {
                log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.StackTrace.ToString());
                System.Windows.Forms.MessageBox.Show(Language.Text.UnhandledException + "\n" + e.Message + "\n*****Stack Trace*****\n\n" + e.StackTrace.ToString(), "Microsoft .NET Framework",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// This method gets fired when an unhandled excpetion occurs in <see cref="MainForm"/>.
        /// </summary>
        private static void WinLauncher_UnhandledException(object sender, Eto.UnhandledExceptionEventArgs e)
        {
            log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.ExceptionObject.ToString());
            MessageBox.Show(Language.Text.UnhandledException + "\n*****Stack Trace*****\n\n" + e.ExceptionObject.ToString(), "Microsoft .NET Framework", MessageBoxType.Error);
        }

        // This is a duplicate of CrossPlatformOperations.GenerateCurrentPath, because trying to invoke that would cause a crash due to currentPlatform not being initialized.
        private static string GenerateCurrentPath()
        {
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
            // Windows has the path where the exe is located as default
            return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        }
    }
}
