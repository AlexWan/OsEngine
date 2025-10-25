/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System.Drawing;
using OsEngine.Language;

/*Discription
Trading robot for osengine.

Trend strategy based on Adaptive Look Back and ROC indicators.

Buy:
1. The candle closed above the high for the period Candles Count High + entry coefficient * Adaptive Look Back. (we set BuyAtStop).
2. ROC is above 0.

Sell: 
1. The candle closed below the lot during the period of the minimum number of candles - the entry coefficient * Adaptive look back (we install SellAtStop).
2. ROC is below 0.

Exit:
by the reverse signal of the RoC indicator.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyAdaptiveLookBackAndROC")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    public class StrategyAdaptiveLookBackAndROC : BotPanel
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

        // Indicator Settings
        private StrategyParameterInt _candlesCountHigh;
        private StrategyParameterInt _candlesCountLow;
        private StrategyParameterInt _periodALB;
        private StrategyParameterInt _lengthROC;
        private StrategyParameterDecimal _multALB;

        // Indicator
        private Aindicator ROC;
        private Aindicator ALB;

        // The last value of the indicators      
        private decimal _lastALB;
        private decimal _lastROC;

        public StrategyAdaptiveLookBackAndROC(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Indicator Settings
            _lengthROC = CreateParameter("Rate of Change", 14, 1, 10, 1, "Indicator");
            _periodALB = CreateParameter("Adaptive Look Back", 5, 1, 10, 1, "Indicator");
            _multALB = CreateParameter("CoefEntryALB", 0.0002m, 0.0001m, 0.01m, 0.0001m, "Indicator");
            _candlesCountHigh = CreateParameter("CandlesCountHigh", 10, 50, 100, 20, "Indicator");
            _candlesCountLow = CreateParameter("CandlesCountLow", 5, 20, 100, 10, "Indicator");

            // Create indicator Adaptive Look Back
            ALB = IndicatorsFactory.CreateIndicatorByName("AdaptiveLookBack", name + "ALB", false);
            ALB = (Aindicator)_tab.CreateCandleIndicator(ALB, "NewArea0");
            ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = _periodALB.ValueInt;
            ALB.Save();

            // Create indicator ROC
            ROC = IndicatorsFactory.CreateIndicatorByName("ROC", name + "Rate of Change", false);
            ROC = (Aindicator)_tab.CreateCandleIndicator(ROC, "NewArea1");
            ((IndicatorParameterInt)ROC.Parameters[0]).ValueInt = _lengthROC.ValueInt;
            ROC.DataSeries[0].Color = Color.Red;
            ROC.Save();

            ParametrsChangeByUser += _strategyAdaptiveLookBackAndROC_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel237;
        }

        // Indicator Update event
        private void _strategyAdaptiveLookBackAndROC_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)ALB.Parameters[0]).ValueInt = _periodALB.ValueInt;
            ALB.Save();
            ALB.Reload();

            ((IndicatorParameterInt)ROC.Parameters[0]).ValueInt = _lengthROC.ValueInt;
            ROC.Save();
            ROC.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyAdaptiveLookBackAndROC";
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
            if (candles.Count < _periodALB.ValueInt + 100 || candles.Count < _candlesCountHigh.ValueInt ||
                candles.Count < _lengthROC.ValueInt || candles.Count < _candlesCountLow.ValueInt)
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
            _lastROC = ROC.DataSeries[0].Last;
            _lastALB = ALB.DataSeries[0].Last;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;
            decimal lastPrice = candles[candles.Count - 1].Close;

            _tab.BuyAtStopCancel();
            _tab.SellAtStopCancel();

            decimal openBuy = GetOpenBuy(candles, candles.Count - 1);
            decimal openSell = GetOpenSell(candles, candles.Count - 1);

            // Long
            if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                if (lastPrice > openBuy  && _lastROC > 0)
                {
                    _tab.BuyAtStop(GetVolume(_tab),
                        lastPrice + _multALB.ValueDecimal * _lastALB/100 + _slippage,
                        lastPrice + _multALB.ValueDecimal * _lastALB/100, StopActivateType.HigherOrEqual,1);
                }
            }

            // Short
            if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {
                if (lastPrice < openSell  && _lastROC < 0)
                {
                    _tab.SellAtStop(GetVolume(_tab),
                        lastPrice - _multALB.ValueDecimal * _lastALB/100 - _slippage,
                        lastPrice - _multALB.ValueDecimal * _lastALB/100, StopActivateType.LowerOrEqual,1);
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            _lastROC = ROC.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is buy
                {
                    if (_lastROC < 0)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (_lastROC > 0)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
                }
            }
        }
        
        public decimal GetOpenSell(List<Candle> candles, int index)
        {
            if (candles == null || index < _candlesCountLow.ValueInt)
            {
                return 0;
            }

            decimal price = decimal.MaxValue;

            for (int i = index - 1; i > 0 && i > index - _candlesCountLow.ValueInt; i--)
            {
                // Looking at the maximum low
                if (candles[i].Low < price)
                {
                    price = candles[i].Low;
                }
            }

            return price;
        }

        public decimal GetOpenBuy(List<Candle> candles, int index)
        {
            if (candles == null || index < _candlesCountHigh.ValueInt)
            {
                return 0;
            }

            decimal price = decimal.MinValue;

            for (int i = index - 1; i > 0 && i > index - _candlesCountHigh.ValueInt; i--)
            {
                // Looking at the maximum high
                if (candles[i].High > price)
                {
                    price = candles[i].High;
                }
            }

            return price;
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