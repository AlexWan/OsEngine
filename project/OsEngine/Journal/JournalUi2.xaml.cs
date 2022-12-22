/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Journal.Internal;
using OsEngine.Logging;
using Color = System.Drawing.Color;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Language;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;
using ChartArea = System.Windows.Forms.DataVisualization.Charting.ChartArea;
using ContextMenu = System.Windows.Forms.ContextMenu;
using MenuItem = System.Windows.Forms.MenuItem;
using Series = System.Windows.Forms.DataVisualization.Charting.Series;
using System.Threading;

namespace OsEngine.Journal
{

    // GridTabPrime
    // GridActivBots

    /// <summary>
    /// Interaction logic for JournalNewUi.xaml
    /// Логика взаимодействия для JournalNewUi.xaml
    /// </summary>
    public partial class JournalUi2
    {
        /// <summary>
        /// if window recycled
        /// является ли окно утилизированным
        /// </summary>
        public bool IsErase;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public JournalUi2(List<BotPanelJournal> botsJournals, StartProgram startProgram)
        {
            InitializeComponent(); 
            _startProgram = startProgram;
            _botsJournals = botsJournals;
            LoadGroups();

            ComboBoxChartType.Items.Add("Absolute");
            ComboBoxChartType.Items.Add("Persent");
            ComboBoxChartType.SelectedItem = "Absolute";
            ComboBoxChartType.SelectionChanged += ComboBoxChartType_SelectionChanged;

           _currentCulture = CultureInfo.CurrentCulture;

            TabControlPrime.SelectionChanged += TabControlPrime_SelectionChanged;

            Task task = new Task(ThreadWorkerPlace);
            task.Start();

            Title = OsLocalization.Journal.TitleJournalUi;
            Label1.Content = OsLocalization.Journal.Label1;
            Label2.Content = OsLocalization.Journal.Label2;
            Label3.Content = OsLocalization.Journal.Label3;

            TabItem1.Header = OsLocalization.Journal.TabItem1;
            TabItem2.Header = OsLocalization.Journal.TabItem2;
            TabItem3.Header = OsLocalization.Journal.TabItem3;
            TabItem4.Header = OsLocalization.Journal.TabItem4;
            TabItem5.Header = OsLocalization.Journal.TabItem5;
            TabItem6.Header = OsLocalization.Journal.TabItem6;
            LabelFrom.Content = OsLocalization.Journal.Label5;
            LabelTo.Content = OsLocalization.Journal.Label6;
            ButtonReload.Content = OsLocalization.Journal.Label7;
            LabelEqutyCharteType.Content = OsLocalization.Journal.Label8;

            CreatePositionsLists();

            Closing += JournalUi_Closing;

            ButtonShowLeftPanel.Visibility = Visibility.Hidden;

            CreateBotsGrid();
            PaintBotsGrid();

            Thread task2 = new Thread(LeftBotsPanelPainter);
            task2.Start();

            CreateSlidersShowPositions();

            this.Activate();
            this.Focus();
        }

        private CultureInfo _currentCulture;

        private void JournalUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsErase = true;

            TabControlPrime.SelectionChanged -= TabControlPrime_SelectionChanged;
            TabControlPrime.Items.Clear();

            Closing -= JournalUi_Closing;
            _botsJournals.Clear();
            _botsJournals = null;

            if (_allPositions != null)
            {
                _allPositions.Clear();
                _allPositions = null;
            }

            if (_longPositions != null)
            {
                _longPositions.Clear();
                _longPositions = null;
            }

            if (_shortPositions != null)
            {
                _shortPositions.Clear();
                _shortPositions = null;
            }

            if (_chartEquity != null)
            {
                _chartEquity.Series.Clear();
                _chartEquity.ChartAreas.Clear();
                _chartEquity = null;
                HostEquity.Child.Hide();
                HostEquity.Child = null;
                HostEquity = null;
            }

            if (_chartVolume != null)
            {
                _chartVolume.Series.Clear();
                _chartVolume.ChartAreas.Clear();
                _chartVolume.Click -= _chartVolume_Click;
                _chartVolume = null;
                HostVolume.Child.Hide();
                HostVolume.Child = null;
                HostVolume = null;
            }

            if (_chartDd != null)
            {
                _chartDd.Series.Clear();
                _chartDd.ChartAreas.Clear();
                _chartDd = null;
                HostDrawdown.Child.Hide();
                HostDrawdown.Child = null;
                HostDrawdown = null;
            }

            if (_gridStatistics != null)
            {
                DataGridFactory.ClearLinks(_gridStatistics);
                _gridStatistics.Rows.Clear();
                _gridStatistics = null;
                HostStatistics.Child.Hide();
                HostStatistics.Child = null;
                HostStatistics = null;
            }

            if (_openPositionGrid != null)
            {
                DataGridFactory.ClearLinks(_openPositionGrid);
                _openPositionGrid.Rows.Clear();
                _openPositionGrid.Click -= _openPositionGrid_Click;
                _openPositionGrid.DoubleClick -= _openPositionGrid_DoubleClick;
                _openPositionGrid = null;
                HostOpenPosition.Child.Hide();
                HostOpenPosition.Child = null;
                HostOpenPosition = null;
            }

            if (_closePositionGrid != null)
            {
                DataGridFactory.ClearLinks(_closePositionGrid);
                _closePositionGrid.Rows.Clear();
                _closePositionGrid.Click -= _closePositionGrid_Click;
                _closePositionGrid.DoubleClick -= _closePositionGrid_DoubleClick;
                _closePositionGrid = null;
                HostClosePosition.Child.Hide();
                HostClosePosition.Child = null;
                HostClosePosition = null;
            }

            if(_gridLeftBotsPanel != null)
            {
                HostBotsSelected.Child = null;
                _gridLeftBotsPanel.CellEndEdit -= _gridLeftBotsPanel_CellEndEdit;
                _gridLeftBotsPanel.CellBeginEdit -= _gridLeftBotsPanel_CellBeginEdit;
                DataGridFactory.ClearLinks(_gridLeftBotsPanel);
                _gridLeftBotsPanel = null;
            }
        }

        private object _paintLocker = new object();

        private StartProgram _startProgram;

