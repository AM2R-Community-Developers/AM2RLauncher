using AM2RLauncher.Helpers;
using AM2RLauncher.XML;
using Eto.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AM2RLauncher
{
    /// <summary>
    /// Everything state machine-related goes in here
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
            if (updateState != UpdateState.Downloading && updateState != UpdateState.Installing)
            {
                if (apkButtonState != ApkButtonState.Creating)
                {
                    playButton.Enabled = true;
                    // If PatchData is cloned...
                    if (HelperMethods.IsPatchDataCloned())
                    {
                        // If 1.1 is installed...
                        if (Is11Installed())
                        {
                            // If current profile is installed...
                            if (IsProfileIndexValid() && IsProfileInstalled(profileList[profileIndex.Value]))
                            {
                                // We're ready to play!
                                SetPlayButtonState(UpdateState.Play);
                            }
                            // Otherwise, if profile is NOT installable...
                            else if (IsProfileIndexValid() && profileList[profileIndex.Value].Installable == false)
                            {
                                // We delete the profile, because we can't install it and it therefor holds no value!
                                DeleteProfile(profileList[profileIndex.Value]);
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
                else
                {
                    playButton.Enabled = false;
                }
            }
        }

        /// <summary>
        /// Determines current conditions and enables or disables <see cref="apkButton"/> accordingly.
        /// </summary>
        private void UpdateApkState()
        {
            // Safety check
            if (apkButton != null)
            {
                // If profile supports Android...
                if (IsProfileIndexValid() && profileList[profileIndex.Value].SupportsAndroid && profileList[profileIndex.Value].Installable)
                {
                    // If we are NOT already creating an APK...
                    if (apkButtonState == ApkButtonState.Create)
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
                    }
                    else // Otherwise, disable.
                    {
                        apkButton.Enabled = false;
                        apkButton.ToolTip = Language.Text.ApkButtonDisabledToolTip;
                    }
                }
                else // Otherwise, disable.
                {
                    apkButton.Enabled = false;
                    apkButton.ToolTip = Language.Text.ApkButtonDisabledToolTip;
                }
            }
        }

        /// <summary>
        /// Determines current conditions and enables or disables the <see cref="profileDropDown"/> and related controls accordingly.
        /// </summary>
        private void UpdateProfileState()
        {
            // Safety check
            if (profileDropDown != null)
            {
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

                if (Platform.IsWinForms)
                    profileDropDown.TextColor = col;
                profileAuthorLabel.TextColor = col;
                profileVersionLabel.TextColor = col;
                profileLabel.TextColor = col;
            }
        }

        /// <summary>
        /// Determines current conditions and enables or disables <see cref="profilePage"/> controls accordingly.
        /// </summary>
        private void UpdateProfileSettingsState()
        {
            // Safety check
            if (settingsProfileDropDown == null) return;
            if (settingsProfileDropDown.Items.Count > 0)
            {
                bool enabled = false;
                switch (updateState)
                {
                    case UpdateState.Download: enabled = false; break;
                    case UpdateState.Downloading: enabled = false; break;
                    case UpdateState.Select11: enabled = false; break;
                    case UpdateState.Install: enabled = true; break;
                    case UpdateState.Installing: enabled = false; break;
                    case UpdateState.Play: enabled = true; break;
                    case UpdateState.Playing: enabled = false; break;
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
                    updateModButton.Enabled = enabled;
                    updateModButton.ToolTip = Language.Text.UpdateModButtonToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
                    deleteModButton.Enabled = enabled;
                    deleteModButton.ToolTip = Language.Text.DeleteModButtonToolTip.Replace("$NAME", settingsProfileDropDown.Items[settingsProfileDropDown.SelectedIndex].Text);
                }

                Color col = enabled ? colGreen : colInactive;

                if (Platform.IsWinForms)
                    settingsProfileDropDown.TextColor = col;

                settingsProfileLabel.TextColor = col;

                if (enabled)
                {
                    settingsProfileDropDown.SelectedIndex = profileDropDown.SelectedIndex;
                }
            }
        }

        /// <summary>
        /// Sets the global <see cref="updateState"/> and then changes the state of <see cref="playButton"/> accordingly. 
        /// </summary>
        /// <param name="state">The state that should be set to.</param>
        private void SetPlayButtonState(UpdateState state)
        {
            updateState = state;
            string profileName = (profileDropDown != null && profileDropDown.Items.Count > 0) ? profileDropDown.Items[profileDropDown.SelectedIndex].Text : "";
            switch (updateState)
            {
                case UpdateState.Download: playButton.Enabled = true; playButton.ToolTip = Language.Text.PlayButtonDownloadToolTip; break;
                case UpdateState.Downloading: playButton.Enabled = true; playButton.ToolTip = ""; break; // ; playButton.ToolTip = Language.Text.PlayButtonDownladingToolTip; break;
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
            }
            return null;
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
            }
            return null;
        }
        /// <summary>
        /// Checks if AM2R 1.1 has been installed already, aka if a valid AM2R 1.1 Zip exists.
        /// </summary>
        /// <returns><see langword="true"/> if yes, <see langword="false"/> if not.</returns>
        public static bool Is11Installed()
        {
            // If we have a cache, return that instead
            if (isAM2R11InstalledCache != null) return isAM2R11InstalledCache.Value;

            // Return safely if file doesn't exist
            if (!File.Exists(CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip")) return false;
            var returnCode = HelperMethods.CheckIfZipIsAM2R11(CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip");
            // Check if it's valid, if not log it, rename it and silently leave
            if (returnCode != IsZipAM2R11ReturnCodes.Successful)
            {
                log.Info("Detected invalid AM2R_11 zip with following error code: " + returnCode);
                HelperMethods.RecursiveRollover(CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip");
                isAM2R11InstalledCache = false;
                return false;
            }
            isAM2R11InstalledCache = true;
            return true;
        }

        /// <summary>
        /// Invalidates <see cref="isAM2R11InstalledCache"/>.
        /// </summary>
        public static void InvalidateAM2R11InstallCache()
        {
            isAM2R11InstalledCache = null;
        }

    }
}
