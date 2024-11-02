/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using OsEngine.Logging;
using OsEngine.Entity;
using OsEngine.Language;
using System.Threading;

namespace OsEngine.OsData
{
    public class OsDataMaster
    {
        #region Service

        public OsDataMaster()
        {
            _awaitUiMasterAloneTest = new AwaitObject(OsLocalization.Data.Label46, 100, 0, true);
            AwaitUi ui = new AwaitUi(_awaitUiMasterAloneTest);

            Task.Run(Load);
            ui.ShowDialog();
            Thread.Sleep(500);
        }

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

            if (NeedUpDateTableEvent != null)
            {
                NeedUpDateTableEvent();
            }
        }

        public List<OsDataSet> Sets = new List<OsDataSet>();

        private AwaitObject _awaitUiMasterAloneTest;

        public event Action NeedUpDateTableEvent;

        #endregion

        #region Set switching

        public OsDataSet SelectedSet;

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

        #endregion

        #region Logging

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (NewLogMessageEvent != null)
            {
                NewLogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> NewLogMessageEvent;

        #endregion
    }
}