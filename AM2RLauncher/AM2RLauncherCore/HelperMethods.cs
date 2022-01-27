using LibGit2Sharp;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace AM2RLauncher.Core
{
    /// <summary>
    /// An enum, that has possible return codes for <see cref="HelperMethods.CheckIfZipIsAM2R11"/>.
    /// </summary>
    public enum IsZipAM2R11ReturnCodes
    {
        Successful,
        MissingOrInvalidAM2RExe,
        MissingOrInvalidD3DX9_43Dll,
        MissingOrInvalidDataWin,
        GameIsInASubfolder
    }

    /// <summary>
    /// Class that has various Helper functions. Basically anything that could be used outside the Launcher GUI resides here.
    /// </summary>
    public static class HelperMethods
    {
        // Load reference to logger
        /// <summary>
        /// Our log object, that handles logging the current execution to a file.
        /// </summary>
        private static readonly ILog Log = Core.Log;

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
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
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
        public static string CalculateMD5(string filename)
        {
            // Check if File exists first
            if (!File.Exists(filename))
                return "";

            using (var stream = File.OpenRead(filename))
            {
                using (var md5 = MD5.Create())
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Performs recursive rollover on a set of log files.
        /// </summary>
        /// <param name="logFile">The log file to begin the rollover from.</param>
        /// <param name="max">The maximum amount of log files to retain. Default is 9, as that's the highest digit.</param>
        public static void RecursiveRollover(string logFile, int max = 9)
        {
            int index = 1;
            char endChar = logFile[logFile.Length - 1];
            string fileName;

            // If not the original file, set the new index and get the new fileName.
            if (char.IsNumber(endChar))
            {
                index = int.Parse(endChar.ToString()) + 1;
                fileName = logFile.Remove(logFile.Length - 1) + index;
            }
            else // Otherwise, if the original file, just set fileName to log.txt.1.
                fileName = logFile + ".1";

            // If new name already exists, run the rollover algorithm on it!
            if (File.Exists(fileName))
            {
                RecursiveRollover(fileName, max);
            }

            //TODO: this can fail
            // If index is less than max, rename file.
            if (index < max)
            {
                File.Move(logFile, fileName);
            }
            else // Otherwise, delete the file.
                File.Delete(logFile);
        }

        /// <summary>
        /// Checks if we currently have an internet connection, by pinging github.
        /// </summary>
        /// <returns><see langword="true"/> if we have internet, <see langword="false"/> if not.</returns>
        public static bool IsConnectedToInternet()
        {
            Log.Info("Checking internet connection...");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://github.com");
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException)
            {
                Log.Info("Internet connection failed.");
                return false;
            }
            Log.Info("Internet connection established!");
            return true;
        }

        /// <summary>
        /// Checks if the repository has been validly cloned already.
        /// </summary>
        /// <returns><see langword="true"/> if yes, <see langword="false"/> if not.</returns>
        public static bool IsPatchDataCloned()
        {
            // isValid seems to only check for a .git folder, and there are cases where that exists, but not the profile.xml
            return Repository.IsValid(CrossPlatformOperations.CURRENTPATH + "/PatchData") && File.Exists(CrossPlatformOperations.CURRENTPATH + "/PatchData/profile.xml");
        }

        /// <summary>
        /// Checks if a Zip file is a valid AM2R_1.1 zip.
        /// </summary>
        /// <param name="zipPath">Full Path to the Zip file to check.</param>
        /// <returns><see cref="IsZipAM2R11ReturnCodes"/> detailing the result</returns>
        public static IsZipAM2R11ReturnCodes CheckIfZipIsAM2R11(string zipPath)
        {
            const string d3dHash = "86e39e9161c3d930d93822f1563c280d";
            const string dataWinHash = "f2b84fe5ba64cb64e284be1066ca08ee";
            const string am2rHash = "15253f7a66d6ea3feef004ebbee9b438";

            string tmpPath = Path.GetTempPath() + Path.GetFileNameWithoutExtension(zipPath);

            // Clean up in case folder exists already
            if (Directory.Exists(tmpPath))
                Directory.Delete(tmpPath, true);

            Directory.CreateDirectory(tmpPath);

            // Open archive
            ZipArchive am2rZip = ZipFile.OpenRead(zipPath);

            // Check if exe exists anywhere
            ZipArchiveEntry am2rExe = am2rZip.Entries.FirstOrDefault(x => x.FullName.Contains("AM2R.exe"));
            if (am2rExe == null)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidAM2RExe;
            // Check if it's not in a subfolder. if it'd be in a subfolder, fullname would be "folder/AM2R.exe"
            if (am2rExe.FullName != "AM2R.exe")
                return IsZipAM2R11ReturnCodes.GameIsInASubfolder;
            // Check validity
            am2rExe.ExtractToFile(tmpPath + "/" + am2rExe.FullName);
            if (HelperMethods.CalculateMD5(tmpPath + "/" + am2rExe.FullName) != am2rHash)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidAM2RExe;

            // Check if data.win exists / is valid
            ZipArchiveEntry dataWin = am2rZip.Entries.FirstOrDefault(x => x.FullName == "data.win");
            if (dataWin == null)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidDataWin;
            dataWin.ExtractToFile(tmpPath + "/" + dataWin.FullName);
            if (HelperMethods.CalculateMD5(tmpPath + "/" + dataWin.FullName) != dataWinHash)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidDataWin;

            // Check if d3d.dll exists / is valid
            ZipArchiveEntry d3dx = am2rZip.Entries.FirstOrDefault(x => x.FullName == "D3DX9_43.dll");
            if (d3dx == null)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidD3DX9_43Dll;
            d3dx.ExtractToFile(tmpPath + "/" + d3dx.FullName);
            if (HelperMethods.CalculateMD5(tmpPath + "/" + d3dx.FullName) != d3dHash)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidD3DX9_43Dll;

            // Clean up
            Directory.Delete(tmpPath, true);

            // If we didn't exit before, everything is fine
            Log.Info("AM2R_11 check successful!");
            return IsZipAM2R11ReturnCodes.Successful;
        }

    }
}
