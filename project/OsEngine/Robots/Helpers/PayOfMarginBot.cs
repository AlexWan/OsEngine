/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Journal;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels.Tab.Internal;
using OsEngine.Market.Connectors;
using OsEngine.Language;
using System.Globalization;
using OsEngine.Market.Servers.Tester;
using OsEngine.Market.Servers;
using OsEngine.Market;
using System.ComponentModel.Design;

/* Description
Робот работает только в Тестере.
Робот предназначен для ежедневного списывания маржинальной комиссии, если сумма взятых ордеров превышает размеры депозита.

The bot only works in the Tester.
The bot is designed to charge a margin commission daily if the total amount of accepted orders exceeds the deposit amount.
 */

namespace OsEngine.Robots.Helpers
{
    [Bot("PayOfMarginBot")]
    public class PayOfMarginBot : BotPanel
    {
        private BotTabSimple _tab;
        private StrategyParameterString _regime;
        private StartProgram _startProgram;
        List<BotPanel> _bots;
        private StrategyParameterBool _fullLogIsOn;

        public PayOfMarginBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            if (startProgram != StartProgram.IsTester)
            {
                string message = OsLocalization.ConvertToLocString("Eng:The bot only works in the Tester._" + "Ru:Бот работает только в Тестере_");

                SendNewLogMessage(message, Logging.LogMessageType.Error);
                return;
            }

