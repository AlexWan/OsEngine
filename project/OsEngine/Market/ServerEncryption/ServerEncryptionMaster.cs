/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;

namespace OsEngine.Market.ServerEncryption
{
    /// <summary>
    /// master password encryption status
    /// статус шифрования мастер-паролем
    /// </summary>
    public enum ServerEncryptionStatus
    {
        NotChosen,
        Encrypted,
        Declined
    }

    /// <summary>
    /// global encryptor of server password parameters. One master password for all connectors
    /// глобальный шифрователь парольных параметров серверов. Один мастер-пароль на все коннекторы
    /// </summary>
    public static class ServerEncryptionMaster
    {
        #region Constants and state

        private const string StateFilePath = @"Engine\Encryption.txt";

        private const string EncPrefix = "ENC1:";

        // PBKDF2-SHA256, 600k iterations (OWASP recommendation). Brute-force ~17k attempts/sec on one GPU
        private const int Iterations = 600000;

        private const int SaltSize = 16;

        private const int KeySize = 32;

        private const int VerifierSize = 32;

        private const int IvSize = 16;

        private static readonly object _keyLocker = new object();

        private static readonly object _dialogLocker = new object();

        private static byte[] _key;

        private static bool _dialogShown;

        #endregion

        #region Properties

        public static bool IsUnlocked
        {
            get
            {
                lock (_keyLocker)
                {
                    return _key != null;
                }
            }
        }

        public static bool UnlockDeclinedThisSession
        {
            get
            {
                lock (_keyLocker)
                {
                    return _unlockDeclinedThisSession;
                }
            }
        }

        private static bool _unlockDeclinedThisSession;

        #endregion

        #region Status file

