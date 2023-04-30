using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using OsEngine.Logging;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System.Threading;

namespace OsEngine.OsData
{
    public class OsDataMaster
    {
        public OsDataMaster()
        {
            _awaitUiMasterAloneTest = new AwaitObject(OsLocalization.Data.Label46, 100, 0, true);
            AwaitUi ui = new AwaitUi(_awaitUiMasterAloneTest);

            Task.Run(Load);
            ui.ShowDialog();
            Thread.Sleep(500);
        }

        public List<OsDataSet> Sets = new List<OsDataSet>();

        // set switching/переключение сетов

        /// <summary>
        /// active set/активный сет
        /// </summary>
        public OsDataSet SelectSet;

        public void SortSets()
        {
            if (Sets == null ||
                Sets.Count == 0)
            {
                return;
            }

            List<OsDataSet> sortSets = new List<OsDataSet>();

            for (int i = 0; i < Sets.Count; i++)
            {
                if (Sets[i].BaseSettings.Regime == DataSetState.On)
                {
                    sortSets.Add(Sets[i]);
                }
            }

            for (int i = 0; i < Sets.Count; i++)
            {
                if (Sets[i].BaseSettings.Regime == DataSetState.Off)
                {
                    sortSets.Add(Sets[i]);
                }
            }
            Sets = sortSets;
        }

        AwaitObject _awaitUiMasterAloneTest;

        /// <summary>
        /// load settings from file/загрузить настройки из файла
        /// </summary>
        private void Load()
        {
            if (!Directory.Exists("Data"))
            {
                Directory.CreateDirectory("Data");
            }

            // folder name is our name of the set/название папок это у нас название сетов

            string[] folders = Directory.GetDirectories("Data");

            if (folders != null 
                && folders.Length > 0)
            {
                string[] nameFolders = new string[folders.Length];

                for (int i = 0; i < folders.Length; i++)
                {
                    nameFolders[i] = folders[i].Split('\\')[1];
                }

                Sets.Clear();

                for (int i = 0; i < nameFolders.Length; i++)
                {
                    if (nameFolders[i].Split('_')[0] == "Set")
                    {
                        Sets.Add(new OsDataSet(nameFolders[i]));
                        Sets[Sets.Count - 1].NewLogMessageEvent += SendNewLogMessage;

                    }
                }
            }

            _awaitUiMasterAloneTest.Dispose();

            if(NeadUpDateTableEvent != null)
            {
                NeadUpDateTableEvent();
            }
        }

        /// <summary>
        /// send new message to log/выслать новое сообщение в лог
        /// </summary>
        void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// new message event to log/событие нового сообщения в лог
        /// </summary>
        public event Action<string, LogMessageType> NewLogMessageEvent;

        public event Action NeadUpDateTableEvent;
    }
}
