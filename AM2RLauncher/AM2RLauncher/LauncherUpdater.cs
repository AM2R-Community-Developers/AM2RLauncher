using AM2RLauncherLib;
using Eto.Forms;
using log4net;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;

namespace AM2RLauncher;

/// <summary>
/// Class that checks for Updates and then updates the Launcher.
/// </summary>
//TODO: Mac support for auto updater in general
public static class LauncherUpdater
{
    // How often this was broken count: 8
    // Auto updating is fun!

    /// <summary>The Version that identifies this current release.</summary>
    private const string VERSION = Core.Version;

    /// <summary>The Path of the oldConfig. Only gets used Windows-only</summary>
    private static readonly string oldConfigPath = updatePath + "/" + CrossPlatformOperations.LauncherName + ".oldCfg";

    /// <summary>The actual Path where the executable is stored, only used for updating.</summary>
    private static string updatePath
    {
        get
        {
            if (OS.IsWindows)
                return Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
            if (OS.IsLinux)
                return Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
            if (OS.IsMac)
                // TODO: double check on mac if this is correct
                return Path.GetDirectoryName($"{AppDomain.CurrentDomain.BaseDirectory}../../../");
            throw new NotSupportedException($"{OS.Name} does not have an update path!");
        }
    }

    /// <summary>
    /// Our log object, that handles logging the current execution to a file.
    /// </summary>
    private static readonly ILog log = LogManager.GetLogger(typeof(LauncherUpdater));

    /// <summary>
    /// Performs the entire AM2RLauncher update procedure.
    /// </summary>
    public static void Main()
    {
        #if NOAUTOUPDATE
        log.Info("On \"No auto update\" configuration, skipping auto update.");
        return;
        #endif
        
        log.Info("Running update check...");

        // Update section

        // Clean old files that have been left
        if (File.Exists(CrossPlatformOperations.CurrentPath + "/AM2RLauncher.bak"))
        {
            log.Info("AM2RLauncher.bak detected. Removing file.");
            File.Delete(CrossPlatformOperations.CurrentPath + "/AM2RLauncher.bak");
        }
        if (OS.IsWindows && File.Exists(oldConfigPath))
        {
            log.Info(CrossPlatformOperations.LauncherName + ".oldCfg detected. Removing file.");
            File.Delete(oldConfigPath);
        }
        if (OS.IsWindows && Directory.Exists(updatePath + "/oldLib"))
        {
            log.Info("Old lib folder detected, removing folder.");
            Directory.Delete(updatePath + "/oldLib", true);
        }

        // Clean up old update libs
        if (OS.IsWindows && Directory.Exists(updatePath + "/lib"))
        {
            foreach (FileInfo file in new DirectoryInfo(updatePath + "/lib").GetFiles())
            {
                if (!file.Name.EndsWith(".bak"))
                    continue;
                log.Info("Old bak file detected, deleting " + file.FullName);
                file.Delete();
            }

            // Do the same for each subdirectory
            foreach (DirectoryInfo dir in new DirectoryInfo(updatePath + "/lib").GetDirectories())
            {
                foreach (FileInfo file in dir.GetFiles())
                {
                    if (!file.Name.EndsWith(".bak"))
                        continue;
                    log.Info("Old bak file detected, deleting " + file.FullName);
                    file.Delete();
                }
            }
        }

        // Check settings if autoUpdateLauncher is set to true
        bool autoUpdate = Boolean.Parse(MainForm.ReadFromConfig("AutoUpdateLauncher"));

        if (!autoUpdate)
        {
            log.Info("AutoUpdate Launcher set to false. Exiting update check.");
            return;
        }
        
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
            log.Error("WebException caught during version request! Displaying MessageBox.");
            MessageBox.Show(Language.Text.NoInternetConnection);
            return;
        }

        // The URL from above redirects to the latest version, which we extract from the new url
        Uri realUri = response.ResponseUri;
        string onlineVersion = realUri.AbsoluteUri.Substring(realUri.AbsoluteUri.LastIndexOf('/') + 1);
        bool isCurrentVersionOutdated = false;