        public static ServerEncryptionStatus GetStatus()
        {
            try
            {
                if (!File.Exists(StateFilePath))
                {
                    return ServerEncryptionStatus.NotChosen;
                }

                string[] lines = File.ReadAllLines(StateFilePath);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("Status="))
                    {
                        string status = lines[i].Substring("Status=".Length);

                        if (status == "Encrypted")
                        {
                            return ServerEncryptionStatus.Encrypted;
                        }

                        if (status == "Declined")
                        {
                            return ServerEncryptionStatus.Declined;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return ServerEncryptionStatus.NotChosen;
        }

        public static void SetDeclined()
        {
            try
            {
                File.WriteAllText(StateFilePath, "Status=Declined");
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private static bool TryReadSaltAndVerifier(out byte[] salt, out byte[] verifier)
        {
            salt = null;
            verifier = null;

            try
            {
                if (!File.Exists(StateFilePath))
                {
                    return false;
                }

                string[] lines = File.ReadAllLines(StateFilePath);

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("Salt="))
                    {
                        salt = Convert.FromBase64String(lines[i].Substring("Salt=".Length));
                    }

                    if (lines[i].StartsWith("Verifier="))
                    {
                        verifier = Convert.FromBase64String(lines[i].Substring("Verifier=".Length));
                    }
                }

                return salt != null && verifier != null;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        private static void WriteEncryptedState(byte[] salt, byte[] verifier)
        {
            string content = "Status=Encrypted" + Environment.NewLine
                + "Salt=" + Convert.ToBase64String(salt) + Environment.NewLine
                + "Verifier=" + Convert.ToBase64String(verifier);

            File.WriteAllText(StateFilePath, content);
        }

        #endregion

        #region Enable / Disable / Change password / Unlock

        public static bool Enable(string password)
        {
            try
            {
                if (GetStatus() == ServerEncryptionStatus.Encrypted)
                {
                    ServerMaster.SendNewLogMessage("Encryption is already enabled / Шифрование уже включено", LogMessageType.Error);
                    return false;
                }

                byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
                byte[] derived = DeriveKeyAndVerifier(password, salt);

                byte[] key = GetPart(derived, 0, KeySize);
                byte[] verifier = GetPart(derived, KeySize, VerifierSize);

                WriteEncryptedState(salt, verifier);

                lock (_keyLocker)
                {
                    _key = key;
                    _unlockDeclinedThisSession = false;
                }

                // файлы, которые не удалось записать, остались открытым текстом - зашифруются при следующем сохранении
                return ProcessAllParamsFiles(null, key);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public static bool Disable(string password)
        {
            try
            {
                if (GetStatus() != ServerEncryptionStatus.Encrypted)
                {
                    return false;
                }

                // пароль проверяется всегда, даже если сессия уже разблокирована
                if (TryUnlock(password) == false)
                {
                    return false;
                }

                byte[] key;
                lock (_keyLocker)
                {
                    key = _key;
                }

                // сначала расшифровываем файлы. При сбое статус остаётся Encrypted, файлы откачены - всё консистентно
                if (ProcessAllParamsFiles(key, null) == false)
                {
                    return false;
                }

                lock (_keyLocker)
                {
                    _key = null;
                }

                Zeroize(key);

                SetDeclined();

                return true;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public static bool ChangePassword(string oldPassword, string newPassword)
        {
            try
            {
                if (GetStatus() != ServerEncryptionStatus.Encrypted)
                {
                    return false;
                }

                byte[] oldKey;

                // пароль проверяется всегда, даже если сессия уже разблокирована
                if (TryUnlock(oldPassword) == false)
                {
                    return false;
                }

                lock (_keyLocker)
                {
                    oldKey = _key;
                }

                byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
                byte[] derived = DeriveKeyAndVerifier(newPassword, salt);

                byte[] newKey = GetPart(derived, 0, KeySize);
                byte[] verifier = GetPart(derived, KeySize, VerifierSize);

                // сначала перешифровываем файлы. При сбое старый верификатор на месте, файлы откачены - всё консистентно
                if (ProcessAllParamsFiles(oldKey, newKey) == false)
                {
                    return false;
                }

                WriteEncryptedState(salt, verifier);

                lock (_keyLocker)
                {
                    _key = newKey;
                }

                Zeroize(oldKey);

                return true;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public static bool TryUnlock(string password)
        {
            try
            {
                if (GetStatus() != ServerEncryptionStatus.Encrypted)
                {
                    return false;
                }

                if (TryReadSaltAndVerifier(out byte[] salt, out byte[] verifier) == false)
                {
                    return false;
                }

                byte[] derived = DeriveKeyAndVerifier(password, salt);
                byte[] actualVerifier = GetPart(derived, KeySize, VerifierSize);

                if (CryptographicOperations.FixedTimeEquals(verifier, actualVerifier) == false)
                {
                    return false;
                }

                lock (_keyLocker)
                {
                    _key = GetPart(derived, 0, KeySize);
                    _unlockDeclinedThisSession = false;
                }

                return true;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public static void SetUnlockDeclined()
        {
            lock (_keyLocker)
            {
                _unlockDeclinedThisSession = true;
            }
        }

        #endregion

        #region Encrypt / Decrypt values

        public static bool IsEncryptedValue(string value)
        {
            return value != null && value.StartsWith(EncPrefix);
        }

        public static string Encrypt(string clearText)
        {
            byte[] key;

            lock (_keyLocker)
            {
                key = _key;
            }

            if (key == null)
            {
                return null;
            }

            return EncryptWithKey(key, clearText);
        }

        public static bool TryDecrypt(string encValue, out string result)
        {
            result = null;

            try
            {
                if (IsEncryptedValue(encValue) == false)
                {
                    result = encValue;
                    return true;
                }

                byte[] key;

                lock (_keyLocker)
                {
                    key = _key;
                }

                if (key == null)
                {
                    return false;
                }

                return TryDecryptWithKey(key, encValue, out result);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        private static string EncryptWithKey(byte[] key, string clearText)
        {
            byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        byte[] clearBytes = Encoding.UTF8.GetBytes(clearText);
                        cs.Write(clearBytes, 0, clearBytes.Length);
                    }

                    return EncPrefix + Convert.ToBase64String(iv) + ":" + Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private static bool TryDecryptWithKey(byte[] key, string encValue, out string result)
        {
            result = null;

            try
            {
                string body = encValue.Substring(EncPrefix.Length);
                string[] parts = body.Split(':');

                if (parts.Length != 2)
                {
                    return false;
                }

                byte[] iv = Convert.FromBase64String(parts[0]);
                byte[] cipherBytes = Convert.FromBase64String(parts[1]);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                        }

                        result = Encoding.UTF8.GetString(ms.ToArray());
                        return true;
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        #endregion

        #region Unlock dialog request

        public static void RequestUnlock()
        {
            try
            {
                if (GetStatus() != ServerEncryptionStatus.Encrypted
                    || IsUnlocked
                    || UnlockDeclinedThisSession)
                {
                    return;
                }

                lock (_dialogLocker)
                {
                    if (_dialogShown)
                    {
                        return;
                    }

                    _dialogShown = true;
                }

                try
                {
                    if (Application.Current != null
                        && Application.Current.Dispatcher.CheckAccess() == false)
                    {
                        Application.Current.Dispatcher.Invoke(ShowUnlockDialog);
                    }
                    else
                    {
                        ShowUnlockDialog();
                    }
                }
                finally
                {
                    lock (_dialogLocker)
                    {
                        _dialogShown = false;
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private static void ShowUnlockDialog()
        {
            ServerEncryptionUi ui = new ServerEncryptionUi(true);
            ui.ShowDialog();
        }

        #endregion

        #region Params files processing

        private static bool ProcessAllParamsFiles(byte[] decryptKey, byte[] encryptKey)
        {
            List<string> filePaths = new List<string>();
            List<string[]> originalLines = new List<string[]>();
            List<string[]> transformedLines = new List<string[]>();

            // первый проход - читаем и трансформируем в памяти. Ошибка дешифровки = полный отказ без записи

            try
            {
                if (Directory.Exists(@"Engine") == false)
                {
                    return true;
                }

                string[] files = Directory.GetFiles(@"Engine", "*Params.txt");

                for (int i = 0; i < files.Length; i++)
                {
                    if (TryTransformParamsFile(files[i], decryptKey, encryptKey, out string[] original, out string[] transformed) == false)
                    {
                        ServerMaster.SendNewLogMessage("Encryption processing aborted. No files were changed / Обработка прервана. Файлы не изменены", LogMessageType.Error);
                        return false;
                    }

                    filePaths.Add(files[i]);
                    originalLines.Add(original);
                    transformedLines.Add(transformed);
                }

                // ключ MCP API хранится в отдельном файле своего формата - обрабатываем в том же проходе
                string mcpSettingsPath = @"Engine\McpSettings.txt";

                if (File.Exists(mcpSettingsPath))
                {
                    if (TryTransformMcpSettingsFile(mcpSettingsPath, decryptKey, encryptKey, out string[] mcpOriginal, out string[] mcpTransformed) == false)
                    {
                        ServerMaster.SendNewLogMessage("Encryption processing aborted. No files were changed / Обработка прервана. Файлы не изменены", LogMessageType.Error);
                        return false;
                    }

                    filePaths.Add(mcpSettingsPath);
                    originalLines.Add(mcpOriginal);
                    transformedLines.Add(mcpTransformed);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }

            // второй проход - запись. При сбое откатываем уже записанные файлы к исходному содержимому

            List<int> writtenIndexes = new List<int>();

            for (int i = 0; i < filePaths.Count; i++)
            {
                if (transformedLines[i] == null)
                {
                    continue;
                }

                if (TryWriteFileWithRetries(filePaths[i], transformedLines[i]) == false)
                {
                    for (int j = 0; j < writtenIndexes.Count; j++)
                    {
                        int index = writtenIndexes[j];
                        TryWriteFileWithRetries(filePaths[index], originalLines[index]);
                    }

                    ServerMaster.SendNewLogMessage("Encryption processing rolled back. Files were not changed / Обработка откачена. Файлы не изменены", LogMessageType.Error);
                    return false;
                }

                writtenIndexes.Add(i);
            }

            return true;
        }

        private static bool TryTransformParamsFile(string filePath, byte[] decryptKey, byte[] encryptKey, out string[] original, out string[] transformed)
        {
            original = null;
            transformed = null;

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                bool changed = false;

                string[] result = (string[])lines.Clone();

                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("Password^") == false)
                    {
                        continue;
                    }

                    string[] parts = lines[i].Split('^');

                    if (parts.Length != 3)
                    {
                        continue;
                    }

                    string value = parts[2];

                    if (decryptKey != null
                        && IsEncryptedValue(value))
                    {
                        if (TryDecryptWithKey(decryptKey, value, out string plain) == false)
                        {
                            ServerMaster.SendNewLogMessage("Failed to decrypt value. File: " + filePath + ", parameter: " + parts[1], LogMessageType.Error);
                            return false;
                        }

                        parts[2] = plain;
                        changed = true;
                    }
                    else if (encryptKey != null
                        && IsEncryptedValue(value) == false)
                    {
                        parts[2] = EncryptWithKey(encryptKey, value);
                        changed = true;
                    }

                    result[i] = parts[0] + "^" + parts[1] + "^" + parts[2];
                }

                if (changed)
                {
                    original = lines;
                    transformed = result;
                }

                return true;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString() + " File: " + filePath, LogMessageType.Error);
                return false;
            }
        }

        private static bool TryTransformMcpSettingsFile(string filePath, byte[] decryptKey, byte[] encryptKey, out string[] original, out string[] transformed)
        {
            original = null;
            transformed = null;

            try
            {
                string[] lines = File.ReadAllLines(filePath);

                if (lines.Length < 2)
                {
                    return true;
                }

                string keyLine = lines[1];
                string newKeyLine = null;

                if (decryptKey != null
                    && IsEncryptedValue(keyLine))
                {
                    if (TryDecryptWithKey(decryptKey, keyLine, out string plain) == false)
                    {
                        ServerMaster.SendNewLogMessage("Failed to decrypt value. File: " + filePath + ", MCP API key", LogMessageType.Error);
                        return false;
                    }

                    newKeyLine = plain;
                }
                else if (encryptKey != null
                    && IsEncryptedValue(keyLine) == false
                    && string.IsNullOrEmpty(keyLine) == false)
                {
                    newKeyLine = EncryptWithKey(encryptKey, keyLine);
                }

                if (newKeyLine == null)
                {
                    return true;
                }

                original = lines;
                transformed = (string[])lines.Clone();
                transformed[1] = newKeyLine;

                return true;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString() + " File: " + filePath, LogMessageType.Error);
                return false;
            }
        }

        private static bool TryWriteFileWithRetries(string filePath, string[] lines)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    File.WriteAllLines(filePath, lines);
                    return true;
                }
                catch (Exception error)
                {
                    if (attempt == 2)
                    {
                        ServerMaster.SendNewLogMessage("Failed to write file after 3 attempts: " + filePath + ". " + error.ToString(), LogMessageType.Error);
                    }
                    else
                    {
                        Thread.Sleep(300);
                    }
                }
            }

            return false;
        }

        #endregion

        #region Helpers

        private static byte[] DeriveKeyAndVerifier(string password, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize + VerifierSize);
        }

        private static byte[] GetPart(byte[] source, int offset, int count)
        {
            byte[] part = new byte[count];
            Array.Copy(source, offset, part, 0, count);
            return part;
        }

        private static void Zeroize(byte[] data)
        {
            if (data == null)
            {
                return;
            }

            Array.Clear(data, 0, data.Length);
        }

        #endregion
    }
}
