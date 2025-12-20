/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using System;
using System.Diagnostics;
using System.Threading;

namespace OsEngine.OsTrader.MemoryRH
{
    public class MemoryCleaner
    {
        public MemoryCleaner(int maxTimeWithNoCleaning)
        {
            _maxTimeWithNoCleaning = maxTimeWithNoCleaning;
            _timeCreateObj = DateTime.Now;
            Thread worker = new Thread(WorkMethod);
            worker.Start();
        }

        private int _maxTimeWithNoCleaning;

        private DateTime _lastStartTime;

        private DateTime _timeCreateObj;

        public void WorkMethod()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    // 1 первый запуск через пять минут после старта

                    if (_lastStartTime == DateTime.MinValue)
                    {
                        if(_timeCreateObj.AddMinutes(5) < DateTime.Now)
                        {
                            _lastStartTime = DateTime.Now;

                            CleanUpSystem();
                        }
                    }
                    else if(_lastStartTime.AddMinutes(_maxTimeWithNoCleaning) < DateTime.Now)
                    {// 2 второй запуск по промежутку
                        _lastStartTime = DateTime.Now;

                        CleanUpSystem();
                    }
                    else if (_maxTimeWithNoCleaning == 1440
                        && DateTime.Now.Date != _lastStartTime.Date)
                    {// 3 третий запуск. Раз в день. Делаем в 12 часов ночи, при смене дня
                        _lastStartTime = DateTime.Now;

                        CleanUpSystem();
                    }
                }
                catch(Exception e)
                {
                    SendNewLogMessage(e.ToString(),LogMessageType.Error);
                }
            }
        }

        private void CleanUpSystem()
        {
            string curDir = Environment.CurrentDirectory;

            string dirExe = curDir + "\\MemoryRH\\ByeByeBill.exe";

            try
            {
                Process.Start(dirExe);
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }

        }

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;
    }


}
