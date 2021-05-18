using Eto;
using System;

namespace AM2RLauncher
{
    class Splash
    {
        // Splash strings
        readonly string[] splashList =
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

        // Get random splash string
        public string GetSplash()
        {
            Random rng = new Random();
            string splashString = splashList[rng.Next(0, splashList.Length - (Platform.IsGtk ? 0 : 13))];
            return splashString;
        }
    }
}
