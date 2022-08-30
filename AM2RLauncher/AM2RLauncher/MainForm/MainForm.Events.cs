using AM2RLauncherLib;
using AM2RLauncherLib.XML;
using AM2RLauncher.Language;
using Eto.Forms;
using LibGit2Sharp;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AM2RLauncher;
//TODO: comment a bunch of this stuff for readability

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

    #region Misc events

    /// <summary>
    /// Fires when the profile layout completes loading. This makes sure that if <see cref="modSettingsProfileDropDown"/> has nothing in it "on boot",
    /// that everything is disabled.
    /// </summary>
    private void ProfileLayoutLoadComplete(object sender, EventArgs e)
    {
        // Safety check
        if ((modSettingsProfileDropDown == null) || (modSettingsProfileDropDown.Items.Count != 0)) return;
        addModButton.Enabled = false;
        settingsProfileLabel.TextColor = colorInactive;
        modSettingsProfileDropDown.Enabled = false;
        profileButton.Enabled = false;
        saveButton.Enabled = false;
        updateModButton.Enabled = false;
        deleteModButton.Enabled = false;
        profileNotesTextArea.TextColor = colorInactive;
    }

    /// <summary>
    /// The <see cref="MainForm"/> calls this when you're resizing, in order to resize and scale the application accordingly.
    /// </summary>
    private void DrawablePaintEvent(object sender, PaintEventArgs e)
    {
        // Exit if sender is not a Drawable
        Drawable drawable = sender as Drawable;
        if (drawable == null) return;

        // Get drawing variables
        float height = drawable.Height;
        float width = drawable.Width;
        //TODO: apparently winforms is the big outlier here. Works normal on wpf, I have *no* idea why, seems related to our image. issue has been submitted at eto
        float scaleDivisor = OS.IsWindows ? formBG.Width : formBG.Height;

        float scale = height / scaleDivisor;

        // Do the actual scaling
        e.Graphics.ScaleTransform(scale);

        // Draw the image, change x offset with some absurd wizardry written at 5 AM
        e.Graphics.DrawImage(formBG, ((width / 2) - (height / 1.4745f)) / scale, 0);
    }

    /// <summary>
    /// Gets called when user tries to close <see cref="MainForm"/>. This does a few things:<br/>
    /// 1) Writes the Width, Height, the check if <see cref="MainForm"/> is currently maximized and the ProfileIndex to the Config<br/>
    /// 2) Checks if current <see cref="updateState"/> is <see cref="PlayButtonState.Downloading"/>. If yes, it creates a Warning to the end user.
    /// </summary>
    private void MainFormClosing(object sender, CancelEventArgs e)
    {
        log.Info("Attempting to close MainForm!");

        WriteToConfig("Width", ClientSize.Width);
        WriteToConfig("Height", ClientSize.Height);
        WriteToConfig("IsMaximized", WindowState == WindowState.Maximized);
        WriteToConfig("ProfileIndex", profileIndex.ToString());

        switch (updateState)
        {
            // If we're currently still downloading, ask first if user really wants to close and cancel the event if necessary
            case PlayButtonState.Downloading:
            {
                var result = MessageBox.Show(this, Text.CloseOnCloningText, Text.WarningWindowTitle, MessageBoxButtons.YesNo,
                    MessageBoxType.Warning, MessageBoxDefaultButton.No);

                if (result == DialogResult.No)
                    e.Cancel = true;
                else
                    isGitProcessGettingCancelled = true;
                // We don't need to delete any folders here, the cancelled gitClone will do that automatically for us :)
                break;
            }
            // We can't close during installing, so we cancel the event.
            case PlayButtonState.Installing:
            {
                MessageBox.Show(this, Text.CloseOnInstallingText, Text.WarningWindowTitle, MessageBoxButtons.OK, MessageBoxType.Warning);
                e.Cancel = true;
                break;
            }
        }

        if (e.Cancel)
            log.Info($"Cancelled MainForm closing event during UpdateState.{updateState}.");
        else
            log.Info("Successfully closed MainForm. Exiting main thread.");
    }

    /// <summary>Shows the <see cref="MainForm"/> and brings it to the front again.</summary>
    private void ShowButtonClick(object sender, EventArgs e)
    {
        log.Info("User has opened the launcher from system tray.");

        Show();
        BringToFront();
    }

    #endregion

    #region MAIN TAB

    /// <summary>
    /// After the <see cref="playButton"/> has been loaded, git pull if a repo has been cloned already.
    /// </summary>
    private async void PlayButtonLoadComplete(object sender, EventArgs e)
    {
        //Only pull if Patchdata is cloned and user wants it updated
        LoadProfilesAndAdjustLists();
        if (!Profile.IsPatchDataCloned() || !autoUpdateAM2RCheck.Checked.Value)
            return;

        SetPlayButtonState(PlayButtonState.Downloading);
        EnableProgressBarAndLabel();

        // Try to pull
        try
        {
            log.Info("Attempting to pull repository " + currentMirror + "...");
            await Task.Run(() => Profile.PullPatchData(TransferProgressHandlerMethod));
        }
        catch (UserCancelledException ex)
        {
            // TODO: why do we delete patchdata if user cancels pulling?
            log.Info(ex.Message);
            MessageBox.Show(this, Text.CorruptPatchData, Text.ErrorWindowTitle, MessageBoxType.Error);
            HelperMethods.DeleteDirectory(Core.PatchDataPath);
        }
        // This is for any exceptions from libgit
        catch (LibGit2SharpException ex)
        {
            string errMessage = ex.Message.ToLower();
            // Libgit2sharp error messages are always in english!
            // If internet connection suddenly dropped or site not reachable
            if (errMessage.Contains("failed to send request") || errMessage.Contains("connection with the server was terminated") ||
                errMessage.Contains("failed to resolve address"))
            {
                log.Error("Internet connection failed while attempting to pull repository" + currentMirror + "!");
                MessageBox.Show(this, Text.InternetConnectionDrop, Text.WarningWindowTitle, MessageBoxType.Warning);
            }
            // Error message on protected folders. See this for more info: https://docs.microsoft.com/en-us/microsoft-365/security/defender-endpoint/controlled-folders
            /*else if (errMessage.Contains("access is denied")) TODO: implement this
            {
                // Needs localizable text, logging and message box
                // Also, check if this is the right place for it.
                throw new NotImplementedException();
            }*/
            else
            {
                log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                MessageBox.Show(this, ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Text.ErrorWindowTitle, MessageBoxType.Error);
            }
        }
        // This is if somehow any other exception might get thrown as well.
        catch (Exception ex)
        {
            log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
            MessageBox.Show(this, ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Text.ErrorWindowTitle, MessageBoxType.Error);
        }
        // At the end of everything, reset progressBar controls
        finally
        {
            DisableProgressBarAndProgressLabel();
            LoadProfilesAndAdjustLists();
        }

        // Handling for updates - if current version does not match PatchData version, rename folder so that we attempt to install!
        // Also, add a non-installable profile for it so people can access the older version or delete it from the mod manager.
        if ((profileList.Count > 0) && Profile.IsProfileInstalled(profileList[0]))
        {
            ProfileXML installedUpdatesProfile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(Core.ProfilesPath + "/Community Updates (Latest)/profile.xml"));

            if (installedUpdatesProfile.Version != profileList[0].Version)
            {
                log.Info("New game version (" + profileList[0].Version + ") detected! Beginning archival of version " + installedUpdatesProfile.Version + "...");
                Profile.ArchiveProfile(installedUpdatesProfile);
                profileDropDown.SelectedIndex = 0;
                LoadProfilesAndAdjustLists();
            }
        }

        SetPlayButtonState(PlayButtonState.Install);
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
            case PlayButtonState.Download:

                log.Info("Attempting to clone repository " + currentMirror + "...");
                bool successful = true;

                // Update playButton states and progress controls
                SetPlayButtonState(PlayButtonState.Downloading);
                EnableProgressBarAndLabel();

                // Set up progressBar update method
                CloneOptions cloneOptions = new CloneOptions { OnTransferProgress = TransferProgressHandlerMethod };

                // Try to clone
                try
                {
                    // Cleanup invalid PatchData directory if it exists
                    if (Directory.Exists(Core.PatchDataPath))
                    {
                        log.Info("PatchData directory already exists, cleaning up...");
                        HelperMethods.DeleteDirectory(Core.PatchDataPath);
                    }

                    // Separate thread so launcher doesn't get locked
                    await Task.Run(() => Repository.Clone(currentMirror, Core.PatchDataPath, cloneOptions));
                }
                // We deliberately cancelled this, so no error handling
                catch (UserCancelledException)
                {
                    successful = false;
                }
                //TODO: this is currently copy-pasted with the PullPatchData method. put this into a separate method.
                // For any exceptions from libgit
                catch (LibGit2SharpException ex)
                {
                    // Libgit2sharp error messages are always in english!
                    if (ex.Message.ToLower().Contains("failed to send request") || ex.Message.ToLower().Contains("connection with the server was terminated") ||
                        ex.Message.ToLower().Contains("failed to resolve address"))
                    {
                        log.Error("Internet connection dropped while attempting to clone repository" + currentMirror + "!");
                        MessageBox.Show(this, Text.InternetConnectionDrop, Text.WarningWindowTitle, MessageBoxType.Warning);
                    }
                    else
                    {
                        log.Error("LibGit2SharpException: " + ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                        MessageBox.Show(this, ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Text.ErrorWindowTitle, MessageBoxType.Error);
                        if (Directory.Exists(Core.PatchDataPath))
                            HelperMethods.DeleteDirectory(Core.PatchDataPath);
                    }
                    successful = false;
                }
                // This is if somehow any other exception might get thrown as well.
                catch (Exception ex)
                {
                    log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                    MessageBox.Show(this, ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Text.ErrorWindowTitle, MessageBoxType.Error);

                    if (Directory.Exists(CrossPlatformOperations.CurrentPath + " / PatchData"))
                        HelperMethods.DeleteDirectory(Core.PatchDataPath);
                    successful = false;
                }

                log.Info("Repository clone attempt finished " + (successful ? "successfully." : "unsuccessfully."));

                currentGitObject = 0;

                // Reset progressBar after clone is finished
                DisableProgressBarAndProgressLabel();

                // Just need to switch this to anything that isn't an "active" state so SetUpdateState() actually does something
                SetPlayButtonState(PlayButtonState.Install);

                // This needs to be run BEFORE the state check so that the Mod Settings tab doesn't weird out
                LoadProfilesAndAdjustLists();

                // Do a state check
                UpdateStateMachine();

                break;
            #endregion

            #region Downloading

            case PlayButtonState.Downloading:
                DialogResult result = MessageBox.Show(this, Text.CloseOnCloningText, Text.WarningWindowTitle, MessageBoxButtons.YesNo,
                    MessageBoxType.Warning, MessageBoxDefaultButton.No);
                if (result != DialogResult.Yes)
                    return;

                log.Info("User cancelled download!");
                isGitProcessGettingCancelled = true;

                // We don't need to delete any folders here, the cancelled gitClone will do that automatically for us :)
                // But we should probably wait a bit before proceeding, since cleanup can take a while
                Thread.Sleep(1000);
                isGitProcessGettingCancelled = false;
                break;

            #endregion

            #region Select11
            case PlayButtonState.Select11:

                log.Info("Requesting user input for AM2R_11.zip...");

                OpenFileDialog fileFinder = GetSingleZipDialog(Text.Select11FileDialog);
                if (fileFinder.ShowDialog(this) != DialogResult.Ok)
                {
                    log.Info("User cancelled the selection.");
                    return;
                }

                // Default filename is whitespace
                if (String.IsNullOrWhiteSpace(fileFinder.FileName))
                {
                    log.Error("User did not supply valid input. Cancelling import.");
                    return;
                }

                // If either a directory was selected or the file somehow went missing, cancel
                if (!File.Exists(fileFinder.FileName))
                {
                    log.Error("Selected AM2R_11.zip file not found! Cancelling import.");
                    return;
                }

                IsZipAM2R11ReturnCodes errorCode = Profile.CheckIfZipIsAM2R11(fileFinder.FileName);
                if (errorCode != IsZipAM2R11ReturnCodes.Successful)
                {
                    log.Error("User tried to input invalid AM2R_11.zip file (" + errorCode + "). Cancelling import.");
                    MessageBox.Show(this, Text.ZipIsNotAM2R11 + "\n\nError Code: " + errorCode, Text.ErrorWindowTitle, MessageBoxType.Error);
                    return;
                }

                // We check if it exists first, because someone coughDRUIDcough might've copied it into here while on the showDialog
                if (fileFinder.FileName != Core.AM2R11File)
                    File.Copy(fileFinder.FileName, Core.AM2R11File);

                log.Info("AM2R_11.zip successfully imported.");
                UpdateStateMachine();
                break;
            #endregion

            #region Install
            case PlayButtonState.Install:
                EnableProgressBar();
                SetPlayButtonState(PlayButtonState.Installing);

                // Make sure the main interface state machines properly
                UpdateApkState();
                UpdateProfileState();

                // If the file cannot be launched due to anti-virus shenanigans or any other reason, we try catch here
                try
                {
                    // Check if xdelta is installed on unix and exit if not
                    if (OS.IsUnix && !CrossPlatformOperations.CheckIfXdeltaIsInstalled())
                    {
                        MessageBox.Show(this, Text.XdeltaNotFound, Text.WarningWindowTitle, MessageBoxButtons.OK);
                        SetPlayButtonState(PlayButtonState.Install);
                        UpdateStateMachine();
                        log.Error("Xdelta not found. Aborting installing a profile...");
                        return;
                    }
                    Progress<int> progressIndicator = new Progress<int>(UpdateProgressBar);
                    bool useHQMusic = hqMusicPCCheck.Checked.Value;
                    await Task.Run(() => Profile.InstallProfile(profileList[profileIndex.Value], useHQMusic, progressIndicator));
                    // This is just for visuals because the average windows end user will ask why it doesn't go to the end otherwise.
                    if (OS.IsWindows)
                        Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                    MessageBox.Show(this, ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Text.ErrorWindowTitle, MessageBoxType.Error);
                }
                DisableProgressBar();

                // Just need to switch this to anything that isn't an "active" state so SetUpdateState() actually does something
                SetPlayButtonState(PlayButtonState.Play);
                UpdateStateMachine();
                break;
            #endregion

            #region Play
            case PlayButtonState.Play:

                if (!IsProfileIndexValid())
                    return;

                ProfileXML profile = profileList[profileIndex.Value];
                Visible = false;
                SetPlayButtonState(PlayButtonState.Playing);

                // Make sure the main interface state machines properly
                UpdateApkState();
                UpdateProfileState();

                ShowInTaskbar = false;
                trayIndicator.Visible = true;
                WindowState windowStateBeforeLaunching = WindowState;
                Minimize();
                
                bool createDebugLogs = profileDebugLogCheck.Checked.Value;

                try
                {
                    await Task.Run(() => Profile.RunGame(profile, createDebugLogs));
                }
                catch
                {
                    // ignore any errors that occur.
                }

                ShowInTaskbar = true;
                trayIndicator.Visible = false;
                Show();
                BringToFront();
                Visible = true;
                WindowState = windowStateBeforeLaunching;

                SetPlayButtonState(PlayButtonState.Play);
                UpdateStateMachine();
                break;

            #endregion

            default: log.Error("Encountered invalid update state: " + updateState + "!"); break;
        }
    }

    /// <summary>
    /// Does stuff, depending on the current state of <see cref="apkButtonState"/>.
    /// </summary>
    private async void ApkButtonClickEvent(object sender, EventArgs e)
    {
        // If we're currently creating something, exit
        if (apkButtonState == ApkButtonState.Creating) return;

        // Check for java, exit safely with a warning if not found!
        if (!CrossPlatformOperations.IsJavaInstalled())
        {
            MessageBox.Show(this, Text.JavaNotFound, Text.WarningWindowTitle, MessageBoxButtons.OK);
            SetApkButtonState(ApkButtonState.Create);
            UpdateStateMachine();
            log.Error("Java not found! Aborting Android APK creation.");
            return;
        }

        // Check if xdelta is installed on unix, exit with a warning if not.
        if (OS.IsUnix && !CrossPlatformOperations.CheckIfXdeltaIsInstalled())
        {
            MessageBox.Show(this, Text.XdeltaNotFound, Text.WarningWindowTitle, MessageBoxButtons.OK);
            SetApkButtonState(ApkButtonState.Create);
            UpdateStateMachine();
            log.Error("Xdelta not found. Aborting Android APK creation...");
            return;
        }

        UpdateStateMachine();

        if (apkButtonState != ApkButtonState.Create) return;

        SetApkButtonState(ApkButtonState.Creating);
        UpdateStateMachine();

        EnableProgressBar();
        bool useHQMusic = hqMusicAndroidCheck.Checked.Value;

        Progress<int> progressIndicator = new Progress<int>(UpdateProgressBar);
        await Task.Run(() => Profile.CreateAPK(profileList[profileIndex.Value], useHQMusic, progressIndicator));

        SetApkButtonState(ApkButtonState.Create);
        DisableProgressBar();
        UpdateStateMachine();
    }

    /// <summary>Gets called when user selects a different item from <see cref="profileDropDown"/> and changes <see cref="profileAuthorLabel"/> accordingly.</summary>
    private void ProfileDropDownSelectedIndexChanged(object sender, EventArgs e)
    {
        if ((profileDropDown.SelectedIndex == -1) && (profileDropDown.Items.Count == 0)) return;

        profileIndex = profileDropDown.SelectedIndex;
        log.Debug("profileDropDown.SelectedIndex has been changed to " + profileIndex + ".");

        profileAuthorLabel.Text = Text.Author + " " + profileList[profileDropDown.SelectedIndex].Author;
        profileVersionLabel.Text = Text.VersionLabel + " " + profileList[profileDropDown.SelectedIndex].Version;

        if ((profileDropDown.SelectedIndex != 0) && ((profileList[profileDropDown.SelectedIndex].SaveLocation == "%localappdata%/AM2R") ||
                                                     (profileList[profileDropDown.SelectedIndex].SaveLocation == "default")))
            saveWarningLabel.Visible = true;
        else
            saveWarningLabel.Visible = false;

        UpdateStateMachine();
    }

    #endregion

    /// <summary>
    /// If no internet access is available, this changes the content of <paramref name="tabPage"/> to an empty page only displaying <paramref name="errorLabel"/>.
    /// </summary>
    /// <param name="tabPage">The <see cref="TabPage"/> to change the contents of.</param>
    /// <param name="errorLabel">The <see cref="Label"/> that should be displayed.</param>
    private void ChangeToEmptyPageOnNoInternet(TabPage tabPage, Label errorLabel)
    {
        if (isInternetThere)
            return;

        tabPage.Content = new TableLayout
        {
            Rows =
            {
                null,
                errorLabel,
                null
            }
        };
    }

    #region SETTINGS

    /// <summary>Gets called when user selects a different item from <see cref="languageDropDown"/> and writes that to the config.</summary>
    private void LanguageDropDownSelectedIndexChanged(object sender, EventArgs e)
    {
        log.Info("languageDropDown.SelectedIndex has been changed to " + languageDropDown.SelectedIndex + ".");
        WriteToConfig("Language", languageDropDown.SelectedIndex == 0 ? "Default" : languageDropDown.Items[languageDropDown.SelectedIndex].Text);
    }

    /// <summary>Gets called when <see cref="autoUpdateAM2RCheck"/> gets clicked and writes its new value to the config.</summary>
    private void AutoUpdateAM2RCheckChanged(object sender, EventArgs e)
    {
        log.Info("Auto Update AM2R has been set to " + autoUpdateAM2RCheck.Checked + ".");
        WriteToConfig("AutoUpdateAM2R", autoUpdateAM2RCheck.Checked.Value);
    }

    /// <summary>Gets called when <see cref="autoUpdateLauncherCheck"/> gets clicked and writes its new value to the config.</summary>
    private void AutoUpdateLauncherCheckChanged(object sender, EventArgs e)
    {
        log.Info("Auto Update Launcher has been set to " + autoUpdateAM2RCheck.Checked + ".");
        WriteToConfig("AutoUpdateLauncher", autoUpdateAM2RCheck.Checked.Value);
    }

    /// <summary>Gets called when <see cref="hqMusicPCCheck"/> gets clicked and writes its new value to the config.</summary>
    private void HQMusicPCCheckChanged(object sender, EventArgs e)
    {
        log.Info("PC HQ Music option has been changed to " + hqMusicPCCheck.Checked);
        WriteToConfig("MusicHQPC", hqMusicPCCheck.Checked);
    }

    /// <summary>Gets called when <see cref="hqMusicAndroidCheck"/> gets clicked and writes its new value to the config.</summary>
    private void HQMusicAndroidCheckChanged(object sender, EventArgs e)
    {
        log.Info("Android HQ Music option has been changed to " + hqMusicAndroidCheck.Checked);
        WriteToConfig("MusicHQAndroid", hqMusicAndroidCheck.Checked);
    }

    /// <summary>
    /// Gets called when <see cref="profileDebugLogCheck"/> gets clicked, and writes it's new value to the config.
    /// </summary>
    private void ProfileDebugLogCheckedChanged(object sender, EventArgs e)
    {
        log.Info("Create Game Debug Logs option has been set to " + profileDebugLogCheck.Checked + ".");
        WriteToConfig("ProfileDebugLog", profileDebugLogCheck.Checked);
    }

    /// <summary>Gets called when user selects a different item from <see cref="mirrorDropDown"/>.
    /// It then writes that to the config, and if <see cref="updateState"/> is not <see cref="PlayButtonState.Downloading"/>
    /// it also overwrites the upstream URL in .git/config.</summary>
    private void MirrorDropDownSelectedIndexChanged(object sender, EventArgs e)
    {
        currentMirror = mirrorList[mirrorDropDown.SelectedIndex];

        log.Info("Current mirror has been set to " + currentMirror + ".");

        WriteToConfig("MirrorIndex", mirrorDropDown.SelectedIndex);

        // Don't overwrite the git config while we download!!!
        if (updateState == PlayButtonState.Downloading) return;

        log.Info("Overwriting mirror in gitconfig.");

        // Check if the gitConfig exists, if yes regex the gitURL, and replace it with the new current Mirror.
        string gitConfigPath = Core.PatchDataPath + "/.git/config";
        if (!File.Exists(gitConfigPath)) return;

        string gitConfig = File.ReadAllText(gitConfigPath);
        Regex gitURLRegex = new Regex("https://.*\\.git");
        Match match = gitURLRegex.Match(gitConfig);
        gitConfig = gitConfig.Replace(match.Value, currentMirror);
        File.WriteAllText(gitConfigPath, gitConfig);
    }

    /// <summary>Gets called when <see cref="customMirrorCheck"/> gets clicked, displays a warning <see cref="MessageBox"/>
    /// and enables <see cref="customMirrorTextBox"/> accordingly.</summary>
    private void CustomMirrorCheckChanged(object sender, EventArgs e)
    {
        log.Info("Use Custom Mirror option has been set to " + customMirrorCheck.Checked + ".");
        WriteToConfig("CustomMirrorEnabled", customMirrorCheck.Checked.Value);

        EnableMirrorControlsAccordingly();

        // Create warning dialog when enabling
        if (customMirrorCheck.Checked.Value)
        {
            MessageBox.Show(this, Text.WarningWindowText, Text.WarningWindowTitle, MessageBoxType.Warning);
            currentMirror = customMirrorTextBox.Text;
        }
        else
        {
            // Revert mirror to selected index in mirror dropdown
            currentMirror = mirrorList[mirrorDropDown.SelectedIndex];
        }
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
            MessageBox.Show(this, HelperMethods.GetText(Text.InvalidGitURL, mirrorText), Text.ErrorWindowTitle, MessageBoxType.Error);
            return;
        }

        currentMirror = mirrorText;
        WriteToConfig("CustomMirrorText", currentMirror);

        log.Info("Overwriting mirror in gitconfig.");

        // Check if the gitConfig exists, if yes regex the gitURL, and replace it with the new current Mirror.
        string gitConfigPath = Core.PatchDataPath + "/.git/config";
        if (!File.Exists(gitConfigPath)) return;
        string gitConfig = File.ReadAllText(gitConfigPath);
        Match match = gitURLRegex.Match(gitConfig);
        gitConfig = gitConfig.Replace(match.Value, currentMirror);
        File.WriteAllText(gitConfigPath, gitConfig);

        log.Info("Custom Mirror has been set to " + currentMirror + ".");
    }
    
    #endregion

    #region MOD SETTINGS

    /// <summary>
    /// Runs when <see cref="addModButton"/> is clicked. Brings up a file select to select a mod, and adds that to the mod directory.
    /// </summary>
    private void AddModButtonClicked(object sender, EventArgs e)
    {
        log.Info("User requested to add mod. Requesting user input for new mod .zip...");

        OpenFileDialog fileFinder = GetSingleZipDialog(Text.SelectModFileDialog);

        // If user didn't press ok, cancel
        if (fileFinder.ShowDialog(this) != DialogResult.Ok)
        {
            log.Info("User cancelled the Mod selection.");
            return;
        }

        log.Info("User selected \"" + fileFinder.FileName + "\"");

        // If either a directory was selected, user pressed OK without selecting anything or the file somehow went missing, cancel
        if (!File.Exists(fileFinder.FileName))
        {
            log.Error("Selected mod .zip file not found! Cancelling import.");
            return;
        }

        //TODO: move most of this into AM2RLauncher.Profile?

        FileInfo modFile = new FileInfo(fileFinder.FileName);
        string modFileName = Path.GetFileNameWithoutExtension(modFile.Name);
        string extractedModDir = Core.ModsPath + "/" + modFileName;

        // Check first, if the directory is already there, if yes, throw error
        if (Directory.Exists(extractedModDir))
        {
            string existingProfileName = Serializer.Deserialize<ProfileXML>(File.ReadAllText(extractedModDir + "/profile.xml")).Name;
            log.Error("Mod is already imported as " + modFileName + "! Cancelling mod import.");
            MessageBox.Show(this, HelperMethods.GetText(Text.ModIsAlreadyInstalledMessage, existingProfileName), Text.WarningWindowTitle, MessageBoxType.Warning);
            return;
        }

        // Directory doesn't exist -> extract!
        ZipFile.ExtractToDirectory(modFile.FullName, extractedModDir);
        log.Info("Imported and extracted mod .zip as " + modFileName);

        // If profile.xml doesn't exist, throw an error and cleanup
        if (!File.Exists(extractedModDir + "/profile.xml"))
        {
            log.Error(modFile.Name + " does not contain profile.xml! Cancelling mod import.");
            MessageBox.Show(this, HelperMethods.GetText(Text.ModIsInvalidMessage, modFileName), Text.ErrorWindowTitle, MessageBoxType.Error);
            Directory.Delete(extractedModDir, true);
            return;
        }

        ProfileXML profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(extractedModDir + "/profile.xml"));

        // If OS versions mismatch, throw error and cleanup
        if (OS.Name != profile.OperatingSystem)
        {
            log.Error("Mod is for " + profile.OperatingSystem + " while current OS is " + OS.Name + ". Cancelling mod import.");
            MessageBox.Show(this, HelperMethods.GetText(Text.ModIsForWrongOS, profile.Name).Replace("$OS", profile.OperatingSystem).Replace("$CURRENTOS", OS.Name),
                Text.ErrorWindowTitle, MessageBoxType.Error);
            HelperMethods.DeleteDirectory(extractedModDir);
            return;
        }

        // If mod was installed/added by *name* already, throw error and cleanup
        if (profileList.FirstOrDefault(p => p.Name == profile.Name) != null)
        {
            log.Error(profile.Name + " is already installed.");
            MessageBox.Show(this, HelperMethods.GetText(Text.ModIsAlreadyInstalledMessage, profile.Name), Text.WarningWindowTitle, MessageBoxType.Warning);
            HelperMethods.DeleteDirectory(extractedModDir);
            return;
        }

        // Reload list so mod gets recognized
        LoadProfilesAndAdjustLists();
        // Adjust profileIndex to point to newly added mod. if its not found for whatever reason, we default to first community updates
        modSettingsProfileDropDown.SelectedIndex = profileList.FindIndex(p => p.Name == profile.Name);
        if (modSettingsProfileDropDown.SelectedIndex == -1)
            modSettingsProfileDropDown.SelectedIndex = 0;

        log.Info(profile.Name + " successfully added.");
        MessageBox.Show(this, HelperMethods.GetText(Text.ModSuccessfullyInstalledMessage, profile.Name), Text.SuccessWindowTitle);
    }

    /// <summary>
    /// Enabled / disables <see cref="updateModButton"/> and <see cref="deleteModButton"/> accordingly.
    /// </summary>
    private void ModSettingsProfileDropDownSelectedIndexChanged(object sender, EventArgs e)
    {
        if (modSettingsProfileDropDown.SelectedIndex == -1 && modSettingsProfileDropDown.Items.Count == 0) return;

        string profileName = modSettingsProfileDropDown.Items[modSettingsProfileDropDown.SelectedIndex].Text;

        log.Info("SettingsProfileDropDown.SelectedIndex has been changed to " + modSettingsProfileDropDown.SelectedIndex + ".");
        if (modSettingsProfileDropDown.SelectedIndex <= 0 || modSettingsProfileDropDown.Items.Count == 0)
        {
            deleteModButton.Enabled = false;
            deleteModButton.ToolTip = null;
            updateModButton.Enabled = false;
            updateModButton.ToolTip = null;
            profileNotesTextArea.TextColor = colorInactive;
        }
        else
        {
            desktopShortcutButton.Enabled = true;
            deleteModButton.Enabled = true;
            deleteModButton.ToolTip = HelperMethods.GetText(Text.DeleteModButtonToolTip, profileName);
            // On non-installable profiles we want to disable updating
            updateModButton.Enabled = profileList[modSettingsProfileDropDown.SelectedIndex].Installable;
            updateModButton.ToolTip = HelperMethods.GetText(Text.UpdateModButtonToolTip, profileName);
        }

        desktopShortcutButton.Enabled = Directory.Exists(Core.ProfilesPath + "/" + profileName);
        profileButton.Enabled = Directory.Exists(Core.ProfilesPath + "/" + profileName);
        profileButton.ToolTip = HelperMethods.GetText(Text.OpenProfileFolderToolTip, profileName);
        saveButton.Enabled = true;
        saveButton.ToolTip = HelperMethods.GetText(Text.OpenSaveFolderToolTip, profileName);
        profileNotesTextArea.TextColor = colorGreen;
        profileNotesTextArea.Text = Text.ProfileNotes + "\n" + profileList[modSettingsProfileDropDown.SelectedIndex].ProfileNotes;
    }

    /// <summary>
    /// Creates a shortcut of the selected profile on the Desktop
    /// </summary>
    private void DesktopShortcutButtonClicked(object sender, EventArgs e)
    {
        ProfileXML profile = profileList[modSettingsProfileDropDown.SelectedIndex];
        log.Info($"User wants to create a desktop shortcut for {profile.Name}.");
        
        // We want to give a warning to users, so they don't complain with "why didn't I get 2.0???"
        if (profile.Name == "Community Updates (Latest)")
        {
            Application.Instance.Invoke(() =>
            {
                MessageBox.Show(this, Text.ShortcutWarning, Text.WarningWindowTitle, MessageBoxType.Warning);
            });
        }
        
        string desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop, Environment.SpecialFolderOption.Create);
        string shortcutFile = "";

        try 
        { 
            if (OS.IsWindows)
            {
                //TODO: implement this
            }
            else if (OS.IsLinux)
            {
                shortcutFile = $"{desktopFolder}/{profile.Name}.desktop";
                
                const string desktopEntryTemplate =
                    "[Desktop Entry]\n" +
                    "Type=Application\n" +
                    "Categories=Game\n" +
                    "Encoding=UTF-8\n" +
                    "Name=PROFILENAME\n" +
                    "Comment=PROFILEDESCRIPTION\n" +
                    "Exec=EXECUTABLE\n" +
                    "Icon=ICONPATH\n" +
                    "Terminal=false";

                string desktopEntryText = desktopEntryTemplate;
                
                // Replace values
                desktopEntryText = desktopEntryText.Replace("PROFILENAME", $"{profile.Name}");
                desktopEntryText = desktopEntryText.Replace("PROFILEDESCRIPTION", $"A shortcut for {profile.Name}.");
                desktopEntryText = desktopEntryText.Replace("ICONPATH", $"{Core.PatchDataPath}/data/files_to_copy/icon.png");

                string gameName;
                #if !NOAPPIMAGE
                gameName = "runner";
                #else
                gameName = "AM2R.AppImage";
                #endif
                if (OS.IsThisRunningFromFlatpak)
                    desktopEntryText = desktopEntryText.Replace("EXECUTABLE", $"flatpak run \"--command={Core.ProfilesPath}/{profile.Name}/{gameName}\" io.github.am2r_community_developers.AM2RLauncher");
                else
                    desktopEntryText = desktopEntryText.Replace("EXECUTABLE", $"{Core.ProfilesPath}/{profile.Name}/{gameName}");
                
                File.WriteAllText(shortcutFile, desktopEntryText);
            }
            else if (OS.IsMac)
            {
                throw new NotImplementedException("Creating Desktop Shortcuts on Mac has currently not been implemented!");
            }
            else
            {
                log.Error($"{OS.Name} has no way of creating shortcuts");
                return;
            }
            
            CrossPlatformOperations.OpenFolderAndSelectFile(shortcutFile);
        }
        // We only care about io exceptions (file not readable, drive not available etc.) The rest should throw normally
        catch (IOException exception)
        {
            Application.Instance.Invoke(() =>
            {
                MessageBox.Show(this, exception.Message, Text.UnhandledException, MessageBoxType.Error);
            });
        }
    }
    
    /// <summary>
    /// This opens the game files directory for the current profile.
    /// </summary>
    private void ProfileDataButtonClickEvent(object sender, EventArgs e)
    {
        if (!IsProfileIndexValid())
            return;
        ProfileXML profile = profileList[modSettingsProfileDropDown.SelectedIndex];
        log.Info("User opened the profile directory for profile " + profile.Name + ", which is " + profile.SaveLocation);
        CrossPlatformOperations.OpenFolder(Core.ProfilesPath + "/" + profile.Name);
    }

    /// <summary>
    /// This opens the save directory for the current profile.
    /// </summary>
    private void SaveButtonClickEvent(object sender, EventArgs e)
    {
        if (!IsProfileIndexValid())
            return;
        ProfileXML profile = profileList[modSettingsProfileDropDown.SelectedIndex];
        log.Info("User opened the save directory for profile " + profile.Name + ", which is " + profile.SaveLocation);
        CrossPlatformOperations.OpenFolder(profile.SaveLocation);
    }

    /// <summary>
    /// Gets called when <see cref="deleteModButton"/> gets clicked. Deletes the current selected <see cref="ProfileXML"/> in <see cref="modSettingsProfileDropDown"/>.
    /// </summary>
    private void DeleteModButtonClicked(object sender, EventArgs e)
    {
        ProfileXML profile = profileList[modSettingsProfileDropDown.SelectedIndex];
        log.Info("User is attempting to delete profile " + profile.Name + ".");

        DialogResult result = MessageBox.Show(this, HelperMethods.GetText(Text.DeleteModWarning, profile.Name), Text.WarningWindowTitle,
            MessageBoxButtons.OKCancel, MessageBoxType.Warning, MessageBoxDefaultButton.Cancel);

        // if user didn't press ok, cancel
        if (result != DialogResult.Ok)
        {
            log.Info("User has cancelled profile deletion.");
            return;
        }

        log.Info("User did not cancel. Proceeding to delete " + profile);
        DeleteProfileAndAdjustLists(profile);
        log.Info(profile + " has been deleted");
        MessageBox.Show(this, HelperMethods.GetText(Text.DeleteModButtonSuccess, profile.Name), Text.SuccessWindowTitle);
    }

    /// <summary>
    /// Gets called, when <see cref="updateModButton"/> gets clicked. Opens a window, so the user can select a zip, which will be updated over
    /// the current selected <see cref="ProfileXML"/> in <see cref="modSettingsProfileDropDown"/>.
    /// </summary>
    private void UpdateModButtonClicked(object sender, EventArgs e)
    {
        log.Info("User requested to update mod. Requesting user input for new mod .zip...");

        ProfileXML currentProfile = profileList[modSettingsProfileDropDown.SelectedIndex];
        OpenFileDialog fileFinder = GetSingleZipDialog(Text.SelectModFileDialog);

        // If user didn't click OK, cancel
        if (fileFinder.ShowDialog(this) != DialogResult.Ok)
        {
            log.Info("User cancelled the Mod selection.");
            return;
        }

        log.Info("User selected \"" + fileFinder.FileName + "\"");

        // If either a directory was selected, no file was selected or the file somehow went missing, cancel
        if (!File.Exists(fileFinder.FileName))
        {
            log.Error("Selected mod .zip file not found! Cancelling mod update.");
            return;
        }

        //TODO: move most of this into AM2RLauncher.Profile?

        FileInfo modFile = new FileInfo(fileFinder.FileName);
        string extractedName = Path.GetFileNameWithoutExtension(modFile.Name) + "_new";
        string extractedModDir = Core.ModsPath + "/" + extractedName;

        // If for some reason old files remain, delete them so that extraction doesn't throw
        if (Directory.Exists(extractedModDir))
            Directory.Delete(extractedModDir, true);

        // Directory doesn't exist -> extract!
        ZipFile.ExtractToDirectory(fileFinder.FileName, extractedModDir);

        // If mod doesn't have a profile.xml, throw an error and cleanup
        if (!File.Exists(extractedModDir + "/profile.xml"))
        {
            log.Error(fileFinder.FileName + " does not contain profile.xml! Cancelling mod update.");
            MessageBox.Show(this, HelperMethods.GetText(Text.ModIsInvalidMessage, extractedName), Text.ErrorWindowTitle, MessageBoxType.Error);
            Directory.Delete(extractedModDir, true);
            return;
        }

        // Check by *name*, if the mod was installed already
        ProfileXML profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(extractedModDir + "/profile.xml"));

        // If the selected mod is not installed, tell user that they should add it and cleanup
        if (profileList.FirstOrDefault(p => p.Name == profile.Name) == null)
        {
            log.Error("Mod is not installed! Cancelling mod update.");
            MessageBox.Show(this, HelperMethods.GetText(Text.UpdateModButtonWrongMod, currentProfile.Name).Replace("$SELECT", profile.Name),
                Text.WarningWindowTitle, MessageBoxButtons.OK);
            HelperMethods.DeleteDirectory(extractedModDir);
            return;
        }

        // If user doesn't want to update, cleanup
        DialogResult updateResult = MessageBox.Show(this, HelperMethods.GetText(Text.UpdateModWarning, currentProfile.Name), Text.WarningWindowTitle,
            MessageBoxButtons.OKCancel, MessageBoxType.Warning, MessageBoxDefaultButton.Cancel);
        if (updateResult != DialogResult.Ok)
        {
            log.Error("User has cancelled mod update!");
            HelperMethods.DeleteDirectory(extractedModDir);
            return;
        }

        // If the profile isn't installed, don't ask about archiving it
        if (Profile.IsProfileInstalled(currentProfile))
        {
            DialogResult archiveResult = MessageBox.Show(this, HelperMethods.GetText(Text.ArchiveMod, currentProfile.Name + " " + Text.VersionLabel + currentProfile.Version), Text.WarningWindowTitle, MessageBoxButtons.YesNo, MessageBoxType.Warning, MessageBoxDefaultButton.No);

            // User wants to archive profile
            if (archiveResult == DialogResult.Yes)
                ArchiveProfileAndAdjustLists(currentProfile);
        }

        DeleteProfileAndAdjustLists(currentProfile);

        // Rename directory to take the old one's place
        string originalFolder = Core.ModsPath + "/" + Path.GetFileNameWithoutExtension(modFile.Name);
        Directory.Move(extractedModDir, originalFolder);

        // Adjust our lists so it gets recognized
        LoadProfilesAndAdjustLists();

        modSettingsProfileDropDown.SelectedIndex = profileList.FindIndex(p => p.Name == currentProfile.Name);
        if (modSettingsProfileDropDown.SelectedIndex == -1)
            modSettingsProfileDropDown.SelectedIndex = 0;

        log.Info("Successfully updated mod profile " + profile.Name + ".");
        MessageBox.Show(this, HelperMethods.GetText(Text.ModSuccessfullyInstalledMessage, currentProfile.Name), Text.SuccessWindowTitle);
    }

    #endregion
}