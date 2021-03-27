using Eto.Drawing;
using Eto.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Pablo.Controls;
using System.IO;
using System.Linq;
using System.Threading;
using System.Globalization;
using AM2RLauncher.XML;
using log4net;
using log4net.Config;

namespace AM2RLauncher
{
    /// <summary>
    /// Basically our Launcher window.
    /// </summary>
    public partial class MainForm : Form
    {
        // Load reference to logger
        /// <summary>
        /// Our log object, that handles logging the current execution to a file.
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger(typeof(MainForm));

        /// <summary>
        /// An enum, that has possible states for our Launcher.
        /// </summary>
        enum UpdateState
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
        enum ApkButtonState
        {
            Create,
            Creating
        }

        // Splashes
        /// <summary>
        /// A <see cref="string"/>-<see cref="Array"/> of custom splashes.
        /// </summary>
        static readonly private string[] SPLASH_LIST = {
            "The real Ridley is the friends we made along the way.",
            "Now with 100% more Septoggs!",
            "Now with 100% more Blob Throwers!",
            "Speedrun THIS, I dare you.",
            "The broken pipe is a lie.",
            "Overcommitting to April Fool's since 2018.",
            "Also try Metroid II: Return of Samus!",
            "Also try Metroid: Samus Returns!",
            "Trust me, it's an 'unintentional feature.'",
            "Coming soon to a PC near you!",
            "This ain't your parents' Metroid 2!",
            "Now setting boss pattern to S P E E N",
            "You S P E E N me right round, baby!",
            "Wait, I thought Metroid was a guy!",
            "jellyfish is helping yes",
            "Why can't Metroid crawl?",
            "When in doubt, blame GameMaker.",
            "It just works™",
            "Reject C++, return to ABSTRACTION",
            "C# is just C++++ with Windows support.",
            "Use of the Launcher has now been authorized!",
            // GTK splashes begin here
            "Sorry this is ugly, but at least it works.",
            "GTK + QT = 💣",
            "I hope you use Arch, btw",
            "All your Ubuntu are belong to Arch btw.",
            "Help, how do I quit vim!?",
            "The bloat isn't our fault, YoYo Games forced 1GB dependencies!",
            "The quieter you are, the more Metroids you can hear.",
            "What you are referring to as AM2R, is in fact, GNU/AM2R.",
            "GNOME be gone!",
            "Go compile your own girlfriend. This one doesn't count, she's out of your league.",
            "Year 3115, NVIDIA still doesn't support Wayland.",
            "Was a mouse really that expensive, i3 users!?",
            "Imagine using non-free software."
        };

        /// <summary>
        /// The current Launcher version.
        /// </summary>
        static readonly private string VERSION = LauncherUpdater.VERSION;

        /// <summary>
        /// A <see cref="Bitmap"/> of the AM2R icon.
        /// </summary>
        static readonly private Bitmap am2rIcon = new Bitmap(AM2RLauncher.Properties.Resources.AM2RIcon);

        /// <summary>
        /// This variable has the current global state of the Launcher.
        /// </summary>
        static private UpdateState updateState = UpdateState.Download;
        /// <summary>
        /// This variable has the current global statue of the <see cref="apkButton"/>.
        /// </summary>
        static private ApkButtonState apkButtonState = ApkButtonState.Create;

        /// <summary>
        /// Stores the index for <see cref="profileDropDown"/>.
        /// </summary>
        static private int? profileIndex = null;
        /// <summary>
        /// Stores the index for <see cref="mirrorDropDown"/>.
        /// </summary>
        static private int mirrorIndex = 0;
        /// <summary>
        /// Stores the current mirror from either <see cref="currentMirror"/> or <see cref="customMirrorTextBox"/>.
        /// </summary>
        static private string currentMirror;

        /// <summary>
        /// Indicates whether or not we have established an internet connection.
        /// </summary>
        readonly static private bool isInternetThere = IsConnectedToInternet();

