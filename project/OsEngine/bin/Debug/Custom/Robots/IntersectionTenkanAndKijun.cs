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
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Ichimocu.

Buy: The Tenkan line crosses the Kijun line from bottom to top.

Sell: The Tenkan line crosses the Kijun line from top to bottom.

If the intersection occurs in the cloud (between the lines Senkou A and Senkou B), we do not enter the position.

Exit: on the opposite signal.
 */

namespace OsEngine.Robots
{
    [Bot("IntersectionTenkanAndKijun")] // We create an attribute so that we don't write anything to the BotFactory
    public class IntersectionTenkanAndKijun : BotPanel
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
        private StrategyParameterInt _tenkanLength;
        private StrategyParameterInt _kijunLength;
        private StrategyParameterInt _senkouLength;
        private StrategyParameterInt _chinkouLength;
        private StrategyParameterInt _offset;

        // Indicator
        private Aindicator _ichomoku;

        // The last value of the indicator
        private decimal _lastTenkan;
        private decimal _lastKijun;
        private decimal _lastSenkouA;
        private decimal _lastSenkouB;

        // The prev value of the indicator
        private decimal _prevTenkan;
        private decimal _prevKijun;

        public IntersectionTenkanAndKijun(string name, StartProgram startProgram) : base(name, startProgram)
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
            _tenkanLength = CreateParameter("Tenkan Length", 9, 1, 50, 3, "Indicator");
            _kijunLength = CreateParameter("Kijun Length", 26, 1, 50, 4, "Indicator");
            _senkouLength = CreateParameter("Senkou Length", 52, 1, 100, 8, "Indicator");
            _chinkouLength = CreateParameter("Chinkou Length", 26, 1, 50, 4, "Indicator");
            _offset = CreateParameter("Offset", 26, 1, 50, 4, "Indicator");

            // Create indicator _Ichomoku
            _ichomoku = IndicatorsFactory.CreateIndicatorByName("Ichimoku", name + "Ichimoku", false);
            _ichomoku = (Aindicator)_tab.CreateCandleIndicator(_ichomoku, "Prime");
            ((IndicatorParameterInt)_ichomoku.Parameters[0]).ValueInt = _tenkanLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[1]).ValueInt = _kijunLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[2]).ValueInt = _senkouLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[3]).ValueInt = _chinkouLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[4]).ValueInt = _offset.ValueInt;
            _ichomoku.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += BreakChaikin_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel217;
        }

        private void BreakChaikin_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ichomoku.Parameters[0]).ValueInt = _tenkanLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[1]).ValueInt = _kijunLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[2]).ValueInt = _senkouLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[3]).ValueInt = _chinkouLength.ValueInt;
            ((IndicatorParameterInt)_ichomoku.Parameters[4]).ValueInt = _offset.ValueInt;
            _ichomoku.Save();
            _ichomoku.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionTenkanAndKijun";
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
            if (candles.Count < _tenkanLength.ValueInt ||
                candles.Count < _kijunLength.ValueInt ||
                candles.Count < _senkouLength.ValueInt ||
                candles.Count < _chinkouLength.ValueInt ||
                candles.Count < _offset.ValueInt)
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
            _lastTenkan = _ichomoku.DataSeries[0].Last;
            _lastKijun = _ichomoku.DataSeries[1].Last;
            _lastSenkouA = _ichomoku.DataSeries[3].Last;
            _lastSenkouB = _ichomoku.DataSeries[4].Last;

            // The prev value of the indicator
            _prevTenkan = _ichomoku.DataSeries[0].Values[_ichomoku.DataSeries[0].Values.Count - 2];
            _prevKijun = _ichomoku.DataSeries[1].Values[_ichomoku.DataSeries[1].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastTenkan > _lastKijun)
                    {
                        if(_lastKijun > _lastSenkouA && _lastKijun < _lastSenkouB ||
                            _lastKijun < _lastSenkouA && _lastKijun > _lastSenkouB)
                        {
                            return;
                        }

                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastTenkan < _lastKijun)
                    {
                        if (_lastKijun > _lastSenkouA && _lastKijun < _lastSenkouB ||
                            _lastKijun < _lastSenkouA && _lastKijun > _lastSenkouB)
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
            _lastTenkan = _ichomoku.DataSeries[0].Last;
            _lastKijun = _ichomoku.DataSeries[1].Last;
            _lastSenkouA = _ichomoku.DataSeries[3].Last;
            _lastSenkouB = _ichomoku.DataSeries[4].Last;

            // The prev value of the indicator
            _prevTenkan = _ichomoku.DataSeries[0].Values[_ichomoku.DataSeries[0].Values.Count - 2];
            _prevKijun = _ichomoku.DataSeries[1].Values[_ichomoku.DataSeries[1].Values.Count - 2];

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
                    if (_lastTenkan < _lastKijun)
                    {
                        _tab.CloseAtLimit(pos, lastPrice - _slippage, pos.OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (_lastTenkan > _lastKijun)
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