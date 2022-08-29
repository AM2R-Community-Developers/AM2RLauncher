using Eto.Drawing;
using System;
using AM2RLauncherLib;
using AM2RLauncherLib.XML;
using AM2RLauncher.Language;

namespace AM2RLauncher;

/// <summary>
/// Everything UI/Form state machine-related goes in here
/// </summary>
public partial class MainForm
{

    /// <summary>
    /// An enum, that has possible states for the play button.
    /// </summary>
    public enum PlayButtonState
    {
        Download,
        Downloading,
        Select11,
        Install,
        Installing,
        Play,
        Playing
    }

    /// <summary>
    /// An enum, that has different states for <see cref="apkButton"/>.
    /// </summary>
    public enum ApkButtonState
    {
        Create,
        Creating
    }

    /// <summary>
    /// Updates <see cref="updateState"/>, <see cref="playButton"/>, <see cref="apkButtonState"/>, and <see cref="apkButton"/> according to the current conditions.
    /// </summary>
    private void UpdateStateMachine()
    {
        UpdatePlayState();
        UpdateApkState();
        UpdateProfileState();
        UpdateModSettingsState();
    }

    /// <summary>
    /// Determines current conditions and calls <see cref="SetPlayButtonState(PlayButtonState)"/> accordingly.
    /// </summary>
    private void UpdatePlayState()
    {
        // If we're downloading or installing, don't change anything
        if ((updateState == PlayButtonState.Downloading) || (updateState == PlayButtonState.Installing))
            return;

        // If we're currently creating an APK, we disable the play button
        if (apkButtonState == ApkButtonState.Creating)
        {
            playButton.Enabled = false;
            return;
        }

        playButton.Enabled = true;
        // If PatchData isn't cloned, we still need to download
        if (!Profile.IsPatchDataCloned())
        {
            SetPlayButtonState(PlayButtonState.Download);
            return;
        }

        // If 1.1 isn't installed, we still need to select it
        if (!Profile.Is11Installed())
        {
            SetPlayButtonState(PlayButtonState.Select11);
            return;
        }

        var isProfileValid = IsProfileIndexValid();
        // If current profile is installed, we're ready to play!
        if (isProfileValid && Profile.IsProfileInstalled(profileList[profileIndex.Value]))
        {
            SetPlayButtonState(PlayButtonState.Play);
            return;
        }
        // Otherwise, if profile is NOT installable, we delete the profile because we can't install it and therefore holds no value!
        if (isProfileValid && profileList[profileIndex.Value].Installable == false)
        {
            DeleteProfileAndAdjustLists(profileList[profileIndex.Value]);
            return;
        }

        // Otherwise, we still need to install.
        SetPlayButtonState(PlayButtonState.Install);
    }

    /// <summary>
    /// Determines current conditions and enables or disables <see cref="apkButton"/> accordingly.
    /// </summary>
    private void UpdateApkState()
    {
        // Safety check
        if (apkButton == null)
            return;

        // Our default values
        apkButton.Enabled = false;
        apkButton.ToolTip = Text.ApkButtonDisabledToolTip;

        // If profile supports Android and if we are NOT already creating an APK...
        if (!IsProfileIndexValid())
            return;

        var profile = profileList[profileIndex.Value];
        if (!profile.SupportsAndroid || !profile.Installable || apkButtonState != ApkButtonState.Create)
            return;

        // Switch status based on main button's state
        switch (updateState)
        {
            case PlayButtonState.Download:
            case PlayButtonState.Downloading:
            case PlayButtonState.Select11:
            case PlayButtonState.Installing:
            case PlayButtonState.Playing: return;

            case PlayButtonState.Install:
            case PlayButtonState.Play: apkButton.Enabled = true; apkButton.ToolTip = HelperMethods.GetText(Text.ApkButtonEnabledToolTip, profileDropDown?.Items[profileDropDown.SelectedIndex]?.Text ?? ""); break;
        }
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
            case PlayButtonState.Download:
            case PlayButtonState.Downloading:
            case PlayButtonState.Select11:
            case PlayButtonState.Installing:
            case PlayButtonState.Playing: profileDropDown.Enabled = false; break;

            case PlayButtonState.Install:
            case PlayButtonState.Play: profileDropDown.Enabled = true; break;

        }
        if (apkButtonState == ApkButtonState.Creating) profileDropDown.Enabled = false;

