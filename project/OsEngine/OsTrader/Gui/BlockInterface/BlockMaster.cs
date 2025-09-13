using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OsEngine.OsTrader.Gui.BlockInterface
{
    public static class BlockMaster
    {
        public static string Password
        {
            get
            {
                if (!File.Exists(@"Engine\PrimeSettingss.txt"))
                {
                    return "";
                }
                try
                {
                    using (StreamReader reader = new StreamReader(@"Engine\PrimeSettingss.txt"))
                    {
                        return Decrypt(reader.ReadLine());
                    }
                }
                catch (Exception)
                {
                    // ignore
                }

                return "";
            }
            set
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(@"Engine\PrimeSettingss.txt", false))
                    {
                        string saveStr = Encrypt(value);

                        writer.WriteLine(saveStr);

                        writer.Close();
                    }
                }
                catch (Exception)
                {
                    // ignore
                }
            }
        }

        public static bool IsBlocked
        {
            get
            {
                if (!File.Exists(@"Engine\PrimeSettingsss.txt"))
                {
                    return false;
                }
                try
                {
                    using (StreamReader reader = new StreamReader(@"Engine\PrimeSettingsss.txt"))
                    {
                        string res = reader.ReadLine();

                        if(res == null)
                        {
                            return false;
                        }

                        return Convert.ToBoolean(Decrypt(res));
                    }
                }
                catch (Exception)
                {
                    // ignore
                }

                return false;
            }
            set
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(@"Engine\PrimeSettingsss.txt", false))
                    {
                        string saveStr = Encrypt(value.ToString());

                        writer.WriteLine(saveStr);

                        writer.Close();
                    }
                }
                catch (Exception)
                {
                    // ignore
                }
            }
        }

        public static string Encrypt(string clearText)
        {
            string EncryptionKey = "dfg2335";
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 },1,HashAlgorithmName.SHA256);
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;
        }

        public static string Decrypt(string cipherText)
        {
            if(cipherText == null)
            {
                return null;
            }

            string EncryptionKey = "dfg2335";
            cipherText = cipherText.Replace(" ", "+");
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 }, 1, HashAlgorithmName.SHA256);
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }
            return cipherText;
        }
    }
}
