/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.TechSamples
{
    [Bot("StopByTradeFeedSample")]
    public class StopByTradeFeedSample : BotPanel
    {
        public StopByTradeFeedSample(string name, StartProgram startProgram)
      : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            Slippage = CreateParameter("Slippage in price step", 0, 0, 20, 1);
            IndLength = CreateParameter("Price channel length", 10, 10, 80, 3);

            TrailStopPercent = CreateParameter("Trail stop percent", 0.2m, 0.5m, 5, 4);

            VolumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 10, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tab.CreateCandleIndicator(_pc, "Prime");

            _pc.ParametersDigit[0].Value = IndLength.ValueInt;
            _pc.ParametersDigit[1].Value = IndLength.ValueInt;

            _pc.Save();

            _tab.CandleFinishedEvent += Strateg_CandleFinishedEvent;
            _tab.NewTickEvent += _tab_NewTickEvent;

            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            Description = "An example of a robot that pulls up the stop for a position based on changes in the deals feed. IMPORTANT! Tests of this robot should be conducted on the deals feed.";
        }

        void Event_ParametrsChangeByUser()
        {
            if (IndLength.ValueInt != _pc.ParametersDigit[0].Value ||
                IndLength.ValueInt != _pc.ParametersDigit[1].Value)
            {
                _pc.ParametersDigit[0].Value = IndLength.ValueInt;
                _pc.ParametersDigit[1].Value = IndLength.ValueInt;

                _pc.Reload();
            }
        }

        public override string GetNameStrategyType()
        {
            return "StopByTradeFeedSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabSimple _tab;

        private Aindicator _pc;

        public StrategyParameterString Regime;

        public StrategyParameterInt IndLength;

        public StrategyParameterDecimal TrailStopPercent;

        public StrategyParameterInt Slippage;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        private void Strateg_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_pc.DataSeries[0].Values == null || _pc.DataSeries[1].Values == null)
            {
                return;
            }

            if (_pc.DataSeries[0].Values.Count < _pc.ParametersDigit[0].Value + 2 || _pc.DataSeries[1].Values.Count < _pc.ParametersDigit[1].Value + 2)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }
            if (openPositions == null 
                || openPositions.Count == 0)
            {
                LogicOpenPosition(candles, openPositions);
            }
        }

        private void LogicOpenPosition(List<Candle> candles, List<Position> position)
        {
            decimal _lastPrice = candles[candles.Count - 1].Close;
            decimal _lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 2];
            decimal _lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 2];

            List<Position> openPositions = _tab.PositionsOpenAll;
            if (openPositions == null || openPositions.Count == 0)
            {
                // long
                if (Regime.ValueString != "OnlyShort")
                {
                    if (_lastPrice > _lastPcUp)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _lastPrice + Slippage.ValueInt * _tab.Security.PriceStep);
                    }
                }

                // Short
                if (Regime.ValueString != "OnlyLong")
                {
                    if (_lastPrice < _lastPcDown)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _lastPrice - Slippage.ValueInt * _tab.Security.PriceStep);
                    }
                }
            }
        }

        private void _tab_NewTickEvent(Trade trade)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (Regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            if (openPositions == null
                || openPositions.Count == 0)
            {
                return;
            }

            Position myPos = openPositions[0];

            if(myPos.State != PositionStateType.Open)
            {
                return;
            }

            decimal stopPrice = 0;
            decimal orderPrice = 0;

            if (myPos.Direction == Side.Buy)
            {
                stopPrice = trade.Price - (trade.Price * (TrailStopPercent.ValueDecimal/100));
                orderPrice = stopPrice - Slippage.ValueInt * _tab.Security.PriceStep;
            }
            else if(myPos.Direction == Side.Sell)
            {
                stopPrice = trade.Price + (trade.Price * (TrailStopPercent.ValueDecimal / 100));
                orderPrice = stopPrice + Slippage.ValueInt * _tab.Security.PriceStep;
            }

            _tab.CloseAtTrailingStop(myPos,stopPrice,orderPrice);

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
                    tab.Securiti.Lot != 0 &&
                        tab.Securiti.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Securiti.Lot);
                    }

                    volume = Math.Round(volume, tab.Securiti.DecimalsVolume);
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
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Securiti.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Securiti.DecimalsVolume);
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