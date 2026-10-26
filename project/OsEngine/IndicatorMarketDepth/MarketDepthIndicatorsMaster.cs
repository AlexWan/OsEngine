/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;
using System.IO;

namespace OsEngine.IndicatorMarketDepth
{
    /// <summary>
    /// Хранит, рассчитывает и прорисовывает стаканные индикаторы для одного источника (BotTabSimple).
    /// </summary>
    public class MarketDepthIndicatorsMaster
    {
        public MarketDepthIndicatorsMaster(string name, StartProgram startProgram)
        {
            _name = name;
            _startProgram = startProgram;

            Load();
        }

        private string _name;

        private StartProgram _startProgram;

        public List<AMarketDepth> Indicators = new List<AMarketDepth>();

        public AMarketDepth CreateIndicator(string typeName, string name)
        {
            for (int i = 0; i < Indicators.Count; i++)
            {
                if (Indicators[i].Name == name)
                {
                    return null;
                }
            }

            AMarketDepth indicator = MarketDepthIndicatorsFactory.CreateByName(typeName, name, true, _startProgram);

            if (indicator == null)
            {
                return null;
            }

            indicator.NeedToReloadEvent += Indicator_NeedToReloadEvent;
            Indicators.Add(indicator);

            Save();

            if (IndicatorAddedEvent != null)
            {
                IndicatorAddedEvent(indicator);
            }

            return indicator;
        }

        public void DeleteIndicator(AMarketDepth indicator)
        {
            if (indicator == null || Indicators.Contains(indicator) == false)
            {
                return;
            }

            indicator.NeedToReloadEvent -= Indicator_NeedToReloadEvent;
            Indicators.Remove(indicator);
            indicator.Delete();

            Save();

            if (IndicatorRemovedEvent != null)
            {
                IndicatorRemovedEvent(indicator);
            }
        }

        public void Clear()
        {
            for (int i = Indicators.Count - 1; i >= 0; i--)
            {
                DeleteIndicator(Indicators[i]);
            }
        }

        public void Process(MarketDepth depth)
        {
            for (int i = 0; i < Indicators.Count; i++)
            {
                Indicators[i].Process(depth);
            }
        }

        public void ProcessTrade(Trade trade)
        {
            for (int i = 0; i < Indicators.Count; i++)
            {
                Indicators[i].ProcessTrade(trade);
            }
        }

        private string GetListFilePath()
        {
            return @"Engine\" + _name + @"MarketDepthIndicators.txt";
        }

        private void Load()
        {
            if (string.IsNullOrEmpty(_name)
                || _startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            string path = GetListFilePath();

            if (File.Exists(path) == false)
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    while (reader.EndOfStream == false)
                    {
                        string line = reader.ReadLine();

                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        string[] parts = line.Split('@');

                        if (parts.Length < 2)
                        {
                            continue;
                        }

                        string typeName = parts[0];
                        string indicatorName = parts[1];
                        bool canDelete = parts.Length > 2 ? Convert.ToBoolean(parts[2]) : true;

                        AMarketDepth indicator = MarketDepthIndicatorsFactory.CreateByName(typeName, indicatorName, canDelete, _startProgram);

                        if (indicator != null)
                        {
                            indicator.NeedToReloadEvent += Indicator_NeedToReloadEvent;
                            Indicators.Add(indicator);
                        }
                    }
                    reader.Close();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_name)
                || _startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(GetListFilePath(), false))
                {
                    for (int i = 0; i < Indicators.Count; i++)
                    {
                        writer.WriteLine(Indicators[i].GetType().Name + "@" + Indicators[i].Name + "@" + Indicators[i].CanDelete);
                    }

                    writer.Close();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Delete()
        {
            Clear();

            try
            {
                string path = GetListFilePath();

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Indicator_NeedToReloadEvent()
        {
            if (NeedToRePaintEvent != null)
            {
                NeedToRePaintEvent();
            }
        }

        public event Action<AMarketDepth> IndicatorAddedEvent;

        public event Action<AMarketDepth> IndicatorRemovedEvent;

        public event Action NeedToRePaintEvent;
    }
}
