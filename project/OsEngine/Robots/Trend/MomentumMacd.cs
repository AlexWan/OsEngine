/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

/* Description
Trading robot for osengine.

Trend strategy based on 2 indicators Momentum and Macd. 

Buy:
If lastMacdUp > lastMacdDown and lastMom > 100 - close position and open Long.

Sell: 
If lastMacdUp < lastMacdDown and lastMom < 100 - close position and open Short.
*/

namespace OsEngine.Robots.Trend
{
    [Bot("MomentumMacd")] // We create an attribute so that we don't write anything to the BotFactory
    public class MomentumMacd : BotPanel
    {
        private BotTabSimple _tab;

        // Indicators
        private Aindicator _macdLine;
        private Aindicator _momentum;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _momentumPeriod;
        private StrategyParameterInt _smaShortLen;
        private StrategyParameterInt _smaLongLen;
        private StrategyParameterInt _smaSignalLen;

        // The last value of the indicator and close
        private decimal _lastClose;
        private decimal _lastMacdUp;
        private decimal _lastMacdDown;
        private decimal _lastMom;

        public MomentumMacd(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

            // Indicator settings
            _momentumPeriod = CreateParameter("Momentum Period", 5, 0, 20, 1);
            _smaShortLen = CreateParameter("Macd Sma Short", 12, 0, 20, 1);
            _smaLongLen = CreateParameter("Macd Sma Long", 26, 0, 20, 1);
            _smaSignalLen = CreateParameter("Macd Sma Signal", 9, 0, 20, 1);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Create indicator MacdLine
            _macdLine = IndicatorsFactory.CreateIndicatorByName("MacdLine", name + "MACD", false);
            _macdLine = (Aindicator)_tab.CreateCandleIndicator(_macdLine, "MacdArea");
            ((IndicatorParameterInt)_macdLine.Parameters[0]).ValueInt = _smaShortLen.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[1]).ValueInt = _smaLongLen.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[2]).ValueInt = _smaSignalLen.ValueInt;
            _macdLine.Save();

            // Create indicator Momentum
            _momentum = IndicatorsFactory.CreateIndicatorByName("Momentum", name + "Momentum Length", false);
            _momentum = (Aindicator)_tab.CreateCandleIndicator(_momentum, "MomentumArea");
            ((IndicatorParameterInt)_momentum.Parameters[0]).ValueInt = _momentumPeriod.ValueInt;
            _momentum.Save();

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            // Subscribe to the strategy delete event
            DeleteEvent += Strategy_DeleteEvent;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += MomentumMacd_ParametrsChangeByUser;

            Description = OsLocalization.Description.DescriptionLabel113;
        }

        private void MomentumMacd_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_macdLine.Parameters[0]).ValueInt = _smaShortLen.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[1]).ValueInt = _smaLongLen.ValueInt;
            ((IndicatorParameterInt)_macdLine.Parameters[2]).ValueInt = _smaSignalLen.ValueInt;
            _macdLine.Save();
            _macdLine.Reload();
            ((IndicatorParameterInt)_momentum.Parameters[0]).ValueInt = _momentumPeriod.ValueInt;
            _momentum.Save();
            _momentum.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "MomentumMacd";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        private void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        // Logic
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_macdLine.DataSeries[0].Values == null || _macdLine.DataSeries[1].Values == null)
            {
                return;
            }

            _lastClose = candles[candles.Count - 1].Close;
            _lastMacdUp = _macdLine.DataSeries[0].Last;
            _lastMacdDown = _macdLine.DataSeries[1].Last;
            _lastMom = _momentum.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

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

        // Logic open position
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastMacdUp > _lastMacdDown && _lastMom > 100 && _regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                _tab.BuyAtLimit(GetVolume(_tab), _lastClose + _slippage.ValueInt * _tab.Security.PriceStep);
            }
            if (_lastMacdUp < _lastMacdDown && _lastMom < 100 && _regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {
                _tab.SellAtLimit(GetVolume(_tab), _lastClose - _slippage.ValueInt * _tab.Security.PriceStep);
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy) // If the direction of the position is long
            {
                decimal exitPrice = _lastClose - _slippage.ValueInt * _tab.Security.PriceStep;

                if (_lastMacdUp < _lastMacdDown && _lastMom < 100)
                {
                    _tab.CloseAtLimit(position, exitPrice, position.OpenVolume);

                    if (_regime.ValueString != "OnlyLong"
                        && _regime.ValueString != "OnlyClosePosition")
                    {
                        _tab.SellAtLimit(GetVolume(_tab), exitPrice);
                    }
                }
            }

            if (position.Direction == Side.Sell) // If the direction of the position is short
            {
                decimal exitPrice = _lastClose + _slippage.ValueInt * _tab.Security.PriceStep;

                if (_lastMacdUp > _lastMacdDown && _lastMom > 100)
                {
                    _tab.CloseAtLimit(position, exitPrice, position.OpenVolume);

                    if (_regime.ValueString != "OnlyShort" 
                        && _regime.ValueString != "OnlyClosePosition")
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), exitPrice);
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
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, LogMessageType.Error);
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