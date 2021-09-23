using Eto;
using Eto.Drawing;
using Eto.Forms;
using System;
using System.Collections.Generic;
using System.Text;
using Pablo.Controls;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using System.IO;
using System.Runtime.InteropServices;
using LibGit2Sharp.Handlers;
using System.Text.RegularExpressions;
using System.Threading;
using System.ComponentModel;
using System.Net;
using System.IO.Compression;

using AM2RLauncher.XML;
using AM2RLauncher.Helpers;

namespace AM2RLauncher
{
    public partial class MainForm : Form
    {
        /// <summary>This is used for <see cref="TranferProgressHandlerMethod"/> to get the current Git Object during cloning.</summary>
        private static int currentGitObject = 0;

        /// <summary>
        /// This is a static variable, that <see cref="MainForm.TransferProgressHandlerMethod(TransferProgress)"/> uses, to check if it should cancel the current git process.
        /// </summary>
        private static bool isGitProcessGettingCancelled = false;

        /// <summary>
        /// This is used on Windows only. This sets a window to be in foreground, is used i.e. to fix am2r just being hidden.
        /// </summary>
        /// <param name="hWnd">Pointer to the process you want to have in the foreground.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// After the <see cref="playButton"/> has bee loaded, git pull if a repo has been cloned already.
        /// </summary>
        private async void PlayButtonLoadComplete(object sender, EventArgs e)
        {
            LoadProfiles();
            if (IsPatchDataCloned() && (bool)autoUpdateAM2RCheck.Checked)
            {
                SetPlayButtonState(UpdateState.Downloading);

                progressBar.Visible = true;
                progressLabel.Visible = true;
                progressBar.Value = 0;

                // Try to pull first.
                try
                {
                    await Task.Run(() => PullPatchData());

                    // thank you druid, for this case that should never happen
                    if (!File.Exists(CrossPlatformOperations.CURRENTPATH + "/PatchData/profile.xml"))
                    {
                        log.Error("Druid PatchData corruption occurred!");
                        MessageBox.Show(Language.Text.CorruptPatchData, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                        HelperMethods.DeleteDirectory(CrossPlatformOperations.CURRENTPATH + "/PatchData");
                        return;
                    }
                }
                catch (UserCancelledException) { }   //we deliberately cancelled this!
                catch (LibGit2SharpException ex)    //this is for any exceptions from libgit
                {
                    //libgit2sharp error messages are always in english!
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
                catch (Exception ex)             //this is if somehow any other exception might get thrown as well.
                {
                    log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                    MessageBox.Show(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                }
                finally
                {
                    progressBar.Visible = false;
                    progressLabel.Visible = false;
                    LoadProfiles();
                }

                // Handling for updates - if current version does not match PatchData version, rename folder so that we attempt to install!
                // Also, add a non-installable profile for it so people can access the older version or delete it from the mod manager.
                if (IsProfileInstalled(profileList[0]))
                {
                    ProfileXML currentXML = Serializer.Deserialize<ProfileXML>(File.ReadAllText(CrossPlatformOperations.CURRENTPATH + "/Profiles/Community Updates (Latest)/profile.xml"));

                    if (currentXML.Version != profileList[0].Version)
                    {
                        log.Info("New game version (" + profileList[0].Version + ") detected! Beginning archival of version " + currentXML.Version + "...");

                        string profileArchivePath = CrossPlatformOperations.CURRENTPATH + "/Profiles/Community Updates (" + currentXML.Version + ")";

                        // Do NOT overwrite if a path with this name already exists! It is likely an existing user archive.
                        if (!Directory.Exists(profileArchivePath))
                        {
                            // Rename current Community Updates
                            Directory.Move(CrossPlatformOperations.CURRENTPATH + "/Profiles/Community Updates (Latest)", profileArchivePath);

                            currentXML.Name = "Community Updates (" + currentXML.Version + ")";

                            // Set as non-installable so that it's just treated as a launching reference
                            currentXML.Installable = false;
                            currentXML.SupportsAndroid = false;

                            string modArchivePath = CrossPlatformOperations.CURRENTPATH + "/Mods/" + currentXML.Name;

                            // Do NOT overwrite if a path with this name already exists! It is likely an existing user archive.
                            if (!Directory.Exists(modArchivePath))
                            {
                                Directory.CreateDirectory(modArchivePath);
                                File.WriteAllText(modArchivePath + "/profile.xml", Serializer.Serialize<ProfileXML>(currentXML));
                                log.Info("Finished archival.");
                            }
                            else
                            {
                                HelperMethods.DeleteDirectory(profileArchivePath);
                                log.Info("Cancelling archival! User-defined archive in Mods already exists.");
                            }

                            
                        }
                        else // If our desired rename already exists, it's probably a user archive... so we just delete the folder and move on with installation of the new version.
                        {
                            HelperMethods.DeleteDirectory(CrossPlatformOperations.CURRENTPATH + "/Profiles/Community Updates (Latest)");
                            log.Info("Cancelling archival! User-defined archive in Profiles already exists.");
                        }

                        profileDropDown.SelectedIndex = 0;

                        LoadProfiles();

                        
                    }
                }

                SetPlayButtonState(UpdateState.Install);
                UpdateStateMachine();
            }
        }

        /// <summary>
        /// Does a bunch of stuff, depending on the current state of <see cref="updateState"/>.
        /// </summary>
        private async void PlayButtonClickEvent(object sender, EventArgs e)
        {
            // State Check
            UpdateStateMachine();

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
                        OnTransferProgress = new TransferProgressHandler(TransferProgressHandlerMethod)
                    };

                    // Everything after this is basically on a different thread, so the rest of the launcher isn't locked up.
                    try
                    {
                        if (Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/PatchData"))
                            HelperMethods.DeleteDirectory(CrossPlatformOperations.CURRENTPATH + "/PatchData");

                        await Task.Run(() => Repository.Clone(currentMirror, CrossPlatformOperations.CURRENTPATH + "/PatchData", c));
                    }
                    catch (UserCancelledException)
                    {
                        successful = false;
                    }   //we deliberately cancelled this!
                    catch (LibGit2SharpException ex)    //this is for any exceptions from libgit
                    {
                        //libgit2sharp error messages are always in english!
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
                    catch(Exception ex)             //this is if somehow any other exception might get thrown as well.
                    {
                        log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                        MessageBox.Show(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Language.Text.ErrorWindowTitle, MessageBoxType.Error);

                        if(Directory.Exists(CrossPlatformOperations.CURRENTPATH + " / PatchData"))
                            HelperMethods.DeleteDirectory(CrossPlatformOperations.CURRENTPATH + "/PatchData");
                        successful = false;
                    }

                    log.Info("Repository clone attempt finished " + (successful ? "successfully." : "unsuccesfully."));

                    currentGitObject = 0;

                    // Reset progressBar after clone is finished
                    progressLabel.Visible = false;
                    progressLabel.Text = "";
                    progressBar.Visible = false;
                    progressBar.Value = 0;

                    // Just need to switch this to anything that isn't an "active" state so SetUpdateState() actually does something
                    SetPlayButtonState(UpdateState.Install);

                    // This needs to be run BEFORE the state check so that the Mod Settings tab doesn't weird out
                    LoadProfiles();

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
                    // but we should probably wait a bit before proceeding, since cleanup can take a while
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

                    fileFinder.Filters.Add(new FileFilter(Language.Text.ZipArchiveText, new[] { ".zip" }));

                    fileFinder.ShowDialog(this);

                    if (!string.IsNullOrWhiteSpace(fileFinder.FileName)) // This is default
                    {
                        if (Directory.Exists(fileFinder.FileName)) return; // this can happen on linux, and maybe windows as well

                        IsZipAM2R11ReturnCodes errorCode = CheckIfZipIsAM2R11(fileFinder.FileName);
                        if (errorCode != IsZipAM2R11ReturnCodes.Successful)
                        {
                            log.Error("User tried to input invalid AM2R_11.zip file (" + errorCode + "). Cancelling import.");
                            MessageBox.Show(Language.Text.ZipIsNotAM2R11 + "\n\nError Code: " + errorCode, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                            return;
                        }

                        //if either a directory was selected or the file somehow went missing, cancel
                        if (!File.Exists(fileFinder.FileName))
                        {
                            log.Error("Selected AM2R_11.zip file not found! Cancelling import.");
                            break;
                        }

                        //we check if it exists first, because someone coughDRUIDcough might've copied it into here while on the showDialog
                        if(!File.Exists(CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip"))
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

                    await Task.Run(() =>
                    {
                        //if the file cannot be launched due to anti-virus shenanigans or any other reason, we try catch here
                        try
                        {
                            InstallProfile(profileList[profileIndex.Value]);
                        }
                        catch(Exception ex)
                        {
                            log.Error(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace);
                            MessageBox.Show(ex.Message + "\n*****Stack Trace*****\n\n" + ex.StackTrace, Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                        }
                    });
                    progressBar.Visible = false;
                    progressBar.Value = 0;

                    // Just need to switch this to anything that isn't an "active" state so SetUpdateState() actually does something
                    SetPlayButtonState(UpdateState.Play);

                    UpdateStateMachine();

                    break;
                #endregion

                #region Play
                case UpdateState.Play:

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

                    await Task.Run(() => RunGame());

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
            //thank you random issue on the gitlib2sharp repo!!!!
            //also tldr; rtfm
            if (isGitProcessGettingCancelled) return false;

            // This needs to be in an Invoke, in order to access the variables from the main thread
            // Otherwise this wil throw a runtime exception
            Application.Instance.Invoke(new Action(() =>
            {
                progressBar.MinValue = 0;
                progressBar.MaxValue = transferProgress.TotalObjects;
                progressLabel.Text = Language.Text.ProgressbarProgress + transferProgress.ReceivedObjects + " (" + (int)transferProgress.ReceivedBytes / 1000000 + "MB) / " + transferProgress.TotalObjects + " objects";
                if (currentGitObject < transferProgress.ReceivedObjects)
                {
                    currentGitObject = transferProgress.ReceivedObjects;
                    progressBar.Value = transferProgress.ReceivedObjects;
                    
                }
            }));

            return true;
        }

        /// <summary>
        /// Does stuff, depending on the current state of <see cref="apkButtonState"/>.
        /// </summary>
        private async void ApkButtonClickEvent(object sender, EventArgs e)
        {
            UpdateStateMachine();

            if (apkButtonState == ApkButtonState.Create)
            {
                SetApkButtonState(ApkButtonState.Creating);

                UpdateStateMachine();

                progressBar.Visible = true;

                await Task.Run(() => CreateAPK(profileList[profileIndex.Value]));

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

            ProfileXML addedProfile = null;

            OpenFileDialog fileFinder = new OpenFileDialog
            {
                Directory = new Uri(CrossPlatformOperations.CURRENTPATH),
                MultiSelect = false,
                Title = Language.Text.SelectModFileDialog
            };

            fileFinder.Filters.Add(new FileFilter(Language.Text.ZipArchiveText, new[] { ".zip" }));

            fileFinder.ShowDialog(this);

            if (!string.IsNullOrWhiteSpace(fileFinder.FileName)) // This is default
            {
                log.Info("User selected \"" + fileFinder.FileName + "\"");

                //if either a directory was selected or the file somehow went missing, cancel
                if (!File.Exists(fileFinder.FileName))
                {
                    log.Error("Selected mod .zip file not found! Cancelling import.");
                    return;
                }

                FileInfo modFile = new FileInfo(fileFinder.FileName);

                //we check if it exists first, because it might've copied it into here while on the showDialog
                // This is irrelevant - we don't need to copy the zip over
                // if (!File.Exists(CrossPlatformOperations.CURRENTPATH + "/Mods/" + modFile.Name))
                //     File.Copy(fileFinder.FileName, CrossPlatformOperations.CURRENTPATH + "/Mods/" + modFile.Name);

                string modsDir = new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/Mods").FullName;
                string extractedName = modFile.Name.Replace(".zip", "");

                //extract it and see if it contains a profile.xml. If not, this is invalid

                // check first, if the directory is already there, if yes, throw a message
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

                // let's check if profile.xml exists in there! If it doesn't throw an error and cleanup
                if (!File.Exists(modsDir + "/" + extractedName + "/profile.xml"))
                {
                    log.Error(fileFinder.FileName + " does not contain profile.xml! Cancelling mod import.");

                    MessageBox.Show(Language.Text.ModIsInvalidMessage.Replace("$NAME", extractedName), Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                    Directory.Delete(modsDir + "/" + extractedName, true);
                    File.Delete(CrossPlatformOperations.CURRENTPATH + "/Mods/" + modFile.Name);
                    return;
                }

                ProfileXML profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(modsDir + "/" + extractedName + "/profile.xml"));

                // check if the OS versions match
                if((Platform.IsWinForms && profile.OperatingSystem != "Windows") || (Platform.IsGtk && profile.OperatingSystem != "Linux"))
                {
                    string currentOS = "";
                    if (Platform.IsWinForms) currentOS = "Windows";
                    else if (Platform.IsGtk) currentOS = "Linux";           ///teeeeechnically, windows users and macos users could run GTK applications as well, so this would prolly need clarification.


                    log.Error("Mod is for " + profile.OperatingSystem + " while current OS is " + Platform + ". Cancelling mod import.");

                    MessageBox.Show(Language.Text.ModIsForWrongOS.Replace("$NAME",profile.Name).Replace("$OS",profile.OperatingSystem).Replace("$CURRENTOS",currentOS), Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                    HelperMethods.DeleteDirectory(modsDir + "/" + extractedName);
                    return;
                }

                // check by *name*, if the mod was installed already
                if (profileList.Where(p => p.Name == profile.Name).FirstOrDefault() != null || Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name))
                {
                    log.Error(profile.Name + " is already installed.");
                    MessageBox.Show(Language.Text.ModIsAlreadyInstalledMessage.Replace("$NAME", profile.Name), Language.Text.WarningWindowTitle, MessageBoxType.Warning);
                    HelperMethods.DeleteDirectory(modsDir + "/" + extractedName);
                    return;
                }

                addedProfile = profile;
                log.Info(profile.Name + " successfully installed.");
                MessageBox.Show(Language.Text.ModSuccessfullyInstalledMessage.Replace("$NAME", profile.Name), Language.Text.SuccessWindowTitle);
                
            }
            else
            {
                log.Error("User did not supply valid input. Cancelling import.");
                LoadProfiles();
                return;
            }

            LoadProfiles();
            settingsProfileDropDown.SelectedIndex = profileList.FindIndex(p => p.Name == addedProfile.Name);
            if (settingsProfileDropDown.SelectedIndex == -1)
                settingsProfileDropDown.SelectedIndex = 0;
        }

        /// <summary>
        /// This opens the save directory for the current profile.
        /// </summary>
        private void SaveButtonClickEvent(object sender, EventArgs e)
        {
            if (IsProfileIndexValid())
            {
                log.Info("User opened the save directory for profile " + profileList[settingsProfileDropDown.SelectedIndex].Name + ", which is " + profileList[settingsProfileDropDown.SelectedIndex].SaveLocation);
                CrossPlatformOperations.OpenFolder(profileList[settingsProfileDropDown.SelectedIndex].SaveLocation);
            }
        }

        /// <summary>
        /// Enabled / disables <see cref="updateModButton"/> and <see cref="deleteModButton"/> accordingly.
        /// </summary>
        private void SettingsProfileDropDownSelectedIndexChanged(object sender, EventArgs e)
        {
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
                updateModButton.Enabled = true;
                updateModButton.ToolTip = Language.Text.UpdateModButtonToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
            }

            if (!(settingsProfileDropDown.SelectedIndex < 0 || settingsProfileDropDown.Items.Count == 0))
            {
                profileNotesTextArea.TextColor = colGreen;
                profileNotesTextArea.Text = Language.Text.ProfileNotes + profileList[settingsProfileDropDown.SelectedIndex].ProfileNotes;
            }
        }

        /// <summary>
        /// Fires when the profile layout completes loading. This makes sure that if <see cref="settingsProfileDropDown"/> has nothing in it "on boot",
        /// that everything is disabled.
        /// </summary>
        private void ProfileLayoutLoadComplete(object sender, EventArgs e)
        {
            // safety check
            if (settingsProfileDropDown == null) return;
            if(settingsProfileDropDown.Items.Count == 0)
            {
                addModButton.Enabled = false;
                settingsProfileLabel.TextColor = colInactive;
                settingsProfileDropDown.Enabled = false;
                saveButton.Enabled = false;
                updateModButton.Enabled = false;
                deleteModButton.Enabled = false;
                profileNotesTextArea.TextColor = colInactive;
            }
        }

        /// <summary>
        /// The <see cref="MainForm"/> calls this when you're resizing, in order to resize and scale the application accordingly.
        /// </summary>
        private void DrawablePaintEvent(object sender, PaintEventArgs e)
        {
            // Get drawing variables
            int height = drawable.Height;
            int width = drawable.Width;
            float scaleDivisor = Platform.IsWinForms ? 955f : 715f; // Magic brute-forced values. Don't ask questions, because we don't have answers >_>
                                                                    // Also, seems like nix systems share the same scaleDivisor. Again, don't ask.
            float scale = height / scaleDivisor;

            // Do the actual scaling
            e.Graphics.ScaleTransform(scale);

            // Draw the image, change x offset with some absurd wizardry written at 5 AM
            e.Graphics.DrawImage(formBG, ((width / 2) - (height / 1.4745f)) / scale, 0);
        }

        #region ICON EVENTS

        /// <summary>Gets called when <see cref="redditIcon"/> gets clicked.</summary>
        private void RedditIconOnClick(object sender, EventArgs e) { CrossPlatformOperations.OpenURL("http://www.reddit.com/r/AM2R"); }
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
            profileIndex = profileDropDown.SelectedIndex;
            log.Info("profileDropDown.SelectedIndex has been changed to " + profileIndex + ".");

            profileAuthorLabel.Text = Language.Text.Author + " " + profileList[profileDropDown.SelectedIndex].Author;
            profileVersionLabel.Text = Language.Text.VersionLabel + " " + profileList[profileDropDown.SelectedIndex].Version;
            CrossPlatformOperations.WriteToConfig("ProfileIndex", profileIndex + "");       //Loj, tell me a better way to do this

            if (profileDropDown.SelectedIndex != 0 && (profileList[profileDropDown.SelectedIndex].SaveLocation == "%localappdata%/AM2R" || profileList[profileDropDown.SelectedIndex].SaveLocation == "default"))
                saveWarningLabel.Visible = true;
            else
                saveWarningLabel.Visible = false;
            UpdateStateMachine();
        }

        /// <summary>Gets called when user selects a different item from <see cref="languageDropDown"/> and writes that to the config.</summary>
        private void LanguageDropDownSelectedIndexChanged(object sender, EventArgs e)
        {
            log.Info("languageDropDown.SelectedIndex has been changed to " + languageDropDown.SelectedIndex + ".");
            if (languageDropDown.SelectedIndex == 0) CrossPlatformOperations.WriteToConfig("Language", "Default");
            else CrossPlatformOperations.WriteToConfig("Language", languageDropDown.Items[languageDropDown.SelectedIndex].Text);
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
            if (Platform.IsWinForms)
                mirrorDropDown.TextColor = mirrorDropDown.Enabled ? colGreen : colInactive;   // Not sure why the dropdown menu needs this hack, but the textBox does not.
            mirrorLabel.TextColor =  !enabled ? colGreen : colInactive;

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

            CrossPlatformOperations.WriteToConfig("MirrorIndex",mirrorDropDown.SelectedIndex);

            //don't overwrite the git config while we download!!!
            if (updateState == UpdateState.Downloading) return;

            log.Info("Overwriting mirror in gitconfig.");

            //check if the gitConfig exists, if yes regex the gitURL, and replace it with the new current Mirror.
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

            currentMirror = customMirrorTextBox.Text;
            CrossPlatformOperations.WriteToConfig("CustomMirrorText", currentMirror);

            log.Info("Overwriting mirror in gitconfig.");

            //check if the gitConfig exists, if yes regex the gitURL, and replace it with the new current Mirror.
            string gitConfigPath = CrossPlatformOperations.CURRENTPATH + "/PatchData/.git/config";
            if (!File.Exists(gitConfigPath)) return;
            string gitConfig = File.ReadAllText(gitConfigPath);
            Regex gitURLRegex = new Regex("https://.*\\.git");
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
            if (Platform.IsWinForms)
                mirrorDropDown.TextColor = mirrorDropDown.Enabled ? colGreen : colInactive;
        }

        /// <summary>
        /// Gets called when <see cref="deleteModButton"/> gets clicked. Deletes the current selected <see cref="ProfileXML"/> in <see cref="settingsProfileDropDown"/>.
        /// </summary>
        private void DeleteModButtonClicked(object sender, EventArgs e)
        {
            ProfileXML profile = profileList[settingsProfileDropDown.SelectedIndex];
            log.Info("User is attempting to delete profile " + profile.Name + ".");

            DialogResult result = MessageBox.Show(Language.Text.DeleteModWarning.Replace("$NAME", profile.Name), Language.Text.WarningWindowTitle, MessageBoxButtons.OKCancel, MessageBoxType.Warning, MessageBoxDefaultButton.Cancel);

            if (result == DialogResult.Ok)
            {
                log.Info("User did not cancel. Proceeding to delete " + profile);
                DeleteProfile(profile);
                log.Info(profile + " has been deleted");
                MessageBox.Show(Language.Text.DeleteModButtonSuccess.Replace("$NAME", profile.Name), Language.Text.SuccessWindowTitle, MessageBoxType.Information);
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

            fileFinder.Filters.Add(new FileFilter(Language.Text.ZipArchiveText, new[] { ".zip" }));

            fileFinder.ShowDialog(this);

            if (!string.IsNullOrWhiteSpace(fileFinder.FileName)) // This is default
            {
                log.Info("User selected \"" + fileFinder.FileName + "\"");

                //if either a directory was selected or the file somehow went missing, cancel
                if (!File.Exists(fileFinder.FileName))
                {
                    log.Error("Selected mod .zip file not found! Cancelling mod update.");
                    return;
                }

                FileInfo modFile = new FileInfo(fileFinder.FileName);

                string modsDir = new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/Mods").FullName;
                string extractedName = modFile.Name.Replace(".zip", "_new");

                //extract it and see if it contains a profile.xml. If not, this is invalid

                // Directory doesn't exist -> extract!
                ZipFile.ExtractToDirectory(fileFinder.FileName, modsDir + "/" + extractedName);

                // let's check if profile.xml exists in there! If it doesn't throw an error and cleanup
                if (!File.Exists(modsDir + "/" + extractedName + "/profile.xml"))
                {
                    log.Error(fileFinder.FileName + " does not contain profile.xml! Cancelling mod update.");
                    MessageBox.Show(Language.Text.ModIsInvalidMessage.Replace("$NAME", extractedName), Language.Text.ErrorWindowTitle, MessageBoxType.Error);
                    Directory.Delete(modsDir + "/" + extractedName, true);
                    File.Delete(CrossPlatformOperations.CURRENTPATH + "/Mods/" + modFile.Name);
                    return;
                }

                // check by *name*, if the mod was installed already
                ProfileXML profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(modsDir + "/" + extractedName + "/profile.xml"));

                if (profileList.Where(p => p.Name == profile.Name).FirstOrDefault() != null || Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name))
                {
                    // Mod is already installed, so we can update!
                    DialogResult result = MessageBox.Show(Language.Text.UpdateModWarning.Replace("$NAME", currentProfile.Name), Language.Text.WarningWindowTitle, MessageBoxButtons.OKCancel, MessageBoxType.Warning, MessageBoxDefaultButton.Cancel);

                    if (result == DialogResult.Ok)
                    {
                        // Delete profile
                        DeleteProfile(currentProfile);

                        // Rename directory to take the old one's place
                        string originalFolder = modsDir + "/" + extractedName.Replace("_new", "");
                        Directory.Move(modsDir + "/" + extractedName, originalFolder);
                        
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
                    log.Error("Mod is not already installed! Cancelling mod update.");
                    MessageBox.Show(Language.Text.UpdateModButtonWrongMod.Replace("$NAME", currentProfile.Name).Replace("$SELECT", profile.Name), Language.Text.WarningWindowTitle, MessageBoxButtons.OK);
                    abort = true;
                }

                if (abort)
                {
                    // file cleanup
                    HelperMethods.DeleteDirectory(modsDir + "/" + extractedName);
                    LoadProfiles();
                    return;
                }

                log.Info("Successfully updated mod profile " + profile.Name + ".");
                MessageBox.Show(Language.Text.ModSuccessfullyInstalledMessage.Replace("$NAME", currentProfile.Name), Language.Text.SuccessWindowTitle);
            }

            ProfileXML currentSelectedProfile = profileList[settingsProfileDropDown.SelectedIndex];
            LoadProfiles();
            settingsProfileDropDown.SelectedIndex = profileList.FindIndex(p => p.Name == currentSelectedProfile.Name);
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
            CrossPlatformOperations.WriteToConfig("IsMaximized", (this.WindowState == WindowState.Maximized));

            if (updateState == UpdateState.Downloading)
            {
                var result = MessageBox.Show(Language.Text.CloseOnCloningText, Language.Text.WarningWindowTitle, MessageBoxButtons.YesNo, MessageBoxType.Warning, MessageBoxDefaultButton.No);
                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                }
                else 
                    isGitProcessGettingCancelled = true;
                    //We don't need to delete any folders here, the cancelled gitClone will do that automatically for us :)

            }
            else if (updateState == UpdateState.Installing)
            {
                MessageBox.Show(Language.Text.CloseOnInstallingText, Language.Text.WarningWindowTitle, MessageBoxButtons.OK, MessageBoxType.Warning);
                e.Cancel = true;
            }

            //this needs to be made invisible, otherwise a tray indicator will be visible (on linux?) that clicking crashes the application
            trayIndicator.Visible = false;

            if (e.Cancel)
                log.Info("Cancelled MainForm closing event during UpdateState." + updateState.ToString() + ".");
            else
                log.Info("Successfully closed MainForm. Exiting main thread.");
        }

    }
}
