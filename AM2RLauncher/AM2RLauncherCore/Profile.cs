using AM2RLauncher.Core.XML;
using LibGit2Sharp;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace AM2RLauncher.Core;

/// <summary>
/// An enum, that has possible return codes for <see cref="Profile.CheckIfZipIsAM2R11"/>.
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
    /// The logger for <see cref="Core"/>, used to write any caught exceptions.
    /// </summary>
    private static readonly ILog log = Core.Log;


    /// <summary>
    /// Caches the result of <see cref="Is11Installed"/> so that we don't extract and verify it too often.
    /// </summary>
    private static bool? isAM2R11InstalledCache;

    /// <summary>
    /// Caches the MD5 hash of the provided AM2R_11.zip so we don't end up checking the zip if it hasn't changed.
    /// </summary>
    private static string lastAM2R11ZipMD5 = "";

    /// <summary>
    /// Checks if AM2R 1.1 has been installed already, aka if a valid AM2R 1.1 Zip exists.
    /// This method will store the result in a cache and return that, unless it's invalidated by <paramref name="invalidateCache"/>.
    /// </summary>
    /// <param name="invalidateCache">Determines if the AM2R_11 Cache should be invalidated.</param>
    /// <returns></returns>
    public static bool Is11Installed(bool invalidateCache = false)
    {
        // Only invalidate if we need to
        if (invalidateCache) InvalidateAM2R11InstallCache();

        // If we have a cache, return that instead
        if (isAM2R11InstalledCache != null) return isAM2R11InstalledCache.Value;

        string am2r11file = Core.AM2R11File;
        // Return safely if file doesn't exist
        if (!File.Exists(am2r11file)) return false;
        lastAM2R11ZipMD5 = HelperMethods.CalculateMD5(am2r11file);
        var returnCode = CheckIfZipIsAM2R11(am2r11file);
        // Check if it's valid, if not log it, rename it and silently leave
        if (returnCode != IsZipAM2R11ReturnCodes.Successful)
        {
            log.Info("Detected invalid AM2R_11 zip with following error code: " + returnCode);
            HelperMethods.RecursiveRollover(am2r11file);
            isAM2R11InstalledCache = false;
            return false;
        }
        isAM2R11InstalledCache = true;
        return true;
    }

    /// <summary>
    /// Invalidates <see cref="isAM2R11InstalledCache"/> if necessary.
    /// </summary>
    private static void InvalidateAM2R11InstallCache()
    {
        // If the file exists, and its hash matches with ours, don't invalidate
        if ((HelperMethods.CalculateMD5(Core.AM2R11File) == lastAM2R11ZipMD5))
            return;

        isAM2R11InstalledCache = null;
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
        log.Info("AM2R_11 check successful!");
        return IsZipAM2R11ReturnCodes.Successful;
    }

    /// <summary>
    /// Git Pulls from the repository.
    /// </summary>
    public static void PullPatchData(Func<TransferProgress, bool> transferProgressHandlerMethod)
    {
        using Repository repo = new Repository(Core.PatchDataPath);

        // Throw if we neither have a master nor main branch
        Branch originMaster = repo.Branches.FirstOrDefault(b => b.FriendlyName.Contains("origin/master") || b.FriendlyName.Contains("origin/main"));
        if (originMaster == null)
            throw new UserCancelledException("Neither branch 'master' nor branch 'main' could be found! Corrupted or invalid git repo?");

        // Permanently undo commits not pushed to remote
        repo.Reset(ResetMode.Hard, originMaster.Tip);

        // Credential information to fetch
        PullOptions options = new PullOptions
        {
            FetchOptions = new FetchOptions { OnTransferProgress = tp => transferProgressHandlerMethod(tp)}
        };

        // Create dummy user information to create a merge commit
        Signature signature = new Signature("null", "null", DateTimeOffset.Now);

        // Pull
        try
        {
            Commands.Pull(repo, signature, options);
        }
        catch
        {
            log.Error("Repository pull attempt failed!");
            return;
        }
        log.Info("Repository pulled successfully.");
    }

    /// <summary>
    /// Scans the PatchData and Mods folders for valid profile entries, creates and returns a list of them.
    /// </summary>
    /// <returns>A <see cref="List{ProfileXML}"/> containing all valid profile entries.</returns>
    public static List<ProfileXML> LoadProfiles()
    {
        log.Info("Loading profiles...");

        List<ProfileXML> profileList = new List<ProfileXML>();

        // Check for and add the Community Updates profile
        if (File.Exists(Core.PatchDataPath + "/profile.xml"))
        {
            ProfileXML profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(Core.PatchDataPath + "/profile.xml"));
            profile.DataPath = "/PatchData/data";
            profileList.Add(profile);
        }

        // Safety check to generate the Mods folder if it does not exist
        if (!Directory.Exists(Core.ModsPath))
            Directory.CreateDirectory(Core.ModsPath);

        // Get Mods folder info
        DirectoryInfo modsDir = new DirectoryInfo(Core.ModsPath);

        // Add all extracted profiles in Mods to the profileList.
        foreach (DirectoryInfo dir in modsDir.GetDirectories())
        {
            // If no profile.xml exists we don't add anything
            if (!File.Exists(dir.FullName + "/profile.xml"))
                continue;

            ProfileXML prof = Serializer.Deserialize<ProfileXML>(File.ReadAllText(dir.FullName + "/profile.xml"));
            // Safety check for non-installable profiles
            if (prof.Installable || IsProfileInstalled(prof))
            {
                prof.DataPath = "/Mods/" + dir.Name;
                profileList.Add(prof);
            }
            // If not installable and isn't installed, remove it
            else if (!IsProfileInstalled(prof))
            {
                prof.DataPath = "/Mods/" + dir.Name;
                DeleteProfile(prof);
            }
        }

        log.Info("Loaded " + profileList.Count + " profile(s).");
        return profileList;
    }

    /// <summary>
    /// Archives a given Profile by making a copy with "Name (version)". Does silently nothing if user archives already exist
    /// </summary>
    /// <param name="profile">The profile to archive</param>
    public static void ArchiveProfile(ProfileXML profile)
    {
        // temporarily serialize and deserialize to essentially "clone" the variable as otherwise we'd modify references
        File.WriteAllText(Path.GetTempPath() + "/" + profile.Name, Serializer.Serialize<ProfileXML>(profile));
        profile = Serializer.Deserialize<ProfileXML>(File.ReadAllText(Path.GetTempPath() + "/" + profile.Name));

        string originalName = profile.Name;
        // Change name to include version and be unique
        profile.Name += " (" + profile.Version + ")";
        // if we're archiving community updates, remove the "latest" part
        profile.Name = profile.Name.Replace("Community Updates Latest", "Community Updates");

        log.Info("Archiving " + profile.Name);

        string profileArchivePath = Core.ProfilesPath + "/" + profile.Name;

        // Do NOT overwrite if a path with this name already exists! It is likely an existing user archive.
        if (!Directory.Exists(profileArchivePath))
        {
            // Rename current profile if we have it installed
            if (Directory.Exists(Core.ProfilesPath + "/" + originalName))
                Directory.Move(Core.ProfilesPath + "/" + originalName, profileArchivePath);

            // Set as non-installable so that it's just treated as a launching reference
            profile.Installable = false;

            string modArchivePath = Core.ModsPath + "/" + profile.Name;

            // Do NOT overwrite if a path with this name already exists! It is likely an existing user archive.
            if (!Directory.Exists(modArchivePath))
            {
                Directory.CreateDirectory(modArchivePath);
                File.WriteAllText(modArchivePath + "/profile.xml", Serializer.Serialize<ProfileXML>(profile));
                log.Info("Finished archival.");
            }
            else
            {
                HelperMethods.DeleteDirectory(profileArchivePath);
                log.Info("Cancelling archival! User-defined archive in Mods already exists.");
            }
        }
        // If our desired rename already exists, it's probably a user archive... so we just delete the original folder and move on with installation of the new version.
        else
        {
            HelperMethods.DeleteDirectory(Core.ProfilesPath + "/" + originalName);
            log.Info("Cancelling archival! User-defined archive in Profiles already exists.");
        }
    }

    /// <summary>
    /// Deletes a profile from the Mods and Profiles folder.
    /// </summary>
    /// <param name="profile">The profile to delete.</param>
    public static void DeleteProfile(ProfileXML profile)
    {
        log.Info("Attempting to delete profile " + profile.Name + "...");

        // Delete folder in Mods
        if (Directory.Exists(CrossPlatformOperations.CurrentPath + profile.DataPath))
            HelperMethods.DeleteDirectory(CrossPlatformOperations.CurrentPath + profile.DataPath);

        // Delete the zip file in Mods
        if (File.Exists(CrossPlatformOperations.CurrentPath + profile.DataPath + ".zip"))
        {
            // For some reason, it was set at read only, so we undo that here
            File.SetAttributes(CrossPlatformOperations.CurrentPath + profile.DataPath + ".zip", FileAttributes.Normal);
            File.Delete(CrossPlatformOperations.CurrentPath + profile.DataPath + ".zip");
        }

        // Delete folder in Profiles
        if (Directory.Exists(Core.ProfilesPath + "/" + profile.Name))
            HelperMethods.DeleteDirectory(Core.ProfilesPath + "/" + profile.Name);

        log.Info("Successfully deleted profile " + profile.Name + ".");
    }

    /// <summary>
    /// Installs <paramref name="profile"/>.
    /// </summary>
    /// <param name="profile"><see cref="ProfileXML"/> to be installed.</param>
    /// <param name="useHqMusic">Whether to patch this with high quality music or not.</param>
    /// <param name="progress">Provides the current progress of this method.</param>
    public static void InstallProfile(ProfileXML profile, bool useHqMusic, IProgress<int> progress)
    {
        log.Info("Installing profile " + profile.Name + "...");

        string profilesHomePath = Core.ProfilesPath;
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

            log.Info("ProfileInstallation: Created folder structure.");
        }

        // Extract 1.1
        ZipFile.ExtractToDirectory(Core.AM2R11File, profilePath);

        // Extracted 1.1
        progress.Report(33);
        log.Info("Profile folder created and AM2R_11.zip extracted.");

        // Set local dataPath for installation files
        var dataPath = CrossPlatformOperations.CurrentPath + profile.DataPath;

        string datawin = null, exe = null;

        if (OS.IsWindows)
        {
            datawin = "data.win";
            exe = "AM2R.exe";
        }
        else if (OS.IsLinux)
        {
            datawin = "game.unx";
            // Use the exe name based on the desktop file in the AppImage, rather than hard coding it.
            string desktopContents = File.ReadAllText(Core.PatchDataPath + "/data/AM2R.AppDir/AM2R.desktop");
            exe = Regex.Match(desktopContents, @"(?<=Exec=).*").Value;
            log.Info("According to AppImage desktop file, using \"" + exe + "\" as game name.");
        }
        else if (OS.IsMac)
        {
            datawin = "game.ios";
            exe = "Mac_Runner";
        }
        else
        {
            log.Error(OS.Name + " does not have valid runner / data.win names!");
        }

        log.Info("Attempting to patch in " + profilePath);

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
        else if (OS.IsUnix) // YYC and VM look exactly the same on Linux and Mac so we're all good here.
        {
            CrossPlatformOperations.ApplyXdeltaPatch(profilePath + "/data.win", dataPath + "/game.xdelta", profilePath + "/" + datawin);
            CrossPlatformOperations.ApplyXdeltaPatch(profilePath + "/AM2R.exe", dataPath + "/AM2R.xdelta", profilePath + "/" + exe);
            // Just in case the resulting file isn't chmod-ed...
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
            log.Error(OS.Name + " does not have patching methods!");
        }

        // Applied patch
        if (OS.IsWindows || OS.IsMac) progress.Report(66);
        else if (OS.IsLinux) progress.Report(44); // Linux will take a bit longer, due to AppImage creation
        log.Info("xdelta patch(es) applied.");

        // Install new datafiles
        HelperMethods.DirectoryCopy(dataPath + "/files_to_copy", profilePath);

        // HQ music
        if (!profile.UsesCustomMusic && useHqMusic)
            HelperMethods.DirectoryCopy(Core.PatchDataPath + "/data/HDR_HQ_in-game_music", profilePath);


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
            HelperMethods.DirectoryCopy(Core.PatchDataPath + "/data/AM2R.AppDir", profilePath + "/AM2R.AppDir/");

            // Safety checks, in case the folders don't exist
            Directory.CreateDirectory(profilePath + "/AM2R.AppDir/usr/bin/");
            Directory.CreateDirectory(profilePath + "/AM2R.AppDir/usr/bin/assets/");

            // Copy game assets to the AppImageDir
            HelperMethods.DirectoryCopy(assetsPath, profilePath + "/AM2R.AppDir/usr/bin/assets/");
            File.Copy(profilePath + "/" + exe, profilePath + "/AM2R.AppDir/usr/bin/" + exe);

            progress.Report(66);
            log.Info("Gtk-specific formatting finished.");

            // Temp save the currentWorkingDirectory and console.error, change it to profilePath and null, call the script, and change it back.
            string workingDir = Directory.GetCurrentDirectory();
            TextWriter cliError = Console.Error;
            Directory.SetCurrentDirectory(profilePath);
            Console.SetError(new StreamWriter(Stream.Null));
            Environment.SetEnvironmentVariable("ARCH", "x86_64");
            Process.Start(Core.PatchDataPath + "/utilities/appimagetool-x86_64.AppImage", "-n AM2R.AppDir")?.WaitForExit();
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
            // Loading custom fonts crashes on Mac, so we delete those if they exist
            if (Directory.Exists(profilePath + "/lang/fonts"))
                Directory.Delete(profilePath + "/lang/fonts", true);
            // Move Frameworks, Info.plist and PkgInfo over
            HelperMethods.DirectoryCopy(Core.PatchDataPath + "/data/Frameworks", profilePath.Replace("Resources", "Frameworks"));
            File.Copy(dataPath + "/Info.plist", profilePath.Replace("Resources", "") + "/Info.plist", true);
            File.Copy(Core.PatchDataPath + "/data/PkgInfo", profilePath.Replace("Resources", "") + "/PkgInfo", true);
            //Put profilePath back to what it was before
            profilePath = profilesHomePath + "/" + profile.Name;
        }

        // Copy profile.xml so we can grab data to compare for updates later!
        // tldr; check if we're in PatchData or not
        if (new DirectoryInfo(dataPath).Parent?.Name == "PatchData")
            File.Copy(dataPath + "/../profile.xml", profilePath + "/profile.xml");
        else File.Copy(dataPath + "/profile.xml", profilePath + "/profile.xml");

        // Installed datafiles
        progress.Report(100);
        log.Info("Successfully installed profile " + profile.Name + ".");
    }

    /// <summary>
    /// Checks if <paramref name="profile"/> is installed.
    /// </summary>
    /// <param name="profile">The <see cref="ProfileXML"/> that should be checked for installation.</param>
    /// <returns><see langword="true"/> if yes, <see langword="false"/> if not.</returns>
    public static bool IsProfileInstalled(ProfileXML profile)
    {
        if (OS.IsWindows) return File.Exists(Core.ProfilesPath + "/" + profile.Name + "/AM2R.exe");
        if (OS.IsLinux) return File.Exists(Core.ProfilesPath + "/" + profile.Name + "/AM2R.AppImage");
        if (OS.IsMac) return Directory.Exists(Core.ProfilesPath + "/" + profile.Name + "/AM2R.app");

        log.Error(OS.Name + " can't have profiles installed!");
        return false;
    }

    /// <summary>
    /// Creates an APK of the selected <paramref name="profile"/>.
    /// </summary>
    /// <param name="profile"><see cref="ProfileXML"/> to be compiled into an APK.</param>
    /// <param name="useHqMusic">Whether to create the APK with high quality music or not.</param>
    /// <param name="progress">Provides the current progress of this method.</param>
    public static void CreateAPK(ProfileXML profile, bool useHqMusic, IProgress<int> progress)
    {
        // Overall safety check just in case of bad situations
        if (!profile.SupportsAndroid)
        {
            progress.Report(100);
            return;
        }

        log.Info("Creating Android APK for profile " + profile.Name + ".");

        // Create working dir after some cleanup
        string apktoolPath = Core.PatchDataPath + "/utilities/android/apktool.jar",
               uberPath = Core.PatchDataPath + "/utilities/android/uber-apk-signer.jar",
               tempDir = new DirectoryInfo(CrossPlatformOperations.CurrentPath + "/temp").FullName,
               dataPath = CrossPlatformOperations.CurrentPath + profile.DataPath;
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
        Directory.CreateDirectory(tempDir);

        log.Info("Cleanup, variables, and working directory created.");
        progress.Report(14);

        // Decompile AM2RWrapper.apk
        CrossPlatformOperations.RunJavaJar("\"" + apktoolPath + "\" d \"" + dataPath + "/android/AM2RWrapper.apk\"", tempDir);
        log.Info("AM2RWrapper decompiled.");
        progress.Report(28);

        // Add datafiles: 1.1, new datafiles, hq music, am2r.ini
        string workingDir = tempDir + "/AM2RWrapper/assets";
        ZipFile.ExtractToDirectory(Core.AM2R11File, workingDir);
        HelperMethods.DirectoryCopy(dataPath + "/files_to_copy", workingDir);

        if (useHqMusic)
            HelperMethods.DirectoryCopy(Core.PatchDataPath + "/data/HDR_HQ_in-game_music", workingDir);
        // Yes, I'm aware this is dumb. If you've got any better ideas for how to copy a seemingly randomly named .ini from this folder to the APK, please let me know.
        foreach (FileInfo file in new DirectoryInfo(dataPath).GetFiles().Where(f => f.Name.EndsWith("ini")))
            File.Copy(file.FullName, workingDir + "/" + file.Name);

        log.Info("AM2R_11.zip extracted and datafiles copied into AM2RWrapper.");
        progress.Report(42);

        // Patch data.win to game.droid
        CrossPlatformOperations.ApplyXdeltaPatch(workingDir + "/data.win", dataPath + "/droid.xdelta", workingDir + "/game.droid");
        log.Info("game.droid successfully patched.");
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
        log.Info("Unnecessary files removed, apktool.yml modified to prevent ogg compression.");
        progress.Report(70);

        // Rebuild APK
        CrossPlatformOperations.RunJavaJar("\"" + apktoolPath + "\" b AM2RWrapper -o \"" + profile.Name + ".apk\"", tempDir);
        log.Info("AM2RWrapper rebuilt into " + profile.Name + ".apk.");
        progress.Report(84);

        // Debug-sign APK
        CrossPlatformOperations.RunJavaJar("\"" + uberPath + "\" -a \"" + profile.Name + ".apk\"", tempDir);

        // Extra file cleanup
        File.Copy(tempDir + "/" + profile.Name + "-aligned-debugSigned.apk", CrossPlatformOperations.CurrentPath + "/" + profile.Name + ".apk", true);
        log.Info(profile.Name + ".apk signed and moved to " + CrossPlatformOperations.CurrentPath + "/" + profile.Name + ".apk.");
        HelperMethods.DeleteDirectory(tempDir);

        // Done
        progress.Report(100);
        log.Info("Successfully created Android APK for profile " + profile.Name + ".");
        CrossPlatformOperations.OpenFolderAndSelectFile(CrossPlatformOperations.CurrentPath + "/" + profile.Name + ".apk");
    }

    /// <summary>
    /// Runs the Game, works cross platform.
    /// </summary>
    public static void RunGame(ProfileXML profile, bool useLogging, string envVars = "")
    {
        // These are used on both windows and linux for game logging
        string savePath = OS.IsWindows ? profile.SaveLocation.Replace("%localappdata%", Environment.GetEnvironmentVariable("LOCALAPPDATA"))
            : profile.SaveLocation.Replace("~", CrossPlatformOperations.Home);
        DirectoryInfo logDir = new DirectoryInfo(savePath + "/logs");
        string date = String.Join("-", DateTime.Now.ToString().Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        log.Info("Launching game profile " + profile.Name + ".");
        if (OS.IsWindows)
        {
            // Sets the arguments to empty, or to the profiles save path/logs and create time based logs. Creates the folder if necessary.
            string arguments = "";

            // Game logging
            if (useLogging)
            {
                log.Info("Performing logging setup for profile " + profile.Name + ".");

                if (!Directory.Exists(logDir.FullName))
                    Directory.CreateDirectory(logDir.FullName);

                if (File.Exists(logDir.FullName + "/" + profile.Name + ".txt"))
                    HelperMethods.RecursiveRollover(logDir.FullName + "/" + profile.Name + ".txt", 5);

                StreamWriter stream = File.AppendText(logDir.FullName + "/" + profile.Name + ".txt");

                stream.WriteLine("AM2RLauncher " + Core.Version + " log generated at " + date);

                if (Core.IsThisRunningFromWine)
                    stream.WriteLine("Using WINE!");

                stream.Flush();

                stream.Close();

                arguments = "-debugoutput \"" + logDir.FullName + "/" + profile.Name + ".txt\" -output \"" + logDir.FullName + "/" + profile.Name + ".txt\"";
            }

            ProcessStartInfo proc = new ProcessStartInfo();

            proc.WorkingDirectory = Core.ProfilesPath + "/" + profile.Name;
            proc.FileName = proc.WorkingDirectory + "/AM2R.exe";
            proc.Arguments = arguments;

            log.Info("CWD of Profile is " + proc.WorkingDirectory);

            using var p = Process.Start(proc);
            Core.SetForegroundWindow(p.MainWindowHandle);
            p.WaitForExit();
        }
        else if (OS.IsLinux)
        {

            ProcessStartInfo startInfo = new ProcessStartInfo();
            log.Info("User does " + (String.IsNullOrWhiteSpace(envVars) ? "not" : "") + " have custom environment variables set.");

            //TODO: make this more readable at one day
            if (!String.IsNullOrWhiteSpace(envVars))
            {
                for (int i = 0; i < envVars.Count(f => f == '='); i++)
                {
                    // Env var variable
                    string variable = envVars.Substring(0, envVars.IndexOf('='));
                    envVars = envVars.Replace(variable + "=", "");

                    // This thing here is the value parser. Since values are sometimes in quotes, i need to compensate for them.
                    int valueSubstringLength;
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

                    log.Info("Adding user variable \"" + variable + "\" with value \"" + value + "\"");
                    startInfo.EnvironmentVariables[variable] = value;
                }
            }

            // If we're supposed to log profiles, add events that track those and append them to this var. otherwise keep it null
            string terminalOutput = null;

            startInfo.UseShellExecute = false;
            startInfo.WorkingDirectory = Core.ProfilesPath + "/" + profile.Name;
            startInfo.FileName = startInfo.WorkingDirectory + "/AM2R.AppImage";

            log.Info("CWD of Profile is " + startInfo.WorkingDirectory);

            log.Debug("Launching game with following variables: ");
            foreach (System.Collections.DictionaryEntry item in startInfo.EnvironmentVariables)
            {
                log.Debug("Key: \"" + item.Key + "\" Value: \"" + item.Value + "\"");
            }

            using (Process p = new Process())
            {
                p.StartInfo = startInfo;
                if (useLogging)
                {
                    p.StartInfo.RedirectStandardOutput = true;
                    p.OutputDataReceived += (_, e) => { terminalOutput += e.Data + "\n"; };

                    p.StartInfo.RedirectStandardError = true;
                    p.ErrorDataReceived += (_, e) => { terminalOutput += e.Data + "\n"; };
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
                log.Info("Performed logging setup for profile " + profile.Name + ".");

                if (!Directory.Exists(logDir.FullName))
                    Directory.CreateDirectory(logDir.FullName);

                if (File.Exists(logDir.FullName + "/" + profile.Name + ".txt"))
                    HelperMethods.RecursiveRollover(logDir.FullName + "/" + profile.Name + ".txt", 5);

                StreamWriter stream = File.AppendText(logDir.FullName + "/" + profile.Name + ".txt");

                // Write general info
                stream.WriteLine("AM2RLauncher " + Core.Version + " log generated at " + date);

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
                log.Info("Performing logging setup for profile " + profile.Name + ".");

                if (!Directory.Exists(logDir.FullName))
                    Directory.CreateDirectory(logDir.FullName);

                if (File.Exists(logDir.FullName + "/" + profile.Name + ".txt"))
                    HelperMethods.RecursiveRollover(logDir.FullName + "/" + profile.Name + ".txt", 5);

                StreamWriter stream = File.AppendText(logDir.FullName + "/" + profile.Name + ".txt");

                stream.WriteLine("AM2RLauncher " + Core.Version + " log generated at " + date);

                stream.Flush();

                stream.Close();

                arguments += " --stdout \"" + logDir.FullName + "/" + profile.Name + ".txt\" --stderr \"" + logDir.FullName + "/" + profile.Name + ".txt\"";
            }

            ProcessStartInfo proc = new ProcessStartInfo();

            proc.WorkingDirectory = Core.ProfilesPath + "/" + profile.Name;
            proc.FileName = "open";
            proc.Arguments = arguments;

            log.Info("CWD of Profile is " + proc.WorkingDirectory);

            using var p = Process.Start(proc);
            p?.WaitForExit();
        }
        else
            log.Error(OS.Name + " cannot run games!");

        log.Info("Profile " + profile.Name + " process exited.");
    }

    /// <summary>
    /// Checks if the repository has been validly cloned already.
    /// </summary>
    /// <returns><see langword="true"/> if yes, <see langword="false"/> if not.</returns>
    public static bool IsPatchDataCloned()
    {
        // isValid seems to only check for a .git folder, and there are cases where that exists, but not the profile.xml
        return File.Exists(Core.PatchDataPath + "/profile.xml") && Repository.IsValid(Core.PatchDataPath);
    }
}