/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/*Discription
Trading robot for osengine.

Trend strategy on Bears Power Divergence.

Buy:
1. Bears Power columns must be below 0.
2. The minimums on the chart are decreasing, but on the indicator they are growing.

Sell:
1. The price is lower than the Parabolic value. For the next candle, the price crosses the indicator from top to bottom.
2. Bulls Power columns must be below 0.
3. Bears Power columns must be below 0.

Exit:
The Bears Power indicator has become higher than 0.
*/

namespace OsEngine.Robots
{
    // We create an attribute so that we don't write anything to the BotFactory
    [Bot("BearsPowerDivergence")]
    public class BearsPowerDivergence : BotPanel
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
        private StrategyParameterInt _lengthBearsPower;
        private StrategyParameterInt _lengthZZ;
        private StrategyParameterInt _lengthZZBearsPower;

        // Indicator
        private Aindicator _zz;
        private Aindicator _zzBP;

        // The last value of the indicators           
        private decimal _lastZZBP;

        public BearsPowerDivergence(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _startTradeTime = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _endTradeTime = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _lengthZZ = CreateParameter("Zig Zag", 20, 10, 300, 10, "Indicator");
            _lengthZZBearsPower = CreateParameter("_length ZZ Bears Power", 20, 10, 300, 10, "Indicator");
            _lengthBearsPower = CreateParameter("Zig Zag BP", 20, 10, 300, 10, "Indicator");

            // Create indicator Zig Zag
            _zz = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "Zig Zag", false);
            _zz = (Aindicator)_tab.CreateCandleIndicator(_zz, "Prime");
            ((IndicatorParameterInt)_zz.Parameters[0]).ValueInt = _lengthZZ.ValueInt;
            _zz.Save();

            // Create indicator Zig Zag BP
            _zzBP = IndicatorsFactory.CreateIndicatorByName("ZigZagBP", name + "Zig Zag BP", false);
            _zzBP = (Aindicator)_tab.CreateCandleIndicator(_zzBP, "NewArea");
            ((IndicatorParameterInt)_zzBP.Parameters[0]).ValueInt = _lengthBearsPower.ValueInt;
            ((IndicatorParameterInt)_zzBP.Parameters[1]).ValueInt = _lengthZZBearsPower.ValueInt;
            _zzBP.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BearsPowerDivergence_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel131;
        }

        // Indicator Update event
        private void BearsPowerDivergence_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_zz.Parameters[0]).ValueInt = _lengthZZ.ValueInt;
            _zz.Save();
            _zz.Reload();
            ((IndicatorParameterInt)_zzBP.Parameters[0]).ValueInt = _lengthBearsPower.ValueInt;
            ((IndicatorParameterInt)_zzBP.Parameters[1]).ValueInt = _lengthZZBearsPower.ValueInt;
            _zzBP.Save();
            _zzBP.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BearsPowerDivergence";
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
            if (candles.Count < _lengthZZ.ValueInt || candles.Count < _lengthBearsPower.ValueInt)
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

            List<decimal> zzLow = _zz.DataSeries[3].Values;
            List<decimal> zzBPLow = _zzBP.DataSeries[4].Values;
            List<decimal> zzBPHigh = _zzBP.DataSeries[3].Values;
           
            // The last value of the indicators
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // He last value of the indicator           
            if (openPositions == null || openPositions.Count == 0)
            {
                _lastZZBP = _zzBP.DataSeries[0].Last;

                if (DevirgenceBuy(zzLow, zzBPLow, _lastZZBP,zzBPHigh) == true)
                {
                    _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // He last value of the indicator
            _lastZZBP = _zzBP.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // if the direction of the position is long
                {
                    if (_lastZZBP > 0)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }                
                }
            }
        }

        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzBPLow, decimal lastBP,List <decimal> zzBPHigh)
        {
            if(lastBP > 0)
            {
                return false;
            }

            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzBPLowOne = 0;
            decimal zzBPLowTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexHigh = 0;

            for (int i = zzBPHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzBPHigh[i] != 0)
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

            for (int i = zzBPLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;
               
                if (zzBPLow[i] != 0 && zzBPLowOne == 0)
                {
                    zzBPLowOne = zzBPLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzBPLow[i] != 0 && indexTwo != i && zzBPLowTwo == 0)
                {
                    zzBPLowTwo = zzBPLow[i];
                    cnt++;
                }

                if (cnt == 2)
                {
                    break;
                }
            }

            decimal cntLow = 0;

            if (zzLowOne < zzLowTwo && zzLowOne != 0)
            {
                cntLow++;
            }

            if (zzBPLowOne > zzBPLowTwo && zzBPLowOne != 0 && indexTwo<indexHigh)
            {
                cntLow++;
            }

            if (cntLow == 2)
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