        string[] localVersionArray = VERSION.Split('.');
        string[] onlineVersionArray = onlineVersion.Split('.');

        // compare the remote version to our local version
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
        // hardcoded string, since also temporarily until it gets supported one day :tm:.
        if (OS.IsMac)
        {
            MessageBox.Show("Your current version is outdated! The newest version is " + onlineVersion + ". " +
                            "Please recompile AM2RLauncher again or disable auto-updating");
            return;
        }

        log.Info("Current version (" + VERSION + ") is outdated! Initiating update for version " + onlineVersion + ".");

        string tmpUpdatePath = Path.GetTempPath() + "/AM2RLauncherTmpUpdate/";
        string zipPath = tmpUpdatePath + "/launcher.zip";

        // Clean tmpupdate & zippath
        if (Directory.Exists(tmpUpdatePath))
            Directory.Delete(tmpUpdatePath, true);
        Directory.CreateDirectory(tmpUpdatePath);
        
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        // Download the new remote version
        try
        {
            using WebClient client = new WebClient();
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

        // Delete the zip, as we won't need it anymore
        File.Delete(zipPath);
        
        // Windows won't let us replace files directly, but it will let us rename files. So we start renaming every file with a .bak suffix
        File.Move(updatePath + "/" + CrossPlatformOperations.LauncherName, updatePath + "/AM2RLauncher.bak");
        if (OS.IsWindows) File.Move(updatePath + "/" + CrossPlatformOperations.LauncherName + ".config", updatePath + "/" + CrossPlatformOperations.LauncherName + ".oldCfg");

        // Move everything from root tmpupdate to root updatePath
        foreach (FileInfo file in new DirectoryInfo(tmpUpdatePath).GetFiles())
        {
            log.Info("Moving " + file.FullName + " to " + CrossPlatformOperations.CurrentPath + "/" + file.Name);
            if (File.Exists(updatePath + "/" + file.Name))
                File.Delete(updatePath + "/" + file.Name);
            File.Move(file.FullName, updatePath + "/" + file.Name);
        }
        // For windows, the actual application is in "AM2RLauncher.dll". Which means, we need to update the lib folder as well.
        if (OS.IsWindows && Directory.Exists(updatePath + "/lib"))
        {
            // So, because Windows behavior is dumb...

            // Rename all files in lib to *.bak
            foreach (FileInfo file in new DirectoryInfo(updatePath + "/lib").GetFiles())
            {
                log.Info("Moving " + file.FullName + " to " + file.Directory + "/" + file.Name + ".bak");
                file.MoveTo(file.Directory + "/" + file.Name + ".bak");
            }

            // Do the same for each sub directory
            foreach (DirectoryInfo dir in new DirectoryInfo(updatePath + "/lib").GetDirectories())
            {
                foreach (FileInfo file in dir.GetFiles())
                {
                    log.Info("Moving " + file.FullName + " to " + file.Directory + "/" + file.Name + ".bak");
                    file.MoveTo(file.Directory + "/" + file.Name + ".bak");
                }
            }

            // Yes, the above calls could be recursive. No, I can't be bothered to make them as such.
            // Finally, we put the new lib folder into tmpupdate path
            if (Directory.Exists(tmpUpdatePath + "lib"))
            {
                log.Info("Moving lib directory from '" + tmpUpdatePath + "' to current path");
                HelperMethods.DirectoryCopy(tmpUpdatePath + "/lib", updatePath + "/lib");
            }
        }
        
        // We did everything with the new update, it can now be deleted
        Directory.Delete(tmpUpdatePath, true);
        log.Info("Deleted temporary update path: '" + tmpUpdatePath + "'");

        // Transfer config files
        MainForm.CopyOldConfigToNewConfig();

        log.Info("Files extracted. Preparing to restart executable...");
        if (OS.IsLinux) Process.Start("chmod", "+x " + updatePath + "/" + CrossPlatformOperations.LauncherName);

        // And finally we restart, and boot into the new file
        Process.Start(updatePath + "/" + CrossPlatformOperations.LauncherName);
        Environment.Exit(0);
    }
}