/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Divergence.

Buy:
1. The lows of the Awesome histogram are gradually increasing.
2. The lows of the chart, on the contrary, gradually decrease.
Sell:
1. The extreme points AO decrease successively.
2. Extremes of the price chart, on the contrary, are rising.

Buy Exit:
1. Stop behind the minimum for a certain number of candles
2. Profit - for the maximum for a certain number of candles
Sell Exit:
1. Stop behind the maximum for a certain number of candles
2. Profit - for a minimum for a certain number of candles
*/

namespace OsEngine.Robots
{
    // We create an attribute so that we don't write anything to the BotFactory
    [Bot("FigureTwoPeaksDivergence")]
    public class FigureTwoPeaksDivergence : BotPanel
    {
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
        private StrategyParameterInt _fastLineLengthAO;
        private StrategyParameterInt _slowLineLengthAO;
        private StrategyParameterInt _lenghtZig;
        private StrategyParameterInt _lenghtZigAO;

        // Indicator
        private Aindicator _zigZag;
        private Aindicator _zigZagAO;

        public FigureTwoPeaksDivergence(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _fastLineLengthAO = CreateParameter("Fast Line Length AO", 12, 10, 300, 10, "Indicator");
            _slowLineLengthAO = CreateParameter("Slow Line Length AO", 24, 10, 300, 10, "Indicator");
            _lenghtZig = CreateParameter("Period Zig", 24, 10, 300, 10, "Indicator");
            _lenghtZigAO = CreateParameter("Period Zig AO", 30, 10, 300, 10, "Indicator");

            // Create indicator ZigZagAO
            _zigZagAO = IndicatorsFactory.CreateIndicatorByName("ZigZagAO", name + "ZigZagAO", false);
            _zigZagAO = (Aindicator)_tab.CreateCandleIndicator(_zigZagAO, "NewArea");
            ((IndicatorParameterInt)_zigZagAO.Parameters[0]).ValueInt = _fastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_zigZagAO.Parameters[1]).ValueInt = _slowLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_zigZagAO.Parameters[3]).ValueInt = _lenghtZigAO.ValueInt;
            _zigZagAO.Save();

            // Create indicator ZigZag
            _zigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _zigZag = (Aindicator)_tab.CreateCandleIndicator(_zigZag, "Prime");
            ((IndicatorParameterInt)_zigZag.Parameters[0]).ValueInt = _lenghtZig.ValueInt;
            _zigZag.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyOnAwesomeOscillatorAndParabolicSAR_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel316;
        }

        // Indicator Update event
        private void StrategyOnAwesomeOscillatorAndParabolicSAR_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_zigZagAO.Parameters[0]).ValueInt = _fastLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_zigZagAO.Parameters[1]).ValueInt = _slowLineLengthAO.ValueInt;
            ((IndicatorParameterInt)_zigZagAO.Parameters[3]).ValueInt = _lenghtZigAO.ValueInt;
            _zigZagAO.Save();
            _zigZagAO.Reload();

            ((IndicatorParameterInt)_zigZag.Parameters[0]).ValueInt = _lenghtZig.ValueInt;
            _zigZag.Save();
            _zigZag.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "FigureTwoPeaksDivergence";
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
            if (candles.Count < _slowLineLengthAO.ValueInt)
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
            List<Position> openPositions = _tab.PositionsOpenAll;

            List<decimal> zzHigh = _zigZag.DataSeries[2].Values;
            List<decimal> zzLow = _zigZag.DataSeries[3].Values;

            List<decimal> zzAOLow = _zigZagAO.DataSeries[4].Values;
            List<decimal> zzAOHigh = _zigZagAO.DataSeries[3].Values;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (DevirgenceBuy(zzLow,zzAOLow, zzAOHigh) == true)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (DevirgenceSell(zzHigh, zzAOHigh, zzAOLow) == true)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        //  logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            List<decimal> zzHigh = _zigZag.DataSeries[2].Values;
            List<decimal> zzLow = _zigZag.DataSeries[3].Values;

            List<decimal> zzAOLow = _zigZagAO.DataSeries[4].Values;
            List<decimal> zzAOHigh = _zigZagAO.DataSeries[3].Values;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (DevirgenceSell(zzHigh, zzAOHigh, zzAOLow) == true)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (DevirgenceBuy(zzLow, zzAOLow,zzAOHigh) == true)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
                }
            }
        }

        // Method for finding divergence
        private bool DevirgenceBuy(List<decimal> zzLow,List<decimal> zzAOLow,List<decimal> zzAOHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzAOLowOne = 0;
            decimal zzAOLowTwo = 0;
            
            int indexOne = 0;
            int indexTwo = 0;
            int indexHigh = 0;

            for (int i = zzAOHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAOHigh[i] != 0)
                {
                    cnt++;
                    indexHigh = i;
                }

                if (cnt == 1)
                {
                    break;
                }
            }

            for (int i = zzLow.Count - 1;i >= 0;i--)
            {
                int cnt = 0;
                
                if (zzLow[i] != 0 &&  zzLowOne == 0)
                {
                    zzLowOne = zzLow[i];
                    cnt++;
                    indexOne = i;
                }

                if (zzLow[i] != 0 && indexOne != i && zzLowTwo == 0)
                {
                    zzLowTwo = zzLow[i];
                    cnt++;
                }

                if(cnt == 2)
                {
                    break;
                }
            }

            for (int i = zzAOLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAOLow[i] != 0 && zzAOLowOne == 0)
                {
                    zzAOLowOne = zzAOLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzAOLow[i] != 0 && indexTwo != i && zzAOLowTwo == 0)
                {
                    zzAOLowTwo = zzAOLow[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            decimal cntLow = 0;

            if (zzLowOne < zzLowTwo && zzLowOne != 0 && indexTwo < indexHigh)
            {
                cntLow++;
            }

            if (zzAOLowOne > zzAOLowTwo && zzAOLowOne !=0)
            {
                cntLow++;
            }

            if(cntLow == 2) 
            {
                return true;
            }

            return false;
        }

        // Method for finding divergence
        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzAOHigh, List<decimal> zzAOLow)
        {
            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzAOHighOne = 0;
            decimal zzAOHighTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexLow = 0;

            for (int i = zzAOLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAOLow[i] != 0)
                {
                    cnt++;
                    indexLow = i;
                }

                if (cnt == 1)
                {
                    break;
                }
            }

            for (int i = zzHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzHigh[i] != 0 && zzHighOne == 0)
                {
                    zzHighOne = zzHigh[i];
                    cnt++;
                    indexOne = i;
                }

                if (zzHigh[i] != 0 && indexOne != i && zzHighTwo == 0)
                {
                    zzHighTwo = zzHigh[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            for (int i = zzAOHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzAOHigh[i] != 0 && zzAOHighOne == 0)
                {
                    zzAOHighOne = zzAOHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzAOHigh[i] != 0 && indexTwo != i && zzAOHighTwo == 0)
                {
                    zzAOHighTwo = zzAOHigh[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            decimal cntHigh = 0;

            if (zzHighOne > zzHighTwo && zzHighTwo != 0 && indexTwo < indexLow)
            {
                cntHigh++;
            }

            if (zzAOHighOne < zzAOHighTwo && zzAOHighOne != 0)
            {
                cntHigh++;
            }

            if (cntHigh == 2)
            {
                return true;
            }

            return false;
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