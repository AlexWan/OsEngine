/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.Language;

/* Description
Fisher based on multithreading

Entering a position - we are waiting for a sharp price deviation 
by the specified number of percent from the edge of the order book.

Exit a position when the price rolls back by 50 percent or more of the entry
price. Those. if the price has returned half or more of the original movement.
 */

namespace OsEngine.Robots.High_Frequency
{
    [Bot("Fisher")] // We create an attribute so that we don't write anything to the BotFactory
    public class Fisher : BotPanel
    {
        private BotTabSimple _tab;
        
        // Basic settings
        public StrategyParameterString Regime;
        public StrategyParameterDecimal PersentFromBorder;
        public StrategyParameterInt TimeRebuildOrder;
        public StrategyParameterInt PriceDecimals;

        // GetVolume settings
        public StrategyParameterDecimal Volume;

        // Indicator
        private Aindicator _sma;

        public Fisher(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            TimeRebuildOrder = CreateParameter("Time Rebuild Order", 30, 0, 20, 5);
            PersentFromBorder = CreateParameter("Persent From Border ", 2m, 0.3m, 4, 0.3m);
            PriceDecimals = CreateParameter("Price Decimals", 0, 0, 20, 1);

            // GetVolume settings
            Volume = CreateParameter("Volume", 0.1m, 0.1m, 50, 0.1m);

            // Create worker Area
            Thread worker = new Thread(Logic);
            worker.IsBackground = true;
            worker.Start();
            
            // Create indicator Sma
            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Bollinger", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");

            DeleteEvent += Fisher_DeleteEvent;

            Description = OsLocalization.Description.DescriptionLabel42;
        }

        private void Fisher_DeleteEvent()
        {
            _isDisposed = true;
        }

        private bool _isDisposed;

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "Fisher";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        // Logic
        private void Logic()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(TimeRebuildOrder.ValueInt * 1000);

                    if (_isDisposed)
                    {
                        return;
                    }

                    if (Regime.ValueString == "Off")
                    {
                        continue;
                    }

                    if (_sma.DataSeries[0].Values == null ||
                        _sma.ParametersDigit[0].Value + 3 > _sma.DataSeries[0].Values.Count)
                    {
                        continue;
                    }

                    CanselAllOrders();
                    CloseAllPositions();
                    OpenOrders();
                }
                catch (Exception e) 
                {
                    _tab.SetNewLogMessage(e.ToString(),Logging.LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        // Cansel all orders
        private void CanselAllOrders()
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            Position[] poses = openPositions.ToArray();

            for (int i = 0; poses != null && i < poses.Length;i++)
            {
                if(poses[i].State != PositionStateType.Open)
                {
                    _tab.CloseAllOrderToPosition(poses[i]);
                }
                Thread.Sleep(200);
            }
            Thread.Sleep(1000);

            if(openPositions.Count != 0)
            {
                Thread.Sleep(1000);
            }
            if (openPositions.Count != 0)
            {
                Thread.Sleep(1000);
            }
        }

        // Close all position logic
        private void CloseAllPositions()
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            Position[] poses = openPositions.ToArray();

            decimal lastBestBuy = _tab.PriceBestBid;
            decimal lastBestSell = _tab.PriceBestAsk;

            for (int i = 0; poses != null && i < poses.Length; i++)
            {
                if (poses[i].State == PositionStateType.Open ||
                    poses[i].State == PositionStateType.ClosingFail)
                {
                    if (poses[i].CloseActive)
                    {
                        continue;
                    }

                    if (poses[i].Direction == Side.Buy)
                    {
                        decimal price = lastBestSell;
                        decimal priceToPercent = poses[i].EntryPrice + poses[i].EntryPrice * (PersentFromBorder.ValueDecimal / 100 / 2); 

                        if((poses[i].CloseOrders == null ||
                            poses[i].CloseOrders.Count < 3) &&
                            priceToPercent > price)
                        {
                            price = priceToPercent;
                        }

                        _tab.CloseAtLimit(poses[i], price, poses[i].OpenVolume);
                    }

                    if (poses[i].Direction == Side.Sell)
                    {
                        decimal price = lastBestBuy;
                        decimal priceToPercent = poses[i].EntryPrice - poses[i].EntryPrice * (PersentFromBorder.ValueDecimal / 100 / 2);

                        if ((poses[i].CloseOrders == null ||
                            poses[i].CloseOrders.Count < 3) &&
                            priceToPercent < price)
                        {
                            price = priceToPercent;
                        }

                        _tab.CloseAtLimit(poses[i], price, poses[i].OpenVolume);
                    }
                }
            }

            Thread.Sleep(200);
        }

        // Open orders logic
        private void OpenOrders()
        {
            List<Position> openPositions = _tab.PositionsOpenAll;

            if(openPositions.Count > 0)
            {
                return;
            }

            decimal lastMa = _sma.DataSeries[0].Last;
            decimal lastBestBuy = _tab.PriceBestBid;
            decimal lastBestSell = _tab.PriceBestAsk;

            if(lastMa == 0
                || lastBestBuy == 0
                || lastBestSell == 0)
            {
                return;
            }

            // we check that prices are no further than 1% from Sma

            if (Math.Abs(lastMa / lastBestBuy) > 1.01m ||
                Math.Abs(lastMa / lastBestBuy) < 0.99m)
            {
                return;
            }

            if (Math.Abs(lastMa / lastBestSell) > 1.01m ||
                Math.Abs(lastMa / lastBestSell) < 0.99m)
            {
                return;
            }

            decimal priceBuy = _tab.PriceBestAsk - _tab.PriceBestAsk * (PersentFromBorder.ValueDecimal /100);
            decimal priceSell = _tab.PriceBestBid + _tab.PriceBestAsk * (PersentFromBorder.ValueDecimal / 100);

            _tab.BuyAtLimit(Volume.ValueDecimal, Math.Round(priceBuy,PriceDecimals.ValueInt));
            _tab.SellAtLimit(Volume.ValueDecimal, Math.Round(priceSell, PriceDecimals.ValueInt));
        }
    }
}