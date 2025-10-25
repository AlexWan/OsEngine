/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
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

The robot on strategy Devergence OsMa.

Buy: The lows on the chart are decreasing, but on the indicator they are growing.

Sell: The highs on the chart are rising, and on the indicator they are decreasing.

Exit: After a certain number of candles.
*/

namespace OsEngine.Robots
{
    [Bot("DivergenceOsMa")] // We create an attribute so that we don't write anything to the BotFactory
    internal class DivergenceOsMa : BotPanel
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
        private StrategyParameterInt _lenghZigZag;
        private StrategyParameterInt _lenghtFastLine;
        private StrategyParameterInt _lenghtSlowLine;
        private StrategyParameterInt _lenghtSignalLine;

        // Indicator
        private Aindicator _zigZag;
        private Aindicator _zigZagOsma;
        
        // Exit Setting
        private StrategyParameterInt _exitCandles;

        public DivergenceOsMa(string name, StartProgram startProgram) : base(name, startProgram)
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
            _lenghZigZag = CreateParameter("lenght ZigZag", 10, 10, 300, 10, "Indicator");
            _lenghtFastLine = CreateParameter("lenght Fast Line", 12, 10, 300, 1, "Indicator");
            _lenghtSlowLine = CreateParameter("lenght Slow Line", 26, 10, 300, 1, "Indicator");
            _lenghtSignalLine = CreateParameter("lenght Signal Line", 9, 9, 300, 1, "Indicator");

            // Create indicator ZigZag
            _zigZag = IndicatorsFactory.CreateIndicatorByName("ZigZag", name + "ZigZag", false);
            _zigZag = (Aindicator)_tab.CreateCandleIndicator(_zigZag, "Prime");
            ((IndicatorParameterInt)_zigZag.Parameters[0]).ValueInt = _lenghZigZag.ValueInt;
            _zigZag.Save();

            // Create indicator ZigZag Osma
            _zigZagOsma = IndicatorsFactory.CreateIndicatorByName("ZigZagOsMa", name + "ZigZagOsma", false);
            _zigZagOsma = (Aindicator)_tab.CreateCandleIndicator(_zigZagOsma, "NewZZOsMa");
            ((IndicatorParameterInt)_zigZagOsma.Parameters[0]).ValueInt = _lenghtFastLine.ValueInt;
            ((IndicatorParameterInt)_zigZagOsma.Parameters[1]).ValueInt = _lenghtSlowLine.ValueInt;
            ((IndicatorParameterInt)_zigZagOsma.Parameters[2]).ValueInt = _lenghtSignalLine.ValueInt;
            ((IndicatorParameterInt)_zigZagOsma.Parameters[3]).ValueInt = _lenghZigZag.ValueInt;
            _zigZagOsma.Save();

            // Exit Setting
            _exitCandles = CreateParameter("Exit Candles", 10, 5, 1000, 10, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += DivergenceOsMa_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel314;
        }
       
        private void DivergenceOsMa_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_zigZag.Parameters[0]).ValueInt = _lenghZigZag.ValueInt;
            _zigZag.Save();
            _zigZag.Reload();
            ((IndicatorParameterInt)_zigZagOsma.Parameters[0]).ValueInt = _lenghtFastLine.ValueInt;
            ((IndicatorParameterInt)_zigZagOsma.Parameters[1]).ValueInt = _lenghtSlowLine.ValueInt;
            ((IndicatorParameterInt)_zigZagOsma.Parameters[2]).ValueInt = _lenghtSignalLine.ValueInt;
            ((IndicatorParameterInt)_zigZagOsma.Parameters[3]).ValueInt = _lenghZigZag.ValueInt;
            _zigZagOsma.Save();
            _zigZagOsma.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "DivergenceOsMa";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _lenghZigZag.ValueInt)
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

            List<decimal> zzOsMaLow = _zigZagOsma.DataSeries[4].Values;
            List<decimal> zzOsMaHigh = _zigZagOsma.DataSeries[3].Values;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (DevirgenceBuy(zzLow, zzOsMaLow, zzOsMaHigh) == true)
                    {
                        DateTime time = candles.Last().TimeStart;

                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage, time.ToString());
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (DevirgenceSell(zzHigh, zzOsMaHigh, zzOsMaLow) == true)
                    {
                        DateTime time = candles.Last().TimeStart;

                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage, time.ToString());
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position position = openPositions[i];

                if (position.State != PositionStateType.Open)
                {
                    continue;
                }

                if (!NeedClosePosition(position, candles))
                {
                    continue;
                }

                if (position.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    _tab.CloseAtLimit(position, lastPrice - _slippage, position.OpenVolume);
                }
                else // If the direction of the position is sale
                {
                    _tab.CloseAtLimit(position, lastPrice + _slippage, position.OpenVolume);
                }
            }
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
                    if (counter >= _exitCandles.ValueInt + 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Method for finding divergence
        private bool DevirgenceBuy(List<decimal> zzLow, List<decimal> zzOsMaLow, List<decimal> zzOsMaHigh)
        {
            decimal zzLowOne = 0;
            decimal zzLowTwo = 0;
            decimal zzOsMaLowOne = 0;
            decimal zzOsMaLowTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexHigh = 0;

            for (int i = zzOsMaHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzOsMaHigh[i] != 0)
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

            for (int i = zzOsMaLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzOsMaLow[i] != 0 && zzOsMaLowOne == 0)
                {
                    zzOsMaLowOne = zzOsMaLow[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzOsMaLow[i] != 0 && indexTwo != i && zzOsMaLowTwo == 0)
                {
                    zzOsMaLowTwo = zzOsMaLow[i];
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

            if (zzOsMaLowOne > zzOsMaLowTwo && zzOsMaLowOne != 0)
            {
                cntLow++;
            }

            if (cntLow == 2)
            {
                return true;
            }

            return false;
        }

        // Method for finding divergence
        private bool DevirgenceSell(List<decimal> zzHigh, List<decimal> zzOsMaHigh, List<decimal> zzOsMaLow)
        {
            decimal zzHighOne = 0;
            decimal zzHighTwo = 0;
            decimal zzAsiHighOne = 0;
            decimal zzAsiHighTwo = 0;

            int indexOne = 0;
            int indexTwo = 0;
            int indexLow = 0;

            for (int i = zzOsMaLow.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzOsMaLow[i] != 0)
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

            for (int i = zzOsMaHigh.Count - 1; i >= 0; i--)
            {
                int cnt = 0;

                if (zzOsMaHigh[i] != 0 && zzAsiHighOne == 0)
                {
                    zzAsiHighOne = zzOsMaHigh[i];
                    cnt++;
                    indexTwo = i;
                }

                if (zzOsMaHigh[i] != 0 && indexTwo != i && zzAsiHighTwo == 0)
                {
                    zzAsiHighTwo = zzOsMaHigh[i];
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

            if (zzAsiHighOne < zzAsiHighTwo && zzAsiHighOne != 0)
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