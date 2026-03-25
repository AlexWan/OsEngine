/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Entity.SynteticBondEntity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.OsTrader.Iceberg;
using OsEngine.OsTrader.Panels.Tab.SyntheticBondTab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.OsTrader.Iceberg;

namespace OsEngine.OsTrader.Panels.Tab.SynteticBondTab
{
    public class BotTabSyntheticBond : IIBotTab
    {
        #region Constructor

        public BotTabSyntheticBond(string nameTab, StartProgram startProgram)
        {
            TabName = nameTab;
            StartProgram = startProgram;

            LoadSynteticBond();
        }

        private void LoadSynteticBond()
        {
            try
            {
                if (!File.Exists(@"Engine\" + TabName + @"SynteticBondNamesToLoad.txt"))
                {
                    return;
                }

                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"SynteticBondNamesToLoad.txt"))
                {
                    _eventsIsOn = Convert.ToBoolean(reader.ReadLine());
                    _emulatorIsOn = Convert.ToBoolean(reader.ReadLine());

                    while (reader.EndOfStream == false)
                    {
                        string synteticBondName = reader.ReadLine();
                        int synteticBondNumber = Convert.ToInt32(reader.ReadLine());

                        SyntheticBondSeries newBond = new SyntheticBondSeries(StartProgram, synteticBondName);
                        newBond.SyntheticBondNum = synteticBondNumber;
                        newBond.SecuritySubscribeEvent += SecuritySubscribeEvent;
                        newBond.ContangoChangeEvent += SynteticBond_SeparationChangeEvent;
                        SynteticBondSeries.Add(newBond);
                    }

                    reader.Close();
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SaveSynteticBond()
        {
            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"SynteticBondNamesToLoad.txt", false))
                {
                    writer.WriteLine(_eventsIsOn);
                    writer.WriteLine(_emulatorIsOn);

                    for (int i = 0; i < SynteticBondSeries.Count; i++)
                    {
                        writer.WriteLine("SynteticBond" + (i + 1) + TabName);
                        writer.WriteLine(SynteticBondSeries[i].SyntheticBondNum);
                    }

                    writer.Close();
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
                for (int i = 0; i < SynteticBondSeries.Count; i++)
                {
                    SynteticBondSeries[i].EmulatorIsOn = value;
                }

                if (_emulatorIsOn == value)
                {
                    return;
                }
                _emulatorIsOn = value;
                SaveSynteticBond();
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
                for (int i = 0; i < SynteticBondSeries.Count; i++)
                {
                    SynteticBondSeries[i].EventsIsOn = value;
                }

                if (_eventsIsOn == value)
                {
                    return;
                }

                _eventsIsOn = value;
                SaveSynteticBond();
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

        public DateTime TimeServerCurrent
        {
            get
            {
                for (int i = 0; i < SynteticBondSeries.Count; i++)
                {
                    SyntheticBondSeries synteticBondSeries = SynteticBondSeries[i];

                    if (synteticBondSeries.BaseTab != null)
                    {
                        return synteticBondSeries.BaseTab.TimeServerCurrent;
                    }
                }

                return DateTime.MinValue;
            }
        }

        public void Delete()
        {
            try
            {
                _isDeleted = true;

                try
                {
                    if (File.Exists(@"Engine\" + TabName + @"StandartPairsSettings.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"StandartPairsSettings.txt");
                    }
                }
                catch
                {
                    // ignore
                }

                try
                {
                    if (File.Exists(@"Engine\" + TabName + @"StrategSettings.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"StrategSettings.txt");
                    }
                }
                catch
                {
                    // ignore
                }

                try
                {
                    if (File.Exists(@"Engine\" + TabName + @"PairsNamesToLoad.txt"))
                    {
                        File.Delete(@"Engine\" + TabName + @"PairsNamesToLoad.txt");
                    }
                }
                catch
                {
                    // ignore
                }

                for (int i = 0; i < SynteticBondSeries.Count; i++)
                {
                    SynteticBondSeries[i].DeleteSynteticBond();
                }

                if (SynteticBondSeries != null)
                {
                    SynteticBondSeries.Clear();
                    SynteticBondSeries = null;
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
        public List<SyntheticBondSeries> SynteticBondSeries = new List<SyntheticBondSeries>();

        private bool _emulatorIsOn = false;
        private bool _eventsIsOn = true;

        private bool _isDeleted;

        private bool? _allSecuritiesReadyState;
        private Thread _painterThread;
        private GlobalPositionViewer _positionViewer;

        #endregion

        #region Public fields

        public SyntheticBond MaxSyntheticBondSettings;

        public SyntheticBond MinSyntheticBondSettings;

        public event Action<SyntheticBond> SeparationChangeEvent;

        public event Action<Position, SignalType> UserSelectActionEvent;

        public event Action<string> UserClickOnPositionShowBotInTableEvent;

        public event Action<BotTabSimple> NewTabCreateEvent;

        public event Action<SyntheticBond, SyntheticBond> MaxSeparationBondChangedEvent;

        public event Action<SyntheticBond, SyntheticBond> MinSeparationBondChangedEvent;

        public event Action AllSecuritiesReadyEvent;

        public event Action AllSecuritiesNotReadyEvent;

        #endregion

        public void Clear()
        {
            for (int i = 0; i < SynteticBondSeries.Count; i++)
            {
                SyntheticBondSeries series = SynteticBondSeries[i];

                if (series.SyntheticBonds == null)
                {
                    continue;
                }

                for (int j = 0; j < series.SyntheticBonds.Count; j++)
                {
                    SyntheticBond settings = series.SyntheticBonds[j];

                    if (settings.BaseIcebergParameters != null
                        && settings.BaseIcebergParameters.BotTab != null)
                    {
                        settings.BaseIcebergParameters.BotTab.Clear();
                    }

                    if (settings.FuturesIcebergParameters != null
                        && settings.FuturesIcebergParameters.BotTab != null)
                    {
                        settings.FuturesIcebergParameters.BotTab.Clear();
                    }

                    if (settings.BaseRationingSecurity != null)
                    {
                        settings.BaseRationingSecurity.Clear();
                    }

                    if (settings.FuturesRationingSecurity != null)
                    {
                        settings.FuturesRationingSecurity.Clear();
                    }

                    for (int k = 0; k < settings.Scenarios.Count; k++)
                    {
                        if (settings.Scenarios[k].ArbitrationIceberg != null)
                        {
                            settings.Scenarios[k].ArbitrationIceberg.Pause();
                        }
                    }
                }
            }
        }

        public List<string> GetAllSecurityNames()
        {
            List<string> names = new List<string>();

            for (int i = 0; i < SynteticBondSeries.Count; i++)
            {
                SyntheticBondSeries series = SynteticBondSeries[i];

                if (series.BaseTab != null
                    && series.BaseTab.Security != null)
                {
                    names.Add(series.BaseTab.Security.Name);
                }

                if (series.SyntheticBonds == null)
                {
                    continue;
                }

                for (int j = 0; j < series.SyntheticBonds.Count; j++)
                {
                    SyntheticBond settings = series.SyntheticBonds[j];

                    if (settings.FuturesIcebergParameters != null
                        && settings.FuturesIcebergParameters.BotTab != null
                        && settings.FuturesIcebergParameters.BotTab.Security != null)
                    {
                        names.Add(settings.FuturesIcebergParameters.BotTab.Security.Name);
                    }

                    if (settings.BaseRationingSecurity != null
                        && settings.BaseRationingSecurity.Security != null)
                    {
                        names.Add(settings.BaseRationingSecurity.Security.Name);
                    }

                    if (settings.FuturesRationingSecurity != null
                        && settings.FuturesRationingSecurity.Security != null)
                    {
                        names.Add(settings.FuturesRationingSecurity.Security.Name);
                    }
                }
            }

            return names;
        }

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

                for (int i = 0; i < SynteticBondSeries.Count; i++)
                {
                    SyntheticBondSeries curBond = SynteticBondSeries[i];

                    for (int j = 0; curBond.SyntheticBonds != null && j < curBond.SyntheticBonds.Count; j++)
                    {
                        if (curBond.SyntheticBonds[j].BaseIcebergParameters != null &&
                            curBond.SyntheticBonds[j].BaseIcebergParameters.BotTab != null)
                        {
                            journals.Add(curBond.SyntheticBonds[j].BaseIcebergParameters.BotTab.GetJournal());
                        }

                        if (curBond.SyntheticBonds[j].FuturesIcebergParameters != null &&
                            curBond.SyntheticBonds[j].FuturesIcebergParameters.BotTab != null)
                        {
                            journals.Add(curBond.SyntheticBonds[j].FuturesIcebergParameters.BotTab.GetJournal());
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

            for (int i = 0; SynteticBondSeries != null && i < SynteticBondSeries.Count; i++)
            {
                SyntheticBondSeries bond = SynteticBondSeries[i];

                if (bond.SyntheticBonds == null)
                {
                    continue;
                }

                for (int i2 = 0; i2 < bond.SyntheticBonds.Count; i2++)
                {
                    if (i2 == 0)
                    {
                        rows.Add(GetFirstSynteticBondGridRow(bond, bond.SyntheticBonds[i2]));
                        continue;
                    }

                    rows.Add(GetSecondSynteticBondGridRow(bond.SyntheticBonds[i2]));
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

        private DataGridViewRow GetFirstSynteticBondGridRow(SyntheticBondSeries bond, SyntheticBond syntheticBondSettings)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            DataGridViewButtonCell numberCell = new DataGridViewButtonCell(); // 0 номер / удалить облигацию
            numberCell.Value = bond.SyntheticBondNum + " / " + OsLocalization.Trader.Label39;
            numberCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(numberCell);

            DataGridViewButtonCell baseCell = new DataGridViewButtonCell(); // 1 база облигации

            if (bond.BaseTab == null ||
               (bond.BaseTab.Connector == null) ||
               (bond.BaseTab.Connector.SecurityName == null))
            {
                baseCell.Value = "None";
                baseCell.Style.ForeColor = Color.Red;
            }
            else
                baseCell.Value = bond.BaseTab.Connector.SecurityName;

            baseCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(baseCell);

            DataGridViewButtonCell futuresCell = new DataGridViewButtonCell(); // 2 фьючерс облигации

            if (syntheticBondSettings == null ||
                (syntheticBondSettings.FuturesIcebergParameters.BotTab.Connector != null && syntheticBondSettings.FuturesIcebergParameters.BotTab.Connector.SecurityName == null))
            {
                futuresCell.Value = "None";
                futuresCell.Style.ForeColor = Color.Red;
            }
            else
                futuresCell.Value = syntheticBondSettings.FuturesIcebergParameters.BotTab.Connector.SecurityName;

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

            bool isExpired = syntheticBondSettings.DaysBeforeExpiration != -1
                && syntheticBondSettings.DaysBeforeExpiration <= 0;

            DataGridViewTextBoxCell deviationCell = new DataGridViewTextBoxCell(); // 7 Отклонение

            if (isExpired)
            {
                deviationCell.Value = "Экспирация";
                deviationCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBondSettings.AbsoluteSeparationCandles == null ||
                syntheticBondSettings.AbsoluteSeparationCandles != null && syntheticBondSettings.AbsoluteSeparationCandles.Count == 0)
            {
                deviationCell.Value = "None";
                deviationCell.Style.ForeColor = Color.Red;
            }
            else
            {
                deviationCell.Value = syntheticBondSettings.AbsoluteSeparationCandles[^1].Value.ToString();
            }

            deviationCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(deviationCell);

            DataGridViewTextBoxCell percentCell = new DataGridViewTextBoxCell(); // 8 в %

            if (isExpired)
            {
                percentCell.Value = "Экспирация";
                percentCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBondSettings.PercentSeparationCandles == null ||
                syntheticBondSettings.PercentSeparationCandles != null && syntheticBondSettings.PercentSeparationCandles.Count == 0)
            {
                percentCell.Value = "None";
                percentCell.Style.ForeColor = Color.Red;
            }
            else
            {
                if (MaxSyntheticBondSettings != null && MaxSyntheticBondSettings.FuturesIcebergParameters.BotTab.TabName == syntheticBondSettings.FuturesIcebergParameters.BotTab.TabName)
                {
                    percentCell.Style.ForeColor = Color.Green;
                }

                if (MinSyntheticBondSettings != null && MinSyntheticBondSettings.FuturesIcebergParameters.BotTab.TabName == syntheticBondSettings.FuturesIcebergParameters.BotTab.TabName)
                {
                    percentCell.Style.ForeColor = Color.Orange;
                }

                percentCell.Value = syntheticBondSettings.PercentSeparationCandles[^1].Value.ToString();
            }

            percentCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(percentCell);

            DataGridViewTextBoxCell daysCell = new DataGridViewTextBoxCell(); // 9 Дней

            if (isExpired)
            {
                daysCell.Value = "Экспирация";
                daysCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBondSettings.DaysBeforeExpiration == -1)
            {
                daysCell.Value = "None";
                daysCell.Style.ForeColor = Color.Red;
            }
            else
            {
                daysCell.Value = syntheticBondSettings.DaysBeforeExpiration.ToString();
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
                profitDaysCell.Value = Math.Round(syntheticBondSettings.ProfitPerDay, 5).ToString();
            }

            profitDaysCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(profitDaysCell);

            DataGridViewTextBoxCell positionCell = new DataGridViewTextBoxCell(); // 11 Позиция
            positionCell.Value = "B: 0; F: 0";
            positionCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(positionCell);

            return nRow;
        }

        private DataGridViewRow GetSecondSynteticBondGridRow(SyntheticBond syntheticBondSettings)
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

            if (syntheticBondSettings.FuturesIcebergParameters.BotTab.Connector.SecurityName != null)
            {
                futuresCell.Value = syntheticBondSettings.FuturesIcebergParameters.BotTab.Connector.SecurityName;
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

            bool isExpired = syntheticBondSettings.DaysBeforeExpiration != -1
                && syntheticBondSettings.DaysBeforeExpiration <= 0;

            DataGridViewTextBoxCell deviationCell = new DataGridViewTextBoxCell(); // 7 Отклонение

            if (isExpired)
            {
                deviationCell.Value = "Экспирация";
                deviationCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBondSettings.AbsoluteSeparationCandles == null ||
                syntheticBondSettings.AbsoluteSeparationCandles != null && syntheticBondSettings.AbsoluteSeparationCandles.Count == 0)
            {
                deviationCell.Value = "None";
                deviationCell.Style.ForeColor = Color.Red;
            }
            else
            {
                deviationCell.Value = syntheticBondSettings.AbsoluteSeparationCandles[^1].Value.ToString();
            }

            deviationCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(deviationCell);

            DataGridViewTextBoxCell percentCell = new DataGridViewTextBoxCell(); // 8 в %

            if (isExpired)
            {
                percentCell.Value = "Экспирация";
                percentCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBondSettings.PercentSeparationCandles == null ||
                syntheticBondSettings.PercentSeparationCandles != null && syntheticBondSettings.PercentSeparationCandles.Count == 0)
            {
                percentCell.Value = "None";
                percentCell.Style.ForeColor = Color.Red;
            }
            else
            {
                if (MaxSyntheticBondSettings != null && MaxSyntheticBondSettings.FuturesIcebergParameters.BotTab.TabName == syntheticBondSettings.FuturesIcebergParameters.BotTab.TabName)
                {
                    percentCell.Style.ForeColor = Color.Green;
                }

                if (MinSyntheticBondSettings != null && MinSyntheticBondSettings.FuturesIcebergParameters.BotTab.TabName == syntheticBondSettings.FuturesIcebergParameters.BotTab.TabName)
                {
                    percentCell.Style.ForeColor = Color.Orange;
                }

                percentCell.Value = syntheticBondSettings.PercentSeparationCandles[^1].Value.ToString();
            }

            percentCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(percentCell);

            DataGridViewTextBoxCell daysCell = new DataGridViewTextBoxCell(); // 9 Дней

            if (isExpired)
            {
                daysCell.Value = "Экспирация";
                daysCell.Style.ForeColor = Color.Red;
            }
            else if (syntheticBondSettings.DaysBeforeExpiration == -1)
            {
                daysCell.Value = "None";
                daysCell.Style.ForeColor = Color.Red;
            }
            else
            {
                daysCell.Value = syntheticBondSettings.DaysBeforeExpiration.ToString();
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
                profitDaysCell.Value = Math.Round(syntheticBondSettings.ProfitPerDay, 5).ToString();
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

        private void CreateSyntheticBond()
        {
            try
            {
                int number = 1;

                if (SynteticBondSeries.Count == 0)
                {
                    SyntheticBondSeries newBond = new SyntheticBondSeries(StartProgram, "SynteticBond" + number + TabName);
                    newBond.SyntheticBondNum = number;
                    newBond.ContangoChangeEvent += SynteticBond_SeparationChangeEvent;

                    newBond.SyntheticBonds = new List<SyntheticBond>();
                    string baseTabName = "SynteticBond" + number + TabName + "Base";
                    SyntheticBond bondSettings = CreateNewBondSettings(baseTabName, number);
                    newBond.SyntheticBonds.Add(bondSettings);

                    newBond.SecuritySubscribeEvent += SecuritySubscribeEvent;

                    SynteticBondSeries.Add(newBond);
                    newBond.SaveSettingsSyntheticBond();
                    SetJournalsInPosViewer();
                    return;
                }

                bool inserted = false;

                for (int i = 0; i < SynteticBondSeries.Count; i++)
                {
                    if (SynteticBondSeries[i].SyntheticBondNum > number)
                    {
                        SyntheticBondSeries newBond = new SyntheticBondSeries(StartProgram, "SynteticBond" + number + TabName);
                        newBond.SyntheticBondNum = number;
                        newBond.ContangoChangeEvent += SynteticBond_SeparationChangeEvent;

                        if (newBond.SyntheticBonds == null)
                        {
                            newBond.SyntheticBonds = new List<SyntheticBond>();
                        }

                        string baseTabName = "SynteticBond" + number + TabName + "Base";
                        SyntheticBond bondSettings = CreateNewBondSettings(baseTabName, number);
                        newBond.SyntheticBonds.Add(bondSettings);

                        newBond.SecuritySubscribeEvent += SecuritySubscribeEvent;

                        SynteticBondSeries.Insert(i, newBond);
                        inserted = true;
                        newBond.SaveSettingsSyntheticBond();
                        SetJournalsInPosViewer();
                        break;
                    }
                    else
                    {
                        number++;
                    }
                }

                if (!inserted)
                {
                    SyntheticBondSeries newBond = new SyntheticBondSeries(StartProgram, "SynteticBond" + number + TabName);
                    newBond.SyntheticBondNum = number;
                    newBond.ContangoChangeEvent += SynteticBond_SeparationChangeEvent;

                    newBond.SyntheticBonds = new List<SyntheticBond>();
                    string baseTabName = "SynteticBond" + number + TabName + "Base";
                    SyntheticBond bondSettings = CreateNewBondSettings(baseTabName, number);

                    newBond.SyntheticBonds.Add(bondSettings);
                    newBond.SecuritySubscribeEvent += SecuritySubscribeEvent;

                    SynteticBondSeries.Add(newBond);
                    newBond.SaveSettingsSyntheticBond();
                    SetJournalsInPosViewer();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private SyntheticBond CreateNewBondSettings(string baseTabName, int number)
        {
            string futuresTabName = "SynteticBond" + number + TabName + "Futures";

            SyntheticBond bondSettings = new SyntheticBond();

            BotTabSimple baseBotTab = new BotTabSimple(baseTabName, StartProgram);
            BotTabSimple futuresBotTab = new BotTabSimple(futuresTabName, StartProgram);

            bondSettings.BaseIcebergParameters = new ArbitrationParameters();
            bondSettings.BaseIcebergParameters.BotTab = baseBotTab;

            bondSettings.FuturesIcebergParameters = new ArbitrationParameters();
            bondSettings.FuturesIcebergParameters.BotTab = futuresBotTab;

            // Create the default "Script 1" scenario
            BondScenario defaultScenario = new BondScenario(futuresTabName, "Script 1");
            defaultScenario.SetBotTabs(baseBotTab, futuresBotTab);
            bondSettings.Scenarios.Add(defaultScenario);

            bondSettings.CointegrationBuilder = new CointegrationBuilder();
            bondSettings.MainRationingMode = RationingMode.Difference;

            return bondSettings;
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

                Dictionary<int, SyntheticBondSeries> bondsDictionary = new Dictionary<int, SyntheticBondSeries>(); // key - номер строки с базой облигации, value - кол-во фьючерсов

                int rowCount = 1;
                for (int i = 0; i < SynteticBondSeries.Count; i++)
                {
                    bondsDictionary[rowCount] = SynteticBondSeries[i];

                    if (SynteticBondSeries[i].SyntheticBonds == null)
                    {
                        rowCount += +1; // 1 это кнопка добавления фьючерса
                    }
                    else
                    {
                        rowCount += SynteticBondSeries[i].SyntheticBonds.Count + 1; // 1 это кнопка добавления фьючерса
                    }
                }

                if (column == 0) // создание новой облигации | удаление облигации
                {
                    if (_grid.Rows.Count - 1 == row) // создание новой облигации
                    {
                        CreateSyntheticBond();
                        SaveSynteticBond();
                        RePaintGrid();
                    }
                    else if (bondsDictionary.ContainsKey(row)) // удаление облигации
                    {
                        AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label691);
                        ui.ShowDialog();

                        if (ui.UserAcceptAction == false)
                        {
                            return;
                        }

                        SyntheticBondSeries bondDictionary = bondsDictionary[row];

                        for (int i = SynteticBondSeries.Count - 1; i >= 0; i--)
                        {
                            SyntheticBondSeries synteticBond = SynteticBondSeries[i];

                            if (bondDictionary.BaseTab.TabName != synteticBond.BaseTab.TabName) continue;

                            synteticBond.ContangoChangeEvent -= SynteticBond_SeparationChangeEvent;
                            synteticBond.SecuritySubscribeEvent -= SecuritySubscribeEvent;
                            synteticBond.DeleteSynteticBond();
                            SynteticBondSeries.RemoveAt(i);

                            break;
                        }

                        SaveSynteticBond();
                        RePaintGrid();
                    }
                }
                else if (column == 1) // выбор бумаги для базы
                {
                    if (bondsDictionary.ContainsKey(row))
                    {
                        SyntheticBondSeries bond = bondsDictionary[row];

                        bond.ChooseBaseSecurity();
                        bond.SaveSettingsSyntheticBond();
                    }
                }
                else if (column == 2) // выбор бумаги для фьючерса или добавление нового фьючерса
                {
                    if (bondsDictionary.ContainsKey(row)) // Выбор фьючерса ( основная строка облигации)
                    {
                        SyntheticBondSeries bond = bondsDictionary[row];
                        SyntheticBond firstSettings = bond.SyntheticBonds[0];

                        firstSettings.FuturesIcebergParameters.BotTab.SecuritySubscribeEvent -= SecuritySubscribeEvent;
                        firstSettings.FuturesIcebergParameters.BotTab.Clear();

                        bond.CloseTradeWindow(firstSettings);

                        for (int s = firstSettings.Scenarios.Count - 1; s >= 0; s--)
                        {
                            firstSettings.Scenarios[s].Delete();
                        }
                        firstSettings.Scenarios.Clear();

                        string futuresTabName = firstSettings.FuturesIcebergParameters.BotTab.TabName;
                        BondScenario defaultScenario = new BondScenario(futuresTabName, "Script 1");
                        defaultScenario.SetBotTabs(firstSettings.BaseIcebergParameters.BotTab, firstSettings.FuturesIcebergParameters.BotTab);
                        firstSettings.Scenarios.Add(defaultScenario);

                        firstSettings.CointegrationBuilder = new CointegrationBuilder();
                        firstSettings.PercentSeparationCandles.Clear();
                        firstSettings.AbsoluteSeparationCandles.Clear();
                        firstSettings.DaysBeforeExpiration = -1;
                        firstSettings.ProfitPerDay = 0;

                        bond.ChooseFuturesSecurity(firstSettings);
                        bond.SaveSettingsSyntheticBond();
                        RePaintGrid();
                    }
                    else
                    {
                        int foundKey = int.MinValue;
                        foreach (int k in bondsDictionary.Keys)
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

                        SyntheticBondSeries bond = bondsDictionary[foundKey];

                        if (row == bond.SyntheticBonds.Count + foundKey) // Добавить новый фьючерс
                        {
                            string botName = bond.SynteticBondName + "Futures";

                            int index = 1;
                            for (int i = 0; i < bond.SyntheticBonds.Count; i++)
                            {
                                if (botName + (bond.SyntheticBonds.Count + index) == bond.SyntheticBonds[i].FuturesIcebergParameters.BotTab.TabName)
                                {
                                    index++;
                                }
                            }

                            string newFuturesTabName = botName + (bond.SyntheticBonds.Count + index);
                            string newBaseTabName = bond.SynteticBondName + "Base" + (bond.SyntheticBonds.Count + index);

                            SyntheticBond settingsFutures = new SyntheticBond();
                            BotTabSimple newBaseBotTab = new BotTabSimple(newBaseTabName, StartProgram);
                            BotTabSimple futuresBotTab = new BotTabSimple(newFuturesTabName, StartProgram);

                            settingsFutures.BaseIcebergParameters = new ArbitrationParameters();
                            settingsFutures.BaseIcebergParameters.BotTab = newBaseBotTab;

                            settingsFutures.FuturesIcebergParameters = new ArbitrationParameters();
                            settingsFutures.FuturesIcebergParameters.BotTab = futuresBotTab;

                            BondScenario defaultScenario = new BondScenario(newFuturesTabName, "Script 1");
                            defaultScenario.SetBotTabs(newBaseBotTab, futuresBotTab);
                            settingsFutures.Scenarios.Add(defaultScenario);

                            settingsFutures.CointegrationBuilder = new CointegrationBuilder();

                            bond.ContangoChangeEvent += SynteticBond_SeparationChangeEvent;
                            bond.SyntheticBonds.Add(settingsFutures);
                            bond.PropagateBaseSecurityToAll();
                            bond.SaveSettingsSyntheticBond();
                            RePaintGrid();
                        }
                        else // выбрать бумагу для фьючерса
                        {
                            for (int i = 1; i < bond.SyntheticBonds.Count; i++)
                            {
                                if (row == foundKey + i)
                                {
                                    bond.ChooseFuturesSecurity(bond.SyntheticBonds[i]);
                                    bond.SaveSettingsSyntheticBond();
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (column == 3) // удаление фьючерса
                {
                    int foundKey = int.MinValue;
                    foreach (int k in bondsDictionary.Keys)
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

                    SyntheticBondSeries bondDictionary = bondsDictionary[foundKey];

                    if (bondsDictionary.ContainsKey(row)) // Удаление фьючерса ( основная строка облигации)
                    {
                        SyntheticBond firstSettings = bondDictionary.SyntheticBonds[0];

                        firstSettings.FuturesIcebergParameters.BotTab.SecuritySubscribeEvent -= SecuritySubscribeEvent;
                        firstSettings.FuturesIcebergParameters.BotTab.Clear();

                        for (int s = firstSettings.Scenarios.Count - 1; s >= 0; s--)
                        {
                            firstSettings.Scenarios[s].Delete();
                        }
                        firstSettings.Scenarios.Clear();

                        bondDictionary.ContangoChangeEvent -= SynteticBond_SeparationChangeEvent;
                        bondDictionary.SaveSettingsSyntheticBond();
                        RePaintGrid();
                        return;
                    }

                    for (int i = SynteticBondSeries.Count - 1; i >= 0; i--)
                    {
                        SyntheticBondSeries synteticBond = SynteticBondSeries[i];

                        if (bondDictionary.BaseTab.TabName != synteticBond.BaseTab.TabName) continue;

                        for (int i2 = 0; i2 < synteticBond.SyntheticBonds.Count; i2++)
                        {
                            if (foundKey + i2 + 1 != row) continue;

                            SyntheticBond settingsToRemove = synteticBond.SyntheticBonds[i2 + 1];
                            settingsToRemove.FuturesIcebergParameters.BotTab.SecuritySubscribeEvent -= SecuritySubscribeEvent;
                            settingsToRemove.FuturesIcebergParameters.BotTab.Delete();

                            for (int s = 0; s < settingsToRemove.Scenarios.Count; s++)
                            {
                                settingsToRemove.Scenarios[s].Delete();
                            }

                            synteticBond.ContangoChangeEvent -= SynteticBond_SeparationChangeEvent;
                            synteticBond.SyntheticBonds.RemoveAt(i2 + 1);
                            synteticBond.SaveSettingsSyntheticBond();

                            break;
                        }
                    }

                    RePaintGrid();
                }
                else if (column == 4) // окно смещений
                {
                    bool isFirstRow = bondsDictionary.ContainsKey(row);
                    int foundKey = int.MinValue;

                    if (isFirstRow)
                    {
                        foundKey = row;
                    }
                    else
                    {
                        foreach (int k in bondsDictionary.Keys)
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

                    SyntheticBondSeries bond = bondsDictionary[foundKey];

                    SyntheticBond modificationSyntheticBond = null;
                    if (isFirstRow) // Выбор чарта фьючерса ( основная строка облигации)
                    {
                        modificationSyntheticBond = bondsDictionary[row].SyntheticBonds[0];
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
                    bool isFirstRow = bondsDictionary.ContainsKey(row);
                    int foundKey = int.MinValue;

                    if (isFirstRow)
                    {
                        foundKey = row;
                    }
                    else
                    {
                        foreach (int k in bondsDictionary.Keys)
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

                    SyntheticBondSeries bond = bondsDictionary[foundKey];

                    SyntheticBond modificationSyntheticBond = null;
                    if (isFirstRow) // Выбор чарта фьючерса ( основная строка облигации)
                    {
                        modificationSyntheticBond = bondsDictionary[row].SyntheticBonds[0];
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
                    bool isFirstRow = bondsDictionary.ContainsKey(row);
                    int foundKey = int.MinValue;

                    if (isFirstRow == true)
                    {
                        foundKey = row;
                    }
                    else
                    {
                        foreach (int k in bondsDictionary.Keys)
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

                    SyntheticBondSeries bond = bondsDictionary[foundKey];

                    SyntheticBond modificationSyntheticBond = null;
                    if (isFirstRow) // Выбор чарта фьючерса ( основная строка облигации)
                    {
                        modificationSyntheticBond = bondsDictionary[row].SyntheticBonds[0];
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

                    bond.ShowTradeWindow(ref modificationSyntheticBond);
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

        private void SynteticBond_SeparationChangeEvent(SyntheticBond syntheticBondSettings)
        {
            SeparationChangeEvent?.Invoke(syntheticBondSettings);
        }

        private void CheckAllSecuritiesReadiness()
        {
            if (SynteticBondSeries == null
                || SynteticBondSeries.Count == 0)
            {
                return;
            }

            bool allReady = true;

            for (int i = 0; i < SynteticBondSeries.Count; i++)
            {
                SyntheticBondSeries series = SynteticBondSeries[i];

                if (series.BaseTab == null
                    || series.BaseTab.IsConnected == false
                    || series.BaseTab.IsReadyToTrade == false)
                {
                    allReady = false;
                    break;
                }

                if (series.SyntheticBonds == null)
                {
                    allReady = false;
                    break;
                }

                for (int j = 0; j < series.SyntheticBonds.Count; j++)
                {
                    SyntheticBond settings = series.SyntheticBonds[j];

                    if (settings.FuturesIcebergParameters == null
                        || settings.FuturesIcebergParameters.BotTab == null
                        || settings.FuturesIcebergParameters.BotTab.IsConnected == false
                        || settings.FuturesIcebergParameters.BotTab.IsReadyToTrade == false)
                    {
                        allReady = false;
                        break;
                    }
                }

                if (allReady == false)
                {
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
