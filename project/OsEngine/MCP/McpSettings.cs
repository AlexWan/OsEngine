/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;

namespace OsEngine.MCP
{
    /// <summary>
    /// Settings for MCP API host.
    /// Stored in Engine\McpSettings.txt.
    /// </summary>
    public class McpSettings
    {
        #region Default values

        private const int DefaultPort = 6500;

        private const string DefaultApiKey = "osengine-mcp-default-key";

        private const bool DefaultIsFullLogEnabled = false;

        #endregion

        #region Public properties

        public static int Port
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _port;
            }
            set
            {
                if (_port == value)
                {
                    return;
                }
                _port = value;
                Save();
            }
        }
        private static int _port = DefaultPort;

        public static string ApiKey
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _apiKey;
            }
            set
            {
                if (_apiKey == value)
                {
                    return;
                }
                _apiKey = value;
                Save();
            }
        }
        private static string _apiKey = DefaultApiKey;

        public static bool IsEnabled
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _isEnabled;
            }
            set
            {
                if (_isEnabled == value)
                {
                    return;
                }
                _isEnabled = value;
                Save();
            }
        }
        private static bool _isEnabled = false;

        public static bool IsFullLogEnabled
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _isFullLogEnabled;
            }
            set
            {
                if (_isFullLogEnabled == value)
                {
                    return;
                }
                _isFullLogEnabled = value;
                Save();
            }
        }
        private static bool _isFullLogEnabled = DefaultIsFullLogEnabled;

        #endregion

        #region Save / Load

        private static bool _isLoad;

        public static void Save()
        {
            try
            {
                if (!Directory.Exists("Engine"))
                {
                    Directory.CreateDirectory("Engine");
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\McpSettings.txt", false))
                {
                    writer.WriteLine(_port);
                    writer.WriteLine(_apiKey);
                    writer.WriteLine(_isEnabled);
                    writer.WriteLine(_isFullLogEnabled);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private static void Load()
        {
            _isLoad = true;

            if (!File.Exists(@"Engine\McpSettings.txt"))
            {
                Save();
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\McpSettings.txt"))
                {
                    string portLine = reader.ReadLine();
                    if (int.TryParse(portLine, out int port))
                    {
                        _port = port;
                    }

                    string keyLine = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(keyLine))
                    {
                        _apiKey = keyLine;
                    }

                    string enabledLine = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(enabledLine)
                        && bool.TryParse(enabledLine, out bool enabled))
                    {
                        _isEnabled = enabled;
                    }

                    string fullLogLine = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(fullLogLine)
                        && bool.TryParse(fullLogLine, out bool fullLogEnabled))
                    {
                        _isFullLogEnabled = fullLogEnabled;
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore, use defaults
            }
        }

        #endregion
    }
}
