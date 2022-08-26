# AM2RLauncherRewrite
This is the repository for the AM2RLauncher-Rewrite.

## What is this?
A front-end application that simplifies installing the latest AM2R-Community-Updates, creating APKs for Android use, as well as Mods for AM2R. It supports Windows (x86/x64) as well as Linux (x64).

## Dependencies
Windows needs the [.NET Framework 4.8 runtime](https://dotnet.microsoft.com/download/dotnet-framework/net48) installed.  
Linux needs the following dependencies installed:

- [.NET Core 5.0 runtime](https://dotnet.microsoft.com/download/dotnet/6.0) or later. .NET Core 6.0 is preferred.
- `xdelta3` 
- `gtk3`
- `libappindicator3`
- `webkitgtk`
- `openssl`
- `fuse2`

As well as these dependencies to run AM2R:
- 32-bit version of `libpulse`
- 32-bit version of `openal`
- 32-bit version of your graphics drivers

Optionally, for APK creation any Java runtime is needed.

For more detailed instructions check out the [installation process wiki page](https://github.com/AM2R-Community-Developers/AM2RLauncher/wiki/Installation-Process).

## Downloads
Downloads can be found at the [Release Page](https://github.com/AM2R-Community-Developers/AM2RLauncher/releases).

For all Linux users, a [Flatpak](https://flathub.org/apps/details/io.github.am2r_community_developers.AM2RLauncher) is available and can be installed with all above dependencies bundled. This method is the recommended way for Steam Deck users.

Alternatively, for Arch Linux users an [AUR Package](https://aur.archlinux.org/packages/am2rlauncher/) also exists. Install it with `makepkg -si` or use your favourite AUR helper.

## Configuration and Data Files
The AM2RLauncher stores its files in the following places:
- On Windows, it stores the config file to the `AM2RLauncher.exe.config` next to the binary, and its data files in the same folder as the binary.
- On Linux, it stores the config file to `$XDG_CONFIG_HOME/AM2RLauncher` and its data files to `$XDG_DATA_HOME/AM2RLauncher` (which are defaulting back to `~/.config` and `~/.local/share` respectively).  

The AM2RLauncher data can get quite big, so if you wish to change where it stores it, you can do so with the `AM2RLAUNCHERDATA` environment variable (i.e `$env:AM2RLAUNCHERDATA="D:\MyLauncherData"` or `AM2RLAUNCHERDATA="/mnt/bigDrive/launcherData"`). 
**Data files are different for each OS, you cannot mix and match them!**

If you wish to redistribute the AM2RLauncher to some Linux distro, you can use the `NoAutoUpdate` configuration, in order to build the AM2RLauncher with the auto-updating features disabled. Further assets (.desktop file, icon, appdata) can also be found in this directory: `./AM2RLauncher/distribution/linux`.

# Compiling Instructions:
## Dependencies
For compiling for Windows .Net Framework 4.8 SDK is needed. For Linux and Mac .Net Core 5.0 SDK or later is needed.

## Windows Instructions
Open the solution with Visual Studio 2019.  
Alternatively, build via `dotnet build` /  the `buildAll` batch file.

## Linux Instructions
In order to build for linux, use `dotnet publish AM2RLauncher.Gtk -p:PublishSingleFile=true -p:DebugType=embedded -c release -r ubuntu.18.04-x64 --no-self-contained`, MonoDevelop sadly doesn't work.  
You *have* to specify it to build for Ubuntu, even on non-Ubuntu distros, because one of our Dependencies, libgit2sharp fails on the `linux-x64` RID.  
For Arch Linux users, an `am2rlauncher-git` [AUR Package](https://aur.archlinux.org/packages/am2rlauncher-git/) also exists.

## Mac Instructions
You can open the solution with Visual Studio for Mac, but it likely will crash after compliation. Use `dotnet publish AM2RLauncher.Mac -c release` instead.  
Note that Mac is currently **unsupported**. We will try to answer questions, but cannot guarantee to fix issues with Mac.
