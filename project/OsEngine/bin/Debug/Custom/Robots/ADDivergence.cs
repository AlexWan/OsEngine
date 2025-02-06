using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
trading robot for osengine

The trend robot on AD indicator divergence and price

Buy: 
at the price, the minimum for a certain period of time is below the previous
minimum, and on the indicator, the minimum is higher than the previous one.

Sell: 
on the price the maximum for a certain period of time is higher than the 
previous maximum, and on the indicator the maximum is lower than the previous one.

Exit: after n number of candles.
 
 */

namespace OsEngine.Robots.Mybots
{
    // We create an attribute so that we don't write anything to the BotFactory
    [Bot("ADDivergence")]
    public class ADDivergence : BotPanel
    {
        BotTabSimple _tab;

        // Basic Settings
        StrategyParameterString _regime;
        StrategyParameterString _volumeType;
        StrategyParameterDecimal _volume;
        StrategyParameterString _tradeAssetInPortfolio;
        StrategyParameterDecimal _slippage;
        StrategyParameterTimeOfDay _startTradeTime;
        StrategyParameterTimeOfDay _endTradeTime;

        // Setting indicator
        StrategyParameterInt _fastLineLengthAO;
        StrategyParameterInt _slowLineLengthAO;

        // Indicator
        Aindicator _ZZ;
        Aindicator _ZigZagAD;

        // Divergence
        StrategyParameterInt _lenghtZig;
        StrategyParameterInt _lenghtZigAO;

        // Exit 
        StrategyParameterInt _exitCandlesCount;

        public ADDivergence(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Base");
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4, "Base");
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime", "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // Setting indicator
            _lenghtZig = CreateParameter("Period Zig", 30, 10, 300, 10, "Indicator");
            _lenghtZigAO = CreateParameter("Period Zig AO", 30, 10, 300, 10, "Indicator");


            // Create indicator ZigZagAO
            _ZigZagAD = IndicatorsFactory.CreateIndicatorByName("ZigZagAD", name + "ZigZagAD", false);
            _ZigZagAD = (Aindicator)_tab.CreateCandleIndicator(_ZigZagAD, "NewArea");
            ((IndicatorParameterInt)_ZigZagAD.Parameters[0]).ValueInt = _lenghtZigAO.ValueInt;
            _ZigZagAD.Save();

            // Create indicator ZigZag
            _ZZ = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _ZZ = (Aindicator)_tab.CreateCandleIndicator(_ZZ, "Prime");
            ((IndicatorParameterInt)_ZZ.Parameters[0]).ValueInt = _lenghtZig.ValueInt;
            _ZZ.Save();

            // Exit
            _exitCandlesCount = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ADDivergence_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = "The trend robot on AD indicator divergence and price. " +
                "Buy: at the price, the minimum for a certain period of time is below the previous minimum," +
                " and on the indicator, the minimum is higher than the previous one. " +
                "Sell: on the price the maximum for a certain period of time is higher than the previous maximum," +
                " and on the indicator the maximum is lower than the previous one." +
                "Exit: after n number of candles.";
        }

        // Indicator Update event
        private void ADDivergence_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ZigZagAD.Parameters[0]).ValueInt = _lenghtZigAO.ValueInt;
            _ZigZagAD.Save();
            _ZigZagAD.Reload();

            ((IndicatorParameterInt)_ZZ.Parameters[0]).ValueInt = _lenghtZig.ValueInt;
            _ZZ.Save();
            _ZZ.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ADDivergence";
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

            List<decimal> zzHigh = _ZZ.DataSeries[2].Values;
            List<decimal> zzLow = _ZZ.DataSeries[3].Values;

            List<decimal> zzADLow = _ZigZagAD.DataSeries[4].Values;
            List<decimal> zzADHigh = _ZigZagAD.DataSeries[3].Values;


            if (openPositions == null || openPositions.Count == 0)
            {

                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;
                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (DevirgenceBuy(zzLow, zzADLow, zzADHigh) == true)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (DevirgenceSell(zzHigh, zzADHigh, zzADLow) == true)
                    {
                        var time = candles.Last().TimeStart;
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage, time.ToString());
                    }

                }
                return;
            }
        }
        //  logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                List<Candle> candles1 = new List<Candle>();

                if (!NeedClosePosition(pos, candles))
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtLimit(pos, lastPrice + _slippage, pos.OpenVolume);
                }
            }
        }

        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzADLow, List<decimal> zzADHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzADLowOne = 0;
            decimal zzADLowTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexHigh = 0;

            for (int i = zzADHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzADHigh[i] != 0)
                {
                    cnt++;
                    indexHigh = i;
                }

                if (cnt == 1)
                {
                    break;
                }
            }

            for (int i = zzLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzLow[i] != 0 && zzLowOne == 0)
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

                if (cnt == 2)
                {
                    break;
                }
            }

            for (int i = zzADLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzADLow[i] != 0 && zzADLowOne == 0)
                {
                    zzADLowOne = zzADLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzADLow[i] != 0 && indexTwo != i && zzADLowTwo == 0)
                {
                    zzADLowTwo = zzADLow[i];
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
            if (zzADLowOne > zzADLowTwo && zzADLowOne != 0)
            {
                cntLow++;
            }

            if (cntLow == 2)
            {
                return true;
            }
            return false;
        }

        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzADHigh, List<decimal> zzADLow)
        {
            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzADHighOne = 0;
            decimal zzADHighTwo = 0;

            int indexOne = 0;

            int indexTwo = 0;

            int indexLow = 0;

            for (int i = zzADLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzADLow[i] != 0)
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

            for (int i = zzADHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzADHigh[i] != 0 && zzADHighOne == 0)
                {
                    zzADHighOne = zzADHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzADHigh[i] != 0 && indexTwo != i && zzADHighTwo == 0)
                {
                    zzADHighTwo = zzADHigh[i];
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
            if (zzADHighOne < zzADHighTwo && zzADHighOne != 0)
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

        private bool NeedClosePosition(Position position, List<Candle> candles)
        {
            if (position == null || position.OpenVolume == 0)
            {
                return false;
            }

            DateTime openTime = DateTime.Parse(position.SignalTypeOpen);

            int counter = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                counter++;
                DateTime candelTime = candles[i].TimeStart;
                if (candelTime == openTime)
                {
                    if (counter >= _exitCandlesCount.ValueInt + 1)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

