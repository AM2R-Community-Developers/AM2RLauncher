using Eto.Forms;
using log4net;
using System;
using log4net.Config;
using System.IO;
using System.Reflection;

namespace AM2RLauncher.Gtk
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
            XmlConfigurator.Configure(new FileInfo(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) + "/log4net.config"));

            try
            {
                Application GTKLauncher = new Application(Eto.Platforms.Gtk);
                LauncherUpdater.Main();
                GTKLauncher.UnhandledException += GTKLauncher_UnhandledException;
                GTKLauncher.Run(new MainForm());
            }
            catch(Exception e)
            {
                log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.StackTrace.ToString());
                Console.WriteLine(Language.Text.UnhandledException + "\n" + e.Message + "\n*****Stack Trace*****\n\n" + e.StackTrace.ToString());
            }
        }

        /// <summary>
        /// This method gets fired when an unhandled excpetion occurs in <see cref="MainForm"/>.
        /// </summary>
        private static void GTKLauncher_UnhandledException(object sender, Eto.UnhandledExceptionEventArgs e)
        {
            log.Error("An unhandled exception has occurred: \n*****Stack Trace*****\n\n" + e.ExceptionObject.ToString());
            MessageBox.Show(Language.Text.UnhandledException + "\n*****Stack Trace*****\n\n" + e.ExceptionObject.ToString(), "GTK", MessageBoxType.Error);
        }
    }
}
