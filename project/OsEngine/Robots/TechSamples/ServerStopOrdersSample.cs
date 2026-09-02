/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;

/* Description
TechSample robot for OsEngine

An example of a robot trading with server stop-limit orders. Long only.

Entry - a server stop-limit Buy order. The trigger is at the best ask plus Trigger offset percent,
the limit price of the child order is below the trigger by Limit offset percent.
If the order is not executed within Candles to wait (the stop is not triggered or the triggered
child limit order is not filled), it is cancelled and the stop is re-placed at fresh prices.

Exit - when the position is opened, on the next candle a closing server stop-limit Sell order
is placed by the same mechanics, mirrored.

The robot logic is the same for real trading and for the Tester:
the *OnServer methods themselves decide whether the order goes to the exchange
or falls back to a local stop.
 */

namespace OsEngine.Robots.TechSamples
{
    [Bot("ServerStopOrdersSample")] // We create an attribute so that we don't write anything to the BotFactory
    public class ServerStopOrdersSample : BotPanel
    {
        // Simple tab
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterDecimal _volume;

        // Order settings
        private StrategyParameterDecimal _triggerOffsetPercent;
        private StrategyParameterDecimal _limitOffsetPercent;
        private StrategyParameterInt _candlesToWait;

        public ServerStopOrdersSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create and assign the main trading tab
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Base");
            _volume = CreateParameter("Volume", 1, 1.0m, 50, 1, "Base");

            // Order settings
            _triggerOffsetPercent = CreateParameter("Trigger offset percent", 0.05m, 0.01m, 1m, 0.01m, "Order prices");
            _limitOffsetPercent = CreateParameter("Limit offset percent", 0.05m, 0.01m, 1m, 0.01m, "Order prices");
            _candlesToWait = CreateParameter("Candles to wait", 5, 1, 20, 1, "Order prices");

            // Subscribe to the candle finished event
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // Disable manual position support
            _tab.ManualPositionSupport.DisableManualSupport();

            Description = OsLocalization.Description.DescriptionLabel330;
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "ServerStopOrdersSample";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {

        }

        #region State

        // Number of the position currently being managed
        private int _currentPosNumber = -1;

        // Candles count when the position was opened
        private int _positionOpenCandle;

        // Candles count when the last order was placed (the entry stop or the close stop)
        private int _lastActionCandle = -1;

        // The close stop was already placed for the current position
        private bool _closeStopWasPlaced;

        #endregion

        #region Logic

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            if (_tab.IsConnected == false
                || _tab.IsReadyToTrade == false)
            {
                return;
            }

            if (_tab.Security == null
                || _tab.PriceBestAsk == 0
                || _tab.PriceBestBid == 0)
            {
                return;
            }

            Position lastPos = null;

            if (_tab.PositionsOpenAll != null
                && _tab.PositionsOpenAll.Count > 0)
            {
                lastPos = _tab.PositionsOpenAll[^1];
            }

            // 1. The last position is open - manage the close stop

            if (lastPos != null
                && lastPos.OpenVolume > 0)
            {
                if (lastPos.Number != _currentPosNumber)
                {
                    _currentPosNumber = lastPos.Number;
                    _positionOpenCandle = candles.Count;
                    _closeStopWasPlaced = false;
                }

                ManageClose(candles, lastPos);
                return;
            }

            _currentPosNumber = -1;
            _closeStopWasPlaced = false;

            // 2. The robot is off - cancel the entry orders and exit

            if (_regime.ValueString == "Off")
            {
                if (EntryOrderIsAlive(lastPos))
                {
                    CancelEntryOrders(lastPos);
                }

                return;
            }

            // 3. Flat. An entry order is alive (the standing stop or the filling child
            // limit order - it does not matter which one): wait, cancel on timeout.
            // Nothing alive - place a new entry stop

            if (EntryOrderIsAlive(lastPos))
            {
                if (candles.Count - _lastActionCandle >= _candlesToWait.ValueInt)
                { // not executed in time: cancel, re-place on the next candle
                    CancelEntryOrders(lastPos);
                }

                return;
            }

            PlaceEntryStopLimit(candles);
        }

