/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;
using System;

namespace OsEngine.Robots.OnScriptIndicators
{
    [Bot("GridSample")]
    public class GridSample : BotPanel
    {
        public GridSample(string name, StartProgram startProgram)
           : base(name, startProgram)
        {
            //create a tab for trading
            //создаём вкладку для торговли
            TabCreate(BotTabType.Simple);

            Regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" });

            DownCandlesCountToOpenPos = CreateParameter("Candles Count To Open Pos", 5, 5, 50, 1);

            OpenPosAverCount = CreateParameter("Open Averge Count", 3, 3, 50, 1);

            ClosePosAverCount = CreateParameter("Close Averge Count", 3, 3, 50, 1);

            AverStepUp = CreateParameter("Up Average Step Persent", 0.1m, 1, 100000, 1m);

            AverStepDown = CreateParameter("Down Average Step Persent", 0.1m, 1, 100000, 1m);

            FirstVolume = CreateParameter("First Volume", 1, 1, 100000, 1m);

            NextVolume = CreateParameter("Next Volume", 3, 1, 100000, 1m);

            StopPercent = CreateParameter("Stop Percent", 0.3m, 0.3m, 100000, 1m);

            ProfitPercent = CreateParameter("Profit Percent", 0.3m, 0.3m, 100000, 1m);

            _tab = TabsSimple[0];

            _tab.CandleUpdateEvent += _tab_CandleUpdateEvent;

            Description = "Logic of the first Buy: " +
                "if there are N candles falling down, we place the first order according to the close of the last candle. " +
                "Next: buy if the price is greater than entryUpPrice or less than entryDownPrice. " +
                "Exit: by stop";
        }

        BotTabSimple _tab;

        public override string GetNameStrategyType()
        {
            return "GridSample";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        public StrategyParameterString Regime;

        public StrategyParameterInt DownCandlesCountToOpenPos;

        public StrategyParameterInt OpenPosAverCount;

        public StrategyParameterInt ClosePosAverCount;

        public StrategyParameterDecimal AverStepUp;

        public StrategyParameterDecimal AverStepDown;

        public StrategyParameterDecimal FirstVolume;

        public StrategyParameterDecimal NextVolume;

        public StrategyParameterDecimal StopPercent;

        public StrategyParameterDecimal ProfitPercent;

      // logic

        private void _tab_CandleUpdateEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (DownCandlesCountToOpenPos.ValueInt >= candles.Count + 1)
            {
                return;
            }

            List<Position> openPos = _tab.PositionsOpenAll;

            if(openPos.Count == 0)
            {// если позиций нет, пробуем открывать позицию
                TryByAtFirstTime(candles);
                return;
            }

            Position pos = openPos[0];

            if(pos.CloseActiv == true ||
                pos.OpenActiv == true)
            { // если какой-то ордер на открытие или закрытие активен
                return;
            }

            if(pos.Comment == "TradeFaze")
            {
                // здесь мы пирамидимся и усредняемся на входе
                TradeFaze(pos, candles);
                // проверка стопов и профитов
                CheckStopAndProfit(pos, candles);
            }
            else if(pos.Comment == "CloseFaze")
            { // здесь, после достижения стопа или профита - закрываемся частями
                CloseFaze(pos, candles);
            }
        }

        private int _lastCandlesWatch;

        private void TryByAtFirstTime(List<Candle> candles)
        {
            if (_lastCandlesWatch == candles.Count)
            {
                return;
            }

            _lastCandlesWatch = candles.Count;

            // логика первого входа такова:
            // если накопилось N свечек падающих вниз - выставляем первый ордер по клоузу последней свечи

            for (int i = candles.Count - 2; i > -1 && i > candles.Count - 2 - DownCandlesCountToOpenPos.ValueInt; i--)
            {
                if (candles[i].IsUp)
                {
                    return;
                }
            }

            // N свечек нет растущих свечей
            // открываемся

            Position pos = _tab.BuyAtLimit(FirstVolume.ValueDecimal, _tab.PriceBestAsk);

            if(pos != null)
            {
                pos.Comment = "TradeFaze";
            }

        }

        private void TradeFaze(Position pos, List<Candle> candles)
        {
            int countAver = CountExecuteOrders(pos.OpenOrders);

            if(countAver >= OpenPosAverCount.ValueInt)
            {
                return;
            }

            decimal lastEntryOrderPrice = pos.OpenOrders[pos.OpenOrders.Count - 1].Price;
            decimal entryUpPrice = lastEntryOrderPrice + (lastEntryOrderPrice * AverStepUp.ValueDecimal / 100);
            decimal entryDownPrice = lastEntryOrderPrice - (lastEntryOrderPrice * AverStepDown.ValueDecimal / 100);
            decimal lastPrice = candles[candles.Count - 1].Close;

            if (lastPrice > entryUpPrice)
            {
                _tab.BuyAtLimitToPosition(pos, _tab.PriceBestAsk, NextVolume.ValueDecimal);
            }

            if(lastPrice < entryDownPrice)
            {
                _tab.BuyAtLimitToPosition(pos, _tab.PriceBestAsk, NextVolume.ValueDecimal);
            }
        }

        private void CheckStopAndProfit(Position pos, List<Candle> candles)
        {
            decimal midleEntryPrice = pos.EntryPrice;
            decimal stopPrice = midleEntryPrice - (midleEntryPrice * StopPercent.ValueDecimal / 100);
            decimal profitPrice = midleEntryPrice + (midleEntryPrice * ProfitPercent.ValueDecimal / 100);

            pos.StopOrderPrice = stopPrice;
            pos.ProfitOrderPrice = profitPrice;

            decimal lastPrice = candles[candles.Count - 1].Close;

            if(lastPrice <= pos.StopOrderPrice)
            {
                pos.Comment = "CloseFaze";
            }

            if (lastPrice >= pos.ProfitOrderPrice)
            {
                pos.Comment = "CloseFaze";
            }
        }

        private int CountExecuteOrders(List<Order> orders)
        {
            if(orders == null ||
                orders.Count == 0)
            {
                return 0;
            }

            int result = 0;

            for(int i = 0;i < orders.Count;i++)
            {
                if (orders[i].MyTrades != null &&
                    orders[i].MyTrades.Count != 0)
                {
                    result++;
                }
            }

            return result;
        }

        DateTime _lastCandleCloseTime;

        private void CloseFaze(Position pos, List<Candle> candles)
        {
            if(_lastCandleCloseTime == candles[candles.Count-1].TimeStart)
            {
                return;
            }

            _lastCandleCloseTime = candles[candles.Count - 1].TimeStart;

            // берём объём для закрытия в одной части
            decimal closeVolume = Math.Round(pos.MaxVolume / ClosePosAverCount.ValueInt, _tab.Securiti.DecimalsVolume);

            if(closeVolume * 2 > pos.OpenVolume)
            { // приближаемся к завершению
                closeVolume = pos.OpenVolume;
            }

            if(closeVolume <= 0)
            {// что-то не то наобрезали
                closeVolume = pos.OpenVolume;
            }

            // закрываем
            _tab.CloseAtLimit(pos, _tab.PriceBestBid, closeVolume);
        }

    }
}
