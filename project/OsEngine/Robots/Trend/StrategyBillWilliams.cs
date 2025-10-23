/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Windows;

/* Description
Trading robot for osengine.

Trend Strategy Bill Williams. 

Buy:
1. The current price must be above all three lines of the Alligator indicator.
2. Additionally, the price must be above the upward fractal level (_lastFractalUp).

When there is already an open position:
1. The current Ao indicator value (_lastAo) must be greater than the two previous values (_secondAo and _thirdAo) — this signals a buy.

Sell:
1. The current price must be below all three lines of the Alligator indicator.
2. Additionally, the price must be below the downward fractal level (_lastFractalDown).

When there is already an open position:
1. The Ao indicator value (_lastAo) must be greater than the two previous values (_secondAo and _thirdAo) — this signals a buy.

Exit long: If the current price has fallen below the middle line of the Alligator.  
Exit short: If the price has risen above the middle line of the Alligator.
 */

namespace OsEngine.Robots.Trend
{
    [Bot("StrategyBillWilliams")] //We create an attribute so that we don't write anything in the Boot factory
    public class StrategyBillWilliams : BotPanel
    {
        private BotTabSimple _tab;

        // Basic setting
        private StrategyParameterString _regime;
        private StrategyParameterInt _slippage;
        private StrategyParameterInt _maximumPositions;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volumeFirst;
        private StrategyParameterDecimal _volumeSecond;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _alligatorFastLineLength;
        private StrategyParameterInt _alligatorMiddleLineLength;
        private StrategyParameterInt _alligatorSlowLineLength;

        // Indicators
        private Aindicator _alligator;
        private Aindicator _fractal;
        private Aindicator _ao;

        //The last value of the indicators
        private decimal _lastPrice;
        private decimal _lastUpAlligator;
        private decimal _lastMiddleAlligator;
        private decimal _lastDownAlligator;
        private decimal _lastFractalUp;
        private decimal _lastFractalDown;
        private decimal _lastIndexDown;
        private decimal _lastIndexUp;
        private decimal _lastAo;
        private decimal _secondAo;
        private decimal _thirdAo;

        public StrategyBillWilliams(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);
            _maximumPositions = CreateParameter("MaxPoses", 1, 1, 10, 1);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volumeFirst = CreateParameter("Volume First", 2, 1.0m, 50, 4);
            _volumeSecond = CreateParameter("Volume Second", 2, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _alligatorFastLineLength = CreateParameter("AlligatorFastLineLength", 3, 3, 30, 1);
            _alligatorMiddleLineLength = CreateParameter("AlligatorMiddleLineLength", 10, 10, 70, 5);
            _alligatorSlowLineLength = CreateParameter("AlligatorSlowLineLength", 40, 40, 150, 10);

            // Create indicator Alligator
            _alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _alligator = (Aindicator)_tab.CreateCandleIndicator(_alligator, "Prime");
            ((IndicatorParameterInt)_alligator.Parameters[0]).ValueInt = _alligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_alligator.Parameters[1]).ValueInt = _alligatorFastLineLength.ValueInt;
            ((IndicatorParameterInt)_alligator.Parameters[2]).ValueInt = _alligatorMiddleLineLength.ValueInt;
            _alligator.Save();

            // Create indicator AO
            _ao = IndicatorsFactory.CreateIndicatorByName("AO", name + "AO", false);
            _ao = (Aindicator)_tab.CreateCandleIndicator(_ao, "NewArea");
            _ao.Save();

            // Create indicator Fractal
            _fractal = IndicatorsFactory.CreateIndicatorByName("Fractal", name + "Fractal", false);
            _fractal = (Aindicator)_tab.CreateCandleIndicator(_fractal, "Prime");
            _fractal.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += Bot_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += StrategyBillWilliams_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel119;
        }

