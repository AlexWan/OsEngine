/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;

namespace OsEngine.Journal.Internal
{

    /// <summary>
    /// хранилище сделок 
    /// </summary>
    public class PositionController
    {
        // статическая часть с работой потока сохраняющего позиции

        /// <summary>
        /// поток 
        /// </summary>
        public static Thread Watcher;

        /// <summary>
        /// контроллеры позиций которые нужно обслуживать
        /// </summary>
        public static List<PositionController> ControllersToCheck = new List<PositionController>();

        /// <summary>
        /// активировать поток для сохранения
        /// </summary>
        public static void Activate()
        {
            if (ServerMaster.StartProgram == ServerStartProgramm.IsOsData ||
                ServerMaster.StartProgram == ServerStartProgramm.IsOsOptimizer)
            {
                return;
            }
            Watcher = new Thread(WatcherHome);
            Watcher.Name = "PositionControllerThread";
            Watcher.IsBackground = true;
            Watcher.Start();
        }

        /// <summary>
        /// место работы потока
        /// </summary>
        public static void WatcherHome()
        {

            while (true)
            {
                Thread.Sleep(1000);

                for (int i = 0; i < ControllersToCheck.Count; i++)
                {
                    ControllersToCheck[i].SavePositions();
                    ControllersToCheck[i].TryPaintPositions();
                }

                if (!MainWindow.ProccesIsWorked)
                {
                    return;
                }
            }
        }

        // сервис
        public PositionController(string name)
        {
            if (ServerMaster.StartProgram == ServerStartProgramm.IsTester)
            {
                _typeWork = ConnectorWorkType.Tester;
            }
            else
            {
                _typeWork = ConnectorWorkType.Real;
            }

            _name = name;

            if (Watcher == null)
            {
                Activate();
            }

            ControllersToCheck.Add(this);

            CreateTable();

            Load();

            if (_deals != null &&
                _deals.Count > 0)
            {
                _openPositions = _deals.FindAll(
                    position => position.State != PositionStateType.Done
                                && position.State != PositionStateType.OpeningFail);
            }

        }

        /// <summary>
        /// имя
        /// </summary>
        private string _name;

        private ConnectorWorkType _typeWork;

