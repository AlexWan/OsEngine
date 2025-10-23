/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Indicators;
using OsEngine.Language;

/* Description
trading robot for osengine

The trend robot on Bollinger Momentum Screener.

Buy:
1. If the closing price of the last candle (lastCandleClose) is above the Bollinger upper line (lastUpBollingerLine)
2. And the Momentum level exceeds the minimum value (_minMomentumValue)

Exit: by trailing stop.
 */

namespace OsEngine.Robots.Screeners
{
    [Bot("BollingerMomentumScreener")] // We create an attribute so that we don't write anything to the BotFactory
    public class BollingerMomentumScreener : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _slippage;
        private StrategyParameterInt _maxPositions;

        // Indicator settings
        private StrategyParameterInt _bollingerLen;
        private StrategyParameterDecimal _bollingerDev;
        private StrategyParameterInt _momentumLen;
        private StrategyParameterDecimal _minMomentumValue;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Exit setting
        private StrategyParameterDecimal _trailStop;
        
        public BollingerMomentumScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            _tabScreener.CandleFinishedEvent += _screenerTab_CandleFinishedEvent;

            // Create indicator Bollinger
            _tabScreener.CreateCandleIndicator(1, "Bollinger", new List<string>() { "100" ,"2"}, "Prime");

            // Create indicator Momentum
            _tabScreener.CreateCandleIndicator(2, "Momentum", new List<string>() { "15", "Close" }, "Second");

            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyClosePosition" });
            _maxPositions = CreateParameter("Max positions", 5, 0, 20, 1);
            _slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _minMomentumValue = CreateParameter("Min momentum value", 105m, 0, 20, 1m);
            _bollingerLen = CreateParameter("Bollinger length", 50, 0, 20, 1);
            _bollingerDev = CreateParameter("Bollinger deviation", 2m, 0, 20, 1m);
            _momentumLen = CreateParameter("Momentum length", 50, 0, 20, 1);

            // Exit setting
            _trailStop = CreateParameter("Trail stop %", 2.9m, 0, 20, 1m);

            Description = OsLocalization.Description.DescriptionLabel87;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "BollingerMomentumScreener";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // logic

        private void _screenerTab_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            List<Position> openPositions = tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (_regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicOpenPosition(candles, tab);
            }
            else
            {
                LogicClosePosition(candles, tab, openPositions[0]);
            }
        }

        // Logic open position
        private void LogicOpenPosition(List<Candle> candles, BotTabSimple tab)
        {
            if (_tabScreener.PositionsOpenAll.Count >= _maxPositions.ValueInt)
            {
                return;
            }

            decimal lastCandleClose = candles[candles.Count - 1].Close;

            Aindicator bollinger = (Aindicator)tab.Indicators[0];

            if (bollinger.ParametersDigit[0].Value != _bollingerLen.ValueInt
                || bollinger.ParametersDigit[1].Value != _bollingerDev.ValueDecimal)
            {
                bollinger.ParametersDigit[0].Value = _bollingerLen.ValueInt;
                bollinger.ParametersDigit[1].Value = _bollingerDev.ValueDecimal;
                bollinger.Save();
                bollinger.Reload();
            }

            if (bollinger.DataSeries[0].Values.Count == 0 ||
                bollinger.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal lastUpBollingerLine = bollinger.DataSeries[0].Last;

            Aindicator momentum = (Aindicator)tab.Indicators[1];

            if (momentum.ParametersDigit[0].Value != _momentumLen.ValueInt)
            {
                momentum.ParametersDigit[0].Value = _momentumLen.ValueInt;
                momentum.Save();
                momentum.Reload();
            }

            if (momentum.DataSeries[0].Values.Count == 0 ||
                momentum.DataSeries[0].Last == 0)
            {
                return;
            }

            decimal lastMomentum = momentum.DataSeries[0].Last;

             if (lastCandleClose > lastUpBollingerLine
                && lastMomentum > _minMomentumValue.ValueDecimal)
             {
                 tab.BuyAtLimit(GetVolume(tab), lastCandleClose + lastCandleClose * (_slippage.ValueDecimal / 100));
             }
        }

        // Logic close position
        private void LogicClosePosition(List<Candle> candles, BotTabSimple tab, Position position)
        {
            if (position.State != PositionStateType.Open
                          ||
                          (position.CloseOrders != null
                          && position.CloseOrders.Count > 0)
                          )
            {
                return;
            }

            decimal lastClose = candles[candles.Count - 1].Close;

            decimal stop = 0;
            decimal stopWithSlippage = 0;

            if (position.Direction == Side.Buy)
            {
                stop = lastClose - lastClose * (_trailStop.ValueDecimal / 100);
                stopWithSlippage = stop - stop * (_slippage.ValueDecimal / 100);
            }
            else //if (position.Direction == Side.Sell)
            {
                stop = lastClose + lastClose * (_trailStop.ValueDecimal / 100);
                stopWithSlippage = stop + stop * (_slippage.ValueDecimal / 100);
            }

            tab.CloseAtTrailingStop(position, stop, stopWithSlippage);
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