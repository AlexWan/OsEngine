/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Entity.SyntheticBondEntity;
using OsEngine.Language;
using OsEngine.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace OsEngine.OsTrader.Panels.Tab.SyntheticBondTab
{
    public class BotTabSyntheticBond : IIBotTab
    {
        #region Constructor

        public BotTabSyntheticBond(string nameTab, StartProgram startProgram)
        {
            TabName = nameTab;
            StartProgram = startProgram;

            LoadSyntheticBondSeries();
        }

        private void LoadSyntheticBondSeries()
        {
            try
            {
                if (!File.Exists(@"Engine\" + TabName + @"SyntheticBondSeriesNamesToLoad.txt"))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"SyntheticBondSeriesNamesToLoad.txt"))
                {
                    _eventsIsOn = Convert.ToBoolean(reader.ReadLine());
                    _emulatorIsOn = Convert.ToBoolean(reader.ReadLine());
                    int syntheticBondSeriesCount = Convert.ToInt32(reader.ReadLine());

                    for (int i = 0; i < syntheticBondSeriesCount; i++)
                    {
                        string syntheticBondSeriesName = reader.ReadLine();
                        int syntheticBondSeriesNumber = Convert.ToInt32(reader.ReadLine());

                        SyntheticBondSeries syntheticBondSeries = new SyntheticBondSeries(syntheticBondSeriesName, syntheticBondSeriesNumber, StartProgram);
                        syntheticBondSeries.SecuritySubscribeEvent += SecuritySubscribeEvent;
                        syntheticBondSeries.ContangoChangeEvent += SyntheticBond_SeparationChangeEvent;
                        SyntheticBondSeries.Add(syntheticBondSeries);
                    }
                }

                EventsIsOn = _eventsIsOn;
                EmulatorIsOn = _emulatorIsOn;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Save()
        {
            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"SyntheticBondSeriesNamesToLoad.txt", false))
                {
                    writer.WriteLine(_eventsIsOn);
                    writer.WriteLine(_emulatorIsOn);
                    writer.WriteLine(SyntheticBondSeries.Count.ToString());

                    for (int i = 0; i < SyntheticBondSeries.Count; i++)
                    {
                        writer.WriteLine(SyntheticBondSeries[i].UniqueName);
                        writer.WriteLine(SyntheticBondSeries[i].UniqueNumber.ToString());

                        SyntheticBondSeries[i].Save();
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        #endregion

        #region Public interface properties

        public StartProgram StartProgram;

        public BotTabType TabType
        {
            get
            {
                return BotTabType.SyntheticBond;
            }
        }

        public string TabName { get; set; }

        public int TabNum { get; set; }

        public bool EmulatorIsOn
        {
            get
            {
                return _emulatorIsOn;
            }
            set
            {
                if (_emulatorIsOn == value)
                {
                    return;
                }

                _emulatorIsOn = value;

                for (int i = 0; i < SyntheticBondSeries.Count; i++)
                {
                    SyntheticBondSeries[i].EmulatorIsOn = value;
                }

                Save();
            }
        }

        public DateTime LastTimeCandleUpdate { get; set; }

        public bool EventsIsOn
        {
            get
            {
                return _eventsIsOn;
            }
            set
            {
                for (int i = 0; i < SyntheticBondSeries.Count; i++)
                {
                    SyntheticBondSeries[i].EventsIsOn = value;
                }

                if (_eventsIsOn == value)
                {
                    return;
                }

                _eventsIsOn = value;
                Save();
            }
        }

        public bool AllSecuritiesReady
        {
            get
            {
                if (_allSecuritiesReadyState == null)
                {
                    return false;
                }

                return _allSecuritiesReadyState.Value;
            }
        }

        public void Delete()
        {
            try
            {
                _isDeleted = true;

                for (int i = 0; i < SyntheticBondSeries.Count; i++)
                {
                    SyntheticBondSeries[i].Delete();
                }

                if (SyntheticBondSeries != null)
                {
                    SyntheticBondSeries.Clear();
                    SyntheticBondSeries = null;
                }

                if (_grid != null)
                {
                    _grid.Rows.Clear();
                    _grid.CellClick -= _grid_CellClick;
                    _grid.DataError -= _grid_DataError;
                    DataGridFactory.ClearLinks(_grid);
                }

                if (_host != null)
                {
                    _host.Child = null;
                    _host = null;
                }

                if (_positionViewer != null)
                {
                    _positionViewer.UserSelectActionEvent -= _globalController_UserSelectActionEvent;
                    _positionViewer.UserClickOnPositionShowBotInTableEvent -= _globalPositionViewer_UserClickOnPositionShowBotInTableEvent;
                    _positionViewer.Delete();
                }

                try
                {
                    if (File.Exists(@"Engine\" + TabName + @"SyntheticBondSeriesNamesToLoad.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"SyntheticBondSeriesNamesToLoad.txt");
                    }
                }
                catch
                {
                    // ignore
                }

                if (TabDeletedEvent != null)
                {
                    TabDeletedEvent();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action TabDeletedEvent;

        #endregion

        #region Private fields

        private WindowsFormsHost _host;
        private DataGridView _grid;
        public List<SyntheticBondSeries> SyntheticBondSeries = new List<SyntheticBondSeries>();

        private bool _emulatorIsOn = false;
        private bool _eventsIsOn = true;

        private bool _isDeleted;

        private bool? _allSecuritiesReadyState;
        private Thread _painterThread;
        private GlobalPositionViewer _positionViewer;

        #endregion

        #region Public fields

        public event Action<SyntheticBond> SeparationChangeEvent;

        public event Action<Position, SignalType> UserSelectActionEvent;

        public event Action<string> UserClickOnPositionShowBotInTableEvent;

        public event Action<BotTabSimple> NewTabCreateEvent;

        public event Action<SyntheticBond, SyntheticBond> MaxSeparationBondChangedEvent;

        public event Action<SyntheticBond, SyntheticBond> MinSeparationBondChangedEvent;

        public event Action AllSecuritiesReadyEvent;

        public event Action AllSecuritiesNotReadyEvent;

        #endregion

        #region Paint interface

        public void StartPaint(WindowsFormsHost hostChart,
            WindowsFormsHost hostOpenDeals,
            WindowsFormsHost hostCloseDeals)
        {
            _host = hostChart;

            if (_grid == null)
            {
                CreateGrid();
            }

            _host.Child = _grid;

            RePaintGrid();

            if (_painterThread == null)
            {
                _painterThread = new Thread(MainThread);
                _painterThread.Start();
            }

            if (_positionViewer == null)
            {
                _positionViewer = new GlobalPositionViewer(StartProgram);
                _positionViewer.LogMessageEvent += SendNewLogMessage;
                _positionViewer.UserSelectActionEvent += _globalController_UserSelectActionEvent;
                _positionViewer.UserClickOnPositionShowBotInTableEvent += _globalPositionViewer_UserClickOnPositionShowBotInTableEvent;
            }

            SetJournalsInPosViewer();
            _positionViewer.StartPaint(hostOpenDeals, hostCloseDeals);
        }

        private void _globalController_UserSelectActionEvent(Position pos, SignalType signal)
        {
            if (UserSelectActionEvent != null)
            {
                UserSelectActionEvent(pos, signal);
            }
        }

        private void _globalPositionViewer_UserClickOnPositionShowBotInTableEvent(string botTabName)
        {
            if (UserClickOnPositionShowBotInTableEvent != null)
            {
                UserClickOnPositionShowBotInTableEvent(botTabName);
            }
        }

        public List<Journal.Journal> GetJournals()
        {
            try
            {
                List<Journal.Journal> journals = new List<Journal.Journal>();

                for (int i = 0; i < SyntheticBondSeries.Count; i++)
                {
                    SyntheticBondSeries syntheticBondSeries = SyntheticBondSeries[i];

                    for (int i2 = 0; syntheticBondSeries.SyntheticBonds != null && i2 < syntheticBondSeries.SyntheticBonds.Count; i2++)
                    {
                        SyntheticBond syntheticBond = syntheticBondSeries.SyntheticBonds[i2];

                        for (int i3 = 0; i3 < syntheticBond.ActiveScenarios.Count; i3++)
                        {
                            if (syntheticBond.ActiveScenarios[i3] != null &&
                                syntheticBond.ActiveScenarios[i3].ArbitrationIceberg.MainLegs.Count > 0 &&
                                syntheticBond.ActiveScenarios[i3].ArbitrationIceberg.MainLegs[0].BotTab != null)
                            {
                                journals.Add(syntheticBond.ActiveScenarios[i3].ArbitrationIceberg.MainLegs[0].BotTab.GetJournal());
                            }

                            if (syntheticBond.ActiveScenarios[i3] != null &&
                                syntheticBond.ActiveScenarios[i3].ArbitrationIceberg.SecondaryLegs.Count > 0 &&
                                syntheticBond.ActiveScenarios[i3].ArbitrationIceberg.SecondaryLegs[0].BotTab != null)
                            {
                                journals.Add(syntheticBond.ActiveScenarios[i3].ArbitrationIceberg.SecondaryLegs[0].BotTab.GetJournal());
                            }
                        }
                    }
                }

                return journals;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void SetJournalsInPosViewer()
        {
            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            if (_positionViewer == null)
            {
                return;
            }

            try
            {
                _positionViewer.ClearJournalsArray();

                List<Journal.Journal> journals = GetJournals();

                if (journals != null && journals.Count > 0)
                {
                    _positionViewer.SetJournals(journals);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaint()
        {
            try
            {
                if (_host == null ||
                    _grid == null)
                {
                    return;
                }

                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(StopPaint));
                    return;
                }

                _host.Child = null;

                if (_positionViewer != null)
                {
                    _positionViewer.StopPaint();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void MainThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (_isDeleted)
                    {
                        return;
                    }

                    RePaintGrid();

                    CheckAllSecuritiesReadiness();
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void CreateGrid()
        {
            _grid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells, false);
            _grid.ScrollBars = ScrollBars.Vertical;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _grid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(colum0);

            for (int i = 0; i < 11; i++)
            {
                DataGridViewColumn columN = new DataGridViewColumn();
                columN.CellTemplate = cell0;
                columN.HeaderText = "";
                columN.ReadOnly = true;
                columN.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _grid.Columns.Add(columN);
            }

            _grid.CellClick += _grid_CellClick;
            _grid.DataError += _grid_DataError;
        }

        private void RePaintGrid()
        {
            try
            {
                if (_grid == null)
                {
                    return;
                }

                if (_grid.InvokeRequired)
                {
                    _grid.Invoke(new Action(RePaintGrid));
                    return;
                }

                List<DataGridViewRow> newRows = GetRowsToGrid();

                if (newRows == null)
                {
                    return;
                }

                int existingCount = _grid.Rows.Count;
                int newCount = newRows.Count;

                if (existingCount != newCount)
                {
                    int showRow = _grid.FirstDisplayedScrollingRowIndex;

                    _grid.Rows.Clear();

                    for (int i = 0; i < newCount; i++)
                    {
                        _grid.Rows.Add(newRows[i]);
                    }

                    if (showRow > 0 &&
                        showRow < _grid.Rows.Count)
                    {
                        _grid.FirstDisplayedScrollingRowIndex = showRow;
                    }

                    return;
                }

                for (int i = 0; i < newCount; i++)
                {
                    for (int j = 0; j < _grid.ColumnCount; j++)
                    {
                        DataGridViewCell existingCell = _grid.Rows[i].Cells[j];
                        DataGridViewCell newCell = newRows[i].Cells[j];

                        if (!Equals(existingCell.Value, newCell.Value))
                        {
                            existingCell.Value = newCell.Value;
                        }

                        if (existingCell.Style.ForeColor != newCell.Style.ForeColor)
                        {
                            existingCell.Style.ForeColor = newCell.Style.ForeColor;
                        }

                        if (existingCell.Style.BackColor != newCell.Style.BackColor)
                        {
                            existingCell.Style.BackColor = newCell.Style.BackColor;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<DataGridViewRow> GetRowsToGrid()
        {
            List<DataGridViewRow> rows = new List<DataGridViewRow>();

            rows.Add(GetFirstGridRow());

            for (int i = 0; SyntheticBondSeries != null && i < SyntheticBondSeries.Count; i++)
            {
                SyntheticBondSeries syntheticBondSeries = SyntheticBondSeries[i];

                if (syntheticBondSeries.SyntheticBonds == null)
                {
                    continue;
                }

                for (int i2 = 0; i2 < syntheticBondSeries.SyntheticBonds.Count; i2++)
                {
                    if (i2 == 0)
                    {
                        rows.Add(GetFirstSyntheticBondGridRow(syntheticBondSeries, syntheticBondSeries.SyntheticBonds[i2]));
                        continue;
                    }

                    rows.Add(GetSecondSyntheticBondGridRow(syntheticBondSeries.SyntheticBonds[i2]));
                }

                DataGridViewRow lastFuturesRow = new DataGridViewRow();

                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 0 номер / удалить облигацию

                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 1 база облигации

                DataGridViewButtonCell button = new DataGridViewButtonCell(); // 2 фьючерс облигации
                button.Value = "+";
                lastFuturesRow.Cells.Add(button);

                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 3 Удаление фьючерсов
                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 4 Окно смещений
                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 5 Окно чарта
                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 6 Окно торговли
                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 7 Отклонение
                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 8 в %
                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 9 Дней
                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 10 профит в день
                lastFuturesRow.Cells.Add(new DataGridViewTextBoxCell()); // 11 Позиция

                rows.Add(lastFuturesRow);
            }

            rows.Add(GetLastGridRow());

            return rows;
        }

        private DataGridViewRow GetFirstSyntheticBondGridRow(SyntheticBondSeries syntheticBondSeries, SyntheticBond syntheticBond)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            DataGridViewButtonCell numberCell = new DataGridViewButtonCell(); // 0 номер / удалить облигацию
            numberCell.Value = syntheticBondSeries.UniqueNumber + " / " + OsLocalization.Trader.Label39;
            numberCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(numberCell);

            DataGridViewButtonCell baseCell = new DataGridViewButtonCell(); // 1 база облигации

            if (syntheticBondSeries.PatternBaseTab != null &&
                syntheticBondSeries.PatternBaseTab.Connector != null &&
               !string.IsNullOrEmpty(syntheticBondSeries.PatternBaseTab.Connector.SecurityName))
            {
                baseCell.Value = syntheticBondSeries.PatternBaseTab.Connector.SecurityName;
            }
            else
            {
                baseCell.Value = "None";
                baseCell.Style.ForeColor = Color.Red;
            }

            baseCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(baseCell);

            DataGridViewButtonCell futuresCell = new DataGridViewButtonCell(); // 2 фьючерс облигации

            if (syntheticBond != null &&
                syntheticBond.PatternFuturesTab != null &&
                syntheticBond.PatternFuturesTab.Connector != null &&
               !string.IsNullOrEmpty(syntheticBond.PatternFuturesTab.Connector.SecurityName))
            {
                futuresCell.Value = syntheticBond.PatternFuturesTab.Connector.SecurityName;
            }
            else
            {
                futuresCell.Value = "None";
                futuresCell.Style.ForeColor = Color.Red;
            }

            futuresCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(futuresCell);

            DataGridViewButtonCell deleteFuturesCell = new DataGridViewButtonCell(); // 3 Удаление фьючерсов
            deleteFuturesCell.Value = "-";
            deleteFuturesCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(deleteFuturesCell);

            DataGridViewButtonCell shiftWindowCell = new DataGridViewButtonCell(); // 4 Окно смещений
            shiftWindowCell.Value = "М";
            shiftWindowCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(shiftWindowCell);

            DataGridViewButtonCell chartWindowCell = new DataGridViewButtonCell(); // 5 Окно чарта
            chartWindowCell.Value = "Ч";
            chartWindowCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(chartWindowCell);

            DataGridViewButtonCell tradingWindowCell = new DataGridViewButtonCell(); // 6 Окно торговли
            tradingWindowCell.Value = "Т";
            tradingWindowCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(tradingWindowCell);

            bool isExpired = syntheticBond.DaysBeforeExpiration != -1
                && syntheticBond.DaysBeforeExpiration <= 0;

            DataGridViewTextBoxCell deviationCell = new DataGridViewTextBoxCell(); // 7 Отклонение

            if (isExpired)
            {
                deviationCell.Value = "Экспирация";
                deviationCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBond.AbsoluteSeparationCandles == null ||
                syntheticBond.AbsoluteSeparationCandles != null && syntheticBond.AbsoluteSeparationCandles.Count == 0)
            {
                deviationCell.Value = "None";
                deviationCell.Style.ForeColor = Color.Red;
            }
            else
            {
                deviationCell.Value = syntheticBond.AbsoluteSeparationCandles[^1].Value.ToString();
            }

            deviationCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(deviationCell);

            DataGridViewTextBoxCell percentCell = new DataGridViewTextBoxCell(); // 8 в %

            if (isExpired)
            {
                percentCell.Value = "Экспирация";
                percentCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBond.PercentSeparationCandles == null ||
                syntheticBond.PercentSeparationCandles != null && syntheticBond.PercentSeparationCandles.Count == 0)
            {
                percentCell.Value = "None";
                percentCell.Style.ForeColor = Color.Red;
            }
            else
            {
                percentCell.Value = syntheticBond.PercentSeparationCandles[^1].Value.ToString();
            }

            percentCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(percentCell);

            DataGridViewTextBoxCell daysCell = new DataGridViewTextBoxCell(); // 9 Дней

            if (isExpired)
            {
                daysCell.Value = "Экспирация";
                daysCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBond.DaysBeforeExpiration == -1)
            {
                daysCell.Value = "None";
                daysCell.Style.ForeColor = Color.Red;
            }
            else
            {
                daysCell.Value = syntheticBond.DaysBeforeExpiration.ToString();
            }

            daysCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(daysCell);

            DataGridViewTextBoxCell profitDaysCell = new DataGridViewTextBoxCell(); // 10 профит в день

            if (isExpired)
            {
                profitDaysCell.Value = "Экспирация";
                profitDaysCell.Style.ForeColor = Color.Red;
            }
            else
            {
                profitDaysCell.Value = Math.Round(syntheticBond.ProfitPerDay, 5).ToString();
            }

            profitDaysCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(profitDaysCell);

            DataGridViewTextBoxCell positionCell = new DataGridViewTextBoxCell(); // 11 Позиция
            positionCell.Value = "B: 0; F: 0";
            positionCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(positionCell);

            return nRow;
        }

        private DataGridViewRow GetSecondSyntheticBondGridRow(SyntheticBond syntheticBond)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            DataGridViewButtonCell numberCell = new DataGridViewButtonCell(); // 0 номер / удалить облигацию
            numberCell.Value = "";
            numberCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(numberCell);

            DataGridViewButtonCell baseCell = new DataGridViewButtonCell(); // 1 база облигации
            baseCell.Value = "";
            baseCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(baseCell);

            DataGridViewButtonCell futuresCell = new DataGridViewButtonCell(); // 2 фьючерс облигации

            if (syntheticBond == null ||
                (syntheticBond.ActiveScenarios == null) ||
                (syntheticBond.ActiveScenarios.Count == 0) ||
                (syntheticBond.ActiveScenarios[0].ArbitrationIceberg.SecondaryLegs.Count == 0) ||
                (syntheticBond.ActiveScenarios[0].ArbitrationIceberg.SecondaryLegs[0].BotTab.Connector != null &&
                syntheticBond.ActiveScenarios[0].ArbitrationIceberg.SecondaryLegs[0].BotTab.Connector.SecurityName == null))
            {
                futuresCell.Value = "None";
                futuresCell.Style.ForeColor = Color.Red;
            }
            else
            {
                futuresCell.Value = syntheticBond.ActiveScenarios[0].ArbitrationIceberg.SecondaryLegs[0].BotTab.Connector.SecurityName;
            }

            futuresCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(futuresCell);

            DataGridViewButtonCell deleteFuturesCell = new DataGridViewButtonCell(); // 3 Удаление фьючерсов
            deleteFuturesCell.Value = "-";
            deleteFuturesCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(deleteFuturesCell);

            DataGridViewButtonCell shiftWindowCell = new DataGridViewButtonCell(); // 4 Окно смещений
            shiftWindowCell.Value = "М";
            shiftWindowCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(shiftWindowCell);

            DataGridViewButtonCell chartWindowCell = new DataGridViewButtonCell(); // 5 Окно чарта
            chartWindowCell.Value = "Ч";
            chartWindowCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(chartWindowCell);

            DataGridViewButtonCell tradingWindowCell = new DataGridViewButtonCell(); // 6 Окно торговли
            tradingWindowCell.Value = "Т";
            tradingWindowCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(tradingWindowCell);

            bool isExpired = syntheticBond.DaysBeforeExpiration != -1
                && syntheticBond.DaysBeforeExpiration <= 0;

            DataGridViewTextBoxCell deviationCell = new DataGridViewTextBoxCell(); // 7 Отклонение

            if (isExpired)
            {
                deviationCell.Value = "Экспирация";
                deviationCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBond.AbsoluteSeparationCandles == null ||
                syntheticBond.AbsoluteSeparationCandles != null && syntheticBond.AbsoluteSeparationCandles.Count == 0)
            {
                deviationCell.Value = "None";
                deviationCell.Style.ForeColor = Color.Red;
            }
            else
            {
                deviationCell.Value = syntheticBond.AbsoluteSeparationCandles[^1].Value.ToString();
            }

            deviationCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(deviationCell);

            DataGridViewTextBoxCell percentCell = new DataGridViewTextBoxCell(); // 8 в %

            if (isExpired)
            {
                percentCell.Value = "Экспирация";
                percentCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBond.PercentSeparationCandles == null ||
                syntheticBond.PercentSeparationCandles != null && syntheticBond.PercentSeparationCandles.Count == 0)
            {
                percentCell.Value = "None";
                percentCell.Style.ForeColor = Color.Red;
            }
            else
            {
                percentCell.Value = syntheticBond.PercentSeparationCandles[^1].Value.ToString();
            }

            percentCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(percentCell);

            DataGridViewTextBoxCell daysCell = new DataGridViewTextBoxCell(); // 9 Дней

            if (isExpired)
            {
                daysCell.Value = "Экспирация";
                daysCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBond.DaysBeforeExpiration == -1)
            {
                daysCell.Value = "None";
                daysCell.Style.ForeColor = Color.Red;
            }
            else
            {
                daysCell.Value = syntheticBond.DaysBeforeExpiration.ToString();
            }

            daysCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(daysCell);

            DataGridViewTextBoxCell profitDaysCell = new DataGridViewTextBoxCell(); // 10 профит в день

            if (isExpired)
            {
                profitDaysCell.Value = "Экспирация";
                profitDaysCell.Style.ForeColor = Color.Red;
            }
            else
            {
                profitDaysCell.Value = Math.Round(syntheticBond.ProfitPerDay, 5).ToString();
            }

            profitDaysCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(profitDaysCell);

            DataGridViewTextBoxCell positionCell = new DataGridViewTextBoxCell(); // 11 Позиция
            positionCell.Value = "B: 0; F: 0";
            positionCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(positionCell);

            return nRow;
        }

        private DataGridViewRow GetFirstGridRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            DataGridViewTextBoxCell numberCell = new DataGridViewTextBoxCell(); // 0 номер облигации
            numberCell.Value = "#";
            numberCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(numberCell);

            DataGridViewTextBoxCell baseCell = new DataGridViewTextBoxCell(); // 1 база облигации
            baseCell.Value = OsLocalization.Trader.Label684;
            baseCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(baseCell);

            DataGridViewTextBoxCell futuresCell = new DataGridViewTextBoxCell(); // 2 фьючерс облигации
            futuresCell.Value = OsLocalization.Trader.Label685;
            futuresCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(futuresCell);

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 3 Удаление фьючерсов
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 4 Окно смещений
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 5 Открытие чарта
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 6 Окно для торговли

            DataGridViewTextBoxCell deviationCell = new DataGridViewTextBoxCell(); // 7 Отклонение
            deviationCell.Value = OsLocalization.Trader.Label686;
            deviationCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(deviationCell);

            DataGridViewTextBoxCell percentCell = new DataGridViewTextBoxCell(); // 8 в %
            percentCell.Value = OsLocalization.Trader.Label687;
            percentCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(percentCell);

            DataGridViewTextBoxCell daysCell = new DataGridViewTextBoxCell(); // 9 Дней
            daysCell.Value = OsLocalization.Trader.Label688;
            daysCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(daysCell);

            DataGridViewTextBoxCell profitDaysCell = new DataGridViewTextBoxCell(); // 10 профит в день
            profitDaysCell.Value = OsLocalization.Trader.Label689;
            profitDaysCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(profitDaysCell);

            DataGridViewTextBoxCell positionCell = new DataGridViewTextBoxCell(); // 11 Позиция
            positionCell.Value = OsLocalization.Trader.Label690;
            positionCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(positionCell);

            return nRow;
        }

        private DataGridViewRow GetLastGridRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // 0 добавить пару
            button.Value = "+";
            nRow.Cells.Add(button);

            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 1
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 2
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 3
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 4
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 5
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 6
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 7
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 8
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 9
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 10
            nRow.Cells.Add(new DataGridViewTextBoxCell()); // 11

            return nRow;
        }

        private void CreateSyntheticBondSeries()
        {
            try
            {
                int numberSeries = 1;

                if (SyntheticBondSeries.Count == 0)
                {
                    SyntheticBondSeries newSeries = CreateSyntheticBondSeries(numberSeries);

                    if (newSeries != null)
                    {
                        SyntheticBondSeries.Add(newSeries);
                        SetJournalsInPosViewer();
                    }

                    return;
                }

                bool inserted = false;

                for (int i = 0; i < SyntheticBondSeries.Count; i++)
                {
                    if (SyntheticBondSeries[i].UniqueNumber > numberSeries)
                    {
                        SyntheticBondSeries newSeries = CreateSyntheticBondSeries(numberSeries);

                        if (newSeries != null)
                        {
                            SyntheticBondSeries.Insert(i, newSeries);
                            inserted = true;
                            SetJournalsInPosViewer();
                        }

                        break;
                    }
                    else
                    {
                        numberSeries++;
                    }
                }

                if (!inserted)
                {
                    SyntheticBondSeries newSeries = CreateSyntheticBondSeries(numberSeries);

                    if (newSeries != null)
                    {
                        SyntheticBondSeries.Add(newSeries);
                        SetJournalsInPosViewer();
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private SyntheticBondSeries CreateSyntheticBondSeries(int numberSeries)
        {
            try
            {
                SyntheticBondSeries newSeries = new SyntheticBondSeries(TabName + "SyntheticBondSeries" + numberSeries, numberSeries, StartProgram);
                newSeries.UniqueNumber = numberSeries;
                newSeries.ContangoChangeEvent += SyntheticBond_SeparationChangeEvent;

                string patternBaseTabName = newSeries.UniqueName + "PatternBase";
                newSeries.PatternBaseTab = new BotTabSimple(patternBaseTabName, StartProgram);

                newSeries.SyntheticBonds = new List<SyntheticBond>();

                int syntheticBondNumber = GetAvailableSyntheticBondNumber(newSeries);
                SyntheticBond syntheticBond = CreateSyntheticBond(newSeries.UniqueName.ToString(), syntheticBondNumber);

                if (syntheticBond == null)
                    return null;

                newSeries.SyntheticBonds.Add(syntheticBond);
                newSeries.SecuritySubscribeEvent += SecuritySubscribeEvent;

                return newSeries;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private int GetAvailableSyntheticBondNumber(SyntheticBondSeries series)
        {
            try
            {
                int syntheticBondNumber = 1;
                for (int i = 0; i < series.SyntheticBonds.Count; i++)
                {
                    SyntheticBond syntheticBond = series.SyntheticBonds[i];

                    if (syntheticBond.UniqueNumber == syntheticBondNumber)
                    {
                        syntheticBondNumber++;
                        continue;
                    }
                }

                return syntheticBondNumber;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return 0;
            }
        }

        private SyntheticBond CreateSyntheticBond(string uniqueName, int syntheticBondNumber)
        {
            try
            {
                SyntheticBond syntheticBond = new SyntheticBond(uniqueName + "SyntheticBond" + syntheticBondNumber, syntheticBondNumber, StartProgram);

                syntheticBond.MainRationingMode = RationingMode.Difference;

                return syntheticBond;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(e.Exception.ToString(), LogMessageType.Error);
        }

        private void _grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (_grid == null ||
                    (_grid != null && _grid.Rows == null) ||
                    (_grid != null && _grid.Rows != null && _grid.Rows.Count == 0))
                {
                    return;
                }

                int column = e.ColumnIndex;
                int row = e.RowIndex;

                Dictionary<int, SyntheticBondSeries> seriesDictionary = new Dictionary<int, SyntheticBondSeries>(); // key - номер строки с базой облигации, value - кол-во фьючерсов

                int rowCount = 1;
                for (int i = 0; i < SyntheticBondSeries.Count; i++)
                {
                    seriesDictionary[rowCount] = SyntheticBondSeries[i];

                    if (SyntheticBondSeries[i].SyntheticBonds == null)
                    {
                        rowCount += +1; // 1 это кнопка добавления фьючерса
                    }
                    else
                    {
                        rowCount += SyntheticBondSeries[i].SyntheticBonds.Count + 1; // 1 это кнопка добавления фьючерса
                    }
                }

                if (column == 0) // создание новой облигации | удаление облигации
                {
                    if (_grid.Rows.Count - 1 == row) // создание новой облигации
                    {
                        CreateSyntheticBondSeries();
                        Save();
                        RePaintGrid();
                    }
                    else if (seriesDictionary.ContainsKey(row)) // удаление облигации
                    {
                        AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label691);
                        ui.ShowDialog();

                        if (ui.UserAcceptAction == false)
                        {
                            return;
                        }

                        SyntheticBondSeries syntheticBondSeries = seriesDictionary[row];

                        for (int i = 0; i < SyntheticBondSeries.Count; i++)
                        {
                            SyntheticBondSeries series = SyntheticBondSeries[i];

                            if (series.UniqueName == syntheticBondSeries.UniqueName)
                            {
                                series.Delete();
                                SyntheticBondSeries.RemoveAt(i);
                                break;
                            }
                        }

                        Save();
                        RePaintGrid();
                    }
                }
                else if (column == 1) // выбор бумаги для базы
                {
                    if (seriesDictionary.ContainsKey(row))
                    {
                        SyntheticBondSeries syntheticBondSeries = seriesDictionary[row];

                        syntheticBondSeries.ChooseBaseSecurity();
                        syntheticBondSeries.Save();
                    }
                }
                else if (column == 2) // выбор бумаги для фьючерса или добавление нового фьючерса
                {
                    if (seriesDictionary.ContainsKey(row)) // Выбор фьючерса ( основная строка облигации)
                    {
                        SyntheticBondSeries syntheticBondSeries = seriesDictionary[row];
                        SyntheticBond firstSyntheticBond = syntheticBondSeries.SyntheticBonds[0];

                        syntheticBondSeries.ChooseFuturesSecurity(firstSyntheticBond);
                        syntheticBondSeries.Save();
                    }
                    else
                    {
                        int foundKey = int.MinValue;
                        foreach (int k in seriesDictionary.Keys)
                        {
                            if (k < row && k > foundKey)
                            {
                                foundKey = k;
                            }
                        }

                        if (foundKey == int.MinValue)
                        {
                            return;
                        }

                        SyntheticBondSeries syntheticBondSeries = seriesDictionary[foundKey];

                        if (row == syntheticBondSeries.SyntheticBonds.Count + foundKey) // Добавить новую облигацию
                        {
                            int syntheticBondNumber = GetAvailableSyntheticBondNumber(syntheticBondSeries);

                            SyntheticBond syntheticBond = CreateSyntheticBond(syntheticBondSeries.UniqueName, syntheticBondNumber);

                            if (syntheticBond == null)
                                return;

                            syntheticBondSeries.SyntheticBonds.Add(syntheticBond);
                            syntheticBondSeries.PropagateBaseSecurityToAll();
                            syntheticBondSeries.Save();
                            RePaintGrid();
                        }
                        else // выбрать бумагу для фьючерса
                        {
                            for (int i = 1; i < syntheticBondSeries.SyntheticBonds.Count; i++)
                            {
                                SyntheticBond syntheticBond = syntheticBondSeries.SyntheticBonds[i];
                                if (row == foundKey + i)
                                {
                                    syntheticBondSeries.CloseTradeWindow(syntheticBond);

                                    BondScenario scenario = new BondScenario("Script 1", syntheticBond.UniqueName, scenarioNumber: 1, StartProgram);
                                    syntheticBond.ActiveScenarios.Add(scenario);

                                    syntheticBondSeries.ChooseFuturesSecurity(syntheticBond);
                                    syntheticBondSeries.Save();
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (column == 3) // удаление синтетической облигации
                {
                    int foundKey = int.MinValue;
                    foreach (int k in seriesDictionary.Keys)
                    {
                        if (k < row && k > foundKey)
                        {
                            foundKey = k;
                        }
                    }

                    if (foundKey == int.MinValue)
                    {
                        return;
                    }

                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label692);
                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }

                    SyntheticBondSeries syntheticBondSeries = seriesDictionary[foundKey];

                    if (seriesDictionary.ContainsKey(row)) // Удаление синтетической облигации (первая строка)
                    {
                        SyntheticBond firstSyntheticBond = syntheticBondSeries.SyntheticBonds[0];
                        firstSyntheticBond.Delete();
                        syntheticBondSeries.Save();
                        RePaintGrid();
                        return;
                    }

                    for (int i2 = 0; i2 < syntheticBondSeries.SyntheticBonds.Count; i2++)
                    {
                        if (foundKey + i2 != row) continue;

                        SyntheticBond syntheticBond = syntheticBondSeries.SyntheticBonds[i2];
                        syntheticBond.Delete();

                        syntheticBondSeries.SyntheticBonds.RemoveAt(i2);

                        Save();

                        break;
                    }

                    RePaintGrid();
                }
                else if (column == 4) // окно смещений
                {
                    bool isFirstRow = seriesDictionary.ContainsKey(row);
                    int foundKey = int.MinValue;

                    if (isFirstRow)
                    {
                        foundKey = row;
                    }
                    else
                    {
                        foreach (int k in seriesDictionary.Keys)
                        {
                            if (k < row && k > foundKey)
                            {
                                foundKey = k;
                            }
                        }
                    }

                    if (foundKey == int.MinValue)
                    {
                        return;
                    }

                    SyntheticBondSeries bond = seriesDictionary[foundKey];

                    SyntheticBond modificationSyntheticBond = null;
                    if (isFirstRow) // Выбор чарта фьючерса ( основная строка облигации)
                    {
                        modificationSyntheticBond = seriesDictionary[row].SyntheticBonds[0];
                    }
                    else // Выбор чарта других фьючерсов облигации
                    {
                        for (int i = 1; i < bond.SyntheticBonds.Count; i++)
                        {
                            if (row == foundKey + i)
                            {
                                modificationSyntheticBond = bond.SyntheticBonds[i];
                                break;
                            }
                        }
                    }

                    if (modificationSyntheticBond == null)
                    {
                        return;
                    }

                    bond.ShowOffsetWindow(ref modificationSyntheticBond);
                }
                else if (column == 5) // окно чарта
                {
                    bool isFirstRow = seriesDictionary.ContainsKey(row);
                    int foundKey = int.MinValue;

                    if (isFirstRow)
                    {
                        foundKey = row;
                    }
                    else
                    {
                        foreach (int k in seriesDictionary.Keys)
                        {
                            if (k < row && k > foundKey)
                            {
                                foundKey = k;
                            }
                        }
                    }

                    if (foundKey == int.MinValue)
                    {
                        return;
                    }

                    SyntheticBondSeries bond = seriesDictionary[foundKey];

                    SyntheticBond modificationSyntheticBond = null;
                    if (isFirstRow) // Выбор чарта фьючерса ( основная строка облигации)
                    {
                        modificationSyntheticBond = seriesDictionary[row].SyntheticBonds[0];
                    }
                    else // Выбор чарта других фьючерсов облигации
                    {
                        for (int i = 1; i < bond.SyntheticBonds.Count; i++)
                        {
                            if (row == foundKey + i)
                            {
                                modificationSyntheticBond = bond.SyntheticBonds[i];
                                break;
                            }
                        }
                    }

                    if (modificationSyntheticBond == null)
                    {
                        return;
                    }

                    bond.ShowChartWindow(ref modificationSyntheticBond);
                }
                else if (column == 6) // окно торговли
                {
                    bool isFirstRow = seriesDictionary.ContainsKey(row);
                    int foundKey = int.MinValue;

                    if (isFirstRow == true)
                    {
                        foundKey = row;
                    }
                    else
                    {
                        foreach (int k in seriesDictionary.Keys)
                        {
                            if (k < row && k > foundKey)
                            {
                                foundKey = k;
                            }
                        }
                    }

                    if (foundKey == int.MinValue)
                    {
                        return;
                    }

                    SyntheticBondSeries syntheticBondSeries = seriesDictionary[foundKey];

                    SyntheticBond syntheticBond = null;
                    if (isFirstRow) // Выбор чарта фьючерса ( основная строка облигации)
                    {
                        syntheticBond = seriesDictionary[row].SyntheticBonds[0];
                    }
                    else // Выбор чарта других фьючерсов облигации
                    {
                        for (int i = 1; i < syntheticBondSeries.SyntheticBonds.Count; i++)
                        {
                            if (row == foundKey + i)
                            {
                                syntheticBond = syntheticBondSeries.SyntheticBonds[i];
                                break;
                            }
                        }
                    }

                    if (syntheticBond == null)
                    {
                        return;
                    }

                    syntheticBondSeries.ShowTradeWindow(ref syntheticBond);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void SecuritySubscribeEvent(Security security)
        {
            RePaintGrid();
        }

        #endregion

        #region Helpers

        public void Clear()
        {
            for (int i = 0; i < SyntheticBondSeries.Count; i++)
            {
                SyntheticBondSeries series = SyntheticBondSeries[i];

                if (series.SyntheticBonds == null)
                    continue;

                series.Clear();
            }
        }

        public List<string> GetAllSecurityNames()
        {
            List<string> names = new List<string>();

            for (int i = 0; i < SyntheticBondSeries.Count; i++)
            {
                SyntheticBondSeries series = SyntheticBondSeries[i];

                if (series.PatternBaseTab == null ||
                        (series.PatternBaseTab != null && series.PatternBaseTab.Security == null))
                    continue;

                if (!string.IsNullOrEmpty(series.PatternBaseTab.Security.Name))
                    names.Add(series.PatternBaseTab.Security.Name);

                if (series.SyntheticBonds == null)
                    continue;

                for (int i2 = 0; i2 < series.SyntheticBonds.Count; i2++)
                {
                    SyntheticBond syntheticBond = series.SyntheticBonds[i2];

                    if (syntheticBond.PatternFuturesTab == null ||
                        (syntheticBond.PatternFuturesTab != null && syntheticBond.PatternFuturesTab.Security == null))
                        continue;

                    if (!string.IsNullOrEmpty(syntheticBond.PatternFuturesTab.Security.Name))
                        names.Add(syntheticBond.PatternFuturesTab.Security.Name);
                }
            }

            return names;
        }

        private void SyntheticBond_SeparationChangeEvent(SyntheticBond syntheticBondSettings)
        {
            SeparationChangeEvent?.Invoke(syntheticBondSettings);
        }

        private void CheckAllSecuritiesReadiness()
        {
            if (SyntheticBondSeries == null
                || SyntheticBondSeries.Count == 0)
            {
                return;
            }

            bool allReady = true;

            for (int i = 0; i < SyntheticBondSeries.Count; i++)
            {
                SyntheticBondSeries series = SyntheticBondSeries[i];

                if (series.IsReadyToTrade() == false)
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady == true && _allSecuritiesReadyState != true)
            {
                _allSecuritiesReadyState = true;

                if (AllSecuritiesReadyEvent != null)
                {
                    AllSecuritiesReadyEvent();
                }
            }
            else if (allReady == false && _allSecuritiesReadyState != false)
            {
                _allSecuritiesReadyState = false;

                if (AllSecuritiesNotReadyEvent != null)
                {
                    AllSecuritiesNotReadyEvent();
                }
            }
        }

        #endregion

        #region Logging

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
    }
}