        /// <summary>
        /// загрузить
        /// </summary>
        private void Load()
        {
            if (ServerMaster.StartProgram == ServerStartProgramm.IsTester ||
               ServerMaster.StartProgram == ServerStartProgramm.IsOsOptimizer ||
                ServerMaster.StartProgram == ServerStartProgramm.IsOsMiner)
            {
                return;
            }

            if (!File.Exists(@"Engine\" + _name + @"DealController.txt"))
            {
                return;
            }
            try
            {
                //1 считаем кол-во сделок в файле

                int countDeal = 0;

                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"DealController.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        countDeal++;
                        reader.ReadLine();
                    }
                    reader.Close();
                }

                if (countDeal == 0)
                {
                    return;
                }

                List<Position> positions = new List<Position>();

                int i = 0;

                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"DealController.txt"))
                {
                    while (!reader.EndOfStream)
                    {
                        try
                        {
                            positions.Add(new Position());
                            positions[i].SetDealFromString(reader.ReadLine());
                            UpdeteOpenPositionArray(positions[i]);
                        }
                        catch (Exception)
                        {
                            i--;
                            positions.Remove(positions[i]);
                        }

                        i++;
                    }
                    reader.Close();
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
                SendNewLogMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить
        /// </summary>
        public void Delete()
        {
            try
            {
                _neadToSave = false;
                if (File.Exists(@"Engine\" + _name + @"DealController.txt"))
                {
                    File.Delete(@"Engine\" + _name + @"DealController.txt");
                }

                for (int i = 0; i < ControllersToCheck.Count; i++)
                {
                    if (ControllersToCheck[i]._name == _name)
                    {
                        ControllersToCheck.RemoveAt(i);
                        return;
                    }
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// очистить от сделок
        /// </summary>
        public void Clear()
        {
            try
            {
                List<Position> deals = _deals;

                _deals = null;

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

        /// <summary>
        /// нужно ли сохранить данные
        /// </summary>
        private bool _neadToSave;

        private void SavePositions()
        {
            if (!_neadToSave)
            {
                return;
            }

            if (ServerMaster.StartProgram == ServerStartProgramm.IsTester ||
               ServerMaster.StartProgram == ServerStartProgramm.IsOsOptimizer ||
                ServerMaster.StartProgram == ServerStartProgramm.IsOsMiner)
            {
                return;
            }

            _neadToSave = false;

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"DealController.txt", false))
                {
                    List<Position> deals = _deals;

                    StringBuilder result = new StringBuilder();

                    for (int i = 0; deals != null && i < deals.Count; i++)
                    {
                        result.Append(deals[i].GetStringForSave() + "\r\n");
                    }

                    writer.Write(result);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// сохранить текущее состояние позиций
        /// </summary>
        public void Save()
        {
            _neadToSave = true;
        }

// работа с позицией

        /// <summary>
        /// все сделки
        /// </summary>
        private List<Position> _deals;

        /// <summary>
        /// добавить сделку в хранилище
        /// </summary>
        public void SetNewPosition(Position newPosition)
        {
            if (newPosition == null)
            {
                return;
            }

            // сохраняем

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
        /// удалить позицию
        /// </summary>
        public void DeletePosition(Position position)
        {
            if (_deals == null || _deals.Count == 0)
            {
                return;
            }

            _deals.Remove(position);

            for (int i = 0; i < _openPositions.Count; i++)
            {
                if (_openPositions[i].Number == position.Number)
                {
                    _openPositions.RemoveAt(i);
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

                    ProcesPosition(_deals[i]);
                    break;
                }
            }
            _neadToSave = true;
        }

        /// <summary>
        /// загрузить трейд в хранилище
        /// </summary>
        public void SetNewTrade(MyTrade trade)
        {
            if (_deals == null)
            {
                return;
            }

            for (int i = _deals.Count - 1; i > _deals.Count - 150 && i > -1; i--)
            {
                bool isCloseOrder = false;

                if (_deals[i].CloseOrders != null)
                {
                    for (int indexCloseOrd = 0; indexCloseOrd < _deals[i].CloseOrders.Count; indexCloseOrd++)
                    {
                        if (_deals[i].CloseOrders[indexCloseOrd].NumberMarket == trade.NumberOrderParent ||
                            _deals[i].CloseOrders[indexCloseOrd].NumberUser.ToString() == trade.NumberOrderParent)
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
                    for (int indOpenOrd = 0; indOpenOrd < _deals[i].OpenOrders.Count; indOpenOrd++)
                    {
                        if (_deals[i].OpenOrders[indOpenOrd].NumberMarket == trade.NumberOrderParent ||
                            _deals[i].OpenOrders[indOpenOrd].NumberUser.ToString() == trade.NumberOrderParent)
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

                    _deals[i].SetTrade(trade);

                    if (positionState != _deals[i].State ||
                        lastPosVolume != _deals[i].OpenVolume)
                    {
                        UpdeteOpenPositionArray(_deals[i]);
                        _openLongChanged = true;
                        _openShortChanged = true;
                        _closePositionChanged = true;
                        _closeShortChanged = true;
                        _closeLongChanged = true;
                    }

                    if (positionState != _deals[i].State && PositionStateChangeEvent != null)
                    {
                        PositionStateChangeEvent(_deals[i]);
                    }

                    if (lastPosVolume != _deals[i].OpenVolume && PositionNetVolumeChangeEvent != null)
                    {
                        PositionNetVolumeChangeEvent(_deals[i]);
                    }

                    ProcesPosition(_deals[i]);
                }
            }
            _neadToSave = true;
        }

        /// <summary>
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

            for (int i = positions.Count - 1; i > -1; i--)
            {
                if (positions[i].State == PositionStateType.Open)
                {
                    decimal profitOld = positions[i].ProfitPortfolioPunkt;

                    positions[i].SetBidAsk(bid, ask);

                    if (profitOld != positions[i].ProfitPortfolioPunkt)
                    {
                        ProcesPosition(positions[i]);
                    }
                }
            }
        }

        /// <summary>
        /// пустой лист который мы возвращаем вместо null при запросе массивов
        /// </summary>
        private List<Position> _emptyList = new List<Position>(); 

        // последняя позиция

        /// <summary>
        /// изменилась ли последняя позиция
        /// </summary>
        private bool _lastPositionChange = true;

        private Position _lastPosition;

        /// <summary>
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

        // открытые позиции

        private List<Position> _openPositions = new List<Position>();
        /// <summary>
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
            {// это открытая позиция
                if (_openPositions.Find(pos => pos.Number == position.Number) == null)
                {
                    _openPositions.Add(position);
                }
            }
            else
            {// закрытая
                if (_openPositions.Find(pos => pos.Number == position.Number) != null)
                {
                    _openPositions.Remove(position);
                }
            }
        }

        // открытые лонги

        private bool _openLongChanged = true;

        private List<Position> _openLongPosition;

        /// <summary>
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

        // открытые шорты

        private bool _openShortChanged = true;

        private List<Position> _openShortPositions;

        /// <summary>
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

        // закрытые позиции

        private bool _closePositionChanged = true;

        private List<Position> _closePositions;
        /// <summary>
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

        // закрытые лонги

        private bool _closeLongChanged = true;

        private List<Position> _closeLongPositions;

        /// <summary>
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

        // закрытые шорты

        private bool _closeShortChanged = true;

        private List<Position> _closeShortPositions;

        /// <summary>
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

// все позиции

        /// <summary>
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
        /// взять сделку по номеру
        /// </summary>
        public Position GetPositionForNumber(int number)
        {
            return _deals.Find(position => position.Number == number);
        }

// прорисовка позиций в таблицах

        private void TryPaintPositions()
        {
            if (ServerMaster.StartProgram == ServerStartProgramm.IsOsOptimizer)
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

                lock (_positionPaintLocker)
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
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        object _positionPaintLocker = new object();

        private List<Position> _positionsToPaint = new List<Position>();  

        /// <summary>
        /// создать таблицы для прорисовки позиций
        /// </summary>
        private void CreateTable()
        {
            _gridOpenDeal = CreateNewTable();
            _gridCloseDeal = CreateNewTable();
            _gridOpenDeal.Click += _gridOpenDeal_Click;

        }

        /// <summary>
        /// создать таблицу
        /// </summary>
        /// <returns>таблица для прорисовки на ней позиций</returns>
        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = new DataGridView();

                newGrid.AllowUserToOrderColumns = false;
                newGrid.AllowUserToResizeRows = false;
                newGrid.AllowUserToDeleteRows = false;
                newGrid.AllowUserToAddRows = false;
                newGrid.RowHeadersVisible = false;
                newGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                newGrid.MultiSelect = false;

                DataGridViewCellStyle style = new DataGridViewCellStyle();
                style.Alignment = DataGridViewContentAlignment.BottomRight;

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = style;

                DataGridViewColumn colum0 = new DataGridViewColumn();
                colum0.CellTemplate = cell0;
                colum0.HeaderText = @"Номер";
                colum0.ReadOnly = true;
                colum0.Width = 50;
                colum0.ToolTipText = @"Номер позиции";
                newGrid.Columns.Add(colum0);

                DataGridViewColumn colum01 = new DataGridViewColumn();
                colum01.CellTemplate = cell0;
                colum01.HeaderText = @"Время открытия";
                colum01.ReadOnly = true;
                colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
                colum01.ToolTipText = @"Время совершения первой сделки по позиции";
                newGrid.Columns.Add(colum01);

                DataGridViewColumn colu = new DataGridViewColumn();
                colu.CellTemplate = cell0;
                colu.HeaderText = @"Бот";
                colu.ReadOnly = true;
                colu.Width = 70;

                newGrid.Columns.Add(colu);

                DataGridViewColumn colum1 = new DataGridViewColumn();
                colum1.CellTemplate = cell0;
                colum1.HeaderText = @"Инструмент";
                colum1.ReadOnly = true;
                colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum1);

                DataGridViewColumn colum2 = new DataGridViewColumn();
                colum2.CellTemplate = cell0;
                colum2.HeaderText = @"Напр.";
                colum2.ReadOnly = true;
                colum2.Width = 40;

                newGrid.Columns.Add(colum2);

                DataGridViewColumn colum3 = new DataGridViewColumn();
                colum3.CellTemplate = cell0;
                colum3.HeaderText = @"Статус";
                colum3.ReadOnly = true;
                colum3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum3);

                DataGridViewColumn colum4 = new DataGridViewColumn();
                colum4.CellTemplate = cell0;
                colum4.HeaderText = @"Объём";
                colum4.ReadOnly = true;
                colum4.Width = 60;

                newGrid.Columns.Add(colum4);

                DataGridViewColumn colum45 = new DataGridViewColumn();
                colum45.CellTemplate = cell0;
                colum45.HeaderText = @"Текущий";
                colum45.ReadOnly = true;
                colum45.Width = 60;
                newGrid.Columns.Add(colum45);

                DataGridViewColumn colum5 = new DataGridViewColumn();
                colum5.CellTemplate = cell0;
                colum5.HeaderText = @"Ожидает";
                colum5.ReadOnly = true;
                colum5.Width = 60;

                newGrid.Columns.Add(colum5);

                DataGridViewColumn colum6 = new DataGridViewColumn();
                colum6.CellTemplate = cell0;
                colum6.HeaderText = @"Вход";
                colum6.ReadOnly = true;
                colum6.ToolTipText = @"Средняя цена входа";
                colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum6);

                DataGridViewColumn colum61 = new DataGridViewColumn();
                colum61.CellTemplate = cell0;
                colum61.HeaderText = @"Выход";
                colum61.ReadOnly = true;
                colum61.ToolTipText = @"Средняя цена выхода";
                colum61.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum61);

                DataGridViewColumn colum8 = new DataGridViewColumn();
                colum8.CellTemplate = cell0;
                colum8.HeaderText = @"Прибыль";
                colum8.ReadOnly = true;
                colum8.ToolTipText = @"Прибыль в пунктах";
                colum8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum8);

                DataGridViewColumn colum9 = new DataGridViewColumn();
                colum9.CellTemplate = cell0;
                colum9.HeaderText = @"СтопАктивация";
                colum9.ReadOnly = true;
                colum9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum9);

                DataGridViewColumn colum10 = new DataGridViewColumn();
                colum10.CellTemplate = cell0;
                colum10.HeaderText = @"СтопЦена";
                colum10.ReadOnly = true;
                colum10.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum10);

                DataGridViewColumn colum11 = new DataGridViewColumn();
                colum11.CellTemplate = cell0;
                colum11.HeaderText = @"ПрофитАктивация";
                colum11.ReadOnly = true;
                colum11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum11);

                DataGridViewColumn colum12 = new DataGridViewColumn();
                colum12.CellTemplate = cell0;
                colum12.HeaderText = @"ПрофитЦена";
                colum12.ReadOnly = true;
                colum12.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum12);

                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// очистить таблицы от сделок
        /// </summary>
        private void ClearPositionsGrid()
        {
            if (_gridOpenDeal.InvokeRequired)
            {
                _gridOpenDeal.Invoke(new Action(ClearPositionsGrid));
                return;
            }

            _gridOpenDeal.Rows.Clear();
            _gridCloseDeal.Rows.Clear();
        }

        /// <summary>
        /// поле для открытых сделок
        /// </summary>
        private WindowsFormsHost _hostOpenDeal;

        /// <summary>
        /// поле для закрытых сделок
        /// </summary>
        private WindowsFormsHost _hostCloseDeal;

        /// <summary>
        /// поле для открытых сделок
        /// </summary>
        private DataGridView _gridOpenDeal;

        /// <summary>
        /// поле для закрытых сделок
        /// </summary>
        private DataGridView _gridCloseDeal;

        /// <summary>
        /// включить прорисовку данных
        /// </summary>
        public void StartPaint(WindowsFormsHost dataGridOpenDeal, WindowsFormsHost dataGridCloseDeal)
        {
            _hostCloseDeal = dataGridCloseDeal;
            if (!_hostCloseDeal.Dispatcher.CheckAccess())
            {
                _hostCloseDeal.Dispatcher.Invoke(new Action<WindowsFormsHost,WindowsFormsHost>(StartPaint),dataGridOpenDeal,dataGridCloseDeal);
                return;
            }

            _hostOpenDeal = dataGridOpenDeal;
            _hostCloseDeal = dataGridCloseDeal;

            _hostCloseDeal.Child = _gridCloseDeal;
            _hostOpenDeal.Child = _gridOpenDeal;
        }

        /// <summary>
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
        }

        /// <summary>
        /// прорисовать позицию в таблице
        /// </summary>
        private void PaintPosition(Position position)
        {
            try
            {
                if (_gridOpenDeal.InvokeRequired)
                {
                    _gridOpenDeal.Invoke(new Action<Position>(PaintPosition), position);
                    return;
                }

                if (_deals == null || _deals.Count == 0 ||
                    _deals.Find(position1 => position1.Number == position.Number) == null)
                {// сделка была удалена. Надо её удалить отовсюду
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
                }

                if (position.State == PositionStateType.Done ||
                    position.State == PositionStateType.OpeningFail)
                { // сделкка должна быть прорисована в таблице закрытых сделок
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
                            _gridCloseDeal.Rows.Remove(_gridCloseDeal.Rows[i]);
                            _gridCloseDeal.Rows.Insert(i, GetRow(position));
                            return;
                        }
                    }
                    _gridCloseDeal.Rows.Insert(0, GetRow(position));
                }
                else
                { // сделкка должна быть прорисована в таблице Открытых сделок

                    for (int i = 0; i < _gridOpenDeal.Rows.Count; i++)
                    {
                        if ((int)_gridOpenDeal.Rows[i].Cells[0].Value == position.Number)
                        {
                            _gridOpenDeal.Rows.Remove(_gridOpenDeal.Rows[i]);
                            _gridOpenDeal.Rows.Insert(i, GetRow(position));
                            return;
                        }
                    }
                    _gridOpenDeal.Rows.Insert(0, GetRow(position));
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// добавить позицию в коллекцию на прорисовку
        /// </summary>
        public void ProcesPosition(Position position)
        {
            lock (_positionPaintLocker)
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
            }
        }

        /// <summary>
        /// взять строку для таблицы представляющую позицию
        /// </summary>
        /// <param name="position">позиция</param>
        /// <returns>строка для таблицы</returns>
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
                nRow.Cells[1].Value = position.TimeOpen;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = position.NameBot;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = position.SecurityName;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = position.Direction;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = position.State;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = position.MaxVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = position.OpenVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[8].Value = position.WaitVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[9].Value = position.EntryPrice;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[10].Value = position.ClosePrice;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                
                nRow.Cells[11].Value = position.ProfitPortfolioPunkt;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[12].Value = position.StopOrderRedLine;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[13].Value = position.StopOrderPrice;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[14].Value = position.ProfitOrderRedLine;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[15].Value = position.ProfitOrderPrice;

                return nRow;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
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
                MenuItem[] items = new MenuItem[7];

                items[0] = new MenuItem { Text = @"Закрыть все по маркету" };
                items[0].Click += PositionCloseAll_Click;

                items[1] = new MenuItem { Text = @"Открыть новую" };
                items[1].Click += PositionOpen_Click;

                items[2] = new MenuItem { Text = @"Закрыть текущую" };
                items[2].Click += PositionCloseForNumber_Click;

                items[3] = new MenuItem { Text = @"Модифицировать текущую" };
                items[3].Click += PositionModificationForNumber_Click;

                items[4] = new MenuItem { Text = @"Переставить стоп" };
                items[4].Click += PositionNewStop_Click;

                items[5] = new MenuItem { Text = @"Переставить профит" };
                items[5].Click += PositionNewProfit_Click;

                items[6] = new MenuItem { Text = @"Удалить позицию" };
                items[6].Click += PositionClearDelete_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridOpenDeal.ContextMenu = menu;
                _gridOpenDeal.ContextMenu.Show(_gridOpenDeal, new Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// работа с контекстным меню

        /// <summary>
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
        /// пользователь заказал закрытие сделки по номеру
        /// </summary>
        void PositionCloseForNumber_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
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
        /// пользователь заказал модификацию позиции
        /// </summary>
        void PositionModificationForNumber_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    number = Convert.ToInt32(_gridOpenDeal.Rows[_gridOpenDeal.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }


                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(GetPositionForNumber(number), SignalType.Modificate);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// пользователь заказал новый стоп для позиции
        /// </summary>
        void PositionNewStop_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
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
        /// пользователь заказал новый профит для позиции
        /// </summary>
        void PositionNewProfit_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
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
        /// пользователь заказал удаление позиции
        /// </summary>
        void PositionClearDelete_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
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

// сообщения в лог 

        /// <summary>
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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // события
        /// <summary>
        /// изменился статус сделки
        /// </summary>
        public event Action<Position> PositionStateChangeEvent;

        // события
        /// <summary>
        /// изменился открытый объём по позиции
        /// </summary>
        public event Action<Position> PositionNetVolumeChangeEvent;

        /// <summary>
        /// пользователь выбрал во всплывающем меню некое действие
        /// </summary>
        public event Action<Position, SignalType> UserSelectActionEvent;
    }
}
