using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.PrimeSettings
{
    public class PrimeSettingsMaster
    {

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

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public static void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\PrimeSettings.txt", false)
                )
                {
                    writer.WriteLine(_transactionBeepIsActiv);
                    writer.WriteLine(_errorLogBeepIsActiv);
                    writer.WriteLine(_errorLogMessageBoxIsActiv);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        private static bool _isLoad;

        /// <summary>
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
                    Enum.TryParse(reader.ReadLine(), true, out _transactionBeepIsActiv);
                    Enum.TryParse(reader.ReadLine(), true, out _errorLogBeepIsActiv);
                    Enum.TryParse(reader.ReadLine(), true, out _errorLogMessageBoxIsActiv);
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }
    }
}
