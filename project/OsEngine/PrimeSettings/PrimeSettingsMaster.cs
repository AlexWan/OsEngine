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
                    writer.WriteLine(_serverTestingIsActiv);
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
                    _transactionBeepIsActiv = Convert.ToBoolean(reader.ReadLine());
                    _errorLogBeepIsActiv = Convert.ToBoolean(reader.ReadLine());
                    _errorLogMessageBoxIsActiv = Convert.ToBoolean(reader.ReadLine());
                    _serverTestingIsActiv = Convert.ToBoolean(reader.ReadLine());

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
