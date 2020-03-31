/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Robots.High_Frequency
{
    /// <summary>
    /// robot analyzing the density of the market depth / 
    /// робот анализирующий плотность стакана
    /// </summary>
    public class HighFrequencyTrader : BotPanel
    {

        public HighFrequencyTrader(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });
            Volume = CreateParameter("Volume", 1, 1.0m, 100, 2);
            Stop = CreateParameter("Stop", 5, 5, 15, 1);
            Profit = CreateParameter("Profit", 5, 5, 20, 1);

            MaxLevelsInMarketDepth = CreateParameter("MaxLevelsInMarketDepth", 5, 3, 15, 1);

            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;

            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
            _tab.PositionClosingFailEvent += _tab_PositionClosingFailEvent;

            Task task = new Task(ClosePositionThreadArea);
            task.Start();

        }

        /// <summary>
        /// tab to trade
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        /// <summary>
        /// regime
        /// режим работы
        /// </summary>
        public StrategyParameterString Regime;

        /// <summary>
        /// volume
        /// объем
        /// </summary>
        public StrategyParameterDecimal Volume;

        /// <summary>
        /// levels to marketDepth analize
        /// глубина анализа стакана
        /// </summary>
        public StrategyParameterInt MaxLevelsInMarketDepth;

        /// <summary>
        /// stop order length
        /// длинна стопа
        /// </summary>
        public StrategyParameterInt Stop;

        /// <summary>
        /// profit order length
        /// длинна профита
        /// </summary>
        public StrategyParameterInt Profit;

        public override string GetNameStrategyType()
        {
            return "HighFrequencyTrader";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic / логики

        /// <summary>
        /// last time check marketDepth
        /// последнее время проверки стакана
        /// </summary>
        private DateTime _lastCheckTime = DateTime.MinValue;

        /// <summary>
        /// new marketDepth event
        /// новый входящий стакан
        /// </summary>
        void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }
            if (marketDepth.Asks == null || marketDepth.Asks.Count == 0 ||
                marketDepth.Bids == null || marketDepth.Bids.Count == 0)
            {
                return;
            }

            if (_tab.PositionsOpenAll.Find(pos => pos.State == PositionStateType.Open ||
                pos.State == PositionStateType.Closing
                ) != null)
            {
                return;
            }

            if (StartProgram == StartProgram.IsOsTrader &&
                _lastCheckTime.AddSeconds(1) > DateTime.Now)
            { // in real trade, check marketDepth once at second
              // в реальной торговле, проверяем стакан раз в секунду
                return;
            }

            _lastCheckTime = DateTime.Now;

            Position positionBuy = _tab.PositionsOpenAll.Find(pos => pos.Direction == Side.Buy);
            Position positionSell = _tab.PositionsOpenAll.Find(pos => pos.Direction == Side.Sell);

            // buy / покупка

            decimal buyPrice = 0;
            int lastVolume = 0;

            for (int i = 0; i < marketDepth.Bids.Count && i < MaxLevelsInMarketDepth.ValueInt; i++)
            {
                if (marketDepth.Bids[i].Bid > lastVolume)
                {
                    buyPrice = marketDepth.Bids[i].Price + _tab.Securiti.PriceStep;
                    lastVolume = Convert.ToInt32(marketDepth.Bids[i].Bid);
                }
            }

            if (positionBuy != null &&
                positionBuy.OpenOrders[0].Price != buyPrice &&
                positionBuy.State != PositionStateType.Open &&
                positionBuy.State != PositionStateType.Closing)
            {
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    _positionsToClose.Add(positionBuy);
                }
                else
                {
                    _tab.CloseAllOrderToPosition(positionBuy);
                }
                _tab.BuyAtLimit(Volume.ValueDecimal, buyPrice);
            }
            if (positionBuy == null)
            {
                _tab.BuyAtLimit(Volume.ValueDecimal, buyPrice);
            }

            // sell продажа

            decimal sellPrice = 0;
            int lastVolumeInAsk = 0;

            for (int i = 0; i < marketDepth.Asks.Count && i < MaxLevelsInMarketDepth.ValueInt; i++)
            {
                if (marketDepth.Asks[i].Ask > lastVolumeInAsk)
                {
                    sellPrice = marketDepth.Asks[i].Price - _tab.Securiti.PriceStep;
                    lastVolumeInAsk = Convert.ToInt32(marketDepth.Asks[i].Ask);
                }
            }

            if (positionSell != null &&
                positionSell.OpenOrders[0].Price != sellPrice &&
                positionSell.State != PositionStateType.Open &&
                positionSell.State != PositionStateType.Closing)
            {
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    _positionsToClose.Add(positionSell);
                }
                else
                {
                    _tab.CloseAllOrderToPosition(positionSell);
                }

                _tab.SellAtLimit(Volume.ValueDecimal, sellPrice);
            }
            if (positionSell == null)
            {
                _tab.SellAtLimit(Volume.ValueDecimal, sellPrice);
            }
        }

        /// <summary>
        /// successful position opening
        /// успешное открытие позиции
        /// </summary>
        void _tab_PositionOpeningSuccesEvent(Position position)
        {
            if (position.Direction == Side.Buy)
            {
                _tab.CloseAtStop(position, position.EntryPrice - Stop.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice - Stop.ValueInt * _tab.Securiti.PriceStep);
                _tab.CloseAtProfit(position, position.EntryPrice + Profit.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice + Profit.ValueInt * _tab.Securiti.PriceStep);
            }
            if (position.Direction == Side.Sell)
            {
                _tab.CloseAtStop(position, position.EntryPrice + Stop.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice + Stop.ValueInt * _tab.Securiti.PriceStep);
                _tab.CloseAtProfit(position, position.EntryPrice - Profit.ValueInt * _tab.Securiti.PriceStep, position.EntryPrice - Profit.ValueInt * _tab.Securiti.PriceStep);
            }

            List<Position> positions = _tab.PositionsOpenAll;

            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].Number == position.Number)
                {
                    continue;
                }
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    _positionsToClose.Add(positions[i]);
                }
                else
                {
                    _tab.CloseAllOrderToPosition(positions[i]);
                }
            }
        }

        /// <summary>
        /// the position is not closed and warrants are withdrawn from it
        /// позиция не закрылась и у неё отозваны ордера
        /// </summary>
        void _tab_PositionClosingFailEvent(Position position)
        {
            if (position.CloseActiv)
            {
                return;
            }
            _tab.CloseAtMarket(position, position.OpenVolume);
        }

        // отзыв заявок в реальном подключении
        // withdrawal orders in real connection

        /// <summary>
        /// positions to be recalled
        /// позиции которые нужно отозвать
        /// </summary>
        List<Position> _positionsToClose = new List<Position>();

        /// <summary>
        /// place of work where orders are recalled in a real connection
        /// место работы потока где отзываются заявки в реальном подключении
        /// </summary>
        private async void ClosePositionThreadArea()
        {
            while (true)
            {
                await Task.Delay(1000);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                for (int i = 0; i < _positionsToClose.Count; i++)
                {
                    if (_positionsToClose[i].State != PositionStateType.Opening)
                    {
                        continue;
                    }

                    if (_positionsToClose[i].OpenOrders != null &&
                        !string.IsNullOrWhiteSpace(_positionsToClose[i].OpenOrders[0].NumberMarket))
                    {
                        _tab.CloseAllOrderToPosition(_positionsToClose[i]);
                        _positionsToClose.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
    }
}
