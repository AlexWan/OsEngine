/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Language;

/*Discription
Trading robot for osengine

Trend robot at the intersection of two exponential averages

Buy: the fast Ema is higher than the slow Ema and the value of the last candle is greater than the fast Ema.

Sell: The fast Ema is lower than the slow Ema and the value of the last candle is less than the slow Ema.

Exit: stop and profit in % of the entry price.
*/

namespace OsEngine.Robots
{ 
    [Bot("IntersectionOfTwoEma")] //We create an attribute so that we don't write anything in the Boot factory
    public class IntersectionOfTwoEma : BotPanel
    {
        private BotTabSimple _tab;
       
        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _timeStart;
        private StrategyParameterTimeOfDay _timeEnd;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Settings
        private StrategyParameterInt _periodEmaFast;                       
        private StrategyParameterInt _periodEmaSlow;
        
        // Indicator
        private Aindicator _ema1;
        private Aindicator _ema2;
       
        // The last value of the indicators
        private decimal _lastEmaFast;
        private decimal _lastEmaSlow;

        // Exit Settings
        private StrategyParameterDecimal _stopValue;
        private StrategyParameterDecimal _profitValue;

        public IntersectionOfTwoEma(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
           
            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _timeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Settings
            _periodEmaFast = CreateParameter("fast EMA1 period", 250, 50, 500, 50, "Indicator");
            _periodEmaSlow = CreateParameter("slow EMA2 period", 400, 500, 1500, 100, "Indicator");

            // Creating indicator Ema1
            _ema1 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA1", false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            //Creating indicator Ema2
            _ema2 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EMA2", false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, "Prime");
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema2.DataSeries[0].Color = Color.Green;
            _ema2.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfTwoEma_ParametrsChangeByUser;
           
            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Exit Settings
            _stopValue = CreateParameter("Stop percent", 0.5m, 1, 10, 1, "Exit settings");
            _profitValue = CreateParameter("Profit percent", 0.5m, 1, 10, 1, "Exit settings");

            Description = OsLocalization.Description.DescriptionLabel209;
        }

        // Indicator Update event
        private void IntersectionOfTwoEma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEmaFast.ValueInt;
            _ema1.Save();
            _ema1.Reload();
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEmaSlow.ValueInt;
            _ema2.Save();
            _ema2.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "IntersectionOfTwoEma";
        }
        public override void ShowIndividualSettingsDialog()
        {

        }
       
        // Candle Finished Event
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            // If the robot is turned off, exit the event handler
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count <= _periodEmaFast.ValueInt || candles.Count <= _periodEmaSlow.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_timeStart.Value > _tab.TimeServerCurrent ||
                _timeEnd.Value < _tab.TimeServerCurrent)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            // If there are positions, then go to the position closing method
            if (openPositions != null && openPositions.Count != 0)
            {
                LogicClosePosition(candles);
            }

            // If the position closing mode, then exit the method
            if (_regime.ValueString == "OnlyClosePosition")
            {
                return;
            }

            // If there are no positions, then go to the position opening method
            if (openPositions == null || openPositions.Count == 0)
            {
                LogicOpenPosition(candles);
            }
        }

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            if (openPositions == null || openPositions.Count == 0)
            {               
                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                   // The last value of the indicators
                    _lastEmaFast = _ema1.DataSeries[0].Last;
                    _lastEmaSlow = _ema2.DataSeries[0].Last;

                    if (_lastEmaFast > _lastEmaSlow && lastPrice > _lastEmaFast)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + _slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    if (_lastEmaFast < _lastEmaSlow && lastPrice < _lastEmaFast)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - _slippage);
                    }
                }
            }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is long
                {
                    decimal profitActivation = pos.EntryPrice + pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice - pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is short
                {
                    decimal profitActivation = pos.EntryPrice - pos.EntryPrice * _profitValue.ValueDecimal / 100;
                    decimal stopActivation = pos.EntryPrice + pos.EntryPrice * _stopValue.ValueDecimal / 100;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
                }
            }
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                       && tab.Security.PriceStep != tab.Security.PriceStepCost
                       && tab.PriceBestAsk != 0
                       && tab.Security.PriceStep != 0
                       && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
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