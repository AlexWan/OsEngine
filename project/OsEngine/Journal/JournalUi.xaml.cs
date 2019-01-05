/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Journal.Internal;
using OsEngine.Logging;
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Market;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;
using ChartArea = System.Windows.Forms.DataVisualization.Charting.ChartArea;
using ContextMenu = System.Windows.Forms.ContextMenu;
using MenuItem = System.Windows.Forms.MenuItem;
using Series = System.Windows.Forms.DataVisualization.Charting.Series;

namespace OsEngine.Journal
{
    /// <summary>
    /// Логика взаимодействия для JournalNewUi.xaml
    /// </summary>
    public partial class JournalUi
    {

        /// <summary>
        /// является ли окно утилизированным
        /// </summary>
        public bool IsErase;

        /// <summary>
        /// объекты содержащие позиции и роботов
        /// </summary>
        private List<BotPanelJournal> _botsJournals;

        /// <summary>
        /// конструктор
        /// </summary>
        public JournalUi(List<BotPanelJournal> botsJournals,StartProgram startProgram)
        {
            _startProgram = startProgram;
            InitializeComponent();
            _botsJournals = botsJournals;
            TabControlCreateNameBots();
            CreateTableToStatistic();
            CreateChartProfit();
            CreateChartVolume();
            CreateChartDrowDown();
            CreatPositionTables();

            TabBots.SizeChanged += TabBotsSizeChanged;
            TabControlPrime.SelectionChanged += TabControlPrime_SelectionChanged;

            Thread worker = new Thread(ThreadWorkerPlace);
            worker.IsBackground = true;
            worker.Start();
        }

        /// <summary>
        /// изменился размер вкладок
        /// </summary>
        void TabBotsSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double up = TabBots.ActualHeight - 14;

            if (up < 0)
            {
                up = 0;
            }

            GridTabPrime.Margin = new Thickness(5, up, 0, 0);
        }

        private object _paintLocker = new object();

        private StartProgram _startProgram;

