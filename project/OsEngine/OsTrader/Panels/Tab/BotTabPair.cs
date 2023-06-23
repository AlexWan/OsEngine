/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.ClusterChart;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Connectors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;



namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    ///  tab - for trading pairs
    /// </summary>
    public class BotTabPair : IIBotTab
    {
        public BotTabPair(string name, StartProgram startProgram)
        {


        }

        /// <summary>
        /// source type
        /// </summary>
        public BotTabType TabType
        {
            get
            {
                return BotTabType.Pair;
            }
        }

        /// <summary>
        /// Unique robot name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Tab number
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// Whether the submission of events to the top is enabled or not
        /// </summary>
        public bool EventsIsOn
        {
            get
            {
                return _eventsIsOn;
            }
            set
            {
                if (_eventsIsOn == value)
                {
                    return;
                }
                _eventsIsOn = value;
            }
        }

        private bool _eventsIsOn = true;

        /// <summary>
        /// Is the emulator enabled
        /// </summary>
        public bool EmulatorIsOn { get; set; }

        /// <summary>
        /// Time of the last update of the candle
        /// </summary>
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

        public List<Journal.Journal> GetJournals()
        {
            try
            {
                List<Journal.Journal> journals = new List<Journal.Journal>();

               /* for (int i = 0; i < Tabs.Count; i++)
                {
                    journals.Add(Tabs[i].GetJournal());
                }*/

                return journals;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// Start drawing this robot
        /// </summary> 
        public void StartPaint(WindowsFormsHost host)
        {
            


        }





        // log

        /// <summary>
        /// Send new log message
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
        /// New log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    public class PairToTrade
    {
        public BotTabSimple Tab1;

        public BotTabSimple Tab2;

        public BotTabIndex Index;




    }
}