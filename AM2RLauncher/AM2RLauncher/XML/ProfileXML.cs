using System;
using System.Xml.Serialization;

namespace AM2RLauncher.XML
{
    /// <summary>
    /// Class that handles how the mod settings are saved as XML.
    /// </summary>
    [Serializable]
    [XmlRoot("message")]
    public class ProfileXML
    {
        /// <summary>Indicates the Operating system the mod was made for.</summary>
        [XmlAttribute("OperatingSystem")]
        public string OperatingSystem
        { get; set; }
        /// <summary>Indicates the xml version the mod was made in.</summary>
        [XmlAttribute("XMLVersion")]
        public int XMLVersion
        { get; set; }
        /// <summary>Indicates the version of the mod.</summary>
        [XmlAttribute("Version")]
        public string Version
        { get; set; }
        /// <summary>Indicates the mod's name.</summary>
        [XmlAttribute("Name")]
        public string Name
        { get; set; }
        /// <summary>Indicates the mod's author.</summary>
        [XmlAttribute("Author")]
        public string Author
        { get; set; }
        /// <summary>Indicates wether or not the mod uses custom music.</summary>
        [XmlAttribute("UsesCustomMusic")]
        public bool UsesCustomMusic
        { get; set; }
        /// <summary>Indicates the save location of the mod.</summary>
        [XmlAttribute("SaveLocation")]
        public string SaveLocation
        { get; set; }
        /// <summary>Indicates wether or not the mod supports Android.</summary>
        [XmlAttribute("SupportsAndroid")]
        public bool SupportsAndroid
        { get; set; }
        /// <summary>Indicates wether or not the mod was compiled with YYC.</summary>
        [XmlAttribute("UsesYYC")]
        public bool UsesYYC
        { get; set; }
        /// <summary>Indicates if the mod is installable. This is only <see langword="false"/> for archival community updates mods.</summary>
        [XmlAttribute("Installable")]
        public bool Installable
        { get; set; }
        /// <summary>Indicates any notes that the mod author deemed worthy to share about his mod.</summary>
        [XmlAttribute("ProfileNotes")]
        public string ProfileNotes
        { get; set; }
        /// <summary>This gets calculated at runtime, by <see cref="MainForm.LoadProfiles"/>. Indicates where the install data for the mod is stored.</summary>
        [XmlIgnore]
        public string DataPath
        { get; set; }

        /// <summary>Creates a <see cref="ProfileXML"/> with a default set of attributes.</summary>
        public ProfileXML()
        {
            Installable = true;
        }

        /// <summary>
        /// Creates a <see cref="ProfileXML"/> with a custom set of attributes.
        /// </summary>
        /// <param name="operatingSystem">The operating system the mod was made on.</param>
        /// <param name="xmlVersion">The xml version the mod was created with.</param>
        /// <param name="version">The version of the mod.</param>
        /// <param name="name">The mod name.</param>
        /// <param name="author">The mod author.</param>
        /// <param name="usesCustomMusic">Wether or not the mod uses custom music.</param>
        /// <param name="saveLocation">The save location of the mod.</param>
        /// <param name="android">Wether or not the mod works for android.</param>
        /// <param name="usesYYC">Wether or not the mod was made with YYC.</param>
        /// <param name="installable">Wether or not the mod is installable.</param>
        /// <param name="profileNotes">The notes of the mod.</param>
        public ProfileXML(string operatingSystem, int xmlVersion, string version, string name, string author,
                          bool usesCustomMusic, string saveLocation, bool android, bool usesYYC,
                          bool installable, string profileNotes)
        {
            OperatingSystem = operatingSystem;
            XMLVersion = xmlVersion;
            Version = version;
            Name = name;
            Author = author;
            UsesCustomMusic = usesCustomMusic;
            SaveLocation = saveLocation;
            SupportsAndroid = android;
            UsesYYC = usesYYC;
            Installable = installable;
            ProfileNotes = profileNotes;
        }
    }
}
