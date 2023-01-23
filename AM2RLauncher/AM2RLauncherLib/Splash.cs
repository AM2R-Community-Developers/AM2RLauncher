using System;
using System.Linq;

namespace AM2RLauncherLib;

/// <summary>
/// Class only for providing Splashes
/// </summary>
public static class Splash
{
    /// <summary>
    /// Cross-Platform splash strings
    /// </summary>
    private static readonly string[] generalSplash =
    {
        "The real Ridley is the friends we made along the way.",
        "Now with 100% more Septoggs!",
        "Now with 100% more Blob Throwers!",
        "Speedrun THIS, I dare you.",
        "The broken pipe is a lie.",
        "I am altering the broken pipe. Pray I don't alter it any further.",
        "How dreadful, Yet Another Launcher to bloat my system.",
        "Over committing to April Fool's since 2018.",
        "Also try Metroid II: Return of Samus!",
        "Also try Metroid: Samus Returns!",
        "Also try Prime 2D!",
        "Also try Skippy the Bot!",
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
        "Oh? So you're patching me?",
        "I, Gawron Giovana, have a dream.",
        "Oooh weee ooo, I look just like an Omega",
        "AH THE MOLE",
        "S P I D E R B A L L",
        "Fun is infinite with Community-Developers Inc.",
        "You may only proceed if you wear baggy pants.",
        "Did you know that games take time to develop?",
        "Ask the wombat.",
        "This isn't complicated enough yet."
    };

    /// <summary>
    /// Windows only splash strings
    /// </summary>
    private static readonly string[] windowsSplash =
    {
        ":(",
        "All your machine are belong to MS",
        "All your data are belong to us",
        "Go to Settings to activate AM2R.",
        "(Not Responding)"
    };

    /// <summary>
    /// Linux only splash strings
    /// </summary>
    private static readonly string[] linuxSplash =
    {
        "Sorry this is ugly, but at least it works.",
        "GTK + QT = 💣",
        "I hope you use Arch, btw",
        "All your Ubuntu are belong to Arch btw.",
        "Help, how do I quit vim!?",
        "The bloat isn't our fault, YoYo Games forced 1GB dependencies!",
        "On second thought, maybe the bloat is our fault...",
        "The quieter you are, the more Metroids you can hear.",
        "What you are referring to as AM2R, is in fact, GNU/AM2R.",
        "GNOME be gone!",
        "Kurse you KDE!",
        "Go compile your own girlfriend. This one doesn't count, she's out of your league.",
        "Year 3115, NVIDIA still doesn't support Wayland.",
        "Was a mouse really that expensive, i3 users!?",
        "Imagine using non-free software.",
        "What if... we used a non-FOSS OS? Haha just kidding... Unless?",
        "🐧",
        "You are already patched 🐸🕶",
        "Yes, do as I say!",
        "Did you find the penguin yet?"
    };
    
    /// <summary>
    /// Splashes additionally used in Flatpaks
    /// </summary>
    private static readonly string[] flatpakSplash =
    {
        "All hail Gaben",
        "Thanks to Flatpak, our bugs are now consistent!",
        "You're using SteamOS, aren't you?",
        "Now extra sandboxed, in case of GameMaker jank!",
        "The next remake? Another Metroid 4 Remake of course!",
        "What do you mean that there's a number after 2?",
        "We don't like Sand. It's course, rough, and it gets everywhere.",
        "This is a sandbox, not a sandy B.O.X.",
        "Now easily available on an App Store!"
    };

    /// <summary>
    /// Mac only splash strings
    /// </summary>
    private static readonly string[] macSplash =
    {
        "Does this even work?",
        "You weren't supposed to do that!",
        "Another Mac 2 Remake",
        "This shouldn't even be possible",
        "How long until this breaks?",
        "Have fun trying to convince modders to support this!"
    };

    /// <summary>
    /// Combined splash strings
    /// </summary>
    private static readonly string[] combinedSplash = CombineSplashes();

    /// <summary>
    /// Get a random splash string, according to the current OS.
    /// </summary>
    /// <returns>The randomly chosen splash as a <see cref="string"/>.</returns>
    public static string GetSplash()
    {
        Random rng = new Random();

        string splashString = combinedSplash[rng.Next(0, combinedSplash.Length)];
        return splashString;
    }

    /// <summary>
    /// Creates a string array which is <see cref="generalSplash"/> concatenated with the splash array for the current OS
    /// </summary>
    /// <returns>A string array where <see cref="generalSplash"/> and the splash array for the current OS have been concatenated.</returns>
    private static string[] CombineSplashes()
    {
        string[] totalSplashes;
        if (OS.IsWindows)
            totalSplashes = generalSplash.Concat(windowsSplash).ToArray();
        else if (OS.IsLinux)
            totalSplashes = generalSplash.Concat(linuxSplash).ToArray();
        else if (OS.IsMac)
            totalSplashes = generalSplash.Concat(macSplash).ToArray();
        else
            totalSplashes = generalSplash;
        
        // This is seperate, as we want them to be combined with linux
        if (OS.IsThisRunningFromFlatpak)
            totalSplashes = totalSplashes.Concat(flatpakSplash).ToArray();
        
        return totalSplashes;
    }
}