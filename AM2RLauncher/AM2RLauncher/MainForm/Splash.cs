using Eto;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AM2RLauncher
{
    /// <summary>
    /// Class only for providing Splashes
    /// </summary>
    static class Splash
    {
        /// <summary>
        /// Cross-Platform splash strings
        /// </summary>
        static readonly string[] generalSplash =
        {
            "The real Ridley is the friends we made along the way.",
            "Now with 100% more Septoggs!",
            "Now with 100% more Blob Throwers!",
            "Speedrun THIS, I dare you.",
            "The broken pipe is a lie.",
            "Overcommitting to April Fool's since 2018.",
            "Also try Metroid II: Return of Samus!",
            "Also try Metroid: Samus Returns!",
            "Also try Prime 2D!",
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
            "AH THE MOLE" 
        };

        /// <summary>
        /// Linux only splash strings
        /// </summary>
        static readonly string[] linuxSplash =
        {
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
            "Imagine using non-free software.",
            "What if... we used a non-FOSS OS? Haha just kidding... Unless?"
        };

        /// <summary>
        /// Get a random splash string, according to the current OS.
        /// </summary>
        /// <returns>The randomly chosen splash as a <see cref="string"/>.</returns>
        static public string GetSplash()
        {
            string[] combinedSplash;
            Random rng = new Random();

            if (Platform.Instance.IsGtk) 
                combinedSplash = generalSplash.Concat(linuxSplash).ToArray();
            else 
                combinedSplash = generalSplash;

            string splashString = combinedSplash[rng.Next(0, combinedSplash.Length)];
            return splashString;
        }
    }
}