        public MainForm()
        {
            // Exit if we're already running the AM2RLauncher

            // Thanks, StackOverflow! https://stackoverflow.com/questions/184084/how-to-force-c-sharp-net-app-to-run-only-one-instance-in-windows
            bool singleInstance = true;

            // This mutex needs to CONTINUE existing for the entire application's lifetime, or else the rest of this won't ever work!
            // We're basically using it to key a thread and scan for other instances of that tag.
            Mutex mutex = new Mutex(true, "AM2RLauncher", out singleInstance);

            if(!singleInstance)
            {
                // If on Windows, set the original app to the foreground window to prevent confusion
                if (Platform.IsWinForms)
                {
                    Process current = Process.GetCurrentProcess();
                    foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                    {
                        if (process.Id != current.Id)
                        {
                            SetForegroundWindow(process.MainWindowHandle);
                            break;
                        }
                    }
                }
                Environment.Exit(0);
            }


            

            log.Info("Mutex check passed. Entering main thread.");
            log.Info("Current Launcher Version: " + VERSION);
            log.Info("Current Platform-ID is: " + Platform.ID);


            //Set the language to what User wanted or choose local language
            string userLanguage = CrossPlatformOperations.ReadFromConfig("Language").ToLower();
            if (!userLanguage.Equals("default"))
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultures(CultureTypes.AllCultures).Where(c => c.NativeName.ToLower().Contains(userLanguage)).First();

            log.Info("Language has been set to: " + Thread.CurrentThread.CurrentUICulture.EnglishName);


            #region VARIABLE INITIALIZATION
            log.Info("Beginning UI initialization...");

            // system tray indicator
            showButton = new ButtonMenuItem() { Text = Language.Text.TrayButtonShow };
            trayIndicator = new TrayIndicator()
            {
                Menu = new ContextMenu(showButton),
                Title = "AM2RLauncher",
                Visible = false,
                Image = am2rIcon
            };

            // Create array from validCount
            profileList = new List<ProfileXML>();

            profileNames = new List<ListItem>();
            for (int i = 0; i < profileList.Count; i++)
            {
                profileNames.Add(profileList[i].Name);
            }

            // Custom splash texts
            Random rng = new Random();
            // Some fanciness so we can have Gtk-only quips, because Gtk visuals are lame.
            string splash = SPLASH_LIST[rng.Next(0, SPLASH_LIST.Length - (Platform.IsGtk ? 0 : 13))];
            log.Info("Randomly chosen splash: " + splash);

            // Load bitmaps
            redditIcon = new Bitmap(AM2RLauncher.Properties.Resources.redditIcon48);
            githubIcon = new Bitmap(AM2RLauncher.Properties.Resources.githubIcon48);
            youtubeIcon = new Bitmap(AM2RLauncher.Properties.Resources.youtubeIcon48);
            discordIcon = new Bitmap(AM2RLauncher.Properties.Resources.discordIcon48);
            formBG = new Bitmap(AM2RLauncher.Properties.Resources.bgCentered);

            // Load colors
            colGreen = Color.FromArgb(142, 188, 35);
            colRed = Color.FromArgb(188,10,35);
            colInactive = Color.FromArgb(109, 109, 109);
            colBGNoAlpha = Color.FromArgb(10, 10, 10);
            colBG = Color.FromArgb(10, 10, 10, 80);
            colBGHover = Color.FromArgb(17, 28, 13);

            Font smallButtonFont = new Font(SystemFont.Default, 10);

            // Create mirror list - eventually this should be platform specific!
            // We do this as a List<Uri> so we can add more dynamically on user input... if necessary.
            mirrorList = CrossPlatformOperations.GenerateMirrorList();

            //create mirror list
            //we do this as a list<listItem> for 1) make this dynamic and 2) make ETO happy
            mirrorDescriptionList = new List<ListItem>
            {
                new ListItem { Key = mirrorList[0], Text = Language.Text.MirrorGithubText },
                new ListItem { Key = mirrorList[1], Text = Language.Text.MirrorGitlabText}
            };

            #endregion

            Icon = new Icon(1f, am2rIcon);
            Title = "AM2RLauncher " + VERSION + ": " + splash;
            MinimumSize = new Size(500, 400);
            ClientSize = new Size(int.Parse(CrossPlatformOperations.ReadFromConfig("Width")), int.Parse(CrossPlatformOperations.ReadFromConfig("Height")));
            log.Info("Start the launcher with Size: " + ClientSize.Width + ", " + ClientSize.Height);
            if (bool.Parse(CrossPlatformOperations.ReadFromConfig("IsMaximized"))) Maximize();

            drawable = new Drawable() { BackgroundColor = colBGNoAlpha };

            // Drawable paint event
            drawable.Paint += DrawablePaintEvent;

            #region MAIN WINDOW

            // Center buttons/interface panel
            var centerInterface = new DynamicLayout();

            // PLAY button
            playButton = new ColorButton
            {
                ToolTip = "",
                BackgroundColorHover = colBGHover,
                Height = 40,
                Width = 250,
                TextColor = colGreen,
                TextColorDisabled = colInactive,
                BackgroundColor = colBG,
                FrameColor = colGreen,
                FrameColorDisabled = colInactive
            };

            UpdateStateMachine();

            SetPlayButtonState(updateState);

            centerInterface.AddRow(playButton);

            // 2px spacer between playButton and apkButton (Windows only)
            if (Platform.IsWinForms) centerInterface.AddRow(new Label { BackgroundColor = colBG, Height = 2 });

            // APK button
            apkButton = new ColorButton
            {
                Text = Language.Text.CreateAPK,
                Height = 40,
                Width = 250,
                TextColor = colGreen,
                BackgroundColor = colBG,
                FrameColor = colGreen,
                BackgroundColorHover = colBGHover
            };

            centerInterface.AddRow(apkButton);

            progressBar = new ProgressBar();

            // The ProgressBar class is DUMB.
            // For some reason, height/visibility do not take effect when done through the manual constructor, so we do it on LoadComplete instead.
            progressBar.LoadComplete += (sender, e) =>
            {
                progressBar.Visible = false;
                progressBar.Height = 15;
            };

            // 4px spacer between APK button and progressBar (Windows only)
            if (Platform.IsWinForms) centerInterface.AddRow(new Label { BackgroundColor = colBG, Height = 4 });

            centerInterface.AddRow(progressBar);

            progressLabel = new Label
            {
                BackgroundColor = colBG,
                Height = 15,
                Text = "",
                TextColor = colGreen,
                Visible = false,
            };

            centerInterface.AddRow(progressLabel);

            // 3px spacer between progressBar and profile label (Windows only)
            if (Platform.IsWinForms) centerInterface.AddRow(new Label { BackgroundColor = colBG, Height = 3 });

            profileLabel = new Label
            {
                BackgroundColor = colBG,
                Height = 15,
                Text = Language.Text.CurrentProfile,
                TextColor = colGreen,
            };

            centerInterface.AddRow(profileLabel);

            // Profiles dropdown

            // Yes, we know this looks horrific on GTK. Sorry. 
            // We're not exactly in a position to rewrite the entire DropDown object as a Drawable child, but if you want to, you're more than welcome!
            profileDropDown = new DropDown
            {
                TextColor = colGreen,
                BackgroundColor = colBGNoAlpha,
            };
            // In order to not have conflicting theming, we just always respect the users theme for dropdown on GTK.
            if (Platform.IsGtk)
                profileDropDown = new DropDown { };

            profileDropDown.Items.AddRange(profileNames);   // It's actually more comfortable if it's outside, because of GTK shenanigans

            centerInterface.AddRow(profileDropDown);

            // Profiles label
            profileAuthorLabel = new Label
            {
                BackgroundColor = colBG,
                Height = 16,
                Text = Language.Text.Author + " ",
                TextColor = colGreen
            };

            centerInterface.AddRow(profileAuthorLabel);

            profileVersionLabel = new Label
            {
                BackgroundColor = colBG,
                Height = 16,
                Text = Language.Text.VersionLabel + " ",
                TextColor = colGreen
            };

            centerInterface.AddRow(profileVersionLabel);

            saveWarningLabel = new Label
            {
                Visible = false,
                BackgroundColor = colBG,
                Width = 20,
                Height = 55,
                Text = Language.Text.SaveLocationWarning,
                TextColor = colRed,
            };

            centerInterface.AddRow(saveWarningLabel);


            // Social buttons
            var redditButton = new ImageButton() { ToolTip = Language.Text.RedditToolTip, Image = redditIcon };
            redditButton.Click += RedditIconOnClick;

            var githubButton = new ImageButton() { ToolTip = Language.Text.GithubToolTip, Image = githubIcon };
            githubButton.Click += GithubIconOnClick;

            var youtubeButton = new ImageButton() { ToolTip = Language.Text.YoutubeToolTip, Image = youtubeIcon };
            youtubeButton.Click += YoutubeIconOnClick;

            var discordButton = new ImageButton() { ToolTip = Language.Text.DiscordToolTip, Image = discordIcon };
            discordButton.Click += DiscordIconOnClick;


            // Social button panel
            var socialPanel = new DynamicLayout();
            socialPanel.BeginVertical();
            socialPanel.AddRow(redditButton);
            socialPanel.AddRow(githubButton);
            socialPanel.AddRow(youtubeButton);
            socialPanel.AddRow(discordButton);
            socialPanel.EndVertical();


            // Version number label
            versionLabel = new Label { Text = "v" + VERSION, Width = 48, TextAlignment = TextAlignment.Right, TextColor = colGreen, Font = new Font(SystemFont.Default, 12) };

            // Tie everything together
            var mainLayout = new DynamicLayout();

            mainLayout.BeginHorizontal();
            mainLayout.AddColumn(null, socialPanel);

            mainLayout.AddSpace();
            mainLayout.AddColumn(null, centerInterface, null);
            mainLayout.AddSpace();

            mainLayout.AddColumn(versionLabel, null);

            drawable.Content = mainLayout;

            #endregion

            #region TABS

            // [MAIN PAGE]
            mainPage = new TabPage
            {
                BackgroundColor = colBGNoAlpha,
                Text = Language.Text.PlayTab,
                Content = drawable
            };

            // [CHANGELOG]

            changelogUri = new Uri("https://am2r-community-developers.github.io/DistributionCenter/changelog.html");

            changelogWebView = new WebView { Url = changelogUri };

            if (Platform.IsGtk && !isInternetThere)
                changelogWebView = new WebView();

            changelogNoConnectionLabel = new Label
            {
                Text = Language.Text.NoInternetConnection,
                TextColor = colGreen
            };

            changelogPage = new TabPage
            {
                BackgroundColor = colBGNoAlpha,
                Text = Language.Text.ChangelogTab,

                Content = new TableLayout
                {
                    Rows =
                    {
                        changelogWebView
                    }

                }
            };

            // [NEWS]
            newsUri = new Uri("https://am2r-community-developers.github.io/DistributionCenter/news.html");
            newsWebView = new WebView { Url = newsUri };

            if (Platform.IsGtk && !isInternetThere)
                newsWebView = new WebView();

            newsNoConnectionLabel = new Label
            {
                Text = Language.Text.NoInternetConnection,
                TextColor = colGreen
            };

            newsPage = new TabPage
            {
                Text = Language.Text.NewsTab,
                BackgroundColor = colBGNoAlpha,

                Content = new TableLayout
                {
                    Rows =
                    {
                        newsWebView
                    }
                }
            };


            if(Platform.IsGtk && !isInternetThere)
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

            // [LAUNCHER SETTINGS]
            DynamicLayout settingsLayout = new DynamicLayout();

            //languageLabel
            languageLabel = new Label
            {
                Text = Language.Text.LanguageNotice,
                TextColor = colGreen
            };

            //language DropDown menu

            List<ListItem> languageList = new List<ListItem>()
            {
                Language.Text.SystemLanguage,
                "Deutsch",
                "English",
                "Español",
                "Français",
                "Italiano",
                "Русский",
                "日本語"
            };

            languageDropDown = new DropDown
            {
                TextColor = colGreen,
                BackgroundColor = colBGNoAlpha,
            };
            if (Platform.IsGtk)
                languageDropDown = new DropDown {};

            languageDropDown.Items.AddRange(languageList);

            var tmpLanguage = CrossPlatformOperations.ReadFromConfig("Language");
            languageDropDown.SelectedIndex = tmpLanguage == "Default" ? 0 : languageDropDown.Items.IndexOf(languageDropDown.Items.Where(x => x.Text.Equals(tmpLanguage)).First());


            // autoUpdate checkbox
            autoUpdateCheck = new CheckBox
            {
                Checked = bool.Parse(CrossPlatformOperations.ReadFromConfig("AutoUpdate")),
                Text = Language.Text.AutoUpdate,
                TextColor = colGreen
            };

            // HQ music, PC
            hqMusicPCCheck = new CheckBox
            {
                Checked = bool.Parse(CrossPlatformOperations.ReadFromConfig("MusicHQPC")),
                Text = Language.Text.HighQualityPC,
                TextColor = colGreen
            };

            // HQ music, Android
            hqMusicAndroidCheck = new CheckBox
            {
                Checked = bool.Parse(CrossPlatformOperations.ReadFromConfig("MusicHQAndroid")),
                Text = Language.Text.HighQualityAndroid,
                TextColor = colGreen
            };

            // create game debug logs
            profileDebugLogCheck = null;
            if (Platform.IsWinForms)
            {
                profileDebugLogCheck = new CheckBox
                {
                    Checked = bool.Parse(CrossPlatformOperations.ReadFromConfig("ProfileDebugLog")),
                    Text = Language.Text.ProfileDebugCheckBox,
                    TextColor = colGreen
                };
            }

            // custom environment variables label
            customEnvVarLabel = new Label();
            if(Platform.IsGtk)
            {
                customEnvVarLabel = new Label
                {
                    Text = Language.Text.CustomEnvVarLabel,
                    TextColor = colGreen
                };
            }

            // custom environment variables textbox
            customEnvVarTextBox = null;
            if(Platform.IsGtk)
            {
                customEnvVarTextBox = new TextBox
                {
                    Text = CrossPlatformOperations.ReadFromConfig("CustomEnvVar"),
                    BackgroundColor = colBGNoAlpha,
                    TextColor = colGreen
                };
            }

            // Mirror list
            mirrorLabel = new Label
            {
                Text = Language.Text.DownloadSource,
                TextColor = colGreen
            };

            mirrorDropDown = new DropDown
            {
                TextColor = colGreen,
                BackgroundColor = colBGNoAlpha,
            };
            if (Platform.IsGtk)
                mirrorDropDown = new DropDown { };

            mirrorDropDown.Items.AddRange(mirrorDescriptionList);   //as above, find a way to get this inside the dropDown definition
            mirrorIndex = (int.Parse(CrossPlatformOperations.ReadFromConfig("MirrorIndex")) < mirrorDropDown.Items.Count) ? int.Parse(CrossPlatformOperations.ReadFromConfig("MirrorIndex")) : 0;
            mirrorDropDown.SelectedIndex =  mirrorIndex;

            currentMirror = mirrorList[mirrorDropDown.SelectedIndex];

            // Custom mirror
            customMirrorCheck = new CheckBox
            {
                Checked = bool.Parse(CrossPlatformOperations.ReadFromConfig("CustomMirrorEnabled")),
                Text = Language.Text.CustomMirrorCheck,
                TextColor = colGreen
            };

            customMirrorTextBox = new TextBox
            {
                Text = CrossPlatformOperations.ReadFromConfig("CustomMirrorText"),
                BackgroundColor = colBGNoAlpha,
                TextColor = colGreen
            };

            settingsLayout.BeginHorizontal();
            settingsLayout.AddSpace();
            settingsLayout.AddColumn(null, languageLabel, languageDropDown, autoUpdateCheck, hqMusicPCCheck, hqMusicAndroidCheck, (Control)profileDebugLogCheck ?? new Label(), customEnvVarLabel, (Control)customEnvVarTextBox ?? new Label(), mirrorLabel, mirrorDropDown, customMirrorCheck, customMirrorTextBox, null);
            settingsLayout.AddSpace();

            TabPage settingsPage = new TabPage
            {
                BackgroundColor = colBGNoAlpha,
                Content = settingsLayout,
                Text = Language.Text.LauncherSettingsTab
            };

            // [MOD SETTINGS]

            DynamicLayout profileLayout = new DynamicLayout();


            addModButton = new ColorButton
            {
                ToolTip = null,
                Text = Language.Text.AddNewMod,
                Font = smallButtonFont,
                Height = 30,
                Width = 275,
                TextColor = colGreen,
                BackgroundColor = colBG,
                FrameColor = colGreen,
                BackgroundColorHover = colBGHover
            };

            Label modSpacer = new Label
            {
                Height = 14
            };

            settingsProfileLabel = new Label
            {
                Text = Language.Text.CurrentProfile,
                TextColor = colGreen,
                Width = 275
            };

            settingsProfileDropDown = new DropDown
            {
                TextColor = colGreen,
                BackgroundColor = colBGNoAlpha,
            };

            // In order to not have conflicting theming, we just always respect the users theme for dropdown on GTK.
            if (Platform.IsGtk)
                settingsProfileDropDown = new DropDown { };

            settingsProfileDropDown.Items.AddRange(profileNames);   // It's actually more comfortable if it's outside, because of GTK shenanigans

            saveButton = new ColorButton
            {
                ToolTip = null,
                Text = Language.Text.OpenSaveFolder,
                Font = smallButtonFont,
                Height = 30,
                Width = 275,
                TextColor = colGreen,
                BackgroundColor = colBG,
                FrameColor = colGreen,
                BackgroundColorHover = colBGHover
            };

            updateModButton = new ColorButton
            {
                ToolTip = null,
                Text = Language.Text.UpdateModButtonText,
                Font = smallButtonFont,
                Height = 30,
                Width = 275,
                TextColor = colGreen,
                BackgroundColor = colBG,
                FrameColor = colGreen,
                BackgroundColorHover = colBGHover
            };

            deleteModButton = new ColorButton
            {
                ToolTip = null,
                Text = Language.Text.DeleteModButtonText,
                Font = smallButtonFont,
                Height = 30,
                Width = 275,
                TextColor = colGreen,
                BackgroundColor = colBG,
                FrameColor = colGreen,
                BackgroundColorHover = colBGHover
            };

            profileNotesTextArea = new TextArea
            {
                ReadOnly = true,
                BackgroundColor = colBGNoAlpha,
                TextColor = colInactive,
                SpellCheck = false,
                Width = 275,
                Height = 150,
                Text = Language.Text.ProfileNotes
            };

            profileLayout.BeginHorizontal();
            profileLayout.AddSpace();
            profileLayout.AddColumn(null, addModButton, modSpacer, settingsProfileLabel, settingsProfileDropDown, saveButton, updateModButton, deleteModButton, profileNotesTextArea, null);
            profileLayout.AddSpace();

            profilePage = new TabPage
            {
                BackgroundColor = colBGNoAlpha,
                Content = profileLayout,
                Text = Language.Text.ProfileSettingsTab
            };

            #endregion

            Content = new TabControl
            {
                Pages =
                {
                    mainPage,

                    changelogPage,

                    newsPage,

                    settingsPage,

                    profilePage
                }
            };

            #region EVENTS
            log.Info("All UI objects have been initalized, UI has been set up.");
            log.Info("Beginning event linkage...");

            Closing += MainformClosing;
            showButton.Click += ShowButtonClick;
            profileDropDown.SelectedIndexChanged += ProfileDropDownSelectedIndexChanged;
            languageDropDown.SelectedIndexChanged += LanguageDropDownSelectedIndexChanged;
            autoUpdateCheck.CheckedChanged += AutoUpdateCheckChanged;
            hqMusicAndroidCheck.CheckedChanged += HqMusicAndroidCheckChanged;
            hqMusicPCCheck.CheckedChanged += HqMusicPCCheckChanged;
            customMirrorCheck.CheckedChanged += CustomMirrorCheckChanged;
            customMirrorCheck.LoadComplete += CustomMirrorCheckLoadComplete;
            apkButton.Click += ApkButtonClickEvent;
            apkButton.LoadComplete += (sender, e) => UpdateApkState();
            profileDropDown.LoadComplete += (sender, e) => UpdateProfileState();
            playButton.Click += PlayButtonClickEvent;
            playButton.LoadComplete += PlayButtonLoadComplete;
            customMirrorTextBox.LostFocus += CustomMirrorTextBoxLostFocus;
            mirrorDropDown.SelectedIndexChanged += MirrorDropDownSelectedIndexChanged;
            profileLayout.LoadComplete += ProfileLayoutLoadComplete;
            addModButton.Click += AddModButtonClicked; 
            saveButton.Click += SaveButtonClickEvent;
            settingsProfileDropDown.SelectedIndexChanged += SettingsProfileDropDownSelectedIndexChanged;
            deleteModButton.Click += DeleteModButtonClicked;
            updateModButton.Click += UpdateModButtonClicked;
            if(Platform.IsWinForms)
                profileDebugLogCheck.CheckedChanged += ProfileDebugLogCheckedChanged;
            if(Platform.IsGtk)
                customEnvVarTextBox.LostFocus += CustomEnvVarTextBoxLostFocus;

            newsWebView.DocumentLoaded += NewsWebViewDocumentLoaded;
            changelogWebView.DocumentLoaded += ChangelogWebViewDocumentLoaded;

            log.Info("Events linked successfully.");

            #endregion
        }

