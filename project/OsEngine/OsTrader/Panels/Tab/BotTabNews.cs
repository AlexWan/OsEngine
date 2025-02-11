using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.OsTrader.Panels.Tab
{
    public class BotTabNews : IIBotTab
    {
        public BotTabType TabType { get { return BotTabType.News; } }

        public string TabName { get; set; }

        public int TabNum { get; set; }

        public bool EventsIsOn { get; set; }

        public bool EmulatorIsOn { get; set; }

        public DateTime LastTimeCandleUpdate { get; set; }

        public void Clear()
        {
            

        }

        public void Delete()
        {
        

        }

        public void StopPaint()
        {
            

        }

        public event Action TabDeletedEvent;

        public event Action<string, LogMessageType> LogMessageEvent;
    }
}