        // Parameters changed by user
        void StrategyBillWilliams_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_alligator.Parameters[0]).ValueInt = _alligatorSlowLineLength.ValueInt;
            ((IndicatorParameterInt)_alligator.Parameters[1]).ValueInt = _alligatorFastLineLength.ValueInt;
            ((IndicatorParameterInt)_alligator.Parameters[2]).ValueInt = _alligatorMiddleLineLength.ValueInt;
            _alligator.Save();
            _alligator.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StrategyBillWilliams";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
            MessageBox.Show(OsLocalization.Trader.Label55);
        }

        // Logic
        private void Bot_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader
                && candles[candles.Count-1].TimeStart.Hour < 10)
            {
                return;
            }

            if (_alligator.DataSeries[0].Values == null ||
                _fractal.DataSeries[0].Values == null ||
                _alligatorSlowLineLength.ValueInt > candles.Count ||
                _alligatorMiddleLineLength.ValueInt > candles.Count ||
                _alligatorFastLineLength.ValueInt > candles.Count)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastUpAlligator = _alligator.DataSeries[2].Last;
            _lastMiddleAlligator = _alligator.DataSeries[1].Last;
            _lastDownAlligator = _alligator.DataSeries[0].Last;

            for (int i = _fractal.DataSeries[1].Values.Count - 1; i > -1; i--)
            {
                if (_fractal.DataSeries[1].Values[i] != 0)
                {
                    _lastFractalUp = _fractal.DataSeries[1].Values[i];
                    _lastIndexUp = i;
                    break;
                }
            }

            for (int i = _fractal.DataSeries[0].Values.Count - 1; i > -1; i--)
            {
                if (_fractal.DataSeries[0].Values[i] != 0)
                {
                    _lastFractalDown = _fractal.DataSeries[0].Values[i];
                    _lastIndexDown = i;
                    break;
                }
            }

            _lastAo = _ao.DataSeries[0].Last;

            if (_ao.DataSeries[0].Values.Count > 3)
            {
                _secondAo = _ao.DataSeries[0].Values[_ao.DataSeries[0].Values.Count - 2];
                _thirdAo = _ao.DataSeries[0].Values[_ao.DataSeries[0].Values.Count - 3];
            }

            if (_lastUpAlligator == 0 ||
                _lastMiddleAlligator == 0 ||
                _lastDownAlligator == 0)
            {
                return;
            }

            //we distribute logic depending on the current position

            List<Position> openPosition = _tab.PositionsOpenAll;

            if (openPosition != null && openPosition.Count != 0
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                for (int i = 0; i < openPosition.Count; i++)
                {
                    LogicClosePosition(openPosition[i], candles);
                }
            }

            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            if (openPosition == null || openPosition.Count == 0
                && candles[candles.Count - 1].TimeStart.Hour >= 11
                && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                LogicOpenPosition();
            }
            else if (openPosition.Count != 0 && openPosition.Count < _maximumPositions.ValueInt
                     && candles[candles.Count - 1].TimeStart.Hour >= 11
                     && candles[candles.Count - 1].TimeStart.Hour <= 18)
            {
                LogicOpenPositionSecondary(openPosition[0].Direction);
            }
        }

        // Open position logic
        private void LogicOpenPosition()
        {
            if (_lastPrice > _lastUpAlligator && _lastPrice > _lastMiddleAlligator && _lastPrice > _lastDownAlligator
                && _lastPrice > _lastFractalUp
                && _regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(GetVolume(_tab,_volumeFirst.ValueDecimal), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
            }
            if (_lastPrice < _lastUpAlligator && _lastPrice < _lastMiddleAlligator && _lastPrice < _lastDownAlligator
                && _lastPrice < _lastFractalDown
                && _regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(GetVolume(_tab, _volumeFirst.ValueDecimal), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
            }
        }

        // Open position logic. After first position
        private void LogicOpenPositionSecondary(Side side)
        {
            if (side == Side.Buy && _regime.ValueString != "OnlyShort")
            {
                if (_secondAo < _lastAo &&
                    _secondAo < _thirdAo)
                {
                    _tab.BuyAtLimit(GetVolume(_tab, _volumeSecond.ValueDecimal), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }

            if (side == Side.Sell && _regime.ValueString != "OnlyLong")
            {
                if (_secondAo > _lastAo &&
                    _secondAo > _thirdAo)
                {
                    _tab.SellAtLimit(GetVolume(_tab, _volumeSecond.ValueDecimal), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
                }
            }
        }

        // Close position logic
        private void LogicClosePosition(Position position, List<Candle> candles)
        {
            if (position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                if (_lastPrice < _lastMiddleAlligator)
                {
                    _tab.CloseAtLimit(position, _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastPrice > _lastMiddleAlligator)
                {
                    _tab.CloseAtLimit(position, _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab, decimal _volume)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume / (contractPrice * tab.Security.Lot);
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

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume / 100);

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