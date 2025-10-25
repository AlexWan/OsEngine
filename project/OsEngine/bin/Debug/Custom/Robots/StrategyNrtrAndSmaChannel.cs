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
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

Trend robot on the NRTR and SmaChannel indicators.

Buy:
When the candle closed above the upper SmaChannel line and above the NRTR line.

Sell:
When the candle closed below the lower SmaChannel line and below the NRTR line.

Exit from buy:
Set a trailing stop along the NRTR line and at the lower border of the SmaChannel indicator. 
The calculation method that is further from the current price is selected.

Exit from sell:
Set a trailing stop along the NRTR line and at the upper border of the SmaChannel indicator. 
The calculation method that is further from the current price is selected.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyNrtrAndSmaChannel")] // Instead of manually adding through BotFactory, we use an attribute to simplify the process.
    internal class StrategyNrtrAndSmaChannel : BotPanel
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

        // Indicators settings
        private StrategyParameterInt _lengthNrtr;
        private StrategyParameterDecimal _deviationNrtr;
        private StrategyParameterInt _smaLength;
        private StrategyParameterDecimal _smaDeviation;

        // Indicators
        private Aindicator _nrtr;
        private Aindicator _smaChannel;

        public StrategyNrtrAndSmaChannel(string name, StartProgram startProgram) : base(name, startProgram)
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

            // Indicators settings
            _lengthNrtr = CreateParameter("Length NRTR", 24, 5, 100, 5, "Indicator");
            _deviationNrtr = CreateParameter("Deviation NRTR", 1, 1m, 10, 1, "Indicator");
            _smaLength = CreateParameter("Length Sma", 10, 10, 300, 10, "Indicator");
            _smaDeviation = CreateParameter("Deviation Sma", 1.0m, 1, 10, 1, "Indicator");

            // Create indicator NRTR
            _nrtr = IndicatorsFactory.CreateIndicatorByName("NRTR", name + "Nrtr", false);
            _nrtr = (Aindicator)_tab.CreateCandleIndicator(_nrtr, "Prime");
            ((IndicatorParameterInt)_nrtr.Parameters[0]).ValueInt = _lengthNrtr.ValueInt;
            ((IndicatorParameterDecimal)_nrtr.Parameters[1]).ValueDecimal = _deviationNrtr.ValueDecimal;
            _nrtr.Save();

            // Create indicator SmaChannel
            _smaChannel = IndicatorsFactory.CreateIndicatorByName("SmaChannel", name + "SmaChannel", false);
            _smaChannel = (Aindicator)_tab.CreateCandleIndicator(_smaChannel, "Prime");
            ((IndicatorParameterInt)_smaChannel.Parameters[0]).ValueInt = _smaLength.ValueInt;
            ((IndicatorParameterDecimal)_smaChannel.Parameters[1]).ValueDecimal = _smaDeviation.ValueDecimal;
            _smaChannel.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyNrtrAndAdx_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel258;
        }

        private void StrategyNrtrAndAdx_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_nrtr.Parameters[0]).ValueInt = _lengthNrtr.ValueInt;
            ((IndicatorParameterDecimal)_nrtr.Parameters[1]).ValueDecimal = _deviationNrtr.ValueDecimal;
            _nrtr.Save();
            _nrtr.Reload();

            ((IndicatorParameterInt)_smaChannel.Parameters[0]).ValueInt = _smaLength.ValueInt;
            ((IndicatorParameterDecimal)_smaChannel.Parameters[1]).ValueDecimal = _smaDeviation.ValueDecimal;
            _smaChannel.Save();
            _smaChannel.Reload();
        }

        public override string GetNameStrategyType()
        {
            return "StrategyNrtrAndSmaChannel";
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
            if (candles.Count < _lengthNrtr.ValueInt ||
                candles.Count < _smaLength.ValueInt)
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
            decimal lastNRTR = _nrtr.DataSeries[2].Last;
            decimal lastUpSma = _smaChannel.DataSeries[0].Last;
            decimal lastDownSma = _smaChannel.DataSeries[2].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                // Slippage
                decimal slippage = _slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (lastPrice > lastNRTR && lastPrice > lastUpSma)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (lastPrice < lastNRTR && lastPrice < lastDownSma)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            // The last value of the indicator
            decimal lastNRTR = _nrtr.DataSeries[2].Last;
            decimal lastUpSma = _smaChannel.DataSeries[0].Last;
            decimal lastDownSma = _smaChannel.DataSeries[2].Last;

            decimal stop_level = 0;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is Buy
                {
                    stop_level = lastNRTR < lastDownSma ? lastNRTR : lastDownSma;
                }
                else // If the direction of the position is Sell
                {
                    stop_level = lastNRTR > lastUpSma ? lastNRTR : lastUpSma;
                }
                _tab.CloseAtTrailingStop(pos, stop_level, stop_level);
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