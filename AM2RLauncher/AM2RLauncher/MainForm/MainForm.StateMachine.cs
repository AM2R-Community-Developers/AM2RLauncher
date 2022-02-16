using Eto.Drawing;
using System;
using AM2RLauncher.Core;
using AM2RLauncher.Core.XML;

namespace AM2RLauncher
{
    /// <summary>
    /// Everything UI/Form state machine-related goes in here
    /// </summary>
    public partial class MainForm
    {
        /// <summary>
        /// Updates <see cref="updateState"/>, <see cref="playButton"/>, <see cref="apkButtonState"/>, and <see cref="apkButton"/> according to the current conditiions.
        /// </summary>
        private void UpdateStateMachine()
        {
            UpdatePlayState();
            UpdateApkState();
            UpdateProfileState();
            UpdateProfileSettingsState();
        }

        /// <summary>
        /// Determines current conditions and calls <see cref="SetPlayButtonState(UpdateState)"/> accordingly.
        /// </summary>
        private void UpdatePlayState()
        {
            // If not downloading or installing...
            if ((updateState == UpdateState.Downloading) || (updateState == UpdateState.Installing))
                return;

            // If we're currently creating an APK...
            if (apkButtonState != ApkButtonState.Creating)
            {
                playButton.Enabled = true;
                // If PatchData is cloned...
                if (Profile.IsPatchDataCloned())
                {
                    // If 1.1 is installed or if the current profile is invalid...
                    if (Profile.Is11Installed())
                    {
                        var isProfileValid = IsProfileIndexValid();
                        // If current profile is installed...
                        if (isProfileValid && Profile.IsProfileInstalled(profileList[profileIndex.Value]))
                        {
                            // We're ready to play!
                            SetPlayButtonState(UpdateState.Play);
                        }
                        // Otherwise, if profile is NOT installable...
                        else if (isProfileValid && profileList[profileIndex.Value].Installable == false)
                        {
                            // We delete the profile, because we can't install it and it therefore holds no value!
                            DeleteProfileAndAdjustLists(profileList[profileIndex.Value]);
                        }
                        // Otherwise, we still need to install.
                        else
                        {
                            SetPlayButtonState(UpdateState.Install);
                        }
                    }
                    else // We still need to select 1.1.
                    {
                        SetPlayButtonState(UpdateState.Select11);
                    }
                }
                else // We still need to download.
                {
                    SetPlayButtonState(UpdateState.Download);
                }
            }
            else // We disable the Play button.
            {
                playButton.Enabled = false;
            }
        }

        /// <summary>
        /// Determines current conditions and enables or disables <see cref="apkButton"/> accordingly.
        /// </summary>
        private void UpdateApkState()
        {
            // Safety check
            if (apkButton == null)
                return;
            
            // If profile supports Android and if we are NOT already creating an APK...
            if (IsProfileIndexValid())
            {
                var profile = profileList[profileIndex.Value];
                if (profile.SupportsAndroid && profile.Installable && (apkButtonState == ApkButtonState.Create))
                {
                    // Switch status based on main button's state
                    switch (updateState)
                    {
                        case UpdateState.Download: apkButton.Enabled = false; apkButton.ToolTip = Language.Text.ApkButtonDisabledToolTip; break;
                        case UpdateState.Downloading: apkButton.Enabled = false; apkButton.ToolTip = Language.Text.ApkButtonDisabledToolTip; break;
                        case UpdateState.Select11: apkButton.Enabled = false; apkButton.ToolTip = Language.Text.ApkButtonDisabledToolTip; break;
                        case UpdateState.Install: apkButton.Enabled = true; apkButton.ToolTip = Language.Text.ApkButtonEnabledToolTip.Replace("$NAME", profileDropDown?.Items[profileDropDown.SelectedIndex]?.Text ?? ""); break;
                        case UpdateState.Installing: apkButton.Enabled = false; apkButton.ToolTip = Language.Text.ApkButtonDisabledToolTip; break;
                        case UpdateState.Play: apkButton.Enabled = true; apkButton.ToolTip = Language.Text.ApkButtonEnabledToolTip.Replace("$NAME", profileDropDown?.Items[profileDropDown.SelectedIndex]?.Text ?? ""); break;
                        case UpdateState.Playing: apkButton.Enabled = false; apkButton.ToolTip = Language.Text.ApkButtonDisabledToolTip; break;
                    }
                    return;
                }
            }

            apkButton.Enabled = false;
            apkButton.ToolTip = Language.Text.ApkButtonDisabledToolTip;
        }

