/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
Trading robot for osengine.

The trend robot on countertrend two Envelopes and RSI.

Buy:
1. The price was in the lower zone between between the lower lines of the local Envelopes and the global or below the global. 
Then it returned back and became higher than the lower line of the local Envelopes;
2. Rsi is below a certain value, oversold zone (Oversold Line).

Sell: 
1. The price was in the upper zone between between the milestones of the local Envelopes and the global or above the global. 
Then it came back and became below the upper line of the local Envelopes;
2. The Rsi is above a certain value, the overbought zone (Overbought Line).

Exit: 
On the opposite signal.
 */

namespace OsEngine.Robots
{
    [Bot("CountertrendTwoEnvelopsAndRSI")] // We create an attribute so that we don't write anything to the BotFactory
    public class CountertrendTwoEnvelopsAndRSI : BotPanel
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

        // Indicator settings
        private StrategyParameterInt _envelopsLengthLoc;
        private StrategyParameterDecimal _envelopsDeviationLoc;
        private StrategyParameterInt _envelopsLengthGlob;
        private StrategyParameterDecimal _envelopsDeviationGlob;
        private StrategyParameterInt _periodRsi;
        private StrategyParameterInt _oversoldLine;
        private StrategyParameterInt _overboughtLine;

        // Indicator
        private Aindicator _envelopsLoc;
        private Aindicator _envelopsGlob;
        private Aindicator _RSI;

        // The last value of the indicator
        private decimal _lastUpLineLoc;
        private decimal _lastDownLineLoc;
        private decimal _lastUpLineGlob;
        private decimal _lastDownLineGlob;
        private decimal _lastRSI;

        // The prev value of the indicator
        private decimal _prevUpLineLoc;
        private decimal _prevDownLineLoc;
        private decimal _prevUpLineGlob;
        private decimal _prevDownLineGlob;

        public CountertrendTwoEnvelopsAndRSI(string name, StartProgram startProgram) : base(name, startProgram)
        {
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
            _envelopsLengthLoc = CreateParameter("Envelop Length Loc", 21, 7, 48, 7, "Indicator");
            _envelopsDeviationLoc = CreateParameter("Envelop Deviation Loc", 1.0m, 1, 5, 0.1m, "Indicator");
            _envelopsLengthGlob = CreateParameter("Envelop Length Glob", 21, 7, 48, 7, "Indicator");
            _envelopsDeviationGlob = CreateParameter("Envelop Deviation Glob", 1.0m, 1, 5, 0.1m, "Indicator");
            _periodRsi = CreateParameter("RSI Period", 14, 7, 48, 7, "Indicator");
            _oversoldLine = CreateParameter("Oversold Line", 20, 10,100, 10, "Indicator");
            _overboughtLine = CreateParameter("Overbought Line", 20, 10, 200, 10, "Indicator");
           
            // Create indicator RSI
            _RSI = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
            _RSI = (Aindicator)_tab.CreateCandleIndicator(_RSI, "NewArea");
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = _periodRsi.ValueInt;
            _RSI.Save();

            // Create indicator EnvelopsLoc
            _envelopsLoc = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "_EnvelopsLoc", false);
            _envelopsLoc = (Aindicator)_tab.CreateCandleIndicator(_envelopsLoc, "Prime");
            ((IndicatorParameterInt)_envelopsLoc.Parameters[0]).ValueInt = _envelopsLengthLoc.ValueInt;
            ((IndicatorParameterDecimal)_envelopsLoc.Parameters[1]).ValueDecimal = _envelopsDeviationLoc.ValueDecimal;
            _envelopsLoc.Save();