        #region CONTROL VARIABLES

        //Visual studio does it like this for normal winforms projects, so I just used the same format.

        /// <summary>The tray indicator</summary>
        TrayIndicator trayIndicator;
        /// <summary>The "Show" Button on the tray indicator</summary>
        ButtonMenuItem showButton;

        /// <summary><see cref="List{T}"/> of <see cref="ProfileXML"/>s, used for actually working with profile data.</summary>
        List<ProfileXML> profileList;
        /// <summary><see cref="List{T}"/> of <see cref="ListItem"/>s so that Eto's annoying <see cref="IListItem"/> interface is appeased. Used for profile name display in DropDowns.</summary>
        List<ListItem> profileNames;

        //Bitmaps
        /// <summary>The Reddit icon.</summary>
        private Bitmap redditIcon;
        /// <summary>The Github icon.</summary>
        private Bitmap githubIcon;
        /// <summary>The YouTube icon.</summary>
        private Bitmap youtubeIcon;
        /// <summary>The Discord icon.</summary>
        private Bitmap discordIcon;
        /// <summary>The planet Background.</summary>
        private Bitmap formBG;

        // Colors
        /// <summary>The main green color.</summary>
        private Color colGreen;
        /// <summary>The warning red color.</summary>
        private Color colRed;
        /// <summary>The main inactive color.</summary>
        private Color colInactive;
        /// <summary>The black background color without alpha value.</summary>
        private Color colBGNoAlpha;
        /// <summary>The black background color.</summary>
        private Color colBG;
        /// <summary>The lighter green color on hover.</summary>
        private Color colBGHover;