        /// <summary>
        /// Determines current conditions and enables or disables the <see cref="profileDropDown"/> and related controls accordingly.
        /// </summary>
        private void UpdateProfileState()
        {
            // Safety check
            if (profileDropDown == null)
                return;
            switch (updateState)
            {
                case UpdateState.Download: profileDropDown.Enabled = false; break;
                case UpdateState.Downloading: profileDropDown.Enabled = false; break;
                case UpdateState.Select11: profileDropDown.Enabled = true; break;
                case UpdateState.Install: profileDropDown.Enabled = true; break;
                case UpdateState.Installing: profileDropDown.Enabled = false; break;
                case UpdateState.Play: profileDropDown.Enabled = true; break;
                case UpdateState.Playing: profileDropDown.Enabled = false; break;
            }
            switch (apkButtonState)
            {
                case ApkButtonState.Creating: profileDropDown.Enabled = false; break;
            }

            Color col = profileDropDown.Enabled ? colGreen : colInactive;

            if (OS.IsWindows)
                profileDropDown.TextColor = col;
            profileAuthorLabel.TextColor = col;
            profileVersionLabel.TextColor = col;
            profileLabel.TextColor = col;
        }

        /// <summary>
        /// Determines current conditions and enables or disables <see cref="profilePage"/> controls accordingly.
        /// </summary>
        private void UpdateProfileSettingsState()
        {
            // Safety check
            if (settingsProfileDropDown == null || settingsProfileDropDown.Items.Count <= 0) return;
            
            bool enabled = false;
            switch (updateState)
            {
                case UpdateState.Download:
                case UpdateState.Downloading:
                case UpdateState.Select11:
                case UpdateState.Installing:
                case UpdateState.Playing: enabled = false; break;
                case UpdateState.Install:
                case UpdateState.Play: enabled = true; break;

            }
            if (apkButtonState == ApkButtonState.Creating) enabled = false;

            settingsProfileLabel.TextColor = colGreen;
            settingsProfileDropDown.Enabled = enabled;
            profileButton.Enabled = enabled;
            profileButton.ToolTip = Language.Text.OpenProfileFolderToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
            saveButton.Enabled = enabled;
            saveButton.ToolTip = Language.Text.OpenSaveFolderToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
            addModButton.Enabled = enabled;
            addModButton.ToolTip = Language.Text.AddNewModToolTip;

            // Only enable these, when we're not on the community updates
            if (settingsProfileDropDown.SelectedIndex > 0)
            {
                updateModButton.Enabled = profileList[settingsProfileDropDown.SelectedIndex].Installable;
                updateModButton.ToolTip = Language.Text.UpdateModButtonToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
                deleteModButton.Enabled = enabled;
                deleteModButton.ToolTip = Language.Text.DeleteModButtonToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
            }

            Color col = enabled ? colGreen : colInactive;

            if (OS.IsWindows)
                settingsProfileDropDown.TextColor = col;

            settingsProfileLabel.TextColor = col;

            if (enabled)
            {
                settingsProfileDropDown.SelectedIndex = profileDropDown.SelectedIndex;
            }
        }

        /// <summary>
        /// Sets the global <see cref="updateState"/> and then changes the state of <see cref="playButton"/> accordingly. 
        /// </summary>
        /// <param name="state">The state that should be set to.</param>
        private void SetPlayButtonState(UpdateState state)
        {
            updateState = state;
            string profileName = ((profileDropDown != null) && (profileDropDown.Items.Count > 0)) ? profileDropDown.Items[profileDropDown.SelectedIndex].Text : "";
            switch (updateState)
            {
                case UpdateState.Download: playButton.Enabled = true; playButton.ToolTip = Language.Text.PlayButtonDownloadToolTip; break;
                case UpdateState.Downloading: playButton.Enabled = true; playButton.ToolTip = ""; playButton.ToolTip = Language.Text.PlayButtonDownladingToolTip; break;
                case UpdateState.Select11: playButton.Enabled = true; playButton.ToolTip = Language.Text.PlayButtonSelect11ToolTip; break;
                case UpdateState.Install: playButton.Enabled = true; playButton.ToolTip = Language.Text.PlayButtonInstallToolTip.Replace("$NAME", profileName); break;
                case UpdateState.Installing: playButton.Enabled = false; playButton.ToolTip = Language.Text.PlayButtonInstallingToolTip; break;
                case UpdateState.Play: playButton.Enabled = true; playButton.ToolTip = Language.Text.PlayButtonPlayToolTip.Replace("$NAME", profileName); break;
                case UpdateState.Playing: playButton.Enabled = false; playButton.ToolTip = Language.Text.PlayButtonPlayingToolTip; break;
            }
            playButton.Text = GetPlayButtonText();

            playButton.Invalidate();

            UpdateProfileSettingsState();
        }

