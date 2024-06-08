﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
using MessageBox = System.Windows.MessageBox;
using System.Globalization;

namespace OsEngine.Journal.Internal
{

    /// <summary>
    /// transaction repository
    /// хранилище сделок 
    /// </summary>
    public class PositionController
    {
        // the static part with the work of the saving position flow
        // статическая часть с работой потока сохраняющего позиции

        /// <summary>
        /// position controllers that need to be serviced
        /// контроллеры позиций которые нужно обслуживать
        /// </summary>
        public static List<PositionController> ControllersToCheck = new List<PositionController>();

        /// <summary>
        /// to activate the stream to save
        /// активировать поток для сохранения
        /// </summary>
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

        /// <summary>
        /// flow location
        /// место работы потока
        /// </summary>
        public static async void WatcherHome()
        {

            while (true)
            {
                await Task.Delay(1000);

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
        }
        // service
        // сервис
        public PositionController(string name, StartProgram startProgram)
        {
            _name = name;
            _startProgram = startProgram;

            Activate();

            if(_startProgram != StartProgram.IsOsOptimizer
                && _startProgram != StartProgram.IsOsMiner)
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

        /// <summary>
        /// name
        /// имя
        /// </summary>
        private string _name;

        /// <summary>
        ///  upload
        /// загрузить
        /// </summary>
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
                        Enum.TryParse(reader.ReadLine(), out _comissionType);
                        _comissionValue = reader.ReadLine().ToDecimal();
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
                        UpdeteOpenPositionArray(positions[i]);
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
                        ProcesPosition(_deals[i2]);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// delete
        /// удалить
        /// </summary>
        public void Delete()
        {
            try
            {
                if(_startProgram == StartProgram.IsOsOptimizer)
                {
                    return;
                }

                _neadToSave = false;
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
                    _gridOpenDeal = null;

                }
                if(_gridCloseDeal != null)
                {
                    _gridCloseDeal.Click -= _gridCloseDeal_Click;
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

        /// <summary>
        /// clear from deals
        /// очистить от сделок
        /// </summary>
        public void Clear()
        {
            try
            {
                List<Position> deals = _deals;

                _deals = new List<Position>();

                for (int i = 0; deals != null && i < deals.Count; i++)
                {
                    ProcesPosition(deals[i]);
                }

                _openPositions = new List<Position>();
                _openLongChanged = true;
                _openShortChanged = true;
                _closePositionChanged = true;
                _closeShortChanged = true;
                _closeLongChanged = true;
                _positionsToPaint = new List<Position>();
                ClearPositionsGrid();
                _neadToSave = true;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public ComissionType ComissionType
        {
            get { return _comissionType; }
            set
            {
                if (value == _comissionType)
                {
                    return;
                }
                _comissionType = value;

                for (int i = 0; AllPositions != null && i < AllPositions.Count; i++)
                {
                    AllPositions[i].ComissionType = _comissionType;
                }

                _neadToSave = true;
            }
        }
        private ComissionType _comissionType;

        public decimal ComissionValue
        {
            get { return _comissionValue; }
            set
            {
                if (value == _comissionValue)
                {
                    return;
                }
                _comissionValue = value;


                for (int i = 0; AllPositions != null && i < AllPositions.Count; i++)
                {
                    AllPositions[i].ComissionValue = _comissionValue;
                }

                _neadToSave = true;
            }

        }
        private decimal _comissionValue;

        /// <summary>
        /// is it necessary to save the data
        /// нужно ли сохранить данные
        /// </summary>
        private bool _neadToSave;

        private void SavePositions()
        {
            if (!_neadToSave)
            {
                return;
            }

            if (_startProgram == StartProgram.IsOsOptimizer
                || _startProgram == StartProgram.IsOsMiner)
            {
                return;
            }

            _neadToSave = false;

            try
            {
                string positionsString = PositionsToString();

                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"DealController.txt", false))
                {
                    writer.Write(positionsString);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private string PositionsToString()
        {
            StringBuilder result = new StringBuilder();

            result.Append(_comissionType + "\r\n");
            result.Append(_comissionValue + "\r\n");

            if (_startProgram == StartProgram.IsOsTrader ||
                _startProgram == StartProgram.IsOsMiner)
            {
                List<Position> deals = _deals;

                for (int i = 0; deals != null && i < deals.Count; i++)
                {
                    result.Append(deals[i].GetStringForSave() + "\r\n");
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// to keep the current state of positions
        /// сохранить текущее состояние позиций
        /// </summary>
        public void Save()
        {
            _neadToSave = true;
        }

        public void NeadToUpdateStatePositions()
        {
            for(int i = 0;i < _deals.Count;i++)
            {
                if(_deals[i] == null)
                {
                    _deals.RemoveAt(i);
                    i--;
                    continue;
                }

                UpdeteOpenPositionArray(_deals[i]);
            }
           
            _openLongChanged = true;
            _openShortChanged = true;
            _closePositionChanged = true;
            _closeShortChanged = true;
            _closeLongChanged = true;
        }

        // working with a position
        // работа с позицией

        /// <summary>
        /// all transactions
        /// все сделки
        /// </summary>
        private List<Position> _deals;

        /// <summary>
        /// add a deal to the storage
        /// добавить сделку в хранилище
        /// </summary>
        public void SetNewPosition(Position newPosition)
        {
            if (newPosition == null)
            {
                return;
            }
            // saving
            // сохраняем

            newPosition.ComissionType = ComissionType;
            newPosition.ComissionValue = ComissionValue;

            if (_deals == null)
            {
                _deals = new List<Position>();
                _deals.Add(newPosition);
            }
            else
            {
                _deals.Add(newPosition);
            }

            _openPositions.Add(newPosition);

            ProcesPosition(newPosition);
            _lastPositionChange = true;

            if (newPosition.Direction == Side.Buy)
            {
                _openLongChanged = true;
            }
            else
            {
                _openShortChanged = true;
            }

            _neadToSave = true;
        }

        /// <summary>
        /// delete prosition
        /// удалить позицию
        /// </summary>
        public void DeletePosition(Position position)
        {
            if (_deals == null || _deals.Count == 0)
            {
                return;
            }

            // убираем в общем хранилище

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

            for (int i = 0; _closeShortPositions != null &&  i < _closeShortPositions.Count; i++)
            {
                if (_closeShortPositions[i].Number == position.Number)
                {
                    _closeShortPositions.RemoveAt(i);
                    break;
                }
            }

            _openLongChanged = true;
            _openShortChanged = true;
            _closePositionChanged = true;
            _closeShortChanged = true;
            _closeLongChanged = true;

            ProcesPosition(position);

            _neadToSave = true;
        }

        /// <summary>
        /// load order into storage
        /// загрузить ордер в хранилище
        /// </summary>
        public void SetUpdateOrderInPositions(Order updateOrder)
        {
            if (_deals == null)
            {
                return;
            }

            for (int i = _deals.Count - 1; i > _deals.Count - 150 && i > -1; i--)
            {

                bool isCloseOrder = false;

                if (_deals[i].CloseOrders != null && _deals[i].CloseOrders.Count > 0)
                {
                    for (int indexCloseOrders = 0; indexCloseOrders < _deals[i].CloseOrders.Count; indexCloseOrders++)
                    {
                        if (_deals[i].CloseOrders[indexCloseOrders].NumberUser == updateOrder.NumberUser)
                        {
                            isCloseOrder = true;
                            break;
                        }
                    }
                }

                bool isOpenOrder = false;

                if (isCloseOrder == false ||
                    _deals[i].OpenOrders != null && _deals[i].OpenOrders.Count > 0)
                {
                    for (int indexOpenOrd = 0; indexOpenOrd < _deals[i].OpenOrders.Count; indexOpenOrd++)
                    {
                        if (_deals[i].OpenOrders[indexOpenOrd].NumberUser == updateOrder.NumberUser)
                        {
                            isOpenOrder = true;
                            break;
                        }
                    }
                }

                if (isOpenOrder || isCloseOrder)
                {
                    PositionStateType positionState = _deals[i].State;
                    decimal lastPosVolume = _deals[i].OpenVolume;

                    _deals[i].SetOrder(updateOrder);

                    if (positionState != _deals[i].State ||
                        lastPosVolume != _deals[i].OpenVolume)
                    {
                        _openLongChanged = true;
                        _openShortChanged = true;
                        _closePositionChanged = true;
                        _closeShortChanged = true;
                        _closeLongChanged = true;

                        UpdeteOpenPositionArray(_deals[i]);
                    }

                    if (positionState != _deals[i].State && PositionStateChangeEvent != null)
                    {
                        // AlertMessageManager.ThrowAlert(null, "было " + positionState + "стало" + _deals[i].State, "");
                        PositionStateChangeEvent(_deals[i]);
                    }

                    if (lastPosVolume != _deals[i].OpenVolume && PositionNetVolumeChangeEvent != null)
                    {
                        PositionNetVolumeChangeEvent(_deals[i]);
                    }

                    if (i < _deals.Count)
                    {
                        ProcesPosition(_deals[i]);
                    }

                    break;
                }
            }
            _neadToSave = true;
        }

        /// <summary>
        /// load trade in storage
        /// загрузить трейд в хранилище
        /// </summary>
        public void SetNewTrade(MyTrade trade)
        {
            if (_deals == null)
            {
                return;
            }

            for (int i = _deals.Count - 1; i > -1; i--)
            {
                Position position = _deals[i];

                bool isCloseOrder = false;

                if (position.CloseOrders != null)
                {
                    for (int indexCloseOrd = 0; indexCloseOrd < position.CloseOrders.Count; indexCloseOrd++)
                    {
                        if (position.CloseOrders[indexCloseOrd].NumberMarket == trade.NumberOrderParent 
                            //|| position.CloseOrders[indexCloseOrd].NumberUser.ToString() == trade.NumberOrderParent
                            )
                        {
                            isCloseOrder = true;
                            break;
                        }
                    }
                }
                bool isOpenOrder = false;

                if (isCloseOrder == false ||
                    position.OpenOrders != null && position.OpenOrders.Count > 0)
                {
                    for (int indOpenOrd = 0; indOpenOrd < position.OpenOrders.Count; indOpenOrd++)
                    {
                        if (position.OpenOrders[indOpenOrd].NumberMarket == trade.NumberOrderParent 
                            //|| position.OpenOrders[indOpenOrd].NumberUser.ToString() == trade.NumberOrderParent
                            )
                        {
                            isOpenOrder = true;
                            break;
                        }
                    }
                }

                if (isOpenOrder || isCloseOrder)
                {
                    PositionStateType positionState = position.State;

                    decimal lastPosVolume = position.OpenVolume;

                    position.SetTrade(trade);

                    if (positionState != position.State ||
                        lastPosVolume != position.OpenVolume)
                    {
                        UpdeteOpenPositionArray(position);
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

                    ProcesPosition(position);
                    break;
                }
            }
            _neadToSave = true;
        }

        /// <summary>
        /// upload latest market data to storage
        /// загрузить последние рыночные данные в хранилище
        /// </summary>
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

            if(_startProgram != StartProgram.IsOsOptimizer)
            {
                for (int i = positions.Count - 1; i > -1; i--)
                {
                    if (positions[i].State == PositionStateType.Open 
                        || positions[i].State == PositionStateType.Closing
                        || positions[i].State == PositionStateType.ClosingFail)
                    {
                        decimal profitOld = positions[i].ProfitOperationPunkt;

                        positions[i].SetBidAsk(bid, ask);

                        if (profitOld != positions[i].ProfitOperationPunkt)
                        {
                            ProcesPosition(positions[i]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// an empty sheet that we return instead of null when requesting arrays
        /// пустой лист который мы возвращаем вместо null при запросе массивов
        /// </summary>
        private List<Position> _emptyList = new List<Position>();

        #region StopLimits

        public void SetStopLimits(List<PositionOpenerToStopLimit> stopLimits)
        {
            _actualStopLimits = stopLimits;
            _neadToSaveStopLimit = true;
        }

        private List<PositionOpenerToStopLimit> _actualStopLimits;

        private bool _neadToSaveStopLimit;

        private void TrySaveStopLimits()
        {
            if (_startProgram == StartProgram.IsOsOptimizer
           || _startProgram == StartProgram.IsOsMiner)
            {
                return;
            }

            if (_neadToSaveStopLimit == false)
            {
                return;
            }

            _neadToSaveStopLimit = false;



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

                for(int i = 0;i < _actualStopLimits.Count;i++)
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
                if(_startProgram != StartProgram.IsOsTrader)
                {
                    return null;
                }

                if(File.Exists(@"Engine\" + _name + @"DealControllerStopLimits.txt") == false)
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

                for (int i = 0;i < orders.Count;i++)
                {
                    string str = orders[i];

                    if(String.IsNullOrEmpty(str))
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

        // last position последняя позиция

        /// <summary>
        /// last position has changed
        /// изменилась ли последняя позиция
        /// </summary>
        private bool _lastPositionChange = true;

        private Position _lastPosition;

        /// <summary>
        /// take the last position
        /// взять последную позицию
        /// </summary>
        /// <returns></returns>
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
        // open positions
        // открытые позиции

        private List<Position> _openPositions = new List<Position>();
        /// <summary>
        /// take out open deals
        /// взять открытые сделки
        /// </summary>
        public List<Position> OpenPositions
        {
            get
            {
                return _openPositions;
            }
        }

        private void UpdeteOpenPositionArray(Position position)
        {
            if (position.State != PositionStateType.Done && position.State != PositionStateType.OpeningFail)
            {
                // then the open position
                // это открытая позиция
                if (_openPositions.Find(pos => pos.Number == position.Number) == null)
                {
                    _openPositions.Add(position);
                }
            }
            else
            {
                // closed
                // закрытая
                if (_openPositions.Find(pos => pos.Number == position.Number) != null)
                {
                    _openPositions.Remove(position);
                }
            }
        }
        // open longs
        // открытые лонги

        private bool _openLongChanged = true;

        private List<Position> _openLongPosition;

        /// <summary>
        /// to take open deals in Long
        /// взять открытые сделки в Лонг
        /// </summary>
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
                            position => position.State != PositionStateType.Done
                                        && position.State != PositionStateType.OpeningFail
                                        && position.Direction == Side.Buy
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
        // open shorts
        // открытые шорты

        private bool _openShortChanged = true;

        private List<Position> _openShortPositions;

        /// <summary>
        /// to take open deals in Short
        /// взять открытые сделки в Шорт
        /// </summary>
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
                            position => position.State != PositionStateType.Done
                                        && position.State != PositionStateType.OpeningFail
                                        && position.Direction == Side.Sell
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
        // closed positions
        // закрытые позиции

        private bool _closePositionChanged = true;

        private List<Position> _closePositions;
        /// <summary>
        /// take closed deals
        /// взять закрытые сделки
        /// </summary>
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
                            position => position.State == PositionStateType.Done
                                        || position.State == PositionStateType.OpeningFail);
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
        // closed longs
        // закрытые лонги

        private bool _closeLongChanged = true;

        private List<Position> _closeLongPositions;

        /// <summary>
        /// to take closed deals to Long
        /// взять закрытые сделки в Лонг
        /// </summary>
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
                            position => (position.State == PositionStateType.Done
                                         || position.State == PositionStateType.OpeningFail)
                                        && position.Direction == Side.Buy);
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
        // closed shorts
        // закрытые шорты

        private bool _closeShortChanged = true;

        private List<Position> _closeShortPositions;

        /// <summary>
        /// to take closed deals in Short
        /// взять закрытые сделки в Шорт
        /// </summary>
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
                            position => (position.State == PositionStateType.Done
                                         || position.State == PositionStateType.OpeningFail)
                                        && position.Direction == Side.Sell
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
        // all positions
        // все позиции

        /// <summary>
        /// to take all the deals
        /// взять все сделки
        /// </summary>
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

        /// <summary>
        /// to get a deal on the number
        /// взять сделку по номеру
        /// </summary>
        public Position GetPositionForNumber(int number)
        {
            return _deals.Find(position => position.Number == number);
        }

        // Drawing of positions in the tables
        // прорисовка позиций в таблицах

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

                if(_gridOpenDeal == null ||
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

        object _positionPaintLocker = new object();

        private List<Position> _positionsToPaint = new List<Position>();

        /// <summary>
        /// create tables to draw positions
        /// создать таблицы для прорисовки позиций
        /// </summary>
        private void CreateTable()
        {
            _gridOpenDeal = CreateNewTable();
            _gridCloseDeal = CreateNewTable();

            _gridCloseDeal.ScrollBars = ScrollBars.Vertical;
            _gridOpenDeal.Click += _gridOpenDeal_Click;
            _gridCloseDeal.Click += _gridCloseDeal_Click;
        }

        /// <summary>
        /// create a table
        /// создать таблицу
        /// </summary>
        /// <returns>table for drawing positions on it/таблица для прорисовки на ней позиций</returns>
        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = DataGridFactory.GetDataGridPosition();

                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// clear the tables of trades
        /// очистить таблицы от сделок
        /// </summary>
        private void ClearPositionsGrid()
        {
            if(_gridOpenDeal == null)
            {
                return;
            }
            if (_gridOpenDeal.InvokeRequired)
            {
                _gridOpenDeal.Invoke(new Action(ClearPositionsGrid));
                return;
            }

            if(_gridOpenDeal != null)
            {
                _gridOpenDeal.Rows.Clear();
            }
            else if(_gridCloseDeal != null)
            {
                _gridCloseDeal.Rows.Clear();
            }
        }

        /// <summary>
        /// open trade field
        /// поле для открытых сделок
        /// </summary>
        private WindowsFormsHost _hostOpenDeal;

        /// <summary>
        /// closed trades field
        /// поле для закрытых сделок
        /// </summary>
        private WindowsFormsHost _hostCloseDeal;

        /// <summary>
        /// open trade field
        /// поле для открытых сделок
        /// </summary>
        private DataGridView _gridOpenDeal;

        /// <summary>
        /// closed trades field
        /// поле для закрытых сделок
        /// </summary>
        private DataGridView _gridCloseDeal;

        /// <summary>
        /// enable data drawing
        /// включить прорисовку данных
        /// </summary>
        public void StartPaint(WindowsFormsHost dataGridOpenDeal, WindowsFormsHost dataGridCloseDeal)
        {
            _hostCloseDeal = dataGridCloseDeal;
            if (!_hostCloseDeal.Dispatcher.CheckAccess())
            {
                _hostCloseDeal.Dispatcher.Invoke(new Action<WindowsFormsHost, WindowsFormsHost>(StartPaint), dataGridOpenDeal, dataGridCloseDeal);
                return;
            }

            CreateTable();

            if(_positionsToPaint == null)
            {
                _positionsToPaint = new List<Position>();
            }

            for(int i = 0;i < AllPositions.Count;i++)
            {
                _positionsToPaint.Add(AllPositions[i]);
            }

            _hostOpenDeal = dataGridOpenDeal;
            _hostCloseDeal = dataGridCloseDeal;

            _hostCloseDeal.Child = _gridCloseDeal;
            _hostOpenDeal.Child = _gridOpenDeal;
        }

        /// <summary>
        /// Disable data drawing
        /// отключить прорисовку данных
        /// </summary>
        public void StopPaint()
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

        /// <summary>
        /// draw position in the table
        /// прорисовать позицию в таблице
        /// </summary>
        private void PaintPosition(Position position)
        {
            try
            {
                if(_gridOpenDeal == null)
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
                            RePaintRowPos(position, _gridCloseDeal.Rows[i]);

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
                            RePaintRowPos(position, _gridOpenDeal.Rows[i]);

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

        private void RePaintRowPos(Position position, DataGridViewRow nRow)
        {
            try
            {
                nRow.Cells[1].Value = position.TimeOpen.ToString(_currentCulture);

                if (position.TimeClose != position.TimeOpen)
                {
                    nRow.Cells[2].Value = position.TimeClose.ToString(_currentCulture);
                }
                else
                {
                    nRow.Cells[2].Value = "";
                }

                nRow.Cells[3].Value = position.NameBot;

                nRow.Cells[4].Value = position.SecurityName;

                nRow.Cells[5].Value = position.Direction;

                nRow.Cells[6].Value = position.State;

                nRow.Cells[7].Value = position.MaxVolume.ToStringWithNoEndZero();

                nRow.Cells[8].Value = position.OpenVolume.ToStringWithNoEndZero();

                nRow.Cells[9].Value = position.WaitVolume.ToStringWithNoEndZero();

                if (position.EntryPrice != 0)
                {
                    nRow.Cells[10].Value = position.EntryPrice.ToStringWithNoEndZero();
                }
                else
                {
                    if (position.OpenOrders != null &&
                        position.OpenOrders.Count != 0 &&
                        position.State != PositionStateType.OpeningFail)
                    {
                        nRow.Cells[10].Value = position.OpenOrders[position.OpenOrders.Count - 1].Price.ToStringWithNoEndZero();
                    }
                }

                if (position.ClosePrice != 0)
                {
                    nRow.Cells[11].Value = position.ClosePrice.ToStringWithNoEndZero();
                }
                else
                {
                    if (position.CloseOrders != null &&
                        position.CloseOrders.Count != 0 &&
                        position.State != PositionStateType.ClosingFail)
                    {
                        nRow.Cells[11].Value = position.CloseOrders[position.CloseOrders.Count - 1].Price.ToStringWithNoEndZero();
                    }
                }

                nRow.Cells[12].Value = position.ProfitPortfolioPunkt.ToStringWithNoEndZero();

                nRow.Cells[13].Value = position.StopOrderRedLine.ToStringWithNoEndZero();

                nRow.Cells[14].Value = position.StopOrderPrice.ToStringWithNoEndZero();

                nRow.Cells[15].Value = position.ProfitOrderRedLine.ToStringWithNoEndZero();

                nRow.Cells[16].Value = position.ProfitOrderPrice.ToStringWithNoEndZero();

                nRow.Cells[17].Value = position.SignalTypeOpen;

                nRow.Cells[18].Value = position.SignalTypeClose;

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Update the position in the collection for the drawing
        /// добавить позицию в коллекцию на прорисовку
        /// </summary>
        public void ProcesPosition(Position position)
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if(_positionsToPaint == null)
            {
                return;
            }

            try
            {
                for (int i = 0; i < _positionsToPaint.Count; i++)
                {
                    if (_positionsToPaint[i].Number == position.Number)
                    {
                        _positionsToPaint[i] = position;
                        return;
                    }
                }

                _positionsToPaint.Add(position);

                if(_startProgram == StartProgram.IsTester)
                {
                    if(_positionsToPaint.Count > 200)
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

        /// <summary>
        /// take a row for the table representing the position
        /// взять строку для таблицы представляющую позицию
        /// </summary>
        /// <param name="position">/position/позиция</param>
        /// <returns>table row/строка для таблицы</returns>
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

                if (position.EntryPrice != 0)
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[10].Value = position.EntryPrice.ToStringWithNoEndZero();
                }
                else
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    if (position.OpenOrders != null &&
                        position.OpenOrders.Count != 0 &&
                        position.State != PositionStateType.OpeningFail)
                    {
                        nRow.Cells[10].Value = position.OpenOrders[position.OpenOrders.Count - 1].Price.ToStringWithNoEndZero();
                    }
                }

                if (position.ClosePrice != 0)
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[11].Value = position.ClosePrice.ToStringWithNoEndZero();
                }
                else
                {
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    if (position.CloseOrders != null &&
                        position.CloseOrders.Count != 0 &&
                        position.State != PositionStateType.ClosingFail)
                    {
                        nRow.Cells[11].Value = position.CloseOrders[position.CloseOrders.Count - 1].Price.ToStringWithNoEndZero();
                    }
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[12].Value = position.ProfitPortfolioPunkt.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[13].Value = position.StopOrderRedLine.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[14].Value = position.StopOrderPrice.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[15].Value = position.ProfitOrderRedLine.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[16].Value = position.ProfitOrderPrice.ToStringWithNoEndZero();

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

        /// <summary>
        /// the user clicked on the table of open trades
        /// пользователь кликнул по таблице открытых сделок
        /// </summary>
        void _gridOpenDeal_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            try
            {
                MenuItem[] items = new MenuItem[6];

                items[0] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem1 };
                items[0].Click += PositionCloseAll_Click;

                items[1] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem2 };
                items[1].Click += PositionOpen_Click;

                items[2] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem3 };
                items[2].Click += PositionCloseForNumber_Click;

                items[3] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem5 };
                items[3].Click += PositionNewStop_Click;

                items[4] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem6 };
                items[4].Click += PositionNewProfit_Click;

                items[5] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem7 };
                items[5].Click += PositionClearDelete_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridOpenDeal.ContextMenu = menu;
                _gridOpenDeal.ContextMenu.Show(_gridOpenDeal, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user clicked on the table of closed trades
        /// пользователь кликнул по таблице закрытых сделок
        /// </summary>
        void _gridCloseDeal_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            try
            {
                MenuItem[] items = new MenuItem[2];

                items[0] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem12 };
                items[0].Click += ClosedPosesGrid_PositionScrollOnChart_Click;

                items[1] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem9 };
                items[1].Click += ClosedPosesGrid_PositionDelete_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridCloseDeal.ContextMenu = menu;
                _gridCloseDeal.ContextMenu.Show(_gridCloseDeal, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the user has ordered to find position on chart 
        /// пользователь заказал найти позиции на графике
        /// </summary>
        void ClosedPosesGrid_PositionScrollOnChart_Click(object sender, EventArgs e)
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

                    if(pos == null 
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

        void ClosedPosesGrid_PositionDelete_Click(object sender, EventArgs e)
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

                if(number == -1)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
                ui.ShowDialog();

                if(ui.UserAcceptActioin == false)
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

        // work with context menu

        /// <summary>
        /// the user has ordered the opening of a new position
        /// пользователь заказал открытие новой позиции
        /// </summary>
        void PositionOpen_Click(object sender, EventArgs e)
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

        /// <summary>
        /// the user has ordered the closing of all positions
        /// пользователь заказал закрытие всех позиций
        /// </summary>
        void PositionCloseAll_Click(object sender, EventArgs e)
        {
            try
            {
                if (OpenPositions == null)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message5);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
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

        /// <summary>
        /// the user has ordered the closing of the transaction by number
        /// пользователь заказал закрытие сделки по номеру
        /// </summary>
        void PositionCloseForNumber_Click(object sender, EventArgs e)
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

        /// <summary>
        /// the user has ordered a new stop for the position
        /// пользователь заказал новый стоп для позиции
        /// </summary>
        void PositionNewStop_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if(_gridOpenDeal.Rows.Count == 0)
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

        /// <summary>
        /// the user has ordered a new profit for the position
        /// пользователь заказал новый профит для позиции
        /// </summary>
        void PositionNewProfit_Click(object sender, EventArgs e)
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

        /// <summary>
        /// the user has ordered the deletion of a position
        /// пользователь заказал удаление позиции
        /// </summary>
        void PositionClearDelete_Click(object sender, EventArgs e)
        {
            try
            {
                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
                ui.ShowDialog();

                if (ui.UserAcceptActioin == false)
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
        // messages to the log
        // сообщения в лог 

        /// <summary>
        /// send a new message to the top
        /// выслать новое сообщение на верх
        /// </summary>
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

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
        //events
        // события
        /// <summary>
        /// the status of the deal has changed.
        /// изменился статус сделки
        /// </summary>
        public event Action<Position> PositionStateChangeEvent;
        //events
        // события
        /// <summary>
        /// the open position volume has changed
        /// изменился открытый объём по позиции
        /// </summary>
        public event Action<Position> PositionNetVolumeChangeEvent;

        /// <summary>
        /// the user has selected an action in the pop-up menu
        /// пользователь выбрал во всплывающем меню некое действие
        /// </summary>
        public event Action<Position, SignalType> UserSelectActionEvent;
    }
}
