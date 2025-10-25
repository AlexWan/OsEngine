/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OsEngine.Language;

/*Discription
Trading robot for osengine.

Trend strategy on 4 EMAS and a channel of two EMAS (any slips and different output).

The channel consists of two Emas of the same length with a closing price of high and loy.

Buy:
1. Ema 1 is growing (i.e. the value of 2 candles ago was lower than 1 candle ago);
2. Ema2 is higher than Ema3;
3. The price is above Ema4 and above the upper line of the Ema channel.

Sell:
1. Ema1 falling (i.e. the value of 2 candles ago was higher than 1 candle ago);
2. Ema2 is lower than Ema3;
3. The price is below Ema4 and below the lower line of the Ema channel.

Exit from buy: The price is lower than Ema4.
Exit from sell: The price is higher than Ema4.
*/

namespace OsEngine.Robots
{
    [Bot("StrategyOfFourEma")]//We create an attribute so that we don't write anything in the Boot factory
    public class StrategyOfFourEma : BotPanel
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

        // Indicator
        private Aindicator _ema1;
        private Aindicator _ema2;
        private Aindicator _ema3;
        private Aindicator _ema4;
        private Aindicator _emaUp;
        private Aindicator _emaDown;

        // Indicator Settings 
        private StrategyParameterInt _periodEma1;
        private StrategyParameterInt _periodEma2;
        private StrategyParameterInt _periodEma3;
        private StrategyParameterInt _periodEma4;
        private StrategyParameterInt _periodEmaChannel;

        // Thee last value of the indicators
        private decimal _lastEma2;
        private decimal _lastEma3;
        private decimal _lastEma4;
        private decimal _lastEmaUp;
        private decimal _lastEmaDown;

        public StrategyOfFourEma(string name, StartProgram startProgram) : base(name, startProgram)
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
            _periodEma1 = CreateParameter(" EMA1 period", 100, 10, 300, 1, "Indicator");
            _periodEma2 = CreateParameter(" EMA2 period", 200, 10, 300, 1, "Indicator");
            _periodEma3 = CreateParameter(" EMA3 period", 300, 10, 300, 1, "Indicator");
            _periodEma4 = CreateParameter(" EMA4 period", 400, 10, 300, 1, "Indicator");
            _periodEmaChannel = CreateParameter("EMA Channel Length", 10, 50, 50, 400, "Indicator");

            // Creating indicator Ema1
            _ema1 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema1", false);
            _ema1 = (Aindicator)_tab.CreateCandleIndicator(_ema1, "Prime");
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEma1.ValueInt;
            _ema1.DataSeries[0].Color = Color.Red;
            _ema1.Save();

            // Creating indicator Ema2
            _ema2 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema2", false);
            _ema2 = (Aindicator)_tab.CreateCandleIndicator(_ema2, "Prime");
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEma2.ValueInt;
            _ema2.DataSeries[0].Color = Color.Blue;
            _ema2.Save();

            // Creating indicator Ema3
            _ema3 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema3", false);
            _ema3 = (Aindicator)_tab.CreateCandleIndicator(_ema3, "Prime");
            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _periodEma3.ValueInt;
            _ema3.DataSeries[0].Color = Color.Green;
            _ema3.Save();

            // Creating indicator Ema4
            _ema4 = IndicatorsFactory.CreateIndicatorByName("Ema", name + "Ema4", false);
            _ema4 = (Aindicator)_tab.CreateCandleIndicator(_ema4, "Prime");
            ((IndicatorParameterInt)_ema4.Parameters[0]).ValueInt = _periodEma4.ValueInt;
            _ema4.DataSeries[0].Color = Color.Aqua;
            _ema4.Save();

            // Creating indicator EmaUp
            _emaUp = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaUp", false);
            _emaUp = (Aindicator)_tab.CreateCandleIndicator(_emaUp, "Prime");
            ((IndicatorParameterInt)_emaUp.Parameters[0]).ValueInt = _periodEmaChannel.ValueInt;
            ((IndicatorParameterString)_emaUp.Parameters[1]).ValueString = "High";
            _emaUp.DataSeries[0].Color = Color.BlueViolet;
            _emaUp.Save();

