using Eto.Forms;
using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Reflection;

namespace AM2RLauncher.Mac
{
    /// <summary>
    /// The main class for the Mac project.
    /// </summary>
    class MainClass
    {
        /// <summary>
        /// The logger for <see cref="MainForm"/>, used to write any caught exceptions.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));

        /// <summary>
        /// The main method for the Mac project.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
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

            try
            {
                Application MacLauncher = new Application(Eto.Platforms.Mac64);
                LauncherUpdater.Main();
                MacLauncher.UnhandledException += MacLauncher_UnhandledException;
                MacLauncher.Run(new MainForm());
            }
            catch (Exception e)
            {
                log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.StackTrace.ToString());
                Console.WriteLine(Language.Text.UnhandledException + "\n" + e.Message + "\n*****Stack Trace*****\n\n" + e.StackTrace.ToString());
                Console.WriteLine("Check the logs at " + launcherDataPath + " for more info!");
            }
            //new Application(Eto.Platforms.Mac64).Run(new MainForm());
        }

        /// <summary>
        /// This method gets fired when an unhandled excpetion occurs in <see cref="MainForm"/>.
        /// </summary>
        private static void MacLauncher_UnhandledException(object sender, Eto.UnhandledExceptionEventArgs e)
        {
            log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.ExceptionObject.ToString());
            Application.Instance.Invoke(new Action(() =>
            {
                MessageBox.Show(Language.Text.UnhandledException + "\n*****Stack Trace*****\n\n" + e.ExceptionObject.ToString(), "Mac", MessageBoxType.Error);
            }));
        }

        // This is a duplicate of CrossPlatformOperations.GenerateCurrentPath, because trying to invoke that would cause a crash due to currentPlatform not being initialized.
        private static string GenerateCurrentPath()
        {
            string NIXHOME = Environment.GetEnvironmentVariable("HOME");
            //Mac has the Path at HOME/Library/AM2RLauncher
            string macPath = NIXHOME + "/Library/AM2RLauncher";
            try
            {
                Directory.CreateDirectory(macPath);
                return macPath;
            }
            catch { }

            return Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
