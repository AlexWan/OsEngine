/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.IO;
using System.Globalization;

namespace OsEngine.Robots.Screeners
{
    [Bot("ThreeSoldierAdaptiveScreener")]
    public class ThreeSoldierAdaptiveScreener : BotPanel
    {
        public StrategyParameterString Regime;
        public StrategyParameterInt MaxPositions;
        public StrategyParameterDecimal ProcHeightTake;
        public StrategyParameterDecimal ProcHeightStop;
        public StrategyParameterDecimal Slippage;
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterInt DaysVolatilityAdaptive;
        public StrategyParameterDecimal HeightSoldiersVolaPercent;
        public StrategyParameterDecimal MinHeightOneSoldiersVolaPercent;

        public StrategyParameterBool SmaFilterIsOn;
        public StrategyParameterInt SmaFilterLen;

        private BotTabScreener _tabScreener;

        public ThreeSoldierAdaptiveScreener(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            _tabScreener.CandleFinishedEvent += _tab_CandleFinishedEvent1;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            MaxPositions = CreateParameter("Max positions", 5, 0, 20, 1);

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });

            Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);

            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            Slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            ProcHeightTake = CreateParameter("Profit % from height of pattern", 50m, 0, 20, 1m);

            ProcHeightStop = CreateParameter("Stop % from height of pattern", 20m, 0, 20, 1m);

            DaysVolatilityAdaptive = CreateParameter("Days volatility adaptive", 1, 0, 20, 1);

            HeightSoldiersVolaPercent = CreateParameter("Height soldiers volatility percent", 5, 0, 20, 1m);

            MinHeightOneSoldiersVolaPercent = CreateParameter("Min height one soldier volatility percent", 1, 0, 20, 1m);

            SmaFilterIsOn = CreateParameter("Sma filter is on", true);

            SmaFilterLen = CreateParameter("Sma filter Len", 100, 100, 300, 10);

            Description = "Trading robot Three Soldiers adaptive by volatility. " +
                "When forming a pattern of three growing / falling candles, " +
                "the entrance to the countertrend with a fixation on a profit or a stop";

            if (startProgram == StartProgram.IsOsTrader)
            {
                LoadTradeSettings();
            }

            this.DeleteEvent += ThreeSoldierAdaptiveScreener_DeleteEvent;
        }

        public override string GetNameStrategyType()
        {
            return "ThreeSoldierAdaptiveScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // volatility adaptation

        private List<SecuritiesTradeSettings> _tradeSettings = new List<SecuritiesTradeSettings>();

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

                        SecuritiesTradeSettings newSettings = new SecuritiesTradeSettings();
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

        private void ThreeSoldierAdaptiveScreener_DeleteEvent()
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

        private void AdaptSoldiersHeight(List<Candle> candles, SecuritiesTradeSettings settings)
        {
            if (DaysVolatilityAdaptive.ValueInt <= 0
                || HeightSoldiersVolaPercent.ValueDecimal <= 0
                || MinHeightOneSoldiersVolaPercent.ValueDecimal <= 0)
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

            decimal allSoldiersHeight = volaPercentSma * (HeightSoldiersVolaPercent.ValueDecimal / 100);
            decimal oneSoldiersHeight = volaPercentSma * (MinHeightOneSoldiersVolaPercent.ValueDecimal / 100);

            settings.HeightSoldiers = allSoldiersHeight;
            settings.MinHeightOneSoldier = oneSoldiersHeight;
            settings.LastUpdateTime = candles[candles.Count - 1].TimeStart;
        }

        // logic

        private void _tab_CandleFinishedEvent1(List<Candle> candles, BotTabSimple tab)
        {
            SecuritiesTradeSettings mySettings = null;

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
                mySettings = new SecuritiesTradeSettings();
                mySettings.SecName = tab.Security.Name;
                mySettings.SecClass = tab.Security.NameClass;
                _tradeSettings.Add(mySettings);
            }

            if (mySettings.LastUpdateTime.Date != candles[candles.Count - 1].TimeStart.Date)
            {
                AdaptSoldiersHeight(candles, mySettings);

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    SaveTradeSettings();
                }
            }

            if (mySettings.HeightSoldiers == 0 ||
                mySettings.MinHeightOneSoldier == 0)
            {
                return;
            }

            Logic(candles, tab, mySettings);
        }

        private void Logic(List<Candle> candles, BotTabSimple tab, SecuritiesTradeSettings settings)
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
                LogicClosePosition(candles, tab);
            }
        }

        private void LogicOpenPosition(List<Candle> candles, BotTabSimple tab, SecuritiesTradeSettings settings)
        {
            if (_tabScreener.PositionsOpenAll.Count >= MaxPositions.ValueInt)
            {
                return;
            }

            decimal _lastPrice = candles[candles.Count - 1].Close;

            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < settings.HeightSoldiers)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 3].Open - candles[candles.Count - 3].Close)
                / (candles[candles.Count - 3].Close / 100) < settings.MinHeightOneSoldier)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 2].Open - candles[candles.Count - 2].Close)
                / (candles[candles.Count - 2].Close / 100) < settings.MinHeightOneSoldier)
            {
                return;
            }
            if (Math.Abs(candles[candles.Count - 1].Open - candles[candles.Count - 1].Close)
                / (candles[candles.Count - 1].Close / 100) < settings.MinHeightOneSoldier)
            {
                return;
            }

            //  long
            if (Regime.ValueString != "OnlyShort")
            {
                if (candles[candles.Count - 3].Open < candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open < candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
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

                    tab.BuyAtLimit(GetVolume(tab), _lastPrice + _lastPrice * (Slippage.ValueDecimal / 100));
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                if (candles[candles.Count - 3].Open > candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open > candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open > candles[candles.Count - 1].Close)
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

                    tab.SellAtLimit(GetVolume(tab), _lastPrice - _lastPrice * (Slippage.ValueDecimal / 100));
                }
            }

            return;

        }

        private void LogicClosePosition(List<Candle> candles, BotTabSimple tab)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = tab.PositionsOpenAll;
            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].StopOrderPrice != 0)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy)
                {
                    decimal heightPattern =
                        Math.Abs(tab.CandlesAll[tab.CandlesAll.Count - 4].Open - tab.CandlesAll[tab.CandlesAll.Count - 2].Close);

                    decimal priceStop = _lastPrice - (heightPattern * ProcHeightStop.ValueDecimal) / 100;
                    decimal priceTake = _lastPrice + (heightPattern * ProcHeightTake.ValueDecimal) / 100;
                    tab.CloseAtStop(openPositions[i], priceStop, priceStop - priceStop * (Slippage.ValueDecimal / 100));
                    tab.CloseAtProfit(openPositions[i], priceTake, priceTake - priceStop * (Slippage.ValueDecimal / 100));
                }
                else
                {
                    decimal heightPattern = Math.Abs(tab.CandlesAll[tab.CandlesAll.Count - 2].Close - tab.CandlesAll[tab.CandlesAll.Count - 4].Open);
                    decimal priceStop = _lastPrice + (heightPattern * ProcHeightStop.ValueDecimal) / 100;
                    decimal priceTake = _lastPrice - (heightPattern * ProcHeightTake.ValueDecimal) / 100;
                    tab.CloseAtStop(openPositions[i], priceStop, priceStop + priceStop * (Slippage.ValueDecimal / 100));
                    tab.CloseAtProfit(openPositions[i], priceTake, priceTake + priceStop * (Slippage.ValueDecimal / 100));
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
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
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

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
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

    public class SecuritiesTradeSettings
    {
        public string SecName;

        public string SecClass;

        public decimal HeightSoldiers;

        public decimal MinHeightOneSoldier;

        public DateTime LastUpdateTime;

        public string GetSaveString()
        {
            string result = "";

            result += SecName + "%";
            result += SecClass + "%";
            result += HeightSoldiers + "%";
            result += MinHeightOneSoldier + "%";
            result += LastUpdateTime.ToString(CultureInfo.InvariantCulture) + "%";

            return result;
        }

        public void LoadFromString(string str)
        {
            string[] array = str.Split('%');

            SecName = array[0];
            SecClass = array[1];
            HeightSoldiers = array[2].ToDecimal();
            MinHeightOneSoldier = array[3].ToDecimal();
            LastUpdateTime = Convert.ToDateTime(array[4], CultureInfo.InvariantCulture);
        }
    }
}