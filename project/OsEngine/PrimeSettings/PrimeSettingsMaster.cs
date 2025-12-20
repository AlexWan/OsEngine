/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;

namespace OsEngine.PrimeSettings
{
    public class PrimeSettingsMaster
    {

        public static bool ErrorLogMessageBoxIsActive
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _errorLogMessageBoxIsActive;
            }
            set
            {
                _errorLogMessageBoxIsActive = value;
                Save();
            }
        }
        private static bool _errorLogMessageBoxIsActive = true;

        public static bool ErrorLogBeepIsActive
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _errorLogBeepIsActive;
            }
            set { _errorLogBeepIsActive = value; Save(); }
        }
        private static bool _errorLogBeepIsActive = true;

        public static bool TransactionBeepIsActive
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _transactionBeepIsActive;
            }
            set
            {
                _transactionBeepIsActive = value;
                Save();
            }
        }
        private static bool _transactionBeepIsActive;

        public static bool RebootTradeUiLight
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _rebootTradeUiLight;
            }
            set
            {
                _rebootTradeUiLight = value;
                Save();
            }
        }
        private static bool _rebootTradeUiLight;

        public static bool ReportCriticalErrors
        {
            get
            {
                return _reportCriticalErrors;
            }
            set
            {
                if(_reportCriticalErrors == value)
                {
                    return;
                }
                _reportCriticalErrors = value;
                Save();
            }
        }
        private static bool _reportCriticalErrors = true;

        public static string LabelInHeaderBotStation
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _labelInHeaderBotStation;
            }
            set
            {
                _labelInHeaderBotStation = value;
                Save();
            }
        }
        private static string _labelInHeaderBotStation;

        public static MemoryCleanerRegime MemoryCleanerRegime
        {
            get
            {
                if (_isLoad == false)
                {
                    Load();
                }
                return _memoryCleanerRegime;
            }
            set
            {
                if(_memoryCleanerRegime == value)
                {
                    return;
                }
                _memoryCleanerRegime = value;
                Save();
            }
        }
        public static MemoryCleanerRegime _memoryCleanerRegime;

        public static void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\PrimeSettings.txt", false))
                {
                    writer.WriteLine(_transactionBeepIsActive);
                    writer.WriteLine(_errorLogBeepIsActive);
                    writer.WriteLine(_errorLogMessageBoxIsActive);
                    writer.WriteLine(_labelInHeaderBotStation);
                    writer.WriteLine(_rebootTradeUiLight);
                    writer.WriteLine(_reportCriticalErrors);
                    writer.WriteLine(_memoryCleanerRegime);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private static bool _isLoad;

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
                    _transactionBeepIsActive = Convert.ToBoolean(reader.ReadLine());
                    _errorLogBeepIsActive = Convert.ToBoolean(reader.ReadLine());
                    _errorLogMessageBoxIsActive = Convert.ToBoolean(reader.ReadLine());

                    _labelInHeaderBotStation = reader.ReadLine();

                    if(_labelInHeaderBotStation == "True"
                        || _labelInHeaderBotStation == "False")
                    {
                        _labelInHeaderBotStation = "";
                    }

                    _rebootTradeUiLight = Convert.ToBoolean(reader.ReadLine());
                    _reportCriticalErrors = Convert.ToBoolean(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), out _memoryCleanerRegime);

                    reader.Close();
                }
            }
            catch (Exception)
            {
                _reportCriticalErrors = true;
                // ignore
            }
        }
    }

    public enum MemoryCleanerRegime
    {
        Disable,
        At5Minutes,
        At30Minutes,
        AtDay
    }
}