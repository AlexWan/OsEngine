/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System;

namespace OsEngine.Robots.TechSamples
{
    [Bot("ChangePriceBotExtStopMarket")]
    public class ChangePriceBotExtStopMarket : BotPanel
    {
        public ChangePriceBotExtStopMarket(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;

            SideParam = CreateParameter("Side to trade", "Buy", new[] { "Buy", "Sell" });
            Volume = CreateParameter("Volume", 1, 1m, 10,1);
            AntiSlippagePercent = CreateParameter("Anti slippage percent", 0.2m, 0.1m, 1, 0.1m);
            SecondsOnReplaceOrderPrice = CreateParameter("Order price life time seconds", 5, 1, 1, 1);
            ProfitPercent = CreateParameter("Profit percent", 0.2m, 0.1m, 1, 0.1m);
            StopPercent = CreateParameter("Stop percent", 0.2m, 0.1m, 1, 0.1m);

            StrategyParameterButton button = CreateParameterButton("Start operation");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            StrategyParameterButton buttonStop = CreateParameterButton("Stop operation");
            buttonStop.UserClickOnButtonEvent += ButtonStop_UserClickOnButtonEvent;

            _tab.ManualPositionSupport.DisableManualSupport();
        }

        BotTabSimple _tab;

        StrategyParameterString SideParam;

        StrategyParameterDecimal AntiSlippagePercent;

        StrategyParameterDecimal Volume;

        StrategyParameterInt SecondsOnReplaceOrderPrice;

        StrategyParameterDecimal ProfitPercent;

        StrategyParameterDecimal StopPercent;

        public override string GetNameStrategyType()
        {
            return "ChangePriceBotExtStopMarket";
        }

        public override void ShowIndividualSettingsDialog()
        {
           
        }

        private void Button_UserClickOnButtonEvent()
        {
            _firstOrderIsExecute = false;
            _isStarted = true;
        }

        private void ButtonStop_UserClickOnButtonEvent()
        {
            _isStarted = false; 
        }

        private bool _isStarted;

        private bool _firstOrderIsExecute = false;

        private void _tab_MarketDepthUpdateEvent(MarketDepth md)
        {
            if (_isStarted == false)
            {
                return;
            }

            if (_tab.IsConnected == false ||
                _tab.IsReadyToTrade == false)
            {
                return;
            }

            List<Position> poses = _tab.PositionsOpenAll;
          
            if (poses.Count == 0)
            {
                if(_firstOrderIsExecute == true)
                {
                   //_isStarted = false;
                }
                _lastChangeOrderTime = DateTime.Now;
                _firstOrderIsExecute = true;
                CreateFirstPos();
                return;
            }

            Position pos = poses[0];

            if (pos.State == PositionStateType.Open)
            {
                ClosePositionAtStopAndProfit(pos);
            }
            else if (pos.State == PositionStateType.Opening)
            {
                TryReplaceOrder(pos);
            }
        }

        private void CreateFirstPos()
        {
            if(SideParam.ValueString == "Buy")
            {
                decimal price = _tab.PriceBestBid - _tab.PriceBestBid * (AntiSlippagePercent.ValueDecimal / 100);
                decimal volume = Volume.ValueDecimal;

                _tab.BuyAtLimit(volume, price);
            }
            else if (SideParam.ValueString == "Sell")
            {
                decimal price = _tab.PriceBestAsk + _tab.PriceBestAsk * (AntiSlippagePercent.ValueDecimal / 100);
                decimal volume = Volume.ValueDecimal;

                _tab.SellAtLimit(volume, price);
            }
        }

        DateTime _lastChangeOrderTime = DateTime.Now;

        private void ClosePositionAtStopAndProfit(Position pos)
        {
            if(pos.StopOrderIsActiv == true)
            {
                return;
            }

            if (pos.Direction == Side.Buy)
            {
                decimal priceStop = pos.EntryPrice - pos.EntryPrice * (StopPercent.ValueDecimal/100);
                decimal priceProfit = pos.EntryPrice + pos.EntryPrice * (ProfitPercent.ValueDecimal / 100);
                _tab.CloseAtStopMarket(pos, priceStop);
                _tab.CloseAtProfitMarket(pos, priceProfit);
            }
            else if (pos.Direction == Side.Sell)
            {
                decimal priceStop = pos.EntryPrice + pos.EntryPrice * (StopPercent.ValueDecimal / 100);
                decimal priceProfit = pos.EntryPrice - pos.EntryPrice * (ProfitPercent.ValueDecimal / 100);

                _tab.CloseAtStopMarket(pos, priceStop);
                _tab.CloseAtProfitMarket(pos, priceProfit);
            }
        }

        private void TryReplaceOrder(Position pos)
        {
            if(_lastChangeOrderTime.AddSeconds(SecondsOnReplaceOrderPrice.ValueInt) > DateTime.Now)
            {
                return;
            }

            if (pos.Direction == Side.Buy)
            {
                decimal price = _tab.PriceBestBid - _tab.PriceBestBid * (AntiSlippagePercent.ValueDecimal / 100);

                price = Math.Round(price, _tab.Securiti.Decimals);

                _tab.ChangeOrderPrice(pos.OpenOrders[0], price);
            }
            else if (pos.Direction == Side.Sell)
            {
                decimal price = _tab.PriceBestAsk + _tab.PriceBestAsk * (AntiSlippagePercent.ValueDecimal / 100);

                price = Math.Round(price, _tab.Securiti.Decimals);

                _tab.ChangeOrderPrice(pos.OpenOrders[0], price);
            }

            _lastChangeOrderTime = DateTime.Now;
        }
    }
}