        /// <summary>
        /// Sets the global <see cref="apkButtonState"/> and then changes the state of <see cref="apkButton"/> accordingly. 
        /// </summary>
        /// <param name="state">The state that should be set to.</param>
        private void SetApkButtonState(ApkButtonState state)
        {
            apkButtonState = state;
            switch (apkButtonState)
            {
                case ApkButtonState.Create: apkButton.Enabled = true; break;
                case ApkButtonState.Creating: apkButton.Enabled = false; break;
            }
            apkButton.Text = GetApkButtonText();
        }

        /// <summary>
        /// This returns the text that <see cref="playButton"/> should have depending on the global updateState.
        /// </summary>
        /// <returns>The text as a <see cref="String"/>, or <see langword="null"/> if the current State is invalid.</returns>
        private string GetPlayButtonText()
        {
            switch (updateState)
            {
                case UpdateState.Download: return Language.Text.Download;
                case UpdateState.Downloading: return Language.Text.Abort;
                case UpdateState.Select11: return Language.Text.Select11;
                case UpdateState.Install: return Language.Text.Install;
                case UpdateState.Installing: return Language.Text.Installing;
                case UpdateState.Play: return Language.Text.Play;
                case UpdateState.Playing: return Language.Text.Playing;
                default: return null;
            }
        }

        /// <summary>
        /// This returns the text that the apkButton should have depending on the global updateState.
        /// </summary>
        /// <returns>The text as a <see cref="String"/>, or <see langword="null"/> if the current State is invalid.</returns>
        private string GetApkButtonText()
        {
            switch (apkButtonState)
            {
                case ApkButtonState.Create: return Language.Text.CreateAPK;
                case ApkButtonState.Creating: return Language.Text.CreatingAPK;
                default: return null;
            }
        }

        /// <summary>
        /// Loads valid profile entries and reloads the necessary UI components.
        /// </summary>
        private void LoadProfilesAndAdjustLists()
        {
            // Reset loaded profiles
            profileDropDown.Items.Clear();
            profileList.Clear();
            profileIndex = null;

            // Load the profileList
            profileList = Profile.LoadProfiles();

            // Add profile names to the profileDropDown
            foreach (ProfileXML profile in profileList)
            {
                // Archive version notes
                if (!profile.Installable)
                {
                    //TODO: localizations
                    if (profile.Name.Contains("Community Updates"))
                        profile.ProfileNotes = Language.Text.ArchiveNotesCommunityUpdates;
                    else
                        profile.ProfileNotes = Language.Text.ArchiveNotesMods + "\n\n" + profile.ProfileNotes;
                }

                profileDropDown.Items.Add(profile.Name);
            }

            // Read the value from the config
            string profIndexString = CrossPlatformOperations.ReadFromConfig("ProfileIndex");

            // Check if either no profile was found or the setting says that the last current profile didn't exist
            if (profileDropDown.Items.Count == 0)
                profileIndex = null;
            else
            {
                // We know that profiles exist at this point, so we're going to point it to 0 instead so the following code doesn't fail
                if (profIndexString == "null")
                    profIndexString = "0";

                // We parse from the settings, and check if profiles got deleted from the last time the launcher has been selected. if yes, we revert the last selection to 0;
                int intParseResult = Int32.Parse(profIndexString);
                profileIndex = intParseResult;
                if (profileIndex >= profileDropDown.Items.Count)
                    profileIndex = 0;
                profileDropDown.SelectedIndex = profileIndex.Value;
            }

            // Update stored profiles in the Profile Settings tab
            settingsProfileDropDown.Items.Clear();
            settingsProfileDropDown.Items.AddRange(profileDropDown.Items);
            settingsProfileDropDown.SelectedIndex = profileDropDown.Items.Count != 0 ? 0 : -1;

            // Refresh the author and version label on the main tab
            if (profileList.Count > 0)
            {
                profileAuthorLabel.Text = Language.Text.Author + " " + profileList[profileDropDown.SelectedIndex].Author;
                profileVersionLabel.Text = Language.Text.VersionLabel + " " + profileList[profileDropDown.SelectedIndex].Version;
            }

            log.Info("Reloading UI components after loading successful.");

            UpdateStateMachine();
        }


        /// <summary>
        /// Deletes a profile and reloads the necessary UI components.
        /// </summary>
        /// <param name="profile">The profile to delete.</param>
        private void DeleteProfileAndAdjustLists(ProfileXML profile)
        {
            Profile.DeleteProfile(profile);
            LoadProfilesAndAdjustLists();
        }

        private void ArchiveProfileAndAdjustLists(ProfileXML profile)
        {
            Profile.ArchiveProfile(profile);
            LoadProfilesAndAdjustLists();
        }
    }
}