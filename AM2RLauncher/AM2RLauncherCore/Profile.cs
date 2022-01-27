using AM2RLauncher.Core.XML;
using LibGit2Sharp;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AM2RLauncher.Core
{
    /// <summary>
    /// An enum, that has possible return codes for <see cref="HelperMethods.CheckIfZipIsAM2R11"/>.
    /// </summary>
    public enum IsZipAM2R11ReturnCodes
    {
        Successful,
        MissingOrInvalidAM2RExe,
        MissingOrInvalidD3DX9_43Dll,
        MissingOrInvalidDataWin,
        GameIsInASubfolder
    }

    /// <summary>
    /// Class that has methods related to AM2RLauncher profiles.
    /// </summary>
    public static class Profile
    {

        /// <summary>
        /// The logger for <see cref="MainForm"/>, used to write any caught exceptions.
        /// </summary>
        private static readonly ILog Log = Core.Log;


        /// <summary>
        /// Caches the result of <see cref="Is11Installed"/> so that we don't extract and verify it too often.
        /// </summary>
        private static bool? isAM2R11InstalledCache = null;

        /// <summary>
        /// Caches the MD5 hash of the provided AM2R_11.zip so we don't end up checking the zip if it hasn't changed.
        /// </summary>
        private static string lastAM2R11ZipMD5 = "";

        /// <summary>
        /// Checks if AM2R 1.1 has been installed already, aka if a valid AM2R 1.1 Zip exists.
        /// </summary>
        /// <returns><see langword="true"/> if yes, <see langword="false"/> if not.</returns>
        public static bool Is11Installed()
        {
            InvalidateAM2R11InstallCache();

            // If we have a cache, return that instead
            if (isAM2R11InstalledCache != null) return isAM2R11InstalledCache.Value;

            string am2r11file = CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip";
            // Return safely if file doesn't exist
            if (!File.Exists(am2r11file)) return false;
            lastAM2R11ZipMD5 = HelperMethods.CalculateMD5(am2r11file);
            var returnCode = Profile.CheckIfZipIsAM2R11(am2r11file);
            // Check if it's valid, if not log it, rename it and silently leave
            if (returnCode != IsZipAM2R11ReturnCodes.Successful)
            {
                Log.Info("Detected invalid AM2R_11 zip with following error code: " + returnCode);
                HelperMethods.RecursiveRollover(am2r11file);
                isAM2R11InstalledCache = false;
                return false;
            }
            isAM2R11InstalledCache = true;
            return true;
        }

        /// <summary>
        /// Invalidates <see cref="isAM2R11InstalledCache"/>.
        /// </summary>
        private static void InvalidateAM2R11InstallCache()
        {
            // If the file exists, and its hash matches with ours, don't invalidate
            if (File.Exists(CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip") &&
                HelperMethods.CalculateMD5(CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip") == lastAM2R11ZipMD5)
                return;

            isAM2R11InstalledCache = null;
        }

        /// <summary>
        /// Git Pulls from the repository.
        /// </summary>
        public static void PullPatchData(Func<TransferProgress, bool> TransferProgressHandlerMethod)
        {
            using (var repo = new Repository(CrossPlatformOperations.CURRENTPATH + "/PatchData"))
            {
                // Permanently undo commits not pushed to remote
                Branch originMaster = repo.Branches.ToList().FirstOrDefault(b => b.FriendlyName.Contains("origin/master") || b.FriendlyName.Contains("origin/main"));

                if (originMaster == null)
                    throw new UserCancelledException("Neither branch 'master' nor branch 'main' could be found! Corrupted or invalid git repo ? Deleting PatchData...");
                
                repo.Reset(ResetMode.Hard, originMaster.Tip);

                // Credential information to fetch

                PullOptions options = new PullOptions();
                options.FetchOptions = new FetchOptions();
                options.FetchOptions.OnTransferProgress += (tp) => TransferProgressHandlerMethod(tp);

                // User information to create a merge commit
                var signature = new Signature("null", "null", DateTimeOffset.Now);

                // Pull
                try
                {
                    Commands.Pull(repo, signature, options);
                }
                catch
                {
                    Log.Error("Repository pull attempt failed!");
                    return;
                }
            }
            Log.Info("Repository pulled successfully.");
        }

        /// <summary>
        /// Installs <paramref name="profile"/>.
        /// </summary>
        /// <param name="profile"><see cref="ProfileXML"/> to be installed.</param>
        public static void InstallProfile(ProfileXML profile, bool useHqMusic, IProgress<int> progress)
        {
            Log.Info("Installing profile " + profile.Name + "...");

            string profilesHomePath = CrossPlatformOperations.CURRENTPATH + "/Profiles";
            string profilePath = profilesHomePath + "/" + profile.Name;

            // Failsafe for Profiles directory
            if (!Directory.Exists(profilesHomePath))
                Directory.CreateDirectory(profilesHomePath);


            // This failsafe should NEVER get triggered, but Miepee's broken this too much for me to trust it otherwise.
            if (Directory.Exists(profilePath))
                Directory.Delete(profilePath, true);

            // Create profile directory
            Directory.CreateDirectory(profilePath);

            // Switch profilePath on Gtk
            if (OS.IsLinux)
            {
                profilePath += "/assets";
                Directory.CreateDirectory(profilePath);
            }
            else if (OS.IsMac)
            {
                // Folder structure for mac is like this:
                // am2r.app -> Contents
                //     -Frameworks (some libs)
                //     -MacOS (runner)
                //     -Resources (asset path)
                profilePath += "/AM2R.app/Contents";
                Directory.CreateDirectory(profilePath);
                Directory.CreateDirectory(profilePath + "/MacOS");
                Directory.CreateDirectory(profilePath + "/Resources");
                profilePath += "/Resources";

                Log.Info("ProfileInstallation: Created folder structure.");
            }

            // Extract 1.1
            ZipFile.ExtractToDirectory(CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip", profilePath);

            // Extracted 1.1
            progress.Report(33);
            Log.Info("Profile folder created and AM2R_11.zip extracted.");

            // Set local datapath for installation files
            var dataPath = CrossPlatformOperations.CURRENTPATH + profile.DataPath;

            string datawin = null, exe = null;

            if (OS.IsWindows)
            {
                datawin = "data.win";
                exe = "AM2R.exe";
            }
            else if (OS.IsLinux)
            {
                datawin = "game.unx";
                // Use the exe name based on the desktop file in the appimage, rather than hardcoding it.
                string desktopContents = File.ReadAllText(CrossPlatformOperations.CURRENTPATH + "/PatchData/data/AM2R.AppDir/AM2R.desktop");
                exe = Regex.Match(desktopContents, @"(?<=Exec=).*").Value;
                Log.Info("According to AppImage desktop file, using \"" + exe + "\" as game name.");
            }
            else if (OS.IsMac)
            {
                datawin = "game.ios";
                exe = "Mac_Runner";
            }
            else
            {
                Log.Error(OS.Name + " does not have valid runner / data.win names!");
            }

            Log.Info("Attempting to patch in " + profilePath);

            if (OS.IsWindows)
            {
                // Patch game executable
                if (profile.UsesYYC)
                {
                    CrossPlatformOperations.ApplyXdeltaPatch(profilePath + "/data.win", dataPath + "/AM2R.xdelta", profilePath + "/" + exe);

                    // Delete 1.1's data.win, we don't need it anymore!
                    File.Delete(profilePath + "/data.win");
                }
                else
                {
                    CrossPlatformOperations.ApplyXdeltaPatch(profilePath + "/data.win", dataPath + "/data.xdelta", profilePath + "/" + datawin);
                    CrossPlatformOperations.ApplyXdeltaPatch(profilePath + "/AM2R.exe", dataPath + "/AM2R.xdelta", profilePath + "/" + exe);
                }
            }
            else if (OS.IsUnix)    // YYC and VM look exactly the same on Linux and Mac so we're all good here.
            {
                CrossPlatformOperations.ApplyXdeltaPatch(profilePath + "/data.win", dataPath + "/game.xdelta", profilePath + "/" + datawin);
                CrossPlatformOperations.ApplyXdeltaPatch(profilePath + "/AM2R.exe", dataPath + "/AM2R.xdelta", profilePath + "/" + exe);
                // Just in case the resulting file isn't chmoddded...
                Process.Start("chmod", "+x  \"" + profilePath + "/" + exe + "\"")?.WaitForExit();

                // These are not needed by linux or Mac at all, so we delete them
                File.Delete(profilePath + "/data.win");
                File.Delete(profilePath + "/AM2R.exe");
                File.Delete(profilePath + "/D3DX9_43.dll");

                // Move exe one directory out on Linux, move to MacOS folder instead on Mac
                if (OS.IsLinux)
                    File.Move(profilePath + "/" + exe, profilePath.Substring(0, profilePath.LastIndexOf("/")) + "/" + exe);
                else
                    File.Move(profilePath + "/" + exe, profilePath.Replace("Resources", "MacOS") + "/" + exe);
            }
            else
            {
                Log.Error(OS.Name + " does not have patching methods!");
            }

            // Applied patch
            if (OS.IsWindows || OS.IsMac) progress.Report(66);
            else if (OS.IsLinux) progress.Report(44); // Linux will take a bit longer, due to appimage creation
            Log.Info("xdelta patch(es) applied.");

            // Install new datafiles
            HelperMethods.DirectoryCopy(dataPath + "/files_to_copy", profilePath);

            // HQ music
            if (!profile.UsesCustomMusic && useHqMusic)
                HelperMethods.DirectoryCopy(CrossPlatformOperations.CURRENTPATH + "/PatchData/data/HDR_HQ_in-game_music", profilePath);


            // Linux post-process
            if (OS.IsLinux)
            {
                string assetsPath = profilePath;
                profilePath = profilePath.Substring(0, profilePath.LastIndexOf("/"));

                // Rename all songs to lowercase
                foreach (var file in new DirectoryInfo(assetsPath).GetFiles())
                    if (file.Name.EndsWith(".ogg") && !File.Exists(file.DirectoryName + "/" + file.Name.ToLower()))
                        File.Move(file.FullName, file.DirectoryName + "/" + file.Name.ToLower());

                // Copy AppImage template to here
                HelperMethods.DirectoryCopy(CrossPlatformOperations.CURRENTPATH + "/PatchData/data/AM2R.AppDir", profilePath + "/AM2R.AppDir/");

                // Safety checks, in case the folders don't exist
                Directory.CreateDirectory(profilePath + "/AM2R.AppDir/usr/bin/");
                Directory.CreateDirectory(profilePath + "/AM2R.AppDir/usr/bin/assets/");

                // Copy game assets to the appimageDir
                HelperMethods.DirectoryCopy(assetsPath, profilePath + "/AM2R.AppDir/usr/bin/assets/");
                File.Copy(profilePath + "/" + exe, profilePath + "/AM2R.AppDir/usr/bin/" + exe);

                progress.Report(66);
                Log.Info("Gtk-specific formatting finished.");

                // Temp save the currentWorkingDirectory and console.error, change it to profilePath and null, call the script, and change it back.
                string workingDir = Directory.GetCurrentDirectory();
                TextWriter cliError = Console.Error;
                Directory.SetCurrentDirectory(profilePath);
                Console.SetError(new StreamWriter(Stream.Null));
                Environment.SetEnvironmentVariable("ARCH", "x86_64");
                Process.Start(CrossPlatformOperations.CURRENTPATH + "/PatchData/utilities/appimagetool-x86_64.AppImage", "-n AM2R.AppDir")?.WaitForExit();
                Directory.SetCurrentDirectory(workingDir);
                Console.SetError(cliError);

                // Clean files
                Directory.Delete(profilePath + "/AM2R.AppDir", true);
                Directory.Delete(assetsPath, true);
                File.Delete(profilePath + "/" + exe);
                if (File.Exists(profilePath + "/AM2R.AppImage")) File.Delete(profilePath + "/AM2R.AppImage");
                File.Move(profilePath + "/" + "AM2R-x86_64.AppImage", profilePath + "/AM2R.AppImage");
            }
            // Mac post-process
            else if (OS.IsMac)
            {
                // Rename all songs to lowercase
                foreach (var file in new DirectoryInfo(profilePath).GetFiles())
                    if (file.Name.EndsWith(".ogg") && !File.Exists(file.DirectoryName + "/" + file.Name.ToLower()))
                        File.Move(file.FullName, file.DirectoryName + "/" + file.Name.ToLower());
                // Loading custom fonts crashes on Mac, so we delete those
                Directory.Delete(profilePath + "/lang/fonts", true);
                // Move Frameworks, Info.plist and PkgInfo over
                HelperMethods.DirectoryCopy(CrossPlatformOperations.CURRENTPATH + "/PatchData/data/Frameworks", profilePath.Replace("Resources", "Frameworks"));
                File.Copy(dataPath + "/Info.plist", profilePath.Replace("Resources", "") + "/Info.plist", true);
                File.Copy(CrossPlatformOperations.CURRENTPATH + "/PatchData/data/PkgInfo", profilePath.Replace("Resources", "") + "/PkgInfo", true);
                //Put profilePath back to what it was before
                profilePath = profilesHomePath + "/" + profile.Name;
            }

            // Copy profile.xml so we can grab data to compare for updates later!
            // tldr; check if we're in PatchData or not
            if (new DirectoryInfo(dataPath)?.Parent?.Name == "PatchData")
                File.Copy(dataPath + "/../profile.xml", profilePath + "/profile.xml");
            else File.Copy(dataPath + "/profile.xml", profilePath + "/profile.xml");

            // Installed datafiles
            progress.Report(100);
            Log.Info("Successfully installed profile " + profile.Name + ".");
        }

        /// <summary>
        /// Checks if <paramref name="profile"/> is installed.
        /// </summary>
        /// <param name="profile">The <see cref="ProfileXML"/> that should be checked for installation.</param>
        /// <returns><see langword="true"/> if yes, <see langword="false"/> if not.</returns>
        public static bool IsProfileInstalled(ProfileXML profile)
        {
            if (OS.IsWindows) return File.Exists(CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name + "/AM2R.exe");
            else if (OS.IsLinux) return File.Exists(CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name + "/AM2R.AppImage");
            else if (OS.IsMac) return Directory.Exists(CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name + "/AM2R.app");

            Log.Error(OS.Name + " can't have profiles installed!");
            return false;
        }

        /// <summary>
        /// Creates an APK of the selected <paramref name="profile"/>.
        /// </summary>
        /// <param name="profile"><see cref="ProfileXML"/> to be compiled into an APK.</param>
        public static void CreateAPK(ProfileXML profile, bool useHqMusic, IProgress<int> progress)
        {
            // Overall safety check just in case of bad situations
            if (!profile.SupportsAndroid)
            {
                progress.Report(100);
                return;
            }
            Log.Info("Creating Android APK for profile " + profile.Name + ".");

            // Create working dir after some cleanup
            string apktoolPath = CrossPlatformOperations.CURRENTPATH + "/PatchData/utilities/android/apktool.jar",
                   uberPath = CrossPlatformOperations.CURRENTPATH + "/PatchData/utilities/android/uber-apk-signer.jar",
                   tempDir = new DirectoryInfo(CrossPlatformOperations.CURRENTPATH + "/temp").FullName,
                   dataPath = CrossPlatformOperations.CURRENTPATH + profile.DataPath;
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            Log.Info("Cleanup, variables, and working directory created.");
            progress.Report(14);

            // Decompile AM2RWrapper.apk
            CrossPlatformOperations.RunJavaJar("\"" + apktoolPath + "\" d \"" + dataPath + "/android/AM2RWrapper.apk\"", tempDir);
            Log.Info("AM2RWrapper decompiled.");
            progress.Report(28);

            // Add datafiles: 1.1, new datafiles, hq music, am2r.ini
            string workingDir = tempDir + "/AM2RWrapper/assets";
            ZipFile.ExtractToDirectory(CrossPlatformOperations.CURRENTPATH + "/AM2R_11.zip", workingDir);
            HelperMethods.DirectoryCopy(dataPath + "/files_to_copy", workingDir);
            
            if (useHqMusic)
                HelperMethods.DirectoryCopy(CrossPlatformOperations.CURRENTPATH + "/PatchData/data/HDR_HQ_in-game_music", workingDir);
            // Yes, I'm aware this is dumb. If you've got any better ideas for how to copy a seemingly randomly named .ini from this folder to the APK, please let me know.
            foreach (FileInfo file in new DirectoryInfo(dataPath).GetFiles().Where(f => f.Name.EndsWith("ini")))
                File.Copy(file.FullName, workingDir + "/" + file.Name);

            Log.Info("AM2R_11.zip extracted and datafiles copied into AM2RWrapper.");
            progress.Report(42);

            // Patch data.win to game.droid
            CrossPlatformOperations.ApplyXdeltaPatch(workingDir + "/data.win", dataPath + "/droid.xdelta", workingDir + "/game.droid");
            Log.Info("game.droid successfully patched.");
            progress.Report(56);

            // Delete unnecessary files
            File.Delete(workingDir + "/AM2R.exe");
            File.Delete(workingDir + "/D3DX9_43.dll");
            File.Delete(workingDir + "/explanations.txt");
            File.Delete(workingDir + "/modifiers.ini");
            File.Delete(workingDir + "/readme.txt");
            File.Delete(workingDir + "/data.win");
            Directory.Delete(workingDir + "/mods", true);
            Directory.Delete(workingDir + "/lang/headers", true);
            if (OS.IsLinux) File.Delete(workingDir + "/icon.png");

            // Modify apktool.yml to NOT compress ogg files
            string apktoolText = File.ReadAllText(workingDir + "/../apktool.yml");
            apktoolText = apktoolText.Replace("doNotCompress:", "doNotCompress:\n- ogg");
            File.WriteAllText(workingDir + "/../apktool.yml", apktoolText);
            Log.Info("Unnecessary files removed, apktool.yml modified to prevent ogg compression.");
            progress.Report(70);

            // Rebuild APK
            CrossPlatformOperations.RunJavaJar("\"" + apktoolPath + "\" b AM2RWrapper -o \"" + profile.Name + ".apk\"", tempDir);
            Log.Info("AM2RWrapper rebuilt into " + profile.Name + ".apk.");
            progress.Report(84);

            // Debug-sign APK
            CrossPlatformOperations.RunJavaJar("\"" + uberPath + "\" -a \"" + profile.Name + ".apk\"", tempDir);

            // Extra file cleanup
            File.Copy(tempDir + "/" + profile.Name + "-aligned-debugSigned.apk", CrossPlatformOperations.CURRENTPATH + "/" + profile.Name + ".apk", true);
            Log.Info(profile.Name + ".apk signed and moved to " + CrossPlatformOperations.CURRENTPATH + "/" + profile.Name + ".apk.");
            HelperMethods.DeleteDirectory(tempDir);

            // Done
            progress.Report(100);
            Log.Info("Successfully created Android APK for profile " + profile.Name + ".");
            CrossPlatformOperations.OpenFolderAndSelectFile(CrossPlatformOperations.CURRENTPATH + "/" + profile.Name + ".apk");
        }

        /// <summary>
        /// Runs the Game, works cross platform.
        /// </summary>
        public static void RunGame(ProfileXML profile, bool useLogging, string envVars = "")
        {
            // These are used on both windows and linux for game logging
            string savePath = OS.IsWindows ? profile.SaveLocation.Replace("%localappdata%", Environment.GetEnvironmentVariable("LOCALAPPDATA"))
                                                  : profile.SaveLocation.Replace("~", CrossPlatformOperations.NIXHOME);
            DirectoryInfo logDir = new DirectoryInfo(savePath + "/logs");
            string date = String.Join("-", DateTime.Now.ToString().Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

            Log.Info("Launching game profile " + profile.Name + ".");
            if (OS.IsWindows)
            {
                // Sets the arguments to empty, or to the profiles save path/logs and create time based logs. Creates the folder if necessary.
                string arguments = "";

                // Game logging
                if (useLogging)
                {
                    Log.Info("Performing logging setup for profile " + profile.Name + ".");

                    if (!Directory.Exists(logDir.FullName))
                        Directory.CreateDirectory(logDir.FullName);

                    if (File.Exists(logDir.FullName + "/" + profile.Name + ".txt"))
                        HelperMethods.RecursiveRollover(logDir.FullName + "/" + profile.Name + ".txt", 5);

                    StreamWriter stream = File.AppendText(logDir.FullName + "/" + profile.Name + ".txt");

                    stream.WriteLine("AM2RLauncher " + Core.VERSION + " log generated at " + date);

                    if (Core.isThisRunningFromWine)
                        stream.WriteLine("Using WINE!");

                    stream.Flush();

                    stream.Close();

                    arguments = "-debugoutput \"" + logDir.FullName + "/" + profile.Name + ".txt\" -output \"" + logDir.FullName + "/" + profile.Name + ".txt\"";
                }

                ProcessStartInfo proc = new ProcessStartInfo();

                proc.WorkingDirectory = CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name;
                proc.FileName = proc.WorkingDirectory + "/AM2R.exe";
                proc.Arguments = arguments;

                Log.Info("CWD of Profile is " + proc.WorkingDirectory);

                using (var p = Process.Start(proc))
                {
                    Core.SetForegroundWindow(p.MainWindowHandle);
                    p.WaitForExit();
                }
            }
            else if (OS.IsLinux)
            {

                ProcessStartInfo startInfo = new ProcessStartInfo();
                Log.Info("Is the environment textbox null or whitespace = " + String.IsNullOrWhiteSpace(envVars));

                if (!String.IsNullOrWhiteSpace(envVars))
                {
                    for (int i = 0; i < envVars.Count(f => f == '='); i++)
                    {
                        // Env var variable
                        string variable = envVars.Substring(0, envVars.IndexOf('='));
                        envVars = envVars.Replace(variable + "=", "");

                        // This thing here is the value parser. Since values are sometimes in quotes, i need to compensate for them.
                        int valueSubstringLength = 0;
                        if (envVars[0] != '"') // If value is not embedded in "", check if there are spaces left. If yes, get the index of the space, if not that was the last
                        {
                            if (envVars.IndexOf(' ') >= 0)
                                valueSubstringLength = envVars.IndexOf(' ') + 1;
                            else
                                valueSubstringLength = envVars.Length;
                        }
                        else // If value is embedded in "", check if there are spaces after the "". if yes, get index of that, if not that was the last
                        {
                            int secondQuoteIndex = envVars.IndexOf('"', envVars.IndexOf('"') + 1);
                            if (envVars.IndexOf(' ', secondQuoteIndex) >= 0)
                                valueSubstringLength = envVars.IndexOf(' ', secondQuoteIndex) + 1;
                            else
                                valueSubstringLength = envVars.Length;
                        }
                        // Env var value
                        string value = envVars.Substring(0, valueSubstringLength);
                        envVars = envVars.Substring(value.Length);

                        Log.Info("Adding variable \"" + variable + "\" with value \"" + value + "\"");
                        startInfo.EnvironmentVariables[variable] = value;
                    }
                }

                // If we're supposed to log profiles, add events that track those and append them to this var. otherwise keep it null
                string terminalOutput = null;

                startInfo.UseShellExecute = false;
                startInfo.WorkingDirectory = CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name;
                startInfo.FileName = startInfo.WorkingDirectory + "/AM2R.AppImage";

                Log.Info("CWD of Profile is " + startInfo.WorkingDirectory);

                Log.Info("Launching game with following variables: ");
                foreach (System.Collections.DictionaryEntry item in startInfo.EnvironmentVariables)
                {
                    Log.Info("Key: \"" + item.Key + "\" Value: \"" + item.Value + "\"");
                }

                using (Process p = new Process())
                {
                    p.StartInfo = startInfo;
                    if (useLogging)
                    {
                        p.StartInfo.RedirectStandardOutput = true;
                        p.OutputDataReceived += (sender, e) => { terminalOutput += e.Data + "\n"; };

                        p.StartInfo.RedirectStandardError = true;
                        p.ErrorDataReceived += (sender, e) => { terminalOutput += e.Data + "\n"; };
                    }

                    p.Start();

                    if (useLogging)
                    {
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                    }

                    p.WaitForExit();
                }

                if (terminalOutput != null)
                {
                    Log.Info("Performed logging setup for profile " + profile.Name + ".");

                    if (!Directory.Exists(logDir.FullName))
                        Directory.CreateDirectory(logDir.FullName);

                    if (File.Exists(logDir.FullName + "/" + profile.Name + ".txt"))
                        HelperMethods.RecursiveRollover(logDir.FullName + "/" + profile.Name + ".txt", 5);

                    StreamWriter stream = File.AppendText(logDir.FullName + "/" + profile.Name + ".txt");

                    // Write general info
                    stream.WriteLine("AM2RLauncher " + Core.VERSION + " log generated at " + date);

                    // Write what was in the terminal
                    stream.WriteLine(terminalOutput);

                    stream.Flush();

                    stream.Close();
                }

            }
            else if (OS.IsMac)
            {
                // Sets the arguments to only open the game, or append the profiles save path/logs and create time based logs. Creates the folder if necessary.
                string arguments = "AM2R.app -W";

                // Game logging
                if (useLogging)
                {
                    Log.Info("Performing logging setup for profile " + profile.Name + ".");

                    if (!Directory.Exists(logDir.FullName))
                        Directory.CreateDirectory(logDir.FullName);

                    if (File.Exists(logDir.FullName + "/" + profile.Name + ".txt"))
                        HelperMethods.RecursiveRollover(logDir.FullName + "/" + profile.Name + ".txt", 5);

                    StreamWriter stream = File.AppendText(logDir.FullName + "/" + profile.Name + ".txt");

                    stream.WriteLine("AM2RLauncher " + Core.VERSION + " log generated at " + date);

                    stream.Flush();

                    stream.Close();

                    arguments += " --stdout \"" + logDir.FullName + "/" + profile.Name + ".txt\" --stderr \"" + logDir.FullName + "/" + profile.Name + ".txt\"";
                }

                ProcessStartInfo proc = new ProcessStartInfo();

                proc.WorkingDirectory = CrossPlatformOperations.CURRENTPATH + "/Profiles/" + profile.Name;
                proc.FileName = "open";
                proc.Arguments = arguments;

                Log.Info("CWD of Profile is " + proc.WorkingDirectory);

                using (var p = Process.Start(proc))
                {
                    p?.WaitForExit();
                }
            }
            else
                Log.Error(OS.Name + " cannot run games!");

            Log.Info("Profile " + profile.Name + " process exited.");
        }

        /// <summary>
        /// Checks if the repository has been validly cloned already.
        /// </summary>
        /// <returns><see langword="true"/> if yes, <see langword="false"/> if not.</returns>
        public static bool IsPatchDataCloned()
        {
            // isValid seems to only check for a .git folder, and there are cases where that exists, but not the profile.xml
            return Repository.IsValid(CrossPlatformOperations.CURRENTPATH + "/PatchData") && File.Exists(CrossPlatformOperations.CURRENTPATH + "/PatchData/profile.xml");
        }

        /// <summary>
        /// Checks if a Zip file is a valid AM2R_1.1 zip.
        /// </summary>
        /// <param name="zipPath">Full Path to the Zip file to check.</param>
        /// <returns><see cref="IsZipAM2R11ReturnCodes"/> detailing the result</returns>
        public static IsZipAM2R11ReturnCodes CheckIfZipIsAM2R11(string zipPath)
        {
            const string d3dHash = "86e39e9161c3d930d93822f1563c280d";
            const string dataWinHash = "f2b84fe5ba64cb64e284be1066ca08ee";
            const string am2rHash = "15253f7a66d6ea3feef004ebbee9b438";

            string tmpPath = Path.GetTempPath() + Path.GetFileNameWithoutExtension(zipPath);

            // Clean up in case folder exists already
            if (Directory.Exists(tmpPath))
                Directory.Delete(tmpPath, true);

            Directory.CreateDirectory(tmpPath);

            // Open archive
            ZipArchive am2rZip = ZipFile.OpenRead(zipPath);

            // Check if exe exists anywhere
            ZipArchiveEntry am2rExe = am2rZip.Entries.FirstOrDefault(x => x.FullName.Contains("AM2R.exe"));
            if (am2rExe == null)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidAM2RExe;
            // Check if it's not in a subfolder. if it'd be in a subfolder, fullname would be "folder/AM2R.exe"
            if (am2rExe.FullName != "AM2R.exe")
                return IsZipAM2R11ReturnCodes.GameIsInASubfolder;
            // Check validity
            am2rExe.ExtractToFile(tmpPath + "/" + am2rExe.FullName);
            if (HelperMethods.CalculateMD5(tmpPath + "/" + am2rExe.FullName) != am2rHash)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidAM2RExe;

            // Check if data.win exists / is valid
            ZipArchiveEntry dataWin = am2rZip.Entries.FirstOrDefault(x => x.FullName == "data.win");
            if (dataWin == null)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidDataWin;
            dataWin.ExtractToFile(tmpPath + "/" + dataWin.FullName);
            if (HelperMethods.CalculateMD5(tmpPath + "/" + dataWin.FullName) != dataWinHash)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidDataWin;

            // Check if d3d.dll exists / is valid
            ZipArchiveEntry d3dx = am2rZip.Entries.FirstOrDefault(x => x.FullName == "D3DX9_43.dll");
            if (d3dx == null)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidD3DX9_43Dll;
            d3dx.ExtractToFile(tmpPath + "/" + d3dx.FullName);
            if (HelperMethods.CalculateMD5(tmpPath + "/" + d3dx.FullName) != d3dHash)
                return IsZipAM2R11ReturnCodes.MissingOrInvalidD3DX9_43Dll;

            // Clean up
            Directory.Delete(tmpPath, true);

            // If we didn't exit before, everything is fine
            Log.Info("AM2R_11 check successful!");
            return IsZipAM2R11ReturnCodes.Successful;
        }
    }
}
