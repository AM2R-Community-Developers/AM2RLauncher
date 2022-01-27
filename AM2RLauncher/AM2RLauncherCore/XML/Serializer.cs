using System;
using System.IO;
using System.Text;

namespace AM2RLauncher.Core.XML
{
    /// <summary>
    /// The <c>Serializer</c> class, that serializes to and deserializes from XML files.
    /// </summary>
    public static class Serializer
    {
        /// <summary>
        /// Serializes <paramref name="item"/> as a <typeparamref name="T"/> to XML.
        /// </summary>
        /// <typeparam name="T">The class to serialize to.</typeparam>
        /// <param name="item">The object that will be serialized.</param>
        /// <returns>The serialized XML as a <see cref="string"/>.</returns>
        public static string Serialize<T>(object item)
        {
            Type t = typeof(T);
            MemoryStream memStream = new MemoryStream();
            System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(t);

            serializer.Serialize(memStream, item);

            string xml = Encoding.UTF8.GetString(memStream.ToArray());

            memStream.Flush();
            memStream.Close();
            memStream.Dispose();
            memStream = null;

            return xml;
        }

        /// <summary>
        /// Deserialize <paramref name="xmlString"/> into an object of class <typeparamref name="T"/> that can be assigned.
        /// </summary>
        /// <typeparam name="T">The class that <paramref name="xmlString"/> will be deserialized to.</typeparam>
        /// <param name="xmlString">An XML <see cref="string"/> that will be deserialized.</param>
        /// <returns>A deserialized object of class <typeparamref name="T"/> from <paramref name="xmlString"/>.</returns>
        public static T Deserialize<T>(string xmlString)
        {
            Type t = typeof(T);
            using (MemoryStream memStream = new MemoryStream(Encoding.UTF8.GetBytes(xmlString)))
            {
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(t);
                return (T)serializer.Deserialize(memStream);
            }
        }
    }
}