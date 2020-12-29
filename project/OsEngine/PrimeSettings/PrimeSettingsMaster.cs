/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using OsEngine.OsTrader.AdminPanelApi;

namespace OsEngine.PrimeSettings
{
    public class PrimeSettingsMaster
    {
        public static ApiState ApiState { get; set; }

        public static bool ErrorLogMessageBoxIsActiv
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _errorLogMessageBoxIsActiv;
            }
            set
            {
                _errorLogMessageBoxIsActiv = value;
                Save();
            }
        }

        private static bool _errorLogMessageBoxIsActiv = true;

        public static bool ErrorLogBeepIsActiv
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _errorLogBeepIsActiv;
            }
            set { _errorLogBeepIsActiv = value; Save(); }
        }

        private static bool _errorLogBeepIsActiv = true;

        public static bool TransactionBeepIsActiv
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _transactionBeepIsActiv;
            }
            set
            {
                _transactionBeepIsActiv = value;
                Save();
            }
        }

        private static bool _transactionBeepIsActiv;

        public static bool ServerTestingIsActive
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _serverTestingIsActiv;
            }
            set
            {
                _serverTestingIsActiv = value;
                Save();
            }
        }

        private static bool _serverTestingIsActiv;

        public static bool AutoStartApi
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _autoStartApi;
            }
            set
            {
                _autoStartApi = value;
                Save();
            }
        }

        private static bool _autoStartApi;

        public static string Token
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _token;
            }
            set
            {
                _token = value;
                Save();
            }
        }

        private static string _token;

        public static string Ip
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _ip;
            }
            set
            {
                _ip = value;
                Save();
            }
        }

        private static string _ip;

        public static string Port
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
                _port = value;
                Save();
            }
        }

       
        private static string _port;

        public static bool UseOxyPlotChart
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _useOxyPlotChart;
            }
            set
            {
                _useOxyPlotChart = value;
                Save();
            }
        }

        private static bool _useOxyPlotChart;

        /// <summary>
        /// save settings
        /// сохранить настройки
        /// </summary>
        public static void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\PrimeSettings.txt", false))
                {
                    writer.WriteLine(_transactionBeepIsActiv);
                    writer.WriteLine(_errorLogBeepIsActiv);
                    writer.WriteLine(_errorLogMessageBoxIsActiv);
                    writer.WriteLine(_serverTestingIsActiv);
                    writer.WriteLine(_autoStartApi);
                    writer.WriteLine(_token);
                    writer.WriteLine(_ip);
                    writer.WriteLine(_port);
                    writer.WriteLine(_useOxyPlotChart);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private static bool _isLoad;

        /// <summary>
        /// load settings
        /// загрузить настройки
        /// </summary>
        private static void Load()
        {
            _isLoad = true;
            if (!File.Exists(@"Engine\PrimeSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\PrimeSettings.txt"))
                {
                    _transactionBeepIsActiv = Convert.ToBoolean(reader.ReadLine());
                    _errorLogBeepIsActiv = Convert.ToBoolean(reader.ReadLine());
                    _errorLogMessageBoxIsActiv = Convert.ToBoolean(reader.ReadLine());
                    _serverTestingIsActiv = Convert.ToBoolean(reader.ReadLine());
                    _autoStartApi = Convert.ToBoolean(reader.ReadLine());
                    _token = reader.ReadLine();
                    _ip = reader.ReadLine();
                    _port = reader.ReadLine();
                    _useOxyPlotChart = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }
}
