using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.OnScriptIndicators
{
    public class TimeOfDayBot : BotPanel
    {
        public TimeOfDayBot(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new[] {"Off", "Buy", "Sell"});
            Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
            Slippage = CreateParameter("Slippage", 0, 0, 20m, 0.1m);
            TimeToInter = CreateParameterTimeOfDay("Time to Inter", 10, 0, 1, 0);
            Stop = CreateParameter("Stop", 1, 1.0m, 10, 0.1m);
            Profit = CreateParameter("Profit", 1, 1.0m, 10, 0.1m);

            _tab.NewTickEvent += TabOnNewTickEvent;
            _tab.PositionOpeningSuccesEvent += TabOnPositionOpeningSuccesEvent;
        }

        /// <summary>
        /// bot name
        /// взять уникальное имя
        /// </summary>
        public override string GetNameStrategyType()
        {
            return "TimeOfDayBot";
        }

        /// <summary>
        /// strategy name
        /// показать окно настроек
        /// </summary>
        public override void ShowIndividualSettingsDialog()
        {


        }

        /// <summary>
        /// trade tab
        /// вкладка для торговли
        /// </summary>
        private BotTabSimple _tab;

        //settings настройки публичные

        public StrategyParameterDecimal Slippage;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString Regime;

        public StrategyParameterTimeOfDay TimeToInter;

        public StrategyParameterDecimal Stop;

        public StrategyParameterDecimal Profit;

        // logic логика

        private void TabOnNewTickEvent(Trade trade)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions != null && openPositions.Count != 0)
            {
                return;
            }

            if (TimeToInter.Value < trade.Time)
            {
                LogicOpenPosition();
            }
        }

        private void LogicOpenPosition()
        {
            if (Regime.ValueString == "Buy")
            {
                _tab.BuyAtLimit(Volume.ValueDecimal,
                    _tab.PriceBestAsk + _tab.PriceBestAsk * (Slippage.ValueDecimal / 100));
            }
            if (Regime.ValueString == "Sell")
            {
                _tab.SellAtLimit(Volume.ValueDecimal,
                    _tab.PriceBestBid - _tab.PriceBestBid * (Slippage.ValueDecimal / 100));
            }

            Regime.ValueString = "Off";
        }

        private void TabOnPositionOpeningSuccesEvent(Position position)
        {
            decimal stopPrice = 0;
            decimal stopActivationPrice = 0;
            decimal profitPrice = 0;
            decimal profitActivationPrice = 0;

            if (position.Direction == Side.Buy)
            {
                stopPrice = position.EntryPrice - position.EntryPrice * (Stop.ValueDecimal / 100);
                stopActivationPrice = stopPrice - stopPrice * (Slippage.ValueDecimal / 100);
                profitPrice = position.EntryPrice + position.EntryPrice * (Profit.ValueDecimal / 100);
                profitActivationPrice = profitPrice - stopPrice * (Slippage.ValueDecimal / 100);
            }
            if (position.Direction == Side.Sell)
            {
                stopPrice = position.EntryPrice + position.EntryPrice * (Stop.ValueDecimal / 100);
                stopActivationPrice = stopPrice + stopPrice * (Slippage.ValueDecimal / 100);
                profitPrice = position.EntryPrice - position.EntryPrice * (Profit.ValueDecimal / 100);
                profitActivationPrice = profitPrice + stopPrice * (Slippage.ValueDecimal / 100);
            }

            _tab.CloseAtStop(position, stopActivationPrice, stopPrice);
            _tab.CloseAtProfit(position, profitActivationPrice, profitPrice);
        }
    }
}