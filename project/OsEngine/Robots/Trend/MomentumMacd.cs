/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;


namespace OsEngine.Robots.Trend
{
    /// <summary>
    /// Trend strategy based on 2 indicators Momentum and Macd
    /// Трендовая стратегия на основе 2х индикаторов Momentum и Macd
    /// </summary>
    [Bot("MomentumMacd")]
    public class MomentumMacd : BotPanel
    {
        public MomentumMacd(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            MomentumPeriod = CreateParameter("Momentum Period", 5, 0, 20, 1);

            SmaShortLen = CreateParameter("Macd Sma Short", 12, 0, 20, 1);

            SmaLongLen = CreateParameter("Macd Sma Long", 26, 0, 20, 1);

            SmaSignalLen = CreateParameter("Macd Sma Signal", 9, 0, 20, 1);

            Slippage = CreateParameter("Slippage", 0, 0, 20, 1);

            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            _macd = new MacdLine(name + "Macd", false);
            _macd.SmaShortLen = SmaShortLen.ValueInt;
            _macd.SmaLongLen = SmaLongLen.ValueInt;
            _macd.SmaSignalLen = SmaSignalLen.ValueInt;
            _macd = (MacdLine)_tab.CreateCandleIndicator(_macd, "MacdArea");
            _macd.Save();

            _mom = new Momentum(name + "Momentum", false);
            _mom.Nperiod = MomentumPeriod.ValueInt;
            _mom = (Momentum)_tab.CreateCandleIndicator(_mom, "Momentum");
            _mom.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;

            DeleteEvent += Strategy_DeleteEvent;

            Description = "Trend strategy based on 2 indicators Momentum and Macd. " +
                "if lastMacdUp < lastMacdDown and lastMom < 100 - close position and open Short. " +
                "if lastMacdUp > lastMacdDown and lastMom > 100 - close position and open Long.";

            this.ParametrsChangeByUser += MomentumMacd_ParametrsChangeByUser;
        }

        private void MomentumMacd_ParametrsChangeByUser()
        {
            if (_macd.SmaShortLen != SmaShortLen.ValueInt
                || _macd.SmaLongLen != SmaLongLen.ValueInt
                || _macd.SmaSignalLen != SmaSignalLen.ValueInt)
            {
                _macd.SmaShortLen = SmaShortLen.ValueInt;
                _macd.SmaLongLen = SmaLongLen.ValueInt;
                _macd.SmaSignalLen = SmaSignalLen.ValueInt;
                _macd.Reload();
            }

            if(_mom.Nperiod != MomentumPeriod.ValueInt)
            {
                _mom.Nperiod = MomentumPeriod.ValueInt;
                _mom.Reload();
            }
        }

        /// <summary>
        /// strategy uniq name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "MomentumMacd";
        }

        /// <summary>
        /// strategy GUI
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {

        }

        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //indicators индикаторы

        private MacdLine _macd;

        private Momentum _mom;

        //settings настройки публичные

        public StrategyParameterString Regime;

        public StrategyParameterInt MomentumPeriod;

        public StrategyParameterInt SmaShortLen;

        public StrategyParameterInt SmaLongLen;

        public StrategyParameterInt SmaSignalLen;

        public StrategyParameterInt Slippage;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        private void Strategy_DeleteEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
            }
        }

        private decimal _lastClose;

        private decimal _lastMacdUp;

        private decimal _lastMacdDown;

        private decimal _lastMom;

        // logic логика

        /// <summary>
        /// candle finished event
        /// событие завершения свечи
        /// </summary>
        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_macd.ValuesUp == null || _macd.ValuesDown == null ||
                _mom.Nperiod + 3 > _mom.Values.Count)
            {
                return;
            }

            _lastClose = candles[candles.Count - 1].Close;
            _lastMacdUp = _macd.ValuesUp[_macd.ValuesUp.Count - 1];
            _lastMacdDown = _macd.ValuesDown[_macd.ValuesDown.Count - 1];
            _lastMom = _mom.Values[_mom.Values.Count - 1];

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    LogicClosePosition(candles, openPositions[i]);

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

        /// <summary>
        /// logic open position
        /// логика открытия первой позиции
        /// </summary>
        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            if (_lastMacdUp > _lastMacdDown && _lastMom > 100 && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtLimit(GetVolume(_tab), _lastClose + Slippage.ValueInt * _tab.Security.PriceStep);
            }
            if (_lastMacdUp < _lastMacdDown && _lastMom < 100 && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtLimit(GetVolume(_tab), _lastClose - Slippage.ValueInt * _tab.Security.PriceStep);
            }
        }

        /// <summary>
        /// logic close position
        /// логика зыкрытия позиции и открытие по реверсивной системе
        /// </summary>
        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State != PositionStateType.Open)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                decimal exitPrice = _lastClose - Slippage.ValueInt * _tab.Security.PriceStep;

                if (_lastMacdUp < _lastMacdDown && _lastMom < 100)
                {
                    _tab.CloseAtLimit(position, exitPrice, position.OpenVolume);

                    if (Regime.ValueString != "OnlyLong"
                        && Regime.ValueString != "OnlyClosePosition")
                    {
                        _tab.SellAtLimit(GetVolume(_tab), exitPrice);
                    }
                }
            }

            if (position.Direction == Side.Sell)
            {
                decimal exitPrice = _lastClose + Slippage.ValueInt * _tab.Security.PriceStep;

                if (_lastMacdUp > _lastMacdDown && _lastMom > 100)
                {
                    _tab.CloseAtLimit(position, exitPrice, position.OpenVolume);

                    if (Regime.ValueString != "OnlyShort" 
                        && Regime.ValueString != "OnlyClosePosition")
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), exitPrice);
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