        // Mirror lists
        /// <summary><see cref="List{String}"/> of mirror <see cref="string"/>s, used for actually working with mirrors.</summary>
        private List<string> mirrorList;
        /// <summary><see cref="List{ListItem}"/> of <see cref="ListItem"/> so that Eto's annoying IListItem interface is appeased. Used for mirror name display in DropDowns.</summary>
        private List<ListItem> mirrorDescriptionList;

        // UI Elements
        /// <summary>The main control of the <see cref="mainPage"/>, used to draw the <see cref="formBG"/> and hold the main interface.</summary>
        private Drawable drawable;

        /// <summary>A <see cref="ColorButton"/> that acts as the main Button</summary>
        private ColorButton playButton;
        /// <summary>A <see cref="ColorButton"/> which is only used for creating APK's</summary>
        private ColorButton apkButton;
        /// <summary>A <see cref="ColorButton"/> that is used to add mods.</summary>
        private ColorButton addModButton;
        /// <summary>A <see cref="ColorButton"/> that will open the save directory for the selected mod.</summary>
        private ColorButton saveButton;
        /// <summary>A <see cref="ColorButton"/> that is used to update a mod</summary>
        private ColorButton updateModButton;
        /// <summary>A <see cref="ColorButton"/> that is used to delete a mod</summary>
        private ColorButton deleteModButton;