        /// <summary>
        /// главный метод перерисовки таблиц отчётов
        /// </summary>
        public void RePaint()
        {
            if (!TabControlLeft.CheckAccess())
            {
                TabControlLeft.Dispatcher.Invoke(RePaint);
                return;
            }

            if (_botsJournals == null || _botsJournals.Count == 0) // TabBotsPrime.SelectedItem
            {
                return;
            }

            if (TabControlLeft.SelectedItem == null)
            {
                return;
            }

            lock (_paintLocker)
            {
                // 1 собираем все журналы
                List<Journal> myJournals = new List<Journal>();

                for (int i = 0; i < _botsJournals.Count; i++)
                {
                    string name = ((TabItem)TabBots.SelectedItem).Header.ToString();
                    // 1 берём только нашего бота
                    if (name == "V" || name == _botsJournals[i].BotName)
                    {
                        for (int i2 = 0; i2 < _botsJournals[i]._Tabs.Count; i2++)
                        {
                            string nameTab = ((TabItem)TabControlLeft.SelectedItem).Header.ToString().Replace(" ","");
                            // 2 берём только наши вкладки
                            if (name == "V" || nameTab == "V" || nameTab == _botsJournals[i]._Tabs[i2].TabNum.ToString())
                            {
                                myJournals.Add(_botsJournals[i]._Tabs[i2].Journal);
                            }
                        }
                    }
                }

                if (myJournals.Count == 0)
                {
                    return;
                }

                // 2 сортируем сделки на ВСЕ / Лонг / Шорт

                List<Position> positionsAll = new List<Position>();
                List<Position> positionsLong = new List<Position>();
                List<Position> positionsShort = new List<Position>();

                for (int i = 0; i < myJournals.Count; i++)
                {
                    if (myJournals[i].AllPosition != null) positionsAll.AddRange(myJournals[i].AllPosition);
                    if (myJournals[i].CloseAllLongPositions != null)
                        positionsLong.AddRange(myJournals[i].CloseAllLongPositions);
                    if (myJournals[i].CloseAllShortPositions != null)
                        positionsShort.AddRange(myJournals[i].CloseAllShortPositions);
                }


                positionsAll =
                    positionsAll.FindAll(
                        pos => pos.State != PositionStateType.OpeningFail && pos.State != PositionStateType.Opening);
                positionsLong =
                    positionsLong.FindAll(
                        pos => pos.State != PositionStateType.OpeningFail && pos.State != PositionStateType.Opening);
                positionsShort =
                    positionsShort.FindAll(
                        pos => pos.State != PositionStateType.OpeningFail && pos.State != PositionStateType.Opening);

                // 3 сортируем сделки по времени(это лучше в отдельном методе)


                List<Position> newPositionsAll = new List<Position>();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    if (newPositionsAll.Count == 0 ||
                        newPositionsAll[newPositionsAll.Count - 1].TimeCreate < positionsAll[i].TimeCreate)
                    {
                        newPositionsAll.Add(positionsAll[i]);
                    }
                    else if (newPositionsAll[0].TimeCreate > positionsAll[i].TimeCreate)
                    {
                        newPositionsAll.Insert(0, positionsAll[i]);
                    }
                    else
                    {
                        for (int i2 = 0; i2 < newPositionsAll.Count-1; i2++)
                        {
                            if (newPositionsAll[i2].TimeCreate <= positionsAll[i].TimeCreate &&
                                newPositionsAll[i2 + 1].TimeCreate >= positionsAll[i].TimeCreate)
                            {
                                newPositionsAll.Insert(i2 + 1, positionsAll[i]);
                                break;
                            }
                        }
                    }
                }

                positionsAll = newPositionsAll;

                List<Position> newPositionsLong = new List<Position>();

                for (int i = 0; i < positionsLong.Count; i++)
                {
                    if (newPositionsLong.Count == 0 ||
                        newPositionsLong[newPositionsLong.Count - 1].TimeCreate < positionsLong[i].TimeCreate)
                    {
                        newPositionsLong.Add(positionsLong[i]);
                    }
                    else if (newPositionsLong[0].TimeCreate > positionsLong[i].TimeCreate)
                    {
                        newPositionsLong.Insert(0, positionsLong[i]);
                    }
                    else
                    {
                        for (int i2 = 0; i2 < newPositionsLong.Count-1; i2++)
                        {
                            if (newPositionsLong[i2].TimeCreate <= positionsLong[i].TimeCreate &&
                                newPositionsLong[i2 + 1].TimeCreate >= positionsLong[i].TimeCreate)
                            {
                                newPositionsLong.Insert(i2 + 1, positionsLong[i]);
                                break;
                            }
                        }
                    }
                }

                positionsLong = newPositionsLong;

                List<Position> newPositionsShort = new List<Position>();

                for (int i = 0; i < positionsShort.Count; i++)
                {
                    if (newPositionsShort.Count == 0 ||
                        newPositionsShort[newPositionsShort.Count - 1].TimeCreate < positionsShort[i].TimeCreate)
                    {
                        newPositionsShort.Add(positionsShort[i]);
                    }
                    else if (newPositionsShort[0].TimeCreate > positionsShort[i].TimeCreate)
                    {
                        newPositionsShort.Insert(0, positionsShort[i]);
                    }
                    else
                    {
                        for (int i2 = 0; i2 < newPositionsShort.Count-1; i2++)
                        {
                            if (newPositionsShort[i2].TimeCreate <= positionsShort[i].TimeCreate &&
                                newPositionsShort[i2 + 1].TimeCreate >= positionsShort[i].TimeCreate)
                            {
                                newPositionsShort.Insert(i2 + 1, positionsShort[i]);
                                break;
                            }
                        }
                    }
                }

                positionsShort = newPositionsShort;

                if (TabControlPrime.SelectedIndex == 0)
                {
                    PaintProfitOnChart(positionsAll);
                }
                else if (TabControlPrime.SelectedIndex == 1)
                {
                    bool neadShowTickState = !(myJournals.Count > 1);
                    PaintStatTable(positionsAll, positionsLong, positionsShort, neadShowTickState);
                }
                else if (TabControlPrime.SelectedIndex == 2)
                {
                    PaintDrowDown(positionsAll);
                }
                else if (TabControlPrime.SelectedIndex == 3)
                {
                    PaintVolumeOnChart(positionsAll);
                }
                else if (TabControlPrime.SelectedIndex == 4)
                {
                    PaintOpenPositionGrid(positionsAll);
                }
                else if (TabControlPrime.SelectedIndex == 5)
                {
                    PaintClosePositionGrid(positionsAll);
                }
            }
        }

        /// <summary>
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
                        PositionUi ui = new PositionUi(pos);
                        ui.ShowDialog();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// место работы потока обновляющего статистку
        /// </summary>
        private void ThreadWorkerPlace()
        {
            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }
            while (true)
            {
                Thread.Sleep(30000);

                if (IsErase == true)
                {
                    return;
                }

                RePaint();
            }
        }