        // Exit cycle: the same mechanics as the entry, mirrored
        private void ManageClose(List<Candle> candles, Position pos)
        {
            if (pos.OpenVolume == 0)
            {
                return;
            }

            // A close order is alive (the standing stop or the filling child limit order):
            // wait, cancel on timeout

            Order lastCloseOrder = GetLastOrder(pos.CloseOrders);

            if (lastCloseOrder != null
                && IsLiveState(lastCloseOrder.State))
            {
                if (candles.Count - _lastActionCandle >= _candlesToWait.ValueInt)
                { // not executed in time: cancel, re-place on the next candle
                    _tab.CloseOrder(lastCloseOrder);
                }

                return;
            }

            // The local close stop is standing inside the position (Tester fallback)

            if (pos.StopOrderIsActive)
            {
                if (candles.Count - _lastActionCandle >= _candlesToWait.ValueInt)
                { // re-place with fresh prices (updates the local stop level in place)
                    PlaceCloseStopLimit(pos, candles);
                }

                return;
            }

            // Wait for the entry child order to finish filling

            Order lastOpenOrder = GetLastOrder(pos.OpenOrders);

            if (lastOpenOrder != null
                && IsLiveState(lastOpenOrder.State))
            {
                return;
            }

            // The first close stop is placed on the next candle after the position opened
            if (_closeStopWasPlaced == false
                && candles.Count <= _positionOpenCandle)
            {
                return;
            }

            PlaceCloseStopLimit(pos, candles);
            _closeStopWasPlaced = true;
        }

        #endregion

        #region Order placement

        // Entry: server stop-limit Buy. Trigger at the best ask + offset, limit below the trigger
        private void PlaceEntryStopLimit(List<Candle> candles)
        {
            decimal volume = _volume.ValueDecimal;

            if (volume <= 0)
            {
                return;
            }

            decimal ask = _tab.PriceBestAsk;

            decimal activation = _tab.RoundPrice(
                ask + ask * _triggerOffsetPercent.ValueDecimal / 100,
                _tab.Security, Side.Buy);

            decimal priceLimit = _tab.RoundPrice(
                activation - activation * _limitOffsetPercent.ValueDecimal / 100,
                _tab.Security, Side.Buy);

            _lastActionCandle = candles.Count;

            _tab.BuyAtStopOnServer(volume, priceLimit, activation, "ServerStopLimitEntry");
        }

        // Exit: server stop-limit Sell. Trigger at the best bid - offset, limit above the trigger
        private void PlaceCloseStopLimit(Position pos, List<Candle> candles)
        {
            decimal bid = _tab.PriceBestBid;

            decimal activation = _tab.RoundPrice(
                bid - bid * _triggerOffsetPercent.ValueDecimal / 100,
                _tab.Security, Side.Sell);

            decimal priceOrder = _tab.RoundPrice(
                activation + activation * _limitOffsetPercent.ValueDecimal / 100,
                _tab.Security, Side.Sell);

            _lastActionCandle = candles.Count;

            _tab.CloseAtStopOnServer(pos, activation, priceOrder, "ServerStopLimitExit");
        }

        // Cancel the entry order wherever it lives and whatever it is:
        // BuyAtStopCancel removes local openers (Tester),
        // CloseOrder cancels the last live order of the position (real trading)
        private void CancelEntryOrders(Position lastPos)
        {
            _tab.BuyAtStopCancel();

            if (lastPos == null)
            {
                return;
            }

            Order lastOrder = GetLastOrder(lastPos.OpenOrders);

            if (lastOrder != null
                && IsLiveState(lastOrder.State))
            {
                _tab.CloseOrder(lastOrder);
            }
        }

        #endregion

        #region Order helpers

        // An entry order is alive somewhere: in the local openers list (Tester)
        // or as the last live order of the pending position (real trading)
        private bool EntryOrderIsAlive(Position lastPos)
        {
            if (_tab.PositionOpenerToStopsAll != null
                && _tab.PositionOpenerToStopsAll.Count > 0)
            {
                return true;
            }

            if (lastPos != null
                && lastPos.OpenVolume == 0)
            {
                Order lastOrder = GetLastOrder(lastPos.OpenOrders);

                if (lastOrder != null
                    && IsLiveState(lastOrder.State))
                {
                    return true;
                }
            }

            return false;
        }

        // The last (newest) order among the orders, or null.
        // Orders are stored in chronological order: the server stop comes first,
        // its child exchange order is appended after the trigger
        private Order GetLastOrder(List<Order> orders)
        {
            if (orders == null
                || orders.Count == 0)
            {
                return null;
            }

            return orders[^1];
        }

        private bool IsLiveState(OrderStateType state)
        {
            return state == OrderStateType.Active
                || state == OrderStateType.Pending
                || state == OrderStateType.Partial
                || state == OrderStateType.None;
        }

        #endregion
    }
}