        /// <summary>The <see cref="Label"/> that gives information for <see cref="languageDropDown"/>.</summary>
        private Label languageLabel;
        /// <summary>The <see cref="Label"/> that entitles <see cref="profileDropDown"/>.</summary>
        private Label profileLabel;
        /// <summary>The <see cref="Label"/> that gives author information for <see cref="profileDropDown"/>.</summary>
        private Label profileAuthorLabel;
        /// <summary>The <see cref="Label"/> that gives version information for <see cref="profileDropDown"/>.</summary>
        private Label profileVersionLabel;
        /// <summary>The <see cref="Label"/> that gives information for <see cref="mirrorDropDown"/>.</summary>
        private Label mirrorLabel;
        /// <summary>The <see cref="Label"/> that displays <see cref="VERSION"/>, aka the current launcher version.</summary>
        private Label versionLabel;
        /// <summary>The <see cref="Label"/> that gives information for <see cref="settingsProfileDropDown"/>.</summary>
        private Label settingsProfileLabel;
        /// <summary>The <see cref="Label"/> that compliments <see cref="progressBar"/>.</summary>
        private Label progressLabel;
        /// <summary>The <see cref="Label"/> that gives a warning on failure to load the <see cref="newsWebView"/>.</summary>
        private Label newsNoConnectionLabel;
        /// <summary>The <see cref="Label"/> that gives a warning on failure to load the <see cref="changelogWebView"/>.</summary>
        private Label changelogNoConnectionLabel;
        /// <summary>The <see cref="Label"/> that gives a warning if the current selected <see cref="ProfileXML"/> shares the same save location has default AM2R.</summary>
        private Label saveWarningLabel;
        /// <summary>The <see cref="Label"/> that describes <see cref="customEnvVarTextBox"/>.</summary>
        private Label customEnvVarLabel;



