using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AM2RLauncher.Helpers
{
    static class HelperMethods
    {
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
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, overwriteFiles, copySubDirs);
                }
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
        /// <param name="max">The maximum amount of log files to retain. Default is int.MaxValue, in other words, never delete files.</param>
        public static void RecursiveRollover(string logFile, int max = int.MaxValue)
        {
            int index = 1;
            char endChar = logFile[logFile.Length - 1];
            string fileName;

            // If not the original file, set the new index and get the new fileName.
            if (endChar != 't')
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

            // If index is less than max, rename file.
            if (index < max)
            {
                File.Move(logFile, fileName);
            }
            else // Otherwise, delete the file.
                File.Delete(logFile);
        }
    }
}
