using AM2RLauncher.Core;
using AM2RLauncher.Core.XML;
using Eto.Forms;
using LibGit2Sharp;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AM2RLauncher
{
    /// <summary>
    /// Methods for UI Events that get triggered go in here
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>This is used for <see cref="TransferProgressHandlerMethod"/> to get the current Git Object during cloning.</summary>
        private static int currentGitObject = 0;

        /// <summary>
        /// This is a static variable, that <see cref="MainForm.TransferProgressHandlerMethod(TransferProgress)"/> uses, to check if it should cancel the current git process.
        /// </summary>
        private static bool isGitProcessGettingCancelled = false;

        /// <summary>
        /// After the <see cref="playButton"/> has bee loaded, git pull if a repo has been cloned already.
        /// </summary>
        private async void PlayButtonLoadComplete(object sender, EventArgs e)
        {
            LoadProfilesAndAdjustLists();
            if (!Profile.IsPatchDataCloned() || !(bool)autoUpdateAM2RCheck.Checked)
                return;
            
            SetPlayButtonState(UpdateState.Downloading);

            progressBar.Visible = true;
            progressLabel.Visible = true;
            progressBar.Value = 0;

            // Try to pull first.
            try
            {
                log.Info("Attempting to pull repository " + currentMirror + "...");
                await Task.Run(() => Profile.PullPatchData(TransferProgressHandlerMethod));

                // Thank you druid, for this case that should never happen
                if (!File.Exists(CrossPlatformOperations.CURRENTPATH + "/PatchData/profile.xml"))
                {
                    log.Error("Druid PatchData corruption occurred!");
                    await Application.Instance.InvokeAsync(() =>
                    {
                        MessageBox.Show(Language.Text.CorruptPatchData, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                    });
                    HelperMethods.DeleteDirectory(CrossPlatformOperations.CURRENTPATH + "/PatchData");
                    return;
                }
            }
            catch (UserCancelledException ex) 
            {
                log.Info(ex.Message);
                MessageBox.Show(Language.Text.CorruptPatchData, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                HelperMethods.DeleteDirectory(CrossPlatformOperations.CURRENTPATH + "/PatchData");
            }
            catch (LibGit2SharpException ex)   // This is for any exceptions from libgit
            {
                // Libgit2sharp error messages are always in english!
                if (ex.Message.ToLower().Contains("failed to send request") || ex.Message.ToLower().Contains("connection with the server was terminated") ||
                    ex.Message.ToLower().Contains("failed to resolve address"))
                {
                    if (!(bool)autoUpdateAM2RCheck.Checked)
                    {
                        log.Error("Internet connection failed while attempting to pull repository" + currentMirror + "!");
                        MessageBox.Show(Language.Text.InternetConnectionDrop, Language.Text.WarningWindowTitle, MessageBoxType.Warning);
                    }
                }
                else
                {
                    log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                    MessageBox.Show(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                }
            }
            catch (Exception ex) // This is if somehow any other exception might get thrown as well.
            {
                log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                MessageBox.Show(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
            }
            finally
            {
                progressBar.Visible = false;
                progressLabel.Visible = false;
                LoadProfilesAndAdjustLists();
            }

            // Handling for updates - if current version does not match PatchData version, rename folder so that we attempt to install!
            // Also, add a non-installable profile for it so people can access the older version or delete it from the mod manager.
            if (profileList.Count > 0 && Profile.IsProfileInstalled(profileList[0]))
            {
                ProfileXML currentXML = Serializer.Deserialize<ProfileXML>(File.ReadAllText(CrossPlatformOperations.CURRENTPATH + "/Profiles/Community Updates (Latest)/profile.xml"));

                if (currentXML.Version != profileList[0].Version)
                {
                    log.Info("New game version (" + profileList[0].Version + ") detected! Beginning archival of version " + currentXML.Version + "...");
                    Profile.ArchiveProfile(currentXML);
                    profileDropDown.SelectedIndex = 0;
                    LoadProfilesAndAdjustLists();
                }
            }

            SetPlayButtonState(UpdateState.Install);
            UpdateStateMachine();
        }

        /// <summary>
        /// Does a bunch of stuff, depending on the current state of <see cref="updateState"/>.
        /// </summary>
        private async void PlayButtonClickEvent(object sender, EventArgs e)
        {
            // State Check
            UpdateStateMachine();

            // Check if 1.1 is installed by forcing invalidation
            Profile.Is11Installed(true);

            switch (updateState)
            {
                #region Download
                case UpdateState.Download:

                    log.Info("Attempting to clone repository " + currentMirror + "...");
                    bool successful = true;

                    // Update playButton states
                    SetPlayButtonState(UpdateState.Downloading);

                    // Enable progressBar
                    progressBar.Visible = true;
                    progressLabel.Visible = true;
                    progressBar.Value = 0;

                    // Set up progressBar update method
                    var c = new CloneOptions
                    {
                        OnTransferProgress = TransferProgressHandlerMethod
                    };

                    // Everything after this is on a different thread, so the rest of the launcher isn't locked up.
                    try
                    {
                        if (Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/PatchData"))
                        {
                            log.Info("PatchData directory already exists, cleaning up...");
                            HelperMethods.DeleteDirectory(CrossPlatformOperations.CURRENTPATH + "/PatchData");
                        }

                        await Task.Run(() => Repository.Clone(currentMirror, CrossPlatformOperations.CURRENTPATH + "/PatchData", c));
                    }
                    catch (UserCancelledException)
                    {
                        // We deliberately cancelled this!
                        successful = false;
                    }
                    catch (LibGit2SharpException ex)    // This is for any exceptions from libgit
                    {
                        // Libgit2sharp error messages are always in english!
                        if (ex.Message.ToLower().Contains("failed to send request") || ex.Message.ToLower().Contains("connection with the server was terminated") ||
                            ex.Message.ToLower().Contains("failed to resolve address"))
                        {
                            log.Error("Internet connection dropped while attempting to clone repository" + currentMirror + "!");
                            MessageBox.Show(Language.Text.InternetConnectionDrop, Language.Text.WarningWindowTitle, MessageBoxType.Warning);
                        }
                        else
                        {
                            log.Error("LibGit2SharpException: " + ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                            MessageBox.Show(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                            if (Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/PatchData"))
                                HelperMethods.DeleteDirectory(CrossPlatformOperations.CURRENTPATH + "/PatchData");
                        }
                        successful = false;
                    }
                    catch (Exception ex)             // This is if somehow any other exception might get thrown as well.
                    {
                        log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                        MessageBox.Show(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Language.Text.ErrorWindowTitle, MessageBoxType.Error);

                        if (Directory.Exists(CrossPlatformOperations.CURRENTPATH + " / PatchData"))
                            HelperMethods.DeleteDirectory(CrossPlatformOperations.CURRENTPATH + "/PatchData");
                        successful = false;
                    }

                    log.Info("Repository clone attempt finished " + (successful ? "successfully." : "unsuccessfully."));

                    currentGitObject = 0;

                    // Reset progressBar after clone is finished
                    progressLabel.Visible = false;
                    progressLabel.Text = "";
                    progressBar.Visible = false;
                    progressBar.Value = 0;

                    // Just need to switch this to anything that isn't an "active" state so SetUpdateState() actually does something
                    SetPlayButtonState(UpdateState.Install);

                    // This needs to be run BEFORE the state check so that the Mod Settings tab doesn't weird out
                    LoadProfilesAndAdjustLists();

                    // Do a state check
                    UpdateStateMachine();

                    break;
                #endregion

                #region Downloading

                case UpdateState.Downloading:
                    var result = MessageBox.Show(Language.Text.CloseOnCloningText, Language.Text.WarningWindowTitle, MessageBoxButtons.YesNo, MessageBoxType.Warning, MessageBoxDefaultButton.No);
                    if (result == DialogResult.No)
                        return;
                    else
                    {
                        log.Info("User cancelled download!");
                        isGitProcessGettingCancelled = true;
                    }
                    // We don't need to delete any folders here, the cancelled gitClone will do that automatically for us :)
                    // But we should probably wait a bit before proceeding, since cleanup can take a while
                    Thread.Sleep(1000);
                    isGitProcessGettingCancelled = false;
                    break;

                #endregion

                #region Select11
                case UpdateState.Select11:

                    log.Info("Requesting user input for AM2R_11.zip...");

                    OpenFileDialog fileFinder = new OpenFileDialog
                    {
                        Directory = new Uri(CrossPlatformOperations.CURRENTPATH),
                        MultiSelect = false,
                        Title = Language.Text.Select11FileDialog
                    };

                    fileFinder.Filters.Add(new FileFilter(Language.Text.ZipArchiveText, ".zip"));

                    if (fileFinder.ShowDialog(this) != DialogResult.Ok)
                    {
                        log.Info("User cancelled the selection.");
                        return;
                    }

                    if (!String.IsNullOrWhiteSpace(fileFinder.FileName)) // This is default
                    {
                        if (Directory.Exists(fileFinder.FileName))
                        {
                            // This can happen on linux, and maybe windows as well
                            log.Error("User selected a Directory. Cancelling import.");
                            return;
                        }

                        IsZipAM2R11ReturnCodes errorCode = Profile.CheckIfZipIsAM2R11(fileFinder.FileName);
                        if (errorCode != IsZipAM2R11ReturnCodes.Successful)
                        {
                            log.Error("User tried to input invalid AM2R_11.zip file (" + errorCode + "). Cancelling import.");
                            MessageBox.Show(Language.Text.ZipIsNotAM2R11 + "\n\nError Code: " + errorCode, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                            return;
                        }

                        // If either a directory was selected or the file somehow went missing, cancel
                        if (!File.Exists(fileFinder.FileName))
                        {
                            log.Error("Selected AM2R_11.zip file not found! Cancelling import.");
                            break;
                        }

                        // We check if it exists first, because someone coughDRUIDcough might've copied it into here while on the showDialog
                        if (!File.Exists(CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip"))
                            File.Copy(fileFinder.FileName, CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip");

                        log.Info("AM2R_11.zip successfully imported.");
                    }
                    else
                    {
                        log.Error("User did not supply valid input. Cancelling import.");
                    }

                    UpdateStateMachine();
                    break;
                #endregion

                #region Install
                case UpdateState.Install:
                    progressBar.Visible = true;
                    progressBar.Value = 0;
                    SetPlayButtonState(UpdateState.Installing);

                    // Make sure the main interface state machines properly
                    UpdateApkState();
                    UpdateProfileState();

                    // If the file cannot be launched due to anti-virus shenanigans or any other reason, we try catch here
                    try
                    {
                        // Check if xdelta is installed on linux´and exit if not
                        if ((OS.IsUnix) && !CrossPlatformOperations.CheckIfXdeltaIsInstalled())
                        {
                            MessageBox.Show(Language.Text.XdeltaNotFound, Language.Text.WarningWindowTitle, MessageBoxButtons.OK);
                            
                            SetPlayButtonState(UpdateState.Install);
                            UpdateStateMachine();
                            log.Error("Xdelta not found. Aborting installing a profile...");
                            break;
                        }
                        var progressIndicator = new Progress<int>(UpdateProgressBar);
                        bool useHqMusic = hqMusicPCCheck.Checked.Value;
                        await Task.Run(() => Profile.InstallProfile(profileList[profileIndex.Value], useHqMusic, progressIndicator));
                        // This is just for visuals because the average windows end user will ask why it doesn't go to the end otherwise.
                        if (OS.IsWindows)
                            Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                        MessageBox.Show(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                    }
                    progressBar.Visible = false;
                    progressBar.Value = 0;

                    // Just need to switch this to anything that isn't an "active" state so SetUpdateState() actually does something
                    SetPlayButtonState(UpdateState.Play);

                    UpdateStateMachine();

                    break;
                #endregion

                #region Play
                case UpdateState.Play:

                    if (!IsProfileIndexValid())
                        return;

                    ProfileXML profile = profileList[profileIndex.Value];

                    Visible = false;

                    SetPlayButtonState(UpdateState.Playing);

                    // Make sure the main interface state machines properly
                    UpdateApkState();
                    UpdateProfileState();

                    this.ShowInTaskbar = false;
                    trayIndicator.Visible = true;
                    WindowState windowStateBeforeLaunching = this.WindowState;
                    if (windowStateBeforeLaunching == WindowState.Maximized)
                        this.WindowState = WindowState.Normal;
                    Minimize();

                    string envVarText = customEnvVarTextBox?.Text;
                    bool createDebugLogs = profileDebugLogCheck.Checked.Value;

                    await Task.Run(() => Profile.RunGame(profile, createDebugLogs, envVarText));

                    this.ShowInTaskbar = true;
                    trayIndicator.Visible = false;
                    Show();
                    BringToFront();
                    if (windowStateBeforeLaunching == WindowState.Maximized)
                        Maximize();

                    SetPlayButtonState(UpdateState.Play);

                    UpdateStateMachine();

                    Visible = true;

                    break;

                #endregion

                default: break;
            }
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
                progressLabel.Text = Language.Text.ProgressbarProgress + " " + transferProgress.ReceivedObjects + " (" + ((int)transferProgress.ReceivedBytes / 1000000) + "MB) / " + transferProgress.TotalObjects + " objects";
                currentGitObject = transferProgress.ReceivedObjects;
                progressBar.Value = transferProgress.ReceivedObjects;
            });

            return true;
        }

        /// <summary>
        /// Does stuff, depending on the current state of <see cref="apkButtonState"/>.
        /// </summary>
        private async void ApkButtonClickEvent(object sender, EventArgs e)
        {
            // Check for java, exit safely with a warning if not found!
            if (!CrossPlatformOperations.IsJavaInstalled())
            {
                MessageBox.Show(Language.Text.JavaNotFound, Language.Text.WarningWindowTitle, MessageBoxButtons.OK);   
                SetApkButtonState(ApkButtonState.Create);
                UpdateStateMachine();
                log.Error("Java not found! Aborting Android APK creation.");
                return;
            }
            // Check if xdelta is installed on linux
            if (OS.IsUnix && !CrossPlatformOperations.CheckIfXdeltaIsInstalled())
            {
                MessageBox.Show(Language.Text.XdeltaNotFound, Language.Text.WarningWindowTitle, MessageBoxButtons.OK);
                SetApkButtonState(ApkButtonState.Create);
                UpdateStateMachine();
                log.Error("Xdelta not found. Aborting Android APK creation...");
                return;
            }

            UpdateStateMachine();

            if (apkButtonState == ApkButtonState.Create)
            {
                SetApkButtonState(ApkButtonState.Creating);

                UpdateStateMachine();

                progressBar.Visible = true;
                bool useHqMusic = hqMusicAndroidCheck.Checked.Value;

                var progressIndicator = new Progress<int>(UpdateProgressBar);
                await Task.Run(() => Profile.CreateAPK(profileList[profileIndex.Value], useHqMusic, progressIndicator));

                SetApkButtonState(ApkButtonState.Create);

                progressBar.Visible = false;
            }

            UpdateStateMachine();
        }

        /// <summary>
        /// Runs on <see cref="newsWebView"/>'s DocumentLoaded event. Manages the warning for no internet connection.
        /// </summary>
        private void NewsWebViewDocumentLoaded(object sender, WebViewLoadedEventArgs e)
        {
            if (!isInternetThere)
            {
                newsPage.Content = new TableLayout
                {
                    Rows =
                    {
                        null,
                        newsNoConnectionLabel,
                        null
                    }
                };
            }
        }

        /// <summary>
        /// Runs on <see cref="changelogWebView"/>'s DocumentLoaded event. Manages the warning for no internet connection.
        /// </summary>
        private void ChangelogWebViewDocumentLoaded(object sender, WebViewLoadedEventArgs e)
        {
            if (!isInternetThere)
            {
                changelogPage.Content = new TableLayout
                {
                    Rows =
                    {
                        null,
                        changelogNoConnectionLabel,
                        null
                    }
                };
            }
        }

        /// <summary>
        /// Runs when <see cref="addModButton"/> is clicked. Brings up a file select to select a mod, and adds that to the mod directory.
        /// </summary>
        private void AddModButtonClicked(object sender, EventArgs e)
        {
            log.Info("User requested to add mod. Requesting user input for new mod .zip...");

            OpenFileDialog fileFinder = new OpenFileDialog
            {
                Directory = new Uri(CrossPlatformOperations.CURRENTPATH),
                MultiSelect = false,
                Title = Language.Text.SelectModFileDialog
            };

            fileFinder.Filters.Add(new FileFilter(Language.Text.ZipArchiveText, ".zip"));

            if (fileFinder.ShowDialog(this) != DialogResult.Ok)
            {
                log.Info("User cancelled the Mod selection.");
                return;
            }

            if (String.IsNullOrWhiteSpace(fileFinder.FileName))
            {
                log.Error("User did not supply valid input. Cancelling import.");
                LoadProfilesAndAdjustLists();
                return;
            }

            log.Info("User selected \"" + fileFinder.FileName + "\"");

            // If either a directory was selected or the file somehow went missing, cancel
            if (!File.Exists(fileFinder.FileName))
            {
                log.Error("Selected mod .zip file not found! Cancelling import.");
                return;
            }

            FileInfo modFile = new FileInfo(fileFinder.FileName);

            string modsDir = new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/Mods").FullName;
            string extractedName = modFile.Name.Replace(".zip", "");

            // Extract it and see if it contains a profile.xml. If not, this is invalid

            // Check first, if the directory is already there, if yes, throw a message
            if (Directory.Exists(modsDir + "/" + extractedName))
            {
                ProfileXML profile2 = Serializer.Deserialize<ProfileXML>(File.ReadAllText(modsDir + "/" + extractedName + "/profile.xml"));
                log.Error("Mod is already imported as " + extractedName + "! Cancelling mod import.");

                MessageBox.Show(Language.Text.ModIsAlreadyInstalledMessage.Replace("$NAME", profile2.Name), Language.Text.WarningWindowTitle, MessageBoxType.Warning);
                return;
            }
            // Directory doesn't exist -> extract!
            ZipFile.ExtractToDirectory(fileFinder.FileName, modsDir + "/" + extractedName);
            log.Info("Imported and extracted mod .zip as " + extractedName);

            // Let's check if profile.xml exists in there! If it doesn't throw an error and cleanup
            if (!File.Exists(modsDir + "/" + extractedName + "/profile.xml"))
            {
                log.Error(fileFinder.FileName + " does not contain profile.xml! Cancelling mod import.");

                MessageBox.Show(Language.Text.ModIsInvalidMessage.Replace("$NAME", extractedName), Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                Directory.Delete(modsDir + "/" + extractedName, true);
                File.Delete(CrossPlatformOperations.CURRENTPATH + "/Mods/" + modFile.Name);
                return;
            }

            ProfileXML profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(modsDir + "/" + extractedName + "/profile.xml"));

            // Check if the OS versions match
            if (OS.Name != profile.OperatingSystem)
            {
                log.Error("Mod is for " + profile.OperatingSystem + " while current OS is " + OS.Name + ". Cancelling mod import.");

                MessageBox.Show(Language.Text.ModIsForWrongOS.Replace("$NAME", profile.Name).Replace("$OS", profile.OperatingSystem).Replace("$CURRENTOS", OS.Name),
                                Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                HelperMethods.DeleteDirectory(modsDir + "/" + extractedName);
                return;
            }

            // Check by *name*, if the mod was installed already
            if (profileList.FirstOrDefault(p => p.Name == profile.Name) != null || Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name))
            {
                log.Error(profile.Name + " is already installed.");
                MessageBox.Show(Language.Text.ModIsAlreadyInstalledMessage.Replace("$NAME", profile.Name), Language.Text.WarningWindowTitle, MessageBoxType.Warning);
                HelperMethods.DeleteDirectory(modsDir + "/" + extractedName);
                return;
            }

            log.Info(profile.Name + " successfully installed.");
            MessageBox.Show(Language.Text.ModSuccessfullyInstalledMessage.Replace("$NAME", profile.Name), Language.Text.SuccessWindowTitle);

            LoadProfilesAndAdjustLists();
            // Adjust profileIndex to point to newly added mod. if its not found for whatever reason, we default to first community updates
            settingsProfileDropDown.SelectedIndex = profileList.FindIndex(p => p.Name == profile.Name);
            if (settingsProfileDropDown.SelectedIndex == -1)
                settingsProfileDropDown.SelectedIndex = 0;
        }

        /// <summary>
        /// This opens the game files directory for the current profile.
        /// </summary>
        private void ProfilesButtonClickEvent(object sender, EventArgs e)
        {
            if (!IsProfileIndexValid())
                return;
            log.Info("User opened the profile directory for profile " + profileList[settingsProfileDropDown.SelectedIndex].Name +
                     ", which is " + profileList[settingsProfileDropDown.SelectedIndex].SaveLocation);
            CrossPlatformOperations.OpenFolder(CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profileList[settingsProfileDropDown.SelectedIndex].Name);
        }

        /// <summary>
        /// This opens the save directory for the current profile.
        /// </summary>
        private void SaveButtonClickEvent(object sender, EventArgs e)
        {
            if (!IsProfileIndexValid())
                return;
            log.Info("User opened the save directory for profile " + profileList[settingsProfileDropDown.SelectedIndex].Name + ", which is " + profileList[settingsProfileDropDown.SelectedIndex].SaveLocation);
            CrossPlatformOperations.OpenFolder(profileList[settingsProfileDropDown.SelectedIndex].SaveLocation);
        }

        /// <summary>
        /// Enabled / disables <see cref="updateModButton"/> and <see cref="deleteModButton"/> accordingly.
        /// </summary>
        private void SettingsProfileDropDownSelectedIndexChanged(object sender, EventArgs e)
        {
            if (settingsProfileDropDown.SelectedIndex == -1 && settingsProfileDropDown.Items.Count == 0) return;

            log.Info("SettingsProfileDropDown.SelectedIndex has been changed to " + settingsProfileDropDown.SelectedIndex + ".");
            if (settingsProfileDropDown.SelectedIndex <= 0 || settingsProfileDropDown.Items.Count == 0)
            {
                deleteModButton.Enabled = false;
                deleteModButton.ToolTip = null;
                updateModButton.Enabled = false;
                updateModButton.ToolTip = null;
                profileNotesTextArea.TextColor = colInactive;
            }
            else
            {
                deleteModButton.Enabled = true;
                deleteModButton.ToolTip = Language.Text.DeleteModButtonToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
                // On non-installable profiles we want to disable updating
                updateModButton.Enabled = profileList[settingsProfileDropDown.SelectedIndex].Installable;
                updateModButton.ToolTip = Language.Text.UpdateModButtonToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
            }

            profileButton.Enabled = Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profileList[settingsProfileDropDown.SelectedIndex].Name);
            profileButton.ToolTip = Language.Text.OpenProfileFolderToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
            saveButton.Enabled = true;
            saveButton.ToolTip = Language.Text.OpenSaveFolderToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);

            if (settingsProfileDropDown.SelectedIndex < 0 || settingsProfileDropDown.Items.Count == 0)
                return;
            profileNotesTextArea.TextColor = colGreen;
            profileNotesTextArea.Text = Language.Text.ProfileNotes + "\n" + profileList[settingsProfileDropDown.SelectedIndex].ProfileNotes;

        }

        /// <summary>
        /// Fires when the profile layout completes loading. This makes sure that if <see cref="settingsProfileDropDown"/> has nothing in it "on boot",
        /// that everything is disabled.
        /// </summary>
        private void ProfileLayoutLoadComplete(object sender, EventArgs e)
        {
            // Safety check
            if ((settingsProfileDropDown == null) || (settingsProfileDropDown.Items.Count != 0)) return;
            addModButton.Enabled = false;
            settingsProfileLabel.TextColor = colInactive;
            settingsProfileDropDown.Enabled = false;
            profileButton.Enabled = false;
            saveButton.Enabled = false;
            updateModButton.Enabled = false;
            deleteModButton.Enabled = false;
            profileNotesTextArea.TextColor = colInactive;
        }

        /// <summary>
        /// The <see cref="MainForm"/> calls this when you're resizing, in order to resize and scale the application accordingly.
        /// </summary>
        private void DrawablePaintEvent(object sender, PaintEventArgs e)
        {
            // Get drawing variables
            float height = drawable.Height;
            float width = drawable.Width;
            float scaleDivisor = OS.IsWindows ? 955f : 715f; // Magic brute-forced values. Don't ask questions, because we don't have answers >_>
                                                                    // Also, seems like nix systems share the same scaleDivisor. Again, don't ask.
            float scale = height / scaleDivisor;

            // Do the actual scaling
            e.Graphics.ScaleTransform(scale);

            // Draw the image, change x offset with some absurd wizardry written at 5 AM
            e.Graphics.DrawImage(formBG, ((width / 2) - (height / 1.4745f)) / scale, 0);
        }

        #region ICON EVENTS

        /// <summary>Gets called when <see cref="redditIcon"/> gets clicked.</summary>
        private void RedditIconOnClick(object sender, EventArgs e) { CrossPlatformOperations.OpenURL("https://www.reddit.com/r/AM2R"); }
        /// <summary>Gets called when <see cref="githubIcon"/> gets clicked.</summary>
        private void GithubIconOnClick(object sender, EventArgs e) { CrossPlatformOperations.OpenURL("https://www.github.com/AM2R-Community-Developers"); }
        /// <summary>Gets called when <see cref="youtubeIcon"/> gets clicked.</summary>
        private void YoutubeIconOnClick(object sender, EventArgs e) { CrossPlatformOperations.OpenURL("https://www.youtube.com/c/AM2RCommunityUpdates"); }
        /// <summary>Gets called when <see cref="discordIcon"/> gets clicked.</summary>
        private void DiscordIconOnClick(object sender, EventArgs e) { CrossPlatformOperations.OpenURL("https://discord.gg/nk7UYPbd5u"); }

        #endregion

        /// <summary>Gets called when <see cref="showButton"/> gets clicked and shows the <see cref="MainForm"/> and brings it to the front again.</summary>
        private void ShowButtonClick(object sender, EventArgs e)
        {
            log.Info("User has opened the launcher from system tray.");

            this.Show();
            this.BringToFront();
        }

        /// <summary>Gets called when <see cref="hqMusicPCCheck"/> gets clicked and writes its new value to the config.</summary>
        private void HqMusicPCCheckChanged(object sender, EventArgs e)
        {
            log.Info("PC HQ Music option has been changed to " + hqMusicPCCheck.Checked);
            CrossPlatformOperations.WriteToConfig("MusicHQPC", hqMusicPCCheck.Checked);
        }

        /// <summary>Gets called when <see cref="hqMusicAndroidCheck"/> gets clicked and writes its new value to the config.</summary>
        private void HqMusicAndroidCheckChanged(object sender, EventArgs e)
        {
            log.Info("Android HQ Music option has been changed to " + hqMusicAndroidCheck.Checked);
            CrossPlatformOperations.WriteToConfig("MusicHQAndroid", hqMusicAndroidCheck.Checked);
        }

        /// <summary>Gets called when user selects a different item from <see cref="profileDropDown"/> and changes <see cref="profileAuthorLabel"/> accordingly.</summary>
        private void ProfileDropDownSelectedIndexChanged(object sender, EventArgs e)
        {
            if (profileDropDown.SelectedIndex == -1 && profileDropDown.Items.Count == 0) return;

            profileIndex = profileDropDown.SelectedIndex;
            log.Debug("profileDropDown.SelectedIndex has been changed to " + profileIndex + ".");

            profileAuthorLabel.Text = Language.Text.Author + " " + profileList[profileDropDown.SelectedIndex].Author;
            profileVersionLabel.Text = Language.Text.VersionLabel + " " + profileList[profileDropDown.SelectedIndex].Version;

            if (profileDropDown.SelectedIndex != 0 && (profileList[profileDropDown.SelectedIndex].SaveLocation == "%localappdata%/AM2R" ||
                                                       profileList[profileDropDown.SelectedIndex].SaveLocation == "default"))
                saveWarningLabel.Visible = true;
            else
                saveWarningLabel.Visible = false;

            UpdateStateMachine();
        }

        /// <summary>Gets called when user selects a different item from <see cref="languageDropDown"/> and writes that to the config.</summary>
        private void LanguageDropDownSelectedIndexChanged(object sender, EventArgs e)
        {
            log.Info("languageDropDown.SelectedIndex has been changed to " + languageDropDown.SelectedIndex + ".");
            CrossPlatformOperations.WriteToConfig("Language", languageDropDown.SelectedIndex == 0 ? "Default" : languageDropDown.Items[languageDropDown.SelectedIndex].Text);
        }

        /// <summary>Gets called when <see cref="autoUpdateAM2RCheck"/> gets clicked and writes its new value to the config.</summary>
        private void AutoUpdateAM2RCheckChanged(object sender, EventArgs e)
        {
            log.Info("Auto Update AM2R has been set to " + autoUpdateAM2RCheck.Checked + ".");
            CrossPlatformOperations.WriteToConfig("AutoUpdateAM2R", (bool)autoUpdateAM2RCheck.Checked);
        }

        /// <summary>Gets called when <see cref="autoUpdateLauncherCheck"/> gets clicked and writes its new value to the config.</summary>
        private void AutoUpdateLauncherCheckChanged(object sender, EventArgs e)
        {
            log.Info("Auto Update Launcher has been set to " + autoUpdateAM2RCheck.Checked + ".");
            CrossPlatformOperations.WriteToConfig("AutoUpdateLauncher", (bool)autoUpdateAM2RCheck.Checked);
        }

        /// <summary>Gets called when <see cref="customMirrorCheck"/> gets clicked, displays a warning <see cref="MessageBox"/>
        /// and enables <see cref="customMirrorTextBox"/> accordingly.</summary>
        private void CustomMirrorCheckChanged(object sender, EventArgs e)
        {
            log.Info("Use Custom Mirror option has been set to " + customMirrorCheck.Checked + ".");
            CrossPlatformOperations.WriteToConfig("CustomMirrorEnabled", (bool)customMirrorCheck.Checked);

            bool enabled = (bool)customMirrorCheck.Checked;
            customMirrorTextBox.Enabled = enabled;
            mirrorDropDown.Enabled = !enabled;
            // Not sure why the dropdown menu needs this hack, but the textBox does not.
            if (OS.IsWindows)
                mirrorDropDown.TextColor = mirrorDropDown.Enabled ? colGreen : colInactive;
            mirrorLabel.TextColor = !enabled ? colGreen : colInactive;

            // Create warning dialog when enabling
            if (enabled)
            {
                MessageBox.Show(Language.Text.WarningWindowText, Language.Text.WarningWindowTitle, MessageBoxType.Warning);
                currentMirror = customMirrorTextBox.Text;
            }
            else
            {
                // Revert mirror to selected index in mirror dropdown
                currentMirror = mirrorList[mirrorDropDown.SelectedIndex];
            }
        }

        /// <summary>Gets called when user selects a different item from <see cref="mirrorDropDown"/>.
        /// It then writes that to the config, and if <see cref="updateState"/> is not <see cref="UpdateState.Downloading"/>
        /// it also overwrites the upstream URL in .git/config.</summary>
        private void MirrorDropDownSelectedIndexChanged(object sender, EventArgs e)
        {
            currentMirror = mirrorList[mirrorDropDown.SelectedIndex];

            log.Info("Current mirror has been set to " + currentMirror + ".");

            CrossPlatformOperations.WriteToConfig("MirrorIndex", mirrorDropDown.SelectedIndex);

            // Don't overwrite the git config while we download!!!
            if (updateState == UpdateState.Downloading) return;

            log.Info("Overwriting mirror in gitconfig.");

            // Check if the gitConfig exists, if yes regex the gitURL, and replace it with the new current Mirror.
            string gitConfigPath = CrossPlatformOperations.CURRENTPATH + "/PatchData/.git/config";
            if (!File.Exists(gitConfigPath)) return;
            string gitConfig = File.ReadAllText(gitConfigPath);
            Regex gitURLRegex = new Regex("https://.*\\.git");
            Match match = gitURLRegex.Match(gitConfig);
            gitConfig = gitConfig.Replace(match.Value, currentMirror);
            File.WriteAllText(gitConfigPath, gitConfig);
        }

        /// <summary>
        /// Gets called when <see cref="profileDebugLogCheck"/> gets clicked, and writes it's new value to the config.
        /// </summary>
        private void ProfileDebugLogCheckedChanged(object sender, EventArgs e)
        {
            log.Info("Create Game Debug Logs option has been set to " + profileDebugLogCheck.Checked + ".");
            CrossPlatformOperations.WriteToConfig("ProfileDebugLog", profileDebugLogCheck.Checked);
        }

        /// <summary>
        /// If the <see cref="customMirrorTextBox"/> has lost focus, we set its text as the new <see cref="currentMirror"/>.
        /// </summary>
        private void CustomMirrorTextBoxLostFocus(object sender, EventArgs e)
        {
            // Check first, if the text is a valid git repo
            Regex gitURLRegex = new Regex("https://.*\\.git");
            string mirrorText = customMirrorTextBox.Text;
            if (!gitURLRegex.IsMatch(mirrorText))
            {
                log.Info("User used " + mirrorText + " as a custom Mirror, didn't pass git validation test.");
                MessageBox.Show(Language.Text.InvalidGitURL.Replace("$NAME", mirrorText), Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                return;
            }

            currentMirror = mirrorText;
            CrossPlatformOperations.WriteToConfig("CustomMirrorText", currentMirror);

            log.Info("Overwriting mirror in gitconfig.");

            // Check if the gitConfig exists, if yes regex the gitURL, and replace it with the new current Mirror.
            string gitConfigPath = CrossPlatformOperations.CURRENTPATH + "/PatchData/.git/config";
            if (!File.Exists(gitConfigPath)) return;
            string gitConfig = File.ReadAllText(gitConfigPath);
            Match match = gitURLRegex.Match(gitConfig);
            gitConfig = gitConfig.Replace(match.Value, currentMirror);
            File.WriteAllText(gitConfigPath, gitConfig);

            log.Info("Custom Mirror has been set to " + currentMirror + ".");
        }

        /// <summary>
        /// If <see cref="customEnvVarTextBox"/> has lost focus, we write its text to the config.
        /// </summary>
        private void CustomEnvVarTextBoxLostFocus(object sender, EventArgs e)
        {
            log.Info("Custom Environment variables have been set to \"" + customEnvVarTextBox.Text + "\".");
            CrossPlatformOperations.WriteToConfig("CustomEnvVar", customEnvVarTextBox.Text);
        }

        /// <summary>Gets called when <see cref="customMirrorCheck"/> gets loaded.
        /// Enables and changes colors for <see cref="customMirrorTextBox"/> and <see cref="mirrorDropDown"/> accordingly.</summary>
        private void CustomMirrorCheckLoadComplete(object sender, EventArgs e)
        {
            bool enabled = (bool)customMirrorCheck.Checked;
            customMirrorTextBox.Enabled = enabled;
            mirrorDropDown.Enabled = !enabled;
            if (OS.IsWindows)
                mirrorDropDown.TextColor = mirrorDropDown.Enabled ? colGreen : colInactive;
        }

        /// <summary>
        /// Gets called when <see cref="deleteModButton"/> gets clicked. Deletes the current selected <see cref="ProfileXML"/> in <see cref="settingsProfileDropDown"/>.
        /// </summary>
        private void DeleteModButtonClicked(object sender, EventArgs e)
        {
            ProfileXML profile = profileList[settingsProfileDropDown.SelectedIndex];
            log.Info("User is attempting to delete profile " + profile.Name + ".");

            DialogResult result = MessageBox.Show(Language.Text.DeleteModWarning.Replace("$NAME", profile.Name), Language.Text.WarningWindowTitle,
                                                  MessageBoxButtons.OKCancel, MessageBoxType.Warning, MessageBoxDefaultButton.Cancel);

            if (result == DialogResult.Ok)
            {
                log.Info("User did not cancel. Proceeding to delete " + profile);
                DeleteProfileAndAdjustLists(profile);
                log.Info(profile + " has been deleted");
                MessageBox.Show(Language.Text.DeleteModButtonSuccess.Replace("$NAME", profile.Name), Language.Text.SuccessWindowTitle);
            }
            else
            {
                log.Info("User has cancelled profile deletion.");
            }
        }

        /// <summary>
        /// Gets called, when <see cref="updateModButton"/> gets clicked. Opens a window, so the user can select a zip, which will be updated over
        /// the current selected <see cref="ProfileXML"/> in <see cref="settingsProfileDropDown"/>.
        /// </summary>
        private void UpdateModButtonClicked(object sender, EventArgs e)
        {
            log.Info("User requested to update mod. Requesting user input for new mod .zip...");

            bool abort = false;

            ProfileXML currentProfile = profileList[settingsProfileDropDown.SelectedIndex];

            OpenFileDialog fileFinder = new OpenFileDialog
            {
                Directory = new Uri(CrossPlatformOperations.CURRENTPATH),
                MultiSelect = false,
                Title = Language.Text.SelectModFileDialog
            };

            fileFinder.Filters.Add(new FileFilter(Language.Text.ZipArchiveText, ".zip"));

            if (fileFinder.ShowDialog(this) != DialogResult.Ok)
            {
                log.Info("User cancelled the Mod selection.");
                return;
            }

            // Exit if nothing was selected
            if (String.IsNullOrWhiteSpace(fileFinder.FileName))
            {
                log.Info("Nothing was selected, cancelling mod update.");
                LoadProfilesAndAdjustLists();
                return;
            }

            log.Info("User selected \"" + fileFinder.FileName + "\"");

            // If either a directory was selected or the file somehow went missing, cancel
            if (!File.Exists(fileFinder.FileName))
            {
                log.Error("Selected mod .zip file not found! Cancelling mod update.");
                return;
            }

            FileInfo modFile = new FileInfo(fileFinder.FileName);

            string modsDir = new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/Mods").FullName;
            string extractedName = modFile.Name.Replace(".zip", "_new");
            string extractedFolder = modsDir + "/" + extractedName;

            // Extract it and see if it contains a profile.xml. If not, this is invalid

            // If for some reason old files remain, delete them
            if (Directory.Exists(extractedFolder))
                Directory.Delete(extractedFolder, true);

            // Directory doesn't exist -> extract!
            ZipFile.ExtractToDirectory(fileFinder.FileName, extractedFolder);

            // Let's check if profile.xml exists in there! If it doesn't throw an error and cleanup
            if (!File.Exists(extractedFolder + "/profile.xml"))
            {
                log.Error(fileFinder.FileName + " does not contain profile.xml! Cancelling mod update.");
                MessageBox.Show(Language.Text.ModIsInvalidMessage.Replace("$NAME", extractedName), Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                Directory.Delete(extractedFolder, true);
                File.Delete(CrossPlatformOperations.CURRENTPATH + "/Mods/" + modFile.Name);
                return;
            }

            // Check by *name*, if the mod was installed already
            ProfileXML profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(extractedFolder + "/profile.xml"));

            if (profileList.FirstOrDefault(p => p.Name == profile.Name) != null || Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name))
            {
                // Mod is already installed, so we can update!
                DialogResult updateResult = MessageBox.Show(Language.Text.UpdateModWarning.Replace("$NAME", currentProfile.Name), Language.Text.WarningWindowTitle,
                                                      MessageBoxButtons.OKCancel, MessageBoxType.Warning, MessageBoxDefaultButton.Cancel);

                if (updateResult == DialogResult.Ok)
                {
                    // If the profile isn't installed, don't ask about archiving it
                    if (Profile.IsProfileInstalled(currentProfile))
                    {
                        //TODO: localize
                        DialogResult archiveResult = MessageBox.Show(Language.Text.ArchiveMod.Replace("$NAME", currentProfile.Name + " " + Language.Text.VersionLabel + currentProfile.Version), Language.Text.WarningWindowTitle, MessageBoxButtons.YesNo, MessageBoxType.Warning, MessageBoxDefaultButton.No);

                        // User wants to archive profile
                        if (archiveResult == DialogResult.Yes)
                            ArchiveProfileAndAdjustLists(currentProfile);
                    }
                    // Now we delete the profile
                    DeleteProfileAndAdjustLists(currentProfile);

                    // Rename directory to take the old one's place
                    string originalFolder = modsDir + "/" + extractedName.Replace("_new", "");
                    Directory.Move(extractedFolder, originalFolder);
                }
                else // Cancel the operation!
                {
                    log.Error("User has cancelled mod update!");
                    abort = true;
                }
            }
            else
            {
                // Cancel the operation!
                // Show message to tell user that mod could not be found, install this separately
                log.Error("Mod is not installed! Cancelling mod update.");
                MessageBox.Show(Language.Text.UpdateModButtonWrongMod.Replace("$NAME", currentProfile.Name).Replace("$SELECT", profile.Name),
                                Language.Text.WarningWindowTitle, MessageBoxButtons.OK);
                abort = true;
            }

            if (abort)
            {
                // File cleanup
                HelperMethods.DeleteDirectory(extractedFolder);
                LoadProfilesAndAdjustLists();
                return;
            }

            log.Info("Successfully updated mod profile " + profile.Name + ".");
            MessageBox.Show(Language.Text.ModSuccessfullyInstalledMessage.Replace("$NAME", currentProfile.Name), Language.Text.SuccessWindowTitle);
            UpdateStateMachine();

            LoadProfilesAndAdjustLists();

            settingsProfileDropDown.SelectedIndex = profileList.FindIndex(p => p.Name == currentProfile.Name);
            if (settingsProfileDropDown.SelectedIndex == -1)
                settingsProfileDropDown.SelectedIndex = 0;
        }

        /// <summary>
        /// Gets called when user tries to close <see cref="MainForm"/>. This does a few things:<br/>
        /// 1) Writes the Width, Height and the check if <see cref="MainForm"/> is currently maximized to the Config<br/>
        /// 2) Checks if current <see cref="updateState"/> is <see cref="UpdateState.Downloading"/>. If yes, it creates a Warning to the end user.
        /// </summary>
        private void MainformClosing(object sender, CancelEventArgs e)
        {
            log.Info("Attempting to close MainForm!");

            CrossPlatformOperations.WriteToConfig("Width", ClientSize.Width);
            CrossPlatformOperations.WriteToConfig("Height", ClientSize.Height);
            CrossPlatformOperations.WriteToConfig("IsMaximized", this.WindowState == WindowState.Maximized);
            CrossPlatformOperations.WriteToConfig("ProfileIndex", profileIndex.ToString());

            switch (updateState)
            {
                case UpdateState.Downloading:
                {
                    var result = MessageBox.Show(Language.Text.CloseOnCloningText, Language.Text.WarningWindowTitle, MessageBoxButtons.YesNo,
                                                 MessageBoxType.Warning, MessageBoxDefaultButton.No);
                    if (result == DialogResult.No)
                    {
                        e.Cancel = true;
                    }
                    else
                        isGitProcessGettingCancelled = true;
                    // We don't need to delete any folders here, the cancelled gitClone will do that automatically for us :)
                    break;
                }
                case UpdateState.Installing:
                    MessageBox.Show(Language.Text.CloseOnInstallingText, Language.Text.WarningWindowTitle, MessageBoxButtons.OK, MessageBoxType.Warning);
                    e.Cancel = true;
                    break;
            }

            // This needs to be made invisible, otherwise a tray indicator will be visible (on linux?) that clicking crashes the application
            trayIndicator.Visible = false;

            if (e.Cancel)
                log.Info("Cancelled MainForm closing event during UpdateState." + updateState + ".");
            else
                log.Info("Successfully closed MainForm. Exiting main thread.");
        }

    }
}