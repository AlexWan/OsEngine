using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots;
using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.OsOptimizer.OptimizerEntity
{
    public class AsyncBotFactory
    {

        public AsyncBotFactory()
        {
            Task.Run(WorkerArea);
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
            }
        }

        public void CreateNewBots(List<string> botsName, string botType, bool isScript, StartProgram startProgramm)
        {
            lock (_lockStr)
            {
                _targetChange = true;
                _bots = new List<BotPanel>();
                _botType = botType;
                _botNames = botsName;
                _isScript = isScript;
                _startProgramm = startProgramm;
            }
        }

        public List<BotPanel> _bots = new List<BotPanel>();

        private List<string> _botNames;

        private string _botType;

        private bool _targetChange;

        private bool _isScript;

        StartProgram _startProgramm;

        private string _lockStr = "someStr";


        private void WorkerArea()
        {
            while (true)
            {
                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (_botNames == null ||
                    _botNames.Count == 0)
                {
                    Thread.Sleep(500);
                    continue;
                }

                for (int i = 0; i < _botNames.Count; i++)
                {
                    lock (_lockStr)
                    {
                        if (_targetChange == true)
                        {
                            Thread.Sleep(500);
                            _targetChange = false;
                            break;
                        }
                        BotPanel bot = BotFactory.GetStrategyForName(_botType, _botNames[i], _startProgramm, _isScript);

                        lock (_botLocker)
                        {
                            _bots.Add(bot);
                        }
                        if (i + 1 == _botNames.Count)
                        {
                            _botNames.Clear();
                            break;
                        }
                    }
                }


            }
        }
    }
}
