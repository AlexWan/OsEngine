/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using System.Globalization;

namespace OsEngine.Journal.Internal
{
    public class PositionController
    {
        #region Static part with the work of the saving position flow

        public static List<PositionController> ControllersToCheck = new List<PositionController>();

        public static void Activate()
        {
            if (_worker == null)
            {
                _currentCulture = OsLocalization.CurCulture;
                _worker = new Task(WatcherHome);
                _worker.Start();
            }
        }

        private static Task _worker;

        private static CultureInfo _currentCulture;

        public static async void WatcherHome()
        {

            while (true)
            {
                try
                {
                    for (int i = 0; i < ControllersToCheck.Count; i++)
                    {
                        PositionController controller = ControllersToCheck[i];

                        if (controller == null)
                        {
                            continue;
                        }

                        controller.SavePositions();
                        controller.TryPaintPositions();
                        controller.TrySaveStopLimits();
                    }

                    if (!MainWindow.ProccesIsWorked)
                    {
                        return;
                    }
                }
                catch
                {
                    // ignore
                }
                await Task.Delay(1000);
            }
        }

        #endregion

        #region Service

        public PositionController(string name, StartProgram startProgram)
        {
            _name = name;
            _startProgram = startProgram;

            Activate();

            if (_startProgram != StartProgram.IsOsOptimizer)
            {
                ControllersToCheck.Add(this);
                Load();
            }

            if (_deals != null &&
                _deals.Count > 0)
            {
                _openPositions = _deals.FindAll(
                    position => position.State != PositionStateType.Done
                                && position.State != PositionStateType.OpeningFail);
            }
        }

        private StartProgram _startProgram;

        private string _name;

