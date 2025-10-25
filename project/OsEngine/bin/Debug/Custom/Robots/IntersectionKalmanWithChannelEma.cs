/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System.Linq;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Intersection Kalman With ChannelEma.

Buy:
1. The price is above the Kalman and above the upper line of the Ema channel.
2. Kalman is above the upper line of the Ema channel.

Sell:
1. The price is below the Kalman and below the lower line of the Ema channel.
2. The Kalman is below the lower line of the Ema channel.

Exit from buy: the kalman is below the upper line.

Exit from sell: Kalman is above the bottom line.
 */

namespace OsEngine.Robots.AO
{
    [Bot("IntersectionKalmanWithChannelEma")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionKalmanWithChannelEma : BotPanel
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
        private StrategyParameterDecimal _sharpness;
        private StrategyParameterDecimal _coefK;
        private StrategyParameterInt _lengthEmaChannel;

        // Indicator
        private Aindicator _kalman;
        private Aindicator _emaHigh;
        private Aindicator _emaLow;

        // The last value of the indicator
        private decimal _lastKalman;
        private decimal _lastEmaHigh;
        private decimal _lastEmaLow;

        public IntersectionKalmanWithChannelEma(string name, StartProgram startProgram) : base(name, startProgram)
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
            _sharpness = CreateParameter("Sharpness", 1.0m, 1, 50, 1, "Indicator");
            _coefK = CreateParameter("CoefK", 1.0m, 1, 50, 1, "Indicator");
            _lengthEmaChannel = CreateParameter("Period VWMA", 100, 10, 300, 1, "Indicator");

            // Create indicator ChaikinOsc
            _kalman = IndicatorsFactory.CreateIndicatorByName("KalmanFilter", name + "KalmanFilter", false);
            _kalman = (Aindicator)_tab.CreateCandleIndicator(_kalman, "Prime");
            ((IndicatorParameterDecimal)_kalman.Parameters[0]).ValueDecimal = _sharpness.ValueDecimal;
            ((IndicatorParameterDecimal)_kalman.Parameters[1]).ValueDecimal = _coefK.ValueDecimal;
            _kalman.Save();

            // Create indicator VwmaHigh
            _emaHigh = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema High", false);
            _emaHigh = (Aindicator)_tab.CreateCandleIndicator(_emaHigh, "Prime");
            ((IndicatorParameterInt)_emaHigh.Parameters[0]).ValueInt = _lengthEmaChannel.ValueInt;
            ((IndicatorParameterString)_emaHigh.Parameters[1]).ValueString = "High";
            _emaHigh.Save();

            // Create indicator VwmaLow
            _emaLow = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema Low", false);
            _emaLow = (Aindicator)_tab.CreateCandleIndicator(_emaLow, "Prime");
            ((IndicatorParameterInt)_emaLow.Parameters[0]).ValueInt = _lengthEmaChannel.ValueInt;
            ((IndicatorParameterString)_emaLow.Parameters[1]).ValueString = "Low";
            _emaLow.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionKalmanWithChannelEma_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel321;
        }

        private void IntersectionKalmanWithChannelEma_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_kalman.Parameters[0]).ValueDecimal = _sharpness.ValueDecimal;
            ((IndicatorParameterDecimal)_kalman.Parameters[1]).ValueDecimal = _coefK.ValueDecimal;
            _kalman.Save();
            _kalman.Reload();
            ((IndicatorParameterInt)_emaHigh.Parameters[0]).ValueInt = _lengthEmaChannel.ValueInt;
            ((IndicatorParameterString)_emaHigh.Parameters[1]).ValueString = "High";
            _emaHigh.Save();
            _emaHigh.Reload();
            ((IndicatorParameterInt)_emaLow.Parameters[0]).ValueInt = _lengthEmaChannel.ValueInt;
            ((IndicatorParameterString)_emaLow.Parameters[1]).ValueString = "Low";
            _emaLow.Save();
            _emaLow.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionKalmanWithChannelEma";
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
            if (candles.Count < _coefK.ValueDecimal ||
                candles.Count < _sharpness.ValueDecimal ||
                candles.Count < _lengthEmaChannel.ValueInt)
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
            _lastKalman = _kalman.DataSeries[0].Last;
            _lastEmaHigh = _emaHigh.DataSeries[0].Last;
            _lastEmaLow = _emaLow.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastKalman < lastPrice && _lastEmaHigh < lastPrice && _lastKalman > _lastEmaHigh)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastKalman > lastPrice && _lastEmaLow > lastPrice && _lastKalman < _lastEmaLow)
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

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            // The last value of the indicator
            _lastKalman = _kalman.DataSeries[0].Last;
            _lastEmaHigh = _emaHigh.DataSeries[0].Last;
            _lastEmaLow = _emaLow.DataSeries[0].Last;

            decimal lastPrice = candles[candles.Count - 1].Close;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }

                if (openPositions[i].Direction == Side.Buy) // If the direction of the position is long
                {
                    if (_lastKalman < _lastEmaHigh)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastKalman > _lastEmaLow)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
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