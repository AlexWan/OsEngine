/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;

namespace OsEngine.Language
{
    public class OsLocalization
    {
        public enum OsLocalType
        {
            None,

            Ru,

            Eng,
        }

        public static List<OsLocalType> GetExistLocalizationTypes()
        {
            List<OsLocalType> localizations = new List<OsLocalType>();

            localizations.Add(OsLocalType.Eng);
            localizations.Add(OsLocalType.Ru);

            return localizations;
        }

        public static string CurLocalizationCode
        {
            get
            {
                if (_curLocalization == OsLocalType.Eng)
                {
                    return "en-US";
                }
                else if(_curLocalization == OsLocalType.Ru)
                {
                    return "ru-RU";
                }

                return "en-US";
            }
        }

        // h:mm:ss tt
        // H:mm:ss
        public static string LongTimePattern
        {
            get
            {
                return _longTimePattern;
            }
            set
            {
                if (_longTimePattern != value)
                {
                    _longTimePattern = value;
                    Save();
                }
            }
        }

        private static string _longTimePattern;

        // M/d/yyyy
        // dd.MM.yyyy
        public static string ShortDatePattern
        {
            get
            {
                return _shortDatePattern;
            }
            set
            {
                if (_shortDatePattern != value)
                {
                    _shortDatePattern = value;
                    Save();
                }
            }
        }

        private static string _shortDatePattern;

        public static CultureInfo CurCulture
        {
            get
            {
                CultureInfo culture = new CultureInfo(CurLocalizationCode);

                if(_longTimePattern == null)
                {
                    Load();
                }

                if(_longTimePattern != null)
                {
                    culture.DateTimeFormat.LongTimePattern = _longTimePattern;
                }

                if (_shortDatePattern != null)
                {
                    culture.DateTimeFormat.ShortDatePattern = _shortDatePattern;
                    if(_shortDatePattern == "M/d/yyyy")
                    {
                        culture.DateTimeFormat.DateSeparator = "/";
                        culture.DateTimeFormat.AMDesignator = "AM";
                        culture.DateTimeFormat.PMDesignator = "PM";
                    }
                    else
                    {
                        culture.DateTimeFormat.DateSeparator = ".";
                    }
                }
                

                return culture;
            }
        }

        public static string ShortDateFormatString
        {
            get
            {
                CultureInfo culture = CurCulture;

                return CurCulture.DateTimeFormat.ShortDatePattern;
            }
        }

        public static OsLocalType CurLocalization
        {
            get
            {
                if (_curLocalization == OsLocalType.None)
                {
                    Load();
                    if (_curLocalization == OsLocalType.None)
                    {
                        _curLocalization = OsLocalType.Eng;
                    }
                }
                return _curLocalization;
            }
            set
            {
                if (_curLocalization == value)
                {
                    return;
                }
                _curLocalization = value;

                // System.Threading.Thread.CurrentThread.CurrentUICulture = OsLocalization.CurCulture;
                // System.Threading.Thread.CurrentThread.CurrentCulture = OsLocalization.CurCulture;

                LocalizationTypeChangeEvent?.Invoke();
                Save();
            }
        }

        private static OsLocalType _curLocalization;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public static void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\local.txt", false)
                )
                {
                    writer.WriteLine(_curLocalization);
                    writer.WriteLine(_longTimePattern);
                    writer.WriteLine(_shortDatePattern);
                    writer.Close();
                }
            }
            catch
            {
               
            }
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private static void Load()
        {
            if (!File.Exists(@"Engine\local.txt"))
            {
                _longTimePattern = "H:mm:ss";
                _shortDatePattern = "dd.MM.yyyy";
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\local.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out _curLocalization);
                    _longTimePattern = reader.ReadLine();
                    _shortDatePattern = reader.ReadLine();
                    reader.Close();
                }

                // System.Threading.Thread.CurrentThread.CurrentUICulture = OsLocalization.CurCulture;
                // System.Threading.Thread.CurrentThread.CurrentCulture = OsLocalization.CurCulture;
            }
            catch 
            {
               
            }

            if(string.IsNullOrEmpty(_longTimePattern))
            {
                _longTimePattern = "H:mm:ss";
            }
            if(string.IsNullOrEmpty(_shortDatePattern))
            {
                _shortDatePattern = "dd.MM.yyyy";
            }
        }

        public static event Action LocalizationTypeChangeEvent;

        public static string ConvertToLocString(string str)
        {
            try
            {
                //"Eng:Main&Ru:Главное меню&"

                string[] locStrings = str.Split('_');

                string engLoc = "";

                for (int i = 0; i < locStrings.Length; i++)
                {
                    if (locStrings[i] == "" || locStrings[i] == " ")
                    {
                        continue;
                    }

                    string [] locCur = locStrings[i].Split(':');

                    OsLocalType cultureTypeCur;
                    if (Enum.TryParse(locCur[0], out cultureTypeCur))
                    {
                        if (cultureTypeCur == CurLocalization)
                        {
                            return locCur[1];
                        }
                        if (cultureTypeCur == OsLocalType.Eng)
                        {
                            engLoc = locCur[1];
                        }
                    }
                }

                return engLoc;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
                return "error";
            }

        }

        public static MainWindowLocal MainWindow = new MainWindowLocal();

        public static PrimeSettingsMasterUiLocal PrimeSettings = new PrimeSettingsMasterUiLocal();

        public static AlertsLocal Alerts = new AlertsLocal();

        public static ChartsLocal Charts = new ChartsLocal();

        public static EntityLocal Entity = new EntityLocal();

        public static JournalLocal Journal = new JournalLocal();

        public static LoggingLocal Logging = new LoggingLocal();

        public static MarketLocal Market = new MarketLocal();

        public static ConverterLocal Converter = new ConverterLocal();

        public static DataLocal Data = new DataLocal();

        public static MinerLocal Miner = new MinerLocal();

        public static OptimizerLocal Optimizer = new OptimizerLocal();

        public static TraderLocal Trader = new TraderLocal();

    }
}