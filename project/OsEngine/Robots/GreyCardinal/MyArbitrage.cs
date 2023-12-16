using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.Indicators;

/*

namespace OsEngine.Robots.GreyCardinal
{
    internal class MyArbitrage
    {
    }
}
*/

namespace OsEngine.Robots.GreyCardinal
{
    [Bot("MyArbitrage")]

    internal class MyArbitrage : BotPanel
    {

        private BotTabSimple _tabSimple1,_tabSimple2;
        private BotTabIndex _tabIndex;
        private StrategyParameterBool isActive;
        private StrategyParameterDecimal volumeParam;
        private StrategyParameterInt pointsSL, pointsTP;

        private Aindicator _sma, _ivashov;


        public override string GetNameStrategyType()
        {
            return "MyArbitrage";
        }


        public override void ShowIndividualSettingsDialog()
        {

        }


        // Constructor
        public MyArbitrage(string name, StartProgram startProgram) : base(name, startProgram)
        {
            Description = "This is bot description";

            TabCreate(BotTabType.Simple); _tabSimple1 = TabsSimple[0];
            TabCreate(BotTabType.Simple); _tabSimple2 = TabsSimple[0];
            TabCreate(BotTabType.Index);  _tabIndex = TabsIndex[0];

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "sma", false);
            _sma = (Aindicator)_tabIndex.CreateCandleIndicator(_sma, "Prime");
            _sma.Save();

            _ivashov = IndicatorsFactory.CreateIndicatorByName("IvashovRange", name + "Ivashov", false);
            _ivashov = (Aindicator)_tabIndex.CreateCandleIndicator(_ivashov, "NewArea");
            _ivashov.Save();

            _tabIndex.SpreadChangeEvent += _tabIndex_SpreadChangeEvent;
            
            // Events
            _tabSimple1.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tabSimple1.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tabSimple1.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
            _tabSimple1.NewTickEvent += _tab_NewTickEvent;

            // Params
            isActive = CreateParameter("Is Active", true);
            volumeParam = CreateParameter("Volume", 1.0m, 1.0m, 10.0m, 1.0m);
            pointsSL = CreateParameter("Points to StopLost", 50, 10, 1000, 1);
            pointsTP = CreateParameter("Points to TakeProfit", 150, 10, 1000, 1);

        }

        private void _tabIndex_SpreadChangeEvent(List<Candle> indexCandles)
        {
            if (_sma.DataSeries[0].Values == null || _ivashov.DataSeries[0].Values == null)
            {
                return;
            }
            List<Position> positionTabSample = _tabSimple1.PositionsOpenAll
                , positionTab2 = _tabSimple2.PositionsOpenAll;

            decimal lastIndexPrice = indexCandles[indexCandles.Count - 1].Close;
            decimal lastSma = _sma.DataSeries[0].Last;
            decimal lastIvashov = _ivashov.DataSeries[0].Last;

            if (positionTabSample.Count == 0 && positionTab2.Count == 0)
            {
                if (lastIndexPrice > lastSma + 2 * lastIvashov)
                {
                    _tabSimple1.BuyAtMarket(1);
                    _tabSimple2.SellAtMarket(1);
                }
                else if (lastIndexPrice < lastSma - 2 * lastIvashov)
                {
                    _tabSimple1.SellAtMarket(1);
                    _tabSimple2.BuyAtMarket(1);
                }

            }
            else
            {
                if ((positionTabSample.Count != 0 && positionTabSample[0].Direction == Side.Buy) ||
                    (positionTab2.Count != 0 && positionTab2[0].Direction == Side.Sell))
                {
                    if (lastIndexPrice < lastSma - 2 * lastIvashov)
                    {
                        _tabSimple1.CloseAllAtMarket();
                        _tabSimple2.CloseAllAtMarket();
                    }

                }
                else if ((positionTabSample.Count != 0 && positionTabSample[0].Direction == Side.Sell) ||
                      (positionTab2.Count != 0 && positionTab2[0].Direction == Side.Buy))
                {
                    if (lastIndexPrice > lastSma + 2 * lastIvashov)
                    {
                        _tabSimple1.CloseAllAtMarket();
                        _tabSimple2.CloseAllAtMarket();
                    }

                }
            }
        }
        // Follow opening positions
        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            trallingPosition(position, _tabSimple1);
        }

        private void trallingPosition(Position position, BotTabSimple tab)
        {
            decimal _newStopPrice = 0, _newProfitPrice = 0;
            if (position.Direction == Side.Buy)
            {
                _newStopPrice = tab.PriceBestAsk - pointsSL.ValueInt * tab.Securiti.PriceStep;
                if (_newStopPrice > position.StopOrderPrice)
                {
                    tab.CloseAtStop(position, _newStopPrice, _newStopPrice);
                }
                _newProfitPrice = tab.PriceBestBid + pointsTP.ValueInt * tab.Securiti.PriceStep;
                if (_newProfitPrice > position.ProfitOrderPrice)
                {
                    tab.CloseAtProfit(position, _newProfitPrice, _newProfitPrice);
                }
                //                tab.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * tab.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * tab.Securiti.PriceStep);
            }
            if (position.Direction == Side.Sell)
            {
                _newStopPrice = tab.PriceBestBid + pointsSL.ValueInt * tab.Securiti.PriceStep;
                if (_newStopPrice < position.StopOrderPrice)
                    tab.CloseAtStop(position, _newStopPrice, _newStopPrice);
                _newProfitPrice = tab.PriceBestAsk - pointsTP.ValueInt * tab.Securiti.PriceStep;
                if (_newProfitPrice < position.ProfitOrderPrice)
                {
                    tab.CloseAtProfit(position, _newProfitPrice, _newProfitPrice);
                }
                //  tab.CloseAtTrailingStop(position, position.EntryPrice + pointsSL.ValueInt * tab.Securiti.PriceStep, position.EntryPrice + pointsSL.ValueInt * tab.Securiti.PriceStep);
            }
        }

        // Logics for enter position
        private void _tab_CandleFinishedEvent(List<Candle> candels)
        {
            if (isActive.ValueBool == false) { return; }


            List<Position> positions = _tabSimple1.PositionsOpenAll;

            if (positions.Count == 0)
            {

            }
            else
            {
                foreach (var position in positions)
                {
                    trallingPosition(position, _tabSimple1);
                }
            }
        }

        private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            if (!_tabSimple1.IsConnected)
            {
                return;
            }

        }

        private void _tab_NewTickEvent(Trade trade)
        {
            if (!_tabSimple1.IsConnected)
            {
                return;
            }
        }

    }
}


