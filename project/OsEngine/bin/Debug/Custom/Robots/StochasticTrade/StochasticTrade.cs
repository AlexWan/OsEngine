/*
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
        public StrategyParameterString Regime;
        public StrategyParameterInt Slippage;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Line Settings
        public StrategyParameterDecimal UpLineValue;
        public StrategyParameterDecimal DownLineValue;

        // Indicator Settings 
        public StrategyParameterInt StochPeriod1;
        public StrategyParameterInt StochPeriod2;
        public StrategyParameterInt StochPeriod3;

        // Indicator
        private Aindicator _stoch;

        // Line on chart
        public LineHorisontal Upline;
        public LineHorisontal Downline;

        // The last value of the indicator and price
        private decimal _stocLastUp;
        private decimal _stocLastDown;
        private decimal _lastPrice;

        public StochasticTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            Slippage = CreateParameter("Slippage", 0, 0, 20, 1);

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Line settungs
            UpLineValue = CreateParameter("Up Line Value", 80, 60.0m, 90, 0.5m);
            DownLineValue = CreateParameter("Down Line Value", 20, 10.0m, 40, 0.5m);

            // Indicator settings
            StochPeriod1 = CreateParameter("Stoch Period 1", 5, 3, 40, 1);
            StochPeriod2 = CreateParameter("Stoch Period 2", 3, 2, 40, 1);
            StochPeriod3 = CreateParameter("Stoch Period 3", 3, 2, 40, 1);

            // Create indicator Stochastic
            _stoch = IndicatorsFactory.CreateIndicatorByName("Stochastic", name + "Stochastic", false);
            _stoch = (Aindicator)_tab.CreateCandleIndicator(_stoch, "StochasticArea");
            _stoch.ParametersDigit[0].Value = StochPeriod1.ValueInt;
            _stoch.ParametersDigit[1].Value = StochPeriod2.ValueInt;
            _stoch.ParametersDigit[2].Value = StochPeriod3.ValueInt;
            _stoch.Save();

            // Create Upline on StochasticArea
            Upline = new LineHorisontal("upline", "StochasticArea", false)
            {
                Color = Color.Green,
                Value = 0,
            };
            _tab.SetChartElement(Upline);
            Upline.Value = UpLineValue.ValueDecimal;
            Upline.TimeEnd = DateTime.Now;

            // Create Downline on StochasticArea
            Downline = new LineHorisontal("downline", "StochasticArea", false)
            {
                Color = Color.Yellow,
                Value = 0

            };
            _tab.SetChartElement(Downline);
            Downline.Value = DownLineValue.ValueDecimal;
            Downline.TimeEnd = DateTime.Now;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += RviTrade_ParametrsChangeByUser;

            Description = "counter trend strategy stochastic. " +
                "The strategy is reverse. " +
                "Long entry: Crossing the lower horizontal line (default 20) by two values ​​of the Stochastic indicator. " +
                "Short Entry: Crossing the upper horizontal line (default 80) by two values ​​of the Stochastic indicator. " +
                "Exit Long: Crossing the upper horizontal line (80 by default) by two values ​​of the Stochastic indicator. " +
                "Exit Short: Crossing the lower horizontal line (default 20) by two values ​​of the Stochastic indicator.";
        }

        void RviTrade_ParametrsChangeByUser()
        {
            _stoch.ParametersDigit[0].Value = StochPeriod1.ValueInt;
            _stoch.ParametersDigit[1].Value = StochPeriod2.ValueInt;
            _stoch.ParametersDigit[2].Value = StochPeriod3.ValueInt;

            Upline.Value = UpLineValue.ValueDecimal;
            Upline.Refresh();

            Downline.Value = DownLineValue.ValueDecimal;
            Downline.Refresh();
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
            if (Regime.ValueString == "Off")
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

                    Upline.Refresh();
                    Downline.Refresh();
                }
            }

            if (Regime.ValueString == "OnlyClosePosition")
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
            if (_stocLastDown < Downline.Value && _stocLastDown > _stocLastUp
                                               && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(GetVolume(_tab),
                    _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
            }

            if (_stocLastDown > Upline.Value && _stocLastDown < _stocLastUp
                                             && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(GetVolume(_tab),
                    _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
            }
        }

        // logic close position
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if (position.Direction == Side.Buy)
            {
                if (_stocLastDown > Upline.Value && _stocLastDown < _stocLastUp)
                {
                    _tab.CloseAtLimit(
                        position,
                        _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep,
                        position.OpenVolume);

                    if (Regime.ValueString != "OnlyLong" && Regime.ValueString != "OnlyClosePosition")
                    {
                        List<Position> positions = _tab.PositionsOpenAll;
                        if (positions.Count >= 2)
                        {
                            return;
                        }

                        _tab.SellAtLimit(GetVolume(_tab), _lastPrice - Slippage.ValueInt * _tab.Securiti.PriceStep);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_stocLastDown < Downline.Value && _stocLastDown > _stocLastUp)
                {
                    _tab.CloseAtLimit(
                        position,
                        _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep,
                        position.OpenVolume);

                    if (Regime.ValueString != "OnlyShort" && Regime.ValueString != "OnlyClosePosition")
                    {
                        List<Position> positions = _tab.PositionsOpenAll;
                        if (positions.Count >= 2)
                        {
                            return;
                        }
                        _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + Slippage.ValueInt * _tab.Securiti.PriceStep);
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