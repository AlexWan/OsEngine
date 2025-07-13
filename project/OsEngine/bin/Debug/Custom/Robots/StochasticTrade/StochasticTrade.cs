﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Charts.CandleChart.Elements;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/* Description
trading robot for osengine

The countertrend robot on Stochastic.

The strategy is reverse. 

Long entry: Crossing the lower horizontal line (default 20) by two values ​​of the Stochastic indicator.
Short Entry: Crossing the upper horizontal line (default 80) by two values ​​of the Stochastic indicator.

Exit Long: Crossing the upper horizontal line (80 by default) by two values ​​of the Stochastic indicator.
Exit Short: Crossing the lower horizontal line (default 20) by two values ​​of the Stochastic indicator.
*/

namespace OsEngine.Robots
{
    public class StochasticTrade : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _slippage;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Line Settings
        private StrategyParameterDecimal _upLineValue;
        private StrategyParameterDecimal _downLineValue;

        // Indicator Settings 
        private StrategyParameterInt _stochPeriod1;
        private StrategyParameterInt _stochPeriod2;
        private StrategyParameterInt _stochPeriod3;

        // Indicator
        private Aindicator _stoch;

        // Line on chart
        private LineHorisontal _upline;
        private LineHorisontal _downline;

        // The last value of the indicator and price
        private decimal _stocLastUp;
        private decimal _stocLastDown;
        private decimal _lastPrice;

        public StochasticTrade(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Line settungs
            _upLineValue = CreateParameter("Up Line Value", 80, 60.0m, 90, 0.5m);
            _downLineValue = CreateParameter("Down Line Value", 20, 10.0m, 40, 0.5m);

            // Indicator settings
            _stochPeriod1 = CreateParameter("Stoch Period 1", 5, 3, 40, 1);
            _stochPeriod2 = CreateParameter("Stoch Period 2", 3, 2, 40, 1);
            _stochPeriod3 = CreateParameter("Stoch Period 3", 3, 2, 40, 1);

            // Create indicator Stochastic
            _stoch = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _stoch = (Aindicator)_tab.CreateCandleIndicator(_stoch, "StochasticArea");
            _stoch.ParametersDigit[0].Value = _stochPeriod1.ValueInt;
            _stoch.ParametersDigit[1].Value = _stochPeriod2.ValueInt;
            _stoch.ParametersDigit[2].Value = _stochPeriod3.ValueInt;
            _stoch.Save();

            // Create Upline on StochasticArea
            _upline = new LineHorisontal("upline", "StochasticArea", false)
            {
                Color = Color.Green,
                Value = 0,
            };
            _tab.SetChartElement(_upline);
            _upline.Value = _upLineValue.ValueDecimal;
            _upline.TimeEnd = DateTime.Now;

            // Create Downline on StochasticArea
            _downline = new LineHorisontal("downline", "StochasticArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(_downline);
            _downline.Value = _downLineValue.ValueDecimal;
            _downline.TimeEnd = DateTime.Now;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += RviTrade_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel126;
        }

        void RviTrade_ParametrsChangeByUser()
        {
            _stoch.ParametersDigit[0].Value = _stochPeriod1.ValueInt;
            _stoch.ParametersDigit[1].Value = _stochPeriod2.ValueInt;
            _stoch.ParametersDigit[2].Value = _stochPeriod3.ValueInt;

            _upline.Value = _upLineValue.ValueDecimal;
            _upline.Refresh();

            _downline.Value = _downLineValue.ValueDecimal;
            _downline.Refresh();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "StochasticTrade";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Candle Finished Event
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_stoch.DataSeries[0].Values == null ||
                _stoch.DataSeries[1].Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _stocLastUp = _stoch.DataSeries[0].Values[_stoch.DataSeries[0].Values.Count - 1];
            _stocLastDown = _stoch.DataSeries[1].Values[_stoch.DataSeries[1].Values.Count - 1];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

                    _upline.Refresh();
                    _downline.Refresh();
                }
            }

            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        // logic open position
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_stocLastDown < _downline.Value && _stocLastDown > _stocLastUp
                                               && _regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(GetVolume(_tab),
                    _lastPrice + _slippage.ValueInt * _tab.Securiti.PriceStep);
            }

            if (_stocLastDown > _upline.Value && _stocLastDown < _stocLastUp
                                             && _regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(GetVolume(_tab),
                    _lastPrice - _slippage.ValueInt * _tab.Securiti.PriceStep);
            }
        }

        // logic close position
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_stocLastDown > _upline.Value && _stocLastDown < _stocLastUp)
                {
                    _tab.CloseAtLimit(
                        position,
                        _lastPrice - _slippage.ValueInt * _tab.Securiti.PriceStep,
                        position.OpenVolume);

                    if (_regime.ValueString != "OnlyLong" && _regime.ValueString != "OnlyClosePosition")
                    {
                        List<Position> positions = _tab.PositionsOpenAll;
                        if (positions.Count >= 2)
                        {
                            return;
                        }

                        _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage.ValueInt * _tab.Securiti.PriceStep);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_stocLastDown < _downline.Value && _stocLastDown > _stocLastUp)
                {
                    _tab.CloseAtLimit(
                        position,
                        _lastPrice + _slippage.ValueInt * _tab.Securiti.PriceStep,
                        position.OpenVolume);

                    if (_regime.ValueString != "OnlyShort" && _regime.ValueString != "OnlyClosePosition")
                    {
                        List<Position> positions = _tab.PositionsOpenAll;
                        if (positions.Count >= 2)
                        {
                            return;
                        }
                        _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage.ValueInt * _tab.Securiti.PriceStep);
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