// менеджмент вкладок

        /// <summary>
        /// заполнение основного TabControl именами роботов
        /// </summary>
        private void TabControlCreateNameBots()
        {
            TabBots.SelectionChanged -= TabBotsSelectionChanged;
            TabBots.Items.Clear();

            if (_botsJournals.Count >= 1)
            {
                TabItem item = new TabItem() { Header = "V", FontSize = 12 };
                TabBots.Items.Add(item);
            }

            for (int i = 0; i < _botsJournals.Count; i++)
            {
                // добавление нового элемента
                TabItem item = new TabItem() { Header = _botsJournals[i].BotName.ToString(), FontSize = 12 };
                TabBots.Items.Add(item);
            }
            TabBots.SelectionChanged += TabBotsSelectionChanged;
        }

        /// <summary>
        /// событие выбора на панели роботов , заполнение TabControl с Tabs роботов
        /// </summary>
        private void TabBotsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReloadTabs();
            RePaint();
        }

        /// <summary>
        /// переключилась вкладка статистки
        /// </summary>
        void TabControlPrime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RePaint();
        }

        /// <summary>
        /// обновление левой вкладки робота
        /// </summary>
        private void ReloadTabs()
        {
            TabControlLeft.SelectionChanged -= TabControlLeftSelectionChanged;
            TabControlLeft.Items.Clear();
            if (_botsJournals.Count != 0) // TabBotsPrime.SelectedItem
            {
                for (int i = 0; i < _botsJournals.Count; i++)
                {
                    if (((TabItem)TabBots.Items[TabBots.SelectedIndex]).Header.ToString() ==
                        _botsJournals[i].BotName)
                    {
                        if (_botsJournals[i]._Tabs.Count > 1)
                        {
                            TabItem item = new TabItem() { Header = " V", FontSize = 12 };
                            TabControlLeft.Items.Add(item);
                        }

                        for (int i2 = 0; i2 < _botsJournals[i]._Tabs.Count; i2++)
                        {
                            TabItem item = new TabItem() { Header = " " + i2.ToString(), FontSize = 12 };
                            TabControlLeft.Items.Add(item);
                        }

                        TabControlLeft.SelectedIndex = 0;
                    }
                }

                if (((TabItem)TabBots.Items[TabBots.SelectedIndex]).Header.ToString() == "V")
                {
                    TabItem item = new TabItem() { Header = " V", FontSize = 12 };
                    TabControlLeft.Items.Add(item);
                    TabControlLeft.SelectedItem = item;
                }

                TabControlLeft.SelectionChanged += TabControlLeftSelectionChanged;

            }
        }

        /// <summary>
        /// событие при изменении выборе Item TabControl с Tabs роботов
        /// </summary>
        private void TabControlLeftSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // выбор вкладки робота
            RePaint();
        }

// заполнение таблицы статистики

        /// <summary>
        /// таблица для прорисовки Статистики
        /// </summary>
        private DataGridView _gridStatistics;

        /// <summary>
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
                column1.HeaderText = @"Все";
                column1.ReadOnly = true;
                column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridStatistics.Columns.Add(column1);


                DataGridViewColumn column2 = new DataGridViewColumn();
                column2.CellTemplate = cell0;
                column2.HeaderText = @"Лонг";
                column2.ReadOnly = true;
                column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridStatistics.Columns.Add(column2);

                DataGridViewColumn column3 = new DataGridViewColumn();
                column3.CellTemplate = cell0;
                column3.HeaderText = @"Шорт";
                column3.ReadOnly = true;
                column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridStatistics.Columns.Add(column3);

                for (int i = 0; i < 27; i++)
                {
                    _gridStatistics.Rows.Add(); // добавление строки
                }

                _gridStatistics.Rows[0].Cells[0].Value = @"Чистый П\У";
                _gridStatistics.Rows[1].Cells[0].Value = @"Чистый ПУ %";
                _gridStatistics.Rows[2].Cells[0].Value = "Количество сделок";
                _gridStatistics.Rows[3].Cells[0].Value = "Profit Factor";
                _gridStatistics.Rows[4].Cells[0].Value = "Recovery";

                _gridStatistics.Rows[6].Cells[0].Value = @"Сред. П\У   движение";
                _gridStatistics.Rows[7].Cells[0].Value = @"Сред. П\У % движение";
                _gridStatistics.Rows[8].Cells[0].Value = @"Сред. П\У    капитал";
                _gridStatistics.Rows[9].Cells[0].Value = @"Сред. П\У %  капитал";


                _gridStatistics.Rows[11].Cells[0].Value = "Прибыльных сделок";
                _gridStatistics.Rows[12].Cells[0].Value = @"Прибыльных %";
                _gridStatistics.Rows[13].Cells[0].Value = @"Сред. П\У     движение";
                _gridStatistics.Rows[14].Cells[0].Value = @"Сред. П\У %   движение";
                _gridStatistics.Rows[15].Cells[0].Value = @"Сред. П\У      капитал";
                _gridStatistics.Rows[16].Cells[0].Value = @"Сред. П\У %    капитал";
                _gridStatistics.Rows[17].Cells[0].Value = "Максимум подряд";

                _gridStatistics.Rows[19].Cells[0].Value = "Убыточных сделок";
                _gridStatistics.Rows[20].Cells[0].Value = "Убыточных %";
                _gridStatistics.Rows[21].Cells[0].Value = @"Сред. П\У     движение";
                _gridStatistics.Rows[22].Cells[0].Value = @"Сред. П\У %   движение";
                _gridStatistics.Rows[23].Cells[0].Value = @"Сред. П\У      капитал";
                _gridStatistics.Rows[24].Cells[0].Value = @"Сред. П\У %    капитал";
                _gridStatistics.Rows[25].Cells[0].Value = @"Макс. подряд";
                _gridStatistics.Rows[26].Cells[0].Value = "";
                _gridStatistics.Rows[27].Cells[0].Value = @"Макс. просадка% капитал";
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// прорисовать таблицу статистики
        /// </summary>
        private void PaintStatTable(List<Position> positionsAll, List<Position> positionsLong, List<Position> positionsShort, bool neadShowTickState)
        {
            List<string> positionsAllState = PositionStaticticGenerator.GetStatisticNew(positionsAll, neadShowTickState);
            List<string> positionsLongState = PositionStaticticGenerator.GetStatisticNew(positionsLong, neadShowTickState);
            List<string> positionsShortState = PositionStaticticGenerator.GetStatisticNew(positionsShort, neadShowTickState);

            if (positionsAllState == null)
            {
                for (int i = 0; i < 27; i++)
                {
                    _gridStatistics.Rows[i].Cells[1].Value = "";
                }
            }
            if (positionsLongState == null)
            {
                for (int i = 0; i < 27; i++)
                {
                    _gridStatistics.Rows[i].Cells[2].Value = "";
                }
            }
            if (positionsShortState == null)
            {
                for (int i = 0; i < 27; i++)
                {
                    _gridStatistics.Rows[i].Cells[3].Value = "";
                }
            }
            if (positionsLongState != null)
            {
                for (int i = 0; i < 27; i++)
                {
                    _gridStatistics.Rows[i].Cells[2].Value = positionsLongState[i].ToString();
                }
            }
            if (positionsShortState != null)
            {
                for (int i = 0; i < 27; i++)
                {
                    _gridStatistics.Rows[i].Cells[3].Value = positionsShortState[i].ToString();
                }
            }
            if (positionsAllState != null)
            {
                for (int i = 0; i < 27; i++)
                {
                    _gridStatistics.Rows[i].Cells[1].Value = positionsAllState[i].ToString();
                }
            }
        }

