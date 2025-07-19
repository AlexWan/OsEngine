/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.Market.Proxy;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OsEngine.Market.AutoFollow
{
    public class CopyMaster
    {
        public void Activate()
        {
            LoadCopyTraders();

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
            if (!File.Exists(@"Engine\" + @"CopyTradersHub.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"CopyTradersHub.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string line = reader.ReadLine();

                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        CopyTrader newProxy = new CopyTrader();
                        newProxy.LoadFromString(line);
                        CopyTraders.Add(newProxy);
                    }

                    reader.Close();
                }
            }
            catch (Exception error)
            {
                //SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void SaveCopyTraders()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"CopyTradersHub.txt", false))
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
            CopyTrader newProxy = new CopyTrader();

            int actualNumber = 0;

            for (int i = 0; i < CopyTraders.Count; i++)
            {
                if (CopyTraders[i].Number >= actualNumber)
                {
                    actualNumber = CopyTraders[i].Number + 1;
                }
            }

            newProxy.Number = actualNumber;

            CopyTraders.Add(newProxy);
            SaveCopyTraders();

            return newProxy;
        }

        #endregion

        #region Log

        public event Action<string, LogMessageType> LogMessageEvent;

        public void SendLogMessage(string message, LogMessageType messageType)
        {
            message = "Copy master.  " + message;
            LogMessageEvent?.Invoke(message, messageType);
        }

        #endregion
    }

    public class CopyTrader
    {
        public int Number;

        public string Name;

        public CopyTraderType Type;

        public bool IsOn;

       

        public string GetStringToSave()
        {
            string result = IsOn + "%";
            result += Number + "%";


            return result;
        }

        public void LoadFromString(string saveStr)
        {
            IsOn = Convert.ToBoolean(saveStr.Split('%')[0]);
            Number = Convert.ToInt32(saveStr.Split('%')[1]);

        }
    }

    public enum CopyTraderType
    {
        Portfolio,
        Robot
    }
}