        private void ComboBoxChartType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RePaint();
        }

        /// <summary>
        /// main method of repainting report tables
        /// главный метод перерисовки таблиц отчётов
        /// </summary>
        public void RePaint()
        {
            if (!TabControlPrime.CheckAccess())
            {
                TabControlPrime.Dispatcher.Invoke(RePaint);
                return;
            }

            if (IsErase == true)
            {
                return;
            }

            if(_allPositions == null)
            {
                return;
            }

            try
            {
                List<Position> allSortPoses = new List<Position>();
                List<Position> longPositions = new List<Position>();
                List<Position> shortPositions = new List<Position>();

                for (int i = 0; i < _allPositions.Count; i++)
                {
                    if(_allPositions[i] == null)
                    {
                        continue;
                    }
                    if (_allPositions[i].TimeCreate < startTime
                        || _allPositions[i].TimeCreate > endTime)
                    {
                        continue;
                    }
                    allSortPoses.Add(_allPositions[i]);
                }

                for (int i = 0; i < _longPositions.Count; i++)
                {
                    if (_longPositions[i] == null)
                    {
                        continue;
                    }
                    if (_longPositions[i].TimeCreate < startTime
                        || _longPositions[i].TimeCreate > endTime)
                    {
                        continue;
                    }
                    longPositions.Add(_longPositions[i]);
                }

                for (int i = 0; i < _shortPositions.Count; i++)
                {
                    if (_shortPositions[i] == null)
                    {
                        continue;
                    }
                    if (_shortPositions[i].TimeCreate < startTime
                        || _shortPositions[i].TimeCreate > endTime)
                    {
                        continue;
                    }
                    shortPositions.Add(_shortPositions[i]);
                }


                lock (_paintLocker)
                {

                    if (TabControlPrime.SelectedIndex == 0)
                    {
                        PaintProfitOnChart(allSortPoses);
                    }
                    else if (TabControlPrime.SelectedIndex == 1)
                    {
                        bool neadShowTickState = !(_botsJournals.Count > 1);

                        PaintStatTable(allSortPoses, _longPositions, _shortPositions, neadShowTickState);
                    }
                    else if (TabControlPrime.SelectedIndex == 2)
                    {
                        PaintDrowDown(allSortPoses);
                    }
                    else if (TabControlPrime.SelectedIndex == 3)
                    {
                        PaintVolumeOnChart(allSortPoses);
                    }
                    else if (TabControlPrime.SelectedIndex == 4)
                    {
                        PaintOpenPositionGrid(allSortPoses);
                    }
                    else if (TabControlPrime.SelectedIndex == 5)
                    {
                        PaintClosePositionGrid(allSortPoses);
                    }
                }
            }
            catch(Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        /// <summary>
        /// delete position by number
        /// удалить позицию по номеру
        /// </summary>
        private void DeletePosition(int number)
        {
            for (int i = 0; i < _botsJournals.Count; i++)
            {
                for (int i2 = 0; i2 < _botsJournals[i]._Tabs.Count; i2++)
                {
                    if (_botsJournals[i]._Tabs[i2].Journal.AllPosition == null)
                    {
                        continue;
                    }
                    Position pos = _botsJournals[i]._Tabs[i2].Journal.AllPosition.Find(p => p.Number == number);

                    if (pos != null)
                    {
                        _botsJournals[i]._Tabs[i2].Journal.DeletePosition(pos);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// show a window of transaction details
        /// показать окно с подробностями по сделке
        /// </summary>
        private void ShowPositionDialog(int number)
        {
            for (int i = 0; i < _botsJournals.Count; i++)
            {
                for (int i2 = 0; i2 < _botsJournals[i]._Tabs.Count; i2++)
                {
                    if (_botsJournals[i]._Tabs[i2].Journal.AllPosition == null)
                    {
                        continue;
                    }
                    Position pos = _botsJournals[i]._Tabs[i2].Journal.AllPosition.Find(p => p.Number == number);

                    if (pos != null)
                    {
                        PositionUi ui = new PositionUi(pos, _startProgram);
                        ui.ShowDialog();

                        if (ui.PositionChanged)
                        {
                            _botsJournals[i]._Tabs[i2].Journal.Save();
                            _botsJournals[i]._Tabs[i2].Journal.NeadToUpdateStatePositions();
                            RePaint();
                        }
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// the location of stream updating statistics
        /// место работы потока обновляющего статистку
        /// </summary>
        private async void ThreadWorkerPlace()
        {
            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }
            while (true)
            {
                await Task.Delay(30000);

                if (IsErase == true)
                {
                    return;
                }

                RePaint();
            }
        }
        // tab management
        // менеджмент вкладок

        /// <summary>
        /// statistics tab switched
        /// переключилась вкладка статистки
        /// </summary>
        void TabControlPrime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RePaint();
        }

        /// <summary>
        /// event when you change the Item TabControl selection from Tabs robots
        /// событие при изменении выборе Item TabControl с Tabs роботов
        /// </summary>
        private void TabControlLeftSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // robot tab selection
            // выбор вкладки робота
            RePaint();
        }
        // filling in the statistics table
        // заполнение таблицы статистики

        /// <summary>
        /// table for drawing statistics
        /// таблица для прорисовки Статистики
        /// </summary>
        private DataGridView _gridStatistics;

        /// <summary>
        /// creating an empty Statistics form for the ItemStatistics tab
        /// создание пустой формы Статистики для вкладки ItemStatistics
        /// </summary>
        public void CreateTableToStatistic()
        {
            try
            {
                _gridStatistics = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

                _gridStatistics.AllowUserToResizeRows = false;

                CustomDataGridViewCell cell0 = new CustomDataGridViewCell();
                cell0.Style = _gridStatistics.DefaultCellStyle;
                cell0.AdvancedBorderStyle = new DataGridViewAdvancedBorderStyle
                {
                    Bottom = DataGridViewAdvancedCellBorderStyle.None,
                    Top = DataGridViewAdvancedCellBorderStyle.None,
                    Left = DataGridViewAdvancedCellBorderStyle.Inset,
                    Right = DataGridViewAdvancedCellBorderStyle.Inset
                };

                HostStatistics.Child = _gridStatistics;
                HostStatistics.Child.Show();

                DataGridViewColumn column0 = new DataGridViewColumn();
                column0.CellTemplate = cell0;
                column0.HeaderText = @"";
                column0.ReadOnly = true;
                column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _gridStatistics.Columns.Add(column0);

                DataGridViewColumn column1 = new DataGridViewColumn();
                column1.CellTemplate = cell0;
                column1.HeaderText = OsLocalization.Journal.GridColumn1;
                column1.ReadOnly = true;
                column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridStatistics.Columns.Add(column1);


                DataGridViewColumn column2 = new DataGridViewColumn();
                column2.CellTemplate = cell0;
                column2.HeaderText = OsLocalization.Journal.GridColumn2;
                column2.ReadOnly = true;
                column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridStatistics.Columns.Add(column2);

                DataGridViewColumn column3 = new DataGridViewColumn();
                column3.CellTemplate = cell0;
                column3.HeaderText = OsLocalization.Journal.GridColumn3;
                column3.ReadOnly = true;
                column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridStatistics.Columns.Add(column3);

                for (int i = 0; i < 30; i++)
                {
                    _gridStatistics.Rows.Add(); //string addition/ добавление строки
                }

                _gridStatistics.Rows[0].Cells[0].Value = OsLocalization.Journal.GridRow1;
                _gridStatistics.Rows[1].Cells[0].Value = OsLocalization.Journal.GridRow2;
                _gridStatistics.Rows[2].Cells[0].Value = OsLocalization.Journal.GridRow3;
                _gridStatistics.Rows[3].Cells[0].Value = OsLocalization.Journal.GridRow17;

                _gridStatistics.Rows[4].Cells[0].Value = OsLocalization.Journal.GridRow4;
                _gridStatistics.Rows[5].Cells[0].Value = OsLocalization.Journal.GridRow5;

                _gridStatistics.Rows[7].Cells[0].Value = OsLocalization.Journal.GridRow6;
                _gridStatistics.Rows[8].Cells[0].Value = OsLocalization.Journal.GridRow7;
                _gridStatistics.Rows[9].Cells[0].Value = OsLocalization.Journal.GridRow8;
                _gridStatistics.Rows[10].Cells[0].Value = OsLocalization.Journal.GridRow9;


                _gridStatistics.Rows[12].Cells[0].Value = OsLocalization.Journal.GridRow10;
                _gridStatistics.Rows[13].Cells[0].Value = OsLocalization.Journal.GridRow11;
                _gridStatistics.Rows[14].Cells[0].Value = OsLocalization.Journal.GridRow6;
                _gridStatistics.Rows[15].Cells[0].Value = OsLocalization.Journal.GridRow7;
                _gridStatistics.Rows[16].Cells[0].Value = OsLocalization.Journal.GridRow8;
                _gridStatistics.Rows[17].Cells[0].Value = OsLocalization.Journal.GridRow9;
                _gridStatistics.Rows[18].Cells[0].Value = OsLocalization.Journal.GridRow12;

                _gridStatistics.Rows[20].Cells[0].Value = OsLocalization.Journal.GridRow13;
                _gridStatistics.Rows[21].Cells[0].Value = OsLocalization.Journal.GridRow14;
                _gridStatistics.Rows[22].Cells[0].Value = OsLocalization.Journal.GridRow6;
                _gridStatistics.Rows[23].Cells[0].Value = OsLocalization.Journal.GridRow7;
                _gridStatistics.Rows[24].Cells[0].Value = OsLocalization.Journal.GridRow8;
                _gridStatistics.Rows[25].Cells[0].Value = OsLocalization.Journal.GridRow9;
                _gridStatistics.Rows[26].Cells[0].Value = OsLocalization.Journal.GridRow12;
                _gridStatistics.Rows[27].Cells[0].Value = "";
                _gridStatistics.Rows[28].Cells[0].Value = OsLocalization.Journal.GridRow15;
                _gridStatistics.Rows[29].Cells[0].Value = OsLocalization.Journal.GridRow16;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// draw a table of statistics
        /// прорисовать таблицу статистики
        /// </summary>
        private void PaintStatTable(List<Position> positionsAll, List<Position> positionsLong, List<Position> positionsShort, bool neadShowTickState)
        {
            if (_gridStatistics == null)
            {
                CreateTableToStatistic();
            }

            List<string> positionsAllState = PositionStaticticGenerator.GetStatisticNew(positionsAll);
            List<string> positionsLongState = PositionStaticticGenerator.GetStatisticNew(positionsLong);
            List<string> positionsShortState = PositionStaticticGenerator.GetStatisticNew(positionsShort);

            if (positionsAllState == null)
            {
                for (int i = 0; i < 30; i++)
                {
                    _gridStatistics.Rows[i].Cells[1].Value = "";
                }
            }
            if (positionsLongState == null)
            {
                for (int i = 0; i < 30; i++)
                {
                    _gridStatistics.Rows[i].Cells[2].Value = "";
                }
            }
            if (positionsShortState == null)
            {
                for (int i = 0; i < 30; i++)
                {
                    _gridStatistics.Rows[i].Cells[3].Value = "";
                }
            }
            if (positionsLongState != null)
            {
                for (int i = 0; i < 30; i++)
                {
                    _gridStatistics.Rows[i].Cells[2].Value = positionsLongState[i].ToString();
                }
            }
            if (positionsShortState != null)
            {
                for (int i = 0; i < 30; i++)
                {
                    _gridStatistics.Rows[i].Cells[3].Value = positionsShortState[i].ToString();
                }
            }
            if (positionsAllState != null)
            {
                for (int i = 0; i < 30; i++)
                {
                    _gridStatistics.Rows[i].Cells[1].Value = positionsAllState[i].ToString();
                }
            }
        }
        // profit drawing
        // прорисовка профита

        /// <summary>
        /// chart
        /// чарт
        /// </summary>
        Chart _chartEquity;

        /// <summary>
        /// to create a profit chart
        /// создать чарт для профита
        /// </summary>
        private void CreateChartProfit()
        {
            try
            {
                _chartEquity = new Chart();
                HostEquity.Child = _chartEquity;
                HostEquity.Child.Show();

                _chartEquity.Series.Clear();
                _chartEquity.ChartAreas.Clear();
                _chartEquity.BackColor = Color.FromArgb(17, 18, 23);

                ChartArea areaLineProfit = new ChartArea("ChartAreaProfit");
                areaLineProfit.Position.Height = 70;
                areaLineProfit.Position.Width = 100;
                areaLineProfit.Position.Y = 0;
                areaLineProfit.CursorX.IsUserSelectionEnabled = false; //allow the user to change the view scope/ разрешаем пользователю изменять рамки представления
                areaLineProfit.CursorX.IsUserEnabled = true; //trait/чертa

                _chartEquity.ChartAreas.Add(areaLineProfit);

                ChartArea areaLineProfitBar = new ChartArea("ChartAreaProfitBar");
                areaLineProfitBar.AlignWithChartArea = "ChartAreaProfit";
                areaLineProfitBar.Position.Height = 30;
                areaLineProfitBar.Position.Width = 100;
                areaLineProfitBar.Position.Y = 70;
                areaLineProfitBar.AxisX.Enabled = AxisEnabled.False;
                areaLineProfitBar.CursorX.IsUserEnabled = true; //trait/чертa

                _chartEquity.ChartAreas.Add(areaLineProfitBar);

                for (int i = 0; i < _chartEquity.ChartAreas.Count; i++)
                {
                    _chartEquity.ChartAreas[i].BorderColor = Color.Black;
                    _chartEquity.ChartAreas[i].BackColor = Color.FromArgb(17, 18, 23);
                    _chartEquity.ChartAreas[i].CursorY.LineColor = Color.Gainsboro;
                    _chartEquity.ChartAreas[i].CursorX.LineColor = Color.Black;
                    _chartEquity.ChartAreas[i].AxisX.TitleForeColor = Color.Gainsboro;
                    _chartEquity.ChartAreas[i].AxisY.TitleForeColor = Color.Gainsboro;

                    foreach (var axe in _chartEquity.ChartAreas[i].Axes)
                    {
                        axe.LabelStyle.ForeColor = Color.Gainsboro;
                    }
                }

                _chartEquity.MouseMove += _chartEquity_MouseMove;

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _chartEquity_MouseMove(object sender, MouseEventArgs e)
        {
            if (_chartEquity.Series == null
                || _chartEquity.Series.Count == 0)
            {
                return;
            }
            if (_chartEquity.ChartAreas[0].AxisX.ScaleView.Size == double.NaN)
            {
                return;
            }

            int curCountOfPoints = _chartEquity.Series[0].Points.Count;

            double sizeArea = _chartEquity.ChartAreas[0].InnerPlotPosition.Size.Width;
            double allSizeAbs = _chartEquity.Size.Width * (sizeArea/100);
           
            double onePointLen = allSizeAbs / curCountOfPoints;

            double curMousePosAbs = e.X;

            double curPointNum = curMousePosAbs / onePointLen-1;
            try
            {
                curPointNum = Convert.ToDouble(Convert.ToInt32(curPointNum));
            }
            catch
            {
                return;
            }
            
            //_chartEquity.ChartAreas[0].CursorX.Position = curPointNum;
            if(_chartEquity.ChartAreas[0].CursorX.Position != curPointNum)
            {
                _chartEquity.ChartAreas[0].CursorX.SetCursorPosition(curPointNum);
            }

            int numPointInt = Convert.ToInt32(curPointNum);

            if(numPointInt <= 0)
            {
                return;
            }

            if(_chartEquity.Series[0].Points.Count > _lastSeriesEquityChartPointWithLabel)
            {
                 _chartEquity.Series[0].Points[_lastSeriesEquityChartPointWithLabel].Label = "";
            }
            if (_chartEquity.Series[0].Points.Count > numPointInt)
            {
                _chartEquity.Series[0].Points[numPointInt].Label
                = _chartEquity.Series[0].Points[numPointInt].AxisLabel;
            }

            if (_chartEquity.Series[1].Points.Count > _lastSeriesEquityChartPointWithLabel)
            {
                _chartEquity.Series[1].Points[_lastSeriesEquityChartPointWithLabel].Label = "";
            }
            if (_chartEquity.Series[1].Points.Count > numPointInt)
            {
                _chartEquity.Series[1].Points[numPointInt].Label
                = _chartEquity.Series[1].Points[numPointInt].AxisLabel;
            }

            if (_chartEquity.Series[2].Points.Count > _lastSeriesEquityChartPointWithLabel)
            {
                _chartEquity.Series[2].Points[_lastSeriesEquityChartPointWithLabel].Label = "";
            }
            if (_chartEquity.Series[2].Points.Count > numPointInt)
            {
                _chartEquity.Series[2].Points[numPointInt].Label
                = _chartEquity.Series[2].Points[numPointInt].AxisLabel;
            }

            if (_chartEquity.Series[3].Points.Count > _lastSeriesEquityChartPointWithLabel)
            {
                _chartEquity.Series[3].Points[_lastSeriesEquityChartPointWithLabel].Label = "";
            }
            if (_chartEquity.Series[3].Points.Count > numPointInt)
            {
                _chartEquity.Series[3].Points[numPointInt].Label
                = _chartEquity.Series[3].Points[numPointInt].AxisLabel;
            }

            if (_chartEquity.Series[4].Points.Count > _lastSeriesEquityChartPointWithLabel)
            {
                _chartEquity.Series[4].Points[_lastSeriesEquityChartPointWithLabel].Label = "";
            }
            if (_chartEquity.Series[4].Points.Count > numPointInt)
            {
                _chartEquity.Series[4].Points[numPointInt].Label
                = _chartEquity.Series[4].Points[numPointInt].AxisLabel;
            }


            _lastSeriesEquityChartPointWithLabel = numPointInt;
        }

        int _lastSeriesEquityChartPointWithLabel = 0;

        /// <summary>
        /// chart out the volumes
        /// прорисовать чарт объёмов
        /// </summary>
        private void PaintProfitOnChart(List<Position> positionsAll)
        {
            if (!GridTabPrime.Dispatcher.CheckAccess())
            {
                GridTabPrime.Dispatcher.Invoke(
                    new Action<List<Position>>(PaintProfitOnChart), positionsAll);
                return;
            }

            if (_chartEquity == null)
            {
                CreateChartProfit();
            }

            _chartEquity.Series.Clear();

            if (positionsAll == null)
            {
                return;
            }

            Series profit = new Series("SeriesProfit");
            profit.ChartType = SeriesChartType.Line;
            profit.Color = Color.White;   //DeepSkyBlue;
            profit.LabelForeColor = Color.White;
            profit.YAxisType = AxisType.Secondary;
            profit.ChartArea = "ChartAreaProfit";
            profit.BorderWidth = 4;
            profit.ShadowOffset = 2;

            Series profitLong = new Series("SeriesProfitLong");
            profitLong.ChartType = SeriesChartType.Line;
            profitLong.Color = Color.DeepSkyBlue;   //DeepSkyBlue;
            profitLong.LabelForeColor = Color.DeepSkyBlue;
            profitLong.YAxisType = AxisType.Secondary;
            profitLong.ChartArea = "ChartAreaProfit";
            profitLong.BorderWidth = 2;
            profitLong.ShadowOffset = 2;

            Series profitShort = new Series("SeriesProfitShort");
            profitShort.ChartType = SeriesChartType.Line;
            profitShort.Color = Color.DarkOrange;  //DeepSkyBlue;
            profitShort.LabelForeColor = Color.DarkOrange;
            profitShort.YAxisType = AxisType.Secondary;
            profitShort.ChartArea = "ChartAreaProfit";
            profitShort.ShadowOffset = 2;
            profitShort.BorderWidth = 2;

            Series profitBar = new Series("SeriesProfitBar");
            profitBar.ChartType = SeriesChartType.Column;
            profitBar.YAxisType = AxisType.Secondary;
            profitBar.LabelForeColor = Color.White;
            profitBar.ChartArea = "ChartAreaProfitBar";
            profitBar.ShadowOffset = 2;

            Series nullLine = new Series("SeriesNullLine");
            nullLine.ChartType = SeriesChartType.Line;
            nullLine.YAxisType = AxisType.Secondary;
            nullLine.LabelForeColor = Color.White;
            nullLine.ChartArea = "ChartAreaProfit";
            nullLine.ShadowOffset = 0;



            try
            {
                /*

                */

                decimal profitSum = 0;
                decimal profitSumLong = 0;
                decimal profitSumShort = 0;
                decimal maxYVal = 0;
                decimal minYval = decimal.MaxValue;

                decimal maxYValBars = 0;
                decimal minYvalBars = decimal.MaxValue;

                decimal curProfit = 0;
                string chartType = ComboBoxChartType.SelectedItem.ToString();



                for (int i = 0; i < positionsAll.Count; i++)
                {
                    decimal curMult = positionsAll[i].MultToJournal;

                    if(chartType == "Absolute")
                    {
                        curProfit = positionsAll[i].ProfitPortfolioPunkt * (curMult / 100);
                    }
                    else if (chartType == "Persent")
                    {
                        curProfit = positionsAll[i].ProfitOperationPersent * (curMult / 100);
                    }


                    curProfit = Math.Round(curProfit, 8);

                    if(curProfit > maxYValBars)
                    {
                        maxYValBars = curProfit;
                    }

                    if(curProfit < minYvalBars)
                    {
                        minYvalBars = curProfit;
                    }

                    profitSum += curProfit;
                    profit.Points.AddXY(i, profitSum);

                    nullLine.Points.AddXY(i, 0);
                    nullLine.Points[nullLine.Points.Count - 1].AxisLabel =
                        positionsAll[i].TimeCreate.ToString(_currentCulture);

                    profit.Points[profit.Points.Count - 1].AxisLabel = profitSum.ToString();

                    profitBar.Points.AddXY(i, curProfit);
                    profitBar.Points[profitBar.Points.Count - 1].AxisLabel = curProfit.ToString();

                    if (positionsAll[i].Direction == Side.Buy)
                    {
                        profitSumLong += curProfit;
                    }

                    if (positionsAll[i].Direction == Side.Sell)
                    {
                        profitSumShort += curProfit;
                    }

                    if (profitSum > maxYVal)
                    {
                        maxYVal = profitSum;
                    }
                    if (profitSumLong > maxYVal)
                    {
                        maxYVal = profitSumLong;
                    }
                    if (profitSumShort > maxYVal)
                    {
                        maxYVal = profitSumShort;
                    }

                    if (profitSum < minYval)
                    {
                        minYval = profitSum;
                    }
                    if (profitSumLong < minYval)
                    {
                        minYval = profitSumLong;
                    }
                    if (profitSumShort < minYval)
                    {
                        minYval = profitSumShort;
                    }

                    profitLong.Points.AddXY(i, profitSumLong);
                    profitLong.Points[profitLong.Points.Count - 1].AxisLabel = profitSumLong.ToString();

                    profitShort.Points.AddXY(i, profitSumShort);
                    profitShort.Points[profitShort.Points.Count - 1].AxisLabel = profitSumShort.ToString();

                    if (curProfit > 0)
                    {
                        profitBar.Points[profitBar.Points.Count - 1].Color = Color.Gainsboro;
                    }
                    else
                    {
                        profitBar.Points[profitBar.Points.Count - 1].Color = Color.DarkRed;
                    }

                }

                _chartEquity.Series.Add(profit);
                _chartEquity.Series.Add(profitLong);
                _chartEquity.Series.Add(profitShort);
                _chartEquity.Series.Add(profitBar);
                _chartEquity.Series.Add(nullLine);

                if (minYval != decimal.MaxValue &&
                    maxYVal != 0)
                {
                    decimal chartHeigh = maxYVal - minYval;

                    maxYVal = maxYVal + chartHeigh * 0.05m;
                    minYval = minYval - chartHeigh * 0.05m;
                    _chartEquity.ChartAreas[0].AxisY2.Maximum = (double)maxYVal;
                    _chartEquity.ChartAreas[0].AxisY2.Minimum = (double)minYval;
                }

                if(minYvalBars != decimal.MaxValue &&
                    maxYValBars != 0)
                {
                    decimal chartHeigh = maxYValBars - minYvalBars;
                    maxYValBars = maxYValBars + chartHeigh * 0.05m;
                    minYvalBars = minYvalBars - chartHeigh * 0.05m;
                    _chartEquity.ChartAreas[1].AxisY2.Maximum = (double)maxYValBars;
                    _chartEquity.ChartAreas[1].AxisY2.Minimum = (double)minYvalBars;
                }

                PaintXLabelsOnEquityChart(positionsAll);

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintXLabelsOnEquityChart(List<Position> poses)
        {
            int values = poses.Count;

            int labelCount = 5;

            if(values < 5)
            {
                labelCount = values;
            }

            if(labelCount == 0)
            {
                return;
            }

            ChartArea area = _chartEquity.ChartAreas[0];

            area.AxisX.Interval = values / labelCount;

            while (area.AxisX.CustomLabels.Count < labelCount)
            {
                area.AxisX.CustomLabels.Add(new CustomLabel());
            }
            while (area.AxisX.CustomLabels.Count > labelCount)
            {
                area.AxisX.CustomLabels.RemoveAt(0);
            }

            double value = 0 + area.AxisX.Interval;

            if (labelCount < 4)
            {
                value = 0;
            }


            for (int i = 0; i < labelCount; i++)
            {
                area.AxisX.CustomLabels[i].FromPosition = value - area.AxisX.Interval * 0.7;
                area.AxisX.CustomLabels[i].ToPosition = value + area.AxisX.Interval * 0.7;
                area.AxisX.CustomLabels[i].Text = poses[(int)value].TimeCreate.ToString();

                value += area.AxisX.Interval;

                if (value >= poses.Count)
                {
                    value = poses.Count - 1;
                }
            }
        }

        // volume drawing
        // прорисовка объёма

        /// <summary>
        /// volume chart
        /// чарт для объёмов
        /// </summary>
        Chart _chartVolume;

        /// <summary>
        /// to create a chart for drawing volumes
        /// создать чарт для прорисовки объёмов
        /// </summary>
        private void CreateChartVolume()
        {
            try
            {
                _chartVolume = new Chart();
                HostVolume.Child = _chartVolume;
                HostVolume.Child.Show();

                _chartVolume.BackColor = Color.FromArgb(17, 18, 23);
                _chartVolume.Click += _chartVolume_Click;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// draw the chart
        /// прорисовать чарт
        /// </summary>
        private void PaintVolumeOnChart(List<Position> positionsAll)
        {
            if (!GridTabPrime.Dispatcher.CheckAccess())
            {
                GridTabPrime.Dispatcher.Invoke(
                    new Action<List<Position>>(PaintVolumeOnChart), positionsAll);
                return;
            }

            if (_chartVolume == null)
            {
                CreateChartVolume();
            }

            _chartVolume.Series.Clear();
            _chartVolume.ChartAreas.Clear();

            if(positionsAll == null 
                || positionsAll.Count == 0)
            {
                return;
            }

            //  take the number of tools
            // берём кол-во инструментов
            List<VolumeSecurity> volumes = new List<VolumeSecurity>();

            for (int i = 0; i < positionsAll.Count; i++)
            {
                if (volumes.Find(vol => vol.Security == positionsAll[i].SecurityName) == null)
                {
                    volumes.Add(new VolumeSecurity() { Security = positionsAll[i].SecurityName });
                }
            }

            if (volumes.Count == 0)
            {
                return;
            }
            // create a common time line with all the changes
            // создаём общую линию времени со всеми изменениями

            List<DateTime> allChange = new List<DateTime>();

            for (int i = 0; i < positionsAll.Count; i++)
            {

                if (allChange.FindIndex(chnge => chnge == positionsAll[i].TimeCreate) == -1)
                {
                    allChange.Add(positionsAll[i].TimeCreate);
                }

                if (positionsAll[i].State == PositionStateType.Done)
                {
                    if (allChange.FindIndex(chnge => chnge == positionsAll[i].TimeClose) == -1)
                    {
                        allChange.Add(positionsAll[i].TimeClose);
                    }
                }
            }

            List<DateTime> allChangeSort = new List<DateTime>();

            for (int i = 0; i < allChange.Count; i++)
            {
                if (allChangeSort.Count == 0 ||
                    allChangeSort[allChangeSort.Count - 1] <= allChange[i])
                {
                    allChangeSort.Add(allChange[i]);
                }
                else if (allChangeSort[0] > allChange[i])
                {
                    allChangeSort.Insert(0, allChange[i]);
                }
                else
                {
                    for (int i2 = 0; i2 < allChangeSort.Count; i2++)
                    {
                        if (allChangeSort[i2] <= allChange[i] &&
                            allChangeSort[i2 + 1] >= allChange[i])
                        {
                            allChangeSort.Insert(i2 + 1, allChange[i]);
                            break;
                        }
                    }
                }
            }

            allChange = allChangeSort;

            for (int i = 0; i < volumes.Count; i++)
            {
                volumes[i].Volume = new decimal[allChange.Count].ToList();
            }

            for (int i = 0; i < positionsAll.Count; i++)
            {
                if (positionsAll[i].SecurityName == "si.txt")
                {

                }
                VolumeSecurity volume = volumes.Find(vol => vol.Security == positionsAll[i].SecurityName);
                int indexOpen = allChange.FindIndex(change => change == positionsAll[i].TimeCreate);
                int indexClose = allChange.FindIndex(change => change == positionsAll[i].TimeClose);

                for (int i2 = indexOpen; i2 < volume.Volume.Count; i2++)
                {
                    if (positionsAll[i].Direction == Side.Buy)
                    {
                        volume.Volume[i2] += positionsAll[i].MaxVolume;
                    }
                    else
                    {
                        volume.Volume[i2] -= positionsAll[i].MaxVolume;
                    }
                }
                if (positionsAll[i].State == PositionStateType.Done)
                {
                    for (int i2 = indexClose; i2 < volume.Volume.Count; i2++)
                    {
                        if (positionsAll[i].Direction == Side.Buy)
                        {
                            volume.Volume[i2] -= positionsAll[i].MaxVolume;
                        }
                        else
                        {
                            volume.Volume[i2] += positionsAll[i].MaxVolume;
                        }

                    }
                }
            }

            for (int i = 0; i < volumes.Count; i++)
            {
                if (i % 2 == 0)
                {
                    PaintValuesVolume(volumes[i].Volume, volumes[i].Security, Color.DeepSkyBlue, allChange);
                }
                else
                {
                    PaintValuesVolume(volumes[i].Volume, volumes[i].Security, Color.DarkOrange, allChange);
                }
            }

            float step = 100 / _chartVolume.ChartAreas.Count;

            float y = 0;

            for (int i = 0; i < _chartVolume.ChartAreas.Count; i++)
            {
                _chartVolume.ChartAreas[i].Position.Width = 100;
                _chartVolume.ChartAreas[i].Position.Height = step;
                _chartVolume.ChartAreas[i].Position.Y = y;
                _chartVolume.ChartAreas[i].AlignWithChartArea = _chartVolume.ChartAreas[0].Name;
                y += step;
            }
        }

        /// <summary>
        /// draw volumes by instrument
        /// прорисовать объёмы по инструменту
        /// </summary>
        /// <param name="volume">array/массив</param>
        /// <param name="name">paper/бумага</param>
        /// <param name="color">Color series/цвет серии</param>
        /// <param name="times">times/времена</param>
        private void PaintValuesVolume(List<decimal> volume, string name, Color color, List<DateTime> times)
        {
            if (volume == null ||
                volume.Count == 0)
            {
                return;
            }

            ChartArea areaLineSecurity = new ChartArea("ChartArea" + name);

            areaLineSecurity.CursorX.IsUserSelectionEnabled = false; //allow the user to change the view scope/ разрешаем пользователю изменять рамки представления
            areaLineSecurity.CursorX.IsUserEnabled = true; //trait/чертa

            areaLineSecurity.BorderColor = Color.Black;
            areaLineSecurity.BackColor = Color.FromArgb(17, 18, 23);
            areaLineSecurity.CursorY.LineColor = Color.Gainsboro;
            areaLineSecurity.CursorX.LineColor = Color.Black;
            areaLineSecurity.AxisX.TitleForeColor = Color.Gainsboro;
            areaLineSecurity.AxisY.TitleForeColor = Color.Gainsboro;
            areaLineSecurity.AxisY2.IntervalAutoMode = IntervalAutoMode.FixedCount;

            foreach (var axe in areaLineSecurity.Axes)
            {
                axe.LabelStyle.ForeColor = Color.Gainsboro;
            }

            _chartVolume.ChartAreas.Add(areaLineSecurity);

            Series volumeSeries = new Series("Series" + name);
            volumeSeries.ChartType = SeriesChartType.Line;
            volumeSeries.Color = color;
            volumeSeries.YAxisType = AxisType.Secondary;
            volumeSeries.ChartArea = areaLineSecurity.Name;
            volumeSeries.BorderWidth = 3;
            volumeSeries.ShadowOffset = 2;

            int maxVolume = 0;
            int minVolume = int.MaxValue;

            for (int i = 0; i < volume.Count; i++)
            {
                if (volume[i] > maxVolume)
                {
                    maxVolume = Convert.ToInt32(volume[i]);
                }
                if (volume[i] < minVolume)
                {
                    minVolume = Convert.ToInt32(volume[i]);
                }
                volumeSeries.Points.Add(Convert.ToDouble(volume[i]));
                volumeSeries.Points[volumeSeries.Points.Count - 1].AxisLabel =
                times[i].ToString(_currentCulture);
            }

            _chartVolume.Series.Add(volumeSeries);

            Series nameSeries = new Series("Name" + name);
            nameSeries.ChartType = SeriesChartType.Point;
            nameSeries.Color = color;
            nameSeries.YAxisType = AxisType.Secondary;
            nameSeries.ChartArea = areaLineSecurity.Name;
            nameSeries.BorderWidth = 3;
            nameSeries.ShadowOffset = 2;
            nameSeries.MarkerStyle = MarkerStyle.Square;
            nameSeries.MarkerSize = 4;

            nameSeries.Points.Add(maxVolume);
            nameSeries.Points[0].Label = name;
            nameSeries.Points[0].LabelForeColor = color;


            _chartVolume.Series.Add(nameSeries);

            areaLineSecurity.AxisY2.Maximum = maxVolume + 1;
            areaLineSecurity.AxisY2.Minimum = minVolume - 1;

            int interval = Convert.ToInt32(Math.Abs(maxVolume - minVolume) / 8);

            if (interval > 1)
            {
                areaLineSecurity.AxisY2.Interval = interval;
            }
            else
            {
                areaLineSecurity.AxisY2.Interval = 1;
            }
        }

        /// <summary>
        /// click on the volume website
        /// клик на чарте объёмов
        /// </summary>
        void _chartVolume_Click(object sender, EventArgs e)
        {
            if (_chartVolume.ChartAreas.Count == 0)
            {
                return;
            }
            if (double.IsNaN(_chartVolume.ChartAreas[0].CursorX.Position) ||
                _chartVolume.ChartAreas[0].CursorX.Position == 0)
            {
                return;
            }

            for (int i = 0; i < _chartVolume.Series.Count; i++)
            {
                if (_chartVolume.Series[i].Points.Count == 1)
                {
                    continue;
                }
                for (int i2 = 0; i2 < _chartVolume.Series[i].Points.Count; i2++)
                {
                    _chartVolume.Series[i].Points[i2].Label = "";
                }
            }

            for (int i = 0; i < _chartVolume.Series.Count; i++)
            {
                if (_chartVolume.Series[i].Points.Count == 1)
                {
                    continue;
                }
                string label = "";

                int index = Convert.ToInt32(_chartVolume.ChartAreas[0].CursorX.Position) - 1;

                if (index >= _chartVolume.Series[i].Points.Count)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_chartVolume.Series[i].Points[index].AxisLabel))
                {
                    label += _chartVolume.Series[i].Points[index].AxisLabel + "\n";
                }

                label += _chartVolume.Series[i].Points[index].YValues[0];

                _chartVolume.Series[i].Points[index].Label = label;
                _chartVolume.Series[i].Points[index].LabelForeColor = _chartVolume.Series[i].Points[index].Color;
                _chartVolume.Series[i].Points[index].LabelBackColor = Color.FromArgb(17, 18, 23);
            }
        }
        // sketch of drawdown
        // прорисовка просадки

        /// <summary>
        /// drawdown chart
        /// чарт для просадки
        /// </summary>
        Chart _chartDd;

        /// <summary>
        /// to create a drawdown chart
        /// создать чарт для просадки
        /// </summary>
        private void CreateChartDrowDown()
        {
            try
            {
                _chartDd = new Chart();
                HostDrawdown.Child = _chartDd;
                HostDrawdown.Child.Show();

                _chartDd.Series.Clear();
                _chartDd.ChartAreas.Clear();
                _chartDd.BackColor = Color.FromArgb(17, 18, 23);

                ChartArea areaDdPunct = new ChartArea("ChartAreaDdPunct");
                areaDdPunct.Position.Height = 50;
                areaDdPunct.Position.Width = 100;
                areaDdPunct.Position.Y = 0;
                areaDdPunct.CursorX.IsUserSelectionEnabled = false; //allow the user to change the view scope/ разрешаем пользователю изменять рамки представления
                areaDdPunct.CursorX.IsUserEnabled = true; //trait/чертa

                _chartDd.ChartAreas.Add(areaDdPunct);

                ChartArea areaDdPersent = new ChartArea("ChartAreaDdPersent");
                areaDdPersent.AlignWithChartArea = "ChartAreaDdPunct";
                areaDdPersent.Position.Height = 50;
                areaDdPersent.Position.Width = 100;
                areaDdPersent.Position.Y = 50;
                areaDdPersent.AxisX.Enabled = AxisEnabled.False;
                areaDdPersent.CursorX.IsUserEnabled = true; //trait/чертa

                _chartDd.ChartAreas.Add(areaDdPersent);

                for (int i = 0; i < _chartDd.ChartAreas.Count; i++)
                {
                    _chartDd.ChartAreas[i].BorderColor = Color.Black;
                    _chartDd.ChartAreas[i].BackColor = Color.FromArgb(17, 18, 23);
                    _chartDd.ChartAreas[i].CursorY.LineColor = Color.Gainsboro;
                    _chartDd.ChartAreas[i].CursorX.LineColor = Color.Black;
                    _chartDd.ChartAreas[i].AxisX.TitleForeColor = Color.Gainsboro;
                    _chartDd.ChartAreas[i].AxisY.TitleForeColor = Color.Gainsboro;

                    foreach (var axe in _chartDd.ChartAreas[i].Axes)
                    {
                        axe.LabelStyle.ForeColor = Color.Gainsboro;
                    }
                }

                _chartDd.MouseMove += _chartDd_MouseMove;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _chartDd_MouseMove(object sender, MouseEventArgs e)
        {
            if (_chartDd.ChartAreas[0].AxisX.ScaleView.Size == double.NaN)
            {
                return;
            }

            if(_chartDd.Series == null 
                || _chartDd.Series.Count == 0)
            {
                return;
            }

            int curCountOfPoints = _chartDd.Series[0].Points.Count;

            double sizeArea = _chartDd.ChartAreas[0].InnerPlotPosition.Size.Width;
            double allSizeAbs = _chartDd.Size.Width * (sizeArea / 100);

            double onePointLen = allSizeAbs / curCountOfPoints;

            double curMousePosAbs = e.X;

            double curPointNum = curMousePosAbs / onePointLen - 1;
            try
            {
                curPointNum = Convert.ToDouble(Convert.ToInt32(curPointNum));
            }
            catch
            {
                return;
            }

            if (_chartDd.ChartAreas[0].CursorX.Position != curPointNum)
            {
                _chartDd.ChartAreas[0].CursorX.SetCursorPosition(curPointNum);
            }

            

            int numPointInt = Convert.ToInt32(curPointNum);

            if (numPointInt <= 0)
            {
                return;
            }

            if (_chartDd.Series[0].Points.Count > _lastSeriesDrowDownPointWithLabel)
            {
                _chartDd.Series[0].Points[_lastSeriesDrowDownPointWithLabel].Label = "";
            }
            if (_chartDd.Series[0].Points.Count > numPointInt)
            {
                _chartDd.Series[0].Points[numPointInt].Label
                = _chartDd.Series[0].Points[numPointInt].LegendText;
            }

            if (_chartDd.Series[1].Points.Count > _lastSeriesDrowDownPointWithLabel)
            {
                _chartDd.Series[1].Points[_lastSeriesDrowDownPointWithLabel].Label = "";
            }
            if (_chartDd.Series[1].Points.Count > numPointInt)
            {
                _chartDd.Series[1].Points[numPointInt].Label
                = _chartDd.Series[1].Points[numPointInt].AxisLabel;
            }

            if (_chartDd.Series[2].Points.Count > _lastSeriesDrowDownPointWithLabel)
            {
                _chartDd.Series[2].Points[_lastSeriesDrowDownPointWithLabel].Label = "";
            }
            if (_chartDd.Series[2].Points.Count > numPointInt)
            {
                _chartDd.Series[2].Points[numPointInt].Label
                = _chartDd.Series[2].Points[numPointInt].LegendText;
            }

            _lastSeriesDrowDownPointWithLabel = numPointInt;
        }

        int _lastSeriesDrowDownPointWithLabel;

        /// <summary>
        /// sketch out a drawdown chart
        /// прорисовать чарт для просадки
        /// </summary>
        /// <param name="positionsAll"></param>
        private void PaintDrowDown(List<Position> positionsAll)
        {
            if (_chartDd == null)
            {
                CreateChartDrowDown();
            }

            _chartDd.Series.Clear();

            if (positionsAll == null 
                || positionsAll.Count == 0)
            {
                return;
            }

            List<decimal> ddPunct = new decimal[positionsAll.Count].ToList();
            decimal lastMax = 0;
            decimal currentProfit = 0;

            for (int i = 0; i < positionsAll.Count; i++)
            {
                currentProfit += positionsAll[i].ProfitPortfolioPunkt * (positionsAll[i].MultToJournal /100);

                if (lastMax < currentProfit)
                {
                    lastMax = currentProfit;
                }

                if (currentProfit - lastMax < 0)
                {
                    ddPunct[i] = currentProfit - lastMax;
                }
            }

            Series drowDownPunct = new Series("SeriesDdPunct");
            drowDownPunct.ChartType = SeriesChartType.Line;
            drowDownPunct.Color = Color.DeepSkyBlue;  
            drowDownPunct.LabelForeColor = Color.White;   
            drowDownPunct.YAxisType = AxisType.Secondary;
            drowDownPunct.ChartArea = "ChartAreaDdPunct";
            drowDownPunct.BorderWidth = 2;
            drowDownPunct.ShadowOffset = 2;

            Series nullLine = new Series("SeriesNullLine");
            nullLine.ChartType = SeriesChartType.Line;
            nullLine.YAxisType = AxisType.Secondary;
            nullLine.LabelForeColor = Color.White;
            nullLine.ChartArea = "ChartAreaDdPunct";
            nullLine.ShadowOffset = 0;

            for (int i = 0; i < ddPunct.Count; i++)
            {
                decimal val = Math.Round(ddPunct[i], 6);
                drowDownPunct.Points.Add(Convert.ToDouble(val));

                drowDownPunct.Points[drowDownPunct.Points.Count - 1].AxisLabel =
               positionsAll[i].TimeCreate.ToString(_currentCulture);

                drowDownPunct.Points[drowDownPunct.Points.Count - 1].LegendText = val.ToString();

                nullLine.Points.Add(0);
                nullLine.Points[nullLine.Points.Count - 1].AxisLabel =
                    positionsAll[i].TimeCreate.ToString(_currentCulture);
            }

            _chartDd.Series.Add(drowDownPunct);
            _chartDd.Series.Add(nullLine);

            decimal minOnY = decimal.MaxValue;

            for(int i = 0;i< ddPunct.Count;i++)
            {
                if(ddPunct[i] < minOnY)
                {
                    minOnY = ddPunct[i];
                }
            }

            if(minOnY != decimal.MaxValue)
            {
                _chartDd.ChartAreas[0].AxisY2.Maximum = - Convert.ToDouble(minOnY) * 0.05;
                _chartDd.ChartAreas[0].AxisY2.Minimum = Convert.ToDouble(minOnY) + Convert.ToDouble(minOnY) * 0.05;
            }

            // dd in %
            // дд в %

            List<decimal> ddPepcent = new decimal[positionsAll.Count].ToList();
            lastMax = 0;
            currentProfit = 0;

            for (int i = 0; i < positionsAll.Count; i++)
            {
                currentProfit += positionsAll[i].ProfitPortfolioPersent * (positionsAll[i].MultToJournal / 100);

                if (lastMax < currentProfit)
                {
                    lastMax = currentProfit;
                }

                if (currentProfit - lastMax < 0)
                {
                    ddPepcent[i] = Math.Round(currentProfit - lastMax, 4);
                }
            }

            Series drowDownPersent = new Series("SeriesDdPercent");
            drowDownPersent.ChartType = SeriesChartType.Line;
            drowDownPersent.Color = Color.DarkOrange;
            drowDownPersent.LabelForeColor = Color.White;
            drowDownPersent.YAxisType = AxisType.Secondary;
            drowDownPersent.ChartArea = "ChartAreaDdPersent";
            drowDownPersent.BorderWidth = 2;
            drowDownPersent.ShadowOffset = 2;

            for (int i = 0; i < ddPepcent.Count; i++)
            {
                decimal val = Math.Round(ddPepcent[i], 6);
                drowDownPersent.Points.Add(Convert.ToDouble(val));
                drowDownPersent.Points[drowDownPersent.Points.Count - 1].AxisLabel =
               positionsAll[i].TimeCreate.ToString(_currentCulture);

                drowDownPersent.Points[drowDownPersent.Points.Count - 1].LegendText = val.ToString();
            }

            _chartDd.Series.Add(drowDownPersent);

            decimal minOnY2 = decimal.MaxValue;

            for (int i = 0; i < ddPepcent.Count; i++)
            {
                if (ddPepcent[i] < minOnY2)
                {
                    minOnY2 = ddPepcent[i];
                }
            }

            if (minOnY2 != decimal.MaxValue)
            {
                _chartDd.ChartAreas[1].AxisY2.Maximum = -Convert.ToDouble(minOnY2) * 0.05;
                _chartDd.ChartAreas[1].AxisY2.Minimum = Convert.ToDouble(minOnY2) + Convert.ToDouble(minOnY2) * 0.05;
            }
        }

        // positions
        // позиции

        /// <summary>
        /// open position table
        /// таблица открытых позиций
        /// </summary>
        private DataGridView _openPositionGrid;

        /// <summary>
        /// closed position table
        /// таблица закрытых позиций
        /// </summary>
        private DataGridView _closePositionGrid;

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
                newGrid.ScrollBars = ScrollBars.Vertical;

                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// take a row for the table representing the position
        /// взять строку для таблицы представляющую позицию
        /// </summary>
        /// <param name="position">position/позиция</param>
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

                if (position.ProfitPortfolioPunkt > 0)
                {
                    nRow.DefaultCellStyle.ForeColor = Color.FromArgb(57, 157, 54);
                }
                else if (position.ProfitPortfolioPunkt <= 0)
                {
                    nRow.DefaultCellStyle.ForeColor = Color.FromArgb(254, 84, 0);
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = position.Number;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = position.TimeCreate;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[2].Value = position.TimeClose;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = position.NameBot;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = position.SecurityName;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = position.Direction;

                if (position.Direction == Side.Buy)
                {
                    nRow.Cells[5].Style.ForeColor = Color.DodgerBlue;
                    //nRow.Cells[5].Style.SelectionBackColor = Color.DodgerBlue;
                }
                else
                {
                    nRow.Cells[5].Style.ForeColor = Color.DarkRed;
                    //nRow.Cells[5].Style.SelectionBackColor = Color.DarkOrange;
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = position.State;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = position.MaxVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[8].Value = position.OpenVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[9].Value = position.WaitVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[10].Value = position.EntryPrice.ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[11].Value = position.ClosePrice.ToStringWithNoEndZero();

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
        /// Double-click on the table of open positions
        /// двойной клик по таблице открытых позиций
        /// </summary>
        void _openPositionGrid_DoubleClick(object sender, EventArgs e)
        {
            int number;
            try
            {
                number = Convert.ToInt32(_openPositionGrid.Rows[_openPositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            ShowPositionDialog(number);
        }

        /// <summary>
        /// Click on the table of open positions
        /// клик по таблице открытых позиций
        /// </summary>
        void _openPositionGrid_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            try
            {
                List<MenuItem> items = new List<MenuItem>();

                items.Add(new MenuItem { Text = OsLocalization.Journal.PositionMenuItem8 });
                items[0].Click += OpenDealMoreInfo_Click;

                items.Add(new MenuItem { Text = OsLocalization.Journal.PositionMenuItem9 });
                items[1].Click += OpenDealDelete_Click;

                items.Add(new MenuItem { Text = OsLocalization.Journal.PositionMenuItem10 });
                items[2].Click += OpenDealClearAll_Click;

                if (_botsJournals.Count != 0)
                {
                    List<MenuItem> itemsBots = new List<MenuItem>();

                    for (int i = 0; i < _botsJournals.Count; i++)
                    {
                        itemsBots.Add(new MenuItem { Text = _botsJournals[i].BotName });
                        itemsBots[i].Click += OpenDealCreatePosition_Click;
                    }

                    items.Add(new MenuItem(OsLocalization.Journal.PositionMenuItem13, itemsBots.ToArray()));
                }

                ContextMenu menu = new ContextMenu(items.ToArray());

                _openPositionGrid.ContextMenu = menu;
                _openPositionGrid.ContextMenu.Show(_openPositionGrid, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// open additional information on the position
        /// открыть дополнительную информацию по позиции
        /// </summary>
        void OpenDealMoreInfo_Click(object sender, EventArgs e)
        {
            int number;
            try
            {
                number = Convert.ToInt32(_openPositionGrid.Rows[_openPositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            ShowPositionDialog(number);
        }

        /// <summary>
        /// Delete position
        /// удалить позицию
        /// </summary>
        void OpenDealDelete_Click(object sender, EventArgs e)
        {
            if (_openPositionGrid.Rows.Count == 0)
            {
                return;
            }

            int number;
            try
            {
                number = Convert.ToInt32(_openPositionGrid.Rows[_openPositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                return;
            }

            DeletePosition(number);

            RePaint();
        }

        /// <summary>
        /// clear out the open positions
        /// очистить открытые позиции
        /// </summary>
        void OpenDealClearAll_Click(object sender, EventArgs e)
        {
            List<int> numbers = new List<int>();
            try
            {
                for (int i = 0; i < _openPositionGrid.Rows.Count; i++)
                {
                    numbers.Add(Convert.ToInt32(_openPositionGrid.Rows[i].Cells[0].Value));
                }

            }
            catch (Exception)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message4);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                return;
            }

            for (int i = 0; i < numbers.Count; i++)
            {
                DeletePosition(numbers[i]);
            }
            RePaint();
        }

        /// <summary>
        /// создать новую позицию
        /// </summary>
        void OpenDealCreatePosition_Click(object sender, EventArgs e)
        {
            if (_botsJournals == null ||
                _botsJournals.Count == 0)
            {
                return;
            }

            int number = ((MenuItem)sender).Index;

            string botName = _botsJournals[number].BotName;

            Position newPos = new Position();

            newPos.Number = NumberGen.GetNumberDeal(_startProgram);
            newPos.NameBot = botName;
            _botsJournals[number]._Tabs[0].Journal.SetNewDeal(newPos);

            RePaint();
        }

        private void CreateOpenPositionTable()
        {
            _openPositionGrid = CreateNewTable();
            HostOpenPosition.Child = _openPositionGrid;
            _openPositionGrid.Click += _openPositionGrid_Click;
            _openPositionGrid.DoubleClick += _openPositionGrid_DoubleClick;
        }

        /// <summary>
        /// Draw open positions on the table
        /// прорисовать открытые позиции на таблице
        /// </summary>
        private void PaintOpenPositionGrid(List<Position> positionsAll)
        {
            if (_openPositionGrid == null)
            {
                CreateOpenPositionTable();
            }
            _openPositionGrid.Rows.Clear();

            if(positionsAll == null 
                || positionsAll.Count == 0)
            {
                return;
            }

            for (int i = 0; i < positionsAll.Count; i++)
            {
                if (positionsAll[i].State != PositionStateType.Done &&
                    positionsAll[i].State != PositionStateType.OpeningFail)
                {
                    _openPositionGrid.Rows.Insert(0, GetRow(positionsAll[i]));
                }
            }
        }
        // closed positions
        // позиции закрытые

        /// <summary>
        /// Double-click on the closed seat table
        /// двойной клик по таблице закрытых седлок
        /// </summary>
        void _closePositionGrid_DoubleClick(object sender, EventArgs e)
        {
            if (_closePositionGrid.Rows.Count == 0)
            {
                return;
            }

            int number;
            try
            {
                number = Convert.ToInt32(_closePositionGrid.Rows[_closePositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            ShowPositionDialog(number);
        }

        /// <summary>
        /// Click on the table of closed transactions
        /// клик по таблице закрытых сделок
        /// </summary>
        void _closePositionGrid_Click(object sender, EventArgs e)
        {
            MouseEventArgs mouse = (MouseEventArgs)e;
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }

            try
            {
                MenuItem[] items = new MenuItem[4];

                items[0] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem8 };
                items[0].Click += CloseDealMoreInfo_Click;

                items[1] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem9 };
                items[1].Click += CloseDealDelete_Click;

                items[2] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem10 };
                items[2].Click += CloseDealClearAll_Click;

                items[3] = new MenuItem { Text = OsLocalization.Journal.PositionMenuItem11 };
                items[3].Click += CloseDealSaveInFile_Click;

                ContextMenu menu = new ContextMenu(items);

                _closePositionGrid.ContextMenu = menu;
                _closePositionGrid.ContextMenu.Show(_closePositionGrid, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// save information about closed transactions to a file
        /// сохранить в файл информацию о закрытых сделках
        /// </summary>
        void CloseDealSaveInFile_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog myDialog = new SaveFileDialog();
                myDialog.Filter = "*.txt|";
                myDialog.ShowDialog();

                if (string.IsNullOrEmpty(myDialog.FileName))
                {
                    System.Windows.Forms.MessageBox.Show(OsLocalization.Journal.Message1);
                    return;
                }

                StringBuilder workSheet = new StringBuilder();
                workSheet.Append(OsLocalization.Entity.PositionColumn1);
                workSheet.Append(OsLocalization.Entity.PositionColumn2);
                workSheet.Append(OsLocalization.Entity.PositionColumn3);
                workSheet.Append(OsLocalization.Entity.PositionColumn4);
                workSheet.Append(OsLocalization.Entity.PositionColumn5);
                workSheet.Append(OsLocalization.Entity.PositionColumn6);
                workSheet.Append(OsLocalization.Entity.PositionColumn7);
                workSheet.Append(OsLocalization.Entity.PositionColumn8);
                workSheet.Append(OsLocalization.Entity.PositionColumn9);
                workSheet.Append(OsLocalization.Entity.PositionColumn10);
                workSheet.Append(OsLocalization.Entity.PositionColumn11);
                workSheet.Append(OsLocalization.Entity.PositionColumn12);
                workSheet.Append(OsLocalization.Entity.PositionColumn13);
                workSheet.Append(OsLocalization.Entity.PositionColumn14);
                workSheet.Append(OsLocalization.Entity.PositionColumn15);
                workSheet.Append(OsLocalization.Entity.PositionColumn16);
                workSheet.Append(OsLocalization.Entity.PositionColumn17);
                workSheet.Append(OsLocalization.Entity.PositionColumn18);
                workSheet.Append(OsLocalization.Entity.PositionColumn19 + "\r\n");

                for (int i = 0; i < _closePositionGrid.Rows.Count; i++)
                {
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[0].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[1].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[2].Value + ";");

                    workSheet.Append(_closePositionGrid.Rows[i].Cells[3].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[4].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[5].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[6].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[7].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[8].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[9].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[10].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[11].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[12].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[13].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[14].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[15].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[16].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[17].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[18].Value + "\r\n");
                }

                string fileName = myDialog.FileName;
                if (fileName.Split('.').Length == 1)
                {
                    fileName = fileName + ".txt";
                }

                StreamWriter writer = new StreamWriter(fileName);
                writer.Write(workSheet);
                writer.Close();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }


        }

        /// <summary>
        /// open additional information on the position
        /// открыть дополнительную информацию по позиции
        /// </summary>
        void CloseDealMoreInfo_Click(object sender, EventArgs e)
        {
            int number;
            try
            {
                number = Convert.ToInt32(_closePositionGrid.Rows[_closePositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            ShowPositionDialog(number);
        }

        /// <summary>
        /// delete position
        /// удалить позицию
        /// </summary>
        void CloseDealDelete_Click(object sender, EventArgs e)
        {
            int number;
            try
            {
                number = Convert.ToInt32(_closePositionGrid.Rows[_closePositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                return;
            }

            DeletePosition(number);

            RePaint();
        }

        /// <summary>
        /// clear closed positions
        /// очистить закрытые позиции
        /// </summary>
        void CloseDealClearAll_Click(object sender, EventArgs e)
        {
            List<int> numbers = new List<int>();
            try
            {
                for (int i = 0; i < _closePositionGrid.Rows.Count; i++)
                {
                    numbers.Add(Convert.ToInt32(_closePositionGrid.Rows[i].Cells[0].Value));
                }
                List<Position> poses = new List<Position>();

                for (int i = 0; i < _botsJournals.Count; i++)
                {
                    for (int j = 0; j < _botsJournals[i]._Tabs.Count; j++)
                    {
                        poses.AddRange(_botsJournals[i]._Tabs[j].Journal.AllPosition.FindAll(p => p.State == PositionStateType.OpeningFail));
                    }
                }

                for (int i = 0; i < poses.Count; i++)
                {
                    numbers.Add(poses[i].Number);
                }

            }
            catch (Exception)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message4);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                return;
            }

            for (int i = 0; i < numbers.Count; i++)
            {
                DeletePosition(numbers[i]);
            }
            RePaint();
        }

        private void CreateClosePositionTable()
        {

            _closePositionGrid = CreateNewTable();
            HostClosePosition.Child = _closePositionGrid;
            _closePositionGrid.Click += _closePositionGrid_Click;
            _closePositionGrid.DoubleClick += _closePositionGrid_DoubleClick;
        }

        /// <summary>
        /// Draw closed positions on the table
        /// прорисовать закрытые позиции на таблице
        /// </summary>
        private void PaintClosePositionGrid(List<Position> positionsAll)
        {
            if (_closePositionGrid == null)
            {
                CreateClosePositionTable();
            }
            _closePositionGrid.Rows.Clear();

            if (positionsAll == null 
                || positionsAll.Count == 0)
            {
                return;
            }

            List<Position> closePositions = new List<Position>();

            for (int i = 0; i < positionsAll.Count; i++)
            {
                if (positionsAll[i].State == PositionStateType.Done ||
                    positionsAll[i].State == PositionStateType.OpeningFail)
                {
                    closePositions.Add(positionsAll[i]);
                }
            }

            if (closePositions.Count == 0)
            {
                return;
            }

            for (int i = 0; i < closePositions.Count; i++)
            {
                for (int i2 = i; i2 < closePositions.Count; i2++)
                {
                    if (closePositions[i].TimeClose > closePositions[i2].TimeClose)
                    {
                        Position pos = closePositions[i2];
                        closePositions[i2] = closePositions[i];
                        closePositions[i] = pos;
                    }
                }
            }



            for (int i = 0; i < closePositions.Count; i++)
            {
                _closePositionGrid.Rows.Insert(0, GetRow(closePositions[i]));
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
        }

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        private void ButtonHideLeftPanel_Click(object sender, RoutedEventArgs e)
        {
            // GridTabPrime
            GridActivBots.Visibility = Visibility.Hidden;
            ButtonShowLeftPanel.Visibility = Visibility.Visible;
            GridTabPrime.Margin = new Thickness(0, 0, -0.333, -0.333); 
        }

        private void ButtonShowLeftPanel_Click(object sender, RoutedEventArgs e)
        {
            GridActivBots.Visibility = Visibility.Visible;
            ButtonShowLeftPanel.Visibility = Visibility.Hidden;
            GridTabPrime.Margin = new Thickness(510, 0, -0.333, -0.333);
        }

        // Left Bots Panel

        private void LeftBotsPanelPainter()
        {
            while(true)
            {
                if(IsErase == true)
                {
                    return;
                }

                if(_neadToRapaintBotsGrid)
                {
                    _neadToRapaintBotsGrid = false;
                    PaintBotsGrid();
                    RePaint();
                }
                else
                {
                    Thread.Sleep(200);
                }
            }
        }

        private bool _neadToRapaintBotsGrid;

        private List<Position> _allPositions;
        private List<Position> _longPositions;
        private List<Position> _shortPositions;
        List<BotPanelJournal> _botsJournals;

        DataGridView _gridLeftBotsPanel;

        private void CreateBotsGrid()
        {
            _gridLeftBotsPanel = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells, false);

            _gridLeftBotsPanel.AllowUserToResizeRows = true;
            _gridLeftBotsPanel.ScrollBars = ScrollBars.Vertical;

            CustomDataGridViewCell cell0 = new CustomDataGridViewCell();
            cell0.Style = _gridLeftBotsPanel.DefaultCellStyle;

            DataGridViewComboBoxColumn column0 = new DataGridViewComboBoxColumn();
            //column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Journal.Label9;
            column0.ReadOnly = false;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _gridLeftBotsPanel.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"#";
            column1.ReadOnly = true;
            column1.Width = 75;
            _gridLeftBotsPanel.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Journal.Label10;
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridLeftBotsPanel.Columns.Add(column2);

            DataGridViewColumn column22 = new DataGridViewColumn();
            column22.CellTemplate = cell0;
            column22.HeaderText = OsLocalization.Journal.Label11;
            column22.ReadOnly = true;
            column22.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridLeftBotsPanel.Columns.Add(column22);

            DataGridViewCheckBoxColumn column4 = new DataGridViewCheckBoxColumn();
            column4.HeaderText = OsLocalization.Journal.Label12;
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridLeftBotsPanel.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = @"Mult %";
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridLeftBotsPanel.Columns.Add(column5);

            HostBotsSelected.Child = _gridLeftBotsPanel;
            HostBotsSelected.Child.Show();

            _gridLeftBotsPanel.CellEndEdit += _gridLeftBotsPanel_CellEndEdit;
            _gridLeftBotsPanel.CellBeginEdit += _gridLeftBotsPanel_CellBeginEdit;
        }

        private void PaintBotsGrid()
        {
            if (!TabControlPrime.CheckAccess())
            {
                TabControlPrime.Dispatcher.Invoke(PaintBotsGrid);
                return;
            }

            _gridLeftBotsPanel.CellEndEdit -= _gridLeftBotsPanel_CellEndEdit;

            List<PanelGroups> groups = GetGroups(_botsJournals);
            List<DataGridViewRow> rows = new List<DataGridViewRow>();
            List<string> allGroups = GetAllGroups(groups);


            for(int i = 0;i < groups.Count;i++)
            {
                rows.AddRange(GetGroupRowList(groups[i], allGroups));
            }

            _gridLeftBotsPanel.Rows.Clear();

            for(int i = 0;i < rows.Count;i++)
            {
                _gridLeftBotsPanel.Rows.Add(rows[i]);
            }

            _gridLeftBotsPanel.CellEndEdit += _gridLeftBotsPanel_CellEndEdit;
        }

        private List<string> GetAllGroups(List<PanelGroups> groups)
        {
            List<string> groupsInStrArray = new List<string>();

            for(int i = 0;i < groups.Count;i++)
            {
                groupsInStrArray.Add(groups[i].BotGroup);
            }
            return groupsInStrArray;
        }

        private List<DataGridViewRow> GetGroupRowList(PanelGroups group, List<string> groupsAll)
        {
            List<DataGridViewRow> rows = new List<DataGridViewRow>();

            DataGridViewRow row = new DataGridViewRow();

            DataGridViewComboBoxCell groupBox = new DataGridViewComboBoxCell();
            groupBox.Items.Add(group.BotGroup);
            groupBox.Value = group.BotGroup;
            row.Cells.Add(groupBox); // группа
            groupBox.ReadOnly = true;

            row.Cells.Add(new DataGridViewTextBoxCell()); // номер
            row.Cells.Add(new DataGridViewTextBoxCell()); // имя
            row.Cells.Add(new DataGridViewTextBoxCell()); // класс 

            DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();
            cell.Value = group.Panels[0].IsOn;

            row.Cells.Add(cell); // вкл / выкл

            row.Cells.Add(new DataGridViewTextBoxCell()); // мультипликатор
            rows.Add(row);

            for(int i = 0;i < group.Panels.Count;i++)
            {
                rows.AddRange(GetPanelRowList(group.Panels[i], groupsAll,i+1));
            }

            return rows;
        }

        private List<DataGridViewRow> GetPanelRowList(BotPanelJournal panel, List<string> groupNames, int panelNum)
        {
            List<DataGridViewRow> rows = new List<DataGridViewRow>();

            DataGridViewRow row = new DataGridViewRow();

            DataGridViewComboBoxCell groupBox = new DataGridViewComboBoxCell();

            for(int i = 0;i < groupNames.Count;i++)
            {
                groupBox.Items.Add(groupNames[i]);
            }
            groupBox.Items.Add("new");
            groupBox.Value = panel.BotGroup;

            row.Cells.Add(groupBox); // группа
          
            row.Cells.Add(new DataGridViewTextBoxCell()); // номер
            row.Cells[row.Cells.Count - 1].Value = panelNum;
            
            row.Cells.Add(new DataGridViewTextBoxCell()); // имя
            row.Cells[row.Cells.Count - 1].Value = panel.BotName;

            row.Cells.Add(new DataGridViewTextBoxCell()); // класс 
            row.Cells[row.Cells.Count - 1].Value = panel.BotClass;

            DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();
            cell.Value = panel.IsOn.ToString();

            row.Cells.Add(cell); // вкл / выкл

            row.Cells.Add(new DataGridViewTextBoxCell()); // мультипликатор
            row.Cells[row.Cells.Count - 1].Value = panel.Mult.ToString();

            rows.Add(row);

            return rows;
        }

        private List<PanelGroups> GetGroups(List<BotPanelJournal> oldPanels)
        {
            List<PanelGroups> groups = new List<PanelGroups>();

            for (int i = 0; i < oldPanels.Count; i++)
            {
                PanelGroups myGroup = groups.Find(g => g.BotGroup == oldPanels[i].BotGroup);

                if(myGroup == null)
                {
                    myGroup = new PanelGroups();
                    myGroup.BotGroup = oldPanels[i].BotGroup;
                    groups.Add(myGroup);
                }

                myGroup.Panels.Add(oldPanels[i]);
            }

            return groups;
        }

        private List<Journal> GetActiveJournals()
        {
            List<Journal> journals = new List<Journal>();

            for(int i = 0;i < _botsJournals.Count;i++)
            {
                BotPanelJournal panel = _botsJournals[i];

                for(int i2 = 0;i2 < panel._Tabs.Count;i2++)
                {
                    if(panel.IsOn)
                    {
                        journals.Add(panel._Tabs[i2].Journal);

                        List<Position> poses = panel._Tabs[i2].Journal.AllPosition;

                        for(int i3 = 0; poses != null && i3 < poses.Count;i3++)
                        {
                            poses[i3].MultToJournal = panel.Mult;
                        }

                    }
                }
            }

            return journals;
        }

        private void CreatePositionsLists()
        {
            // 1 collecting all journals.
            // 1 собираем все журналы
            List<Journal> myJournals = GetActiveJournals();

            if (myJournals == null
                || myJournals.Count == 0)
            {
                _allPositions?.Clear();
                _longPositions?.Clear();
                _shortPositions?.Clear();
                startTime = DateTime.MinValue;
                endTime = DateTime.MinValue;

                return;
            }
            // 2 sorting deals on ALL / Long / Short
            // 2 сортируем сделки на ВСЕ / Лонг / Шорт

            List<Position> positionsAll = new List<Position>();

            for (int i = 0; i < myJournals.Count; i++)
            {
                if (myJournals[i].AllPosition != null) positionsAll.AddRange(myJournals[i].AllPosition);
            }

            List<Position> newPositionsAll = new List<Position>();

            for (int i = 0; i < positionsAll.Count; i++)
            {
                DateTime timeCreate = positionsAll[i].TimeCreate;

                if (newPositionsAll.Count == 0 ||
                    newPositionsAll[newPositionsAll.Count - 1].TimeCreate <= timeCreate)
                {
                    newPositionsAll.Add(positionsAll[i]);
                }
                else if (newPositionsAll[0].TimeCreate >= timeCreate)
                {
                    newPositionsAll.Insert(0, positionsAll[i]);
                }
                else
                {
                    for (int i2 = 0; i2 < newPositionsAll.Count - 1; i2++)
                    {
                        if (newPositionsAll[i2].TimeCreate <= timeCreate &&
                            newPositionsAll[i2 + 1].TimeCreate >= timeCreate)
                        {
                            newPositionsAll.Insert(i2 + 1, positionsAll[i]);
                            break;
                        }
                    }
                }
            }

            positionsAll = newPositionsAll;

            _allPositions = positionsAll.FindAll(p => p.State != PositionStateType.OpeningFail);
            _longPositions = _allPositions.FindAll(p => p.Direction == Side.Buy);
            _shortPositions = _allPositions.FindAll(p => p.Direction == Side.Sell);

            if(_allPositions.Count == 0)
            {
                return;
            }

            startTime = _allPositions[0].TimeOpen;
            endTime = _allPositions[_allPositions.Count - 1].TimeOpen;
            minTime = startTime;
            maxTime = endTime;
        }

        private void LoadGroups()
        {
            string path = @"Engine\" + _startProgram + @"JournalSettings.txt";

            //_botsJournals;

            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    while(reader.EndOfStream == false)
                    {
                        string botString = reader.ReadLine();

                        if (string.IsNullOrEmpty(botString))
                        {
                            continue;
                        }

                        string[] saveArray = botString.Split('&');

                        string botName = saveArray[0];
                        string botGroup = saveArray[1];
                        decimal mult = saveArray[2].ToDecimal();
                        bool isOn = Convert.ToBoolean(saveArray[3]);

                        BotPanelJournal journal = _botsJournals.Find(b => b.BotName == botName);

                        if(journal == null)
                        {
                            continue;
                        }

                        journal.BotGroup = botGroup;
                        journal.Mult = mult;
                        journal.IsOn = isOn;
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void SaveGroups()
        {
            try
            {
                string path = @"Engine\" + _startProgram + @"JournalSettings.txt";

                using (StreamWriter writer = new StreamWriter(path, false)
                    )
                {
                    for(int i = 0;i < _botsJournals.Count;i++)
                    {
                        string res = _botsJournals[i].BotName + 
                            "&" + _botsJournals[i].BotGroup +
                            "&" + _botsJournals[i].Mult +
                            "&" + _botsJournals[i].IsOn ;


                        writer.WriteLine(res);
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void _gridLeftBotsPanel_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex == 4)
            {
                _lastChangeRow = e.RowIndex;
               Task.Run(ChangeOnOffAwait);
            }
        }

        private void _gridLeftBotsPanel_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if(e.ColumnIndex == 0)
            {
                ChangeGroup(e);
            }
            else if (e.ColumnIndex == 5)
            {
                ChangeMult(e);
            }

        }

        int _lastChangeRow;

        private void ChangeOnOffAwait()
        {
            Thread.Sleep(200);
            ChangeOnOff(_lastChangeRow);
        }

        private void ChangeOnOff(int rowIndx)
        {
            if(TextBoxFrom.Dispatcher.CheckAccess() == false)
            {
                TextBoxFrom.Dispatcher.Invoke(new Action<int>(ChangeOnOff), rowIndx);
                return;
            }

            string textInCell = _gridLeftBotsPanel.Rows[rowIndx].Cells[4].Value.ToString();

            BotPanelJournal bot = GetBotByNum(rowIndx);

            if (bot == null)
            {
                ChangeOnOffByGroup(rowIndx);
                return;
            }

            if(Convert.ToBoolean(textInCell) == false)
            {
                bot.IsOn = true;
            }
            else
            {
                bot.IsOn = false;
            }

            SaveGroups();
            CreatePositionsLists();
            _neadToRapaintBotsGrid = true;
        }

        private void ChangeOnOffByGroup(int rowIndx)
        {
            string textInCell = _gridLeftBotsPanel.Rows[rowIndx].Cells[4].Value.ToString();

            PanelGroups group = GetGroupByNum(rowIndx);

            if (group == null)
            {
                return;
            }

            for (int i = 0; i < group.Panels.Count; i++)
            {
                if(textInCell =="True")
                {
                    group.Panels[i].IsOn = false;
                }
                else if(textInCell == "False")
                {
                    group.Panels[i].IsOn = true;
                }
            }

            SaveGroups();
            _neadToRapaintBotsGrid = true;
            CreatePositionsLists();
        }

        private void ChangeMult(DataGridViewCellEventArgs e)
        {
            string textInCell = _gridLeftBotsPanel.Rows[e.RowIndex].Cells[5].Value.ToString();

            BotPanelJournal bot = GetBotByNum(e.RowIndex);

            if (bot == null)
            {
                return;
            }

            try
            {
                bot.Mult = textInCell.ToDecimal();
            }
            catch
            {
                return;
            }

            SaveGroups();
            _neadToRapaintBotsGrid = true;
            CreatePositionsLists();
        }

        private void ChangeGroup(DataGridViewCellEventArgs e)
        {
            string textInCell = _gridLeftBotsPanel.Rows[e.RowIndex].Cells[0].Value.ToString();

            BotPanelJournal bot = GetBotByNum(e.RowIndex);

            if (bot == null)
            {
                return;
            }

            if (textInCell == "new")
            { // Создаём новую группу
                List<PanelGroups> groups = GetGroups(_botsJournals);
                List<string> allGroups = GetAllGroups(groups);

                NewGroupAddInJournalUi ui = new NewGroupAddInJournalUi(allGroups);
                ui.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ui.ShowDialog();

                if (ui.IsAccepted == false)
                {
                    return;
                }
                bot.BotGroup = ui.NewGroupName;
            }
            else
            { // обновляем группу у бота
                bot.BotGroup = textInCell;
            }

            SaveGroups();
            _neadToRapaintBotsGrid = true;
        }

        private BotPanelJournal GetBotByNum(int num)
        {
            List<PanelGroups> groups = GetGroups(_botsJournals);

            int curBotNum = 1;

            for(int i = 0;i < groups.Count; i++)
            {
                for (int i2 = 0;i2< groups[i].Panels.Count;i2++)
                {
                    if (num == curBotNum)
                    {
                        return groups[i].Panels[i2];
                    }

                    curBotNum++;
                }
                curBotNum++;
            }

            return null;
        }

        private PanelGroups GetGroupByNum(int num)
        {
            List<PanelGroups> groups = GetGroups(_botsJournals);

            int curBotNum = 1;

            for (int i = 0; i < groups.Count; i++)
            {
                for (int i2 = 0; i2 < groups[i].Panels.Count; i2++)
                {
                    if (curBotNum >= num)
                    {
                        return groups[i];
                    }

                    curBotNum++;
                }
                curBotNum++;
            }

            return null;
        }

        // переключалка отображения позиций по времени

        DateTime startTime;
        DateTime minTime;
        DateTime endTime;
        DateTime maxTime;

        private void CreateSlidersShowPositions()
        {
            SliderFrom.ValueChanged -= SliderFrom_ValueChanged;
            SliderTo.ValueChanged -= SliderTo_ValueChanged;

            if (startTime == DateTime.MinValue 
                || endTime == DateTime.MinValue)
            {
                TextBoxFrom.Text = "";
                TextBoxTo.Text = "";
                return;
            }

            TextBoxFrom.Text = startTime.ToString(new CultureInfo("RU-ru"));
            TextBoxTo.Text = endTime.ToString(new CultureInfo("RU-ru"));

            SliderFrom.Minimum = (startTime - DateTime.MinValue).TotalMinutes;
            SliderFrom.Maximum = (endTime - DateTime.MinValue).TotalMinutes;
            SliderFrom.Value = (startTime - DateTime.MinValue).TotalMinutes;

            SliderTo.Minimum = (startTime - DateTime.MinValue).TotalMinutes;
            SliderTo.Maximum = (endTime - DateTime.MinValue).TotalMinutes;
            SliderTo.Value = (startTime - DateTime.MinValue).TotalMinutes;

            if (endTime != DateTime.MinValue &&
                SliderFrom.Minimum + SliderTo.Maximum - (endTime - DateTime.MinValue).TotalMinutes > 0)
            {
                SliderTo.Value =
                SliderFrom.Minimum + SliderTo.Maximum - (endTime - DateTime.MinValue).TotalMinutes;
            }

            SliderFrom.ValueChanged += SliderFrom_ValueChanged;
            SliderTo.ValueChanged += SliderTo_ValueChanged;

        }


        private void SliderTo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TextBoxTo.TextChanged -= TextBoxTo_TextChanged;

            DateTime to = DateTime.MinValue.AddMinutes(SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value);
            endTime = to;
            TextBoxTo.Text = to.ToString(new CultureInfo("RU-ru"));

            if (SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value < SliderFrom.Value)
            {
                SliderFrom.Value = SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value;
            }
            TextBoxTo.TextChanged += TextBoxTo_TextChanged;
        }

        void SliderFrom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TextBoxFrom.TextChanged -= TextBoxFrom_TextChanged;

            DateTime from = DateTime.MinValue.AddMinutes(SliderFrom.Value);
            startTime = from;
            TextBoxFrom.Text = from.ToString(new CultureInfo("RU-ru"));

            if (SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value < SliderFrom.Value)
            {
                SliderTo.Value = SliderFrom.Minimum + SliderFrom.Maximum - SliderFrom.Value;
            }

            TextBoxFrom.TextChanged += TextBoxFrom_TextChanged;
        }

        void TextBoxTo_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DateTime to;
            try
            {
                to = Convert.ToDateTime(TextBoxTo.Text);

                if (to < minTime ||
                    to > maxTime)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                TextBoxTo.Text = endTime.ToString(new CultureInfo("RU-ru"));
                return;
            }

            endTime = to;
            // SliderTo.Value = SliderFrom.Minimum + SliderFrom.Maximum - to.Minute;
            // SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value
            SliderTo.Value = SliderFrom.Minimum + SliderTo.Maximum - (to - DateTime.MinValue).TotalMinutes;
        }

        void TextBoxFrom_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DateTime from;
            try
            {
                from = Convert.ToDateTime(TextBoxFrom.Text);

                if (from < minTime ||
                    from > maxTime)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                TextBoxFrom.Text = startTime.ToString(new CultureInfo("RU-ru"));
                return;
            }

            startTime = from;
            SliderFrom.Value = (startTime - DateTime.MinValue).TotalMinutes;
        }

        private void ButtonReload_Click(object sender, RoutedEventArgs e)
        {
            RePaint();
        }
    }

    /// <summary>
    /// log storage class
    /// класс хранилище журналов
    /// </summary>
    public class BotTabJournal
    {
        public int TabNum;

        public Journal Journal;
    }

    /// <summary>
    /// log storage class
    /// класс хранилище журналов
    /// </summary>
    public class BotPanelJournal
    {
        public string BotName;

        public string BotGroup = "none";

        public string BotClass;

        public bool IsOn = true;

        public decimal Mult = 100;

        public List<BotTabJournal> _Tabs;

        public List<Position> AllPositions
        {
            get
            {
                List<Position> poses = new List<Position>();

                for (int i = 0; i < _Tabs.Count; i++)
                {
                    poses.AddRange(_Tabs[i].Journal.AllPosition);
                }

                return poses;
            }
        }
    }

    public class PanelGroups
    {
        public string BotGroup = "none";

        public List<BotPanelJournal> Panels = new List<BotPanelJournal>();

    }
}