            //  Create indicator EnvelopsGlob
            _envelopsGlob = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "_EnvelopsGlob", false);
            _envelopsGlob = (Aindicator)_tab.CreateCandleIndicator(_envelopsGlob, "Prime");
            ((IndicatorParameterInt)_envelopsGlob.Parameters[0]).ValueInt = _envelopsLengthGlob.ValueInt;
            ((IndicatorParameterDecimal)_envelopsGlob.Parameters[1]).ValueDecimal = _envelopsDeviationGlob.ValueDecimal;
            _envelopsGlob.DataSeries[0].Color = Color.Aquamarine;
            _envelopsGlob.DataSeries[2].Color = Color.Aquamarine;
            _envelopsGlob.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CountertrendTwoEnvelopsAndRSI_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel196;
        }

        private void CountertrendTwoEnvelopsAndRSI_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_envelopsLoc.Parameters[0]).ValueInt = _envelopsLengthLoc.ValueInt;
            ((IndicatorParameterDecimal)_envelopsLoc.Parameters[1]).ValueDecimal = _envelopsDeviationLoc.ValueDecimal;
            _envelopsLoc.Save();
            _envelopsLoc.Reload();
            ((IndicatorParameterInt)_envelopsGlob.Parameters[0]).ValueInt = _envelopsLengthGlob.ValueInt;
            ((IndicatorParameterDecimal)_envelopsGlob.Parameters[1]).ValueDecimal = _envelopsDeviationGlob.ValueDecimal;
            _envelopsGlob.Save();
            _envelopsGlob.Reload();
            ((IndicatorParameterInt)_RSI.Parameters[0]).ValueInt = _periodRsi.ValueInt;
            _RSI.Save();
            _RSI.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CountertrendTwoEnvelopsAndRSI";
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
            if (candles.Count < _envelopsLengthLoc.ValueInt || candles.Count < _envelopsDeviationLoc.ValueDecimal ||
                candles.Count < _envelopsLengthGlob.ValueInt || candles.Count < _envelopsDeviationGlob.ValueDecimal)
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
            _lastUpLineLoc = _envelopsLoc.DataSeries[0].Last;
            _lastDownLineLoc = _envelopsLoc.DataSeries[2].Last;
            _lastUpLineGlob = _envelopsGlob.DataSeries[0].Last;
            _lastDownLineGlob = _envelopsGlob.DataSeries[2].Last;
            _lastRSI = _RSI.DataSeries[0].Last;

            // The prev value of the indicator
            _prevUpLineLoc = _envelopsLoc.DataSeries[0].Values[_envelopsLoc.DataSeries[0].Values.Count - 2];
            _prevDownLineLoc = _envelopsLoc.DataSeries[2].Values[_envelopsLoc.DataSeries[2].Values.Count - 2];
            _prevUpLineGlob = _envelopsGlob.DataSeries[0].Values[_envelopsGlob.DataSeries[0].Values.Count - 2];
            _prevDownLineGlob = _envelopsGlob.DataSeries[2].Values[_envelopsGlob.DataSeries[2].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;
                decimal prevPrice = candles[candles.Count - 2].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (prevPrice < _prevDownLineLoc && prevPrice > _prevDownLineGlob && 
                        lastPrice > _lastDownLineLoc && _lastRSI < _oversoldLine.ValueInt
                        || prevPrice < _prevDownLineGlob && lastPrice > _lastDownLineLoc
                        && _lastRSI < _oversoldLine.ValueInt)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (prevPrice > _prevUpLineLoc && prevPrice < _prevUpLineGlob && lastPrice < _lastUpLineLoc
                        && _lastRSI > _overboughtLine.ValueInt || prevPrice > _prevUpLineGlob && lastPrice < _lastUpLineLoc
                        && _lastRSI > _overboughtLine.ValueInt)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
          
            _lastUpLineLoc = _envelopsLoc.DataSeries[0].Last;
            _lastDownLineLoc = _envelopsLoc.DataSeries[1].Last;
            _lastUpLineGlob = _envelopsGlob.DataSeries[0].Last;
            _lastDownLineGlob = _envelopsGlob.DataSeries[1].Last;
            _lastRSI = _RSI.DataSeries[0].Last;

            // The prev value of the indicator
            _prevUpLineLoc = _envelopsLoc.DataSeries[0].Values[_envelopsLoc.DataSeries[0].Values.Count - 2];
            _prevDownLineLoc = _envelopsLoc.DataSeries[1].Values[_envelopsLoc.DataSeries[1].Values.Count - 2];
            _prevUpLineGlob = _envelopsGlob.DataSeries[0].Values[_envelopsGlob.DataSeries[0].Values.Count - 2];
            _prevDownLineGlob = _envelopsGlob.DataSeries[1].Values[_envelopsGlob.DataSeries[1].Values.Count - 2];

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                decimal lastPrice = candles[candles.Count - 1].Close;
                decimal prevPrice = candles[candles.Count - 2].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    if (prevPrice > _prevUpLineLoc && prevPrice < _prevUpLineGlob && lastPrice < _lastUpLineLoc
                        && _lastRSI > _overboughtLine.ValueInt || prevPrice > _prevUpLineGlob && lastPrice < _lastUpLineLoc
                        && _lastRSI > _overboughtLine.ValueInt)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is sale
                {
                    if (prevPrice < _prevDownLineLoc && prevPrice > _prevDownLineGlob && lastPrice > _lastDownLineLoc && _lastRSI < _oversoldLine.ValueInt
                        || prevPrice < _prevDownLineGlob && lastPrice > _lastDownLineLoc && _lastRSI < _oversoldLine.ValueInt)
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