/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.OsTrader.RiskManager
{
    /// <summary>
    /// Risk Manager
    /// Риск Мэнеджер
    /// </summary>
    public class RiskManager
    {
        // static part with work flow / статическая часть с работой потока

        public static Task Watcher;

        /// <summary>
        /// risk managers who need to be serviced
        /// риск менеджеры которые нужно обслуживать
        /// </summary>
        public static List<RiskManager> RiskManagersToCheck = new List<RiskManager>();

        private static object _activatorLocker = new object();

        /// <summary>
        /// activate flow
        /// активировать поток
        /// </summary>
        public static void Activate()
        {
            lock (_activatorLocker)
            {
                if (Watcher != null)
                {
                    return;
                }
                Watcher = new Task(WatcherHome);
                Watcher.Start();
            }
        }

        /// <summary>
        /// place of work thread
        /// место работы потока
        /// </summary>
        public static async void WatcherHome()
        {
            while (true)
            {
                await Task.Delay(2000);

                for (int i = 0; i < RiskManagersToCheck.Count; i++)
                {
                    RiskManagersToCheck[i].CheckJournals();
                }

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }

        // service / сервис

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="nameBot">uid / имя робота</param>
        /// <param name="startProgram">the program that launched the class / программа которая запустила класс</param>
        public RiskManager(string nameBot, StartProgram startProgram)
        {
            _startProgram = startProgram;
            _name = nameBot + "RiskManager";
            MaxDrowDownToDayPersent = 1;
            Load();

            if (Watcher == null)
            {
                Activate();
            }
            RiskManagersToCheck.Add(this);
        }

        /// <summary>
        /// the program that launched the class
        /// какая программа запустила класс
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// name / имя 
        /// </summary>
        private string _name;

        /// <summary>
        /// maximum drawdown per day
        /// максимальная просадка на день
        /// </summary>
        public decimal MaxDrowDownToDayPersent;

        /// <summary>
        /// type of reaction
        /// тип реакции
        /// </summary>
        public RiskManagerReactionType ReactionType;

        /// <summary>
        /// is the manager included
        /// включен ли менеджер
        /// </summary>
        public bool IsActiv;

        /// <summary>
        /// clear previously loaded jorunals
        /// очистить от ранее загруженых журналов
        /// </summary>
        public void ClearJournals()
        {
            _journals = null;
        }

        /// <summary>
        /// load in Risk Manager
        /// подгрузить в Риск Менеджер журнал
        /// </summary>
        public void SetNewJournal(Journal.Journal newJournal)
        {
            try
            {
                if (_journals != null)
                {
                    for (int i = 0; i < _journals.Count; i++)
                    {
                        if (_journals[i].Name == newJournal.Name)
                        {
                            return;
                        }
                    }
                }

                if (_journals == null)
                {
                    _journals = new List<Journal.Journal>();
                }
                _journals.Add(newJournal);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// risk manager journals
        /// журналы риск менеджера
        /// </summary>
        private List<Journal.Journal> _journals;

        /// <summary>
        /// save
        /// сохранить
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + _name + @".txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @".txt"))
                {
                    MaxDrowDownToDayPersent = Convert.ToDecimal(reader.ReadLine());
                    IsActiv = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), false, out ReactionType);

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// load
        /// загрузить
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @".txt", false))
                {
                    writer.WriteLine(MaxDrowDownToDayPersent);
                    writer.WriteLine(IsActiv);
                    writer.WriteLine(ReactionType);

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// delete
        /// удалить
        /// </summary>
        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\" + _name + @".txt"))
                {
                    File.Delete(@"Engine\" + _name + @".txt");
                }

                RiskManagersToCheck.Remove(this);
                ClearJournals();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// show
        /// показать
        /// </summary>
        public void ShowDialog()
        {
            try
            {
                RiskManagerUi ui = new RiskManagerUi(this);
                ui.ShowDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// method in which we review logs for excess of allowable losses
        /// метод, в котором мы просматриваем журналы на превышение допустимых  норм убытков
        /// </summary>
        private void CheckJournals()
        {
            try
            {
                if (!IsActiv)
                {
                    return;
                }

                if (_startProgram != StartProgram.IsOsTrader)
                {
                    return;
                }

                if (_journals == null)
                {
                    return;
                }

                decimal profit = 0;

                for (int i = 0; i < _journals.Count; i++)
                {
                    profit += _journals[i].GetProfitFromThatDayInPersent();
                }

                if (profit < -Math.Abs(MaxDrowDownToDayPersent))
                {
                    IsActiv = false;

                    if (RiskManagerAlarmEvent != null)
                    {
                        RiskManagerAlarmEvent(ReactionType);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // logging / сообщения в лог 

        /// <summary>
        /// send new message
        /// выслать новое сообщение
        /// </summary>
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

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// send excess loss alert
        /// выслать оповещение о превышении убытков
        /// </summary>
        public event Action<RiskManagerReactionType> RiskManagerAlarmEvent;

    }

    /// <summary>
    /// manager's risk response reaction to too much loss
    /// реакция риск менеджера на слишком большой убыток
    /// </summary>
    public enum RiskManagerReactionType
    {
        /// <summary>
        /// pop up a window
        /// выдать всплывающее окно
        /// </summary>
        ShowDialog,

        /// <summary>
        /// close all positions and disable the robot
        /// закрыть все позиции и отключить робота
        /// </summary>
        CloseAndOff,

        /// <summary>
        /// none
        /// никакой
        /// </summary>
        None
    }
}
