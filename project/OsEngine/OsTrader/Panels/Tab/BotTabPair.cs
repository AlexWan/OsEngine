/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels.Tab.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Threading;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    ///  tab - for trading pairs
    /// </summary>
    public class BotTabPair : IIBotTab
    {
        public BotTabPair(string name, StartProgram startProgram)
        {
            TabName = name;
            _startProgram = startProgram;
            StandartManualControl = new BotManualControl(name, null, _startProgram);

            LoadStandartSettings();
            LoadPairs();
        }

        StartProgram _startProgram;

        /// <summary>
        /// source type
        /// </summary>
        public BotTabType TabType
        {
            get
            {
                return BotTabType.Pair;
            }
        }

        /// <summary>
        /// Unique robot name
        /// </summary>
        public string TabName { get; set; }

        /// <summary>
        /// Tab number
        /// </summary>
        public int TabNum { get; set; }

        /// <summary>
        /// Whether the submission of events to the top is enabled or not
        /// </summary>
        public bool EventsIsOn
        {
            get
            {
                return _eventsIsOn;
            }
            set
            {
                if (_eventsIsOn == value)
                {
                    return;
                }
                _eventsIsOn = value;
            }
        }

        private bool _eventsIsOn = true;

        /// <summary>
        /// Is the emulator enabled
        /// </summary>
        public bool EmulatorIsOn { get; set; }

        /// <summary>
        /// Time of the last update of the candle
        /// </summary>
        public DateTime LastTimeCandleUpdate { get; set; }

        public void Clear()
        {

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

                for (int i = 0; i < Pairs.Count; i++)
                {
                    Pairs[i].Delete();
                    Pairs[i].CointegrationPositionSideChangeEvent -= Pair_CointegrationPositionSideChangeEvent;
                    Pairs[i].CorrelationChangeEvent -= NewPair_CorrelationChangeEvent;
                }

                if (_grid != null)
                {
                    _grid.Rows.Clear();
                    _grid.CellClick -= _grid_CellClick;
                    DataGridFactory.ClearLinks(_grid);
                }
                if (_host != null)
                {
                    _host.Child = null;
                    _host = null;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public List<Journal.Journal> GetJournals()
        {
            try
            {
                List<Journal.Journal> journals = new List<Journal.Journal>();

                /* for (int i = 0; i < Tabs.Count; i++)
                 {
                     journals.Add(Tabs[i].GetJournal());
                 }*/

                return journals;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        // общие настройки

        public OrderPriceType Sec1OrderPriceType = OrderPriceType.Market;

        public decimal Sec1SlippagePercent = 0;

        public decimal Sec1Volume = 7;

        public OrderPriceType Sec2OrderPriceType = OrderPriceType.Market;

        public decimal Sec2SlippagePercent = 0;

        public decimal Sec2Volume = 7;

        public int CorrelationLookBack = 50;

        public int CointegrationLookBack = 50;

        public decimal CointegrationDeviation = 1;

        public BotManualControl StandartManualControl;

        public bool SecondByMarket;

        public MainGridPairSortType SortGridType;

        public void SaveStandartSettings()
        {
            if (_startProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }

            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"StandartPairsSettings.txt", false))
                {

                    writer.WriteLine(Sec1OrderPriceType);
                    writer.WriteLine(Sec1SlippagePercent);
                    writer.WriteLine(Sec1Volume);
                    writer.WriteLine(Sec2OrderPriceType);
                    writer.WriteLine(Sec2SlippagePercent);
                    writer.WriteLine(Sec2Volume);
                    writer.WriteLine(CorrelationLookBack);
                    writer.WriteLine(CointegrationDeviation);
                    writer.WriteLine(CointegrationLookBack);
                    writer.WriteLine(SecondByMarket);
                    writer.WriteLine(SortGridType);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void LoadStandartSettings()
        {
            if (!File.Exists(@"Engine\" + TabName + @"StandartPairsSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"StandartPairsSettings.txt"))
                {
                    Enum.TryParse(reader.ReadLine(), out Sec1OrderPriceType);
                    Sec1SlippagePercent = reader.ReadLine().ToDecimal();
                    Sec1Volume = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out Sec2OrderPriceType);
                    Sec2SlippagePercent = reader.ReadLine().ToDecimal();
                    Sec2Volume = Convert.ToInt32(reader.ReadLine());
                    CorrelationLookBack = Convert.ToInt32(reader.ReadLine());
                    CointegrationDeviation = reader.ReadLine().ToDecimal();
                    CointegrationLookBack = Convert.ToInt32(reader.ReadLine());
                    SecondByMarket = Convert.ToBoolean(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out SortGridType);

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        // хранение пар

        public List<PairToTrade> Pairs = new List<PairToTrade>();

        public void CreatePair()
        {
            int number = 0;

            for (int i = 0; i < Pairs.Count; i++)
            {
                if (Pairs[i].PairNum >= number)
                {
                    number = Pairs[i].PairNum + 1;
                }
            }

            PairToTrade pair = new PairToTrade(TabName + number, _startProgram);
            pair.PairNum = number;

            pair.Sec1OrderPriceType = Sec1OrderPriceType;
            pair.Sec1SlippagePercent = Sec1SlippagePercent;
            pair.Sec1Volume = Sec1Volume;
            pair.Sec2OrderPriceType = Sec2OrderPriceType;
            pair.Sec2SlippagePercent = Sec2SlippagePercent;
            pair.Sec2Volume = Sec2Volume;
            pair.CorrelationLookBack = CorrelationLookBack;
            pair.CointegrationDeviation = CointegrationDeviation;
            pair.CointegrationLookBack = CointegrationLookBack;
            pair.SecondByMarket = SecondByMarket;

            CopyPositionControllerSettings(pair.Tab1, StandartManualControl);
            CopyPositionControllerSettings(pair.Tab2, StandartManualControl);

            pair.Save();

            Pairs.Add(pair);

            SavePairNames();

            pair.CointegrationPositionSideChangeEvent += Pair_CointegrationPositionSideChangeEvent;
            pair.CorrelationChangeEvent += NewPair_CorrelationChangeEvent;
        }

        private void CopyPositionControllerSettings(BotTabSimple tab, BotManualControl control)
        {
            tab.ManualPositionSupport.SecondToClose = control.SecondToClose;
            tab.ManualPositionSupport.SecondToOpen = control.SecondToOpen;
            tab.ManualPositionSupport.DoubleExitIsOn = control.DoubleExitIsOn;
            tab.ManualPositionSupport.DoubleExitSlipage = control.DoubleExitSlipage;
            tab.ManualPositionSupport.ProfitDistance = control.ProfitDistance;
            tab.ManualPositionSupport.ProfitIsOn = control.ProfitIsOn;
            tab.ManualPositionSupport.ProfitSlipage = control.ProfitSlipage;
            tab.ManualPositionSupport.SecondToCloseIsOn = control.SecondToCloseIsOn;
            tab.ManualPositionSupport.SecondToOpenIsOn = control.SecondToOpenIsOn;
            tab.ManualPositionSupport.SetbackToCloseIsOn = control.SetbackToCloseIsOn;
            tab.ManualPositionSupport.SetbackToClosePosition = control.SetbackToOpenPosition;
            tab.ManualPositionSupport.SetbackToOpenIsOn = control.SetbackToOpenIsOn;
            tab.ManualPositionSupport.SetbackToOpenPosition = control.SetbackToOpenPosition;
            tab.ManualPositionSupport.StopDistance = control.StopDistance;
            tab.ManualPositionSupport.StopIsOn = control.StopIsOn;
            tab.ManualPositionSupport.StopSlipage = control.StopSlipage;
            tab.ManualPositionSupport.TypeDoubleExitOrder = control.TypeDoubleExitOrder;
            tab.ManualPositionSupport.ValuesType = control.ValuesType;



        }

        public void DeletePair(int numberInArray)
        {
            for (int i = 0; i < Pairs.Count; i++)
            {
                if (Pairs[i].PairNum == numberInArray)
                {
                    Pairs[i].CointegrationPositionSideChangeEvent -= Pair_CointegrationPositionSideChangeEvent;
                    Pairs[i].CorrelationChangeEvent -= NewPair_CorrelationChangeEvent;
                    Pairs[i].Delete();
                    Pairs.RemoveAt(i);
                    SavePairNames();
                    RePaintGrid();
                    return;
                }
            }
        }

        public void SavePairNames()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + TabName + @"PairsNamesToLoad.txt", false))
                {

                    for (int i = 0; i < Pairs.Count; i++)
                    {
                        writer.WriteLine(Pairs[i].Name);
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void LoadPairs()
        {
            if (!File.Exists(@"Engine\" + TabName + @"PairsNamesToLoad.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + TabName + @"PairsNamesToLoad.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string pairName = reader.ReadLine();
                        PairToTrade newPair = new PairToTrade(pairName, _startProgram);
                        newPair.CointegrationPositionSideChangeEvent += Pair_CointegrationPositionSideChangeEvent;
                        newPair.CorrelationChangeEvent += NewPair_CorrelationChangeEvent;
                        Pairs.Add(newPair);
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public void ApplySettingsFromStandartToAll()
        {
            for (int i = 0; i < Pairs.Count; i++)
            {
                PairToTrade pair = Pairs[i];

                pair.Sec1OrderPriceType = Sec1OrderPriceType;
                pair.Sec1SlippagePercent = Sec1SlippagePercent;
                pair.Sec1Volume = Sec1Volume;
                pair.Sec2OrderPriceType = Sec2OrderPriceType;
                pair.Sec2SlippagePercent = Sec2SlippagePercent;
                pair.Sec2Volume = Sec2Volume;
                pair.CorrelationLookBack = CorrelationLookBack;
                pair.CointegrationDeviation = CointegrationDeviation;
                pair.CointegrationLookBack = CointegrationLookBack;
                pair.SecondByMarket = SecondByMarket;

                CopyPositionControllerSettings(pair.Tab1, StandartManualControl);
                CopyPositionControllerSettings(pair.Tab2, StandartManualControl);

                pair.Save();
            }
        }

        public void SortPairsArray()
        {
            if (Pairs == null ||
                Pairs.Count == 0)
            {
                return;
            }

            if (SortGridType == MainGridPairSortType.No)
            { // по номерам
                for (int i = 1; i < Pairs.Count; i++)
                {
                    for (int i2 = i; i2 < Pairs.Count; i2++)
                    {
                        if (Pairs[i2].PairNum < Pairs[i2 - 1].PairNum)
                        {
                            PairToTrade pair = Pairs[i2];
                            Pairs[i2] = Pairs[i2 - 1];
                            Pairs[i2 - 1] = pair;
                        }
                    }
                }
            }
            else if (SortGridType == MainGridPairSortType.Side)
            { // по стороне коинтеграции

                for (int i = 1; i < Pairs.Count; i++)
                {
                    for (int i2 = i; i2 < Pairs.Count; i2++)
                    {// up - в начало
                        if (Pairs[i2].SideCointegrationValue == CointegrationLineSide.Up
                            && Pairs[i2 - 1].SideCointegrationValue != CointegrationLineSide.Up)
                        {
                            PairToTrade pair = Pairs[i2];
                            Pairs[i2] = Pairs[i2 - 1];
                            Pairs[i2 - 1] = pair;
                        }
                    }

                    for (int i2 = i; i2 < Pairs.Count; i2++)
                    { // no - в конец
                        if (Pairs[i2].SideCointegrationValue != CointegrationLineSide.No
                            && Pairs[i2 - 1].SideCointegrationValue == CointegrationLineSide.No)
                        {
                            PairToTrade pair = Pairs[i2];
                            Pairs[i2] = Pairs[i2 - 1];
                            Pairs[i2 - 1] = pair;
                        }
                    }
                }

            }
            else if (SortGridType == MainGridPairSortType.Correlation)
            { // по наивысшему значению корреляции
                for (int i = 1; i < Pairs.Count; i++)
                {
                    for (int i2 = i; i2 < Pairs.Count; i2++)
                    {
                        if (Pairs[i2].CorrelationLast > Pairs[i2 - 1].CorrelationLast
                            || Pairs[i2].CorrelationLast != 0 && Pairs[i2 - 1].CorrelationLast == 0)
                        {
                            PairToTrade pair = Pairs[i2];
                            Pairs[i2] = Pairs[i2 - 1];
                            Pairs[i2 - 1] = pair;
                        }
                    }
                }
            }
        }

        // исходящие события

        private void NewPair_CorrelationChangeEvent(List<PairIndicatorValue> arg1, PairToTrade arg2)
        {
            if (SortGridType == MainGridPairSortType.Correlation)
            {
                SortPairsArray();
            }
        }

        private void Pair_CointegrationPositionSideChangeEvent(CointegrationLineSide arg1, PairToTrade arg2)
        {
            if (SortGridType == MainGridPairSortType.Side)
            {
                SortPairsArray();
            }
        }

        // прорисовка таблицы

        Thread painterThread;

        private void PainterThread()
        {
            while (true)
            {
                Thread.Sleep(2000);

                if (_isDeleted)
                {
                    return;
                }

                TryRePaintGrid();
            }
        }

        private bool _isDeleted;

        WindowsFormsHost _host;

        public void StartPaint(WindowsFormsHost host)
        {
            _host = host;

            if (_grid == null)
            {
                CreateGrid();
            }

            RePaintGrid();

            _host.Child = _grid;

            if (painterThread == null)
            {
                painterThread = new Thread(PainterThread);
                painterThread.Start();
            }
        }

        public void StopPaint()
        {
            if(_host != null)
            {
                _host.Child = null;
            }
        }

        private void CreateGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            newGrid.ScrollBars = ScrollBars.Vertical;
            DataGridViewCellStyle style = newGrid.DefaultCellStyle;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "";// pairNum
            colum0.ReadOnly = true;
            colum0.Width = 70;
            newGrid.Columns.Add(colum0);

            for (int i = 0; i < 5; i++)
            {
                DataGridViewColumn columN = new DataGridViewColumn();
                columN.CellTemplate = cell0;
                columN.HeaderText = "";
                columN.ReadOnly = false;
                columN.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                newGrid.Columns.Add(columN);
            }

            _grid = newGrid;
            _grid.CellClick += _grid_CellClick;
        }

        private void RePaintGrid()
        {
            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action(RePaintGrid));
                return;
            }

            _grid.Rows.Clear();

            List<DataGridViewRow> rows = GetRowsToGrid();

            if (rows == null)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                _grid.Rows.Add(rows[i]);
            }
        }

        private void TryRePaintGrid()
        {
            List<DataGridViewRow> rows = GetRowsToGrid();

            if (rows == null)
            {
                return;
            }

            if (rows.Count != _grid.Rows.Count)
            {// 1 кол-во строк изменилось - перерисовываем полностью
                RePaintGrid();
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Cells[0].Value != null && _grid.Rows[i].Cells[0].Value != null)
                {
                    if (rows[i].Cells[0].Value.ToString() != _grid.Rows[i].Cells[0].Value.ToString())
                    {
                        // 2 сортировка поменялась. Перерисовываем полностью
                        RePaintGrid();
                        return;
                    }
                    continue;
                }
                if (rows[i].Cells[0].Value == null && _grid.Rows[i].Cells[0].Value != null)
                {
                    // 2 сортировка поменялась. Перерисовываем полностью
                    RePaintGrid();
                    return;
                }
                if (rows[i].Cells[0].Value != null && _grid.Rows[i].Cells[0].Value == null)
                {
                    // 2 сортировка поменялась. Перерисовываем полностью
                    RePaintGrid();
                    return;
                }

                if (rows[i].Cells[0].Value != _grid.Rows[i].Cells[0].Value)
                {
                    // 2 сортировка поменялась. Перерисовываем полностью
                    RePaintGrid();
                    return;
                }
            }

            DataGridViewRow firstOldRow = _grid.Rows[0];

            MainGridPairSortType sideFromTable;

            if (firstOldRow.Cells[5].Value.ToString().EndsWith("Side"))
            {
                sideFromTable = MainGridPairSortType.Side;
            }
            else if (firstOldRow.Cells[5].Value.ToString().EndsWith("Correlation"))
            {
                sideFromTable = MainGridPairSortType.Correlation;
            }
            else
            {
                sideFromTable = MainGridPairSortType.No;
            }

            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].Cells[1].Value == null)
                {
                    continue;
                }

                TryRePaintRow(_grid.Rows[i], rows[i]);
            }

            if (sideFromTable != SortGridType)
            {
                SortGridType = sideFromTable;
                SortPairsArray();
                SaveStandartSettings();
            }
        }

        private void TryRePaintRow(DataGridViewRow rowInGrid, DataGridViewRow rowInArray)
        {
            if (_grid.InvokeRequired)
            {
                _grid.Invoke(new Action<DataGridViewRow, DataGridViewRow>(TryRePaintRow), rowInGrid, rowInArray);
                return;
            }

            try
            {
                if (rowInGrid.Cells[1].Value.ToString() != rowInArray.Cells[1].Value.ToString())
                {
                    rowInGrid.Cells[1].Value = rowInArray.Cells[1].Value.ToString();

                    if (rowInGrid.Cells[1].Value.ToString().EndsWith("No"))
                    {
                        rowInGrid.Cells[1].Style.BackColor = System.Drawing.Color.Black;
                        rowInGrid.Cells[1].Style.SelectionBackColor = System.Drawing.Color.Black;
                    }
                    else if (rowInGrid.Cells[1].Value.ToString().EndsWith("Up"))
                    {
                        rowInGrid.Cells[1].Style.BackColor = System.Drawing.Color.DarkGreen;
                        rowInGrid.Cells[1].Style.SelectionBackColor = System.Drawing.Color.DarkGreen;
                    }
                    else if (rowInGrid.Cells[1].Value.ToString().EndsWith("Down"))
                    {
                        rowInGrid.Cells[1].Style.BackColor = System.Drawing.Color.DarkRed;
                        rowInGrid.Cells[1].Style.SelectionBackColor = System.Drawing.Color.DarkRed;
                    }
                }
                if (rowInGrid.Cells[2].Value.ToString() != rowInArray.Cells[2].Value.ToString())
                {
                    rowInGrid.Cells[2].Value = rowInArray.Cells[2].Value.ToString();
                }
                if (rowInGrid.Cells[3].Value != null &&
                    rowInGrid.Cells[3].Value.ToString() != rowInArray.Cells[3].Value.ToString())
                {
                    rowInGrid.Cells[3].Value = rowInArray.Cells[3].Value.ToString();
                }
                if (rowInGrid.Cells[4].Value.ToString() != rowInArray.Cells[4].Value.ToString())
                {
                    rowInGrid.Cells[4].Value = rowInArray.Cells[4].Value.ToString();
                }
            }
            catch (Exception error)
            {
               
            }
        }

        private List<DataGridViewRow> GetRowsToGrid()
        {
            try
            {
                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                rows.Add(GetFirstGridRow());

                for (int i = 0; i < Pairs.Count; i++)
                {
                    rows.Add(GetPairRowOne(Pairs[i]));
                    rows.Add(GetPairRowTwo(Pairs[i]));
                    rows.Add(GetPairRowThree(Pairs[i]));
                    rows.Add(GetPairRowFour(Pairs[i]));
                }

                rows.Add(GetLastGridRow());

                return rows;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private DataGridViewRow GetFirstGridRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // Общие настройки
            button.Value = OsLocalization.Trader.Label232;
            nRow.Cells.Add(button);

            DataGridViewComboBoxCell comboBox = new DataGridViewComboBoxCell();// Сортировка
            comboBox.Items.Add(OsLocalization.Trader.Label233 + ": " + MainGridPairSortType.No.ToString());
            comboBox.Items.Add(OsLocalization.Trader.Label233 + ": " + MainGridPairSortType.Side.ToString());
            comboBox.Items.Add(OsLocalization.Trader.Label233 + ": " + MainGridPairSortType.Correlation.ToString());

            comboBox.Value = OsLocalization.Trader.Label233 + ": " + SortGridType.ToString();

            nRow.Cells.Add(comboBox);

            return nRow;
        }

        private DataGridViewRow GetPairRowOne(PairToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = OsLocalization.Trader.Label234;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            for (int i = 0; i < nRow.Cells.Count; i++)
            {
                nRow.Cells[i].Style.SelectionBackColor = System.Drawing.Color.Black;
                nRow.Cells[i].Style.BackColor = System.Drawing.Color.Black;
            }

            return nRow;
        }

        private DataGridViewRow GetPairRowTwo(PairToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = pair.PairNum.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = "Side: " + pair.SideCointegrationValue;

            if (pair.SideCointegrationValue == CointegrationLineSide.No)
            {
                nRow.Cells[1].Style.BackColor = System.Drawing.Color.Black;
                nRow.Cells[1].Style.SelectionBackColor = System.Drawing.Color.Black;
            }
            else if (pair.SideCointegrationValue == CointegrationLineSide.Up)
            {
                nRow.Cells[1].Style.BackColor = System.Drawing.Color.DarkGreen;
                nRow.Cells[1].Style.SelectionBackColor = System.Drawing.Color.DarkGreen;
            }
            else if (pair.SideCointegrationValue == CointegrationLineSide.Down)
            {
                nRow.Cells[1].Style.BackColor = System.Drawing.Color.DarkRed;
                nRow.Cells[1].Style.SelectionBackColor = System.Drawing.Color.DarkRed;
            }


            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = "Corr: " + pair.CorrelationLast;

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = "Coin: " + pair.CointegrationLast;

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // Чарт
            button.Value = OsLocalization.Trader.Label172;
            nRow.Cells.Add(button);

            DataGridViewButtonCell button2 = new DataGridViewButtonCell(); // Удалить
            button2.Value = OsLocalization.Trader.Label39;
            nRow.Cells.Add(button2);

            return nRow;
        }

        private DataGridViewRow GetPairRowThree(PairToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // бумага

            if (pair.Tab1.Connector != null &&
                string.IsNullOrEmpty(pair.Tab1.Connector.SecurityName) == false)
            {
                button.Value = pair.Tab1.Connector.SecurityName;
            }
            else
            {
                button.Value = OsLocalization.Trader.Label235;
            }

            nRow.Cells.Add(button);

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = "bid: " + pair.Tab1.PriceBestBid.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = pair.Tab1.Connector.ServerType.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = "pos: " + pair.Tab1.VolumeNetto.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            return nRow;
        }

        private DataGridViewRow GetPairRowFour(PairToTrade pair)
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // бумага

            if (pair.Tab2.Connector != null &&
                string.IsNullOrEmpty(pair.Tab2.Connector.SecurityName) == false)
            {
                button.Value = pair.Tab2.Connector.SecurityName;
            }
            else
            {
                button.Value = OsLocalization.Trader.Label235;
            }

            nRow.Cells.Add(button);

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = "bid: " + pair.Tab2.PriceBestBid.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = pair.Tab2.Connector.ServerType.ToString();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[4].Value = "pos: " + pair.Tab2.VolumeNetto.ToStringWithNoEndZero();

            nRow.Cells.Add(new DataGridViewTextBoxCell());

            return nRow;
        }

        private DataGridViewRow GetLastGridRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells.Add(new DataGridViewTextBoxCell());

            DataGridViewButtonCell button = new DataGridViewButtonCell(); // добавить пару
            button.Value = OsLocalization.Trader.Label236;
            nRow.Cells.Add(button);

            return nRow;
        }

        DataGridView _grid;

        private void _grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            int column = e.ColumnIndex;
            int row = e.RowIndex;

            if (_grid.Rows.Count == row + 1 &&
                column == 5)
            { // создание вкладки
                CreatePair();
                RePaintGrid();
            }
            else if (column == 5)
            {// возможно удаление

                int tabNum = -1;

                try
                {
                    if (_grid.Rows[row].Cells[0].Value == null)
                    {
                        return;
                    }

                    tabNum = Convert.ToInt32(_grid.Rows[row].Cells[0].Value.ToString());
                }
                catch
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label237);

                ui.ShowDialog();

                if (ui.UserAcceptActioin)
                {
                    DeletePair(tabNum);
                }
            }
            else if (column == 1)
            { // возможно кнопка подключения бумаги
                if (_grid.Rows[row].Cells[0].Value != null)
                {
                    return;
                }

                int pairNum = -1;
                int tabNum = -1;

                for (int i = row - 1; i >= 0; i--)
                {
                    if (_grid.Rows[i].Cells[0].Value != null)
                    {
                        pairNum = Convert.ToInt32(_grid.Rows[i].Cells[0].Value);

                        if (i == row - 1)
                        {
                            tabNum = 1;
                        }
                        else
                        {
                            tabNum = 2;
                        }
                        break;
                    }
                }

                PairToTrade pair = null;

                for (int i = 0; i < Pairs.Count; i++)
                {
                    if (Pairs[i].PairNum == pairNum)
                    {
                        pair = Pairs[i];
                        break;
                    }
                }

                if (tabNum == 1)
                {
                    pair.Tab1.ShowConnectorDialog();
                }
                else if (tabNum == 2)
                {
                    pair.Tab2.ShowConnectorDialog();
                }
            }
            else if (column == 4 && row == 0)
            { // кнопка открытия общих настроек

                if (_commonSettingsUi != null)
                {
                    _commonSettingsUi.Activate();
                    return;
                }

                _commonSettingsUi = new BotTabPairCommonSettingsUi(this);
                _commonSettingsUi.Show();
                _commonSettingsUi.Closed += _commonSettingsUi_Closed;
            }
            else if (column == 4)
            {
                // возможно кнопка открытия отдельного окна пары или общих настроек

                int tabNum = -1;

                try
                {
                    if (_grid.Rows[row].Cells[0].Value == null)
                    {
                        return;
                    }

                    tabNum = Convert.ToInt32(_grid.Rows[row].Cells[0].Value.ToString());
                }
                catch
                {
                    return;
                }

                PairToTrade pair = null;

                for (int i = 0; i < Pairs.Count; i++)
                {
                    if (Pairs[i].PairNum == tabNum)
                    {
                        pair = Pairs[i];
                        break;
                    }
                }

                for (int i = 0; i < _uiList.Count; i++)
                {
                    if (_uiList[i].Name == pair.Name)
                    {
                        _uiList[i].Activate();
                        return;
                    }
                }

                BotTabPairUi ui = new BotTabPairUi(pair);
                ui.Show();
                _uiList.Add(ui);

                ui.Closed += Ui_Closed;

            }
        }

        private void Ui_Closed(object sender, EventArgs e)
        {
            string name = ((BotTabPairUi)sender).Name;

            for (int i = 0; i < _uiList.Count; i++)
            {
                if (_uiList[i].Name == name)
                {
                    _uiList.RemoveAt(i);
                    return;
                }
            }
        }

        List<BotTabPairUi> _uiList = new List<BotTabPairUi>();

        BotTabPairCommonSettingsUi _commonSettingsUi;

        private void _commonSettingsUi_Closed(object sender, EventArgs e)
        {
            _commonSettingsUi = null;
        }

        // логирование

        /// <summary>
        /// Send new log message
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
        /// New log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    public class PairToTrade
    {
        public PairToTrade(string name, StartProgram startProgram)
        {
            Name = name;

            Tab1 = new BotTabSimple(name + 1, startProgram);
            Tab2 = new BotTabSimple(name + 2, startProgram);
            Tab1.CandleFinishedEvent += Tab1_CandleFinishedEvent;
            Tab2.CandleFinishedEvent += Tab2_CandleFinishedEvent;
            Tab1.Connector.ConnectorStartedReconnectEvent += Connector_ConnectorStartedReconnectEvent;
            Tab2.Connector.ConnectorStartedReconnectEvent += Connector_ConnectorStartedReconnectEvent1;
            Load();
        }

        public string Name;

        public int PairNum;

        public BotTabSimple Tab1;

        public BotTabSimple Tab2;

        private void Load()
        {
            if (!File.Exists(@"Engine\" + Name + @"PairsSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + Name + @"PairsSettings.txt"))
                {
                    PairNum = Convert.ToInt32(reader.ReadLine());

                    Enum.TryParse(reader.ReadLine(), out Sec1OrderPriceType);
                    Sec1SlippagePercent = reader.ReadLine().ToDecimal();
                    Sec1Volume = Convert.ToInt32(reader.ReadLine());
                    Enum.TryParse(reader.ReadLine(), out Sec2OrderPriceType);
                    Sec2SlippagePercent = reader.ReadLine().ToDecimal();
                    Sec2Volume = Convert.ToInt32(reader.ReadLine());
                    CorrelationLookBack = Convert.ToInt32(reader.ReadLine());
                    CointegrationDeviation = reader.ReadLine().ToDecimal();
                    CointegrationLookBack = Convert.ToInt32(reader.ReadLine());
                    SecondByMarket = Convert.ToBoolean(reader.ReadLine());

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + Name + @"PairsSettings.txt", false))
                {
                    writer.WriteLine(PairNum);

                    writer.WriteLine(Sec1OrderPriceType);
                    writer.WriteLine(Sec1SlippagePercent);
                    writer.WriteLine(Sec1Volume);
                    writer.WriteLine(Sec2OrderPriceType);
                    writer.WriteLine(Sec2SlippagePercent);
                    writer.WriteLine(Sec2Volume);
                    writer.WriteLine(CorrelationLookBack);
                    writer.WriteLine(CointegrationDeviation);
                    writer.WriteLine(CointegrationLookBack);
                    writer.WriteLine(SecondByMarket);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\" + Name + @"PairsSettings.txt"))
                {
                    File.Delete(@"Engine\" + Name + @"PairsSettings.txt");
                }
            }
            catch
            {
                // ignore
            }


            Tab1.CandleFinishedEvent -= Tab1_CandleFinishedEvent;
            Tab2.CandleFinishedEvent -= Tab2_CandleFinishedEvent;

            Tab1.Connector.ConnectorStartedReconnectEvent -= Connector_ConnectorStartedReconnectEvent;
            Tab2.Connector.ConnectorStartedReconnectEvent -= Connector_ConnectorStartedReconnectEvent1;

            Tab1.Delete();
            Tab2.Delete();
        }

        public bool EventsIsOn
        {
            get
            {
                return Tab1.EventsIsOn;
            }
            set
            {
                if (Tab1.EventsIsOn == value
                    && Tab2.EventsIsOn == value)
                {
                    return;
                }

                Tab1.EventsIsOn = value;
                Tab2.EventsIsOn = value;
            }
        }

        public bool EmulatorIsOn
        {
            get
            {
                return Tab1.EmulatorIsOn;
            }
            set
            {
                if (Tab1.EmulatorIsOn == value
                    && Tab2.EmulatorIsOn == value)
                {
                    return;
                }

                Tab1.EmulatorIsOn = value;
                Tab2.EmulatorIsOn = value;
            }
        }

        // торговые методы

        public OrderPriceType Sec1OrderPriceType = OrderPriceType.Market;

        public decimal Sec1SlippagePercent = 0;

        public decimal Sec1Volume = 7;

        public OrderPriceType Sec2OrderPriceType = OrderPriceType.Market;

        public decimal Sec2SlippagePercent = 0;

        public decimal Sec2Volume = 7;

        public bool SecondByMarket;

        public void SellCointegration()
        {



        }

        public void BuyCointegration()
        {



        }

        // корреляция

        CorrelationBuilder correlationBuilder = new CorrelationBuilder();

        public int CorrelationLookBack = 50;

        public List<PairIndicatorValue> CorrelationList = new List<PairIndicatorValue>();

        public decimal CorrelationLast
        {
            get
            {
                if (CorrelationList == null
                    || CorrelationList.Count == 0)
                {
                    return 0;
                }
                return CorrelationList[CorrelationList.Count - 1].Value;
            }
        }

        public void ReloadCorrelationHard()
        {
            List<Candle> candles1 = Tab1.CandlesFinishedOnly;
            List<Candle> candles2 = Tab2.CandlesFinishedOnly;

            if (candles1 == null ||
                candles2 == null)
            {
                return;
            }

            ReloadCorrelation(candles1, candles2);
        }

        private void ReloadCorrelation(List<Candle> candles1, List<Candle> candles2)
        {

            if (candles1.Count < CorrelationLookBack
                || candles2.Count < CorrelationLookBack)
            {
                return;
            }

            CorrelationList = correlationBuilder.ReloadCorrelation(candles1, candles2, CorrelationLookBack);

            if (CorrelationChangeEvent != null)
            {
                CorrelationChangeEvent(CorrelationList, this);
            }
        }

        public event Action<List<PairIndicatorValue>, PairToTrade> CorrelationChangeEvent;

        // коинтеграция

        CointegrationBuilder _cointegrationBuilder = new CointegrationBuilder();

        public int CointegrationLookBack = 50;

        public decimal CointegrationDeviation = 1;

        public List<PairIndicatorValue> Cointegration = new List<PairIndicatorValue>();

        public decimal CointegrationLast
        {
            get
            {
                if (Cointegration == null
                    || Cointegration.Count == 0)
                {
                    return 0;
                }
                return Cointegration[Cointegration.Count - 1].Value;
            }
        }

        public decimal CointegrationMult;

        public decimal CointegrationStandartDeviation;

        public CointegrationLineSide SideCointegrationValue;

        public decimal LineUpCointegration;

        public decimal LineDownCointegration;

        public void ReloadCointegrationHard()
        {
            List<Candle> candles1 = Tab1.CandlesFinishedOnly;
            List<Candle> candles2 = Tab2.CandlesFinishedOnly;

            if (candles1 == null ||
                candles2 == null)
            {
                return;
            }

            ReloadCointegration(candles1, candles2);
        }

        private void ReloadCointegration(List<Candle> candles1, List<Candle> candles2)
        {
            Cointegration.Clear();
            LineUpCointegration = 0;
            LineDownCointegration = 0;
            CointegrationStandartDeviation = 0;

            _cointegrationBuilder.CointegrationLookBack = CointegrationLookBack;
            _cointegrationBuilder.CointegrationDeviation = CointegrationDeviation;

            if (Tab1.StartProgram == StartProgram.IsOsTrader)
            {
                _cointegrationBuilder.ReloadCointegration(candles1, candles2, true);
            }
            else
            {
                // обрезание значения до красивых отнимает много ЦП. В реале можно. В тестере нельзя
                _cointegrationBuilder.ReloadCointegration(candles1, candles2, false);
            }


            CointegrationMult = _cointegrationBuilder.CointegrationMult;
            Cointegration = _cointegrationBuilder.Cointegration;
            CointegrationStandartDeviation = _cointegrationBuilder.CointegrationStandartDeviation;

            LineUpCointegration = CointegrationStandartDeviation * CointegrationDeviation;
            LineDownCointegration = -(CointegrationStandartDeviation * CointegrationDeviation);

            CointegrationLineSide lastSide = SideCointegrationValue;

            if (CointegrationLast > LineUpCointegration)
            {
                SideCointegrationValue = CointegrationLineSide.Up;
            }
            else if (CointegrationLast < LineDownCointegration)
            {
                SideCointegrationValue = CointegrationLineSide.Down;
            }
            else
            {
                SideCointegrationValue = CointegrationLineSide.No;
            }

            if (CointegrationChangeEvent != null)
            {
                CointegrationChangeEvent(Cointegration, this);
            }

            if (lastSide != SideCointegrationValue)
            {
                if (CointegrationPositionSideChangeEvent != null)
                {
                    CointegrationPositionSideChangeEvent(SideCointegrationValue, this);
                }
            }
        }


        public event Action<List<PairIndicatorValue>, PairToTrade> CointegrationChangeEvent;

        public event Action<CointegrationLineSide, PairToTrade> CointegrationPositionSideChangeEvent;

        // входящие события

        List<Candle> _candles1;

        private void Tab1_CandleFinishedEvent(List<Candle> candles1)
        {
            _candles1 = candles1;
            TryReloadIndicators();
        }

        List<Candle> _candles2;

        private void Tab2_CandleFinishedEvent(List<Candle> candles2)
        {
            _candles2 = candles2;
            TryReloadIndicators();
        }

        private void Connector_ConnectorStartedReconnectEvent1(string arg1, TimeFrame arg2, TimeSpan arg3, string arg4, Market.ServerType arg5)
        {
            ClearIndicators();
        }

        private void Connector_ConnectorStartedReconnectEvent(string arg1, TimeFrame arg2, TimeSpan arg3, string arg4, Market.ServerType arg5)
        {
            ClearIndicators();
        }

        private DateTime _timeLastReloadIndicators;

        private void TryReloadIndicators()
        {
            if (_candles1 == null ||
                _candles2 == null)
            {
                return;
            }

            if (_candles1.Count == 0 ||
                _candles2.Count == 0)
            {
                return;
            }

            if (_candles1[_candles1.Count - 1].TimeStart !=
                _candles2[_candles2.Count - 1].TimeStart)
            {
                return;
            }

            if (_candles1[_candles1.Count - 1].TimeStart == _timeLastReloadIndicators)
            {
                return;
            }

            _timeLastReloadIndicators = _candles1[_candles1.Count - 1].TimeStart;

            if (Tab1.TimeFrame != Tab2.TimeFrame)
            {
                SendNewLogMessage(OsLocalization.Trader.Label250 + Name, LogMessageType.Error);
                return;
            }

            ReloadCorrelation(_candles1, _candles2);
            ReloadCointegration(_candles1, _candles2);
        }

        private void ClearIndicators()
        {
            CorrelationList.Clear();

            Cointegration.Clear();

            CointegrationMult = 0;

            CointegrationStandartDeviation = 0;

            SideCointegrationValue = 0;

            LineUpCointegration = 0;

            LineDownCointegration = 0; ;
        }

        // логирование

        /// <summary>
        /// Send new log message
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
        /// New log message event
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    public enum MainGridPairSortType
    {
        No,
        Side,
        Correlation
    }
}