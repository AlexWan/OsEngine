/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.OsTrader.RiskManager
{
    /// <summary>
    /// Риск Мэнеджер
    /// </summary>
    public class RiskManager
    {
        // статическая часть с работой потока

        /// <summary>
        /// поток 
        /// </summary>
        public static Thread Watcher;

        /// <summary>
        /// риск менеджеры которые нужно обслуживать
        /// </summary>
        public static List<RiskManager> RiskManagersToCheck = new List<RiskManager>();

        private static object _activatorLocker = new object();

        /// <summary>
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
                Watcher = new Thread(WatcherHome);
                Watcher.Name = "RiskManagerThread";
                Watcher.IsBackground = true;
                Watcher.Start();
            }
        }

        /// <summary>
        /// место работы потока
        /// </summary>
        public static void WatcherHome()
        {
            while (true)
            {
                Thread.Sleep(2000);

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

// сервис

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="nameBot">uid / имя робота</param>
        /// <param name="startProgram">программа которая запустила класс</param>
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
        /// какая программа запустила класс
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// имя 
        /// </summary>
        private string _name;

        /// <summary>
        /// максимальная просадка на день
        /// </summary>
        public decimal MaxDrowDownToDayPersent;

        /// <summary>
        /// тип реакции
        /// </summary>
        public RiskManagerReactionType ReactionType;

        /// <summary>
        /// включен ли менеджер
        /// </summary>
        public bool IsActiv;

        /// <summary>
        /// очистить от ранее загруженых журналов
        /// </summary>
        public void ClearJournals()
        {
            _journals = null;
        }

        /// <summary>
        /// подгрузить в Риск Менеджер журнал
        /// </summary>
        /// <param name="newJournal">новый журнал</param>
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
        /// журналы риск менеджера
        /// </summary>
        private List<Journal.Journal> _journals;

        /// <summary>
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
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
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

        // сообщения в лог 

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // если на нас никто не подписан и в логе ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        /// <summary>
        /// выслать оповещение о превышении убытков
        /// </summary>
        public event Action<RiskManagerReactionType> RiskManagerAlarmEvent;

    }

    /// <summary>
    /// реакция риск менеджера на слишком большой убыток
    /// </summary>
    public enum RiskManagerReactionType
    {
        /// <summary>
        /// выдать всплывающее окно
        /// </summary>
        ShowDialog,

        /// <summary>
        /// закрыть все позиции и отключить робота
        /// </summary>
        CloseAndOff,

        /// <summary>
        /// никакой
        /// </summary>
        None
    }
}