            _startProgram = startProgram;

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            string tabName = " Main ";

            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "Summ", "Percent" }, tabName);

            _fullLogIsOn = CreateParameter("Full log is on", false, tabName);

            CustomTabToParametersUi customTabSumm = ParamGuiSettings.CreateCustomTab(" Summ ");

            CreateTableSumm();

            if(_dgvSumm != null)
            {
                customTabSumm.AddChildren(_hostSumm);
                LoadTableSumm();

                CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab(" Percent ");

                CreateTable();
                customTab.AddChildren(_host);
                LoadTable();

                _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

                if (StartProgram == StartProgram.IsTester
                    && ServerMaster.GetServers() != null)
                {
                    List<IServer> servers = ServerMaster.GetServers();

                    if (servers != null
                        && servers.Count > 0
                        && servers[0].ServerType == ServerType.Tester)
                    {
                        TesterServer server = (TesterServer)servers[0];
                        server.TestingStartEvent += Server_TestingStartEvent;
                    }
                }
            }

            Description = OsLocalization.ConvertToLocString(
                "En:The bot only works in the Tester. The bot is designed to charge a margin commission daily if the total amount of accepted orders exceeds the deposit amount._" +
                "Ru:Робот работает только в Тестере. Робот предназначен для ежедневного списания маржинальной комиссии, если сумма взятых позиций превышает размеры депозита._");
        }

        private void Server_TestingStartEvent()
        {
            _bots = OsTraderMaster.Master.PanelsArray;
        }

        #region Table Summ

        private WindowsFormsHost _hostSumm;

        private DataGridView _dgvSumm;

        private List<ListTableSumm> _listTableSumm = new();

        private List<TypeValue> _listTypeValue = new() { TypeValue.Absolute, TypeValue.Percent };

        private void CreateTableSumm()
        {
            try
            {
                _hostSumm = new WindowsFormsHost();

                _dgvSumm = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

                _dgvSumm.Dock = DockStyle.Fill;
                _dgvSumm.ScrollBars = ScrollBars.Vertical;
                _dgvSumm.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
                _dgvSumm.GridColor = Color.Gray;
                _dgvSumm.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                _dgvSumm.ColumnHeadersDefaultCellStyle.Font = new Font(_dgvSumm.Font, FontStyle.Bold | FontStyle.Italic);
                _dgvSumm.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                _dgvSumm.ColumnCount = 3;
                _dgvSumm.RowCount = 0;

                _dgvSumm.Columns[0].HeaderText = OsLocalization.ConvertToLocString("Eng:Amount margin_" + "Ru:Сумма непокрытой позиции_");
                _dgvSumm.Columns[1].HeaderText = OsLocalization.ConvertToLocString("Eng:Type rate_" + "Ru:Вид ставки_");
                _dgvSumm.Columns[2].HeaderText = OsLocalization.ConvertToLocString("Eng:Rate_" + "Ru:Ставка_");

                _dgvSumm.Columns[0].ReadOnly = true;
                _dgvSumm.Columns[1].ReadOnly = true;

                _dgvSumm.Columns[0].Width = 250;
                _dgvSumm.Columns[1].Width = 100;
                _dgvSumm.Columns[2].Width = 100;

                foreach (DataGridViewColumn column in _dgvSumm.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                _dgvSumm.CellValueChanged += _dgvSumm_CellValueChanged;
                _dgvSumm.DataError += _dgv_DataError;

                _hostSumm.Child = _dgvSumm;
            }
            catch (Exception ex)
            {
                //SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }
     
        private void _dgvSumm_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.ColumnIndex == 2)
                {
                    ListTableSumm list = new();
                    
                    list.Summ = _listTableSumm[e.RowIndex].Summ;
                    list.TypeValue = _dgvSumm.Rows[e.RowIndex].Cells[1].Value?.ToString() == TypeValue.Absolute.ToString() ? TypeValue.Absolute : TypeValue.Percent;
                    decimal.TryParse(_dgvSumm.Rows[e.RowIndex].Cells[2].Value?.ToString(), out list.Rate);

                    _listTableSumm[e.RowIndex] = list;

                    SaveTableSumm();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void LoadTableSumm()
        {
            try
            {
                string fileName = @"Engine\" + NameStrategyUniq + @"TableSumm.json";

                if (!File.Exists(fileName))
                {
                    SetDefaultTableSumm();
                    return;
                }

                string json = File.ReadAllText(fileName);
                _listTableSumm = JsonConvert.DeserializeObject<List<ListTableSumm>>(json);

                if (_listTableSumm == null || _listTableSumm.Count == 0)
                {
                    SetDefaultTableSumm();
                    return;
                }

                FillTableSumm();                
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void FillTableSumm()
        {
            for (int i = 0; i < 12; i++)
            {
                DataGridViewRow row = new();

                if (i == 11)
                {
                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = $"более {_listTableSumm[i - 1].Summ.ToString("N0", new CultureInfo("ru-RU"))} Р" });
                }
                else
                {
                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = $"до {_listTableSumm[i].Summ.ToString("N0", new CultureInfo("ru-RU"))} Р" });
                }

                row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTableSumm[i].TypeValue });
                row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTableSumm[i].Rate });

                _dgvSumm.Rows.Add(row);
            }
        }

        private void SetDefaultTableSumm()
        {
            _listTableSumm.Clear();

            _listTableSumm.Add(new ListTableSumm() { Summ = 5000, TypeValue = TypeValue.Absolute, Rate = 0 });
            _listTableSumm.Add(new ListTableSumm() { Summ = 50000, TypeValue = TypeValue.Absolute, Rate = 45 });
            _listTableSumm.Add(new ListTableSumm() { Summ = 100000, TypeValue = TypeValue.Absolute, Rate = 90 });
            _listTableSumm.Add(new ListTableSumm() { Summ = 250000, TypeValue = TypeValue.Absolute, Rate = 215 });
            _listTableSumm.Add(new ListTableSumm() { Summ = 500000, TypeValue = TypeValue.Absolute, Rate = 430 });
            _listTableSumm.Add(new ListTableSumm() { Summ = 1000000, TypeValue = TypeValue.Absolute, Rate = 850 });
            _listTableSumm.Add(new ListTableSumm() { Summ = 2500000, TypeValue = TypeValue.Absolute, Rate = 2100 });
            _listTableSumm.Add(new ListTableSumm() { Summ = 5000000, TypeValue = TypeValue.Absolute, Rate = 4100 });
            _listTableSumm.Add(new ListTableSumm() { Summ = 10000000, TypeValue = TypeValue.Absolute, Rate = 8000 });
            _listTableSumm.Add(new ListTableSumm() { Summ = 25000000, TypeValue = TypeValue.Percent, Rate = 0.078m });
            _listTableSumm.Add(new ListTableSumm() { Summ = 50000000, TypeValue = TypeValue.Percent, Rate = 0.075m });
            _listTableSumm.Add(new ListTableSumm() { Summ = 60000000, TypeValue = TypeValue.Percent, Rate = 0.067m });

            FillTableSumm();
        }

        private void SaveTableSumm()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_listTableSumm, Formatting.Indented);
                File.WriteAllText(@"Engine\" + NameStrategyUniq + @"TableSumm.json", json);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private class ListTableSumm
        {
            public int Summ;
            public TypeValue TypeValue;
            public decimal Rate;
        }

        private enum TypeValue
        {
            Absolute,
            Percent
        }

        #endregion

        #region Table Percent

        private WindowsFormsHost _host;

        private DataGridView _dgv;

        private List<ListTablePeriods> _listTable = new();

        private void CreateTable()
        {
            try
            {
                _host = new WindowsFormsHost();

                _dgv = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells);

                _dgv.Dock = DockStyle.Fill;
                _dgv.ScrollBars = ScrollBars.Vertical;
                _dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
                _dgv.GridColor = Color.Gray;
                _dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
                _dgv.ColumnHeadersDefaultCellStyle.Font = new Font(_dgv.Font, FontStyle.Bold | FontStyle.Italic);
                _dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                _dgv.ColumnCount = 3;
                _dgv.RowCount = 1;

                _dgv.Columns[0].HeaderText = OsLocalization.ConvertToLocString("Eng:Year_" + "Ru:Год_");
                _dgv.Columns[1].HeaderText = OsLocalization.ConvertToLocString("Eng:Rate_" + "Ru:Ставка_");

                DataGridViewButtonCell cellButton = new();

                _dgv.Rows[^1].Cells[0] = cellButton;
                _dgv.Rows[^1].Cells[0].Value = OsLocalization.ConvertToLocString("Eng:Add row_" + "Ru:Добавить строку_");
                _dgv.Rows[^1].Cells[0].ReadOnly = true;
                _dgv.Rows[^1].Cells[1].ReadOnly = true;
                _dgv.Rows[^1].Cells[2].ReadOnly = true;

                foreach (DataGridViewColumn column in _dgv.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                _dgv.CellClick += _dgv_CellClick;
                _dgv.CellValueChanged += _dgv_CellValueChanged;
                _dgv.DataError += _dgv_DataError;

                _host.Child = _dgv;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _dgv_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {

        }

        private void _dgv_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex == _dgv.RowCount - 1 && e.ColumnIndex == 0)
                {
                    AddRow();
                }

                if (e.ColumnIndex == 2)
                {
                    if (e.RowIndex > -1 && e.RowIndex < _dgv.RowCount - 1)
                    {
                        DeleteRow(e.RowIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void AddRow()
        {
            DataGridViewButtonCell cellButton = new();
            _dgv.Rows.Insert(_dgv.RowCount - 1);

            _dgv.Rows[^2].Cells[2] = cellButton;
            _dgv.Rows[^2].Cells[2].Value = OsLocalization.ConvertToLocString("Eng:Delete row_" + "Ru:Удалить строку_");
            _dgv.Rows[^2].Cells[2].ReadOnly = true;

            SaveTable();
        }

        private void DeleteRow(int rowIndex)
        {
            int year = 0;

            if (int.TryParse(_dgv[0, rowIndex].Value?.ToString(), out year))
            {
                int deleteIndex = _listTable.FindIndex(x => x.Year == year);

                if (deleteIndex > -1)
                {
                    _listTable.RemoveAt(deleteIndex);
                }
            }

            _dgv.Rows.RemoveAt(rowIndex);

            SaveTable();
        }

        private void _dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex != _dgv.RowCount - 1 && e.ColumnIndex != 2)
                {
                    int year = 0;
                    int.TryParse(_dgv.Rows[e.RowIndex].Cells[0].Value?.ToString(), out year);

                    if (year == 0)
                    {
                        return;
                    }

                    decimal rate = 0;
                    decimal.TryParse(_dgv.Rows[e.RowIndex].Cells[1].Value?.ToString().Replace(".", ","), out rate);

                    ListTablePeriods list = new();
                    list.Year = year;
                    list.Rate = rate;

                    for (int i = 0; i < _dgv.RowCount - 1; i++)
                    {
                        int valueYear = 0;
                        int.TryParse(_dgv.Rows[i].Cells[0].Value?.ToString(), out valueYear);

                        if (valueYear == year)
                        {
                            if (i == e.RowIndex)
                            {
                                int index = _listTable.FindIndex(x => x.Year == year);

                                if (index > -1)
                                {
                                    _listTable[index] = list;
                                }
                                else
                                {
                                    _listTable.Add(list);
                                }

                                SaveTable();
                            }
                            else
                            {
                                _dgv.Rows[e.RowIndex].Cells[0].Value = "";

                                string message = OsLocalization.ConvertToLocString("Eng:There is already such a year in the table._" + "Ru:В таблице уже есть такой год._");
                                SendNewLogMessage(message, Logging.LogMessageType.Error);
                            }
                        }
                    }

                    for (int i = 0; i < _listTable.Count; i++)
                    {
                        int count = 0;

                        for (int j = 0; j < _dgv.RowCount - 1; j++)
                        {
                            int valueYear = 0;
                            int.TryParse(_dgv.Rows[j].Cells[0].Value?.ToString(), out valueYear);

                            if (_listTable[i].Year == valueYear)
                            {
                                count++;
                                break;
                            }
                        }

                        if (count == 0)
                        {
                            _listTable.RemoveAt(i);
                            i--;
                            SaveTable();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void LoadTable()
        {
            try
            {
                string fileName = @"Engine\" + NameStrategyUniq + @"TablePeriod.json";

                if (!File.Exists(fileName))
                {
                    SetDefaultTablePeriods();
                    return;
                }

                string json = File.ReadAllText(fileName);
                _listTable = JsonConvert.DeserializeObject<List<ListTablePeriods>>(json);

                if (_listTable == null || _listTable.Count == 0)
                {
                    SetDefaultTablePeriods();
                    return;
                }

                for (int i = 0; i < _listTable.Count; i++)
                {
                    DataGridViewRow row = new();
                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTable[i].Year });
                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTable[i].Rate });
                    row.Cells.Add(new DataGridViewButtonCell() { Value = OsLocalization.ConvertToLocString("Eng:Delete row_" + "Ru:Удалить строку_") });

                    _dgv.Rows.Insert(_dgv.RowCount - 1, row);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void SetDefaultTablePeriods()
        {
            _listTable.Clear();

            for (int i = 0; i < 31; i++)
            {
                DataGridViewRow row = new();
                row.Cells.Add(new DataGridViewTextBoxCell() { Value = 2000 + i });
                row.Cells.Add(new DataGridViewTextBoxCell() { Value = 13 });
                row.Cells.Add(new DataGridViewButtonCell() { Value = OsLocalization.ConvertToLocString("Eng:Delete row_" + "Ru:Удалить строку_") });

                _dgv.Rows.Insert(_dgv.RowCount - 1, row);

                _listTable.Add(new ListTablePeriods() { Year = 2000 + i, Rate = 13 });
            }
        }

        private void SaveTable()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_listTable, Formatting.Indented);
                File.WriteAllText(@"Engine\" + NameStrategyUniq + @"TablePeriod.json", json);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private class ListTablePeriods
        {
            public int Year;
            public decimal Rate;
        }

        #endregion

        #region Main Logic

        private void _tab_CandleFinishedEvent(List<Candle> candle)
        {
            try
            {
                if (_startProgram != StartProgram.IsTester)
                {
                    return;
                }

                if (_regime == "Off")
                {
                    return;
                }

                if (candle.Count == 0)
                {
                    return;
                }

                if (candle.Count < 2)
                {
                    MainLogic(candle[^1].TimeStart);
                    return;
                }

                if (candle[^1].TimeStart.Day > candle[^2].TimeStart.Day)
                {
                    MainLogic(candle[^2].TimeStart);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void MainLogic(DateTime timeStart)
        {
            if (_fullLogIsOn.ValueBool == true)
            {
                SendNewLogMessage("Logic entry. Date: " + timeStart.ToString("dd.MM.yyyy"), Logging.LogMessageType.System);
            }

            decimal volume = 0;
            decimal deposit = 0;
            DateTime timeDeposit = DateTime.MinValue;

            BotPanel taxBot = null;

            for (int i = 0; i < _bots.Count; i++)
            {
                if (_bots[i].GetNameStrategyType() == "PayOfMarginBot")
                {
                    taxBot = _bots[i];
                    continue;
                }

                if (!_bots[i].OnOffEventsInTabs)
                {
                    continue;
                }

                List<Journal.Journal> journals = _bots[i].GetJournals();

                decimal volumeBot = 0;

                for (int j = 0; j < journals.Count; j++)
                {
                    Journal.Journal curJournal = journals[j];

                    for (int i2 = 0; i2 < curJournal.OpenPositions.Count; i2++)
                    {
                        Position position = curJournal.OpenPositions[i2];

                        if(position.Lots != 0)
                        {
                            volumeBot += position.EntryPrice * position.OpenVolume * position.Lots;
                        }
                        else
                        {
                            volumeBot += position.EntryPrice * position.OpenVolume;
                        }

                        DateTime time = _bots[i].OpenPositions[^1].TimeOpen;

                        if (timeDeposit < time)
                        {
                            deposit = _bots[i].OpenPositions[^1].PortfolioValueOnOpenPosition;
                            timeDeposit = time;
                        }
                    }
                }

                volume += volumeBot;               
            }

            if (volume > deposit)
            {
                TaxDeal(taxBot, timeStart, Math.Round(volume - deposit, 2));
            }
            else
            {
                if (_fullLogIsOn.ValueBool == true)
                {
                    SendNewLogMessage("No margin. Date: " + timeStart.ToString("dd.MM.yyyy") + " Amount positions: " + volume + " Deposit: " + deposit, Logging.LogMessageType.System);
                }
            }
        }

        private void TaxDeal(BotPanel taxBot, DateTime timeStart, decimal margin)
        {
            if (taxBot == null)
            {
                return;
            }

            decimal marginComission = GetMarginComission(margin, timeStart);

            if (marginComission == 0)
            {
                return;
            }

            Security security = new();
            security.Name = "Margin";
            security.NameClass = "TestClass";

            ConnectorCandles connector = taxBot.TabsSimple[0].Connector;
            Portfolio portfolio = taxBot.TabsSimple[0].Portfolio;
            BotManualControl manualPositionSupport = taxBot.TabsSimple[0].ManualPositionSupport;
            Journal.Journal journal = taxBot.TabsSimple[0].GetJournal();

            PositionCreator _dealCreator = new PositionCreator();

            Position newDeal = _dealCreator.CreatePosition(
               taxBot.NameStrategyUniq, Side.Buy, 2, marginComission,
               OrderPriceType.Limit, manualPositionSupport.SecondToOpen,
               security, portfolio, _startProgram,
               manualPositionSupport.OrderTypeTime,
               manualPositionSupport.LimitsMakerOnly);

            newDeal.NameBotClass = this._tab.BotClassName;

            journal.SetNewDeal(newDeal);

            taxBot.TabsSimple[0].OrderFakeExecute(newDeal.OpenOrders[0], new DateTime(timeStart.Year, timeStart.Month, timeStart.Day, 23, 59, 58));

            Position position = taxBot.TabsSimple[0].PositionsLast;

            Order closeOrder
                = _dealCreator.CreateCloseOrderForDeal(security, position, 1,
                OrderPriceType.Limit, new TimeSpan(1, 1, 1, 1),
                StartProgram, manualPositionSupport.OrderTypeTime,
                connector.ServerFullName, manualPositionSupport.LimitsMakerOnly);

            closeOrder.PortfolioNumber = portfolio.Number;
            closeOrder.Volume = marginComission;

            position.AddNewCloseOrder(closeOrder);

            taxBot.TabsSimple[0].OrderFakeExecute(closeOrder, new DateTime(timeStart.Year, timeStart.Month, timeStart.Day, 23, 59, 59));
        }

        private decimal GetMarginComission(decimal margin, DateTime timeStart)
        {
            try
            {
                decimal rate = 0;
                TypeValue typeValue = TypeValue.Absolute;

                if (_regime == "Summ")
                {
                    for (int i = 0; i < _listTableSumm.Count; i++)
                    {
                        if (_listTableSumm[i].Summ > margin && i == 0)
                        {
                            rate = _listTableSumm[i].Rate;
                            typeValue = _listTableSumm[i].TypeValue;
                            break;
                        }

                        if(i > 0 && _listTableSumm[i - 1].Summ <= margin && _listTableSumm[i].Summ > margin)
                        {
                            rate = _listTableSumm[i].Rate;
                            typeValue = _listTableSumm[i].TypeValue;
                            break;
                        }

                        if (i == 11)
                        {
                            rate = _listTableSumm[^1].Rate;
                            typeValue = _listTableSumm[^1].TypeValue;
                            break;
                        }
                    }

                    if (rate <= 0)
                    {
                        if (_fullLogIsOn.ValueBool == true)
                        {
                            SendNewLogMessage("No Rate. Date: " + timeStart.ToString("dd.MM.yyyy") + " Rate: " + rate, Logging.LogMessageType.System);
                        }

                        return 0;
                    }

                    decimal marginComission = rate;

                    if (typeValue == TypeValue.Percent)
                    {
                        marginComission = Math.Round(margin * rate / 100, 2);
                    }

                    if (_fullLogIsOn.ValueBool == true)
                    {
                        SendNewLogMessage($"Date: {timeStart.ToString("dd.MM.yyyy")}, Margin: {margin}, Rate: {rate}, TypeRate: {typeValue}, Comission: {marginComission}", LogMessageType.System);
                    }

                    return marginComission;
                }
                else
                {
                    for (int i = 0; i < _listTable.Count; i++)
                    {
                        if (_listTable[i].Year == timeStart.Year)
                        {
                            rate = _listTable[i].Rate / 100;
                        }
                    }

                    int daysInYear = GetDaysInYear(timeStart.Year);

                    decimal marginComission = Math.Round(rate / daysInYear * margin, 2);

                    if (_fullLogIsOn.ValueBool == true)
                    {
                        SendNewLogMessage($"Date: {timeStart.ToString("dd.MM.yyyy")}, Margin: {margin}, Rate: {rate}, TypeRate: {typeValue}, Comission: {marginComission}", LogMessageType.System);
                    }

                    return marginComission;
                }                                
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                return 0;
            }
        }

        private int GetDaysInYear(int year)
        {
            if (year % 4 == 0)
            {
                return 366;
            }

            return 365;
        }

        #endregion
    }        
}