        private void Load()
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if (!File.Exists(@"Engine\" + _name + @"DealController.txt"))
            {
                return;
            }
            try
            {
                // 1 count the number of transactions in the file
                //1 считаем кол-во сделок в файле

                List<string> deals = new List<string>();

                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"DealController.txt"))
                {
                    try
                    {
                        Enum.TryParse(reader.ReadLine(), out _commissionType);
                        _commissionValue = reader.ReadLine().ToDecimal();
                    }
                    catch
                    {
                        // ignore
                    }

                    while (!reader.EndOfStream)
                    {
                        deals.Add(reader.ReadLine());
                    }
                }

                if (deals.Count == 0)
                {
                    return;
                }

                if (_startProgram == StartProgram.IsTester)
                {
                    return;
                }

                List<Position> positions = new List<Position>();

                int i = 0;
                foreach (string deal in deals)
                {
                    try
                    {
                        positions.Add(new Position());
                        positions[i].SetDealFromString(deal);
                        UpdateOpenPositionArray(positions[i], false);
                    }
                    catch (Exception error)
                    {
                        SendNewLogMessage("ERROR on loading position " + error.ToString(), LogMessageType.Error);
                        positions.Remove(positions[i]);
                        i--;
                    }

                    i++;
                }

                _deals = positions;

                if (_deals != null && _deals.Count != 0)
                {
                    for (int i2 = 0; i2 < _deals.Count; i2++)
                    {
                        ProcessPosition(_deals[i2]);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Delete()
        {
            try
            {
                if (_startProgram == StartProgram.IsOsOptimizer
                    || _startProgram == StartProgram.IsTester)
                {
                    return;
                }

                _needToSave = false;
                string dealControllerPath = @"Engine\" + _name + @"DealController.txt";

                if (File.Exists(dealControllerPath))
                {
                    try
                    {
                        File.Delete(dealControllerPath);
                    }
                    catch (Exception error)
                    {
                        SendNewLogMessage(error.ToString(), LogMessageType.System);
                    }
                }

                string dealControllerStopLimitsPath = @"Engine\" + _name + @"DealControllerStopLimits.txt";

                if (File.Exists(dealControllerStopLimitsPath))
                {
                    try
                    {
                        File.Delete(dealControllerStopLimitsPath);
                    }
                    catch (Exception error)
                    {
                        SendNewLogMessage(error.ToString(), LogMessageType.System);
                    }
                }

                if (_gridOpenDeal != null)
                {
                    _gridOpenDeal.Click -= _gridOpenDeal_Click;
                    _gridOpenDeal.DataError -= _gridOpenDeal_DataError;
                    _gridOpenDeal = null;
                }
                if (_gridCloseDeal != null)
                {
                    _gridCloseDeal.Click -= _gridCloseDeal_Click;
                    _gridCloseDeal.DataError -= _gridOpenDeal_DataError;
                    _gridCloseDeal = null;
                }

                if (_startProgram != StartProgram.IsOsOptimizer)
                {
                    for (int i = 0; i < ControllersToCheck.Count; i++)
                    {
                        if (ControllersToCheck[i] == null)
                        {
                            ControllersToCheck.RemoveAt(i);
                            i--;
                            continue;
                        }
                        if (ControllersToCheck[i]._name == _name)
                        {
                            ControllersToCheck.RemoveAt(i);
                            return;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Clear()
        {
            try
            {
                List<Position> deals = _deals;

                _deals = new List<Position>();

                for (int i = 0; deals != null && i < deals.Count; i++)
                {
                    ProcessPosition(deals[i]);
                }

                _openPositions = new List<Position>();
                _openLongChanged = true;
                _openShortChanged = true;
                _closePositionChanged = true;
                _closeShortChanged = true;
                _closeLongChanged = true;
                _positionsToPaint = new List<Position>();
                ClearPositionsGrid();
                _needToSave = true;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public CommissionType CommissionType
        {
            get { return _commissionType; }
            set
            {
                if (value == _commissionType)
                {
                    return;
                }
                _commissionType = value;

                for (int i = 0; AllPositions != null && i < AllPositions.Count; i++)
                {
                    AllPositions[i].CommissionType = _commissionType;
                }

                _needToSave = true;
            }
        }
        private CommissionType _commissionType;

        public decimal CommissionValue
        {
            get { return _commissionValue; }
            set
            {
                if (value == _commissionValue)
                {
                    return;
                }
                _commissionValue = value;


                for (int i = 0; AllPositions != null && i < AllPositions.Count; i++)
                {
                    AllPositions[i].CommissionValue = _commissionValue;
                }

                _needToSave = true;
            }

        }
        private decimal _commissionValue;

        private bool _needToSave;

        private void SavePositions()
        {
            if (!_needToSave)
            {
                return;
            }

            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            _needToSave = false;

            try
            {
                string saveString = GetSaveString();

                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"DealController.txt", false))
                {
                    writer.Write(saveString);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private string GetSaveString()
        {
            StringBuilder result = new StringBuilder();

            result.Append(_commissionType + "\r\n");
            result.Append(_commissionValue + "\r\n");

            if (_startProgram == StartProgram.IsOsTrader)
            {
                List<Position> deals = _deals;

                for (int i = 0; deals != null && i < deals.Count; i++)
                {
                    Position pos = deals[i];

                    if (pos == null)
                    {
                        continue;
                    }

                    result.Append(deals[i].GetStringForSave() + "\r\n");
                }
            }

            return result.ToString();
        }

        public void Save()
        {
            _needToSave = true;
        }

        public void NeedToUpdateStatePositions()
        {
            for (int i = 0; i < _deals.Count; i++)
            {
                if (_deals[i] == null)
                {
                    _deals.RemoveAt(i);
                    i--;
                    continue;
                }

                UpdateOpenPositionArray(_deals[i]);
            }

            _openLongChanged = true;
            _openShortChanged = true;
            _closePositionChanged = true;
            _closeShortChanged = true;
            _closeLongChanged = true;
        }

        #endregion

        #region Working with a position

        private List<Position> _deals;

        private string _dealsLocker = "_dealsLocker";

        public void SetNewPosition(Position newPosition)
        {
            if (newPosition == null)
            {
                return;
            }

            // saving
            // сохраняем

            newPosition.CommissionType = CommissionType;
            newPosition.CommissionValue = CommissionValue;

            lock (_dealsLocker)
            {
                if (_deals == null)
                {
                    _deals = new List<Position>();
                    _deals.Add(newPosition);
                }
                else
                {
                    _deals.Add(newPosition);
                }

                for (int i = 0; i < _deals.Count; i++)
                {
                    if (_deals[i] == null)
                    {
                        _deals.RemoveAt(i);
                        i--;
                    }
                }

                _openPositions.Add(newPosition);
            }

            ProcessPosition(newPosition);
            _lastPositionChange = true;

            if (newPosition.Direction == Side.Buy)
            {
                _openLongChanged = true;
            }
            else
            {
                _openShortChanged = true;
            }

            _needToSave = true;
        }

        public void DeletePosition(Position position)
        {
            if (_deals == null || _deals.Count == 0)
            {
                return;
            }

            if (position == null)
            {
                return;
            }

            // убираем в общем хранилище

            lock (_dealsLocker)
            {
                for (int i = 0; i < _deals.Count; i++)
                {
                    if (_deals[i].Number == position.Number)
                    {
                        _deals.RemoveAt(i);
                        break;
                    }
                }

                // убираем в хранилищах открытых позиций

                for (int i = 0; i < _openPositions.Count; i++)
                {
                    if (_openPositions[i].Number == position.Number)
                    {
                        _openPositions.RemoveAt(i);
                        break;
                    }
                }

                for (int i = 0; _openLongPosition != null && i < _openLongPosition.Count; i++)
                {
                    if (_openLongPosition[i].Number == position.Number)
                    {
                        _openLongPosition.RemoveAt(i);
                        break;
                    }
                }

                for (int i = 0; _openShortPositions != null && i < _openShortPositions.Count; i++)
                {
                    if (_openShortPositions[i].Number == position.Number)
                    {
                        _openShortPositions.RemoveAt(i);
                        break;
                    }
                }

                // убираем из хранилищь закрытых позиций

                for (int i = 0; _closePositions != null && i < _closePositions.Count; i++)
                {
                    if (_closePositions[i].Number == position.Number)
                    {
                        _closePositions.RemoveAt(i);
                        break;
                    }
                }

                for (int i = 0; _closeLongPositions != null && i < _closeLongPositions.Count; i++)
                {
                    if (_closeLongPositions[i].Number == position.Number)
                    {
                        _closeLongPositions.RemoveAt(i);
                        break;
                    }
                }

                for (int i = 0; _closeShortPositions != null && i < _closeShortPositions.Count; i++)
                {
                    if (_closeShortPositions[i].Number == position.Number)
                    {
                        _closeShortPositions.RemoveAt(i);
                        break;
                    }
                }
            }

            _openLongChanged = true;
            _openShortChanged = true;
            _closePositionChanged = true;
            _closeShortChanged = true;
            _closeLongChanged = true;

            ProcessPosition(position);

            _needToSave = true;
        }

        public void SetUpdateOrderInPositions(Order updateOrder)
        {
            if (_deals == null)
            {
                return;
            }

            lock (_dealsLocker)
            {
                for (int i = _deals.Count - 1; i > -1; i--)
                {
                    Position curPosition = null;

                    try
                    {
                        curPosition = _deals[i];
                    }
                    catch
                    {
                        continue;
                    }

                    if (curPosition == null)
                    {
                        continue;
                    }

                    bool isCloseOrder = false;

                    if (curPosition.CloseOrders != null && curPosition.CloseOrders.Count > 0)
                    {
                        for (int indexCloseOrders = 0; indexCloseOrders < curPosition.CloseOrders.Count; indexCloseOrders++)
                        {
                            if (curPosition.CloseOrders[indexCloseOrders].NumberUser == updateOrder.NumberUser)
                            {
                                isCloseOrder = true;
                                break;
                            }
                        }
                    }

                    bool isOpenOrder = false;

                    if (isCloseOrder == false ||
                        curPosition.OpenOrders != null && curPosition.OpenOrders.Count > 0)
                    {
                        for (int indexOpenOrd = 0; curPosition.OpenOrders != null && indexOpenOrd < curPosition.OpenOrders.Count; indexOpenOrd++)
                        {
                            if (curPosition.OpenOrders[indexOpenOrd] == null)
                            {
                                continue;
                            }

                            if (curPosition.OpenOrders[indexOpenOrd].NumberUser == updateOrder.NumberUser)
                            {
                                isOpenOrder = true;
                                break;
                            }
                        }
                    }

                    if (isOpenOrder || isCloseOrder)
                    {
                        PositionStateType positionState = curPosition.State;
                        decimal lastPosVolume = curPosition.OpenVolume;

                        curPosition.SetOrder(updateOrder);

                        if (positionState != curPosition.State ||
                            lastPosVolume != curPosition.OpenVolume)
                        {
                            _openLongChanged = true;
                            _openShortChanged = true;
                            _closePositionChanged = true;
                            _closeShortChanged = true;
                            _closeLongChanged = true;

                            UpdateOpenPositionArray(curPosition);
                        }

                        if (positionState != curPosition.State && PositionStateChangeEvent != null)
                        {
                            PositionStateChangeEvent(curPosition);
                        }

                        if (lastPosVolume != curPosition.OpenVolume && PositionNetVolumeChangeEvent != null)
                        {
                            PositionNetVolumeChangeEvent(curPosition);
                        }

                        if (i < _deals.Count)
                        {
                            ProcessPosition(curPosition);
                        }

                        break;
                    }
                }
            }
            _needToSave = true;
        }

        public bool SetNewTrade(MyTrade trade)
        {
            if (_deals == null)
            {
                return false;
            }

            bool isMyTrade = false;

            lock (_dealsLocker)
            {
                for (int i = _deals.Count - 1; i > -1; i--)
                {
                    Position position = _deals[i];

                    if (position == null)
                    {
                        continue;
                    }

                    bool isCloseOrder = false;

                    if (position.CloseOrders != null)
                    {
                        for (int indexCloseOrd = 0; indexCloseOrd < position.CloseOrders.Count; indexCloseOrd++)
                        {
                            Order closeOrder = position.CloseOrders[indexCloseOrd];

                            if (closeOrder == null)
                            {
                                continue;
                            }

                            if (closeOrder.NumberMarket == trade.NumberOrderParent)
                            {
                                isCloseOrder = true;
                                break;
                            }
                        }
                    }
                    bool isOpenOrder = false;

                    if (isCloseOrder == false &&
                        position.OpenOrders != null
                        && position.OpenOrders.Count > 0)
                    {
                        for (int indOpenOrd = 0; indOpenOrd < position.OpenOrders.Count; indOpenOrd++)
                        {
                            Order openOrder = position.OpenOrders[indOpenOrd];

                            if (openOrder == null)
                            {
                                continue;
                            }

                            if (openOrder.NumberMarket == trade.NumberOrderParent)
                            {
                                isOpenOrder = true;
                                break;
                            }
                        }
                    }

                    if (isOpenOrder || isCloseOrder)
                    {
                        isMyTrade = true;

                        PositionStateType positionState = position.State;

                        decimal lastPosVolume = position.OpenVolume;

                        position.SetTrade(trade);

                        if (positionState != position.State ||
                            lastPosVolume != position.OpenVolume)
                        {
                            UpdateOpenPositionArray(position);
                            _openLongChanged = true;
                            _openShortChanged = true;
                            _closePositionChanged = true;
                            _closeShortChanged = true;
                            _closeLongChanged = true;
                        }

                        if (positionState != position.State && PositionStateChangeEvent != null)
                        {
                            PositionStateChangeEvent(position);
                        }

                        if (lastPosVolume != position.OpenVolume && PositionNetVolumeChangeEvent != null)
                        {
                            PositionNetVolumeChangeEvent(position);
                        }

                        ProcessPosition(position);
                        break;
                    }
                }
            }

            if (isMyTrade)
            {
                _needToSave = true;
                return true;
            }

            return false;
        }

        public void SetBidAsk(decimal bid, decimal ask)
        {
            if (_deals == null)
            {
                return;
            }

            List<Position> positions = OpenPositions;

            if (positions == null ||
                positions.Count == 0)
            {
                return;
            }

            if (_startProgram != StartProgram.IsOsOptimizer)
            {
                for (int i = positions.Count - 1; i > -1; i--)
                {
                    Position pos = positions[i];

                    if (pos == null)
                    {
                        continue;
                    }

                    if (pos.State == PositionStateType.Open
                        || pos.State == PositionStateType.Closing
                        || pos.State == PositionStateType.ClosingFail)
                    {
                        decimal profitOld = pos.ProfitOperationAbs;

                        pos.SetBidAsk(bid, ask);

                        if (profitOld != pos.ProfitOperationAbs)
                        {
                            ProcessPosition(pos);
                        }
                    }
                }
            }
        }

        private List<Position> _emptyList = new List<Position>();

        #endregion

        #region StopLimits

        public void SetStopLimits(List<PositionOpenerToStopLimit> stopLimits)
        {
            _actualStopLimits = stopLimits;
            _needToSaveStopLimit = true;
        }

        private List<PositionOpenerToStopLimit> _actualStopLimits;

        private bool _needToSaveStopLimit;

        private void TrySaveStopLimits()
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if (_needToSaveStopLimit == false)
            {
                return;
            }

            _needToSaveStopLimit = false;

            try
            {
                if (_actualStopLimits == null
                   || _actualStopLimits.Count == 0)
                { // очищаем файл от записей
                    using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"DealControllerStopLimits.txt", false))
                    {

                    }
                    return;
                }

                string positionsString = "";

                for (int i = 0; i < _actualStopLimits.Count; i++)
                {
                    if (_actualStopLimits[i].LifeTimeType == PositionOpenerToStopLifeTimeType.NoLifeTime)
                    {
                        positionsString += _actualStopLimits[i].GetSaveString() + "\n";
                    }
                }

                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"DealControllerStopLimits.txt", false))
                {
                    writer.Write(positionsString);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        public List<PositionOpenerToStopLimit> LoadStopLimits()
        {
            try
            {
                if (_startProgram != StartProgram.IsOsTrader)
                {
                    return null;
                }

                if (File.Exists(@"Engine\" + _name + @"DealControllerStopLimits.txt") == false)
                {
                    return null;
                }

                // 1 count the number of transactions in the file
                //1 считаем кол-во сделок в файле

                List<string> orders = new List<string>();



                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"DealControllerStopLimits.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        orders.Add(reader.ReadLine());
                    }
                }

                List<PositionOpenerToStopLimit> stopLimits = new List<PositionOpenerToStopLimit>();

                for (int i = 0; i < orders.Count; i++)
                {
                    string str = orders[i];

                    if (String.IsNullOrEmpty(str))
                    {
                        continue;
                    }

                    PositionOpenerToStopLimit stop = new PositionOpenerToStopLimit();
                    stop.LoadFromString(str);
                    stopLimits.Add(stop);
                }

                return stopLimits;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region Last position

        private bool _lastPositionChange = true;

        public Position LastPosition
        {
            get
            {
                if (_lastPositionChange)
                {
                    if (_deals != null && _deals.Count != 0)
                    {
                        _lastPosition = _deals[_deals.Count - 1];
                    }
                    else
                    {
                        _lastPosition = null;
                    }
                    _lastPositionChange = false;
                }

                return _lastPosition;

            }
        }
        private Position _lastPosition;

        #endregion

        #region Open positions

        public List<Position> OpenPositions
        {
            get
            {
                return _openPositions;
            }
        }
        private List<Position> _openPositions = new List<Position>();

        private void UpdateOpenPositionArray(Position position, bool checkNum = true)
        {
            if (position.State != PositionStateType.Done && position.State != PositionStateType.OpeningFail)
            {
                // then the open position
                // это открытая позиция
                if (checkNum == true)
                {
                    if (_openPositions.Find(pos => pos != null && pos.Number == position.Number) == null)
                    {
                        _openPositions.Add(position);
                    }
                }
                else
                {
                    _openPositions.Add(position);
                }
            }
            else
            {
                // closed
                // закрытая
                if (_openPositions.Find(pos => pos != null && pos.Number == position.Number) != null)
                {
                    _openPositions.Remove(position);
                }
            }
        }

        #endregion

        #region Open longs

        private bool _openLongChanged = true;

        public List<Position> OpenLongPosition
        {
            get
            {
                if (_openLongChanged)
                {
                    _openLongChanged = false;

                    if (_deals != null && _deals.Count != 0)
                    {
                        _openLongPosition = _deals.FindAll(
                            position => position != null
                                        && (position.State != PositionStateType.Done
                                        && position.State != PositionStateType.OpeningFail
                                        && position.Direction == Side.Buy)
                            );
                    }
                    else
                    {
                        _openLongPosition = null;
                    }
                }

                if (_openLongPosition == null)
                {
                    return _emptyList;
                }
                return _openLongPosition;
            }
        }
        private List<Position> _openLongPosition;

        #endregion

        #region Open shorts

        private bool _openShortChanged = true;

        public List<Position> OpenShortPosition
        {
            get
            {
                if (_openShortChanged)
                {
                    _openShortChanged = false;

                    if (_deals != null && _deals.Count != 0)
                    {
                        _openShortPositions = _deals.FindAll(
                            position => position != null
                                        && (position.State != PositionStateType.Done
                                        && position.State != PositionStateType.OpeningFail
                                        && position.Direction == Side.Sell)
                            );
                    }
                    else
                    {
                        _openShortPositions = null;
                    }
                }
                if (_openShortPositions == null)
                {
                    return _emptyList;
                }
                return _openShortPositions;
            }
        }
        private List<Position> _openShortPositions;

        #endregion

        #region Closed positions

        private bool _closePositionChanged = true;

        public List<Position> ClosePositions
        {
            get
            {
                if (_closePositionChanged)
                {
                    _closePositionChanged = false;

                    if (_deals != null && _deals.Count != 0)
                    {
                        _closePositions = _deals.FindAll(
                            position => position != null
                                        && (position.State == PositionStateType.Done
                                        || position.State == PositionStateType.OpeningFail));
                    }
                    else
                    {
                        _closePositions = null;
                    }
                }
                if (_closePositions == null)
                {
                    return _emptyList;
                }
                return _closePositions;
            }
        }
        private List<Position> _closePositions;

        #endregion

        #region Closed longs

        private bool _closeLongChanged = true;

        public List<Position> CloseLongPosition
        {
            get
            {
                if (_closeLongChanged)
                {
                    _closeLongChanged = false;

                    if (_deals != null && _deals.Count != 0)
                    {
                        _closeLongPositions = _deals.FindAll(
                            position => position != null && ((position.State == PositionStateType.Done
                                         || position.State == PositionStateType.OpeningFail)
                                        && position.Direction == Side.Buy));
                    }
                    else
                    {
                        _closeLongPositions = null;
                    }
                }
                if (_closeLongPositions == null)
                {
                    return _emptyList;
                }
                return _closeLongPositions;
            }
        }
        private List<Position> _closeLongPositions;

        #endregion

        #region Closed shorts

        private bool _closeShortChanged = true;

        public List<Position> CloseShortPosition
        {
            get
            {
                if (_closeShortChanged)
                {
                    _closeShortChanged = false;

                    if (_deals != null && _deals.Count != 0)
                    {
                        _closeShortPositions = _deals.FindAll(
                            position => position != null && ((position.State == PositionStateType.Done
                                         || position.State == PositionStateType.OpeningFail)
                                        && position.Direction == Side.Sell)
                            );
                    }
                    else
                    {
                        _closeShortPositions = null;
                    }
                }
                if (_closeShortPositions == null)
                {
                    return _emptyList;
                }
                return _closeShortPositions;
            }
        }
        private List<Position> _closeShortPositions;

        #endregion

        #region All positions

        public List<Position> AllPositions
        {
            get
            {
                if (_deals == null)
                {
                    return _emptyList;
                }
                return _deals;
            }
        }

        public Position GetPositionForNumber(int number)
        {
            return _deals.Find(position => position != null && position.Number == number);
        }

        #endregion

        #region Drawing of positions in the tables

        private void TryPaintPositions()
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                if (_hostCloseDeal == null ||
                    _hostOpenDeal == null ||
                    _positionsToPaint.Count == 0)
                {
                    return;
                }

                if (_gridOpenDeal == null ||
                    _gridCloseDeal == null)
                {
                    return;
                }


                try
                {
                    while (_positionsToPaint.Count != 0)
                    {
                        Position newElement = _positionsToPaint[0];

                        if (newElement != null)
                        {
                            PaintPosition(newElement);
                        }
                        _positionsToPaint.RemoveAt(0);
                    }

                    Sort(_gridOpenDeal);
                    Sort(_gridCloseDeal);
                }
                catch
                {
                    // ignore
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<Position> _positionsToPaint = new List<Position>();

        private void CreateTable()
        {
            _gridOpenDeal = CreateNewTable();
            _gridCloseDeal = CreateNewTable();

            _gridCloseDeal.ScrollBars = ScrollBars.Vertical;
            _gridOpenDeal.Click += _gridOpenDeal_Click;
            _gridOpenDeal.DataError += _gridOpenDeal_DataError;
            _gridCloseDeal.Click += _gridCloseDeal_Click;
            _gridCloseDeal.DataError += _gridOpenDeal_DataError;
        }

        private void _gridOpenDeal_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = DataGridFactory.GetDataGridPosition();
                newGrid.ScrollBars = ScrollBars.Vertical;

                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private void ClearPositionsGrid()
        {
            if (_gridOpenDeal == null)
            {
                return;
            }
            if (_gridOpenDeal.InvokeRequired)
            {
                _gridOpenDeal.Invoke(new Action(ClearPositionsGrid));
                return;
            }

            if (_gridOpenDeal != null)
            {
                _gridOpenDeal.Rows.Clear();
            }
            else if (_gridCloseDeal != null)
            {
                _gridCloseDeal.Rows.Clear();
            }
        }

        private void Sort(DataGridView grid)
        {
            try
            {
                if (grid == null)
                {
                    return;
                }

                if (grid.InvokeRequired)
                {
                    grid.Invoke(new Action<DataGridView>(Sort), grid);
                    return;
                }

                bool needToSort = false;

                for (int i = 1; i < grid.Rows.Count; i++)
                {
                    if (grid.Rows[i].Cells[0].Value == null
                        || grid.Rows[i - 1].Cells[0].Value == null)
                    {
                        continue;
                    }

                    int numCur = Convert.ToInt32(grid.Rows[i].Cells[0].Value.ToString());
                    int numPrev = Convert.ToInt32(grid.Rows[i - 1].Cells[0].Value.ToString());

                    if (numCur > numPrev)
                    {
                        needToSort = true;
                        break;
                    }
                }

                if (needToSort == false)
                {
                    return;
                }

                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                rows.Add(grid.Rows[0]);

                for (int i = 1; i < grid.Rows.Count; i++)
                {
                    DataGridViewRow curRow = grid.Rows[i];

                    int numCur = Convert.ToInt32(grid.Rows[i].Cells[0].Value.ToString());

                    bool isInArray = false;

                    for (int i2 = 0; i2 < rows.Count; i2++)
                    {
                        int numCurInRowsGrid = Convert.ToInt32(rows[i2].Cells[0].Value.ToString());

                        if (numCur > numCurInRowsGrid)
                        {
                            rows.Insert(i2, curRow);
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        rows.Add(curRow);
                    }
                }

                grid.Rows.Clear();
                grid.Rows.AddRange(rows.ToArray());
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private WindowsFormsHost _hostOpenDeal;

        private WindowsFormsHost _hostCloseDeal;

        private DataGridView _gridOpenDeal;

        private DataGridView _gridCloseDeal;

        public bool CanShowToolStripMenu = true;

        public void StartPaint(WindowsFormsHost dataGridOpenDeal, WindowsFormsHost dataGridCloseDeal)
        {
            _hostCloseDeal = dataGridCloseDeal;
            if (!_hostCloseDeal.Dispatcher.CheckAccess())
            {
                _hostCloseDeal.Dispatcher.Invoke(new Action<WindowsFormsHost, WindowsFormsHost>(StartPaint), dataGridOpenDeal, dataGridCloseDeal);
                return;
            }

            CreateTable();

            if (_positionsToPaint == null)
            {
                _positionsToPaint = new List<Position>();
            }

            for (int i = 0; i < AllPositions.Count; i++)
            {
                _positionsToPaint.Add(AllPositions[i]);
            }

            _hostOpenDeal = dataGridOpenDeal;
            _hostCloseDeal = dataGridCloseDeal;

            _hostCloseDeal.Child = _gridCloseDeal;
            _hostOpenDeal.Child = _gridOpenDeal;
        }

        public void StopPaint()
        {
            try
            {
                if (_hostCloseDeal == null)
                {
                    return;
                }

                if (!_hostCloseDeal.Dispatcher.CheckAccess())
                {
                    _hostCloseDeal.Dispatcher.Invoke(StopPaint);
                    return;
                }

                if (_hostCloseDeal != null)
                {
                    _hostCloseDeal.Child = null;
                    _hostOpenDeal.Child = null;
                    _hostOpenDeal = null;
                    _hostCloseDeal = null;
                }

                _positionsToPaint.Clear();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintPosition(Position position)
        {
            try
            {
                if (_gridOpenDeal == null)
                {
                    return;
                }

                if (_gridOpenDeal.InvokeRequired)
                {
                    _gridOpenDeal.Invoke(new Action<Position>(PaintPosition), position);
                    return;
                }

                if (_deals == null || _deals.Count == 0 ||
                    _deals.Find(position1 => position1.Number == position.Number) == null)
                {
                    // The deal has been removed. Need to remove it from everywhere
                    // сделка была удалена. Надо её удалить отовсюду
                    for (int i = 0; i < _gridOpenDeal.Rows.Count; i++)
                    {
                        if ((int)_gridOpenDeal.Rows[i].Cells[0].Value == position.Number)
                        {
                            _gridOpenDeal.Rows.Remove(_gridOpenDeal.Rows[i]);
                            return;
                        }
                    }

                    for (int i = 0; i < _gridCloseDeal.Rows.Count; i++)
                    {
                        if ((int)_gridCloseDeal.Rows[i].Cells[0].Value == position.Number)
                        {
                            _gridCloseDeal.Rows.Remove(_gridCloseDeal.Rows[i]);
                            return;
                        }
                    }
                    return;
                }

                if (position.State == PositionStateType.Done ||
                    position.State == PositionStateType.OpeningFail)
                {
                    // The transaction should be drawn in the table of closed transactions
                    // сделкка должна быть прорисована в таблице закрытых сделок
                    for (int i = 0; i < _gridOpenDeal.Rows.Count; i++)
                    {
                        if ((int)(_gridOpenDeal.Rows[i].Cells[0].Value) == position.Number)
                        {
                            _gridOpenDeal.Rows.Remove(_gridOpenDeal.Rows[i]);
                        }
                    }

                    for (int i = 0; i < _gridCloseDeal.Rows.Count; i++)
                    {
                        if ((int)_gridCloseDeal.Rows[i].Cells[0].Value == position.Number)
                        {
                            TryRePaint(position, _gridCloseDeal.Rows[i]);

                            return;
                        }
                    }
                    _gridCloseDeal.Rows.Insert(0, GetRow(position));
                }
                else
                {
                    //  The transaction should be drawn in the table of Open Transactions
                    // сделкка должна быть прорисована в таблице Открытых сделок

                    for (int i = 0; i < _gridOpenDeal.Rows.Count; i++)
                    {
                        if ((int)_gridOpenDeal.Rows[i].Cells[0].Value == position.Number)
                        {
                            if (position.State == PositionStateType.Deleted)
                            {
                                _gridOpenDeal.Rows.Remove(_gridOpenDeal.Rows[i]);
                                return;
                            }
                            TryRePaint(position, _gridOpenDeal.Rows[i]);

                            return;
                        }
                    }

                    if (position.State != PositionStateType.Deleted)
                    {
                        _gridOpenDeal.Rows.Insert(0, GetRow(position));
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TryRePaint(Position position, DataGridViewRow nRow)
        {
            if (nRow.Cells[1].Value == null
                || nRow.Cells[1].Value.ToString() != position.TimeCreate.ToString(_currentCulture))// == false) //AVP убрал, потому что  во вкладке все позиции, дату позиции не обновляло
            {
                nRow.Cells[1].Value = position.TimeCreate.ToString(_currentCulture);
            }
            if (position.TimeClose != position.TimeOpen)
            {
                if (nRow.Cells[2].Value == null
                || nRow.Cells[2].Value.ToString() != position.TimeClose.ToString(_currentCulture))// == false) //AVP убрал потому что во вкладке все позиции, дату позиции не обновляло
                {
                    nRow.Cells[2].Value = position.TimeClose.ToString(_currentCulture);
                }
            }

            if (nRow.Cells[6].Value == null
                || nRow.Cells[6].Value.ToString() != position.State.ToString())
            {
                nRow.Cells[6].Value = position.State;
            }

            if (nRow.Cells[7].Value == null
                || nRow.Cells[7].Value.ToString() != position.MaxVolume.ToStringWithNoEndZero())
            {
                nRow.Cells[7].Value = position.MaxVolume.ToStringWithNoEndZero();
            }

            if (nRow.Cells[8].Value == null
                || nRow.Cells[8].Value.ToString() != position.OpenVolume.ToStringWithNoEndZero())
            {
                nRow.Cells[8].Value = position.OpenVolume.ToStringWithNoEndZero();
            }

            if (nRow.Cells[9].Value == null
                || nRow.Cells[9].Value.ToString() != position.WaitVolume.ToStringWithNoEndZero())
            {
                nRow.Cells[9].Value = position.WaitVolume.ToStringWithNoEndZero();
            }

            int decimalsPrice = position.PriceStep.ToStringWithNoEndZero().DecimalsCount();

            decimalsPrice++;

            decimal openPrice = Math.Round(position.EntryPrice, decimalsPrice);

            if (openPrice == 0)
            {
                if (position.OpenOrders != null &&
                    position.OpenOrders.Count != 0 &&
                    position.State != PositionStateType.OpeningFail)
                {
                    openPrice = position.OpenOrders[position.OpenOrders.Count - 1].Price;
                }
            }

            if (nRow.Cells[10].Value == null
                || nRow.Cells[10].Value.ToString() != openPrice.ToStringWithNoEndZero())
            {
                nRow.Cells[10].Value = openPrice.ToStringWithNoEndZero();
            }

            decimal closePrice = Math.Round(position.ClosePrice, decimalsPrice);

            if (closePrice == 0)
            {
                if (position.CloseOrders != null &&
                    position.CloseOrders.Count != 0 &&
                    position.State != PositionStateType.ClosingFail)
                {
                    closePrice = position.ClosePrice;
                }
            }

            if (nRow.Cells[11].Value == null
                || nRow.Cells[11].Value.ToString() != closePrice.ToStringWithNoEndZero())
            {
                nRow.Cells[11].Value = closePrice.ToStringWithNoEndZero();
            }

            decimal profit = Math.Round(position.ProfitPortfolioAbs, decimalsPrice);

            if (nRow.Cells[12].Value == null
                || nRow.Cells[12].Value.ToString() != profit.ToStringWithNoEndZero())
            {
                nRow.Cells[12].Value = profit.ToStringWithNoEndZero();
            }

            decimal stopRedLine = Math.Round(position.StopOrderRedLine, decimalsPrice);

            if (nRow.Cells[13].Value == null ||
                nRow.Cells[13].Value.ToString() != stopRedLine.ToStringWithNoEndZero())
            {
                nRow.Cells[13].Value = stopRedLine.ToStringWithNoEndZero();
            }

            decimal stopPrice = Math.Round(position.StopOrderPrice, decimalsPrice);

            if (nRow.Cells[14].Value == null
                || nRow.Cells[14].Value.ToString() != stopPrice.ToStringWithNoEndZero())
            {
                nRow.Cells[14].Value = stopPrice.ToStringWithNoEndZero();
            }

            decimal profitRedLine = Math.Round(position.ProfitOrderRedLine, decimalsPrice);

            if (nRow.Cells[15].Value == null ||
                 nRow.Cells[15].Value.ToString() != profitRedLine.ToStringWithNoEndZero())
            {
                nRow.Cells[15].Value = profitRedLine.ToStringWithNoEndZero();
            }

            decimal profitPrice = Math.Round(position.ProfitOrderPrice, decimalsPrice);

            if (nRow.Cells[16].Value == null ||
                nRow.Cells[16].Value.ToString() != profitPrice.ToStringWithNoEndZero())
            {
                nRow.Cells[16].Value = profitPrice.ToStringWithNoEndZero();
            }

            if (string.IsNullOrEmpty(position.SignalTypeOpen) == false)
            {
                if (nRow.Cells[17].Value == null
                ||
                nRow.Cells[17].Value.ToString() != position.SignalTypeOpen.ToString())
                {
                    nRow.Cells[17].Value = position.SignalTypeOpen;
                }
            }
            if (string.IsNullOrEmpty(position.SignalTypeClose) == false)
            {
                if (nRow.Cells[18].Value == null ||
                nRow.Cells[18].Value.ToString() != position.SignalTypeClose)
                {
                    nRow.Cells[18].Value = position.SignalTypeClose;
                }
            }
        }

        public void ProcessPosition(Position position)
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if (_positionsToPaint == null)
            {
                return;
            }

            try
            {
                for (int i = 0; i < _positionsToPaint.Count; i++)
                {
                    if (_positionsToPaint[i] == null)
                    {
                        continue;
                    }

                    if (_positionsToPaint[i].Number == position.Number)
                    {
                        _positionsToPaint[i] = position;
                        return;
                    }
                }

                _positionsToPaint.Add(position);

                if (_startProgram == StartProgram.IsTester)
                {
                    if (_positionsToPaint.Count > 200)
                    {
                        _positionsToPaint.RemoveAt(0);
                    }
                }
                else if (_startProgram == StartProgram.IsOsTrader)
                {
                    if (_positionsToPaint.Count > 500)
                    {
                        _positionsToPaint.RemoveAt(0);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private DataGridViewRow GetRow(Position position)
        {
            if (position == null)
            {
                return null;
            }

            try
            {
                DataGridViewRow nRow = new DataGridViewRow();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = position.Number;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = position.TimeOpen.ToString(_currentCulture);

                nRow.Cells.Add(new DataGridViewTextBoxCell());

                if (position.TimeClose != position.TimeOpen)
                {
                    nRow.Cells[2].Value = position.TimeClose.ToString(_currentCulture);
                }
                else
                {
                    nRow.Cells[2].Value = "";
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = position.NameBot;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = position.SecurityName;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = position.Direction;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = position.State;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = position.MaxVolume.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[8].Value = position.OpenVolume.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[9].Value = position.WaitVolume.ToStringWithNoEndZero();

                int decimalsPrice = position.PriceStep.ToStringWithNoEndZero().DecimalsCount();

                decimalsPrice++;

                if (position.EntryPrice != 0)
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[10].Value = Math.Round(position.EntryPrice, decimalsPrice).ToStringWithNoEndZero();
                }
                else
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    if (position.OpenOrders != null &&
                        position.OpenOrders.Count != 0 &&
                        position.State != PositionStateType.OpeningFail)
                    {
                        nRow.Cells[10].Value = Math.Round(position.OpenOrders[position.OpenOrders.Count - 1].Price, decimalsPrice).ToStringWithNoEndZero();
                    }
                }

                if (position.ClosePrice != 0)
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[11].Value = Math.Round(position.ClosePrice, decimalsPrice).ToStringWithNoEndZero();
                }
                else
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    if (position.CloseOrders != null &&
                        position.CloseOrders.Count != 0 &&
                        position.State != PositionStateType.ClosingFail)
                    {
                        nRow.Cells[11].Value = Math.Round(position.CloseOrders[position.CloseOrders.Count - 1].Price, decimalsPrice).ToStringWithNoEndZero();
                    }
                    else
                    {
                        nRow.Cells[11].Value = "0";
                    }
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[12].Value = Math.Round(position.ProfitPortfolioAbs, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[13].Value = Math.Round(position.StopOrderRedLine, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[14].Value = Math.Round(position.StopOrderPrice, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[15].Value = Math.Round(position.ProfitOrderRedLine, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[16].Value = Math.Round(position.ProfitOrderPrice, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[17].Value = position.SignalTypeOpen;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[18].Value = position.SignalTypeClose;

                return nRow;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private void _gridOpenDeal_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            if (CanShowToolStripMenu == false)
            {
                return;
            }

            try
            {
                ToolStripMenuItem[] items = new ToolStripMenuItem[6];

                items[0] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem1 };
                items[0].Click += PositionCloseAll_Click;

                items[1] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem2 };
                items[1].Click += PositionOpen_Click;

                items[2] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem3 };
                items[2].Click += PositionCloseForNumber_Click;

                items[3] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem5 };
                items[3].Click += PositionNewStop_Click;

                items[4] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem6 };
                items[4].Click += PositionNewProfit_Click;

                items[5] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem7 };
                items[5].Click += PositionClearDelete_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items);

                _gridOpenDeal.ContextMenuStrip = menu;
                _gridOpenDeal.ContextMenuStrip.Show(_gridOpenDeal, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _gridCloseDeal_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;
                if (mouse.Button != MouseButtons.Right)
                {
                    return;
                }

                if (CanShowToolStripMenu == false)
                {
                    return;
                }

                ToolStripMenuItem[] items = new ToolStripMenuItem[2];

                items[0] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem12 };
                items[0].Click += ClosedPosesGrid_PositionScrollOnChart_Click;

                items[1] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem9 };
                items[1].Click += ClosedPosesGrid_PositionDelete_Click;

                ContextMenuStrip menu = new ContextMenuStrip(); menu.Items.AddRange(items);

                _gridCloseDeal.ContextMenuStrip = menu;
                _gridCloseDeal.ContextMenuStrip.Show(_gridCloseDeal, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ClosedPosesGrid_PositionScrollOnChart_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    number = Convert.ToInt32(_gridCloseDeal.Rows[_gridCloseDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    Position pos = GetPositionForNumber(number);

                    if (pos == null
                        || pos.State == PositionStateType.OpeningFail)
                    {
                        return;
                    }


                    UserSelectActionEvent(pos, SignalType.FindPosition);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ClosedPosesGrid_PositionDelete_Click(object sender, EventArgs e)
        {
            try
            {
                int number = -1;
                try
                {
                    number = Convert.ToInt32(_gridCloseDeal.Rows[_gridCloseDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (number == -1)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.DeletePos);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Work with context menu

        private void PositionOpen_Click(object sender, EventArgs e)
        {
            try
            {
                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(null, SignalType.OpenNew);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PositionCloseAll_Click(object sender, EventArgs e)
        {
            try
            {
                if (OpenPositions == null)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message5);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(null, SignalType.CloseAll);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PositionCloseForNumber_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if (_gridOpenDeal.Rows == null ||
                        _gridOpenDeal.Rows.Count == 0 ||
                        _gridOpenDeal.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }


                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.CloseOne);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PositionNewStop_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if (_gridOpenDeal.Rows.Count == 0)
                    {
                        return;
                    }

                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.ReloadStop);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PositionNewProfit_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if (_gridOpenDeal.Rows.Count == 0)
                    {
                        return;
                    }

                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.ReloadProfit);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PositionClearDelete_Click(object sender, EventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                int number;
                try
                {
                    if (_gridOpenDeal.Rows.Count == 0)
                    {
                        return;
                    }

                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.DeletePos);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action<Position, SignalType> UserSelectActionEvent;

        #endregion

        #region Log

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region Events

        public event Action<Position> PositionStateChangeEvent;

        public event Action<Position> PositionNetVolumeChangeEvent;

        #endregion
    }
}