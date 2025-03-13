/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using System;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Attributes;
using System.Globalization;
using System.IO;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Robots.Screeners
{
    [Bot("PinBarVolatilityScreener")]
    public class PinBarVolatilityScreener : BotPanel
    {
        public PinBarVolatilityScreener(string name, StartProgram startProgram)
           : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            _tabScreener.CandleFinishedEvent += _tab_CandleFinishedEvent1;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            MaxPositions = CreateParameter("Max positions", 10, 0, 20, 1);

            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });

            Volume = CreateParameter("Volume", 10, 1.0m, 50, 4);

            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            ProcHeightStop = CreateParameter("Stop % from height of pattern", 20m, 0, 20, 1m);

            DaysVolatilityAdaptive = CreateParameter("Days volatility adaptive", 5, 0, 20, 1);

            HeightPinBarVolaPercent = CreateParameter("Height PinBar volatility percent", 30, 0, 20, 1m);

            SmaFilterIsOn = CreateParameter("Sma filter is on", true);

            SmaFilterLen = CreateParameter("Sma filter Len", 100, 100, 300, 10);

            if (startProgram == StartProgram.IsOsTrader)
            {
                LoadTradeSettings();
            }
            else if(startProgram == StartProgram.IsTester)
            {
                List<IServer> servers = ServerMaster.GetServers();

                if(servers != null 
                    && servers.Count > 0
                    && servers[0].ServerType == ServerType.Tester)
                {
                    TesterServer server = (TesterServer)servers[0];
                    server.TestingStartEvent += Server_TestingStartEvent;
                }
            }

            this.DeleteEvent += Screener_DeleteEvent;
        }

        private void Server_TestingStartEvent()
        {
            _tradeSettings.Clear();
        }

        public override string GetNameStrategyType()
        {
            return "PinBarVolatilityScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabScreener _tabScreener;

        // settings

        public StrategyParameterString Regime;

        public StrategyParameterInt MaxPositions;

        public StrategyParameterDecimal ProcHeightStop;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterInt DaysVolatilityAdaptive;

        public StrategyParameterDecimal HeightPinBarVolaPercent;

        public StrategyParameterBool SmaFilterIsOn;

        public StrategyParameterInt SmaFilterLen;

        // volatility adaptation

        private List<SecuritiesVolatilitySettings> _tradeSettings = new List<SecuritiesVolatilitySettings>();

        private void SaveTradeSettings()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
                    )
                {
                    for (int i = 0; i < _tradeSettings.Count; i++)
                    {
                        writer.WriteLine(_tradeSettings[i].GetSaveString());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void LoadTradeSettings()
        {
            if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string line = reader.ReadLine();

                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }

                        SecuritiesVolatilitySettings newSettings = new SecuritiesVolatilitySettings();
                        newSettings.LoadFromString(line);
                        _tradeSettings.Add(newSettings);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void Screener_DeleteEvent()
        {
            try
            {
                if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
                {
                    File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void AdaptPinBarHeight(List<Candle> candles, SecuritiesVolatilitySettings settings)
        {
            if (DaysVolatilityAdaptive.ValueInt <= 0
                || HeightPinBarVolaPercent.ValueDecimal <= 0)
            {
                return;
            }

            // 1 рассчитываем движение от хая до лоя внутри N дней

            decimal minValueInDay = decimal.MaxValue;
            decimal maxValueInDay = decimal.MinValue;

            List<decimal> volaInDaysPercent = new List<decimal>();

            DateTime date = candles[candles.Count - 1].TimeStart.Date;

            int days = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                Candle curCandle = candles[i];

                if (curCandle.TimeStart.Date < date)
                {
                    date = curCandle.TimeStart.Date;
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);

                    volaInDaysPercent.Add(volaPercentToday);


                    minValueInDay = decimal.MaxValue;
                    maxValueInDay = decimal.MinValue;
                }

                if (days >= DaysVolatilityAdaptive.ValueInt)
                {
                    break;
                }

                if (curCandle.High > maxValueInDay)
                {
                    maxValueInDay = curCandle.High;
                }
                if (curCandle.Low < minValueInDay)
                {
                    minValueInDay = curCandle.Low;
                }

                if (i == 0)
                {
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);
                    volaInDaysPercent.Add(volaPercentToday);
                }
            }

            if (volaInDaysPercent.Count == 0)
            {
                return;
            }

            // 2 усредняем это движение. Нужна усреднённая волатильность. процент

            decimal volaPercentSma = 0;

            for (int i = 0; i < volaInDaysPercent.Count; i++)
            {
                volaPercentSma += volaInDaysPercent[i];
            }

            volaPercentSma = volaPercentSma / volaInDaysPercent.Count;

            // 3 считаем размер свечей с учётом этой волатильности

            decimal allSoldiersHeight = volaPercentSma * (HeightPinBarVolaPercent.ValueDecimal / 100);

            settings.HeightPinBar = allSoldiersHeight;
            settings.LastUpdateTime = candles[candles.Count - 1].TimeStart;
        }

        // logic

        private void _tab_CandleFinishedEvent1(List<Candle> candles, BotTabSimple tab)
        {
            SecuritiesVolatilitySettings mySettings = null;

            for (int i = 0; i < _tradeSettings.Count; i++)
            {
                if (_tradeSettings[i].SecName == tab.Security.Name &&
                    _tradeSettings[i].SecClass == tab.Security.NameClass)
                {
                    mySettings = _tradeSettings[i];
                    break;
                }
            }

            if (mySettings == null)
            {
                mySettings = new SecuritiesVolatilitySettings();
                mySettings.SecName = tab.Security.Name;
                mySettings.SecClass = tab.Security.NameClass;
                _tradeSettings.Add(mySettings);
            }

            if (mySettings.LastUpdateTime.Date != candles[candles.Count - 1].TimeStart.Date)
            {
                AdaptPinBarHeight(candles, mySettings);

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    SaveTradeSettings();
                }
            }

            if (mySettings.HeightPinBar == 0)
            {
                return;
            }

            Logic(candles, tab, mySettings);
        }

        private void Logic(List<Candle> candles, BotTabSimple tab, SecuritiesVolatilitySettings settings)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            List<Position> openPositions = tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (Regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicOpenPosition(candles, tab, settings);
            }
            else
            {
                LogicClosePosition(candles, tab, settings);
            }
        }

        private void LogicOpenPosition(List<Candle> candles, BotTabSimple tab, SecuritiesVolatilitySettings settings)
        {
            if (_tabScreener.PositionsOpenAll.Count >= MaxPositions.ValueInt)
            {
                return;
            }
            decimal lastClose = candles[candles.Count - 1].Close;
            decimal lastOpen = candles[candles.Count - 1].Open;
            decimal lastHigh = candles[candles.Count - 1].High;
            decimal lastLow = candles[candles.Count - 1].Low;

            decimal lenCandlePercent = (lastHigh - lastLow) / (lastLow / 100);

            if (lenCandlePercent < settings.HeightPinBar)
            {
                return;
            }

            //  long
            if (Regime.ValueString != "OnlyShort")
            {
                if (lastClose >= lastHigh - ((lastHigh - lastLow) / 3) && lastOpen >= lastHigh - ((lastHigh - lastLow) / 3))
                {
                    if (SmaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, SmaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, SmaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            return;
                        }
                    }

                    tab.BuyAtMarket(GetVolume(tab));
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                if (lastClose <= lastLow + ((lastHigh - lastLow) / 3) && lastOpen <= lastLow + ((lastHigh - lastLow) / 3))
                {
                    if (SmaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, SmaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, SmaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue > smaPrev)
                        {
                            return;
                        }
                    }

                    tab.SellAtMarket(GetVolume(tab));
                }
            }
        }

        private void LogicClosePosition(List<Candle> candles, BotTabSimple tab, SecuritiesVolatilitySettings settings)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal heightPattern = settings.HeightPinBar;
                    decimal priceStop = _lastPrice - _lastPrice * ((heightPattern * (ProcHeightStop.ValueDecimal / 100))/100);
                    tab.CloseAtTrailingStopMarket(openPositions[i], priceStop);
                }
                else
                {
                    decimal heightPattern = settings.HeightPinBar;
                    decimal priceStop = _lastPrice + _lastPrice * ((heightPattern * (ProcHeightStop.ValueDecimal / 100)) / 100);
                    tab.CloseAtTrailingStopMarket(openPositions[i], priceStop);
                }
            }
        }

        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType.ValueString == "Contracts")
            {
                volume = Volume.ValueDecimal;
            }
            else if (VolumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = Volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Securiti.Lot != 0 &&
                        tab.Securiti.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Securiti.Lot);
                    }

                    volume = Math.Round(volume, tab.Securiti.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (VolumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (TradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Securiti.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Securiti.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }

        private decimal Sma(List<Candle> candles, int len, int index)
        {
            if (candles.Count == 0
                || index >= candles.Count
                || index <= 0)
            {
                return 0;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - len; i--)
            {
                countPoints++;
                summ += candles[i].Close;
            }

            if (countPoints == 0)
            {
                return 0;
            }

            return summ / countPoints;
        }
    }

    public class SecuritiesVolatilitySettings
    {
        public string SecName;

        public string SecClass;

        public decimal HeightPinBar;

        public DateTime LastUpdateTime;

        public string GetSaveString()
        {
            string result = "";

            result += SecName + "%";
            result += SecClass + "%";
            result += HeightPinBar + "%";
            result += LastUpdateTime.ToString(CultureInfo.InvariantCulture) + "%";

            return result;
        }

        public void LoadFromString(string str)
        {
            string[] array = str.Split('%');

            SecName = array[0];
            SecClass = array[1];
            HeightPinBar = array[2].ToDecimal();
            LastUpdateTime = Convert.ToDateTime(array[3], CultureInfo.InvariantCulture);
        }
    }
}