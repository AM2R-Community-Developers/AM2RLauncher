using Eto;
using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AM2RLauncher
{
    /// <summary>
    /// Class that does Operations that work cross-platform.
    /// </summary>
    public class CrossPlatformOperations
    {
        /// <summary>
        /// The logger for <see cref="MainForm"/>, used to write any caught exceptions.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));

        /// <summary>
        /// Gets the current platform. 
        /// </summary>
        private static readonly Platform currentPlatform = Platform.Instance;

        /// <summary>
        /// Name of the Launcher executable.
        /// </summary>
        public static readonly string LAUNCHERNAME = AppDomain.CurrentDomain.FriendlyName;

        /// <summary>
        /// Path to the Home Folder on *Nix-based systems.
        /// </summary>
        public static readonly string NIXHOME = Environment.GetEnvironmentVariable("HOME");

        /// <summary>
        /// Path to the Config folder on *Nix-based systems.
        /// </summary>
        public static readonly string NIXXDGCONFIG = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        /// <summary>
        /// Current Path where the Launcher is located. For more info, check <see cref="GenerateCurrentPath"/>.
        /// </summary>
        public static readonly string CURRENTPATH = GenerateCurrentPath();

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
                // We use the configuration manager in order to read `property` from the app.config and then return it
                ConnectionStringSettings appConfig = ConfigurationManager.ConnectionStrings[property];
                if (appConfig == null) throw new ArgumentException("The property " + property + " could not be found.");
                return appConfig.ConnectionString;
            }
            if (currentPlatform.IsGtk)
            {
                // Config for nix systems will be saved in XDG_CONFIG_HOME/AM2RLauncher (or if empty, ~/.config)
                string homePath = NIXHOME;
                string launcherConfigPath = (String.IsNullOrWhiteSpace(NIXXDGCONFIG) ? (homePath + "/.config") : NIXXDGCONFIG) + "/AM2RLauncher";
                string launcherConfigFilePath = launcherConfigPath + "/config.xml";
                XML.LauncherConfigXML launcherConfig = new XML.LauncherConfigXML();

                // If folder doesn't exist, create it and the config file
                if (!Directory.Exists(launcherConfigPath) || !File.Exists(launcherConfigFilePath))
                {
                    Directory.CreateDirectory(launcherConfigPath);
                    File.WriteAllText(launcherConfigFilePath, XML.Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
                }

                // Deserialize the config xml into launcherConfig
                launcherConfig = XML.Serializer.Deserialize<XML.LauncherConfigXML>(File.ReadAllText(launcherConfigFilePath));

                if (launcherConfig[property] == null)
                    return null;

                // This uses the indexer, which means, we can use the variable in order to get the property. Look at LauncherConfigXML for more info
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
                // We use the configuration manager in order to read from the app.config, change the value and save it
                Configuration appConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (appConfig == null)
                    throw new NullReferenceException("Could not find the Config file! Please make sure it exists!");
                ConnectionStringsSection connectionStringsSection = (ConnectionStringsSection)appConfig.GetSection("connectionStrings");
                if (connectionStringsSection == null || connectionStringsSection.ConnectionStrings[property]?.ConnectionString == null)
                    throw new ArgumentException("The property " + property + " could not be found.");
                connectionStringsSection.ConnectionStrings[property].ConnectionString = value.ToString();
                appConfig.Save();
                ConfigurationManager.RefreshSection("connectionStrings");
            }
            else if (currentPlatform.IsGtk)
            {
                // Config for nix systems will be saved in XDG_CONFIG_HOME/AM2RLauncher (or if empty, ~/.config)
                string homePath = NIXHOME;
                string launcherConfigPath = (String.IsNullOrWhiteSpace(NIXXDGCONFIG) ? (homePath + "/.config") : NIXXDGCONFIG) + "/AM2RLauncher";
                string launcherConfigFilePath = launcherConfigPath + "/config.xml";
                XML.LauncherConfigXML launcherConfig = new XML.LauncherConfigXML();

                // If folder doesn't exist, create it and the config file
                if (!Directory.Exists(launcherConfigPath) || !File.Exists(launcherConfigFilePath))
                {
                    Directory.CreateDirectory(launcherConfigPath);
                    File.WriteAllText(launcherConfigFilePath, XML.Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
                }
                // Deserialize the config xml into launcherConfig
                launcherConfig = XML.Serializer.Deserialize<XML.LauncherConfigXML>(File.ReadAllText(launcherConfigFilePath));

                // Uses indexer. Look at LauncherConfigXML for more info
                launcherConfig[property] = value;

                // Serialize back into the file
                File.WriteAllText(launcherConfigFilePath, XML.Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
            }
        }

        /// <summary>
        /// When a Launcher update occurs that introduces new config properties, this method ensures that the old user config is copied over as much as possible.
        /// </summary>
        public static void CopyOldConfigToNewConfig()
        {
            if (currentPlatform.IsWinForms)
            {
                string oldConfigPath = LAUNCHERNAME + ".oldCfg";
                string newConfigPath = LAUNCHERNAME + ".config";
                string oldConfigText = File.ReadAllText(oldConfigPath);
                string newConfigText = File.ReadAllText(newConfigPath);

                Regex settingRegex = new Regex("<add name=\".*\" />");

                MatchCollection oldMatch = settingRegex.Matches(oldConfigText);
                MatchCollection newMatch = settingRegex.Matches(newConfigText);

                for (int i = 0; i < oldMatch.Count; i++)
                    newConfigText = newConfigText.Replace(newMatch[i].Value, oldMatch[i].Value);

                File.WriteAllText(newConfigPath, newConfigText);

            }
            else if (currentPlatform.IsGtk)
            {
                // Config for nix systems will be saved in XDG_CONFIG_HOME/AM2RLauncher (or if empty, ~/.config)
                string homePath = NIXHOME;
                string launcherConfigPath = (String.IsNullOrWhiteSpace(NIXXDGCONFIG) ? (homePath + "/.config") : NIXXDGCONFIG) + "/AM2RLauncher";
                string launcherConfigFilePath = launcherConfigPath + "/config.xml";
                XML.LauncherConfigXML launcherConfig = new XML.LauncherConfigXML();

                // For some reason deserializing and saving back again works, not exactly sure why, but I'll take it
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
            if (currentPlatform.IsWinForms)
                Process.Start(url);
            else if (currentPlatform.IsGtk)
                Process.Start("xdg-open", url);
        }

        /// <summary>
        /// Opens <paramref name="path"/> in a file explorer. Creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="path">Path to open.</param>
        public static void OpenFolder(string path)
        {
            // We have to replace forward slashes with backslashes here on windows because explorer.exe is picky...
            // And on Nix systems, we want to replace ~ with its corresponding env var
            string realPath = currentPlatform.IsWinForms ? Environment.ExpandEnvironmentVariables(path).Replace("/", "\\")
                                                         : path.Replace("~", NIXHOME);
            if (!Directory.Exists(realPath))
            {
                log.Info(realPath + " did not exist and was created");
                Directory.CreateDirectory(realPath);
            }

            // Needs quotes otherwise paths with space wont open
            if (currentPlatform.IsWinForms)
                // And we're using explorer.exe to prevent people from stuffing system commands in here wholesale. That would be bad.
                Process.Start("explorer.exe", $"\"{realPath}\"");
            // Linux only opens the directory bc opening and selecting a file is pain
            else if (currentPlatform.IsGtk)
                Process.Start("xdg-open", $"\"{realPath}\"");
        }

        /// <summary>
        /// Opens <paramref name="path"/> and selects it in a file explorer. 
        /// Only selects on Windows, on Linux it just opens the folder. Does nothing if file doesn't exist.
        /// </summary>
        /// <param name="path">Path to open.</param>
        public static void OpenFolderAndSelectFile(string path)
        {
            // We have to replace forward slashes with backslashes here on windows because explorer.exe is picky...
            // And on nix systems, we want to replace ~ with its corresponding env var
            string realPath = currentPlatform.IsWinForms ? Environment.ExpandEnvironmentVariables(path).Replace("/", "\\")
                                                         : path.Replace("~", NIXHOME);
            if (!File.Exists(realPath))
            {
                log.Error(realPath + "did not exist, operation to open its folder was cancelled!");
                return;
            }

            // Needs quotes otherwise paths with spaces wont open
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

            ProcessStartInfo javaStart = new ProcessStartInfo
            {
                FileName = process,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };


            Process java = new Process { StartInfo = javaStart };

            // This is primarily for linux, but could be happening on windows as well
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

            ProcessStartInfo xdeltaStart = new ProcessStartInfo
            {
                FileName = process,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };


            Process xdelta = new Process { StartInfo = xdeltaStart };

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
            // For *whatever reason* **sometimes** xdelta patching doesn't work, if output = original. So I'm fixing that here.
            string originalOutput = output;
            if (original == output)
                output = output += "_";

            string arguments = "-f -d -s \"" + original.Replace(CURRENTPATH + "/", "") + "\" \"" + patch.Replace(CURRENTPATH + "/", "")
                               + "\" \"" + output.Replace(CURRENTPATH + "/", "") + "\"";

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

        /// <summary>
        /// Figures out what the AM2RLauncher's <see cref="CURRENTPATH"/> should be.<br/>
        /// Determination is as follows:
        /// <list type="number">
        ///     <item><b>$AM2RLAUNCHERDATA</b> environment variable is read and folders are recursively generated.</item>
        ///     <item>The current OS is checked. For Windows, the path where the executable is located will be returned.<br/>
        ///     For Linux, <b>$XDG_DATA_HOME/AM2RLauncher</b> will be returned. 
        ///     Should <b>$XDG_DATA_HOME</b> be empty, it will default to <b>$HOME/.local/share</b>.</item>
        ///     <item>The path where the executable is located will be returned.</item>
        /// </list>
        /// Should any errors occur, it falls down to the next step.
        /// </summary>
        /// <returns></returns>
        private static string GenerateCurrentPath()
        {
            // First, we check if the user has a custom AM2RLAUNCHERDATA env var
            string am2rLauncherDataEnvVar = Environment.GetEnvironmentVariable("AM2RLAUNCHERDATA");
            if (!String.IsNullOrWhiteSpace(am2rLauncherDataEnvVar))
            {
                try
                {
                    // This will create the directories recursively if they don't exist
                    Directory.CreateDirectory(am2rLauncherDataEnvVar);

                    // Our env var is now set and directories exist
                    log.Info("CurrentPath is set to " + am2rLauncherDataEnvVar);
                    return am2rLauncherDataEnvVar;
                }
                catch (Exception ex)
                {
                    log.Error($"There was an error with '{am2rLauncherDataEnvVar}'!\n{ex.Message} {ex.StackTrace}. Falling back to defaults.");
                }
            }

            if (currentPlatform.IsWinForms)
            {
                log.Info("Using default Windows CurrentPath.");
                // Windows has the path where the exe is located as default
                return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            }
            else if (currentPlatform.IsGtk)
            {
                // First check if XDG_DATA_HOME is set, if not we'll use ~/.local/share
                string xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (string.IsNullOrWhiteSpace(xdgDataHome))
                {
                    log.Info("Using default Linux CurrentPath.");
                    xdgDataHome = NIXHOME + "/.local/share";
                }

                // Add AM2RLauncher to the end of the dataPath
                xdgDataHome += "/AM2RLauncher";

                try
                {
                    // This will create the directories recursively if they don't exist
                    Directory.CreateDirectory(xdgDataHome);

                    // Our env var is now set and directories exist
                    log.Info("CurrentPath is set to " + xdgDataHome);
                    return xdgDataHome;
                }
                catch (Exception ex)
                {
                    log.Error($"There was an error with '{xdgDataHome}'!\n{ex.Message} {ex.StackTrace}. Falling back to defaults.");
                }
            }

            log.Info("Something went wrong, falling back to the default CurrentPath.");
            return Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