// прорисовка профита

        /// <summary>
        /// чарт
        /// </summary>
        Chart _chartEquity;

        /// <summary>
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
                _chartEquity.Click += _chartEquity_Click;

                ChartArea areaLineProfit = new ChartArea("ChartAreaProfit");
                areaLineProfit.Position.Height = 70;
                areaLineProfit.Position.Width = 100;
                areaLineProfit.Position.Y = 0;
                areaLineProfit.CursorX.IsUserSelectionEnabled = false; // разрешаем пользователю изменять рамки представления
                areaLineProfit.CursorX.IsUserEnabled = true; //чертa

                _chartEquity.ChartAreas.Add(areaLineProfit);

                ChartArea areaLineProfitBar = new ChartArea("ChartAreaProfitBar");
                areaLineProfitBar.AlignWithChartArea = "ChartAreaProfit";
                areaLineProfitBar.Position.Height = 30;
                areaLineProfitBar.Position.Width = 100;
                areaLineProfitBar.Position.Y = 70;
                areaLineProfitBar.AxisX.Enabled = AxisEnabled.False;
                areaLineProfitBar.CursorX.IsUserEnabled = true; //чертa

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

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// прорисовать чарт объёмов
        /// </summary>
        private void PaintProfitOnChart(List<Position> positionsAll)
        {
            if (!TabBots.Dispatcher.CheckAccess())
            {
                TabBots.Dispatcher.Invoke(
                    new Action<List<Position>>(PaintProfitOnChart), positionsAll);
                return;
            }

            _chartEquity.Series.Clear();

            Series profit = new Series("SeriesProfit");
            profit.ChartType = SeriesChartType.Line;
            profit.Color = Color.White;   //DeepSkyBlue;
            profit.YAxisType = AxisType.Secondary;
            profit.ChartArea = "ChartAreaProfit";
            profit.BorderWidth = 4;
            profit.ShadowOffset = 2;

            Series profitLong = new Series("SeriesProfitLong");
            profitLong.ChartType = SeriesChartType.Line;
            profitLong.Color = Color.DeepSkyBlue;   //DeepSkyBlue;
            profitLong.YAxisType = AxisType.Secondary;
            profitLong.ChartArea = "ChartAreaProfit";
            profitLong.BorderWidth = 2;
            profitLong.ShadowOffset = 2;

            Series profitShort = new Series("SeriesProfitShort");
            profitShort.ChartType = SeriesChartType.Line;
            profitShort.Color = Color.DarkOrange;  //DeepSkyBlue;
            profitShort.YAxisType = AxisType.Secondary;
            profitShort.ChartArea = "ChartAreaProfit";
            profitShort.ShadowOffset = 2;
            profitShort.BorderWidth = 2;

            Series profitBar = new Series("SeriesProfitBar");
            profitBar.ChartType = SeriesChartType.Column;
            profitBar.YAxisType = AxisType.Secondary;
            profitBar.ChartArea = "ChartAreaProfitBar";
            profitBar.ShadowOffset = 2;
           

            try
            {
                /*

                */

                decimal profitSum = 0;
                decimal profitSumLong = 0;
                decimal profitSumShort = 0;
                //Side d = new Side();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    profitSum += positionsAll[i].ProfitPortfolioPunkt;
                    profit.Points.AddXY(i, profitSum);

                    profit.Points[profit.Points.Count - 1].AxisLabel =
                        positionsAll[i].TimeCreate.ToString(new CultureInfo("ru-RU"));

                    profitBar.Points.AddXY(i, positionsAll[i].ProfitPortfolioPunkt);

                    if (positionsAll[i].Direction == Side.Buy)
                    {
                        profitSumLong += positionsAll[i].ProfitPortfolioPunkt;
                    }

                    if (positionsAll[i].Direction == Side.Sell)
                    {
                        profitSumShort += positionsAll[i].ProfitPortfolioPunkt;
                    }

                    profitLong.Points.AddXY(i, profitSumLong);
                    profitShort.Points.AddXY(i, profitSumShort);

                    if (positionsAll[i].ProfitPortfolioPunkt > 0)
                    {
                        profitBar.Points[profitBar.Points.Count - 1].Color = Color.Gainsboro;
                    }
                    else
                    {
                        profitBar.Points[profitBar.Points.Count - 1].Color = Color.DarkOrange;
                    }

                }

                _chartEquity.Series.Add(profit);
                _chartEquity.Series.Add(profitLong);
                _chartEquity.Series.Add(profitShort);
                _chartEquity.Series.Add(profitBar);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// клик по чарту эквити
        /// </summary>
        void _chartEquity_Click(object sender, EventArgs e)
        {
            if (double.IsNaN(_chartEquity.ChartAreas[0].CursorX.Position) ||
                 _chartEquity.ChartAreas[0].CursorX.Position == 0)
            {
                return;
            }

            for (int i = 0; i < _chartEquity.Series.Count; i++)
            {
                if (_chartEquity.Series[i].Points.Count == 1)
                {
                    continue;
                }
                for (int i2 = 0; i2 < _chartEquity.Series[i].Points.Count; i2++)
                {
                    _chartEquity.Series[i].Points[i2].Label = "";
                }
            }

            for (int i = 0; i < _chartEquity.Series.Count; i++)
            {
                if (_chartEquity.Series[i].Points.Count == 1)
                {
                    continue;
                }
                string label = "";

                int index = Convert.ToInt32(_chartEquity.ChartAreas[0].CursorX.Position) - 1;

                if (index >= _chartEquity.Series[i].Points.Count)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_chartEquity.Series[i].Points[index].AxisLabel))
                {
                    label += _chartEquity.Series[i].Points[index].AxisLabel + "\n";
                }
                
                label += _chartEquity.Series[i].Points[index].YValues[0];

                _chartEquity.Series[i].Points[index].Label = label;
                _chartEquity.Series[i].Points[index].LabelForeColor = _chartEquity.Series[i].Points[index].Color;
                _chartEquity.Series[i].Points[index].LabelBackColor = Color.FromArgb(17, 18, 23);
            }
        }

