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
using OsEngine.Language;

/*Discription
Trading robot for osengine.

Buy:
1. The last candle opened and closed in the upper third of the high-low range.
2. Price is above the SMA.

Sell:
1. The last candle opened and closed in the lower third of the high-low range.
2. Price is below the SMA.

Exit: Positions exit on trailing stop.
*/

namespace OsEngine.Robots.Screeners
{
    [Bot("PinBarScreener")]//We create an attribute so that we don't write anything in the Boot factory
    public class PinBarScreener : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Basic Settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _maxPositions;
        private StrategyParameterDecimal _slippage;

        // GetVolume Parameter 
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Height candles parameter
        private StrategyParameterDecimal _minHeightCandlesPercent;
        private StrategyParameterDecimal _maxHeightCandlesPercent;

        // Sma
        private StrategyParameterInt _smaPeriod;

        // Close setting
        private StrategyParameterDecimal _trailStop;

        public PinBarScreener(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            // Basic Setting
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _maxPositions = CreateParameter("Max positions", 5, 0, 20, 1);
            _slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            // GetVolume Parameter
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Height candles parameter
            _maxHeightCandlesPercent = CreateParameter("Max height candles percent", 1.1m, 0, 20, 1m);
            _minHeightCandlesPercent = CreateParameter("Min height candles percent", 0.5m, 0, 20, 1m);

            // Sma
            _smaPeriod = CreateParameter("Sma Period", 100, 10, 50, 500);
            
            // Close setting
            _trailStop = CreateParameter("Trail stop %", 0.5m, 0, 20, 1m);

            // Subscribe to the candle completion event
            _tabScreener.CandleFinishedEvent += _tab_CandleFinishedEvent1;

            Description = OsLocalization.Description.DescriptionLabel89;
        }

        // Candle Completion Event
        private void _tab_CandleFinishedEvent1(List<Candle> candles, BotTabSimple tab)
        {
            Logic(candles, tab);
        }

        private void Logic(List<Candle> candles, BotTabSimple tab)
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

        // Opening logic
        private void LogicOpenPosition(List<Candle> candles, BotTabSimple tab)
        {
            if (_tabScreener.PositionsOpenAll.Count >= _maxPositions.ValueInt)
            {
                return;
            }

            decimal lastClose = candles[candles.Count - 1].Close;
            decimal lastOpen = candles[candles.Count - 1].Open;
            decimal lastHigh = candles[candles.Count - 1].High;
            decimal lastLow = candles[candles.Count - 1].Low;
            decimal lastSma = Sma(candles, _smaPeriod.ValueInt, candles.Count - 1);

            decimal lenCandlePercent = (lastHigh - lastLow) / (lastLow / 100);

            if(lenCandlePercent > _maxHeightCandlesPercent.ValueDecimal ||
                lenCandlePercent < _minHeightCandlesPercent.ValueDecimal)
            {
                return;
            }

            if (lastClose >= lastHigh - ((lastHigh - lastLow) / 3) && lastOpen >= lastHigh - ((lastHigh - lastLow) / 3)
                && lastSma < lastClose
                && _regime.ValueString != "OnlyShort")
            {
                tab.BuyAtLimit(GetVolume(tab), lastClose + lastClose * (_slippage.ValueDecimal / 100));
            }
            if (lastClose <= lastLow + ((lastHigh - lastLow) / 3) && lastOpen <= lastLow + ((lastHigh - lastLow) / 3)
                && lastSma > lastClose
            && _regime.ValueString != "OnlyLong")
            {
                tab.SellAtLimit(GetVolume(tab), lastClose - lastClose * (_slippage.ValueDecimal / 100));
            }
        }

        // Closing position logic
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

        // The name of the robot in OsEngin
        public override string GetNameStrategyType()
        {
            return "PinBarScreener";
        }

        public override void ShowIndividualSettingsDialog()
        {

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

        // Method for calculating Sma
        private decimal Sma(List<Candle> candles, int len, int index)
        {
            if (candles.Count == 0
                || index >= candles.Count
                || index <= 0)
            {
                return 0;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - len; i--)
            {
                countPoints++;
                summ += candles[i].Close;
            }

            if (countPoints == 0)
            {
                return 0;
            }

            return summ / countPoints;
        }
    }
}