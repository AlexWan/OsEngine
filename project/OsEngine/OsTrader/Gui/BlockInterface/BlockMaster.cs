/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market;
using System;
using System.IO;
using System.Security.Cryptography;

namespace OsEngine.OsTrader.Gui.BlockInterface
{
    public static class BlockMaster
    {
        private const string PasswordFilePath = @"Engine\PrimeSettingss.txt";
        private const string BlockFlagFilePath = @"Engine\PrimeSettingsss.txt";

        private const int Iterations = 100000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static bool HasPassword
        {
            get
            {
                return TryReadHash(out _, out _, out _);
            }
        }

        public static void SetPassword(string newPassword)
        {
            try
            {
                byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
                byte[] hash = Rfc2898DeriveBytes.Pbkdf2(newPassword, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

                string saveStr = "PBKDF2$" + Iterations + "$"
                    + Convert.ToBase64String(salt) + "$"
                    + Convert.ToBase64String(hash);

                File.WriteAllText(PasswordFilePath, saveStr);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        public static bool CheckPassword(string password)
        {
            if (TryReadHash(out int iterations, out byte[] salt, out byte[] expectedHash) == false)
            {
                return false;
            }

            try
            {
                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

                return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                return false;
            }
        }

        private static bool TryReadHash(out int iterations, out byte[] salt, out byte[] hash)
        {
            iterations = 0;
            salt = null;
            hash = null;

            try
            {
                if (!File.Exists(PasswordFilePath))
                {
                    return false;
                }

                string line = File.ReadAllText(PasswordFilePath).Trim();

                string[] parts = line.Split('$');

                if (parts.Length != 4 || parts[0] != "PBKDF2")
                {
                    return false;
                }

                iterations = Convert.ToInt32(parts[1]);
                salt = Convert.FromBase64String(parts[2]);
                hash = Convert.FromBase64String(parts[3]);

                return true;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                return false;
            }
        }

        public static bool IsBlocked
        {
            get
            {
                try
                {
                    if (!File.Exists(BlockFlagFilePath))
                    {
                        return false;
                    }

                    string res = File.ReadAllText(BlockFlagFilePath).Trim();

                    if (string.IsNullOrEmpty(res))
                    {
                        return false;
                    }

                    if (bool.TryParse(res, out bool result))
                    {
                        return result;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                    return false;
                }
            }
            set
            {
                try
                {
                    File.WriteAllText(BlockFlagFilePath, value.ToString());
                }
                catch (Exception ex)
                {
                    ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                }
            }
        }
    }
}
