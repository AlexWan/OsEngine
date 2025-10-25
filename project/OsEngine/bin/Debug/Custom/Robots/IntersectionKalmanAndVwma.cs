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

The trend robot on Intersection Kalman And Vwma.

Buy: Vwma above Kalman.

Sell: Vwma below Kalman.

Exit: stop and profit in % of the entry price.
 */

namespace OsEngine.Robots.AO
{
    [Bot("IntersectionKalmanAndVwma")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionKalmanAndVwma : BotPanel
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
        private StrategyParameterInt _periodVwma;

        // Indicator
        private Aindicator _kalman;
        private Aindicator _vwma;

        // Exit Settings
        private StrategyParameterDecimal _stopValue;
        private StrategyParameterDecimal _profitValue;

        // The last value of the indicator
        private decimal _lastKalman;
        private decimal _lastVWMA;

        // The prev value of the indicator
        private decimal _prevKalman;
        private decimal _prevVWMA;

        public IntersectionKalmanAndVwma(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodVwma = CreateParameter("Period VWMA", 100, 10, 300, 1, "Indicator");

            // Create indicator ChaikinOsc
            _kalman = IndicatorsFactory.CreateIndicatorByName("KalmanFilter", name + "KalmanFilter", false);
            _kalman = (Aindicator)_tab.CreateCandleIndicator(_kalman, "Prime");
            ((IndicatorParameterDecimal)_kalman.Parameters[0]).ValueDecimal = _sharpness.ValueDecimal;
            ((IndicatorParameterDecimal)_kalman.Parameters[1]).ValueDecimal = _coefK.ValueDecimal;
            _kalman.Save();

            // Create indicator VWMAFast
            _vwma = IndicatorsFactory.CreateIndicatorByName("VWMA", name + "VWMA", false);
            _vwma = (Aindicator)_tab.CreateCandleIndicator(_vwma, "Prime");
            ((IndicatorParameterInt)_vwma.Parameters[0]).ValueInt = _periodVwma.ValueInt;
            _vwma.Save();

            // Exit Settings
            _stopValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");
            _profitValue = CreateParameter("Profit Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionKalmanAndVwma_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel320;
        }

        private void IntersectionKalmanAndVwma_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_kalman.Parameters[0]).ValueDecimal = _sharpness.ValueDecimal;
            ((IndicatorParameterDecimal)_kalman.Parameters[1]).ValueDecimal = _coefK.ValueDecimal;
            _kalman.Save();
            _kalman.Reload();
            ((IndicatorParameterInt)_vwma.Parameters[0]).ValueInt = _periodVwma.ValueInt;
            _vwma.Save();
            _vwma.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionKalmanAndVwma";
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
                candles.Count < _periodVwma.ValueInt)
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
            _lastVWMA = _vwma.DataSeries[0].Last;

            // The prev value of the indicator
            _prevKalman = _kalman.DataSeries[0].Values[_kalman.DataSeries[0].Values.Count - 2];
            _prevVWMA = _vwma.DataSeries[0].Values[_vwma.DataSeries[0].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_prevVWMA < _prevKalman && _lastVWMA > _lastKalman)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_prevVWMA > _prevKalman && _lastVWMA < _lastKalman)
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
                    decimal profitActivation = pos.EntryPrice + pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice - pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is short
                {
                    decimal profitActivation = pos.EntryPrice - pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice + pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
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