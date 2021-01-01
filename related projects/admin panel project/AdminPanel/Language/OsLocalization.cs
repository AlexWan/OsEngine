using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace AdminPanel.Language
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
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\local.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), true, out _curLocalization);
                    reader.Close();
                }
            }
            catch
            {

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

                    string[] locCur = locStrings[i].Split(':');

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

        //public static PrimeSettingsMasterUiLocal PrimeSettings = new PrimeSettingsMasterUiLocal();

        public static SettingsLocal SettingsLocal = new SettingsLocal();

        //public static ChartsLocal Charts = new ChartsLocal();

        public static EntityLocal Entity = new EntityLocal();

        //public static JournalLocal Journal = new JournalLocal();

        //public static LoggingLocal Logging = new LoggingLocal();

        //public static MarketLocal Market = new MarketLocal();

        //public static ConverterLocal Converter = new ConverterLocal();

        //public static DataLocal Data = new DataLocal();

        //public static MinerLocal Miner = new MinerLocal();

        //public static OptimizerLocal Optimizer = new OptimizerLocal();

        //public static TraderLocal Trader = new TraderLocal();

    }
}
