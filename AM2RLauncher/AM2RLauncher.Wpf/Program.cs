using Eto.Forms;
using System;
using log4net;
using log4net.Config;
using System.IO;
using System.Reflection;

namespace AM2RLauncher.Wpf
{
    /// <summary>
    /// The main class for the GTK project.
    /// </summary>
    class MainClass
    {
        /// <summary>
        /// The logger for <see cref="MainForm"/>, used to write any caught exceptions.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));
        /// <summary>
        /// The main method for the GTK project.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            // Configure logger
            XmlConfigurator.Configure(new FileInfo(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/log4net.config"));

            // Try catch in case it ever crashes before actually getting to the Eto application
            try
            {
                Application WinLauncher = new Application(Eto.Platforms.WinForms);
                LauncherUpdater.Main();
                WinLauncher.UnhandledException += WinLauncher_UnhandledException;
                WinLauncher.Run(new MainForm());
            }
            catch(Exception e)
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
            MessageBox.Show(Language.Text.UnhandledException + "\n*****Stack Trace*****\n\n" +e.ExceptionObject.ToString(), "Microsoft .NET Framework", MessageBoxType.Error);
        }
    }
}
