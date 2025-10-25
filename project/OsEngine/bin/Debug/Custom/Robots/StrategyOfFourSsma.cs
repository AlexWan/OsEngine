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

Trend robot at the stratege  with two Ssma channels.

Buy:
1. The price is above the slow channel (above the upper line) and above the fast (upper line);
2. The bottom line of the fast channel is higher than the top line of the slow channel.

Sell:
1. The price is below the slow channel (below the lower line) and below the fast channel (below the lower line);
2. The upper line of the fast channel is lower than the lower line of the slow channel.

Exit: stop and profit.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyOfFourSsma")]// We create an attribute so that we don't write anything in the Boot factory
    public class StrategyOfFourSsma : BotPanel
    {
        private BotTabSimple _tab;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterTimeOfDay _simeStart;
        private StrategyParameterTimeOfDay _timeEnd;

        // GetVolume Settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator Settings        
        private StrategyParameterInt _periodSsmaChannelFast;
        private StrategyParameterInt _periodSsmaChannelSlow;
        
        // Indicator
        private Aindicator _ssmaUp1;
        private Aindicator _ssmaDown1;
        private Aindicator _ssmaUp2;
        private Aindicator _ssmaDown2;

        // Thee last value of the indicators
        private decimal _lastSsmaUp1;
        private decimal _lastSsmaDown1;
        private decimal _lastSsmaUp2;
        private decimal _lastSsmaDown2;

        // Exit settings
        private StrategyParameterDecimal _coefProfit;
        private StrategyParameterInt _stopCandles;

        public StrategyOfFourSsma(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic Settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" }, "Base");
            _slippage = CreateParameter("Slippage %", 0m, 0, 20, 1, "Base");
            _simeStart = CreateParameterTimeOfDay("Start Trade Time", 0, 0, 0, 0, "Base");
            _timeEnd = CreateParameterTimeOfDay("End Trade Time", 24, 0, 0, 0, "Base");

            // GetVolume Settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator Setting
            _periodSsmaChannelFast = CreateParameter("Ssma Channel Length Fast", 10, 50, 50, 400, "Indicator");
            _periodSsmaChannelSlow = CreateParameter("Ssma Channel Length Slow", 30, 50, 50, 400, "Indicator");

            // Creating an indicator SsmaUp1
            _ssmaUp1 = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "SsmaUp1", false);
            _ssmaUp1 = (Aindicator)_tab.CreateCandleIndicator(_ssmaUp1, "Prime");
            ((IndicatorParameterInt)_ssmaUp1.Parameters[0]).ValueInt = _periodSsmaChannelFast.ValueInt;
            ((IndicatorParameterString)_ssmaUp1.Parameters[1]).ValueString = "High";
            _ssmaUp1.ParametersDigit[0].Value = _periodSsmaChannelFast.ValueInt;
            _ssmaUp1.DataSeries[0].Color = Color.Yellow;
            _ssmaUp1.Save();

            // Creating an indicator SsmaDown1
            _ssmaDown1 = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "SsmaDown1", false);
            _ssmaDown1 = (Aindicator)_tab.CreateCandleIndicator(_ssmaDown1, "Prime");
            ((IndicatorParameterInt)_ssmaDown1.Parameters[0]).ValueInt = _periodSsmaChannelFast.ValueInt;
            ((IndicatorParameterString)_ssmaDown1.Parameters[1]).ValueString = "Low";
            _ssmaDown1.ParametersDigit[0].Value = _periodSsmaChannelFast.ValueInt;
            _ssmaDown1.DataSeries[0].Color = Color.Yellow;
            _ssmaDown1.Save();

            // Creating an indicator SsmaUp2
            _ssmaUp2 = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "SsmaUp2", false);
            _ssmaUp2 = (Aindicator)_tab.CreateCandleIndicator(_ssmaUp2, "Prime");
            ((IndicatorParameterInt)_ssmaUp2.Parameters[0]).ValueInt = _periodSsmaChannelSlow.ValueInt;
            ((IndicatorParameterString)_ssmaUp2.Parameters[1]).ValueString = "High";
            _ssmaUp2.ParametersDigit[0].Value = _periodSsmaChannelSlow.ValueInt;
            _ssmaUp2.DataSeries[0].Color = Color.AliceBlue;

            // Creating an indicator SsmaDown2
            _ssmaDown2 = IndicatorsFactory.CreateIndicatorByName("Ssma", name + "SsmaDown2", false);
            _ssmaDown2 = (Aindicator)_tab.CreateCandleIndicator(_ssmaDown2, "Prime");
            ((IndicatorParameterInt)_ssmaDown2.Parameters[0]).ValueInt = _periodSsmaChannelSlow.ValueInt;
            ((IndicatorParameterString)_ssmaDown2.Parameters[1]).ValueString = "Low";
            _ssmaDown2.ParametersDigit[0].Value = _periodSsmaChannelSlow.ValueInt;
            _ssmaDown2.DataSeries[0].Color = Color.AliceBlue;
            _ssmaDown2.Save();
            
            // Exit settings
            _coefProfit = CreateParameter("Coef Profit", 1, 1m, 10, 1, "Exit settings");
            _stopCandles = CreateParameter("Stop Candles", 1, 2, 10, 1, "Exit settings");

            // Subscribe to the indicator update event
            ParametrsChangeByUser += IntersectionOfFourSsma_ParametrsChangeByUser;

            // subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel260;
        }
       
        // Indicator Update event
        private void IntersectionOfFourSsma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ssmaUp1.Parameters[0]).ValueInt = _periodSsmaChannelFast.ValueInt;
            _ssmaUp1.Save();
            _ssmaUp1.Reload();
            ((IndicatorParameterInt)_ssmaDown1.Parameters[0]).ValueInt = _periodSsmaChannelFast.ValueInt;
            _ssmaDown1.Save();
            _ssmaDown1.Reload();
            ((IndicatorParameterInt)_ssmaUp2.Parameters[0]).ValueInt = _periodSsmaChannelSlow.ValueInt;
            _ssmaUp2.Save();
            _ssmaUp2.Reload();
            ((IndicatorParameterInt)_ssmaDown2.Parameters[0]).ValueInt = _periodSsmaChannelSlow.ValueInt;
            _ssmaDown2.Save();
            _ssmaDown2.Reload();
        }

        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "StrategyOfFourSsma";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            // If there are not enough candles to build an indicator, we exit
            if (candles.Count < _periodSsmaChannelFast.ValueInt || candles.Count < _periodSsmaChannelSlow.ValueInt)
            {
                return;
            }

            // If the time does not match, we leave
            if (_simeStart.Value > _tab.TimeServerCurrent ||
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

            _lastSsmaUp1 = _ssmaUp1.DataSeries[0].Last;
            _lastSsmaDown1 = _ssmaDown1.DataSeries[0].Last;
            _lastSsmaUp2 = _ssmaUp2.DataSeries[0].Last;
            _lastSsmaDown2 = _ssmaDown2.DataSeries[0].Last;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal slippage = _slippage.ValueDecimal * _tab.Securiti.PriceStep;
                decimal lastPrice = candles[candles.Count - 1].Close;
               
                // Long
                if (_regime.ValueString != "OnlyShort") // if the mode is not only short, then we enter long
                {

                    if (lastPrice > _lastSsmaUp1 && lastPrice > _lastSsmaUp2
                        && _lastSsmaDown1 > _lastSsmaUp2)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + slippage);
                    }
                }
                
                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {

                    if (lastPrice < _lastSsmaDown1 && lastPrice < _lastSsmaDown2
                        && _lastSsmaUp1 < _lastSsmaDown2)
                    {
                        _tab.SellAtLimit(GetVolume(_tab), _tab.PriceBestBid - slippage);
                    }
                }
            }
        }

        //  Logic close position
        private void LogicClosePosition(List<Candle> candles)
        {
            List<Position> openPositions = _tab.PositionsOpenAll;
            
            decimal _slippage = this._slippage.ValueDecimal * _tab.Securiti.PriceStep;

            decimal profitActivation;
            decimal price;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                Position pos = openPositions[i];

                if (pos.State != PositionStateType.Open)
                {
                    continue;
                }

                if (pos.Direction == Side.Buy) // If the direction of the position is purchase
                {
                    decimal stopActivation = GetPriceStop(pos.TimeCreate, Side.Buy, candles, candles.Count - 1);

                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice + (pos.EntryPrice - price) * _coefProfit.ValueDecimal;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation + _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation - _slippage);
                }
                else // If the direction of the position is sale
                {
                    decimal stopActivation = GetPriceStop(pos.TimeCreate, Side.Sell, candles, candles.Count - 1);
                    
                    if (stopActivation == 0)
                    {
                        return;
                    }

                    price = stopActivation;
                    profitActivation = pos.EntryPrice - (price - pos.EntryPrice) * _coefProfit.ValueDecimal;

                    _tab.CloseAtProfit(pos, profitActivation, profitActivation - _slippage);
                    _tab.CloseAtStop(pos, stopActivation, stopActivation + _slippage);
                }
            }
        }
        
        private decimal GetPriceStop(DateTime positionCreateTime, Side side, List<Candle> candles, int index)
        {
            if (candles == null || index < _stopCandles.ValueInt)
            {
                return 0;
            }

            if (side == Side.Buy)
            {
                // We calculate the stop price at Long
                // We find the minimum for the time from the opening of the transaction to the current one
                decimal price = decimal.MaxValue; ;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 2].TimeStart;
                }

                for (int i = index; i > 0; i--)
                {
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - _stopCandles.ValueInt; i--)
                { 
                    // Looking at the minimum after opening
                    if (candles[i].Low < price)
                    {
                        price = candles[i].Low;
                    }
                }

                return price;
            }

            if (side == Side.Sell)
            {
                //  We find the maximum for the time from the opening of the transaction to the current one
                decimal price = 0;
                int indexIntro = 0;
                DateTime openPositionTime = positionCreateTime;

                if (openPositionTime == DateTime.MinValue)
                {
                    openPositionTime = candles[index - 1].TimeStart;
                }

                for (int i = index; i > 0; i--)
                { 
                    // Look at the index of the candle, after which the opening of the pose occurred
                    if (candles[i].TimeStart <= openPositionTime)
                    {
                        indexIntro = i;
                        break;
                    }
                }

                for (int i = indexIntro; i > 0 && i > indexIntro - _stopCandles.ValueInt; i--)
                {
                    // Looking at the maximum high
                    if (candles[i].High > price)
                    {
                        price = candles[i].High;
                    }
                }

                return price;
            }

            return 0;
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