            // Creating indicator EmaDown
            _emaDown = IndicatorsFactory.CreateIndicatorByName("Ema", name + "EmaDown", false);
            _emaDown = (Aindicator)_tab.CreateCandleIndicator(_emaDown, "Prime");
            ((IndicatorParameterInt)_emaDown.Parameters[0]).ValueInt = _periodEmaChannel.ValueInt;
            ((IndicatorParameterString)_emaDown.Parameters[1]).ValueString = "Low";
            _emaDown.DataSeries[0].Color = Color.Bisque;
            _emaDown.Save();

            // Subscribe to the indicator update event           
            ParametrsChangeByUser += IntersectionOfFourEma_ParametrsChangeByUser;

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel259;
        }
        // Indicator Update event
        private void IntersectionOfFourEma_ParametrsChangeByUser()
        {
            ((IndicatorParameterInt)_ema1.Parameters[0]).ValueInt = _periodEma1.ValueInt;
            _ema1.Save();
            _ema1.Reload();
            ((IndicatorParameterInt)_ema2.Parameters[0]).ValueInt = _periodEma2.ValueInt;
            _ema2.Save();
            _ema2.Reload();
            ((IndicatorParameterInt)_ema3.Parameters[0]).ValueInt = _periodEma3.ValueInt;
            _ema3.Save();
            _ema3.Reload();
            ((IndicatorParameterInt)_ema4.Parameters[0]).ValueInt = _periodEma4.ValueInt;
            _ema4.Save();
            _ema4.Reload();
            ((IndicatorParameterInt)_emaUp.Parameters[0]).ValueInt = _periodEmaChannel.ValueInt;
            _emaUp.Save();
            _emaUp.Reload();
            ((IndicatorParameterInt)_emaDown.Parameters[0]).ValueInt = _periodEmaChannel.ValueInt;
            _emaDown.Save();
            _emaDown.Reload();
        }

        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "StrategyOfFourEma";
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
            if (candles.Count < _periodEma1.ValueInt || candles.Count < _periodEma2.ValueInt ||
                candles.Count < _periodEma3.ValueInt || candles.Count < _periodEma4.ValueInt ||
                candles.Count < _periodEmaChannel.ValueInt)
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

            _lastEma2 = _ema2.DataSeries[0].Last;
            _lastEma3 = _ema3.DataSeries[0].Last;
            _lastEma4 = _ema4.DataSeries[0].Last;
            _lastEmaUp = _emaUp.DataSeries[0].Last;
            _lastEmaDown = _emaDown.DataSeries[0].Last;
            var smaValues = _ema1.DataSeries[0].Values;

            if (openPositions == null || openPositions.Count == 0)
            {
                decimal slippage = _slippage.ValueDecimal * _tab.Securiti.PriceStep;

                // Long
                if (_regime.ValueString != "OnlyShort") // If the mode is not only short, then we enter long
                {
                    //bool lastEma1Up = smaValues.Last() > smaValues[smaValues.Count - 2];

                    if (smaValues.Last() > smaValues[smaValues.Count - 2] && _lastEma2 > _lastEma3 && lastPrice > _lastEma4 && lastPrice > _lastEmaUp)
                    {
                        _tab.BuyAtLimit(GetVolume(_tab), _tab.PriceBestAsk + slippage);
                    }
                }

                // Short
                if (_regime.ValueString != "OnlyLong") // If the mode is not only long, then we enter short
                {
                    //bool lastEma1Down = smaValues.Last() < smaValues[smaValues.Count - 2];

                    if (smaValues.Last() < smaValues[smaValues.Count - 2] && _lastEma2 < _lastEma3 && lastPrice < _lastEma4 && lastPrice < _lastEmaDown)
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
            decimal lastPrice = candles[candles.Count - 1].Close;

            _lastEma4 = _ema4.DataSeries[0].Last;

            for (int i = 0; openPositions != null && i < openPositions.Count; i++)
            {
                if (openPositions[i].State != PositionStateType.Open)
                {
                    continue;
                }
                if (openPositions[i].Direction == Side.Buy)  // We put a stop on the long
                {
                    if (lastPrice < _lastEma4)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice - _slippage, openPositions[i].OpenVolume);
                    }
                }
                else // If the direction of the position is short
                {
                    if (lastPrice > _lastEma4)
                    {
                        _tab.CloseAtLimit(openPositions[i], lastPrice + _slippage, openPositions[i].OpenVolume);
                    }
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