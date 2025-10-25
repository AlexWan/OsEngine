/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Strategy Force Index Parabolic And EfficiencyRatio.

Buy:
1. The price is higher than the value of the Parabolic indicator. For the next candle, the price crosses the indicator from the bottom up.
2. The value of the force index indicator is higher than MaxValueFi.
3. The value of the Er indicator is higher than MaxValueEr.
4. The signal is valid only if the current price is higher than the Parabolic value from ParabolicCount candles ago.

Sell:
1. The price is lower than the value of the Parabolic indicator. For the next candle, the price crosses the indicator from top to bottom.
2. The value of the force index indicator is lower than MinValueFi.
3. The value of the Er indicator is higher than MaxValueEr.
4. The signal is valid only if the current price is lower (for short) than the Parabolic value from ParabolicCount candles ago.

Exit: on the opposite Parabolic signal.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyFIParabolicAndER")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyFIParabolicAndER : BotPanel
    {
        // Reference to the main trading tab
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _startTradeTime;
        private StrategyParameterTimeOfDay _endTradeTime;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _lengthFI;
        private StrategyParameterDecimal _step;
        private StrategyParameterDecimal _maxStep;
        private StrategyParameterInt _lengthER;
        private StrategyParameterDecimal _maxValueFi;
        private StrategyParameterDecimal _minValueFi;
        private StrategyParameterDecimal _maxValueER;
        private StrategyParameterInt _parabolicCount;

        // Indicator
        private Aindicator _FI;
        private Aindicator _parabolic;
        private Aindicator _ER;

        // The last value of the indicator
        private decimal _lastFI;
        private decimal _lastParabolic;
        private decimal _lastER;

        public StrategyFIParabolicAndER(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _lengthFI = CreateParameter("FI Length", 13, 7, 48, 7, "Indicator");
            _step = CreateParameter("Step", 0.1m, 0.01m, 0.1m, 0.01m, "Indicator");
            _maxStep = CreateParameter("Max Step", 0.1m, 0.01m, 5.0m, 0.01m, "Indicator");
            _lengthER = CreateParameter("ER Length", 14, 7, 48, 7, "Indicator");
            _maxValueFi = CreateParameter("Max Value Fi", 0.5m, 0.01m, 1.0m, 0.01m, "Indicator");//для лонга
            _minValueFi = CreateParameter("Min Value Fi", -0.5m, -5.0m, 0.0m, 0.01m, "Indicator");//для шорта 
            _maxValueER = CreateParameter("Max Value ER", 0.7m, 0.01m, 1.0m, 0.01m, "Indicator");
            _parabolicCount = CreateParameter("Parabolic Count", 5, 5, 50, 5, "Indicator");

            // Create indicator FI
            _FI = IndicatorsFactory.CreateIndicatorByName("ForceIndex", name + "ForceIndex", false);
            _FI = (Aindicator)_tab.CreateCandleIndicator(_FI, "NewArea");
            ((IndicatorParameterInt)_FI.Parameters[0]).ValueInt = _lengthFI.ValueInt;
            _FI.Save();

            // Create indicator Parabolic
            _parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Par", false);
            _parabolic = (Aindicator)_tab.CreateCandleIndicator(_parabolic, "Prime");
            ((IndicatorParameterDecimal)_parabolic.Parameters[0]).ValueDecimal = _step.ValueDecimal;
            ((IndicatorParameterDecimal)_parabolic.Parameters[1]).ValueDecimal = _maxStep.ValueDecimal;
            _parabolic.Save();

            // Create indicator EfficiencyRatio
            _ER = IndicatorsFactory.CreateIndicatorByName("EfficiencyRatio", name + "EfficiencyRatio", false);
            _ER = (Aindicator)_tab.CreateCandleIndicator(_ER, "NewArea0");
            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = _lengthER.ValueInt;
            _ER.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyFIParabolicAndER_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel251;
        }

        private void StrategyFIParabolicAndER_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_FI.Parameters[0]).ValueInt = _lengthFI.ValueInt;
            _FI.Save();
            _FI.Reload();

            ((IndicatorParameterDecimal)_parabolic.Parameters[0]).ValueDecimal = _step.ValueDecimal;
            ((IndicatorParameterDecimal)_parabolic.Parameters[1]).ValueDecimal = _maxStep.ValueDecimal;
            _parabolic.Save();
            _parabolic.Reload();

            ((IndicatorParameterInt)_ER.Parameters[0]).ValueInt = _lengthER.ValueInt;
            _ER.Save();
            _ER.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyFIParabolicAndER";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count <= _lengthER.ValueInt + 1 ||
                candles.Count <= _lengthFI.ValueInt ||
                candles.Count <= _parabolicCount.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_startTradeTime.Value > _tab.TimeServerCurrent ||
                _endTradeTime.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            // The last value of the indicator
            _lastFI = _FI.DataSeries[0].Last;
            _lastER = _ER.DataSeries[0].Last;
            _lastParabolic = _parabolic.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > _lastParabolic && _lastFI > _maxValueFi.ValueDecimal && _lastER > _maxValueER.ValueDecimal)
                    {
                        if (_parabolic.DataSeries[0].Values[_parabolic.DataSeries[0].Values.Count - _parabolicCount.ValueInt] < lastPrice)
                        {
                            return;
                        }

                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < _lastParabolic && _lastFI < _minValueFi.ValueDecimal && _lastER > _maxValueER.ValueDecimal)
                    {
                        if (_parabolic.DataSeries[0].Values[_parabolic.DataSeries[0].Values.Count - _parabolicCount.ValueInt] > lastPrice)
                        {
                            return;
                        }

                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            // The last value of the indicator
            _lastParabolic = _parabolic.DataSeries[0].Last;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    if (lastPrice < _lastParabolic)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastPrice > _lastParabolic)
                    {
                        _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                    }
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                       && tab.Security.PriceStep != tab.Security.PriceStepCost
                       && tab.PriceBestAsk != 0
                       && tab.Security.PriceStep != 0
                       && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
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
    }
}