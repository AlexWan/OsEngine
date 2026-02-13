/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.IO;
using System.Globalization;
using OsEngine.OsTrader.Grids;
using OsEngine.Language;

/* Description
Grid Trend screener. Adaptive by volatility. 
We turn on the grid on when forming a pattern of three growing / falling candles
Stop by lifetime and positions count
Additionally: Work days / Non-trading periods intraday
Trailing Up/Down is on
 */

namespace OsEngine.Robots.Grids
{
    [Bot("GridScreenerAdaptiveSoldiers")]
    public class GridScreenerAdaptiveSoldiers : BotPanel
    {
        #region Constructor, settings, service

        private StrategyParameterString _regime;
        private StrategyParameterInt _maxGridsCount;

        private StrategyParameterInt _daysVolatilityAdaptive;
        private StrategyParameterDecimal _heightSoldiersVolaPercent;
        private StrategyParameterDecimal _minHeightOneSoldiersVolaPercent;

        private StrategyParameterBool _smaFilterIsOn;
        private StrategyParameterInt _smaFilterLen;

        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        private StrategyParameterInt _linesCount;
        private StrategyParameterDecimal _linesStep;
        private StrategyParameterDecimal _profitValue;
        private StrategyParameterInt _closePositionsCountToCloseGrid;
        private StrategyParameterInt _gridSecondsToLife;

        private BotTabScreener _tabScreener;

        public GridScreenerAdaptiveSoldiers(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            _tabScreener.CandleFinishedEvent += _tab_CandleFinishedEvent1;

            if(startProgram == StartProgram.IsTester)
            {
                _tabScreener.TestStartEvent += _tabScreener_TestStartEvent;
            }

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort" });

            _daysVolatilityAdaptive = CreateParameter("Days volatility adaptive", 3, 1, 20, 1);
            _heightSoldiersVolaPercent = CreateParameter("Height soldiers volatility percent", 50, 10, 70, 1m);
            _minHeightOneSoldiersVolaPercent = CreateParameter("Min height one soldier volatility percent", 10, 5, 20, 1m);

            _smaFilterIsOn = CreateParameter("Sma filter is on", true);
            _smaFilterLen = CreateParameter("Sma filter length", 100, 100, 300, 10);

            _maxGridsCount = CreateParameter("Max grids count", 5, 0, 20, 1, "Grid");
            _linesCount = CreateParameter("Grid lines count", 10, 10, 100, 10, "Grid");
            _linesStep = CreateParameter("Grid lines step", 0.05m, 0.05m, 1, 0.05m, "Grid");
            _profitValue = CreateParameter("Profit % ", 0.05m, 0.05m, 1, 0.05m, "Grid");
            _closePositionsCountToCloseGrid = CreateParameter("Grid close positions max", 50, 10, 300, 10, "Grid");
            _gridSecondsToLife = CreateParameter("Grid life time seconds", 172800, 172800, 2000000, 10000, "Grid");

            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Grid");
            _volume = CreateParameter("Volume on one line", 1, 1.0m, 50, 4, "Grid");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Grid");

            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            Description = OsLocalization.Description.DescriptionLabel39;

            if (startProgram == StartProgram.IsOsTrader)
            {
                LoadTradeSettings();
            }

            this.DeleteEvent += ThreeSoldierAdaptiveScreener_DeleteEvent;
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        public override string GetNameStrategyType()
        {
            return "GridScreenerAdaptiveSoldiers";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void _tabScreener_TestStartEvent()
        {
            for (int i = 0; i < _tabScreener.Tabs.Count; i++)
            {
                BotTabSimple _tab = _tabScreener.Tabs[i];

                if (_tab.GridsMaster == null)
                {
                    continue;
                }

                for (int j = 0; j < _tab.GridsMaster.TradeGrids.Count; j++)
                {
                    TradeGrid grid = _tab.GridsMaster.TradeGrids[j];
                    _tab.GridsMaster.DeleteAtNum(grid.Number);
                    j--;
                }
            }
        }

        #endregion

        #region Volatility adaptation

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
            if (_daysVolatilityAdaptive.ValueInt <= 0
                || _heightSoldiersVolaPercent.ValueDecimal <= 0
                || _minHeightOneSoldiersVolaPercent.ValueDecimal <= 0)
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

                if (days >= _daysVolatilityAdaptive.ValueInt)
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

            decimal allSoldiersHeight = volaPercentSma * (_heightSoldiersVolaPercent.ValueDecimal / 100);
            decimal oneSoldiersHeight = volaPercentSma * (_minHeightOneSoldiersVolaPercent.ValueDecimal / 100);

            settings.HeightSoldiers = allSoldiersHeight;
            settings.MinHeightOneSoldier = oneSoldiersHeight;
            settings.LastUpdateTime = candles[candles.Count - 1].TimeStart;
        }

        #endregion

        #region Logic

        private void _tab_CandleFinishedEvent1(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 150)
            {
                return;
            }

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

            if (tab.GridsMaster.TradeGrids.Count != 0)
            {
                LogicCloseGrid(candles, tab);
            }

            if (tab.GridsMaster.TradeGrids.Count == 0)
            {
                LogicCreateGrid(candles, tab, mySettings);
            }
        }

