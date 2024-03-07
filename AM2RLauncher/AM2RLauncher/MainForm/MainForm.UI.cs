﻿using AM2RLauncherLib;
using AM2RLauncherLib.XML;
using AM2RLauncher.Language;
using AM2RLauncher.Properties;
using Eto.Drawing;
using Eto.Forms;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace AM2RLauncher;

/// <summary>
/// Basically our Launcher window.
/// </summary>
public partial class MainForm : Form
{
    /// <summary>
    /// Our log object, that handles logging the current execution to a file.
    /// </summary>
    private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));

    /// <summary>
    /// The current Launcher version.
    /// </summary>
    private const string VERSION = Core.Version;

    /// <summary>
    /// This variable has the current global state of the Launcher.
    /// </summary>
    private static PlayButtonState updateState = PlayButtonState.Download;
    /// <summary>
    /// This variable has the current global statue of the <see cref="apkButton"/>.
    /// </summary>
    private static ApkButtonState apkButtonState = ApkButtonState.Create;

    /// <summary>
    /// Stores the index for <see cref="profileDropDown"/>.
    /// </summary>
    private static int? profileIndex;

    /// <summary>
    /// Stores the current mirror from either <see cref="currentMirror"/> or <see cref="customMirrorTextBox"/>.
    /// </summary>
    private static string currentMirror;

    /// <summary>
    /// Used for Mutex, checks if there's only a single instance of the Launcher running.
    /// </summary>
    private static bool singleInstance;

    // This mutex needs to CONTINUE existing for the entire application's lifetime, or else the rest of this won't ever work!
    // We're basically using it to key a thread and scan for other instances of that tag.
    // ReSharper disable once UnusedMember.Local - needs to exist
    private readonly Mutex mutex = new Mutex(true, "AM2RLauncher", out singleInstance);

    public MainForm()
    {
        // Exit if we're already running the AM2RLauncher
        // Thanks, StackOverflow! https://stackoverflow.com/q/184084
        if (!singleInstance)
        {
            // If on Windows, set the original app to the foreground window to prevent confusion
            if (OS.IsWindows)
            {
                Process current = Process.GetCurrentProcess();
                Process process = Process.GetProcessesByName(current.ProcessName).First(p => p.Id == current.Id);
                if (process != null)
                    Core.SetForegroundWindow(process.MainWindowHandle);
            }
            Environment.Exit(0);
        }

        log.Info("Mutex check passed. Entering main thread.");
        log.Info($"Current Launcher Version: {VERSION}");
        log.Info($"Current Platform-ID is: {Platform.ID}");
        log.Info($"Current OS is: {OS.Name}");

        // Set the Current Directory to the path the Launcher is located. Fixes some relative path issues.
        Environment.CurrentDirectory = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory) ?? Environment.CurrentDirectory;
        log.Info($"Launcher Location is at {Environment.CurrentDirectory}. Used as CWD.");

        // Set the language to what User wanted or choose local language
        string userLanguage = ReadFromConfig("Language").ToLower();
        if (!userLanguage.Equals("default"))
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultures(CultureTypes.AllCultures).First(c => c.NativeName.ToLower().Contains(userLanguage));

        log.Info($"Language has been set to: {Thread.CurrentThread.CurrentUICulture.EnglishName}");

        #region VARIABLE INITIALIZATION
        log.Info("Beginning UI initialization...");

        Bitmap am2rIcon = new Bitmap(Resources.AM2RIcon);

        // System tray indicator
        ButtonMenuItem showButton = new ButtonMenuItem { Text = Text.TrayButtonShow };
        trayIndicator = new TrayIndicator
        {
            Menu = new ContextMenu(showButton),
            Title = "AM2RLauncher",
            Visible = false,
            Image = am2rIcon
        };

        // Create MenuBar with defaults for mac
        if (OS.IsMac)
            Menu = new MenuBar();

        // Custom splash texts
        string splash = Splash.GetSplash();
        log.Info($"Randomly chosen splash: {splash}");

        // Create mirror list
        mirrorList = CrossPlatformOperations.GenerateMirrorList();

        // We do this as a list<listItem> for 1) make this dynamic and 2) make ETO happy
        List<ListItem> mirrorDescriptionList = new List<ListItem>();
        // Add each entry dynamically instead of hard-coding it to two. If we have neither a github or gitlab mirror, we use the mirror itself as text
        foreach (string mirror in mirrorList)
        {
            string text = mirror;
            if (text.Contains("github.com")) text = Text.MirrorGithubText;
            else if (text.Contains("gitlab.com")) text = Text.MirrorGitlabText;
            mirrorDescriptionList.Add(new ListItem { Key = mirror, Text = text });
        }
        #endregion

        Icon = new Icon(1f, am2rIcon);
        Title = $"AM2RLauncher {VERSION}: {splash}";
        MinimumSize = new Size(550, 500);
        // TODO: for some reason, this doesn't work on Linux. Was reported at eto, stays here until its fixed
        ClientSize = new Size(Int32.Parse(ReadFromConfig("Width")), Int32.Parse(ReadFromConfig("Height")));
        // Workaround for above problem
        if (OS.IsWindows && (ClientSize.Width < 550))
            ClientSize = new Size(550, ClientSize.Height);
        if (OS.IsWindows && (ClientSize.Height < 500))
            ClientSize = new Size(ClientSize.Width, 500);
        log.Info($"Start the launcher with Size: {ClientSize.Width}, {ClientSize.Height}");
        if (Boolean.Parse(ReadFromConfig("IsMaximized"))) Maximize();

        Drawable drawable = new Drawable { BackgroundColor = LauncherColors.BGNoAlpha };

        // Drawable paint event
        drawable.Paint += DrawablePaintEvent;
        // Some systems don't call the paintEvent by default and only do so after actual resizing
        if (OS.IsMac)
            LoadComplete += (_, _) => { Size = new Size(Size.Width + 1, Size.Height); Size = new Size(Size.Width - 1, Size.Height);};

        #region MAIN WINDOW

        // Center buttons/interface panel
        DynamicLayout centerInterface = new DynamicLayout();

        // PLAY button
        playButton = new BigColorButton(Text.Play);

        centerInterface.AddRow(playButton);

        // TODO: consider unifying most spacers to the same unit?
        // 2px spacer between playButton and apkButton
        centerInterface.AddRow(new Spacer(2));

        // APK button
        apkButton = new BigColorButton(Text.CreateAPK);

        centerInterface.AddRow(apkButton);

        progressBar = new ProgressBar
        {
            Visible = false,
            Height = 15
        };

        // 4px spacer between APK button and progressBar
        centerInterface.AddRow(new Spacer(4));

        centerInterface.AddRow(progressBar);

        progressLabel = new Label
        {
            BackgroundColor = LauncherColors.BG,
            Height = 15,
            Text = "",
            TextColor = LauncherColors.Green,
            Visible = false
        };

        centerInterface.AddRow(progressLabel);

        // 3px spacer between progressBar and profile label
        centerInterface.AddRow(new Spacer(3));

        profileLabel = new Label
        {
            BackgroundColor = LauncherColors.BG,
            Height = 15,
            Text = Text.CurrentProfile,
            TextColor = LauncherColors.Green
        };

        centerInterface.AddRow(profileLabel);

        // Profiles dropdown

        // Yes, we know this looks horrific on GTK. Sorry.
        // We're not exactly in a position to rewrite the entire DropDown object as a Drawable child, but if you want to, you're more than welcome!
        // Mac gets a default BackgroundColor because it looks waaaaaaay better.
        profileDropDown = new DropDown
        {
            
            TextColor = LauncherColors.Green,
            BackgroundColor = OS.IsWindows ? LauncherColors.BGNoAlpha : new Color()
        };
        // In order to not have conflicting theming, we just always respect the users theme for dropdown on GTK.
        if (OS.IsLinux)
            profileDropDown = new DropDown();

        profileDropDown.DataStore = profileList;

        centerInterface.AddRow(profileDropDown);

        // Profiles label
        profileAuthorLabel = new Label
        {
            BackgroundColor = LauncherColors.BG,
            Height = 16,
            TextColor = LauncherColors.Green
        };

        centerInterface.AddRow(profileAuthorLabel);

        profileVersionLabel = new Label
        {
            BackgroundColor = LauncherColors.BG,
            Height = 16,
            TextColor = LauncherColors.Green
        };

        centerInterface.AddRow(profileVersionLabel);

        saveWarningLabel = new Label
        {
            Visible = false,
            BackgroundColor = LauncherColors.BG,
            Width = 20,
            Height = 55,
            Text = Text.SaveLocationWarning,
            TextColor = LauncherColors.Red
        };

        centerInterface.AddRow(saveWarningLabel);
        
        // Social buttons
        var redditButton = new URLImageButton(Resources.redditIcon48, "https://www.reddit.com/r/AM2R", Text.RedditToolTip);
        var githubButton = new URLImageButton(Resources.githubIcon48, "https://www.github.com/AM2R-Community-Developers", Text.GithubToolTip);
        var youtubeButton = new URLImageButton(Resources.youtubeIcon48, "https://www.youtube.com/c/AM2RCommunityUpdates", Text.YoutubeToolTip);
        var discordButton = new URLImageButton(Resources.discordIcon48, "https://discord.gg/nk7UYPbd5u", Text.DiscordToolTip);
        var matrixButton = new URLImageButton(Resources.matrixIcon48, "https://matrix.to/#/#am2r-space:matrix.org", Text.MatrixToolTip);
        
        // Social button panel
        DynamicLayout socialPanel = new DynamicLayout();
        socialPanel.BeginVertical();
        socialPanel.AddRow(redditButton);
        socialPanel.AddRow(githubButton);
        socialPanel.AddRow(youtubeButton);
        socialPanel.AddRow(discordButton);
        socialPanel.AddRow(matrixButton);
        socialPanel.EndVertical();

        // Version number label
        Label versionLabel = new Label
        {
            Text = $"v{VERSION}{(OS.IsThisRunningFromWINE ? "-WINE" : "")}",
            Width = 48, TextAlignment = TextAlignment.Right, TextColor = LauncherColors.Green,
            Font = new Font(SystemFont.Default, 12)
        };

        // Tie everything together
        DynamicLayout mainLayout = new DynamicLayout();

        mainLayout.BeginHorizontal();
        mainLayout.AddColumn(null, socialPanel);

        mainLayout.AddSpace();
        mainLayout.AddColumn(null, centerInterface, null);
        mainLayout.AddSpace();

        // Yes, I'm hard-coding this string. Linux users can english.
        Label wineLabel = OS.IsThisRunningFromWINE ? new Label { Text = "Unsupported", TextColor = LauncherColors.Red, TextAlignment = TextAlignment.Right } : null;
        mainLayout.AddColumn(versionLabel, wineLabel);

        drawable.Content = mainLayout;

        #endregion

        #region TABS

        #region MAIN PAGE
        // [MAIN PAGE]
        TabPage mainPage = new TabPage
        {
            BackgroundColor = LauncherColors.BGNoAlpha,
            Text = Text.PlayTab,
            Content = drawable
        };
        #endregion

        #region CHANGELOG PAGE
        // [CHANGELOG]
        Uri changelogUri = new Uri("https://am2r-community-developers.github.io/DistributionCenter/changelog.html");
        WebView changelogWebView = new WebView { Url = changelogUri };

        if (OS.IsUnix && !Core.IsInternetThere)
            changelogWebView = new WebView();

        Label changelogNoConnectionLabel = new Label
        {
            Text = Text.NoInternetConnection,
            TextColor = LauncherColors.Green,
            TextAlignment = TextAlignment.Center
        };

        TabPage changelogPage = new TabPage
        {
            BackgroundColor = LauncherColors.BGNoAlpha,
            Text = Text.ChangelogTab,

            Content = new TableLayout
            {
                Rows =
                {
                    changelogWebView
                }
            }
        };

        #endregion

        #region NEWS PAGE

        // [NEWS]
        Uri newsUri = new Uri("https://am2r-community-developers.github.io/DistributionCenter/news.html");
        WebView newsWebView = new WebView { Url = newsUri };

        //TODO: why exactly is this check necessary?
        if (OS.IsUnix && !Core.IsInternetThere)
            newsWebView = new WebView();

        Label newsNoConnectionLabel = new Label
        {
            Text = Text.NoInternetConnection,
            TextColor = LauncherColors.Green,
            TextAlignment = TextAlignment.Center
        };

        TabPage newsPage = new TabPage
        {
            Text = Text.NewsTab,
            BackgroundColor = LauncherColors.BGNoAlpha,

            Content = new TableLayout
            {
                Rows =
                {
                    newsWebView
                }
            }
        };

        //TODO: this is hack because on linux / mac the other way doesn't work. eto issue?
        if (OS.IsUnix && !Core.IsInternetThere)
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

        #endregion

        #region SETTINGS PAGE

        // [LAUNCHER SETTINGS]
        DynamicLayout settingsLayout = new DynamicLayout();

        // LanguageLabel
        Label languageLabel = new Label
        {
            Text = Text.LanguageNotice,
            TextColor = LauncherColors.Green
        };

        // Language DropDown menu
        List<ListItem> languageList = new List<ListItem>
        {
            Text.SystemLanguage,
            "Deutsch",
            "English",
            "Español",
            "Français",
            "Italiano",
            "Português",
            "Русский",
            "日本語",
            "中文(简体)"
        };

        languageDropDown = new DropDown
        {
            TextColor = LauncherColors.Green,
            BackgroundColor = OS.IsWindows ? LauncherColors.BGNoAlpha : new Color()
        };
        if (OS.IsLinux)
            languageDropDown = new DropDown();

        languageDropDown.Items.AddRange(languageList);

        string tmpLanguage = ReadFromConfig("Language");
        if (tmpLanguage == "Default")
            languageDropDown.SelectedIndex = 0;
        else
            languageDropDown.SelectedIndex = languageDropDown.Items.IndexOf(languageDropDown.Items.FirstOrDefault(x => x.Text.Equals(tmpLanguage)));
        if (languageDropDown.SelectedIndex == -1)
        {
            log.Info($"User has tried to use {tmpLanguage} as a Language, but it was not found. Reverting to System Language");
            languageDropDown.SelectedIndex = 0;
        }

        autoUpdateAM2RCheck = new LauncherCheckbox(Text.AutoUpdateAM2R, Boolean.Parse(ReadFromConfig("AutoUpdateAM2R")));
        autoUpdateLauncherCheck = new LauncherCheckbox(Text.AutoUpdateLauncher, Boolean.Parse(ReadFromConfig("AutoUpdateLauncher")));
        hqMusicPCCheck = new LauncherCheckbox(Text.HighQualityPC, Boolean.Parse(ReadFromConfig("MusicHQPC")));
        hqMusicAndroidCheck = new LauncherCheckbox(Text.HighQualityAndroid, Boolean.Parse(ReadFromConfig("MusicHQAndroid")));
        profileDebugLogCheck = new LauncherCheckbox(Text.ProfileDebugCheckBox, Boolean.Parse(ReadFromConfig("ProfileDebugLog")));
        
        // Mirror list
        mirrorLabel = new Label
        {
            Text = Text.DownloadSource,
            TextColor = LauncherColors.Green
        };

        mirrorDropDown = new DropDown
        {
            TextColor = LauncherColors.Green,
            BackgroundColor = OS.IsWindows ? LauncherColors.BGNoAlpha : new Color()
        };
        if (OS.IsLinux)
            mirrorDropDown = new DropDown();

        mirrorDropDown.Items.AddRange(mirrorDescriptionList);   // As above, find a way to get this inside the dropDown definition
        int mirrorIndex = Int32.Parse(ReadFromConfig("MirrorIndex"));
        if (mirrorIndex >= mirrorDropDown.Items.Count) mirrorIndex = 0;
        mirrorDropDown.SelectedIndex = mirrorIndex;

        currentMirror = mirrorList[mirrorDropDown.SelectedIndex];

        // Custom mirror
        customMirrorCheck = new LauncherCheckbox(Text.CustomMirrorCheck, Boolean.Parse(ReadFromConfig("CustomMirrorEnabled")));

        customMirrorTextBox = new TextBox
        {
            Text = ReadFromConfig("CustomMirrorText"),
            BackgroundColor = LauncherColors.BGNoAlpha,
            TextColor = LauncherColors.Green
        };

        EnableMirrorControlsAccordingly();

        settingsLayout.BeginHorizontal();
        settingsLayout.AddSpace();
        List<Control> settingsElements = new List<Control>
        {
            null, 
            languageLabel, 
            languageDropDown, 
            autoUpdateAM2RCheck, 
            autoUpdateLauncherCheck, 
            hqMusicPCCheck, 
            hqMusicAndroidCheck, 
            profileDebugLogCheck,
            mirrorLabel, 
            mirrorDropDown, 
            customMirrorCheck, 
            customMirrorTextBox, 
            null
        };
        #if NOAUTOUPDATE
        settingsElements.Remove(autoUpdateLauncherCheck);
        #endif
        settingsLayout.AddColumn(settingsElements.ToArray());
        settingsLayout.AddSpace();

        TabPage settingsPage = new TabPage
        {
            BackgroundColor = LauncherColors.BGNoAlpha,
            Content = settingsLayout,
            Text = Text.LauncherSettingsTab
        };

        #endregion

        #region MODSETTINGS PAGE

        // [MOD SETTINGS]
        DynamicLayout modSettingsLayout = new DynamicLayout();

        addModButton = new SmallColorButton(Text.AddNewMod);

        Label modSpacer = new Spacer(14);

        settingsProfileLabel = new Label
        {
            Text = Text.CurrentProfile,
            TextColor = LauncherColors.Green,
            Width = 275
        };

        modSettingsProfileDropDown = new DropDown
        {
            TextColor = LauncherColors.Green,
            BackgroundColor = OS.IsWindows ? LauncherColors.BGNoAlpha : new Color()
        };

        // In order to not have conflicting theming, we just always respect the users theme for dropdown on GTK.
        if (OS.IsLinux)
            modSettingsProfileDropDown = new DropDown();

        modSettingsProfileDropDown.DataStore = profileDropDown.DataStore;
        modSettingsProfileDropDown.SelectedIndexBinding.Bind(profileDropDown, p => p.SelectedIndex);
        
        desktopShortcutButton = new SmallColorButton(Text.CreateShortcut);
        profileButton = new SmallColorButton(Text.OpenProfileFolder);
        saveButton = new SmallColorButton(Text.OpenSaveFolder);
        updateModButton = new SmallColorButton(Text.UpdateModButtonText);
        deleteModButton = new SmallColorButton(Text.DeleteModButtonText);

        profileNotesTextArea = new TextArea
        {
            ReadOnly = true,
            BackgroundColor = LauncherColors.BGNoAlpha,
            TextColor = LauncherColors.Inactive,
            SpellCheck = false,
            Width = 275,
            Height = 150
        };

        modSettingsLayout.BeginHorizontal();
        modSettingsLayout.AddSpace();
        modSettingsLayout.AddColumn(null, addModButton, modSpacer, settingsProfileLabel, modSettingsProfileDropDown, desktopShortcutButton, profileButton, saveButton, updateModButton, deleteModButton, profileNotesTextArea, null);
        modSettingsLayout.AddSpace();

        TabPage modSettingsPage = new TabPage
        {
            BackgroundColor = LauncherColors.BGNoAlpha,
            Content = modSettingsLayout,
            Text = Text.ModSettingsTab
        };

        #endregion

        #endregion

        Content = new TabControl
        {
            Pages =
            {
                mainPage,
                changelogPage,
                newsPage,
                settingsPage,
                modSettingsPage
            }
        };

        #region EVENTS
        log.Info("All UI objects have been initialized, UI has been set up.");
        log.Info("Beginning event linkage...");

        Closing += MainFormClosing;
        showButton.Click += ShowButtonClick;
        profileDropDown.SelectedIndexChanged += ProfileDropDownSelectedIndexChanged;
        languageDropDown.SelectedIndexChanged += LanguageDropDownSelectedIndexChanged;
        autoUpdateAM2RCheck.CheckedChanged += AutoUpdateAM2RCheckChanged;
        autoUpdateLauncherCheck.CheckedChanged += AutoUpdateLauncherCheckChanged;
        hqMusicAndroidCheck.CheckedChanged += HQMusicAndroidCheckChanged;
        hqMusicPCCheck.CheckedChanged += HQMusicPCCheckChanged;
        customMirrorCheck.CheckedChanged += CustomMirrorCheckChanged;
        apkButton.Click += ApkButtonClickEvent;
        apkButton.LoadComplete += (_, _) => UpdateApkState();
        profileDropDown.LoadComplete += (_, _) => UpdateProfileState();
        playButton.Click += PlayButtonClickEvent;
        playButton.LoadComplete += PlayButtonLoadComplete;
        customMirrorTextBox.LostFocus += CustomMirrorTextBoxLostFocus;
        mirrorDropDown.SelectedIndexChanged += MirrorDropDownSelectedIndexChanged;
        modSettingsLayout.LoadComplete += ProfileLayoutLoadComplete;
        addModButton.Click += AddModButtonClicked;
        desktopShortcutButton.Click += DesktopShortcutButtonClicked;
        profileButton.Click += ProfileDataButtonClickEvent;
        saveButton.Click += SaveButtonClickEvent;
        modSettingsProfileDropDown.SelectedIndexChanged += ModSettingsProfileDropDownSelectedIndexChanged;
        deleteModButton.Click += DeleteModButtonClicked;
        updateModButton.Click += UpdateModButtonClicked;
        profileDebugLogCheck.CheckedChanged += ProfileDebugLogCheckedChanged;

        //TODO: Retest if these now work on mac
        newsWebView.DocumentLoaded += (_, _) => ChangeToEmptyPageOnNoInternet(newsPage, newsNoConnectionLabel);
        changelogWebView.DocumentLoaded += (_, _) => ChangeToEmptyPageOnNoInternet(changelogPage, changelogNoConnectionLabel);

        log.Info("Events linked successfully.");

        #endregion
    }

    #region CONTROL VARIABLES

    // Visual studio does it like this for normal winforms projects, so I just used the same format.

    /// <summary>The tray indicator</summary>
    private readonly TrayIndicator trayIndicator;

    /// <summary><see cref="List{T}"/> of <see cref="ProfileXML"/>s, used for actually working with profile data.</summary>
    private ObservableCollection<ProfileXML> profileList = new ObservableCollection<ProfileXML>();

    /// <summary>The planet Background.</summary>
    private readonly Bitmap formBG = new Bitmap(Resources.bgCentered);
    
    // Mirror lists
    /// <summary><see cref="List{String}"/> of mirror <see cref="string"/>s, used for actually working with mirrors.</summary>
    private readonly List<string> mirrorList;

    /// <summary>A <see cref="ColorButton"/> that acts as the main Button</summary>
    private readonly ColorButton playButton;
    /// <summary>A <see cref="ColorButton"/> which is only used for creating APK's</summary>
    private readonly ColorButton apkButton;
    /// <summary>A <see cref="ColorButton"/> that is used to add mods.</summary>
    private readonly ColorButton addModButton;
    /// <summary>A <see cref="ColorButton"/> that will create a desktop shortcut of the current profile.</summary>
    private readonly ColorButton desktopShortcutButton;
    /// <summary>A <see cref="ColorButton"/> that will open the game files directory for the selected mod.</summary>
    private readonly ColorButton profileButton;
    /// <summary>A <see cref="ColorButton"/> that will open the save directory for the selected mod.</summary>
    private readonly ColorButton saveButton;
    /// <summary>A <see cref="ColorButton"/> that is used to update a mod</summary>
    private readonly ColorButton updateModButton;
    /// <summary>A <see cref="ColorButton"/> that is used to delete a mod</summary>
    private readonly ColorButton deleteModButton;

    /// <summary>The <see cref="Label"/> that entitles <see cref="profileDropDown"/>.</summary>
    private readonly Label profileLabel;
    /// <summary>The <see cref="Label"/> that gives author information for <see cref="profileDropDown"/>.</summary>
    private readonly Label profileAuthorLabel;
    /// <summary>The <see cref="Label"/> that gives version information for <see cref="profileDropDown"/>.</summary>
    private readonly Label profileVersionLabel;
    /// <summary>The <see cref="Label"/> that gives information for <see cref="mirrorDropDown"/>.</summary>
    private readonly Label mirrorLabel;
    /// <summary>The <see cref="Label"/> that gives information for <see cref="modSettingsProfileDropDown"/>.</summary>
    private readonly Label settingsProfileLabel;
    /// <summary>The <see cref="Label"/> that compliments <see cref="progressBar"/>.</summary>
    private readonly Label progressLabel;
    /// <summary>The <see cref="Label"/> that gives a warning if the current selected <see cref="ProfileXML"/> shares the same save location has default AM2R.</summary>
    private readonly Label saveWarningLabel;

    /// <summary>A <see cref="CheckBox"/>, that indicates whether to automatically update AM2R or not.</summary>
    private readonly CheckBox autoUpdateAM2RCheck;
    /// <summary>A <see cref="CheckBox"/>, that indicates whether to automatically update the AM2RLauncher or not.</summary>
    private readonly CheckBox autoUpdateLauncherCheck;
    /// <summary>A <see cref="CheckBox"/>, that indicates whether to use a custom mirror or not.</summary>
    private readonly CheckBox customMirrorCheck;
    /// <summary>A <see cref="CheckBox"/>, that indicates whether to use HQ Music when patching to PC or not.</summary>
    private readonly CheckBox hqMusicPCCheck;
    /// <summary>A <see cref="CheckBox"/>, that indicates whether to use HQ Music when patching to Android or not.</summary>
    private readonly CheckBox hqMusicAndroidCheck;
    /// <summary>A <see cref="CheckBox"/>, that indicates whether to create debug logs for profiles.</summary>
    private readonly CheckBox profileDebugLogCheck;

    /// <summary>A <see cref="DropDown"/> where languages can be chosen.</summary>
    private readonly DropDown languageDropDown;
    /// <summary>A <see cref="DropDown"/> where mirrors can be chosen.</summary>
    private readonly DropDown mirrorDropDown;
    /// <summary>A <see cref="DropDown"/> where profiles can be chosen.</summary>
    private readonly DropDown profileDropDown;
    /// <summary>A <see cref="DropDown"/> where profiles can be chosen (located in Profile Settings).</summary>
    private readonly DropDown modSettingsProfileDropDown;

    /// <summary>A <see cref="TextBox"/>, where the user can input their custom mirror.</summary>
    private readonly TextBox customMirrorTextBox;

    /// <summary>A <see cref="TextArea"/>, where the notes from the current selected profile in <see cref="modSettingsProfileDropDown"/> are displayed.</summary>
    private readonly TextArea profileNotesTextArea;

    /// <summary>A <see cref="ProgressBar"/> that can be used to show progress for a specific task.</summary>
    private readonly ProgressBar progressBar;

    #endregion
}
