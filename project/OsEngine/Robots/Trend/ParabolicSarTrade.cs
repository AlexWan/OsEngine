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

/* Description
Trading robot for osengine.

Trend strategy at the intersection of the ParabolicSar indicator.

Buy:
If Price > lastSar - close position and open Long.

Sell: 
If Price < lastSar - close position and open Short.
 */

namespace OsEngine.Robots.Trend
{
    [Bot("ParabolicSarTrade")] // We create an attribute so that we don't write anything to the BotFactory
    public class ParabolicSarTrade : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settings
        public StrategyParameterString _regime;
        public StrategyParameterInt _slippage;

        // GetVolume settings
        public StrategyParameterString _volumeType;
        public StrategyParameterDecimal _volume;
        public StrategyParameterString _tradeAssetInPortfolio;
        
        // Indicator settings
        public StrategyParameterDecimal _parabolicAf;
        public StrategyParameterDecimal _parabolicMaxAf;
        
        // Indicator
        private Aindicator _parabolic;

        // The last value of the indicator and price
        private decimal _lastPrice;
        private decimal _lastSar;

        public ParabolicSarTrade(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage", 0, 0, 20, 1);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            
            // Indicator settings
            _parabolicAf = CreateParameter("Parabolic Af", 0.02m, 0.02m, 0.1m, 0.01m);
            _parabolicMaxAf = CreateParameter("Parabolic Max Af", 0.2m, 0.2m, 0.5m, 0.1m);

            // Create indicator ParabolicSAR
            _parabolic = IndicatorsFactory.CreateIndicatorByName("ParabolicSAR", name + "Par", false);
            _parabolic = (Aindicator)_tab.CreateCandleIndicator(_parabolic, "Prime");
            ((IndicatorParameterDecimal)_parabolic.Parameters[0]).ValueDecimal = _parabolicAf.ValueDecimal;
            ((IndicatorParameterDecimal)_parabolic.Parameters[1]).ValueDecimal = _parabolicMaxAf.ValueDecimal;
            _parabolic.Save();

            // Subscribe to receive events/commands from Telegram
            ServerTelegram.GetServer().TelegramCommandEvent += TelegramCommandHandler;

            // Subscribe to the indicator update event
            ParametrsChangeByUser += ParabolicSarTrade_ParametrsChangeByUser;

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel116;
        }

        private void ParabolicSarTrade_ParametrsChangeByUser()
        {
            ((IndicatorParameterDecimal)_parabolic.Parameters[0]).ValueDecimal = _parabolicAf.ValueDecimal;
            ((IndicatorParameterDecimal)_parabolic.Parameters[1]).ValueDecimal = _parabolicMaxAf.ValueDecimal;
            _parabolic.Save();
            _parabolic.Reload();
        }

        private void TelegramCommandHandler(string botName, Command cmd)
        {
            if (botName != null && !_tab.TabName.Equals(botName)) 
                return;
            
            if (cmd == Command.StopAllBots || cmd == Command.StopBot)
            {
                _regime.ValueString = BotTradeRegime.Off.ToString();
                
                SendNewLogMessage($"Changed Bot {_tab.TabName} Regime to Off " +
                                  $"by telegram command {cmd}", LogMessageType.User);
            }
            else if (cmd == Command.StartAllBots || cmd == Command.StartBot)
            {
                _regime.ValueString = BotTradeRegime.On.ToString();

                //changing bot mode to its previous state or On
                SendNewLogMessage($"Changed bot {_tab.TabName} mode to state {_regime} " +
                                  $"by telegram command {cmd}", LogMessageType.User);
            }
            else if (cmd == Command.CancelAllActiveOrders)
            {
                //Some logic for cancel all active orders
            }
            else if (cmd == Command.GetStatus)
            {
                SendNewLogMessage($"Bot {_tab.TabName} is {_regime}. Emulator - {_tab.EmulatorIsOn}, " +
                                  $"Server Status - {_tab.ServerStatus}.", LogMessageType.User);
            }
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ParabolicSarTrade";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic логика
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (_parabolic.DataSeries[0].Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastSar = _parabolic.DataSeries[0].Last;

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i], openPositions);
                }
            }

            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            if (openPositions == null 
                || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Logic open position
        private void LogicOpenPosition(List<Candle> candles)
        {
            if (_lastPrice > _lastSar && _regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
            {
                _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
            }

            if (_lastPrice < _lastSar && _regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
            {
                _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles, Position position, List<Position> positionsAll)
        {
            if(position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy) // If the direction of the position is long
            {
                if (_lastPrice < _lastSar)
                {
                    _tab.CloseAtLimit(position, _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);

                    if (_regime.ValueString != "OnlyLong" 
                        && _regime.ValueString != "OnlyClosePosition"
                        && positionsAll.Count == 1)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _lastPrice - _slippage.ValueInt * _tab.Security.PriceStep);
                    }
                }
            }

            if (position.Direction == Side.Sell) // If the direction of the position is short
            {
                if (_lastPrice > _lastSar)
                {
                    _tab.CloseAtLimit(position, _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);

                    if (_regime.ValueString != "OnlyShort"
                        && _regime.ValueString != "OnlyClosePosition"
                        && positionsAll.Count == 1)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + _slippage.ValueInt * _tab.Security.PriceStep);
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