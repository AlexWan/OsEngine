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

namespace OsEngine.Robots.Patterns
{
    [Bot("CustomCandlesImpulseTrader")]
    public class CustomCandlesImpulseTrader : BotPanel
    {
        public CustomCandlesImpulseTrader(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On", "OnlyLong", "OnlyShort", "OnlyClosePosition" });

            VolumeType = CreateParameter("Volume type", "Contracts", new[] { "Contracts", "Contract currency", "Deposit percent" });

            Volume = CreateParameter("Volume", 1, 1.0m, 50, 4);

            TradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            Slippage = CreateParameter("Slippage %", 0, 0, 20, 1m);

            CandlesCountToEntry = CreateParameter("Candles count to entry", 2, 0, 20, 1);

            SecondsTimeOnCandlesToEntry = CreateParameter("Seconds time on candles to entry", 120, 0, 20, 1);

            CandlesCountToExit = CreateParameter("Candles count to exit", 1, 0, 20, 1);

            Description = "Trading robot for adaptive by volatility candle series. " +
                "if he sees a movement to one side in a short period of time, it enters the position";
        }

        public override string GetNameStrategyType()
        {
            return "CustomCandlesImpulseTrader";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabSimple _tab;

        // settings

        public StrategyParameterString Regime;

        public StrategyParameterInt CandlesCountToEntry;

        public StrategyParameterInt SecondsTimeOnCandlesToEntry;

        public StrategyParameterInt CandlesCountToExit;

        public StrategyParameterDecimal Slippage;

        public StrategyParameterString VolumeType;

        public StrategyParameterDecimal Volume;

        public StrategyParameterString TradeAssetInPortfolio;

        // logic

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (Regime.ValueString == "Off")
            {
                return;
            }

            if (candles.Count < 5)
            {
                return;
            }

            if(candles.Count < CandlesCountToEntry.ValueInt + 2)
            {
                return;
            }

            List<Position> openPositions = _tab.PositionsOpenAll;

            if (openPositions == null || openPositions.Count == 0)
            {
                if (Regime.ValueString == "OnlyClosePosition")
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

        private void LogicOpenPosition(List<Candle> candles)
        {
            //  long
            if (Regime.ValueString != "OnlyShort")
            {
                bool isLongSignal = true;

                for(int i = candles.Count - 1;i >= 0 && i > candles.Count -1 - CandlesCountToEntry.ValueInt;i--)
                {
                    if (candles[i].IsUp == false)
                    {
                        isLongSignal = false;
                    }
                }

                if(isLongSignal == true)
                {
                    TimeSpan timeCandles = candles[candles.Count - 1].TimeStart - candles[candles.Count-1- CandlesCountToEntry.ValueInt].TimeStart;

                    if(timeCandles.TotalSeconds > SecondsTimeOnCandlesToEntry.ValueInt)
                    {
                        isLongSignal = false;
                    }
                }

                if (isLongSignal)
                {
                    decimal lastPrice = candles[candles.Count - 1].Close;
                    _tab.BuyAtLimit(GetVolume(_tab), lastPrice + lastPrice * (Slippage.ValueDecimal / 100), 
                        candles[candles.Count-1].TimeStart.ToString(CultureInfo.InvariantCulture));
                }
            }

            // Short
            if (Regime.ValueString != "OnlyLong")
            {
                bool isShortSignal = true;

                for (int i = candles.Count - 1; i >= 0 && i > candles.Count - 1 - CandlesCountToEntry.ValueInt; i--)
                {
                    if (candles[i].IsDown == false)
                    {
                        isShortSignal = false;
                    }
                }

                if (isShortSignal == true)
                {
                    TimeSpan timeCandles = candles[candles.Count - 1].TimeStart - candles[candles.Count - 1 - CandlesCountToEntry.ValueInt].TimeStart;

                    if (timeCandles.TotalSeconds > SecondsTimeOnCandlesToEntry.ValueInt)
                    {
                        isShortSignal = false;
                    }
                }

                if (isShortSignal)
                {
                    decimal lastPrice = candles[candles.Count - 1].Close;
                    _tab.SellAtLimit(GetVolume(_tab), lastPrice - lastPrice * (Slippage.ValueDecimal / 100), 
                        candles[candles.Count - 1].TimeStart.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

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

            if(endCandlesFromOpenPosition < CandlesCountToExit.ValueInt)
            {
                return;
            }

            if (position.Direction == Side.Buy)
            {
                decimal priceExit = _tab.PriceBestBid - _tab.PriceBestBid * (Slippage.ValueDecimal / 100);

                _tab.CloseAtLimit(position, priceExit, position.OpenVolume);
            }
            else
            {
                decimal priceExit = _tab.PriceBestAsk + _tab.PriceBestAsk * (Slippage.ValueDecimal / 100);

                _tab.CloseAtLimit(position, priceExit, position.OpenVolume);
            }
        }

        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (VolumeType.ValueString == "Contracts")
            {
                volume = Volume.ValueDecimal;
            }
            else if (VolumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = Volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Securiti.Lot != 0 &&
                        tab.Securiti.Lot > 1)
                    {
                        volume = Volume.ValueDecimal / (contractPrice * tab.Securiti.Lot);
                    }

                    volume = Math.Round(volume, tab.Securiti.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (VolumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (TradeAssetInPortfolio.ValueString == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == TradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + TradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (Volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Securiti.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    qty = Math.Round(qty, tab.Securiti.DecimalsVolume);
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