// прорисовка объёма

        /// <summary>
        /// чарт для объёмов
        /// </summary>
        Chart _chartVolume;

        /// <summary>
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
        /// прорисовать чарт
        /// </summary>
        private void PaintVolumeOnChart(List<Position> positionsAll)
        {
            if (!TabBots.Dispatcher.CheckAccess())
            {
                TabBots.Dispatcher.Invoke(
                    new Action<List<Position>>(PaintVolumeOnChart), positionsAll);
                return;
            }

            _chartVolume.Series.Clear();
            _chartVolume.ChartAreas.Clear();

            // берём кол-во инструментов
            List<VolumeSecurity> volumes = new List<VolumeSecurity>();

            for (int i = 0; i < positionsAll.Count; i++)
            {
                if (volumes.Find(vol => vol.Security == positionsAll[i].SecurityName) == null)
                {
                    volumes.Add(new VolumeSecurity() {Security = positionsAll[i].SecurityName});
                }
            }

            if (volumes.Count == 0)
            {
                return;
            }

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
                    allChangeSort.Insert(0,allChange[i]);
                }
                else
                {
                    for (int i2 = 0; i2 < allChangeSort.Count; i2++)
                    {
                        if (allChangeSort[i2] <= allChange[i] &&
                            allChangeSort[i2 + 1] >= allChange[i])
                        {
                            allChangeSort.Insert(i2+1,allChange[i]);
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
                if(positionsAll[i].SecurityName == "si.txt")
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
                if (i%2 == 0)
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
        /// прорисовать объёмы по инструменту
        /// </summary>
        /// <param name="volume">массив</param>
        /// <param name="name">бумага</param>
        /// <param name="color">цвет серии</param>
        /// <param name="times">времена</param>
        private void PaintValuesVolume(List<decimal> volume, string name, Color color, List<DateTime> times )
        {
            if (volume == null ||
                volume.Count == 0)
            {
                return;
            }

            ChartArea areaLineSecurity = new ChartArea("ChartArea" + name);
            
            areaLineSecurity.CursorX.IsUserSelectionEnabled = false; // разрешаем пользователю изменять рамки представления
            areaLineSecurity.CursorX.IsUserEnabled = true; //чертa

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
                times[i].ToString(new CultureInfo("ru-RU"));
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

            int interval = Convert.ToInt32(Math.Abs(maxVolume - minVolume)/8);

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

// прорисовка просадки

        /// <summary>
        /// чарт для просадки
        /// </summary>
        Chart _chartDd;

        /// <summary>
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
                _chartDd.Click += _chartDd_Click;

                ChartArea areaDdPunct = new ChartArea("ChartAreaDdPunct");
                areaDdPunct.Position.Height = 50;
                areaDdPunct.Position.Width = 100;
                areaDdPunct.Position.Y = 0;
                areaDdPunct.CursorX.IsUserSelectionEnabled = false; // разрешаем пользователю изменять рамки представления
                areaDdPunct.CursorX.IsUserEnabled = true; //чертa

                _chartDd.ChartAreas.Add(areaDdPunct);

                ChartArea areaDdPersent = new ChartArea("ChartAreaDdPersent");
                areaDdPersent.AlignWithChartArea = "ChartAreaDdPunct";
                areaDdPersent.Position.Height = 50;
                areaDdPersent.Position.Width = 100;
                areaDdPersent.Position.Y = 50;
                areaDdPersent.AxisX.Enabled = AxisEnabled.False;
                areaDdPersent.CursorX.IsUserEnabled = true; //чертa

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
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// прорисовать чарт для просадки
        /// </summary>
        /// <param name="positionsAll"></param>
        private void PaintDrowDown(List<Position> positionsAll)
        {
             _chartDd.Series.Clear();

            if (positionsAll.Count == 0)
            {
                return;
            }

            List<decimal> ddPunct = new decimal[positionsAll.Count].ToList();
            decimal lastMax = 0;
            decimal currentProfit = 0;

            for (int i = 0; i < positionsAll.Count; i++)
            {
                currentProfit += positionsAll[i].ProfitPortfolioPunkt;

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
            drowDownPunct.Color = Color.DeepSkyBlue;   //DeepSkyBlue;
            drowDownPunct.YAxisType = AxisType.Secondary;
            drowDownPunct.ChartArea = "ChartAreaDdPunct";
            drowDownPunct.BorderWidth = 2;
            drowDownPunct.ShadowOffset = 2;

            for (int i = 0; i < ddPunct.Count; i++)
            {
                drowDownPunct.Points.Add(Convert.ToDouble(ddPunct[i]));
                drowDownPunct.Points[drowDownPunct.Points.Count - 1].AxisLabel =
               positionsAll[i].TimeCreate.ToString(new CultureInfo("ru-RU"));
            }

            _chartDd.Series.Add(drowDownPunct);

            // дд в %

            List<decimal> ddPepcent = new decimal[positionsAll.Count].ToList();
            lastMax = 0;
            currentProfit = 0;

            for (int i = 0; i < positionsAll.Count; i++)
            {
                currentProfit += positionsAll[i].ProfitPortfolioPersent;

                if (lastMax < currentProfit)
                {
                    lastMax = currentProfit;
                }

                if (currentProfit - lastMax < 0)
                {
                    ddPepcent[i] = Math.Round(currentProfit - lastMax,4);
                }
            }

            Series drowDownPersent = new Series("SeriesDdPercent");
            drowDownPersent.ChartType = SeriesChartType.Line;
            drowDownPersent.Color = Color.DarkOrange;  
            drowDownPersent.YAxisType = AxisType.Secondary;
            drowDownPersent.ChartArea = "ChartAreaDdPersent";
            drowDownPersent.BorderWidth = 2;
            drowDownPersent.ShadowOffset = 2;

            for (int i = 0; i < ddPepcent.Count; i++)
            {
                drowDownPersent.Points.Add(Convert.ToDouble(ddPepcent[i]));
                drowDownPersent.Points[drowDownPersent.Points.Count - 1].AxisLabel =
               positionsAll[i].TimeCreate.ToString(new CultureInfo("ru-RU"));
            }

            _chartDd.Series.Add(drowDownPersent);
        }

        /// <summary>
        /// клик по чарту с макс просадкой
        /// </summary>
        void _chartDd_Click(object sender, EventArgs e)
        {
            if (double.IsNaN(_chartDd.ChartAreas[0].CursorX.Position) ||
                _chartDd.ChartAreas[0].CursorX.Position == 0)
            {
                return;
            }

            for (int i = 0; i < _chartDd.Series.Count; i++)
            {
                if (_chartDd.Series[i].Points.Count == 1)
                {
                    continue;
                }
                for (int i2 = 0; i2 < _chartDd.Series[i].Points.Count; i2++)
                {
                    _chartDd.Series[i].Points[i2].Label = "";
                }
            }

            for (int i = 0; i < _chartDd.Series.Count; i++)
            {
                if (_chartDd.Series[i].Points.Count == 1)
                {
                    continue;
                }
                string label = "";

                int index = Convert.ToInt32(_chartDd.ChartAreas[0].CursorX.Position) - 1;

                if (index >= _chartDd.Series[i].Points.Count)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_chartDd.Series[i].Points[index].AxisLabel))
                {
                    label += _chartDd.Series[i].Points[index].AxisLabel + "\n";
                }

                label += _chartDd.Series[i].Points[index].YValues[0];

                _chartDd.Series[i].Points[index].Label = label;
                _chartDd.Series[i].Points[index].LabelForeColor = _chartDd.Series[i].Points[index].Color;
                _chartDd.Series[i].Points[index].LabelBackColor = Color.Black;
            }
        }

// позиции

        /// <summary>
        /// таблица открытых позиций
        /// </summary>
        private DataGridView _openPositionGrid;

        /// <summary>
        /// таблица закрытых позиций
        /// </summary>
        private DataGridView _closePositionGrid;

        /// <summary>
        /// создать таблицы для позиций
        /// </summary>
        private void CreatPositionTables()
        {
            _openPositionGrid = CreateNewTable();
            HostOpenPosition.Child = _openPositionGrid;
            _openPositionGrid.Click += _openPositionGrid_Click;
            _openPositionGrid.DoubleClick += _openPositionGrid_DoubleClick;

            _closePositionGrid = CreateNewTable();
            HostClosePosition.Child = _closePositionGrid;
            _closePositionGrid.Click += _closePositionGrid_Click;
            _closePositionGrid.DoubleClick += _closePositionGrid_DoubleClick;
        }

        /// <summary>
        /// создать таблицу
        /// </summary>
        /// <returns>таблица для прорисовки на ней позиций</returns>
        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);
                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = newGrid.DefaultCellStyle;
                newGrid.ForeColor = Color.Black;

                DataGridViewColumn colum0 = new DataGridViewColumn();
                colum0.CellTemplate = cell0;
                colum0.HeaderText = @"Номер";
                colum0.ReadOnly = true;
                colum0.Width = 50;
                newGrid.Columns.Add(colum0);

                DataGridViewColumn colum01 = new DataGridViewColumn();
                colum01.CellTemplate = cell0;
                colum01.HeaderText = @"Время отк.";
                colum01.ReadOnly = true;
                colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                newGrid.Columns.Add(colum01);

                DataGridViewColumn colum02 = new DataGridViewColumn();
                colum02.CellTemplate = cell0;
                colum02.HeaderText = @"Время зак.";
                colum02.ReadOnly = true;
                colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                newGrid.Columns.Add(colum02);

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
                colum3.HeaderText = @"Cостояние";
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
                colum6.HeaderText = @"Цена входа";
                colum6.ReadOnly = true;
                colum6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum6);

                DataGridViewColumn colum61 = new DataGridViewColumn();
                colum61.CellTemplate = cell0;
                colum61.HeaderText = @"Цена выхода";
                colum61.ReadOnly = true;
                colum61.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                newGrid.Columns.Add(colum61);

                DataGridViewColumn colum8 = new DataGridViewColumn();
                colum8.CellTemplate = cell0;
                colum8.HeaderText = @"Прибыль";
                colum8.ReadOnly = true;
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
                nRow.Cells[10].Value = position.EntryPrice;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[11].Value = position.ClosePrice;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[12].Value = position.ProfitPortfolioPunkt;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[13].Value = position.StopOrderRedLine;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[14].Value = position.StopOrderPrice;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[15].Value = position.ProfitOrderRedLine;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[16].Value = position.ProfitOrderPrice;

                return nRow;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
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
                MenuItem[] items = new MenuItem[3];

                items[0] = new MenuItem { Text = @"Детализация" };
                items[0].Click += OpenDealMoreInfo_Click;

                items[1] = new MenuItem { Text = @"Удалить" };
                items[1].Click += OpenDealDelete_Click;

                items[2] = new MenuItem { Text = @"Очистить всё" };
                items[2].Click += OpenDealClearAll_Click;

                ContextMenu menu = new ContextMenu(items);

                _openPositionGrid.ContextMenu = menu;
                _openPositionGrid.ContextMenu.Show(_openPositionGrid, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
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
        /// удалить позицию
        /// </summary>
        void OpenDealDelete_Click(object sender, EventArgs e)
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

            DeletePosition(number);

            RePaint();
        }

        /// <summary>
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

            for (int i = 0; i < numbers.Count; i++)
            {
                DeletePosition(numbers[i]);
            }
            RePaint();
        }

        /// <summary>
        /// прорисовать открытые позиции на таблице
        /// </summary>
        private void PaintOpenPositionGrid(List<Position> positionsAll)
        {
            _openPositionGrid.Rows.Clear();

            for (int i = 0; i < positionsAll.Count; i++)
            {
                if (positionsAll[i].State != PositionStateType.Done)
                {
                    _openPositionGrid.Rows.Insert(0, GetRow(positionsAll[i]));
                }
            }
        }

