using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels.Attributes;

namespace OsEngine.Robots.HomeWork
{
    [Bot("HomeWork3_ParamBot")]
    public class HomeWork3_ParamBot : BotPanel
    {        
        private BotTabSimple _tab;

        private StrategyParameterBool IsOnParam;
        private StrategyParameterDecimal VolumeParam;
        private StrategyParameterDecimal ProfitParam;
        private StrategyParameterDecimal StopParam;

        public HomeWork3_ParamBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            IsOnParam = CreateParameter("Is On", false);
            VolumeParam = CreateParameter("Volume", 1.0m, 1.0m, 10m, 1.0m);
            ProfitParam = CreateParameter("Point of TakeProfit", 1000m, 100m, 10000m, 100m);
            StopParam = CreateParameter("Point of StopLoss", 500m, 100m, 10000m, 100m);

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
        }

        public override string GetNameStrategyType()
        {
            return "HomeWork3_ParamBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (IsOnParam.ValueBool == false)
            {
                return;
            }

            if (candles.Count < 6)
            {
                return;
            }

            List<Position> position = _tab.PositionsOpenAll;

            if (position.Count > 0)
            {
                return;
            }

            Candle lastCandle = candles[candles.Count - 1];
            Candle candleMinusOne = candles[candles.Count - 2];
            Candle candleMinusfive = candles[candles.Count - 6];

            if (lastCandle.IsDown && 
                candleMinusOne.IsUp && 
                lastCandle.Body > candleMinusOne.Body * 3 &&
                candleMinusfive.Low < lastCandle.Low)
            {
                _tab.SellAtMarket(VolumeParam.ValueDecimal);
            }
        }

        private void _tab_PositionOpeningSuccesEvent(Position position)
        {
            _tab.CloseAtStopMarket(position, position.EntryPrice + StopParam.ValueDecimal);
            _tab.CloseAtProfitMarket(position, position.EntryPrice - ProfitParam.ValueDecimal);
        }
    }
}

