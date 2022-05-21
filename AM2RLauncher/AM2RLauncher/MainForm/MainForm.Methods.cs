using AM2RLauncher.Core;
using AM2RLauncher.Core.XML;
using AM2RLauncher.Language;
using Eto.Forms;
using LibGit2Sharp;
using System;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using Configuration = System.Configuration.Configuration;

namespace AM2RLauncher
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// Method that updates <see cref="progressBar"/>.
        /// </summary>
        /// <param name="value">The value that <see cref="progressBar"/> should be set to.</param>
        /// <param name="min">The min value that <see cref="progressBar"/> should be set to.</param>
        /// <param name="max">The max value that <see cref="progressBar"/> should be set to.</param>
        private void UpdateProgressBar(int value, int min = 0, int max = 100)
        {
            Application.Instance.Invoke(() =>
            {
                progressBar.MinValue = min;
                progressBar.MaxValue = max;
                progressBar.Value = value;
            });
        }


        /// <summary>
        /// Method that updates <see cref="progressBar"/> with a min value of 0 and max value of 100.
        /// </summary>
        /// <param name="value">The value that <see cref="progressBar"/> should be set to.</param>
        private void UpdateProgressBar(int value)
        {
            UpdateProgressBar(value, 0, 100);
        }

        /// <summary>
        /// Safety check function before accessing <see cref="profileIndex"/>.
        /// </summary>
        /// <returns><see langword="true"/> if it is valid, <see langword="false"/> if not.</returns>
        private bool IsProfileIndexValid()
        {
            return profileIndex != null;
        }

        /// <summary>
        /// This is just a helper method for the git commands in order to have a progress bar display for them.
        /// </summary>
        private bool TransferProgressHandlerMethod(TransferProgress transferProgress)
        {
            // Thank you random issue on the gitlib2sharp repo!!!!
            // Also tldr; rtfm
            if (isGitProcessGettingCancelled) return false;

            // This needs to be in an Invoke, in order to access the variables from the main thread
            // Otherwise this will throw a runtime exception
            Application.Instance.Invoke(() =>
            {
                progressBar.MinValue = 0;
                progressBar.MaxValue = transferProgress.TotalObjects;
                if (currentGitObject >= transferProgress.ReceivedObjects)
                    return;
                progressLabel.Text = Text.ProgressbarProgress + " " + transferProgress.ReceivedObjects + " (" + ((int)transferProgress.ReceivedBytes / 1000000) + "MB) / " + transferProgress.TotalObjects + " objects";
                currentGitObject = transferProgress.ReceivedObjects;
                progressBar.Value = transferProgress.ReceivedObjects;
            });

            return true;
        }

        /// <summary>
        /// Creates a single-file, zip-filtered file dialog.
        /// </summary>
        /// <param name="title">The title of the file dialog.</param>
        /// <returns>The created file dialog.</returns>
        private OpenFileDialog GetSingleZipDialog(string title = "")
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Directory = new Uri(CrossPlatformOperations.CurrentPath),
                MultiSelect = false,
                Title = title
            };
            fileDialog.Filters.Add(new FileFilter(Text.ZipArchiveText, ".zip"));
            return fileDialog;
        }

        private void DisableProgressBar()
        {
            progressBar.Visible = false;
            progressBar.Value = 0;
        }

        private void EnableProgressBar()
        {
            progressBar.Visible = true;
            progressBar.Value = 0;
        }

        private void DisableProgressBarAndProgressLabel()
        {
            DisableProgressBar();
            progressLabel.Visible = false;
            progressLabel.Text = "";
        }

        private void EnableProgressBarAndLabel()
        {
            EnableProgressBar();
            progressLabel.Visible = true;
            progressLabel.Text = "";
        }

        /// <summary>
        /// Reads the Launcher config file on the current Platform and returns the value for <paramref name="property"/>.
        /// </summary>
        /// <param name="property">The property to get the value from.</param>
        /// <returns>The value from <paramref name="property"/> as a string</returns>
        public static string ReadFromConfig(string property)
        {
            log.Info($"Reading {property} from config.");
            if (OS.IsWindows)
            {
                // We use the configuration manager in order to read `property` from the app.config and then return it
                ConnectionStringSettings appConfig = ConfigurationManager.ConnectionStrings[property];
                if (appConfig == null) throw new ArgumentException("The property " + property + " could not be found.");
                return appConfig.ConnectionString;
            }
            if (OS.IsUnix)
            {
                string launcherConfigFilePath = CrossPlatformOperations.NixLauncherConfigFilePath;
                string launcherConfigPath = Path.GetDirectoryName(launcherConfigFilePath);
                XML.LauncherConfigXML launcherConfig = new XML.LauncherConfigXML();

                // If folder doesn't exist, create it and the config file
                if (!Directory.Exists(launcherConfigPath) || !File.Exists(launcherConfigFilePath))
                {
                    Directory.CreateDirectory(launcherConfigPath);
                    File.WriteAllText(launcherConfigFilePath, Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
                }

                // Deserialize the config xml into launcherConfig
                launcherConfig = Serializer.Deserialize<XML.LauncherConfigXML>(File.ReadAllText(launcherConfigFilePath));

                // This uses the indexer, which means, we can use the variable in order to get the property. Look at LauncherConfigXML for more info
                return launcherConfig[property]?.ToString();
            }

            log.Error(OS.Name + " has no config to read from!");
            return null;
        }

        /// <summary>
        /// Writes <paramref name="value"/> to <paramref name="property"/> in the Launcher Config file.
        /// </summary>
        /// <param name="property">The property whose value you want to change.</param>
        /// <param name="value">The value that will be written.</param>
        public static void WriteToConfig(string property, object value)
        {
            log.Info($"Writing {value} of type {value.GetType()} to {property} to config.");
            if (OS.IsWindows)
            {
                // We use the configuration manager in order to read from the app.config, change the value and save it
                Configuration appConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (appConfig == null)
                    throw new NullReferenceException("Could not find the Config file! Please make sure it exists!");
                ConnectionStringsSection connectionStringsSection = (ConnectionStringsSection)appConfig.GetSection("connectionStrings");
                if (connectionStringsSection?.ConnectionStrings[property]?.ConnectionString == null)
                    throw new ArgumentException("The property " + property + " could not be found.");
                connectionStringsSection.ConnectionStrings[property].ConnectionString = value.ToString();
                appConfig.Save();
                ConfigurationManager.RefreshSection("connectionStrings");
            }
            else if (OS.IsUnix)
            {
                string launcherConfigFilePath = CrossPlatformOperations.NixLauncherConfigFilePath;
                string launcherConfigPath = Path.GetDirectoryName(launcherConfigFilePath);
                XML.LauncherConfigXML launcherConfig = new XML.LauncherConfigXML();

                // If folder doesn't exist, create it and the config file
                if (!Directory.Exists(launcherConfigPath) || !File.Exists(launcherConfigFilePath))
                {
                    Directory.CreateDirectory(launcherConfigPath);
                    File.WriteAllText(launcherConfigFilePath, Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
                }
                // Deserialize the config xml into launcherConfig
                launcherConfig = Serializer.Deserialize<XML.LauncherConfigXML>(File.ReadAllText(launcherConfigFilePath));

                // Uses indexer. Look at LauncherConfigXML for more info
                launcherConfig[property] = value;

                // Serialize back into the file
                File.WriteAllText(launcherConfigFilePath, Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
            }
            else
                log.Error(OS.Name + " has no config to write to!");
        }

        /// <summary>
        /// When a Launcher update occurs that introduces new config properties, this method ensures that the old user config is copied over as much as possible.
        /// </summary>
        public static void CopyOldConfigToNewConfig()
        {
            if (OS.IsWindows)
            {
                string oldConfigPath = CrossPlatformOperations.LauncherName + ".oldCfg";
                string newConfigPath = CrossPlatformOperations.LauncherName + ".config";
                string oldConfigText = File.ReadAllText(oldConfigPath);
                string newConfigText = File.ReadAllText(newConfigPath);

                Regex settingRegex = new Regex("<add name=\".*\" />");

                MatchCollection oldMatch = settingRegex.Matches(oldConfigText);
                MatchCollection newMatch = settingRegex.Matches(newConfigText);

                for (int i = 0; i < oldMatch.Count; i++)
                    newConfigText = newConfigText.Replace(newMatch[i].Value, oldMatch[i].Value);

                File.WriteAllText(newConfigPath, newConfigText);

            }
            else if (OS.IsUnix)
            {
                string launcherConfigFilePath = CrossPlatformOperations.NixLauncherConfigFilePath;

                // For some reason deserializing and saving back again works, not exactly sure why, but I'll take it
                XML.LauncherConfigXML launcherConfig = Serializer.Deserialize<XML.LauncherConfigXML>(File.ReadAllText(launcherConfigFilePath));
                File.WriteAllText(launcherConfigFilePath, Serializer.Serialize<XML.LauncherConfigXML>(launcherConfig));
            }
            else
                log.Error(OS.Name + " has no config to transfer over!");
        }
    }
}