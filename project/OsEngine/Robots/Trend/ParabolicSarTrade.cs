/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.Trend
{
    [Bot("ParabolicSarTrade")]
    public class ParabolicSarTrade : BotPanel
    {
        public ParabolicSarTrade(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");
            Slippage = CreateParameter("Slippage", 0, 0, 20, 1);
            ParabolicAf = CreateParameter("Parabolic Af", 0.02m, 0.02m, 0.1m, 0.01m);
            ParabolicMaxAf = CreateParameter("Parabolic Max Af", 0.2m, 0.2m, 0.5m, 0.1m);

            _sar = new ParabolicSaR(name + "Prime", false);
            _sar = (ParabolicSaR)_tab.CreateCandleIndicator(_sar, "Prime");
            _sar.Af = Convert.ToDouble(ParabolicAf.ValueDecimal);
            _sar.MaxAf = Convert.ToDouble(ParabolicMaxAf.ValueDecimal);
            _sar.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            Description = "Trend strategy at the intersection of the ParabolicSar indicator. " +
                "if Price < lastSar - close position and open Short. " +
                "if Price > lastSar - close position and open Long.";

            //Подписка на получение событий/команд из телеграма - Subscribe to receive events/commands from Telegram
            ServerTelegram.GetServer().TelegramCommandEvent += TelegramCommandHandler;

            ParametrsChangeByUser += ParabolicSarTrade_ParametrsChangeByUser;
        }

        private void ParabolicSarTrade_ParametrsChangeByUser()
        {
            double af = Convert.ToDouble(ParabolicAf.ValueDecimal);
            double maxAf = Convert.ToDouble(ParabolicMaxAf.ValueDecimal);

            if(_sar.Af != af ||
                _sar.MaxAf != maxAf)
            {
                _sar.Af = Convert.ToDouble(ParabolicAf.ValueDecimal);
                _sar.MaxAf = Convert.ToDouble(ParabolicMaxAf.ValueDecimal);
                _sar.Reload();
                _sar.Save();
            }
        }

        public StrategyParameterInt Slippage;
        public StrategyParameterString VolumeType;
        public StrategyParameterDecimal Volume;
        public StrategyParameterString TradeAssetInPortfolio;
        public StrategyParameterString Regime;
        public StrategyParameterDecimal ParabolicAf;
        public StrategyParameterDecimal ParabolicMaxAf;
       
        private void TelegramCommandHandler(string botName, Command cmd)
        {
            if (botName != null && !_tab.TabName.Equals(botName)) 
                return;
            
            if (cmd == Command.StopAllBots || cmd == Command.StopBot)
            {
                Regime.ValueString = BotTradeRegime.Off.ToString();
                
                SendNewLogMessage($"Changed Bot {_tab.TabName} Regime to Off " +
                                  $"by telegram command {cmd}", LogMessageType.User);
            }
            else if (cmd == Command.StartAllBots || cmd == Command.StartBot)
            {
                Regime.ValueString = BotTradeRegime.On.ToString();

                //changing bot mode to its previous state or On
                SendNewLogMessage($"Changed bot {_tab.TabName} mode to state {Regime} " +
                                  $"by telegram command {cmd}", LogMessageType.User);
            }
            else if (cmd == Command.CancelAllActiveOrders)
            {
                //Some logic for cancel all active orders
            }
            else if (cmd == Command.GetStatus)
            {
                SendNewLogMessage($"Bot {_tab.TabName} is {Regime}. Emulator - {_tab.EmulatorIsOn}, " +
                                  $"Server Status - {_tab.ServerStatus}.", LogMessageType.User);
            }
        }
        
        public override string GetNameStrategyType()
        {
            return "ParabolicSarTrade";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabSimple _tab;

        private ParabolicSaR _sar;

        private decimal _lastPrice;

        private decimal _lastSar;

        // logic логика

        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            //SendNewLogMessage("Candle finished event", LogMessageType.User);

            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_sar.Values == null)
            {
                return;
            }

            _lastPrice = candles[candles.Count - 1].Close;
            _lastSar = _sar.Values[_sar.Values.Count - 1];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i], openPositions);
                }
            }

            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }
            if (openPositions == null 
                || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        private void LogicOpenPosition(List<Candle> candles)
        {
            if (_lastPrice > _lastSar && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + Slippage.ValueInt * _tab.Security.PriceStep);
            }

            if (_lastPrice < _lastSar && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(GetVolume(_tab), _lastPrice - Slippage.ValueInt * _tab.Security.PriceStep);
            }
        }

        private void LogicClosePosition(List<Candle> candles, Position position, List<Position> positionsAll)
        {
            if(position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                if (_lastPrice < _lastSar)
                {
                    _tab.CloseAtLimit(position, _lastPrice - Slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);

                    if (Regime.ValueString != "OnlyLong" 
                        && Regime.ValueString != "OnlyClosePosition"
                        && positionsAll.Count == 1)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _lastPrice - Slippage.ValueInt * _tab.Security.PriceStep);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                if (_lastPrice > _lastSar)
                {
                    _tab.CloseAtLimit(position, _lastPrice + Slippage.ValueInt * _tab.Security.PriceStep, position.OpenVolume);

                    if (Regime.ValueString != "OnlyShort"
                        && Regime.ValueString != "OnlyClosePosition"
                        && positionsAll.Count == 1)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + Slippage.ValueInt * _tab.Security.PriceStep);
                    }
                }
            }
        }

        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType.ValueString == "Contracts")
            {
                volume = Volume.ValueDecimal;
            }
            else if (VolumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = Volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (VolumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (TradeAssetInPortfolio.ValueString == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, LogMessageType.Error);
                    return 0;
                }
                decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

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