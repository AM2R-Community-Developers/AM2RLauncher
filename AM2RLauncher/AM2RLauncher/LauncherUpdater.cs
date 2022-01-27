using AM2RLauncher.Core;
using Eto;
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
    public static class LauncherUpdater
    {
        // How often this was broken count: 6
        // Auto updating is fun!

        /// <summary>The Version that identifies this current release.</summary>
        public const string VERSION = Core.Core.VERSION;

        /// <summary>The current Running platform.</summary>
        private static readonly Platform CurrentPlatform = Platform.Instance;   // Needs to be declared here as well, because I can't access the one from eto,
                                                                                // Since isn't loaded at this point

        /// <summary>The Path of the oldConfig. Only gets used Windows-only</summary>
        private static readonly string OldConfigPath = CrossPlatformOperations.CURRENTPATH + "/" + CrossPlatformOperations.LAUNCHERNAME + ".oldCfg";

        /// <summary>The actual Path where the executable is stored, only used for updating.</summary>
        //TODO: for mac, this reports the path of the mac runner, not the actual .app
        private static readonly string UpdatePath = CurrentPlatform.IsWinForms ? CrossPlatformOperations.CURRENTPATH : Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

        /// <summary>
        /// Our log object, that handles logging the current execution to a file.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainForm));

        /// <summary>
        /// Performs the entire AM2RLauncher update procedure. 
        /// </summary>
        public static void Main()
        {
            Log.Info("Running update check...");

            // Update section

            // Clean old files that have been left
            if (File.Exists(CrossPlatformOperations.CURRENTPATH + "/AM2RLauncher.bak"))
            {
                Log.Info("AM2RLauncher.bak detected. Removing file.");
                File.Delete(CrossPlatformOperations.CURRENTPATH + "/AM2RLauncher.bak");
            }
            if (CurrentPlatform.IsWinForms && File.Exists(OldConfigPath))
            {
                Log.Info(CrossPlatformOperations.LAUNCHERNAME + ".oldCfg detected. Removing file.");
                File.Delete(OldConfigPath);
            }
            if (CurrentPlatform.IsWinForms && Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/oldLib"))
            {
                Log.Info("Old lib folder detected, removing folder.");
                Directory.Delete(CrossPlatformOperations.CURRENTPATH + "/oldLib", true);
            }

            // Clean up old update libs
            if (CurrentPlatform.IsWinForms && Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/lib"))
            {
                foreach (FileInfo file in new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/lib").GetFiles())
                {
                    if (file.Name.EndsWith(".bak"))
                        file.Delete();
                }

                // Do the same for each subdir
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
            bool autoUpdate = bool.Parse(CrossPlatformOperations.ReadFromConfig("AutoUpdateLauncher"));

            if (autoUpdate)
            {
                Log.Info("AutoUpdate Launcher set to true!");

                // This is supposed to fix the updater throwing an exception on windows 7 and earlier(?)
                // See this for information: https://stackoverflow.com/q/2859790 and https://stackoverflow.com/a/50977774
                if (CurrentPlatform.IsWinForms)
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
                    Log.Error("WebException caught! Displaying MessageBox.");
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
                    int onlineNum = int.Parse(onlineVersionArray[i]);
                    int localNum = int.Parse(localVersionArray[i]);
                    if (onlineNum > localNum)
                    { 
                        isCurrentVersionOutdated = true;
                        break;
                    }
                    else if (localNum > onlineNum)
                        break;
                }

                Log.Info((isCurrentVersionOutdated ? "Updating" : "Not Updating") + " from " + VERSION + " to " + onlineVersion);

                // No new update, exiting
                if (!isCurrentVersionOutdated)
                    return;
                
                Log.Info("Current version (" + VERSION + ") is outdated! Initiating update for version " + onlineVersion + ".");

                string tmpUpdatePath = CrossPlatformOperations.CURRENTPATH + "/tmpupdate/";
                string zipPath = CrossPlatformOperations.CURRENTPATH + "/launcher.zip";

                // Clean tmpupdate
                if (Directory.Exists(tmpUpdatePath))
                    Directory.Delete(tmpUpdatePath, true);
                if (!Directory.Exists(tmpUpdatePath))
                    Directory.CreateDirectory(tmpUpdatePath);

                try
                {
                    using (var client = new WebClient())
                    {
                        string platformSuffix = "";
                        if (CurrentPlatform.IsWinForms) platformSuffix = "_win";
                        else if (CurrentPlatform.IsGtk) platformSuffix = "_lin";

                        Log.Info("Downloading https://github.com/AM2R-Community-Developers/AM2RLauncher/releases/latest/download/AM2RLauncher_" + onlineVersion + platformSuffix + ".zip to " + zipPath + ".");

                        client.DownloadFile("https://github.com/AM2R-Community-Developers/AM2RLauncher/releases/latest/download/AM2RLauncher_" + onlineVersion + platformSuffix + ".zip", zipPath);

                        Log.Info("File successfully downloaded.");
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Log.Error("UnauthorizedAccessException caught! Displaying MessageBox.");
                    MessageBox.Show(Language.Text.UnauthorizedAccessMessage);
                    return;
                }

                ZipFile.ExtractToDirectory(zipPath, tmpUpdatePath);
                Log.Info("Updates successfully extracted to " + tmpUpdatePath);

                File.Delete(zipPath);
                File.Move(UpdatePath + "/" + CrossPlatformOperations.LAUNCHERNAME, CrossPlatformOperations.CURRENTPATH + "/AM2RLauncher.bak");
                if (CurrentPlatform.IsWinForms) File.Move(CrossPlatformOperations.LAUNCHERNAME + ".config", CrossPlatformOperations.LAUNCHERNAME + ".oldCfg");

                foreach (var file in new DirectoryInfo(tmpUpdatePath).GetFiles())
                {
                    Log.Info("Moving " + file.FullName + " to " + CrossPlatformOperations.CURRENTPATH + "/" + file.Name);
                    File.Copy(file.FullName, UpdatePath + "/" + file.Name, true);
                }
                // For windows, the actual application is in "AM2RLauncher.dll". Which means, we need to update the lib folder as well.
                if (CurrentPlatform.IsWinForms && Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/lib"))
                {
                    // So, because Windows behavior is dumb...

                    // Rename all files in lib to *.bak
                    foreach (FileInfo file in new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/lib").GetFiles())
                    {
                        file.CopyTo(file.Directory + "/" +  file.Name + ".bak");
                    }

                    // Do the same for each subdir
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

                Log.Info("Files extracted. Preparing to restart executable...");

                if (CurrentPlatform.IsGtk) System.Diagnostics.Process.Start("chmod", "+x ./AM2RLauncher.Gtk");

                System.Diagnostics.Process.Start(UpdatePath + "/" + CrossPlatformOperations.LAUNCHERNAME);
                Environment.Exit(0);
            }
            else
            {
                Log.Info("AutoUpdate Launcher set to false. Exiting update check.");
            }
        }
    }
}
