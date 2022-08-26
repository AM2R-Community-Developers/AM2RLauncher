using log4net;
using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;

namespace AM2RLauncherLib;

/// <summary>
/// Class that has various Helper functions. Basically anything that could be used outside the Launcher resides here.
/// </summary>
public static class HelperMethods
{
    // Load reference to logger
    /// <summary>
    /// Our log object, that handles logging the current execution to a file.
    /// </summary>
    private static readonly ILog log = Core.Log;

    // Thank you, Microsoft docs: https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
    // Slightly modified by adding overwriteFiles bool, as we need to replace readme, music, etc.
    /// <summary>
    /// This copies the contents of a specified Directory recursively to another Directory.
    /// </summary>
    /// <param name="sourceDirName">Full Path to the Directory that will be recursively copied.</param>
    /// <param name="destDirName">Full Path to the Directory you want to copy to. If the Directory does not exist, it will be created.</param>
    /// <param name="overwriteFiles">Specify if Files should be overwritten or not</param>
    /// <param name="copySubDirs">Specify if you want to copy Sub-Directories as well.</param>
    public static void DirectoryCopy(string sourceDirName, string destDirName, bool overwriteFiles = true, bool copySubDirs = true)
    {
        // Get the subdirectories for the specified directory.
        DirectoryInfo dir = new DirectoryInfo(sourceDirName);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {sourceDirName}");
        }

        DirectoryInfo[] dirs = dir.GetDirectories();

        // If the destination directory doesn't exist, create it.
        Directory.CreateDirectory(destDirName);

        // Get the files in the directory and copy them to the new location.
        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            string tempPath = Path.Combine(destDirName, file.Name);
            file.CopyTo(tempPath, overwriteFiles);
        }

        // If copying subdirectories, copy them and their contents to new location.
        if (!copySubDirs)
            return;

        foreach (DirectoryInfo subDir in dirs)
        {
            string tempPath = Path.Combine(destDirName, subDir.Name);
            DirectoryCopy(subDir.FullName, tempPath, overwriteFiles);
        }

    }

    /// <summary>
    /// This is a custom method, that deletes a Directory. The reason this is used, instead of <see cref="Directory.Delete(string)"/>,
    /// is because this one sets the attributes of all files to be deletable, while <see cref="Directory.Delete(string)"/> does not do that on it's own.
    /// It's needed, because sometimes there are read-only files being generated, that would normally need more code in order to reset the attributes.<br/>
    /// Note, that this method acts recursively.
    /// </summary>
    /// <param name="targetDir">The directory to delete.</param>
    public static void DeleteDirectory(string targetDir)
    {
        if (!Directory.Exists(targetDir)) return;

        File.SetAttributes(targetDir, FileAttributes.Normal);

        foreach (string file in Directory.GetFiles(targetDir))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        foreach (string dir in Directory.GetDirectories(targetDir))
        {
            DeleteDirectory(dir);
        }

        Directory.Delete(targetDir, false);
    }

    /// <summary>
    /// Calculates an MD5 hash from a given file.
    /// </summary>
    /// <param name="filename">Full Path to the file whose MD5 hash is supposed to be calculated.</param>
    /// <returns>The MD5 hash as a <see cref="string"/>, empty string if file does not exist.</returns>
    /*TODO: in the future we should use sha256, as both md5 and sha1 are unsafe.
    This however needs to wait, until we somehow can find a way to publish the windows launcher as .net core...*/
    public static string CalculateMD5(string filename)
    {
        // Check if File exists first
        if (!File.Exists(filename))
            return "";

        using FileStream stream = File.OpenRead(filename);
        using MD5 md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Performs recursive rollover on a set of log files.
    /// </summary>
    /// <param name="logFile">The log file to begin the rollover from.</param>
    /// <param name="max">The maximum amount of log files to retain. Default is 9, as that's the highest digit.</param>
    //TODO: double check this method
    public static void RecursiveRollover(string logFile, int max = 9)
    {
        int index = 1;
        char endChar = logFile[logFile.Length - 1];
        string fileName;

        // If not the original file, set the new index and get the new fileName.
        if (Char.IsNumber(endChar))
        {
            index = Int32.Parse(endChar.ToString()) + 1;
            fileName = logFile.Remove(logFile.Length - 1) + index;
        }
        else // Otherwise, if the original file, just set fileName to log.txt.1.
            fileName = logFile + ".1";

        // If new name already exists, run the rollover algorithm on it!
        if (File.Exists(fileName))
            RecursiveRollover(fileName, max);

        //TODO: this can fail if one doesn't have permissions to move or delete the file
        // If index is less than max, rename file.
        if (index < max)
            File.Move(logFile, fileName);
        else // Otherwise, delete the file.
            File.Delete(logFile);
    }

    /// <summary>
    /// Checks if we currently have an internet connection, by pinging GitHub.
    /// </summary>
    /// <returns><see langword="true"/> if we have internet, <see langword="false"/> if not.</returns>
    public static bool IsConnectedToInternet()
    {
        // TODO: For some reason, using the below approach creates zombie process when checking for Xdelta
        // I have no idea why, but I also can't be bothered to troubleshoot why that is the case right now.
        // Until someone figures out why that is the case, and makes the below approach not create zombie processes
        // it will stay commented out.
        log.Info("Checking internet connection...");
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://github.com");
        try
        {
            request.GetResponse();
        }
        catch (WebException)
        {
            log.Info("Internet connection failed.");
            return false;
        }
        log.Info("Internet connection established!");
        return true;
        
        /*log.Info("Checking internet connection...");
        try
        {
            PingReply pingReply = new Ping().Send("github.com");
            if (pingReply?.Status == IPStatus.Success)
            {
                log.Info("Internet connection established!");
                return true;
            }
        }
        catch { /* ignoring exceptions */ /*}
        log.Info("Internet connection failed.");
        return false;*/
    }

    /// <summary>
    /// Gets <paramref name="languageText"/> and replaces "$NAME" with <paramref name="replacementText"/>.
    /// </summary>
    /// <param name="languageText">The text to get</param>
    /// <param name="replacementText">The text to replace "$NAME" with.</param>
    /// <returns></returns>
    public static string GetText(string languageText, string replacementText = "")
    {
        return languageText.Replace("$NAME", replacementText);
    }
}