// позиции закрытые

        /// <summary>
        /// двойной клик по таблице закрытых седлок
        /// </summary>
        void _closePositionGrid_DoubleClick(object sender, EventArgs e)
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

                items[0] = new MenuItem { Text = @"Детализация" };
                items[0].Click += CloseDealMoreInfo_Click;

                items[1] = new MenuItem { Text = @"Удалить" };
                items[1].Click += CloseDealDelete_Click;

                items[2] = new MenuItem { Text = @"Очистить всё" };
                items[2].Click += CloseDealClearAll_Click;

                items[3] = new MenuItem { Text = @"Сохранить в файл" };
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
                    System.Windows.Forms.MessageBox.Show(@"Необходимо выбрать файл для сохранения!");
                    return;
                }

                StringBuilder workSheet = new StringBuilder();
                workSheet.Append("Номер;");
                workSheet.Append("Время отк.;");
                workSheet.Append("Время зак.;");
                workSheet.Append("Бот;");
                workSheet.Append("Инструмент;");
                workSheet.Append("Напр.;");
                workSheet.Append("Cостояние;");
                workSheet.Append("Объём;");
                workSheet.Append("Текущий;");
                workSheet.Append("Ожидает;");
                workSheet.Append("Цена входа;");
                workSheet.Append("Цена выхода;");
                workSheet.Append("Прибыль;");
                workSheet.Append("СтопАктивация;");
                workSheet.Append("СтопЦена;");
                workSheet.Append("ПрофитАктивация;");
                workSheet.Append("ПрофитЦена\r\n");

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
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[16].Value + "\r\n");
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
                SendNewLogMessage(error.ToString(),LogMessageType.Error);
            }
           

        }

        /// <summary>
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

            DeletePosition(number);

            RePaint();
        }

        /// <summary>
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

            }
            catch (Exception)
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
        /// прорисовать закрытые позиции на таблице
        /// </summary>
        private void PaintClosePositionGrid(List<Position> positionsAll)
        {
            _closePositionGrid.Rows.Clear();

            for (int i = 0; i < positionsAll.Count; i++)
            {
                if (positionsAll[i].State == PositionStateType.Done)
                {
                    _closePositionGrid.Rows.Insert(0, GetRow(positionsAll[i]));
                }
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
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    /// <summary>
    /// класс хранилище журналов
    /// </summary>
    public class BotPanelJournal
    {
        public string BotName;

        public List<BotTabJournal> _Tabs;
    }

    /// <summary>
    /// класс хранилище журналов
    /// </summary>
    public class BotTabJournal
    {
        public int TabNum;

        public Journal Journal;
    }

    /// <summary>
    /// класс модификации ячеек DataGridView. позволяет работать с толщиной границы ячейки
    /// </summary>
    internal class CustomDataGridViewCell : DataGridViewTextBoxCell
    {
        private DataGridViewAdvancedBorderStyle _style;

        public CustomDataGridViewCell()
            : base()
        {
            _style = new DataGridViewAdvancedBorderStyle();
            _style.Bottom = DataGridViewAdvancedCellBorderStyle.None;
            _style.Top = DataGridViewAdvancedCellBorderStyle.None;
            _style.Left = DataGridViewAdvancedCellBorderStyle.None;
            _style.Right = DataGridViewAdvancedCellBorderStyle.None;
        }

        public DataGridViewAdvancedBorderStyle AdvancedBorderStyle
        {
            get { return _style; }
            set
            {
                _style.Bottom = value.Bottom;
                _style.Top = value.Top;
                _style.Left = value.Left;
                _style.Right = value.Right;
            }
        }

        protected override void PaintBorder(Graphics graphics, Rectangle clipBounds, Rectangle bounds,
            DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle)
        {
            base.PaintBorder(graphics, clipBounds, bounds, cellStyle, _style);
        }

        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
            DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
            DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText,
                cellStyle, _style, paintParts);
        }

    }

    /// <summary>
    /// объём во времени по инструменту
    /// </summary>
    public class VolumeSecurity
    {
        /// <summary>
        /// массив данных по объёму во времени
        /// </summary>
        public List<decimal> Volume;

        /// <summary>
        /// название бумаги
        /// </summary>
        public string Security;
    }
}

