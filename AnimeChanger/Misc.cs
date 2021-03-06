﻿using System;
using System.Text;
using System.IO;
using System.Xml;
using System.Security.Cryptography;

namespace AnimeChanger
{
    internal static class Misc
    {
        internal static string FolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DoubleKilled_AniChanger");
        internal static string DiscordData = Path.Combine(FolderPath, "discord.xml");
        internal static string MalData = Path.Combine(FolderPath, "mal.xml");

        #region Folder/File checks
        internal static void CheckFolder()
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            var aniXml = Path.Combine(FolderPath, "ani.xml");
            if (!File.Exists(aniXml))
            {
                File.WriteAllText(Path.Combine(FolderPath, "ani.xml"), Properties.Resources.ani);
            }
        }
        #endregion

        #region Secrets
        /// <summary>
        /// Reads login data from XML (stored as base64) and decrypts it into plain text login data.
        /// </summary>
        /// <param name="filename">System.String, name of the XML file to read without extension.</param>
        /// <returns>AnimeChanger.Secrets, container for plain text login data.</returns>
        internal static Secrets ReadSecrets(string filename)
        {
            // prepare necessary variables
            Secrets ret = new Secrets();
            XmlDocument document = new XmlDocument();
            document.Load(Path.Combine(FolderPath, $"{filename}.xml"));

            try
            {
                // instantiate optional entropy array
                byte[] entropy = { 0 };

                // convert base64 encrypted strings to utf8 unencrypted strings
                ret.id = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(document.SelectSingleNode("Secrets/id").InnerText), entropy, DataProtectionScope.CurrentUser));
                ret.pass = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(document.SelectSingleNode("Secrets/pass").InnerText), entropy, DataProtectionScope.CurrentUser));
            }
            catch (SystemException ex)
            {
                // detect file corruption
                if (ex is CryptographicException || ex is FormatException)
                    System.Windows.Forms.MessageBox.Show(string.Format("(Error: {0})\nCouldn't retrieve login credentials. Please re-enter your login data.", ex.GetType().ToString()), "Error");
                else
                    System.Windows.Forms.MessageBox.Show(ex.Message);

                ret = null;
            }

            return ret;
        }

        /// <summary>
        /// Encrypts login data and writes it to XML as base64 strings.
        /// </summary>
        /// <param name="sec">AnimeChanger.Secters, container for plain text login data.</param>
        /// <param name="filename">System.String, name of the XML file to read without extension.</param>
        internal static void WriteSecrets(Secrets sec, string filename)
        {
            // prepare necessary variables
            byte[] entropy = { 0 };
            Secrets buffer = new Secrets();

            // encrypt login credentials
            buffer.id = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(sec.id), entropy, DataProtectionScope.CurrentUser));
            buffer.pass = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(sec.pass), entropy, DataProtectionScope.CurrentUser));
            
            // serialize encrypted data to file
            System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(typeof(Secrets));
            using (StreamWriter wfile = new StreamWriter(Path.Combine(FolderPath, $"{filename}.xml")))
                writer.Serialize(wfile, buffer);
        }
        #endregion
    }
}
