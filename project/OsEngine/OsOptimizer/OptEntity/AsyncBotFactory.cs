using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.Robots;
using System;

namespace OsEngine.OsOptimizer.OptimizerEntity
{
    public class AsyncBotFactory
    {

        public AsyncBotFactory()
        {
            Thread f1 = new Thread(WorkerArea);
            f1.Name = "0";
            f1.Start();

            Thread f2 = new Thread(WorkerArea);
            f2.Name = "1";
            f2.Start();

            Thread f3 = new Thread(WorkerArea);
            f3.Name = "2";
            f3.Start();

            Thread f4 = new Thread(WorkerArea);
            f4.Name = "3";
            f4.Start();
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
            _botType = botType;

            firstNames.Clear();
            secondNames.Clear();
            firdNames.Clear();

            for (int i = 0; i < botsName.Count; i += 4)
            {
                firstNames.Add(botsName[i]);
            }
            for (int i = 1; i < botsName.Count; i += 4)
            {
                secondNames.Add(botsName[i]);
            }
            for (int i = 2; i < botsName.Count; i += 4)
            {
                firdNames.Add(botsName[i]);
            }
            for (int i = 3; i < botsName.Count; i += 4)
            {
                fourthNames.Add(botsName[i]);
            }

            _isScript = isScript;
            _startProgramm = startProgramm;
        }

        List<string> firstNames = new List<string>();
        List<string> secondNames = new List<string>();
        List<string> firdNames = new List<string>();
        List<string> fourthNames = new List<string>();

        public List<BotPanel> _bots = new List<BotPanel>();

        private string _botType;

        private bool _isScript;

        StartProgram _startProgramm;

        private void WorkerArea()
        {
            int num = Convert.ToInt32(Thread.CurrentThread.Name);

            while (true)
            {
                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (num == 0 && firstNames.Count == 0)
                {
                    Thread.Sleep(200);
                    continue;
                }
                if (num == 1 && secondNames.Count == 0)
                {
                    Thread.Sleep(200);
                    continue;
                }
                if (num == 2 && firdNames.Count == 0)
                {
                    Thread.Sleep(200);
                    continue;
                }
                if (num == 3 && fourthNames.Count == 0)
                {
                    Thread.Sleep(200);
                    continue;
                }

                //System.Windows.Forms.MessageBox.Show("Tram" + num);

                if (num == 0)
                {
                    for (int i = 0; i < firstNames.Count;)
                    {
                        BotPanel bot = BotFactory.GetStrategyForName(_botType, firstNames[0], _startProgramm, _isScript);
                        firstNames.RemoveAt(0);
                        lock (_botLocker)
                        {
                            _bots.Add(bot);
                        }
                    }
                }
                if (num == 1)
                {
                    for (int i = 0; i < secondNames.Count;)
                    {
                        BotPanel bot = BotFactory.GetStrategyForName(_botType, secondNames[0], _startProgramm, _isScript);
                        secondNames.RemoveAt(0);
                        lock (_botLocker)
                        {
                            _bots.Add(bot);
                        }
                    }
                }
                if (num == 2)
                {
                    for (int i = 0; i < firdNames.Count;)
                    {
                        BotPanel bot = BotFactory.GetStrategyForName(_botType, firdNames[0], _startProgramm, _isScript);
                        firdNames.RemoveAt(0);
                        lock (_botLocker)
                        {
                            _bots.Add(bot);
                        }
                    }
                }
                if (num == 3)
                {
                    for (int i = 0; i < fourthNames.Count;)
                    {
                        BotPanel bot = BotFactory.GetStrategyForName(_botType, fourthNames[0], _startProgramm, _isScript);
                        fourthNames.RemoveAt(0);
                        lock (_botLocker)
                        {
                            _bots.Add(bot);
                        }
                    }
                }
            }
        }
    }
}