        /// <summary>A <see cref="CheckBox"/>, that indicates wether to automatically update or not.</summary>
        private CheckBox autoUpdateCheck;
        /// <summary>A <see cref="CheckBox"/>, that indicates wether to use a custom mirror or not.</summary>
        private CheckBox customMirrorCheck;
        /// <summary>A <see cref="CheckBox"/>, that indicates wether to use HQ Music when patching to PC or not.</summary>
        private CheckBox hqMusicPCCheck;
        /// <summary>A <see cref="CheckBox"/>, that indicates wether to use HQ Music when paching to Android or not.</summary>
        private CheckBox hqMusicAndroidCheck;
        /// <summary>A <see cref="CheckBox"/>, that indicates wether to create debug logs for profiles.</summary>
        private CheckBox profileDebugLogCheck;

        /// <summary>A <see cref="DropDown"/> where languages can be chosen.</summary>
        private DropDown languageDropDown;
        /// <summary>A <see cref="DropDown"/> where mirrors can be chosen.</summary>
        private DropDown mirrorDropDown;
        /// <summary>A <see cref="DropDown"/> where profiles can be chosen.</summary>
        private DropDown profileDropDown;
        /// <summary>A <see cref="DropDown"/> where profiles can be chosen (located in Profile Settings).</summary>
        private DropDown settingsProfileDropDown;

