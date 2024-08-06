/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;


namespace OsEngine.Robots.Trend
{
    [Bot("TwoTimeFramesBot")]
    public class TwoTimeFramesBot : BotPanel
    {
        private BotTabSimple _tabToTrade;
        private BotTabSimple _tabBigTf;

        private Aindicator _pc;
        private Aindicator _sma;

        public StrategyParameterString Regime;
        public StrategyParameterDecimal Volume;
        public StrategyParameterInt PcLength;
        public StrategyParameterInt SmaLength;

        public TwoTimeFramesBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            Volume = CreateParameter("Volume", 3, 1.0m, 50, 4);
            PcLength = CreateParameter("PC length", 20, 5, 50, 1);
            SmaLength = CreateParameter("Sma length", 30, 0, 50, 1);

            TabCreate(BotTabType.Simple);
            TabCreate(BotTabType.Simple);

            _tabToTrade = TabsSimple[0];
            _tabBigTf = TabsSimple[1];

            _tabToTrade.CandleFinishedEvent += _tabToTrade_CandleFinishedEvent;

            _pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PriceChannel", false);
            _pc = (Aindicator)_tabToTrade.CreateCandleIndicator(_pc, "Prime");
            _pc.ParametersDigit[0].Value = PcLength.ValueInt;
            _pc.ParametersDigit[1].Value = PcLength.ValueInt;

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "sma", false);
            _sma = (Aindicator)_tabBigTf.CreateCandleIndicator(_sma, "Prime");
            _sma.ParametersDigit[0].Value = SmaLength.ValueInt;

            ParametrsChangeByUser += TwoTimeFramesBot_ParametrsChangeByUser;
        }

        private void TwoTimeFramesBot_ParametrsChangeByUser()
        {
            if(_pc.ParametersDigit[0].Value != PcLength.ValueInt)
            {
                _pc.ParametersDigit[0].Value = PcLength.ValueInt;
                _pc.ParametersDigit[1].Value = PcLength.ValueInt;
                _pc.Reload();
                _pc.Save();
            }

            if(_sma.ParametersDigit[0].Value != SmaLength.ValueInt)
            {
                _sma.ParametersDigit[0].Value = SmaLength.ValueInt;
                _sma.Reload();
                _sma.Save();
            }
        }

        public override string GetNameStrategyType()
        {
            return "TwoTimeFramesBot";
        }

        public override void ShowIndividualSettingsDialog()
        {
            
        }

        // logic

        private void _tabToTrade_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if(_tabBigTf.CandlesAll == null
                || _tabBigTf.CandlesAll.Count < 5 
                || candles.Count < 5)
            {
                return;
            }

            decimal lastPriceOnTradeTab = candles[candles.Count - 1].Close;
            decimal lastPcUp = _pc.DataSeries[0].Values[_pc.DataSeries[0].Values.Count - 2];
            decimal lastPcDown = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 2];

            decimal lastPriceOnBigTfTab = _tabBigTf.CandlesAll[_tabBigTf.CandlesAll.Count - 1].Close;
            decimal lastSmaOnBigTfTab = _sma.DataSeries[0].Last;

            if(lastPriceOnTradeTab == 0 
                || lastPcUp == 0 
                || lastPriceOnBigTfTab == 0 
                || lastSmaOnBigTfTab == 0)
            { // data is note ready
                return;
            }

            List<Position> openPositions = _tabToTrade.PositionsOpenAll;

            if (openPositions == null 
                || openPositions.Count == 0)
            { // Open logic
                if(lastPriceOnTradeTab > lastPcUp 
                    && lastPriceOnBigTfTab > lastSmaOnBigTfTab)
                {
                    _tabToTrade.BuyAtMarket(Volume.ValueDecimal);
                }
            }
            else
            {
                Position openPos = openPositions[0];

                if(openPos.State != PositionStateType.Open)
                {
                    return;
                }

                if(lastPriceOnTradeTab < lastPcDown)
                {
                    _tabToTrade.CloseAtMarket(openPos, openPos.OpenVolume);
                }
            }
        }
    }
}