        private void LogicCreateGrid(List<Candle> candles, BotTabSimple tab, SecuritiesTradeSettings settings)
        {
            if (_tabScreener.SourceWithGridsCount >= _maxGridsCount.ValueInt)
            {
                return;
            }

            if (_tradePeriodsSettings.CanTradeThisTime(tab.TimeServerCurrent) == false)
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
            if (_regime.ValueString != "OnlyShort")
            {
                if (candles[candles.Count - 3].Open < candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open < candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open < candles[candles.Count - 1].Close)
                {
                    if (_smaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue < smaPrev)
                        {
                            return;
                        }
                    }

                    decimal lastPrice = candles[^1].Close;
                    ThrowGrid(lastPrice, Side.Buy, tab);
                }
            }

            // Short
            if (_regime.ValueString != "OnlyLong")
            {
                if (candles[candles.Count - 3].Open > candles[candles.Count - 3].Close
                    && candles[candles.Count - 2].Open > candles[candles.Count - 2].Close
                    && candles[candles.Count - 1].Open > candles[candles.Count - 1].Close)
                {
                    if (_smaFilterIsOn.ValueBool == true)
                    {
                        decimal smaValue = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 1);
                        decimal smaPrev = Sma(candles, _smaFilterLen.ValueInt, candles.Count - 2);

                        if (smaValue > smaPrev)
                        {
                            return;
                        }
                    }

                    decimal lastPrice = candles[^1].Close;
                    ThrowGrid(lastPrice, Side.Sell, tab);
                }
            }
        }

        private void ThrowGrid(decimal lastPrice, Side side, BotTabSimple tab)
        {
            // 1 создаём сетку
            TradeGrid grid = tab.GridsMaster.CreateNewTradeGrid();

            // 2 устанавливаем её тип
            grid.GridType = TradeGridPrimeType.MarketMaking;

            // 3 устанавливаем объёмы
            grid.GridCreator.StartVolume = _volume.ValueDecimal;
            grid.GridCreator.TradeAssetInPortfolio = _tradeAssetInPortfolio.ValueString;
            if (_volumeType.ValueString == "Contracts")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.ContractCurrency;
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                grid.GridCreator.TypeVolume = TradeGridVolumeType.DepositPercent;
            }

            // 4 генерируем линии

            grid.GridCreator.FirstPrice = lastPrice;
            grid.GridCreator.LineCountStart = _linesCount.ValueInt;
            grid.GridCreator.LineStep = _linesStep.ValueDecimal;
            grid.GridCreator.ProfitStep = _profitValue.ValueDecimal;
            grid.GridCreator.TypeStep = TradeGridValueType.Percent;
            grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
            grid.GridCreator.GridSide = side;
            grid.GridCreator.CreateNewGrid(tab, TradeGridPrimeType.MarketMaking);

            // 5 устанавливаем не торговые периоды
            CopyNonTradePeriodsSettingsInGrid(grid);

            // 6 устанавливаем Trailing Up

            grid.TrailingUp.TrailingUpStep = tab.RoundPrice(lastPrice * 0.005m, tab.Security, Side.Sell);
            grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * 0.1m;
            grid.TrailingUp.TrailingUpIsOn = true;
            grid.TrailingUp.TrailingUpCanMoveExitOrder = false;

            // 7 устанавливаем Trailing Down

            grid.TrailingUp.TrailingDownStep = tab.RoundPrice(lastPrice * 0.005m, tab.Security, Side.Buy);
            grid.TrailingUp.TrailingDownLimit = lastPrice - lastPrice * 0.1m;
            grid.TrailingUp.TrailingDownIsOn = true;
            grid.TrailingUp.TrailingDownCanMoveExitOrder = false;

            // 8 устанавливаем закрытие сетки по количеству сделок

            grid.StopBy.StopGridByPositionsCountReaction = TradeGridRegime.CloseForced;
            grid.StopBy.StopGridByPositionsCountValue = _closePositionsCountToCloseGrid.ValueInt;
            grid.StopBy.StopGridByPositionsCountIsOn = true;

            // 9 устанавливаем закрытие сетки по времени жизни

            grid.StopBy.StopGridByLifeTimeIsOn = true; 
            grid.StopBy.StopGridByLifeTimeReaction = TradeGridRegime.CloseForced;
            grid.StopBy.StopGridByLifeTimeSecondsToLife = _gridSecondsToLife.ValueInt;

            // 10 сохраняем
            grid.Save();

            // 11 включаем
            grid.Regime = TradeGridRegime.On;
        }

        private void LogicCloseGrid(List<Candle> candles, BotTabSimple tab)
        {
            TradeGrid grid = tab.GridsMaster.TradeGrids[0];

            // 1 проверяем сетку на то что она уже прекратила работать и её надо удалить

            if (grid.HaveOpenPositionsByGrid == false
                && grid.Regime == TradeGridRegime.Off)
            { // Grid is stop work
                tab.GridsMaster.DeleteAtNum(grid.Number);
                return;
            }
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

        #endregion

        #region Non trade periods

        // Trade periods

        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        private void CopyNonTradePeriodsSettingsInGrid(TradeGrid grid)
        {
            grid.NonTradePeriods.SettingsPeriod1.CopySettings(_tradePeriodsSettings);
        }

        #endregion

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