using Eto;
using Eto.Forms;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AM2RLauncher
{
    /// <summary>
    /// Class that does Operations that work cross-platform.
    /// </summary>
    class CrossPlatformOperations
    {
        /// <summary>
        /// Gets the current platform. 
        /// </summary>
        readonly private static Platform currentPlatform = Platform.Instance;

        /// <summary>
        /// Current Path where the Launcher is located. For Linux this is redirected to ~/.local/share/AM2RLauncher, in order to be more compliant to the XDG Base Directory Specification.
        /// </summary>
        static readonly public string CURRENTPATH = currentPlatform.IsWinForms ? Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) : Environment.GetEnvironmentVariable("HOME") + "/.local/share/AM2RLauncher";

        /// <summary>
        /// Name of the Launcher executable.
        /// </summary>
        static readonly public string LAUNCHERNAME = AppDomain.CurrentDomain.FriendlyName;

        /// <summary>
        /// Generates the mirror list, depending on the current Platform.
        /// </summary>
        /// <returns>A <see cref="List{string}"/> containing the mirror links.</returns>
        public static List<string> GenerateMirrorList()
        {
            if (currentPlatform.IsWinForms)
            {
                return new List<string>
                {
                    "https://github.com/AM2R-Community-Developers/AM2R-Autopatcher-Windows.git",
                    "https://gitlab.com/am2r-community-developers/AM2R-Autopatcher-Windows.git"
                };
            }
            else if (currentPlatform.IsGtk)
            {
                return new List<string>
                {
                    "https://github.com/AM2R-Community-Developers/AM2R-Autopatcher-Linux.git",
                    "https://gitlab.com/am2r-community-developers/AM2R-Autopatcher-Linux.git"
                };
            }
            else // Should never occur, but...
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Reads the Launcher config file on the current Platform and returns the value for <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The property to get the value from.</param>
        /// <returns>The value from <paramref name="property"/> as a string</returns>
        public static string ReadFromConfig(string property)
        {
            if (currentPlatform.IsWinForms)
            {
                //we use the configuration manager in order to read `property` from the app.config and then return it
                ConnectionStringSettings appConfig = ConfigurationManager.ConnectionStrings[property];
                if (appConfig == null) throw new ArgumentException("The property " + property + " could not be found.");
                return appConfig.ConnectionString;
            }
            if (currentPlatform.IsGtk)
            {
                //config for nix systems will be saved in .config/AM2RLauncher
                string homePath = Environment.GetEnvironmentVariable("HOME");
                string launcherConfigPath = homePath + "/.config/AM2RLauncher/";
                string launcherConfigFilePath = launcherConfigPath + "config.xml";
                XML.LauncherConfigXML launcherConfig = new XML.LauncherConfigXML();

                //if folder doesn't exist, create it and the config file
                if (!Directory.Exists(launcherConfigPath) || !File.Exists(launcherConfigFilePath))
                {
                    Directory.CreateDirectory(launcherConfigPath);
                    File.WriteAllText(launcherConfigFilePath, XML.Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
                }

                //deserialize the config xml into launcherConfig
                launcherConfig = XML.Serializer.Deserialize<XML.LauncherConfigXML>(File.ReadAllText(launcherConfigFilePath));

                if (launcherConfig[property] == null)
                    return null;

                //this uses the indexer, which means, we can use the variable in order to get the property. Look at LauncherConfigXML for more info
                return launcherConfig[property]?.ToString();
            }
            return null;
        }

        /// <summary>
        /// Writes <paramref name="value"/> to <paramref name="property"/> in the Launcher Config file.
        /// </summary>
        /// <param name="property">The property whose value you want to change.</param>
        /// <param name="value">The value that will be written.</param>
        public static void WriteToConfig(string property, object value)
        {
            if (currentPlatform.IsWinForms)
            {
                //we use the configuration manager in order to read from the app.config, change the value and save it
                Configuration appConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (appConfig == null)
                    throw new NullReferenceException("Could not find the Config file! Please make sure it exists!");
                ConnectionStringsSection connectionStringsSection = (ConnectionStringsSection)appConfig.GetSection("connectionStrings");
                if(connectionStringsSection == null || connectionStringsSection.ConnectionStrings[property]?.ConnectionString == null) 
                    throw new ArgumentException("The property " + property + " could not be found.");
                connectionStringsSection.ConnectionStrings[property].ConnectionString = value.ToString();
                appConfig.Save();
                ConfigurationManager.RefreshSection("connectionStrings");
            }
            else if (currentPlatform.IsGtk)
            {
                //config for nix systems will be saved in .config/AM2RLauncher
                string homePath = Environment.GetEnvironmentVariable("HOME");
                string launcherConfigPath = homePath + "/.config/AM2RLauncher/";
                string launcherConfigFilePath = launcherConfigPath + "config.xml";
                XML.LauncherConfigXML launcherConfig = new XML.LauncherConfigXML();

                //if folder doesn't exist, create it and the config file
                if (!Directory.Exists(launcherConfigPath) || !File.Exists(launcherConfigFilePath))
                {
                    Directory.CreateDirectory(launcherConfigPath);
                    File.WriteAllText(launcherConfigFilePath, XML.Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
                }
                //deserialize the config xml into launcherConfig
                launcherConfig = XML.Serializer.Deserialize<XML.LauncherConfigXML>(File.ReadAllText(launcherConfigFilePath));

                //uses indexer. Look at LauncherConfigXML for more info
                launcherConfig[property] = value;

                //serialize back into the file
                File.WriteAllText(launcherConfigFilePath, XML.Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
            }
        }
        
        /// <summary>
        /// When a Launcher update occurs that introduces new config properties, this method ensures that the old user config is copied over as much as possible.
        /// </summary>
        public static void CopyOldConfigToNewConfig()
        {
            if(currentPlatform.IsWinForms)
            {
                string oldConfigPath = LAUNCHERNAME + ".oldCfg";
                string newConfigPath = LAUNCHERNAME + ".config";
                string oldConfigText = File.ReadAllText(oldConfigPath);
                string newConfigText = File.ReadAllText(newConfigPath);

                Regex settingRegex = new Regex("<add name=\".*\" />");

                MatchCollection oldMatch = settingRegex.Matches(oldConfigText);
                MatchCollection newMatch = settingRegex.Matches(newConfigText);

                for(int i = 0; i < oldMatch.Count; i++)
                    newConfigText = newConfigText.Replace(newMatch[i].Value, oldMatch[i].Value);

                File.WriteAllText(newConfigPath, newConfigText);

            }
            else if(currentPlatform.IsGtk)
            {
                string homePath = Environment.GetEnvironmentVariable("HOME");
                string launcherConfigPath = homePath + "/.config/AM2RLauncher/";
                string launcherConfigFilePath = launcherConfigPath + "config.xml";
                XML.LauncherConfigXML launcherConfig = new XML.LauncherConfigXML();

                //for some reason deserializing and saving back again works, not exactly sure why, but I'll take it
                launcherConfig = XML.Serializer.Deserialize<XML.LauncherConfigXML>(File.ReadAllText(launcherConfigFilePath));
                File.WriteAllText(launcherConfigFilePath, XML.Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
            }
        }

        /// <summary>
        /// This open a website cross-platform.
        /// </summary>
        /// <param name="url">The URL of the website to be opened.</param>
        public static void OpenURL(string url)
        {
            if(currentPlatform.IsWinForms)
                Process.Start(url);
            else if(currentPlatform.IsGtk)
                Process.Start("xdg-open", url);
        }

        /// <summary>
        /// Opens <paramref name="path"/> in a file explorer. Creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="path">Path to open.</param>
        public static void OpenFolder(string path)
        {
            // We have to replace forward slashes with backslashes here on windows because explorer.exe is picky...
            string realPath = currentPlatform.IsWinForms ? Environment.ExpandEnvironmentVariables(path).Replace("/", "\\") : path.Replace("~", Environment.GetEnvironmentVariable("HOME"));
            if (!Directory.Exists(realPath))
                Directory.CreateDirectory(realPath);

            //needs quotes otherwise paths with space wont open
            if (currentPlatform.IsWinForms)
                // And we're using explorer.exe to prevent people from stuffing system commands in here wholesale. That would be bad.
                Process.Start("explorer.exe", $"\"{realPath}\"");
            // linux only opens the directory bc opening and selecting a file is pain
            else if (currentPlatform.IsGtk)
                Process.Start("xdg-open", $"\"{realPath}\"");
        }

        /// <summary>
        /// Opens <paramref name="path"/> and selects it in a file explorer.
        /// </summary>
        /// <param name="path">Path to open.</param>
        public static void OpenFolderAndSelectFile(string path)
        {
            // We have to replace forward slashes with backslashes here on windows because explorer.exe is picky...
            string realPath = currentPlatform.IsWinForms ? Environment.ExpandEnvironmentVariables(path).Replace("/", "\\") : path.Replace("~", Environment.GetEnvironmentVariable("HOME"));
            if (!File.Exists(realPath))
                return;

            //needs quotes otherwise paths with spaces wont open
            if (currentPlatform.IsWinForms)
                // And we're using explorer.exe to prevent people from stuffing system commands in here wholesale. That would be bad.
                Process.Start("explorer.exe", $"/select, \"{realPath}\"");
            else if (currentPlatform.IsGtk)
                Process.Start("xdg-open", $"\"{Path.GetDirectoryName(realPath)}\"");
        }

        /// <summary>
        /// Checks if command-line Java is installed.
        /// </summary>
        /// <returns><see langword="true"/> if it is installed, <see langword="false"/> if not.</returns>
        public static bool IsJavaInstalled()
        {
            string process = null;
            string arguments = null;

            if (currentPlatform.IsWinForms)
            {
                process = "cmd.exe";
                arguments = "/C java -version";
            }
            else if (currentPlatform.IsGtk)
            {
                process = "java";
                arguments = "-version";
            }

            ProcessStartInfo javaStart = new ProcessStartInfo();
            javaStart.FileName = process;
            javaStart.Arguments = arguments;
            javaStart.UseShellExecute = false;
            javaStart.CreateNoWindow = true;


            Process java = new Process();

            java.StartInfo = javaStart;

            //this is primarily for linux, but could be happening on windows as well
            try
            {
                java.Start();

                java.WaitForExit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }

            return java.ExitCode == 0;
            
        }

        /// <summary>
        /// Checks if command-line xdelta is installed on non-Windows systems.
        /// </summary>
        /// <returns><see langword="true"/> if it is installed, <see langword="false"/> if not.</returns>
        public static bool CheckIfXdeltaIsInstalled()
        {
            string process = "xdelta3";
            string arguments = "-V";      

            ProcessStartInfo xdeltaStart = new ProcessStartInfo();
            xdeltaStart.FileName = process;
            xdeltaStart.Arguments = arguments;
            xdeltaStart.UseShellExecute = false;
            xdeltaStart.CreateNoWindow = true;


            Process xdelta = new Process();

            xdelta.StartInfo = xdeltaStart;

            try
            {
                xdelta.Start();

                xdelta.WaitForExit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }

            return xdelta.ExitCode == 0;
        }

        /// <summary>
        /// This applies an Xdelta Patch cross-platform.
        /// </summary>
        /// <param name="original">Full Path to the original file.</param>
        /// <param name="patch">Full Path to the Xdelta patch to apply.</param>
        /// <param name="output">Full Path to the output file.</param>
        public static void ApplyXdeltaPatch(string original, string patch, string output)
        {
            // for *whatever reason* **sometimes** xdelta patching doesn't work, if output = original. So I'm fixing that here.
            string originalOutput = output;
            if (original == output)
                output = output += "_";

            string arguments = "-f -d -s \"" + original.Replace(CURRENTPATH + "/","") + "\" \"" + patch.Replace(CURRENTPATH + "/", "") + "\" \"" + output.Replace(CURRENTPATH + "/", "") + "\"";

            if (currentPlatform.IsWinForms)
            {
                // We want some fancy parameters for Windows because the terminal scares end users :(
                ProcessStartInfo parameters = new ProcessStartInfo
                {
                    FileName = CURRENTPATH + "/PatchData/utilities/xdelta/xdelta3.exe",
                    WorkingDirectory = CURRENTPATH + "",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = arguments
                };

                using (Process proc = new Process { StartInfo = parameters })
                {
                    proc.Start();

                    proc.WaitForExit();
                }
            }
            else if (currentPlatform.IsGtk)
            {
                ProcessStartInfo parameters = new ProcessStartInfo
                {
                    FileName = "xdelta3",
                    Arguments = arguments,
                    WorkingDirectory = CURRENTPATH
                };

                using (Process proc = Process.Start(parameters))
                {
                    proc.WaitForExit();
                }
            }

            if (originalOutput != output && File.Exists(output))
            {
                File.Delete(originalOutput);
                File.Move(output, originalOutput);
            }
        }
    }
}
