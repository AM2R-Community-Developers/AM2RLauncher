using AM2RLauncher.Core;
using Eto.Forms;
using log4net;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace AM2RLauncher
{
    /// <summary>
    /// Class that checks for Updates and then Updates the Launcher.
    /// </summary>
    //TODO: Mac support for auto updater in general
    public static class LauncherUpdater
    {
        // How often this was broken count: 7
        // Auto updating is fun!

        /// <summary>The Version that identifies this current release.</summary>
        public const string VERSION = Core.Core.VERSION;

        /// <summary>The Path of the oldConfig. Only gets used Windows-only</summary>
        private static readonly string oldConfigPath = CrossPlatformOperations.CURRENTPATH + "/" + CrossPlatformOperations.LAUNCHERNAME + ".oldCfg";

        /// <summary>The actual Path where the executable is stored, only used for updating.</summary>
        private static readonly string updatePath = OS.IsWindows ? CrossPlatformOperations.CURRENTPATH
                                                                 : (OS.IsLinux ? Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) : Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory + "../../../"));

        /// <summary>
        /// Our log object, that handles logging the current execution to a file.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));

        /// <summary>
        /// Performs the entire AM2RLauncher update procedure. 
        /// </summary>
        public static void Main()
        {
            log.Info("Running update check...");

            // Update section

            // Clean old files that have been left
            if (File.Exists(CrossPlatformOperations.CURRENTPATH + "/AM2RLauncher.bak"))
            {
                log.Info("AM2RLauncher.bak detected. Removing file.");
                File.Delete(CrossPlatformOperations.CURRENTPATH + "/AM2RLauncher.bak");
            }
            if (OS.IsWindows && File.Exists(oldConfigPath))
            {
                log.Info(CrossPlatformOperations.LAUNCHERNAME + ".oldCfg detected. Removing file.");
                File.Delete(oldConfigPath);
            }
            if (OS.IsWindows && Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/oldLib"))
            {
                log.Info("Old lib folder detected, removing folder.");
                Directory.Delete(CrossPlatformOperations.CURRENTPATH + "/oldLib", true);
            }

            // Clean up old update libs
            if (OS.IsWindows && Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/lib"))
            {
                foreach (FileInfo file in new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/lib").GetFiles())
                {
                    if (file.Name.EndsWith(".bak"))
                        file.Delete();
                }

                // Do the same for each subdirectory
                foreach (DirectoryInfo dir in new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/lib").GetDirectories())
                {
                    foreach (FileInfo file in dir.GetFiles())
                    {
                        if (file.Name.EndsWith(".bak"))
                            file.Delete();
                    }
                }
            }

            // Check settings if autoUpdateLauncher is set to true
            bool autoUpdate = Boolean.Parse(CrossPlatformOperations.ReadFromConfig("AutoUpdateLauncher"));

            if (autoUpdate)
            {
                log.Info("AutoUpdate Launcher set to true!");

                // This is supposed to fix the updater throwing an exception on windows 7 and earlier(?)
                // See this for information: https://stackoverflow.com/q/2859790 and https://stackoverflow.com/a/50977774
                if (OS.IsWindows)
                {
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://github.com/AM2R-Community-Developers/AM2RLauncher/releases/latest");
                HttpWebResponse response;
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                }
                catch (WebException)
                {
                    log.Error("WebException caught! Displaying MessageBox.");
                    MessageBox.Show(Language.Text.NoInternetConnection);
                    return;
                }

                Uri realUri = response.ResponseUri;
                string onlineVersion = realUri.AbsoluteUri.Substring(realUri.AbsoluteUri.LastIndexOf('/') + 1);
                bool isCurrentVersionOutdated = false;

                string[] localVersionArray = VERSION.Split('.');
                string[] onlineVersionArray = onlineVersion.Split('.');

                for (int i = 0; i < localVersionArray.Length; i++)
                {
                    int onlineNum = Int32.Parse(onlineVersionArray[i]);
                    int localNum = Int32.Parse(localVersionArray[i]);
                    if (onlineNum > localNum)
                    { 
                        isCurrentVersionOutdated = true;
                        break;
                    }
                    if (localNum > onlineNum)
                        break;
                }

                log.Info((isCurrentVersionOutdated ? "Updating" : "Not Updating") + " from " + VERSION + " to " + onlineVersion);

                // No new update, exiting
                if (!isCurrentVersionOutdated)
                    return;

                // For mac, we just show a message box that a new version is available, because I don't want to support it yet.
                // hardcoded string, since also temporarily until it gets supported one day.
                if (OS.IsMac)
                {
                    MessageBox.Show("Your current version is outdated! The newest version is " + onlineVersion + "." +
                                    "Please recompile AM2RLauncher again or disable auto-updating");
                    return;
                }
                
                log.Info("Current version (" + VERSION + ") is outdated! Initiating update for version " + onlineVersion + ".");

                string tmpUpdatePath = CrossPlatformOperations.CURRENTPATH + "/tmpupdate/";
                string zipPath = CrossPlatformOperations.CURRENTPATH + "/launcher.zip";

                // Clean tmpupdate
                if (Directory.Exists(tmpUpdatePath))
                    Directory.Delete(tmpUpdatePath, true);
                if (!Directory.Exists(tmpUpdatePath))
                    Directory.CreateDirectory(tmpUpdatePath);

                try
                {
                    using var client = new WebClient();
                    string platformSuffix = "";
                    if (OS.IsWindows) platformSuffix = "_win";
                    else if (OS.IsLinux) platformSuffix = "_lin";

                    log.Info("Downloading https://github.com/AM2R-Community-Developers/AM2RLauncher/releases/latest/download/AM2RLauncher_" + onlineVersion + platformSuffix + ".zip to " + zipPath + ".");

                    client.DownloadFile("https://github.com/AM2R-Community-Developers/AM2RLauncher/releases/latest/download/AM2RLauncher_" + onlineVersion + platformSuffix + ".zip", zipPath);

                    log.Info("File successfully downloaded.");
                }
                catch (UnauthorizedAccessException)
                {
                    log.Error("UnauthorizedAccessException caught! Displaying MessageBox.");
                    MessageBox.Show(Language.Text.UnauthorizedAccessMessage);
                    return;
                }

                ZipFile.ExtractToDirectory(zipPath, tmpUpdatePath);
                log.Info("Updates successfully extracted to " + tmpUpdatePath);

                File.Delete(zipPath);
                File.Move(updatePath + "/" + CrossPlatformOperations.LAUNCHERNAME, CrossPlatformOperations.CURRENTPATH + "/AM2RLauncher.bak");
                if (OS.IsWindows) File.Move(CrossPlatformOperations.LAUNCHERNAME + ".config", CrossPlatformOperations.LAUNCHERNAME + ".oldCfg");

                foreach (var file in new DirectoryInfo(tmpUpdatePath).GetFiles())
                {
                    log.Info("Moving " + file.FullName + " to " + CrossPlatformOperations.CURRENTPATH + "/" + file.Name);
                    File.Copy(file.FullName, updatePath + "/" + file.Name, true);
                }
                // For windows, the actual application is in "AM2RLauncher.dll". Which means, we need to update the lib folder as well.
                if (OS.IsWindows && Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/lib"))
                {
                    // So, because Windows behavior is dumb...

                    // Rename all files in lib to *.bak
                    foreach (FileInfo file in new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/lib").GetFiles())
                    {
                        file.CopyTo(file.Directory + "/" +  file.Name + ".bak");
                    }

                    // Do the same for each sub directory
                    foreach (DirectoryInfo dir in new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/lib").GetDirectories())
                    {
                        foreach (FileInfo file in dir.GetFiles())
                        {
                            file.CopyTo(file.Directory + "/" + file.Name + ".bak");
                        }
                    }

                    // Yes, the above calls could be recursive. No, I can't be bothered to make them as such.
                    if (Directory.Exists(tmpUpdatePath + "lib"))
                        HelperMethods.DirectoryCopy(tmpUpdatePath + "lib", CrossPlatformOperations.CURRENTPATH + "/lib");
                }

                Directory.Delete(tmpUpdatePath, true);

                CrossPlatformOperations.CopyOldConfigToNewConfig();

                log.Info("Files extracted. Preparing to restart executable...");
                if (OS.IsLinux) System.Diagnostics.Process.Start("chmod", "+x " + updatePath + "./AM2RLauncher.Gtk");

                System.Diagnostics.Process.Start(updatePath + "/" + CrossPlatformOperations.LAUNCHERNAME);
                Environment.Exit(0);
            }
            else
            {
                log.Info("AutoUpdate Launcher set to false. Exiting update check.");
            }
        }
    }
}