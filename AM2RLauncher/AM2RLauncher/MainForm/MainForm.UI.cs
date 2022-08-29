using AM2RLauncherLib;
using AM2RLauncherLib.XML;
using AM2RLauncher.Language;
using AM2RLauncher.Properties;
using Eto.Drawing;
using Eto.Forms;
using log4net;
using Pablo.Controls;
using System;
using System.Collections.Generic;
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
    /// Indicates whether or not we have established an internet connection.
    /// </summary>
    private static readonly bool isInternetThere = Core.IsInternetThere;

    /// <summary>
    /// Checks if the Launcher is run via WINE.
    /// </summary>
    private static readonly bool isThisRunningFromWine = OS.IsThisRunningFromWine;

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
        // Thanks, StackOverflow! https://stackoverflow.com/questions/184084/how-to-force-c-sharp-net-app-to-run-only-one-instance-in-windows
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

        // Create array from validCount
        profileList = new List<ProfileXML>();

        //TODO: whenever profileDropDown gets rewritten to use a datastore, scrap this
        List<ListItem> profileNames = new List<ListItem>();
        foreach (ProfileXML profile in profileList)
            profileNames.Add(profile.Name);

        // Custom splash texts
        string splash = Splash.GetSplash();
        log.Info($"Randomly chosen splash: {splash}");

        Font smallButtonFont = new Font(SystemFont.Default, 10);

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
        MinimumSize = new Size(500, 400);
        // TODO: for some reason, this doesn't work on Linux. Was reported at eto, stays here until its fixed
        ClientSize = new Size(Int32.Parse(ReadFromConfig("Width")), Int32.Parse(ReadFromConfig("Height")));
        // Workaround for above problem
        if (OS.IsWindows && (ClientSize.Width < 500))
            ClientSize = new Size(500, ClientSize.Height);
        if (OS.IsWindows && (ClientSize.Height < 400))
            ClientSize = new Size(ClientSize.Width, 400);
        log.Info($"Start the launcher with Size: {ClientSize.Width}, {ClientSize.Height}");
        if (Boolean.Parse(ReadFromConfig("IsMaximized"))) Maximize();

        Drawable drawable = new Drawable { BackgroundColor = colorBGNoAlpha };

        // Drawable paint event
        drawable.Paint += DrawablePaintEvent;
        // Some systems don't call the paintEvent by default and only do so after actual resizing
        if (OS.IsMac)
            LoadComplete += (_, _) => { Size = new Size(Size.Width + 1, Size.Height); Size = new Size(Size.Width - 1, Size.Height);};

        #region MAIN WINDOW

        // Center buttons/interface panel
        DynamicLayout centerInterface = new DynamicLayout();

        // PLAY button
        playButton = new ColorButton
        {
            BackgroundColorHover = colorBGHover,
            Height = 40,
            Width = 250,
            TextColor = colorGreen,
            TextColorDisabled = colorInactive,
            BackgroundColor = colorBG,
            FrameColor = colorGreen,
            FrameColorDisabled = colorInactive
        };

        centerInterface.AddRow(playButton);

        //TODO: consider making the spacers global?
        // 2px spacer between playButton and apkButton (Windows only)
        if (OS.IsWindows) centerInterface.AddRow(new Label { BackgroundColor = colorBG, Height = 2 });

        // APK button
        apkButton = new ColorButton
        {
            Text = Text.CreateAPK,
            Height = 40,
            Width = 250,
            TextColor = colorGreen,
            BackgroundColor = colorBG,
            FrameColor = colorGreen,
            BackgroundColorHover = colorBGHover
        };

        centerInterface.AddRow(apkButton);

        progressBar = new ProgressBar
        {
            Visible = false,
            Height = 15
        };

        // 4px spacer between APK button and progressBar (Windows only)
        if (OS.IsWindows) centerInterface.AddRow(new Label { BackgroundColor = colorBG, Height = 4 });

        centerInterface.AddRow(progressBar);

        progressLabel = new Label
        {
            BackgroundColor = colorBG,
            Height = 15,
            Text = "",
            TextColor = colorGreen,
            Visible = false
        };

        centerInterface.AddRow(progressLabel);

        // 3px spacer between progressBar and profile label (Windows only)
        if (OS.IsWindows) centerInterface.AddRow(new Label { BackgroundColor = colorBG, Height = 3 });

        profileLabel = new Label
        {
            BackgroundColor = colorBG,
            Height = 15,
            Text = Text.CurrentProfile,
            TextColor = colorGreen
        };

        centerInterface.AddRow(profileLabel);

        // Profiles dropdown

        // Yes, we know this looks horrific on GTK. Sorry.
        // We're not exactly in a position to rewrite the entire DropDown object as a Drawable child, but if you want to, you're more than welcome!
        // Mac gets a default BackgroundColor because it looks waaaaaaay better.
        profileDropDown = new DropDown
        {
            TextColor = colorGreen,
            BackgroundColor = OS.IsWindows ? colorBGNoAlpha : new Color()
        };
        // In order to not have conflicting theming, we just always respect the users theme for dropdown on GTK.
        if (OS.IsLinux)
            profileDropDown = new DropDown();

        profileDropDown.Items.AddRange(profileNames);   // It's actually more comfortable if it's outside, because of GTK shenanigans

        centerInterface.AddRow(profileDropDown);

        // Profiles label
        profileAuthorLabel = new Label
        {
            BackgroundColor = colorBG,
            Height = 16,
            TextColor = colorGreen
        };

        centerInterface.AddRow(profileAuthorLabel);

        profileVersionLabel = new Label
        {
            BackgroundColor = colorBG,
            Height = 16,
            TextColor = colorGreen
        };

        centerInterface.AddRow(profileVersionLabel);

        saveWarningLabel = new Label
        {
            Visible = false,
            BackgroundColor = colorBG,
            Width = 20,
            Height = 55,
            Text = Text.SaveLocationWarning,
            TextColor = colorRed
        };

        centerInterface.AddRow(saveWarningLabel);


        // Social buttons
        Bitmap redditIcon = new Bitmap(Resources.redditIcon48);
        var redditButton = new ImageButton { ToolTip = Text.RedditToolTip, Image = redditIcon };
        redditButton.Click += (_, _) => CrossPlatformOperations.OpenURL("https://www.reddit.com/r/AM2R");

        Bitmap githubIcon = new Bitmap(Resources.githubIcon48);
        var githubButton = new ImageButton { ToolTip = Text.GithubToolTip, Image = githubIcon };
        githubButton.Click += (_, _) => CrossPlatformOperations.OpenURL("https://www.github.com/AM2R-Community-Developers");

        Bitmap youtubeIcon = new Bitmap(Resources.youtubeIcon48);
        var youtubeButton = new ImageButton { ToolTip = Text.YoutubeToolTip, Image = youtubeIcon };
        youtubeButton.Click += (_, _) => CrossPlatformOperations.OpenURL("https://www.youtube.com/c/AM2RCommunityUpdates");

        Bitmap discordIcon = new Bitmap(Resources.discordIcon48);
        var discordButton = new ImageButton { ToolTip = Text.DiscordToolTip, Image = discordIcon };
        discordButton.Click += (_, _) => CrossPlatformOperations.OpenURL("https://discord.gg/nk7UYPbd5u");
        
        //TODO: this needs a new tooltip
        Bitmap matrixIcon = new Bitmap(Resources.matrixIcon48);
        var matrixButton = new ImageButton { ToolTip = Text.MatrixToolTip, Image = matrixIcon };
        matrixButton.Click += (_, _) => CrossPlatformOperations.OpenURL("https://matrix.to/#/#am2r-space:matrix.org");


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
            Text = $"v{VERSION}{(isThisRunningFromWine ? "-WINE" : "")}",
            Width = 48, TextAlignment = TextAlignment.Right, TextColor = colorGreen,
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
        mainLayout.AddColumn(versionLabel, isThisRunningFromWine ? new Label { Text = "Unsupported", TextColor = colorRed, TextAlignment = TextAlignment.Right } : null);

        drawable.Content = mainLayout;

        #endregion

        #region TABS

        #region MAIN PAGE
        // [MAIN PAGE]
        TabPage mainPage = new TabPage
        {
            BackgroundColor = colorBGNoAlpha,
            Text = Text.PlayTab,
            Content = drawable
        };
        #endregion

        #region CHANGELOG PAGE
        // [CHANGELOG]
        Uri changelogUri = new Uri("https://am2r-community-developers.github.io/DistributionCenter/changelog.html");
        WebView changelogWebView = new WebView { Url = changelogUri };

        if (OS.IsUnix && !isInternetThere)
            changelogWebView = new WebView();

        Label changelogNoConnectionLabel = new Label
        {
            Text = Text.NoInternetConnection,
            TextColor = colorGreen,
            TextAlignment = TextAlignment.Center
        };

        TabPage changelogPage = new TabPage
        {
            BackgroundColor = colorBGNoAlpha,
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
        if (OS.IsUnix && !isInternetThere)
            newsWebView = new WebView();

        Label newsNoConnectionLabel = new Label
        {
            Text = Text.NoInternetConnection,
            TextColor = colorGreen,
            TextAlignment = TextAlignment.Center
        };

        TabPage newsPage = new TabPage
        {
            Text = Text.NewsTab,
            BackgroundColor = colorBGNoAlpha,

            Content = new TableLayout
            {
                Rows =
                {
                    newsWebView
                }
            }
        };

        //TODO: this is hack because on linux / mac the other way doesn't work. eto issue?
        if (OS.IsUnix && !isInternetThere)
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
            TextColor = colorGreen
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
            TextColor = colorGreen,
            BackgroundColor = OS.IsWindows ? colorBGNoAlpha : new Color()
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

        // autoUpdateAM2R checkbox
        autoUpdateAM2RCheck = new CheckBox
        {
            Checked = Boolean.Parse(ReadFromConfig("AutoUpdateAM2R")),
            Text = Text.AutoUpdateAM2R,
            TextColor = colorGreen
        };
        
        // autoUpdateLauncher checkbox
        autoUpdateLauncherCheck = new CheckBox
        {
            Checked = Boolean.Parse(ReadFromConfig("AutoUpdateLauncher")),
            Text = Text.AutoUpdateLauncher,
            TextColor = colorGreen
        };

        // HQ music, PC
        hqMusicPCCheck = new CheckBox
        {
            Checked = Boolean.Parse(ReadFromConfig("MusicHQPC")),
            Text = Text.HighQualityPC,
            TextColor = colorGreen
        };

        // HQ music, Android
        hqMusicAndroidCheck = new CheckBox
        {
            Checked = Boolean.Parse(ReadFromConfig("MusicHQAndroid")),
            Text = Text.HighQualityAndroid,
            TextColor = colorGreen
        };

        // Create game debug logs
        profileDebugLogCheck = new CheckBox
        {
            Checked = Boolean.Parse(ReadFromConfig("ProfileDebugLog")),
            Text = Text.ProfileDebugCheckBox,
            TextColor = colorGreen
        };
        
        // Mirror list
        mirrorLabel = new Label
        {
            Text = Text.DownloadSource,
            TextColor = colorGreen
        };

        mirrorDropDown = new DropDown
        {
            TextColor = colorGreen,
            BackgroundColor = OS.IsWindows ? colorBGNoAlpha : new Color()
        };
        if (OS.IsLinux)
            mirrorDropDown = new DropDown();

        mirrorDropDown.Items.AddRange(mirrorDescriptionList);   // As above, find a way to get this inside the dropDown definition
        int mirrorIndex = Int32.Parse(ReadFromConfig("MirrorIndex"));
        if (mirrorIndex >= mirrorDropDown.Items.Count) mirrorIndex = 0;
        mirrorDropDown.SelectedIndex = mirrorIndex;

        currentMirror = mirrorList[mirrorDropDown.SelectedIndex];

        // Custom mirror
        customMirrorCheck = new CheckBox
        {
            Checked = Boolean.Parse(ReadFromConfig("CustomMirrorEnabled")),
            Text = Text.CustomMirrorCheck,
            TextColor = colorGreen
        };

        customMirrorTextBox = new TextBox
        {
            Text = ReadFromConfig("CustomMirrorText"),
            BackgroundColor = colorBGNoAlpha,
            TextColor = colorGreen
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
            BackgroundColor = colorBGNoAlpha,
            Content = settingsLayout,
            Text = Text.LauncherSettingsTab
        };

        #endregion

        #region MODSETTINGS PAGE

        // [MOD SETTINGS]
        DynamicLayout modSettingsLayout = new DynamicLayout();

        addModButton = new ColorButton
        {
            Text = Text.AddNewMod,
            Font = smallButtonFont,
            Height = 30,
            Width = 275,
            TextColor = colorGreen,
            BackgroundColor = colorBG,
            FrameColor = colorGreen,
            BackgroundColorHover = colorBGHover
        };

        Label modSpacer = new Label
        {
            Height = 14
        };

        settingsProfileLabel = new Label
        {
            Text = Text.CurrentProfile,
            TextColor = colorGreen,
            Width = 275
        };

        modSettingsProfileDropDown = new DropDown
        {
            TextColor = colorGreen,
            BackgroundColor = OS.IsWindows ? colorBGNoAlpha : new Color()
        };

        // In order to not have conflicting theming, we just always respect the users theme for dropdown on GTK.
        if (OS.IsLinux)
            modSettingsProfileDropDown = new DropDown();

        modSettingsProfileDropDown.DataStore = profileDropDown.DataStore;   // It's actually more comfortable if it's outside, because of GTK shenanigans
        modSettingsProfileDropDown.Bind(m => m.SelectedIndex, profileDropDown, p => p.SelectedIndex);

        desktopShortcutButton = profileButton = new ColorButton
        {
            Text = Text.CreateShortcut,
            Font = smallButtonFont,
            Height = 30,
            Width = 275,
            TextColor = colorGreen,
            BackgroundColor = colorBG,
            FrameColor = colorGreen,
            BackgroundColorHover = colorBGHover
        }; 
        
        profileButton = new ColorButton
        {
            Text = Text.OpenProfileFolder,
            Font = smallButtonFont,
            Height = 30,
            Width = 275,
            TextColor = colorGreen,
            BackgroundColor = colorBG,
            FrameColor = colorGreen,
            BackgroundColorHover = colorBGHover
        };

        saveButton = new ColorButton
        {
            Text = Text.OpenSaveFolder,
            Font = smallButtonFont,
            Height = 30,
            Width = 275,
            TextColor = colorGreen,
            BackgroundColor = colorBG,
            FrameColor = colorGreen,
            BackgroundColorHover = colorBGHover
        };

        updateModButton = new ColorButton
        {
            Text = Text.UpdateModButtonText,
            Font = smallButtonFont,
            Height = 30,
            Width = 275,
            TextColor = colorGreen,
            BackgroundColor = colorBG,
            FrameColor = colorGreen,
            BackgroundColorHover = colorBGHover
        };

        deleteModButton = new ColorButton
        {
            Text = Text.DeleteModButtonText,
            Font = smallButtonFont,
            Height = 30,
            Width = 275,
            TextColor = colorGreen,
            BackgroundColor = colorBG,
            FrameColor = colorGreen,
            BackgroundColorHover = colorBGHover
        };

        profileNotesTextArea = new TextArea
        {
            ReadOnly = true,
            BackgroundColor = colorBGNoAlpha,
            TextColor = colorInactive,
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
            BackgroundColor = colorBGNoAlpha,
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
    //TODO: this should be moved into AM2RLauncherLib
    private List<ProfileXML> profileList;

    /// <summary>The planet Background.</summary>
    private readonly Bitmap formBG = new Bitmap(Resources.bgCentered);

    // Colors
    /// <summary>The main green color.</summary>
    private readonly Color colorGreen = Color.FromArgb(142, 188, 35);
    /// <summary>The warning red color.</summary>
    private readonly Color colorRed = Color.FromArgb(188, 10, 35);
    /// <summary>The main inactive color.</summary>
    private readonly Color colorInactive = Color.FromArgb(109, 109, 109);
    /// <summary>The black background color without alpha value.</summary>
    private readonly Color colorBGNoAlpha = Color.FromArgb(10, 10, 10);
    /// <summary>The black background color.</summary>
    // XORG can't display alpha anyway, and Wayland breaks with it.
    // TODO: that sounds like an Eto issue. investigate, try to open eto issue.
    private readonly Color colorBG = OS.IsLinux ? Color.FromArgb(10, 10, 10) : Color.FromArgb(10, 10, 10, 80);
    /// <summary>The lighter green color on hover.</summary>
    private readonly Color colorBGHover = Color.FromArgb(17, 28, 13);

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
    //TODO: Use MVVM bindings: https://github.com/picoe/Eto/wiki/Data-Binding#mvvm-binding
    private readonly DropDown modSettingsProfileDropDown;

    /// <summary>A <see cref="TextBox"/>, where the user can input their custom mirror.</summary>
    private readonly TextBox customMirrorTextBox;

    /// <summary>A <see cref="TextArea"/>, where the notes from the current selected profile in <see cref="modSettingsProfileDropDown"/> are displayed.</summary>
    private readonly TextArea profileNotesTextArea;

    /// <summary>A <see cref="ProgressBar"/> that can be used to show progress for a specific task.</summary>
    private readonly ProgressBar progressBar;

    #endregion
}