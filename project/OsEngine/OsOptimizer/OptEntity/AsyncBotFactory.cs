/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots;
using System;
using OsEngine.Logging;

namespace OsEngine.OsOptimizer.OptimizerEntity
{
    public class AsyncBotFactory
    {

        public AsyncBotFactory()
        {
            for (int i = 0; i < 10; i++)
            {
                _botsToStart.Add(new List<string>());
                Thread worker = new Thread(WorkerArea);
                worker.Name = i.ToString();
                worker.Start();
            }
        }

        private string _botLocker = "botLocker";

        public BotPanel GetBot(string botType, string botName)
        {
            BotPanel bot = null;

            while (true)
            {
                for (int i = 0; i < _bots.Count; i++)
                {
                    if (_bots[i] == null)
                    {
                        continue;
                    }

                    if (_bots[i].NameStrategyUniq == botName &&
                        _bots[i].GetNameStrategyType() == botType)
                    {
                        lock (_botLocker)
                        {
                            bot = _bots[i];
                            _bots.RemoveAt(i);
                        }

                        return bot;
                    }
                }
                Thread.Sleep(1);
            }
        }

        public void CreateNewBots(List<string> botsName, string botType, bool isScript, StartProgram startProgramm)
        {
            _botType = botType;
            _isActivate = false;
            for (int i = 0; i < _botsToStart.Count; i++)
            {
                List<string> names = _botsToStart[i];

                for (int i2 = i; i2 < botsName.Count; i2 += _botsToStart.Count)
                {
                    names.Add(botsName[i2]);
                }
            }

            _isScript = isScript;
            _startProgramm = startProgramm;
            _isActivate = true;
        }

        bool _isActivate;

        List<List<string>> _botsToStart = new List<List<string>>();

        public List<BotPanel> _bots = new List<BotPanel>();

        private string _botType;

        private bool _isScript;

        StartProgram _startProgramm;

        private void WorkerArea()
        {
            int num = Convert.ToInt32(Thread.CurrentThread.Name);

            while (true)
            {
                try
                {
                    Thread.Sleep(10);
                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_isActivate == false)
                    {
                        continue;
                    }

                    if (_botsToStart[num].Count != 0)
                    {
                        Load(_botsToStart[num]);
                    }
                }
                catch (Exception e)
                {
                    SendLogMessage("Optimizer critical error. \n Can`t create bot. Error: " + e.ToString(),LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        private void Load(List<string> names)
        {
            while (names.Count != 0)
            {

                BotPanel bot = BotFactory.GetStrategyForName(_botType, names[0], _startProgramm, _isScript);

                try
                {
                    names.RemoveAt(0);
                }
                catch
                {
                    // ignore
                }
             
                lock (_botLocker)
                {
                    _bots.Add(bot);
                }
            }
        }

        public void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
