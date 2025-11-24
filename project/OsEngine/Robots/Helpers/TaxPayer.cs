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

/* Description
The bot only works in the Tester. During testing, the bot checks the Journal of all bots at the end of the year. 
It calculates the profit of trades for the last year in the Journal, calculates the tax, and deducts this tax from the deposit.
 */

namespace OsEngine.Robots.Helpers
{
    [Bot ("TaxPayer")]
    public class TaxPayer : BotPanel
    {
        private BotTabSimple _tab;
        private StrategyParameterString _regime;
        private StartProgram _startProgram;

        public TaxPayer(string name, StartProgram startProgram) : base(name, startProgram)
        {
            if (startProgram != StartProgram.IsTester)
            {
                SendNewLogMessage("Бот работает только в Тестере", Logging.LogMessageType.Error);                
                return;
            }

            _startProgram = startProgram;

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            string tabName = " Параметры ";

            _regime = CreateParameter("Режим", "Off", new string[] { "Off", "On" }, tabName);

            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab(" Периоды ");

            CreateTable();
            customTab.AddChildren(_host);

            LoadTable();

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        }

        #region Table Periods

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

                _dgv.Columns[0].HeaderText = "Год";
                _dgv.Columns[1].HeaderText = "Ставка";

                DataGridViewButtonCell cellButton = new();

                _dgv.Rows[^1].Cells[0] = cellButton;
                _dgv.Rows[^1].Cells[0].Value = "Добавить строку";
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
            _dgv.Rows[^2].Cells[2].Value = "Удалить строку";
            _dgv.Rows[^2].Cells[2].ReadOnly = true;

            SaveTable();
        }

        private void DeleteRow(int rowIndex)
        {
            int year = int.Parse(_dgv[0, rowIndex].Value.ToString());
            int deleteIndex = _listTable.FindIndex(x => x.Year == year);

            if (deleteIndex > -1)
            {
                _listTable.RemoveAt(deleteIndex);
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
                                SendNewLogMessage("В таблице уже есть такой год", Logging.LogMessageType.Error);
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
                    return;
                }

                string json = File.ReadAllText(fileName);
                _listTable = JsonConvert.DeserializeObject<List<ListTablePeriods>>(json);

                for (int i = 0; i < _listTable.Count; i++)
                {
                    DataGridViewRow row = new();
                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTable[i].Year });
                    row.Cells.Add(new DataGridViewTextBoxCell() { Value = _listTable[i].Rate });
                    row.Cells.Add(new DataGridViewButtonCell() { Value = "Удалить строку" });

                    _dgv.Rows.Insert(_dgv.RowCount - 1, row);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
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
                    MainLogic(candle[^1].TimeStart.Year);
                    return;
                }

                if (candle[^1].TimeStart.Year > candle[^2].TimeStart.Year)
                {
                    MainLogic(candle[^2].TimeStart.Year);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void MainLogic(int year)
        {
            decimal profit = 0;

            List<BotPanel> bots = OsTraderMaster.Master.PanelsArray;

            BotPanel taxBot = null;

            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i].GetNameStrategyType() == "TaxPayer")
                {
                    taxBot = bots[i];
                    continue;
                }

                if (!bots[i].OnOffEventsInTabs)
                {
                    continue;
                }

                List<Journal.Journal> journal = bots[i].GetJournals();

                decimal profitBot = 0;

                for (int i2 = 0; i2 < journal[0].CloseAllPositions.Count; i2++)
                {
                    if (journal[0].CloseAllPositions[i2].TimeClose.Year != year)
                    {
                        continue;
                    }

                    profitBot += journal[0].CloseAllPositions[i2].ProfitPortfolioAbs;
                }

                profit += profitBot;
            }

            TaxDeal(taxBot, year, profit);
        }

        private void TaxDeal(BotPanel taxBot, int year, decimal profit)
        {
            if (taxBot == null)
            {
                return;
            }

            decimal rate = 0;

            for (int i = 0; i < _listTable.Count; i++)
            {
                if (_listTable[i].Year == year)
                {
                    rate = _listTable[i].Rate;
                }
            }

            if (rate <= 0)
            {
                return;
            }

            decimal tax = Math.Round(profit * rate / 100, 2);
                 
            if (tax > 0)
            {
                Security security = new();
                security.Name = "Taxes";
                security.NameClass = "TestClass";
                
                ConnectorCandles connector = taxBot.TabsSimple[0].Connector;
                Portfolio portfolio = taxBot.TabsSimple[0].Portfolio;                
                BotManualControl manualPositionSupport = taxBot.TabsSimple[0].ManualPositionSupport;
                Journal.Journal journal = taxBot.TabsSimple[0].GetJournal();

                PositionCreator _dealCreator = new PositionCreator();

                Position newDeal = _dealCreator.CreatePosition(
                   taxBot.NameStrategyUniq, Side.Buy, 2, tax,
                   OrderPriceType.Limit, manualPositionSupport.SecondToOpen,
                   security, portfolio, _startProgram,
                   manualPositionSupport.OrderTypeTime,
                   manualPositionSupport.LimitsMakerOnly);

                journal.SetNewDeal(newDeal);

                taxBot.TabsSimple[0].OrderFakeExecute(newDeal.OpenOrders[0], new DateTime(year, 12, 31, 23, 59, 58));

                Position position = taxBot.TabsSimple[0].PositionsLast;

                Order closeOrder
                    = _dealCreator.CreateCloseOrderForDeal(security, position, 1,
                    OrderPriceType.Limit, new TimeSpan(1, 1, 1, 1),
                    StartProgram, manualPositionSupport.OrderTypeTime,
                    connector.ServerFullName, manualPositionSupport.LimitsMakerOnly);

                closeOrder.PortfolioNumber = portfolio.Number;
                closeOrder.Volume = tax;

                position.AddNewCloseOrder(closeOrder);

                taxBot.TabsSimple[0].OrderFakeExecute(closeOrder, new DateTime(year, 12, 31, 23, 59, 59));
            }
        }

        #endregion
    }

    public class ListTablePeriods
    {
        public int Year;
        public decimal Rate;
    }
}
