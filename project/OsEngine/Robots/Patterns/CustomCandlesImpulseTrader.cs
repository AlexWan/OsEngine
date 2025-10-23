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
using System.Globalization;
using OsEngine.Language;

/*Discription
Trading robot for osengine

Trading robot for adaptive by volatility candle series.
if he sees a movement to one side in a short period of time, it enters the position
*/

namespace OsEngine.Robots.Patterns
{
    [Bot("CustomCandlesImpulseTrader")] //We create an attribute so that we don't write anything in the Boot factory
    public class CustomCandlesImpulseTrader : BotPanel
    {
        private BotTabSimple _tab;

        // Basic settigs
        private StrategyParameterString _regime;
        private StrategyParameterInt _candlesCountToEntry;
        private StrategyParameterInt _secondsTimeOnCandlesToEntry;
        private StrategyParameterDecimal _slippage;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        //Exit setting
        private StrategyParameterInt _candlesCountToExit;

        public CustomCandlesImpulseTrader(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });
            _slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);
            _candlesCountToEntry = CreateParameter("Candles count to entry", 2, 0, 20, 1);
            _secondsTimeOnCandlesToEntry = CreateParameter("Seconds time on candles to entry", 120, 0, 20, 1);

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 1, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Exit settings
            _candlesCountToExit = CreateParameter("Candles count to exit", 1, 0, 20, 1);

            // Subscribe to the candle completion event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Description = OsLocalization.Description.DescriptionLabel73;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CustomCandlesImpulseTrader";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            if(candles.Count < _candlesCountToEntry.ValueInt + 2)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (_regime.ValueString == "OnlyClosePosition")
                {
                    return;
                }
                LogicOpenPosition(candles);
            }
            else
            {
                LogicClosePosition(candles, openPositions[0]);
            }
        }

        // Opening position logic
        private void LogicOpenPosition(List<Candle> candles)
        {
            //  long
            if (_regime.ValueString != "OnlyShort")
            {
                bool isLongSignal = true;

                for(int i = candles.Count - 1;i >= 0 && i > candles.Count -1 - _candlesCountToEntry.ValueInt;i--)
                {
                    if (candles[i].IsUp == false)
                    {
                        isLongSignal = false;
                    }
                }

                if(isLongSignal == true)
                {
                    TimeSpan timeCandles = candles[candles.Count - 1].TimeStart - candles[candles.Count-1- _candlesCountToEntry.ValueInt].TimeStart;

                    if(timeCandles.TotalSeconds > _secondsTimeOnCandlesToEntry.ValueInt)
                    {
                        isLongSignal = false;
                    }
                }

                if (isLongSignal)
                {
                    decimal lastPrice = candles[candles.Count - 1].Close;
                    _tab.BuyAtLimit(GetVolume(_tab), lastPrice + lastPrice * (_slippage.ValueDecimal / 100), 
                        candles[candles.Count-1].TimeStart.ToString(CultureInfo.InvariantCulture));
                }
            }

            // Short
            if (_regime.ValueString != "OnlyLong")
            {
                bool isShortSignal = true;

                for (int i = candles.Count - 1; i >= 0 && i > candles.Count - 1 - _candlesCountToEntry.ValueInt; i--)
                {
                    if (candles[i].IsDown == false)
                    {
                        isShortSignal = false;
                    }
                }

                if (isShortSignal == true)
                {
                    TimeSpan timeCandles = candles[candles.Count - 1].TimeStart - candles[candles.Count - 1 - _candlesCountToEntry.ValueInt].TimeStart;

                    if (timeCandles.TotalSeconds > _secondsTimeOnCandlesToEntry.ValueInt)
                    {
                        isShortSignal = false;
                    }
                }

                if (isShortSignal)
                {
                    decimal lastPrice = candles[candles.Count - 1].Close;
                    _tab.SellAtLimit(GetVolume(_tab), lastPrice - lastPrice * (_slippage.ValueDecimal / 100), 
                        candles[candles.Count - 1].TimeStart.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        // Logic close position 
        private void LogicClosePosition(List<Candle> candles, Position position)
        {

            if (position.State != PositionStateType.Open)
            {
                return;
            }

            if (string.IsNullOrEmpty(position.SignalTypeOpen))
            {
                return;
            }

            DateTime timeOpenPosition = Convert.ToDateTime(position.SignalTypeOpen, CultureInfo.InvariantCulture);

            int endCandlesFromOpenPosition = 0;

            for (int i = candles.Count - 1; i >= 0; i--)
            {
                if (candles[i].TimeStart <= timeOpenPosition)
                {
                    break;
                }

                endCandlesFromOpenPosition++;
            }

            if(endCandlesFromOpenPosition < _candlesCountToExit.ValueInt)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                decimal priceExit = _tab.PriceBestBid - _tab.PriceBestBid * (_slippage.ValueDecimal / 100);

                _tab.CloseAtLimit(position, priceExit, position.OpenVolume);
            }
            else
            {
                decimal priceExit = _tab.PriceBestAsk + _tab.PriceBestAsk * (_slippage.ValueDecimal / 100);

                _tab.CloseAtLimit(position, priceExit, position.OpenVolume);
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