        /// <summary>A <see cref="TextBox"/>, where the user can input their custom mirror.</summary>
        private TextBox customMirrorTextBox;
        /// <summary>A <see cref="TextBox"/>, where the user can input their custom environment variables.</summary>
        private TextBox customEnvVarTextBox;

        /// <summary>A <see cref="TextArea"/>, where the notes from the current selected profile in <see cref="settingsProfileDropDown"/> are displayed.</summary>
        private TextArea profileNotesTextArea;

        /// <summary>A <see cref="ProgressBar"/> that can be used to show progress for a specific task.</summary>
        private ProgressBar progressBar;

        /// <summary>The Uri used by <see cref="newsWebView"/>.</summary>
        private Uri newsUri;
        /// <summary>The Uri used by <see cref="changelogWebView"/>.</summary>
        private Uri changelogUri;

        /// <summary>A <see cref="WebView"/> to display the DistributionCenter news page.</summary>
        private WebView newsWebView;
        /// <summary>A <see cref="WebView"/> to display the DistributionCenter changelog page.</summary>
        private WebView changelogWebView;

        /// <summary>A <see cref="TabPage"/> for the Launcher's main interface.</summary>
        private TabPage mainPage;
        /// <summary>A <see cref="TabPage"/> for the Launcher's news integration.</summary>
        private TabPage newsPage;
        /// <summary>A <see cref="TabPage"/> for the Launcher's changelog integration.</summary>
        private TabPage changelogPage;
        /// <summary>A <see cref="TabPage"/> for the Launcher's profile settings.</summary>
        private TabPage profilePage;

        #endregion
    }
}
