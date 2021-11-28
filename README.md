## YOU MUST KNOW
The original Launcher's address :https://github.com/AM2R-Community-Developers/AM2RLauncher
and I just added Chinese
The effect is shown in the figure
![image](https://user-images.githubusercontent.com/89327487/143777115-6f8355f2-44c5-4e4f-b251-262ca324e1c4.png)
![image](https://user-images.githubusercontent.com/89327487/143777133-7ad302c3-706b-4298-9307-269ab4ed4f6a.png)
![image](https://user-images.githubusercontent.com/89327487/143777141-efbd3330-d8ae-4bd5-9d85-35c3ea2a3960.png)



# AM2RLauncherRewrite
This is the repository for the AM2RLauncher-Rewrite.

## What is this?
A front-end application that simplifies installing the latest AM2R-Community-Updates, creating APKs for Android use, as well as Mods for AM2R. It supports Windows (x86/x64) as well as Linux (x64).

## Dependencies
Windows needs the [.NET Framework 4.8 runtime](https://dotnet.microsoft.com/download/dotnet-framework/net48) installed.  
Linux needs the following dependencies installed:

- [.NET Core 5.0 runtime](https://dotnet.microsoft.com/download/dotnet/5.0) or above
- `xdelta3` 
- `gtk3`
- `libappindicator3`
- `webkitgtk`
- `openssl`
- `fuse2`

Optionally, for APK creation any Java runtime is needed.

### Arch Linux
On Arch Linux, you can install these by running this:  
(Multilib repositories are required, instructions on how to enable them can be found [here](https://wiki.archlinux.org/title/Official_repositories#Enabling_multilib))

`sudo pacman -S --needed dotnet-runtime fuse2 gtk3 libappindicator-gtk3 openssl webkit2gtk xdelta3 lib32-openal lib32-libpulse jre-openjdk`

For other distros, refer to your local package manager for instructions.   

## Downloads
Downloads can be found at the [Release Page](https://github.com/AM2R-Community-Developers/AM2RLauncher/releases).

Alternatively, for Arch Linux users an [AUR Package](https://aur.archlinux.org/packages/am2rlauncher/) also exist. Install it with `makepkg -si` or use your favourite AUR helper.

# Compiling Instructions:
## Dependencies
For compiling for Windows .Net Framework 4.8 SDK is needed. For Linux .Net Core 5.0 SDK is needed.

## Windows Instructions
Open the solution with Visual Studio 2019.  
Alternatively, build via `dotnet build` /  the `buildAll` batch file.

## Linux Instructions
In order to build for linux, use `dotnet publish AM2RLauncher.Gtk -p:PublishSingleFile=true -c release -r ubuntu.18.04-x64`, MonoDevelop sadly doesn't work.  
You *have* to specify it to build for Ubuntu, even on non-Ubuntu distros, because one of our Dependencies, libgit2sharp fails on the `linux-x64` RID.
