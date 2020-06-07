using System;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace DVu.Library.Utility
{
    public sealed class UiUtility
    {
        private static volatile UiUtility _instance;
        private static readonly object SyncRoot = new object();

        public static UiUtility GetInstance()
        {
            if (_instance == null)
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new UiUtility();
                }
            return _instance;
        }

        private readonly RSACryptoServiceProvider _encryptor;
        
        private UiUtility()
        {
            string keyFile = ConfigurationManager.AppSettings["PublicKeyFile"];

            if (HttpContext.Current != null && !Path.IsPathRooted(keyFile))
            {
                keyFile = HttpContext.Current.Server.MapPath("~/" + keyFile);
            }

            if (keyFile != null && File.Exists(keyFile))
            {
                string publicKeyXml;
                using (StreamReader keyFileIn = File.OpenText(keyFile))
                {
                    publicKeyXml = keyFileIn.ReadToEnd();
                }
                //Encrypt this data
                _encryptor = new RSACryptoServiceProvider();
                _encryptor.FromXmlString(publicKeyXml);
            }
        }

        /// <summary>
        /// Encrypts data using the
        /// public key specified in the application configuration.
        /// </summary>
        /// Encrypts the data
        /// <param name="data">The data to be encrypted</param>
        public string Encrypt(string data)
        {
            if (_encryptor == null)
                throw new ApplicationException("Could not get public encryption key");

            //First convert string data to bytes          
            byte[] byteData = Encoding.UTF8.GetBytes(data);

            //Use the encryptor object to perform encryption
            byte[] encryptedData = _encryptor.Encrypt(byteData, true);

            //Convert back to string with Base64Encoding
            //Base64 is used as a robust way to encode an arbitrary bit
            //sequence as a string. It has no value from a security perspective.
            return Convert.ToBase64String(encryptedData);
        }

        /// <summary>
        /// Decrypts the data using the
        /// public key specified in the application configuration.
        /// </summary>
        /// Decrypts the data
        /// <param name="encryptedData">The encrypted data</param>
        public string Decrypt(string encryptedData)
        {
            if (_encryptor == null)
                throw new ApplicationException("Could not get public encryption key");

            //First convert the data to bytes          
            byte[] byteData = Encoding.UTF8.GetBytes(encryptedData);

            //Use the encryptor object to perform encryption
            byte[] data = _encryptor.Decrypt(byteData, true);

            //Convert back to string with Base64Encoding
            //Base64 is used as a robust way to encode an arbitrary bit
            //sequence as a string. It has no value from a security perspective.
            return Convert.ToBase64String(data);
        }
    }
}
