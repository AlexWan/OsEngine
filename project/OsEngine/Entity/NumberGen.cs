/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Threading.Tasks;
using OsEngine.Market;

namespace OsEngine.Entity
{
    /// <summary>
    /// number generator for deals and orders inside the robot
    /// генератор номеров для сделок и ордеров внутри робота
    /// </summary>
    public class NumberGen
    {
        private static bool _isFirstTime = true;
        private static async void SaverSpace()
        {
            while (true)
            {
                await Task.Delay(500);

                if (_neadToSave)
                {
                    _neadToSave = false;
                    Save();
                }

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }

        private static bool _neadToSave;

        /// <summary>
        /// current number of the last transaction
        /// текущий номер последней сделки
        /// </summary>
        private static int _numberDealForRealTrading;

        /// <summary>
        /// current number of the last order
        /// текущий номер последнего ордера
        /// </summary>
        private static int _numberOrderForRealTrading;

        /// <summary>
        /// current number of the last transaction for tests
        /// текущий номер последней сделки для тестов
        /// </summary>
        private static int _numberDealForTesting;

        /// <summary>
        /// current number of the last order for tests
        /// текущий номер последнего ордера для тестов
        /// </summary>
        private static int _numberOrderForTesting;

        private static object _locker = new object();

        /// <summary>
        /// take a number for a deal
        /// взять номер для сделки
        /// </summary>
        public static int GetNumberDeal(StartProgram startProgram)
        {
            lock (_locker)
            {
                if (startProgram == StartProgram.IsOsTrader)
                {
                    return GetNumberForRealTrading();
                }
                else
                {
                    return GetNumberForTesting();
                }
            }
        }

        private static int GetNumberForRealTrading()
        {
            if (_isFirstTime)
            {
                _isFirstTime = false;
                Load();

                Task task = new Task(SaverSpace);
                task.Start();
            }

            _numberDealForRealTrading++;

            _neadToSave = true;
            return _numberDealForRealTrading;
        }

        private static int GetNumberForTesting()
        {
            _numberDealForTesting++;
            return _numberDealForTesting;
        }

        /// <summary>
        /// take the order number
        /// взять номер для ордера
        /// </summary>
        public static int GetNumberOrder(StartProgram startProgram)
        {
            lock (_locker)
            {
                if (startProgram == StartProgram.IsOsTrader)
                {
                    return GetNumberOrderForRealTrading();
                }
                else
                {
                    return GetNumberOrderForTesting();
                }
            }
        }

        private static int GetNumberOrderForRealTrading()
        {
            if (_isFirstTime)
            {
                _isFirstTime = false;
                Load();

                Task task = new Task(SaverSpace);
                task.Start();
            }

            _numberOrderForRealTrading++;
            _neadToSave = true;
            return _numberOrderForRealTrading;
        }

        private static int GetNumberOrderForTesting()
        {
            _numberOrderForTesting++;
            return _numberOrderForTesting;
        }

        /// <summary>
        /// load
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
                    _numberDealForRealTrading = Convert.ToInt32(reader.ReadLine());
                    _numberOrderForRealTrading = Convert.ToInt32(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                //send to log
                // отправить в лог
            }
        }

        /// <summary>
        /// save
        /// сохранить
        /// </summary>
        private static void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"NumberGen.txt", false))
                {
                    writer.WriteLine(_numberDealForRealTrading);
                    writer.WriteLine(_numberOrderForRealTrading);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                //send to log
                // отправить в лог
            }
        }
    }
}
