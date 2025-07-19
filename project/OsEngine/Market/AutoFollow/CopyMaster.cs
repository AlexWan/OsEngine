/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.Market.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.AutoFollow
{
    public class CopyMaster
    {
        public void Activate()
        {
            SendLogMessage("Copy master activated.", LogMessageType.System);



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

        #region Log

        public event Action<string, LogMessageType> LogMessageEvent;

        public void SendLogMessage(string message, LogMessageType messageType)
        {
            message = "Proxy master.  " + message;
            LogMessageEvent?.Invoke(message, messageType);
        }

        #endregion
    }
}
