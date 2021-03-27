using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.Linq;
using System.ComponentModel;

namespace AM2RLauncher.XML
{
    /// <summary>
    /// Class that handles how the Launcher settings are saved as XML. Only affects Linux
    /// </summary>
    [Serializable]
    [XmlRoot("settings")]
    public class LauncherConfigXML
    {
        /// <summary>Indicates wether or not to auto-update the Launcher. Used for <see cref="MainForm.autoUpdateCheck"/></summary>
        [XmlAttribute("AutoUpdate")]
        public bool AutoUpdate
        { get; set; }
        /// <summary>Indicates the Language of the Launcher. Used for <see cref="MainForm.languageDropDown"/></summary>
        [XmlAttribute("Language")]
        public string Language
        { get; set; }
        /// <summary>Indicates wether or not to use High-quality music when patching to PC. Used for <see cref="MainForm.hqMusicPCCheck"/></summary>
        [XmlAttribute("MusicHQPC")]
        public bool MusicHQPC
        { get; set; }
        /// <summary>Indicates wether or not to use High-quality music when patching to Android. Used for <see cref="MainForm.hqMusicAndroidCheck"/></summary>
        [XmlAttribute("MusicHQAndroid")]
        public bool MusicHQAndroid
        { get; set; }
        /// <summary>Indicates the index for <see cref="MainForm.mirrorDropDown"/>.</summary>
        [XmlAttribute("MirrorIndex")]
        public int MirrorIndex
        { get; set; }
        /// <summary>Indicates the index for <see cref="MainForm.profileDropDown"/>.</summary>
        [XmlAttribute("ProfileIndex")]
        public string ProfileIndex
        { get; set; }
        /// <summary>Indicates wether or not to have custom mirrors enabled. Used for <see cref="MainForm.customMirrorCheck"/></summary>
        [XmlAttribute("CustomMirrorEnabled")]
        public bool CustomMirrorEnabled
        { get; set; }
        /// <summary>Indicates the custom mirror as a text. Used for <see cref="MainForm.customMirrorTextBox"/></summary>
        [XmlAttribute("CustomMirrorText")]
        public string CustomMirrorText
        { get; set; }
        /// <summary>Indicates whether or not to create debug logs of profile. Used for <see cref="MainForm.profileDebugLogCheck"/></summary>
        [XmlAttribute("ProfileDebugLog")]
        public string ProfileDebugLog
        { get; set; }
        /// <summary>Indicates the custom environment variable(s) as text. Used for <see cref="MainForm.customEnvVarTextBox"/></summary>
        [XmlAttribute("CustomEnvVar")]
        public string CustomEnvVar
        { get; set; }
        /// <summary>Indicates the Width of the Launcher.</summary>
        [XmlAttribute("Width")]
        public int Width
        { get; set; }
        /// <summary>Indicates the Height of the Launcher.</summary>
        [XmlAttribute("Height")]
        public int Height
        { get; set; }
        /// <summary>Indicates wether or not the Launcher is maximized or not.</summary>
        [XmlAttribute("IsMaximized")]
        public bool IsMaximized
        { get; set; }

        //huge help from James for all of this!!!
        //here's a short explanation of this. Basically, what we do is create an indexer. This makes it possible for this class to be indexed, like this LauncherConfigXML[i]
        //the indexer here has a get and set "submethod". And both use Reflection to get the current class as something that can be used with lambda values
        //so for get we just lambda the properties and search for something that can be read and has the same name as the input property. After that, we just get the value and return it as a string
        //set is basiaclly the same, but instead of returning it, we set the prtoperty value, which would be followed after the `='. 
        //So LauncherConfigXML[property] = hellWorld would set the value of `property` to `hellWorld`
        /// <summary>
        /// An Indexer for <see cref="ProfileXML"/>. Not to be used directly, use <see cref="CrossPlatformOperations.WriteToConfig(string, object)"/>
        /// or <see cref="CrossPlatformOperations.ReadFromConfig(string)"/> instead!
        /// </summary>
        /// <param name="property">The property to get or set.</param>
        /// <returns>The value of <paramref name="property"/> as an <see cref="object"/> if used as a get, <see cref="void"/> if used as a set.</returns>
        public object this[string property]
        {
            get
            { 
                //this is gonna throw an exception, if the property can't be found. because of null.GetValue(this)
                return typeof(LauncherConfigXML).GetProperties().Where(p => p.CanRead && p.Name == property).First().GetValue(this).ToString();
            }
            set
            {
                typeof(LauncherConfigXML).GetProperties().Where(p => p.CanWrite && p.Name == property).First().SetValue(this, value);
            }
        }

        /// <summary>
		/// Creates a <see cref="LauncherConfigXML"/> with a default set of attributes.
		/// </summary>
        public LauncherConfigXML()
        {
            AutoUpdate = true;
            Language = "Default";
            MusicHQPC = true;
            MusicHQAndroid = false;
            ProfileIndex = "null";
            MirrorIndex = 0;
            CustomMirrorEnabled = false;
            CustomMirrorText = "";
            ProfileDebugLog = "true";
            CustomEnvVar = "";
            Width = 600;
            Height = 600;
            IsMaximized = false;
        }

        /// <summary>
        /// Creates a <see cref="LauncherConfigXML"/> with custom attributes.
        /// </summary>
        /// <param name="autoUpdate">Paramater that indicates if <see cref="MainForm.autoUpdateCheck"/> is enabled or not.</param>
        /// <param name="language">Parameter that indicates the language of the launcher.</param>
        /// <param name="musicHQPC">Parameter that indicates if <see cref="MainForm.hqMusicPCCheck"/> is enabled or not.</param>
        /// <param name="musicHQAndroid">Parameter that indicates if <see cref="MainForm.hqMusicAndroidCheck"/> is enabled or not.</param>
        /// <param name="profileIndex">Parameter that saves the index of the selected profile of <see cref="MainForm.profileDropDown"/>.</param>
        /// <param name="mirrorIndex">Parameter that saves the index of the selected mirror in <see cref="MainForm.mirrorDropDown"/>.</param>
        /// <param name="customEnvVar">Parameter that saves custom Environment variables that will be used on Linux for launching a game.</param>
        /// <param name="customMirrorEnabled">Parameter that indicates if <see cref="MainForm.customMirrorCheck"/> is enabled or not.</param>
        /// <param name="customMirrorText">Parameter that's used for <see cref="MainForm.customMirrorTextBox"/>.</param>
        /// <param name="width">Parameter that indicates the width of <see cref="MainForm"/>.</param>
        /// <param name="height">Parameter that indicates the height of <see cref="MainForm"/>.</param>
        /// <param name="isMaximized">Parameter that indicates if <see cref="MainForm"/> has been set to fullscreen or not.</param>
        public LauncherConfigXML(bool autoUpdate, string language, bool musicHQPC, bool musicHQAndroid,
                                 string profileIndex, int mirrorIndex, string customEnvVar, bool customMirrorEnabled,
                                 string customMirrorText, int width, int height, bool isMaximized)
        {
            AutoUpdate = autoUpdate;
            Language = language;
            MusicHQPC = musicHQPC;
            MusicHQAndroid = musicHQAndroid;
            ProfileIndex = profileIndex;
            MirrorIndex = mirrorIndex;
            CustomMirrorEnabled = customMirrorEnabled;
            CustomMirrorText = customMirrorText;
            CustomEnvVar = customEnvVar;
            Width = width;
            Height = height;
            IsMaximized = isMaximized;
        }
    }
}
