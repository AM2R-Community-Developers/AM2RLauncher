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
    public static class CrossPlatformOperations
    {
        /// <summary>
        /// The logger for <see cref="MainForm"/>, used to write any caught exceptions.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainForm));

        /// <summary>
        /// Gets the current platform. 
        /// </summary>
        private static readonly Platform CurrentPlatform = Platform.Instance;

        /// <summary>
        /// Name of the Launcher executable.
        /// </summary>
        public static readonly string LAUNCHERNAME = AppDomain.CurrentDomain.FriendlyName;

        /// <summary>
        /// Path to the Home Folder on *Nix-based systems.
        /// </summary>
        public static readonly string NIXHOME = Environment.GetEnvironmentVariable("HOME");

        /// <summary>
        /// Path to the Config folder on Linux-based systems.
        /// </summary>
        private static readonly string LINUXXDGCONFIG = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

        /// <summary>
        /// Path to the Config file folder on *nix based systems. <br/>
        /// Linux: Will point to XDG_CONFIG_HOME/AM2RLauncher <br/>
        /// Mac: Will point to ~/Library/Preferences/AM2RLauncher
        /// </summary>
        private static readonly string NIXLAUNCHERCONFIGPATH = CurrentPlatform.IsGtk ? ((String.IsNullOrWhiteSpace(LINUXXDGCONFIG) ? (NIXHOME + "/.config")
                                                                                                                                  : LINUXXDGCONFIG) + "/AM2RLauncher")
                                                                                    : NIXHOME + "/Library/Preferences/AM2RLauncher";

        /// <summary>
        /// Config file path for *nix based systems. Will be <see cref="NIXLAUNCHERCONFIGPATH"/> + "/config.xml".
        /// </summary>
        private static readonly string NIXLAUNCHERCONFIGFILEPATH = NIXLAUNCHERCONFIGPATH + "/config.xml";

        /// <summary>
        /// Current Path where the Launcher is located. For more info, check <see cref="GenerateCurrentPath"/>.
        /// </summary>
        public static readonly string CURRENTPATH = GenerateCurrentPath();

        /// <summary>
        /// Generates the mirror list, depending on the current Platform.
        /// </summary>
        /// <returns>A <see cref="List{String}"/> containing the mirror links.</returns>
        public static List<string> GenerateMirrorList()
        {
            if (CurrentPlatform.IsWinForms)
            {
                return new List<string>
                {
                    "https://github.com/AM2R-Community-Developers/AM2R-Autopatcher-Windows.git",
                    "https://gitlab.com/am2r-community-developers/AM2R-Autopatcher-Windows.git"
                };
            }
            else if (CurrentPlatform.IsGtk)
            {
                return new List<string>
                {
                    "https://github.com/AM2R-Community-Developers/AM2R-Autopatcher-Linux.git",
                    "https://gitlab.com/am2r-community-developers/AM2R-Autopatcher-Linux.git"
                };
            }
            else if (CurrentPlatform.IsMac)
            {
                return new List<string>
                {
                    "https://github.com/Miepee/AM2R-Autopatcher-Mac.git",
                    "https://github.com/Miepee/AM2R-Autopatcher-Mac.git"    //TODO: make mac official at some point:tm:, put this on gitlab
                };
                    
            }
            else // Should never occur, but...
            {
                Log.Error(CurrentPlatform.ID + " has no mirror lists!");
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
            Log.Info($"Reading {property} from config.");
            if (CurrentPlatform.IsWinForms)
            {
                // We use the configuration manager in order to read `property` from the app.config and then return it
                ConnectionStringSettings appConfig = ConfigurationManager.ConnectionStrings[property];
                if (appConfig == null) throw new ArgumentException("The property " + property + " could not be found.");
                return appConfig.ConnectionString;
            }
            else if (CurrentPlatform.IsGtk || CurrentPlatform.IsMac)
            {
                string launcherConfigPath = NIXLAUNCHERCONFIGPATH;
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
            else
                Log.Error(CurrentPlatform.ID + " has no config to read from!");
            return null;
        }

        /// <summary>
        /// Writes <paramref name="value"/> to <paramref name="property"/> in the Launcher Config file.
        /// </summary>
        /// <param name="property">The property whose value you want to change.</param>
        /// <param name="value">The value that will be written.</param>
        public static void WriteToConfig(string property, object value)
        {
            Log.Info($"Writing {value} of type {value.GetType()} to {property} to config.");
            if (CurrentPlatform.IsWinForms)
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
            else if (CurrentPlatform.IsGtk || CurrentPlatform.IsMac)
            {
                string launcherConfigPath = NIXLAUNCHERCONFIGPATH;
                string launcherConfigFilePath = NIXLAUNCHERCONFIGFILEPATH;
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
            else
                Log.Error(CurrentPlatform.ID + " has no config to write to!");
        }

        /// <summary>
        /// When a Launcher update occurs that introduces new config properties, this method ensures that the old user config is copied over as much as possible.
        /// </summary>
        public static void CopyOldConfigToNewConfig()
        {
            if (CurrentPlatform.IsWinForms)
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
            else if (CurrentPlatform.IsGtk || CurrentPlatform.IsMac)
            {
                string launcherConfigFilePath = NIXLAUNCHERCONFIGFILEPATH;

                // For some reason deserializing and saving back again works, not exactly sure why, but I'll take it
                XML.LauncherConfigXML launcherConfig = XML.Serializer.Deserialize<XML.LauncherConfigXML>(File.ReadAllText(launcherConfigFilePath));
                File.WriteAllText(launcherConfigFilePath, XML.Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
            }
            else
                Log.Error(CurrentPlatform.ID + " has no config to transfer over!");
        }

        /// <summary>
        /// This open a website cross-platform.
        /// </summary>
        /// <param name="url">The URL of the website to be opened.</param>
        public static void OpenURL(string url)
        {
            if (CurrentPlatform.IsWinForms)
                Process.Start(url);
            else if (CurrentPlatform.IsGtk)
                Process.Start("xdg-open", url);
            else if (CurrentPlatform.IsMac)
                Process.Start("open", url);
            else
                Log.Error(CurrentPlatform.ID + " can't open URLs!");
        }

        /// <summary>
        /// Opens <paramref name="path"/> in a file explorer. Creates the directory if it doesn't exist.
        /// </summary>
        /// <param name="path">Path to open.</param>
        public static void OpenFolder(string path)
        {
            // We have to replace forward slashes with backslashes here on windows because explorer.exe is picky...
            // And on Nix systems, we want to replace ~ with its corresponding env var
            string realPath = CurrentPlatform.IsWinForms ? Environment.ExpandEnvironmentVariables(path).Replace("/", "\\")
                                                         : path.Replace("~", NIXHOME);
            if (!Directory.Exists(realPath))
            {
                Log.Info(realPath + " did not exist and was created");
                Directory.CreateDirectory(realPath);
            }

            // Needs quotes otherwise paths with space wont open
            if (CurrentPlatform.IsWinForms)
                // And we're using explorer.exe to prevent people from stuffing system commands in here wholesale. That would be bad.
                Process.Start("explorer.exe", $"\"{realPath}\"");
            // Linux only opens the directory bc opening and selecting a file is pain
            else if (CurrentPlatform.IsGtk)
                Process.Start("xdg-open", $"\"{realPath}\"");
            else if (CurrentPlatform.IsMac)
                Process.Start("open", $"\"{realPath}\"");
            else
                Log.Error(CurrentPlatform.ID + " can't open folders!");
        }

        /// <summary>
        /// Opens <paramref name="path"/> and selects it in a file explorer. 
        /// Only selects on Windows and Mac, on Linux it just opens the folder. Does nothing if file doesn't exist.
        /// </summary>
        /// <param name="path">Path to open.</param>
        public static void OpenFolderAndSelectFile(string path)
        {
            // We have to replace forward slashes with backslashes here on windows because explorer.exe is picky...
            // And on nix systems, we want to replace ~ with its corresponding env var
            string realPath = CurrentPlatform.IsWinForms ? Environment.ExpandEnvironmentVariables(path).Replace("/", "\\")
                                                         : path.Replace("~", NIXHOME);
            if (!File.Exists(realPath))
            {
                Log.Error(realPath + "did not exist, operation to open its folder was cancelled!");
                return;
            }

            // Needs quotes otherwise paths with spaces wont open
            if (CurrentPlatform.IsWinForms)
                // And we're using explorer.exe to prevent people from stuffing system commands in here wholesale. That would be bad.
                Process.Start("explorer.exe", $"/select, \"{realPath}\"");
            else if (CurrentPlatform.IsGtk)
                Process.Start("xdg-open", $"\"{Path.GetDirectoryName(realPath)}\"");
            else if (CurrentPlatform.IsMac)
                Process.Start("open", $"-R \"{realPath}\"");
            else
                Log.Error(CurrentPlatform.ID + " can't open select files in file explorer!");
        }

        /// <summary>
        /// Checks if command-line Java is installed.
        /// </summary>
        /// <returns><see langword="true"/> if it is installed, <see langword="false"/> if not.</returns>
        public static bool IsJavaInstalled()
        {
            string process = "";
            string arguments = "";

            if (CurrentPlatform.IsWinForms)
            {
                process = "cmd.exe";
                arguments = "/C java -version";
            }
            else if (CurrentPlatform.IsGtk || CurrentPlatform.IsMac)
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
            const string process = "xdelta3";
            const string arguments = "-V";

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
                output += "_";

            string arguments = "-f -d -s \"" + original.Replace(CURRENTPATH + "/", "") + "\" \"" + patch.Replace(CURRENTPATH + "/", "")
                               + "\" \"" + output.Replace(CURRENTPATH + "/", "") + "\"";

            if (CurrentPlatform.IsWinForms)
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
            else if (CurrentPlatform.IsGtk || CurrentPlatform.IsMac)
            {
                ProcessStartInfo parameters = new ProcessStartInfo
                {
                    FileName = "xdelta3",
                    Arguments = arguments,
                    WorkingDirectory = CURRENTPATH
                };

                using (Process proc = Process.Start(parameters))
                {
                    proc?.WaitForExit();
                }
            }

            if (originalOutput != output && File.Exists(output))
            {
                File.Delete(originalOutput);
                File.Move(output, originalOutput);
            }
        }

        public static void RunJavaJar(string arguments = null, string workingDirectory = null)
        {
            if (workingDirectory == null)
                workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string proc = "",
                   javaArgs = "";

            if (CurrentPlatform.IsWinForms)
            {
                proc = "cmd";
                javaArgs = "/C java -jar";
            }
            else if (CurrentPlatform.IsGtk || CurrentPlatform.IsMac)
            {
                proc = "java";
                javaArgs = "-jar";
            }

            ProcessStartInfo jarStart = new ProcessStartInfo
            {
                FileName = proc,
                Arguments = javaArgs + " " + arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process jarProcess = new Process
            {
                StartInfo = jarStart
            };

            jarProcess.Start();

            jarProcess.WaitForExit();
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
                    Log.Info("CurrentPath is set to " + am2rLauncherDataEnvVar);
                    return am2rLauncherDataEnvVar;
                }
                catch (Exception ex)
                {
                    Log.Error($"There was an error with '{am2rLauncherDataEnvVar}'!\n{ex.Message} {ex.StackTrace}. Falling back to defaults.");
                }
            }

            if (CurrentPlatform.IsWinForms)
            {
                Log.Info("Using default Windows CurrentPath.");
                // Windows has the path where the exe is located as default
                return Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
            }
            else if (CurrentPlatform.IsGtk)
            {
                // First check if XDG_DATA_HOME is set, if not we'll use ~/.local/share
                string xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (string.IsNullOrWhiteSpace(xdgDataHome))
                {
                    Log.Info("Using default Linux CurrentPath.");
                    xdgDataHome = NIXHOME + "/.local/share";
                }

                // Add AM2RLauncher to the end of the dataPath
                xdgDataHome += "/AM2RLauncher";

                try
                {
                    // This will create the directories recursively if they don't exist
                    Directory.CreateDirectory(xdgDataHome);

                    // Our env var is now set and directories exist
                    Log.Info("CurrentPath is set to " + xdgDataHome);
                    return xdgDataHome;
                }
                catch (Exception ex)
                {
                    Log.Error($"There was an error with '{xdgDataHome}'!\n{ex.Message} {ex.StackTrace}. Falling back to defaults.");
                }
            }
            else if (CurrentPlatform.IsMac)
            {
                //Mac has the Path at HOME/Library/AM2RLauncher
                string macPath = NIXHOME + "/Library/AM2RLauncher";
                try
                {
                    Directory.CreateDirectory(macPath);
                    Log.Info("Using default Mac CurrentPath.");
                    return macPath;
                }
                catch (Exception ex)
                {
                    Log.Error($"There was an error with '{macPath}'!\n{ex.Message} {ex.StackTrace}. Falling back to defaults.");
                }
            }
            else
                Log.Error(CurrentPlatform.ID + " has no current path!");

            Log.Info("Something went wrong, falling back to the default CurrentPath.");
            return Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
