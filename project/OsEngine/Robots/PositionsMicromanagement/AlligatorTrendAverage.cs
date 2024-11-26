/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.Robots.PositionsMicromanagement
{

    [Bot("AlligatorTrendAverage")]
    public class AlligatorTrendAverage : BotPanel
    {
        private BotTabSimple _tab;

        private Aindicator _alligator;

        public StrategyParameterString Regime;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal PyramidPercent;

        public StrategyParameterInt LengthJaw;
        public StrategyParameterInt LengthTeeth;
        public StrategyParameterInt LengthLips;

        public StrategyParameterInt ShiftJaw;
        public StrategyParameterInt ShiftTeeth;
        public StrategyParameterInt ShiftLips;

        public AlligatorTrendAverage(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            PyramidPercent = CreateParameter("Pyramid percent", 0.1m, 1.0m, 50, 4);

            LengthJaw = CreateParameter("Jaw length", 13, 1, 50, 4);
            LengthTeeth = CreateParameter("Teeth length", 8, 1, 50, 4);
            LengthLips = CreateParameter("Lips length", 5, 1, 50, 4);
            ShiftJaw = CreateParameter("Jaw offset", 8, 1, 50, 4);
            ShiftTeeth = CreateParameter("Teeth offset", 5, 1, 50, 4);
            ShiftLips = CreateParameter("Lips offset", 3, 1, 50, 4);

            _alligator = IndicatorsFactory.CreateIndicatorByName("Alligator", name + "Alligator", false);
            _alligator = (Aindicator)_tab.CreateCandleIndicator(_alligator, "Prime");

            _alligator.ParametersDigit[0].Value = LengthJaw.ValueInt;
            _alligator.ParametersDigit[1].Value = LengthTeeth.ValueInt;
            _alligator.ParametersDigit[2].Value = LengthLips.ValueInt;
            _alligator.ParametersDigit[3].Value = ShiftJaw.ValueInt;
            _alligator.ParametersDigit[4].Value = ShiftTeeth.ValueInt;
            _alligator.ParametersDigit[5].Value = ShiftLips.ValueInt;

            _alligator.Save();

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = "An example of a robot that shows sequential averaging of a position and increase of a position on a trend";
        }

        void Event_ParametrsChangeByUser()
        {
            if (_alligator.ParametersDigit[0].Value != LengthJaw.ValueInt ||
            _alligator.ParametersDigit[1].Value != LengthTeeth.ValueInt ||
            _alligator.ParametersDigit[2].Value != LengthLips.ValueInt ||
            _alligator.ParametersDigit[3].Value != ShiftJaw.ValueInt ||
            _alligator.ParametersDigit[4].Value != ShiftTeeth.ValueInt ||
            _alligator.ParametersDigit[5].Value != ShiftLips.ValueInt)
            {
                _alligator.ParametersDigit[0].Value = LengthJaw.ValueInt;
                _alligator.ParametersDigit[1].Value = LengthTeeth.ValueInt;
                _alligator.ParametersDigit[2].Value = LengthLips.ValueInt;
                _alligator.ParametersDigit[3].Value = ShiftJaw.ValueInt;
                _alligator.ParametersDigit[4].Value = ShiftTeeth.ValueInt;
                _alligator.ParametersDigit[5].Value = ShiftLips.ValueInt;

                _alligator.Reload();
                _alligator.Save();
            }
        }

        public override string GetNameStrategyType()
        {
            return "AlligatorTrendAverage";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (_alligator.DataSeries[0].Last == 0 ||
                _alligator.DataSeries[1].Last == 0 ||
                _alligator.DataSeries[2].Last == 0 ||
                _alligator.ParametersDigit[0].Value > candles.Count + 10 ||
                _alligator.ParametersDigit[1].Value > candles.Count + 10 ||
                _alligator.ParametersDigit[2].Value > candles.Count + 10)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (Regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicOpenPosition(candles);
            }
            else
            {
                LogicClosePosition(candles, openPositions[0]);
            }
        }

        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal lastSlowAlligator = _alligator.DataSeries[0].Last;

            decimal lastMiddleAlligator = _alligator.DataSeries[1].Last;

            decimal lastFastAlligator = _alligator.DataSeries[2].Last;

            if (lastPrice > lastSlowAlligator
             && lastPrice > lastMiddleAlligator
             && lastPrice > lastFastAlligator
             && lastFastAlligator > lastMiddleAlligator
             && lastMiddleAlligator > lastSlowAlligator
             && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtMarket(GetVolume(_tab));
            }

            if (lastPrice < lastSlowAlligator
             && lastPrice < lastMiddleAlligator
             && lastPrice < lastFastAlligator
             && lastFastAlligator < lastMiddleAlligator
             && lastMiddleAlligator < lastSlowAlligator
             && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtMarket(GetVolume(_tab));
            }
        }

        private void LogicClosePosition(List<Candle> candles, Position position)
        {
            if(position.State == PositionStateType.Opening
                || position.SignalTypeClose == "StandardExit")
            {
                return;
            }

            if(position.Comment == null)
            {
                position.Comment = "";
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            decimal lastSlowAlligator = _alligator.DataSeries[0].Last;
            decimal lastMiddleAlligator = _alligator.DataSeries[1].Last;
            decimal lastFastAlligator = _alligator.DataSeries[2].Last;

            // 1 Standard Exit / стандартный выход

            if (position.Direction == Side.Buy
                && lastPrice < lastSlowAlligator
                && lastPrice < lastMiddleAlligator
                && lastPrice < lastFastAlligator)
            {
                _tab.CloseAtMarket(position, position.OpenVolume, "StandardExit");
                return;
            }
            else if (position.Direction == Side.Sell
                && lastPrice > lastSlowAlligator
             && lastPrice > lastMiddleAlligator
             && lastPrice > lastFastAlligator)
            {
                _tab.CloseAtMarket(position, position.OpenVolume, "StandardExit");
                return;
            }

            // 2 Averaging / усреднение при возврате в канал

            if (position.Direction == Side.Buy
                && 
                (lastPrice < lastSlowAlligator
                || lastPrice < lastMiddleAlligator
                || lastPrice < lastFastAlligator))
            {
                if(position.Comment.Contains("Average") == false)
                {
                    position.Comment += "Average";
                    _tab.BuyAtMarketToPosition(position, GetVolume(_tab));
                    return;
                }
            }
            else if (position.Direction == Side.Sell
                &&
                (lastPrice > lastSlowAlligator
                || lastPrice > lastMiddleAlligator
                || lastPrice > lastFastAlligator))
            {
                if (position.Comment.Contains("Average") == false)
                {
                    position.Comment += "Average";
                    _tab.SellAtMarketToPosition(position, GetVolume(_tab));
                    return;
                }
            }

            // 3 Trend follow-up buying / пирамидинг по тренду

            if (position.Direction == Side.Buy
                &&
                (position.Comment.Contains("Pyramid") == false))
            {
                decimal pyramidPrice = 
                    position.EntryPrice + position.EntryPrice * (PyramidPercent.ValueDecimal/100);

                if(lastPrice > pyramidPrice)
                {
                    position.Comment += "Pyramid";
                    _tab.BuyAtMarketToPosition(position, GetVolume(_tab));
                    return;
                }

            }
            else if (position.Direction == Side.Sell 
                &&
                (position.Comment.Contains("Pyramid") == false))
            {
                decimal pyramidPrice =
                    position.EntryPrice - position.EntryPrice * (PyramidPercent.ValueDecimal / 100);

                if (lastPrice < pyramidPrice)
                {
                    position.Comment += "Pyramid";
                    _tab.SellAtMarketToPosition(position, GetVolume(_tab));
                    return;
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
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
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