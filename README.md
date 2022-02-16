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

Optionally, for APK creation any Java runtime is needed.

### Ubuntu
On Ubuntu you can install them by following these instructions:
1. [Follow the instructions here to download and install the .NET Core Runtime](https://docs.microsoft.com/dotnet/core/install/linux-ubuntu#supported-distributions)
2. Enable the i386 architecture if you haven't already:
```
sudo dpkg --add-architecture i386
sudo apt update && sudo apt install libc6:i386 libncurses5:i386 libstdc++6:i386
```
3. Install the rest of the dependencies with `sudo apt install libappindicator3-1 libwebkit2gtk-4.0-37 xdelta3 libgl1:i386 libopenal1:i386 libpulse0:i386 default-jre`

### Arch Linux
On Arch Linux you can install them by running this:  
(Multilib repositories are required, instructions on how to enable them can be found [here](https://wiki.archlinux.org/title/Official_repositories#Enabling_multilib))  
`sudo pacman -S --needed dotnet-runtime fuse2 gtk3 libappindicator-gtk3 openssl webkit2gtk xdelta3 lib32-openal lib32-libpulse jre-openjdk`

### Fedora
On Fedora you can install them by running this command:  
`sudo dnf install dotnet-runtime-6.0 libappindicator-gtk3 xdelta mesa-libGL.i686 pulseaudio-libs.1686 openal-soft.i686 java-latest-openjdk`

### Other distros
For other distros refer to your local package manager for instructions.   

## Downloads
Downloads can be found at the [Release Page](https://github.com/AM2R-Community-Developers/AM2RLauncher/releases).

Alternatively, for Arch Linux users an [AUR Package](https://aur.archlinux.org/packages/am2rlauncher/) also exists. Install it with `makepkg -si` or use your favourite AUR helper.

## Configuration and Data Files
The AM2RLauncher stores its files in the following places:
- On Windows, it stores the config file to the `AM2RLauncher.exe.config` next to the binary, and its data files in the same folder as the binary.
- On Linux, it stores the config file to `$XDG_CONFIG_HOME/AM2RLauncher` and its data files to `$XDG_DATA_HOME/AM2RLauncher` (which are defaulting back to `~/.config` and `~/.local/share` respectively).  

The AM2RLauncher data can get quite big, so if you wish to change where it stores it, you can do so with the `AM2RLAUNCHERDATA` environment variable (i.e `$env:AM2RLAUNCHERDATA="D:\MyLauncherData"` or `AM2RLAUNCHERDATA="/mnt/bigDrive/launcherData"`). 
**Data files are different for each OS, you cannot mix and match them!**

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
