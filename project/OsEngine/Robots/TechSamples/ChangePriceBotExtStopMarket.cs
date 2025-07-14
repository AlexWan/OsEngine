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
using OsEngine.Language;

/* Description
TechSample robot for OsEngine

An example of a robot for programmers, where you can see how changing the order price works.
 */

namespace OsEngine.Robots.TechSamples
{
    [Bot("ChangePriceBotExtStopMarket")] // We create an attribute so that we don't write anything to the BotFactory
    public class ChangePriceBotExtStopMarket : BotPanel
    {
        // Simple tab
        BotTabSimple _tab;

        // Basic settings
        StrategyParameterString SideParam;
        StrategyParameterDecimal AntiSlippagePercent;

        // GetVolume settings
        StrategyParameterDecimal Volume;

        // Exit settings
        StrategyParameterInt SecondsOnReplaceOrderPrice;
        StrategyParameterDecimal ProfitPercent;
        StrategyParameterDecimal StopPercent;

        public ChangePriceBotExtStopMarket(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create simple tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;

            // Basic settings
            SideParam = CreateParameter("Side to trade", "Buy", new[] { "Buy", "Sell" });
            AntiSlippagePercent = CreateParameter("Anti slippage percent", 0.2m, 0.1m, 1, 0.1m);

            // GetVolume settings
            Volume = CreateParameter("Volume", 1, 1m, 10,1);
            
            // Exit settings
            SecondsOnReplaceOrderPrice = CreateParameter("Order price life time seconds", 5, 1, 1, 1);
            ProfitPercent = CreateParameter("Profit percent", 0.2m, 0.1m, 1, 0.1m);
            StopPercent = CreateParameter("Stop percent", 0.2m, 0.1m, 1, 0.1m);

            // Create button
            StrategyParameterButton button = CreateParameterButton("Start operation");
            button.UserClickOnButtonEvent += Button_UserClickOnButtonEvent;

            StrategyParameterButton buttonStop = CreateParameterButton("Stop operation");
            buttonStop.UserClickOnButtonEvent += ButtonStop_UserClickOnButtonEvent;

            _tab.ManualPositionSupport.DisableManualSupport();

            Description = OsLocalization.Description.DescriptionLabel100;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ChangePriceBotExtStopMarket";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
           
        }

        // Button click event

        private void Button_UserClickOnButtonEvent()
        {
            _isStarted = true;
        }

        private void ButtonStop_UserClickOnButtonEvent()
        {
            _isStarted = false; 
        }

        private bool _isStarted;

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
                _lastChangeOrderTime = DateTime.Now;
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

        // Opening first position logic
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

        // Close position logic
        private void ClosePositionAtStopAndProfit(Position pos)
        {
            if(pos.StopOrderIsActive == true)
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

        // Logic of changing the order price
        private void TryReplaceOrder(Position pos)
        {
            if(_lastChangeOrderTime.AddSeconds(SecondsOnReplaceOrderPrice.ValueInt) > DateTime.Now)
            {
                return;
            }

            if (pos.Direction == Side.Buy)
            {
                decimal price = _tab.PriceBestBid - _tab.PriceBestBid * (AntiSlippagePercent.ValueDecimal / 100);

                price = Math.Round(price, _tab.Security.Decimals);

                _tab.ChangeOrderPrice(pos.OpenOrders[0], price);
            }
            else if (pos.Direction == Side.Sell)
            {
                decimal price = _tab.PriceBestAsk + _tab.PriceBestAsk * (AntiSlippagePercent.ValueDecimal / 100);

                price = Math.Round(price, _tab.Security.Decimals);

                _tab.ChangeOrderPrice(pos.OpenOrders[0], price);
            }

            _lastChangeOrderTime = DateTime.Now;
        }
    }
}