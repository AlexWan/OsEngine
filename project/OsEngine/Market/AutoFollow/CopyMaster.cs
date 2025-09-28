/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.Market.AutoFollow
{
    public class CopyMaster
    {
        public void Activate()
        {
            LoadCopyTraders();

            LogCopyMaster = new Log("CopyMaster", StartProgram.IsOsTrader);
            LogCopyMaster.Listen(this);

            SendLogMessage("Copy master activated. Copy traders: " + CopyTraders.Count, LogMessageType.System);
      
            
        }

        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new CopyMasterUi(this);
                _ui.Show();
                _ui.Closed += _ui_Closed;
            }
            else
            {
                if (_ui.WindowState == System.Windows.WindowState.Minimized)
                {
                    _ui.WindowState = System.Windows.WindowState.Normal;
                }

                _ui.Activate();
            }
        }

        private void _ui_Closed(object sender, EventArgs e)
        {
            _ui = null;
        }

        private CopyMasterUi _ui;

        #region CopyTrader hub

        public List<CopyTrader> CopyTraders = new List<CopyTrader>();

        private void LoadCopyTraders()
        {
            if(Directory.Exists(@"Engine\CopyTrader\") == false)
            {
                Directory.CreateDirectory(@"Engine\CopyTrader\");

            }

            if (!File.Exists(@"Engine\CopyTrader\" + @"CopyTradersHub.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\CopyTrader\" + @"CopyTradersHub.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string line = reader.ReadLine();

                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        CopyTrader newCopyTrader = new CopyTrader(line);
                        newCopyTrader.NeedToSaveEvent += NewCopyTrader_NeedToSaveEvent;
                        CopyTraders.Add(newCopyTrader);
                    }

                    reader.Close();
                }
            }
            catch
            {
                // игнор
            }
        }

        private void NewCopyTrader_NeedToSaveEvent()
        {
            SaveCopyTraders();
        }

        public void SaveCopyTraders()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\CopyTrader\" + @"CopyTradersHub.txt", false))
                {
                    for (int i = 0; i < CopyTraders.Count; i++)
                    {
                        writer.WriteLine(CopyTraders[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public CopyTrader CreateNewCopyTrader()
        {
            int actualNumber = 0;

            for (int i = 0; i < CopyTraders.Count; i++)
            {
                if (CopyTraders[i].Number >= actualNumber)
                {
                    actualNumber = CopyTraders[i].Number + 1;
                }
            }

            CopyTrader newCopyTrader = new CopyTrader(actualNumber);
            newCopyTrader.NeedToSaveEvent += NewCopyTrader_NeedToSaveEvent;
            CopyTraders.Add(newCopyTrader);
            SaveCopyTraders();

            return newCopyTrader;
        }

        public void RemoveCopyTraderAt(int number)
        {
            for (int i = 0; i < CopyTraders.Count; i++)
            {
                if (CopyTraders[i].Number == number)
                {
                    CopyTraders[i].ClearDelete();
                    CopyTraders[i].NeedToSaveEvent -= NewCopyTrader_NeedToSaveEvent;
                    CopyTraders.RemoveAt(i);
                    SaveCopyTraders();
                    return;
                }
            }
        }

        #endregion

        #region Log

        public Log LogCopyMaster;

        public event Action<string, LogMessageType> LogMessageEvent;

        public void SendLogMessage(string message, LogMessageType messageType)
        {
            message = "Copy master.  " + message;
            LogMessageEvent?.Invoke(message, messageType);
        }

        #endregion
    }
}
