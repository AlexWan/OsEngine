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
    [Bot("EnvelopsCountertrend")]
    public class EnvelopsCountertrend : BotPanel
    {
        private BotTabSimple _tab;

        private Aindicator _envelop;

        public StrategyParameterString Regime;

        public StrategyParameterInt EnvelopsLength;

        public StrategyParameterDecimal EnvelopsDeviation;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        public StrategyParameterDecimal ProfitPercent;

        public StrategyParameterDecimal StopPercent;

        public StrategyParameterDecimal AveragingOnePercent;

        public StrategyParameterDecimal AveragingTwoPercent;

        public EnvelopsCountertrend(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            Volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            EnvelopsLength = CreateParameter("Envelops length", 50, 10, 80, 3);
            EnvelopsDeviation = CreateParameter("Envelops deviation", 0.1m, 0.5m, 5, 0.1m);

            ProfitPercent = CreateParameter("Profit percent", 0.3m, 1.0m, 50, 4);
            StopPercent = CreateParameter("Stop percent", 0.5m, 1.0m, 50, 4);
            AveragingOnePercent = CreateParameter("Averaging one percent", 0.1m, 1.0m, 50, 4);
            AveragingTwoPercent = CreateParameter("Averaging two percent", 0.2m, 1.0m, 50, 4);

            _envelop = IndicatorsFactory.CreateIndicatorByName("Envelops", name + "Envelops", false);
            _envelop = (Aindicator)_tab.CreateCandleIndicator(_envelop, "Prime");
            _envelop.ParametersDigit[0].Value = EnvelopsLength.ValueInt;
            _envelop.ParametersDigit[1].Value = EnvelopsDeviation.ValueDecimal;
            _envelop.Save();

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            ParametrsChangeByUser += Event_ParametrsChangeByUser;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = "An example of a robot that shows sequential position averaging by opening new positions by pending orders";
        }

        void Event_ParametrsChangeByUser()
        {
            if (EnvelopsLength.ValueInt != _envelop.ParametersDigit[0].Value ||
               _envelop.ParametersDigit[1].Value != EnvelopsDeviation.ValueDecimal)
            {
                _envelop.ParametersDigit[0].Value = EnvelopsLength.ValueInt;
                _envelop.ParametersDigit[1].Value = EnvelopsDeviation.ValueDecimal;
                _envelop.Reload();
                _envelop.Save();
            }
        }

        public override string GetNameStrategyType()
        {
            return "EnvelopsCountertrend";
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

            if (_envelop.DataSeries[0].Values == null
                || _envelop.DataSeries[1].Values == null)
            {
                return;
            }

            if (_envelop.DataSeries[0].Values.Count < _envelop.ParametersDigit[0].Value + 2
                || _envelop.DataSeries[1].Values.Count < _envelop.ParametersDigit[1].Value + 2)
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
                LogicClosePosition(candles, openPositions);
            }
        }

        private void LogicOpenPosition(List<Candle> candles)
        {
            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal lastEnvelopsUp = _envelop.DataSeries[0].Values[_envelop.DataSeries[0].Values.Count - 1];
            decimal lastEnvelopsDown = _envelop.DataSeries[2].Values[_envelop.DataSeries[1].Values.Count - 1];

            if (lastEnvelopsUp == 0
                || lastEnvelopsDown == 0)
            {
                return;
            }

            if (lastPrice > lastEnvelopsUp
                && Regime.ValueString != "OnlyLong")
            {
                _tab.SellAtMarket(GetVolume(_tab));
            }
            if (lastPrice < lastEnvelopsDown
                && Regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtMarket(GetVolume(_tab));
            }
        }

        private void LogicClosePosition(List<Candle> candles, List<Position> positions)
        {
            if (positions[0].SignalTypeClose == "StopActivate"
                || positions[0].SignalTypeClose == "ProfitActivate"
                || positions[0].State == PositionStateType.Opening)
            {
                return;
            }

            // первое усреднение

            if (positions.Count == 1)
            {
                decimal nextEntryPrice = 0;

                decimal firstPosEntryPrice = positions[0].EntryPrice;

                if (positions[0].Direction == Side.Buy)
                {
                    nextEntryPrice = firstPosEntryPrice - firstPosEntryPrice * (AveragingOnePercent.ValueDecimal / 100);
                    _tab.BuyAtStopMarket(GetVolume(_tab), nextEntryPrice, nextEntryPrice, 
                        StopActivateType.LowerOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
                }
                else if (positions[0].Direction == Side.Sell)
                {
                    nextEntryPrice = firstPosEntryPrice + firstPosEntryPrice * (AveragingOnePercent.ValueDecimal / 100);
                    _tab.SellAtStopMarket(GetVolume(_tab), nextEntryPrice, nextEntryPrice, 
                        StopActivateType.HigherOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
                }
            }

            // второе усреднение

            if (positions.Count == 2)
            {
                decimal nextEntryPrice = 0;

                decimal firstPosEntryPrice = positions[0].EntryPrice;

                if (positions[0].Direction == Side.Buy)
                {
                    nextEntryPrice = firstPosEntryPrice - firstPosEntryPrice * (AveragingTwoPercent.ValueDecimal / 100);
                    _tab.BuyAtStopMarket(GetVolume(_tab), nextEntryPrice, nextEntryPrice, 
                        StopActivateType.LowerOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
                }
                else if (positions[0].Direction == Side.Sell)
                {
                    nextEntryPrice = firstPosEntryPrice + firstPosEntryPrice * (AveragingTwoPercent.ValueDecimal / 100);
                    _tab.SellAtStopMarket(GetVolume(_tab), nextEntryPrice, nextEntryPrice, 
                        StopActivateType.HigherOrEqual, 1, "", PositionOpenerToStopLifeTimeType.CandlesCount);
                }
            }

            // считаем среднюю цену входа

            decimal middleEntryPrice = 0;

            decimal allVolume = 0;

            for (int i = 0; i < positions.Count; i++)
            {
                middleEntryPrice += positions[i].EntryPrice * positions[i].OpenVolume;
                allVolume += positions[i].OpenVolume;
            }

            if(allVolume == 0)
            {
                return;
            }

            middleEntryPrice = middleEntryPrice / allVolume;

            // профит

            decimal profitPrice = 0;

            if (positions[0].Direction == Side.Buy)
            {
                profitPrice = middleEntryPrice + middleEntryPrice * (ProfitPercent.ValueDecimal / 100);
            }
            else if (positions[0].Direction == Side.Sell)
            {
                profitPrice = middleEntryPrice - middleEntryPrice * (ProfitPercent.ValueDecimal / 100);
            }

            for (int i = 0; i < positions.Count; i++)
            {
                _tab.CloseAtProfitMarket(positions[i], profitPrice, "ProfitActivate");
            }

            // стоп

            decimal stopPrice = 0;

            if (positions[0].Direction == Side.Buy)
            {
                stopPrice = middleEntryPrice - middleEntryPrice * (StopPercent.ValueDecimal / 100);
            }
            else if (positions[0].Direction == Side.Sell)
            {
                stopPrice = middleEntryPrice + middleEntryPrice * (StopPercent.ValueDecimal / 100);
            }

            for (int i = 0; i < positions.Count; i++)
            {
                _tab.CloseAtStopMarket(positions[i], stopPrice, "StopActivate");
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