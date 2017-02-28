/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Threading;
using OsEngine.Market.Servers;

namespace OsEngine.Entity
{
    /// <summary>
    /// генератор номеров для сделок и ордеров внутри робота
    /// </summary>
    public class NumberGen
    {
        private static bool _isFirstTime = true;
        private static void SaverSpace()
        {
            while (true)
            {
                Thread.Sleep(500);

                if (_neadToSave)
                {
                    _neadToSave = false;
                    Save();
                }
            }
        }

        private static bool _neadToSave;

        /// <summary>
        /// текущий номер последней сделки
        /// </summary>
        private static int _numberDeal;

        /// <summary>
        /// текущий номер последнего ордера
        /// </summary>
        private static int _numberOrder;

        /// <summary>
        /// взять номер для сделки
        /// </summary>
        public static int GetNumberDeal()
        {
            if (_isFirstTime == true && !ServerMaster.IsTester)
            {
                _isFirstTime = false;
                Load();

                Thread saver = new Thread(SaverSpace);
                saver.IsBackground = true;
                saver.Start();
            }

            _numberDeal++;

            _neadToSave = true;
            return _numberDeal;
        }

        /// <summary>
        /// взять номер для ордера
        /// </summary>
        public static int GetNumberOrder()
        {
            if (_isFirstTime == true && !ServerMaster.IsTester)
            {
                _isFirstTime = false;
                Load();

                Thread saver = new Thread(SaverSpace);
                saver.IsBackground = true;
                saver.Start();
            }

            _numberOrder++;
            _neadToSave = true;
            return _numberOrder;
        }

        /// <summary>
        /// загрузить
        /// </summary>
        private static void Load()
        {
            if (!File.Exists(@"Engine\" + @"NumberGen.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"NumberGen.txt"))
                {
                    _numberDeal = Convert.ToInt32(reader.ReadLine());
                    _numberOrder = Convert.ToInt32(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }

        /// <summary>
        /// сохранить
        /// </summary>
        private static void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"NumberGen.txt", false))
                {
                    writer.WriteLine(_numberDeal);
                    writer.WriteLine(_numberOrder);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // отправить в лог
            }
        }
    }
}