        Color col = profileDropDown.Enabled ? colorGreen : colorInactive;

        if (OS.IsWindows)
            profileDropDown.TextColor = col;
        profileAuthorLabel.TextColor = col;
        profileVersionLabel.TextColor = col;
        profileLabel.TextColor = col;
    }

    /// <summary>
    /// Determines current conditions and enables or disabled the mainPage controls accordingly.
    /// </summary>
    private void UpdateModSettingsState()
    {
        // Safety check
        if (modSettingsProfileDropDown == null || modSettingsProfileDropDown.Items.Count <= 0) return;

        bool enabled = false;
        switch (updateState)
        {
            case PlayButtonState.Download:
            case PlayButtonState.Downloading:
            case PlayButtonState.Select11:
            case PlayButtonState.Installing:
            case PlayButtonState.Playing: enabled = false; break;

            case PlayButtonState.Install:
            case PlayButtonState.Play: enabled = true; break;
        }
        if (apkButtonState == ApkButtonState.Creating) enabled = false;

        string selectedProfileName = modSettingsProfileDropDown.Items[modSettingsProfileDropDown.SelectedIndex].Text;

        settingsProfileLabel.TextColor = colorGreen;
        modSettingsProfileDropDown.Enabled = enabled;
        profileButton.Enabled = enabled;
        profileButton.ToolTip = HelperMethods.GetText(Text.OpenProfileFolderToolTip, selectedProfileName);
        saveButton.Enabled = enabled;
        saveButton.ToolTip = HelperMethods.GetText(Text.OpenSaveFolderToolTip, selectedProfileName);
        addModButton.Enabled = enabled;
        addModButton.ToolTip = Text.AddNewModToolTip;

        // Only enable these, when we're not on the community updates
        if (modSettingsProfileDropDown.SelectedIndex > 0)
        {
            desktopShortcutButton.Enabled = enabled;
            updateModButton.Enabled = profileList[modSettingsProfileDropDown.SelectedIndex].Installable;
            updateModButton.ToolTip = HelperMethods.GetText(Text.UpdateModButtonToolTip, selectedProfileName);
            deleteModButton.Enabled = enabled;
            deleteModButton.ToolTip = HelperMethods.GetText(Text.DeleteModButtonToolTip, selectedProfileName);
        }

        Color col = enabled ? colorGreen : colorInactive;

        if (OS.IsWindows)
            modSettingsProfileDropDown.TextColor = col;

        settingsProfileLabel.TextColor = col;

        if (enabled)
            modSettingsProfileDropDown.SelectedIndex = profileDropDown.SelectedIndex;
    }

    /// <summary>
    /// Sets the global <see cref="updateState"/> and then changes the state of <see cref="playButton"/> accordingly.
    /// </summary>
    /// <param name="state">The state that should be set to.</param>
    private void SetPlayButtonState(PlayButtonState state)
    {
        updateState = state;
        switch (updateState)
        {
            case PlayButtonState.Download:
            case PlayButtonState.Downloading:
            case PlayButtonState.Select11:
            case PlayButtonState.Install:
            case PlayButtonState.Play: playButton.Enabled = true; break;

            case PlayButtonState.Installing:
            case PlayButtonState.Playing: playButton.Enabled = false; break;
        }
        playButton.Text = GetPlayButtonText();
        playButton.ToolTip = GetPlayButtonTooltip();

        playButton.Invalidate();

        UpdateModSettingsState();
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
            case PlayButtonState.Download: return Text.Download;
            case PlayButtonState.Downloading: return Text.Abort;
            case PlayButtonState.Select11: return Text.Select11;
            case PlayButtonState.Install: return Text.Install;
            case PlayButtonState.Installing: return Text.Installing;
            case PlayButtonState.Play: return Text.Play;
            case PlayButtonState.Playing: return Text.Playing;
            default: return null;
        }
    }

    /// <summary>
    /// This returns the tooltip that <see cref="playButton"/> should have depending on the global updateState.
    /// </summary>
    /// <returns>The tooltip as a <see cref="String"/>, or <see langword="null"/> if the current State is invalid.</returns>
    private string GetPlayButtonTooltip()
    {
        string profileName = ((profileDropDown != null) && (profileDropDown.Items.Count > 0)) ? profileDropDown.Items[profileDropDown.SelectedIndex].Text : "";
        switch (updateState)
        {
            case PlayButtonState.Download: return Text.PlayButtonDownloadToolTip;
            case PlayButtonState.Downloading: return Text.PlayButtonDownloadToolTip;
            case PlayButtonState.Select11: return Text.PlayButtonSelect11ToolTip;
            case PlayButtonState.Install: return playButton.ToolTip = HelperMethods.GetText(Text.PlayButtonInstallToolTip, profileName);
            case PlayButtonState.Installing: return Text.PlayButtonInstallingToolTip;
            case PlayButtonState.Play: return HelperMethods.GetText(Text.PlayButtonPlayToolTip, profileName);
            case PlayButtonState.Playing: return Text.PlayButtonPlayingToolTip;
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
            case ApkButtonState.Create: return Text.CreateAPK;
            case ApkButtonState.Creating: return Text.CreatingAPK;
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
                if (profile.Name.Contains("Community Updates"))
                    profile.ProfileNotes = Text.ArchiveNotesCommunityUpdates;
                else
                    profile.ProfileNotes = Text.ArchiveNotesMods + "\n\n" + profile.ProfileNotes;
            }

            profileDropDown.Items.Add(profile.Name);
        }

        // Read the value from the config
        string profIndexString = ReadFromConfig("ProfileIndex");

        // Check if either no profile was found or the setting says that the last current profile didn't exist
        if (profileDropDown.Items.Count == 0)
            profileIndex = null;
        else
        {
            // We know that profiles exist at this point, so we're going to point it to 0 instead so the following code doesn't fail
            if ((profIndexString == "null") || String.IsNullOrWhiteSpace(profIndexString))
                profIndexString = "0";

            // We parse from the settings, and check if profiles got deleted from the last time the launcher has been selected. if yes, we revert the last selection to 0;
            int intParseResult = Int32.Parse(profIndexString);
            profileIndex = intParseResult;
            if (profileIndex >= profileDropDown.Items.Count)
                profileIndex = 0;
            profileDropDown.SelectedIndex = profileIndex.Value;
        }

        // Update stored profiles in the Profile Settings tab
        modSettingsProfileDropDown.SelectedIndex = profileDropDown.Items.Count != 0 ? 0 : -1;

        // Refresh the author and version label on the main tab
        if (profileList.Count > 0)
        {
            profileAuthorLabel.Text = Text.Author + " " + profileList[profileDropDown.SelectedIndex].Author;
            profileVersionLabel.Text = Text.VersionLabel + " " + profileList[profileDropDown.SelectedIndex].Version;
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

    /// <summary>Enables and changes colors for <see cref="customMirrorTextBox"/> and <see cref="mirrorDropDown"/> accordingly.</summary>
    private void EnableMirrorControlsAccordingly()
    {
        bool enabled = (bool)customMirrorCheck.Checked;
        customMirrorTextBox.Enabled = enabled;
        mirrorDropDown.Enabled = !enabled;
        // Not sure why the dropdown menu needs this hack, but the textBox does not.
        //TODO: eto feature request
        if (OS.IsWindows)
            mirrorDropDown.TextColor = mirrorDropDown.Enabled ? colorGreen : colorInactive;
        mirrorLabel.TextColor = !enabled ? colorGreen : colorInactive;
    }
}