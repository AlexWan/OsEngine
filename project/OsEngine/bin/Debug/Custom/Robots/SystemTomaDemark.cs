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

The trend robot on Strategy System Toma Demark.

Buy: Fast Ema above slow Ema and Momentum line above Momentum (e.g. above 100);

Sell: Fast Ema below slow Ema and Momentum line below Momentum Cell (e.g. below 100);

Exit from buy: trailing stop in % of the loy of the candle on which you entered.
Exit from sell: trailing stop in % of the high of the candle on which you entered.
 */

namespace OsEngine.Robots
{
    [Bot("SystemTomaDemark")] // We create an attribute so that we don't write anything to the BotFactory
    public class SystemTomaDemark : BotPanel
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
        private StrategyParameterInt _emaPeriodFast;
        private StrategyParameterInt _emaPeriodSlow;
        private StrategyParameterInt _momentumPeriod;
        private StrategyParameterInt _momentumBuy;
        private StrategyParameterInt _momentumSell;

        // Indicator
        private Aindicator _emaFast;
        private Aindicator _emaSlow;
        private Aindicator _momentum;

        // The last value of the indicator
        private decimal _lastEmaFast;
        private decimal _lastEmaSlow;
        private decimal _lastMomentum;

        // Exit setting
        private StrategyParameterDecimal _trailingValue;

        public SystemTomaDemark(string name, StartProgram startProgram) : base(name, startProgram)
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
            _emaPeriodFast = CreateParameter("Ema Length Fast", 50, 7, 480, 7, "Indicator");
            _emaPeriodSlow = CreateParameter("Ema Length Slow", 100, 7, 480, 7, "Indicator");
            _momentumPeriod = CreateParameter("Momentum Period", 16, 10, 300, 7, "Indicator");
            _momentumBuy = CreateParameter("Momentum Buy", 32, 10, 300, 10, "Indicator");
            _momentumSell = CreateParameter("Momentum Sell", 8, 10, 300, 10, "Indicator");

            // Create indicator Ema Fast
            _emaFast = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMAFast", false);
            _emaFast = (Aindicator)_tab.CreateCandleIndicator(_emaFast, "Prime");
            ((IndicatorParameterInt)_emaFast.Parameters[0]).ValueInt = _emaPeriodFast.ValueInt;
            _emaFast.Save();

            // Create indicator Ema Slow
            _emaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMASlow", false);
            _emaSlow = (Aindicator)_tab.CreateCandleIndicator(_emaSlow, "Prime");
            ((IndicatorParameterInt)_emaSlow.Parameters[0]).ValueInt = _emaPeriodSlow.ValueInt;
            _emaSlow.Save();

            // Create indicator MACD
            _momentum = IndicatorsFactory.CreateIndicatorByName("Momentum", name + "Momentum", false);
            _momentum = (Aindicator)_tab.CreateCandleIndicator(_momentum, "NewArea");
            ((IndicatorParameterInt)_momentum.Parameters[0]).ValueInt = _momentumPeriod.ValueInt;
            _momentum.Save();

            // Exit setting
            _trailingValue = CreateParameter("Stop Value", 1.0m, 5, 200, 5, "Exit");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += SystemTomaDemark_ParametrsChangeByUser; ;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel294;
        }

        private void SystemTomaDemark_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_emaFast.Parameters[0]).ValueInt = _emaPeriodFast.ValueInt;
            _emaFast.Save();
            _emaFast.Reload();
            ((IndicatorParameterInt)_emaSlow.Parameters[0]).ValueInt = _emaPeriodSlow.ValueInt;
            _emaSlow.Save();
            _emaSlow.Reload();
            ((IndicatorParameterInt)_momentum.Parameters[0]).ValueInt = _momentumPeriod.ValueInt;
            _momentum.Save();
            _momentum.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "SystemTomaDemark";
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
            if (candles.Count < _emaPeriodFast.ValueInt ||
                candles.Count < _emaPeriodSlow.ValueInt ||
                candles.Count < _momentumPeriod.ValueInt ||
                candles.Count < _momentumBuy.ValueInt ||
                candles.Count < _momentumSell.ValueInt)
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
            _lastEmaFast = _emaFast.DataSeries[0].Last;
            _lastEmaSlow = _emaSlow.DataSeries[0].Last;
            _lastMomentum = _momentum.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal lastPrice = candles[candles.Count - 1].Close;

                // Slippage
                decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    if (_lastEmaFast > _lastEmaSlow && _lastMomentum > _momentumBuy.ValueInt)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEmaFast < _lastEmaSlow && _lastMomentum < _momentumSell.ValueInt)
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

            decimal stopPrice;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal lov = candles[candles.Count - 1].Low;
                    stopPrice = lov - lov * _trailingValue.ValueDecimal / 100;
                }
                else // If the direction of the position is short
                {
                    decimal high = candles[candles.Count - 1].High;
                    stopPrice = high + high * _trailingValue.ValueDecimal / 100;
                }
                _tab.CloseAtTrailingStop(pos, stopPrice, stopPrice);
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