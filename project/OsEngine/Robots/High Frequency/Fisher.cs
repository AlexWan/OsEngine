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

namespace OsEngine.Robots.High_Frequency
{
    public class Fisher : BotPanel
    {
        public Fisher(string name, StartProgram startProgram)
            : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Bollinger", false);
            _sma = (Aindicator)_tab.CreateCandleIndicator(_sma, "Prime");

            Regime = CreateParameter("Regime", "Off", new[] { "Off", "On" });
            TimeRebuildOrder = CreateParameter("Time Rebuild Order", 30, 0, 20, 5);
            Volume = CreateParameter("Volume", 0.1m, 0.1m, 50, 0.1m);
            PersentFromBorder = CreateParameter("Persent From Border ", 2m, 0.3m, 4, 0.3m);
            PriceDecimals = CreateParameter("Price Decimals", 0, 0, 20, 1);

            Thread worker = new Thread(Logic);
            worker.IsBackground = true;
            worker.Start();

            DeleteEvent += Fisher_DeleteEvent;
        }

        private void Fisher_DeleteEvent()
        {
            _isDisposed = true;
        }

        private bool _isDisposed;

        public override string GetNameStrategyType()
        {
            return "Fisher";
        }

        public override void ShowIndividualSettingsDialog()
        {

        }

        private BotTabSimple _tab;

        private Aindicator _sma;

        public StrategyParameterDecimal Volume;

        public StrategyParameterDecimal PersentFromBorder;

        public StrategyParameterInt TimeRebuildOrder;

        public StrategyParameterString Regime;

        public StrategyParameterInt PriceDecimals;

        // logic логика

        private void Logic()
        {
            while(true)
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
        }

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
                    if (poses[i].CloseActiv)
                    {
                        continue;
                    }
                    if (poses[i].Direction == Side.Buy)
                    {
                        decimal price = lastBestSell;
                        decimal priceToPercent = 
                            poses[i].EntryPrice + poses[i].EntryPrice * (PersentFromBorder.ValueDecimal / 100 / 2); 

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

            // проверяем чтобы цены были не дальше 1% от машки

            if(Math.Abs(lastMa / lastBestBuy) > 1.01m ||
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
