using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace OsEngine.Market.Servers.FinamGrpc
{
    public partial class FinamGrpcMarketsUi : Window
    {
        public FinamGrpcMarketsUi(string currentPairs)
        {
            InitializeComponent();

            Title = OsLocalization.ConvertToLocString("Eng:Markets and security classes_Ru:Рынки и классы бумаг_");
            ButtonAccept.Content = OsLocalization.ConvertToLocString("Eng:Accept_Ru:Принять_");
            ButtonCancel.Content = OsLocalization.ConvertToLocString("Eng:Cancel_Ru:Отмена_");

            _selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(currentPairs) == false)
            {
                string[] pairs = currentPairs.Split(';');

                for (int i = 0; i < pairs.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(pairs[i]) == false)
                    {
                        _selected.Add(pairs[i].Trim());
                    }
                }
            }

            CreateTables();

            ButtonAccept.Click += ButtonAccept_Click;
            ButtonCancel.Click += ButtonCancel_Click;
            Closing += FinamGrpcMarketsUi_Closing;
        }

        private readonly HashSet<string> _selected;

        public bool UserAcceptAction;

        public string ResultPairs;

        public event Action<string> AcceptEvent;

        private DataGridView _gridLeft;

        private DataGridView _gridRight;

        private void CreateTables()
        {
            try
            {
                _gridLeft = CreateGrid();
                HostLeft.Child = _gridLeft;
                _gridLeft.DataError += Grid_DataError;

                _gridRight = CreateGrid();
                HostRight.Child = _gridRight;
                _gridRight.DataError += Grid_DataError;

                FillTables();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridView CreateGrid()
        {
            DataGridView newGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersVisible = false;
            newGrid.ScrollBars = ScrollBars.None;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(column0);

            DataGridViewCheckBoxColumn column1 = new DataGridViewCheckBoxColumn();
            column1.ReadOnly = false;
            column1.Width = 45;
            newGrid.Columns.Add(column1);

            return newGrid;
        }

        private void FillTables()
        {
            for (int g = 0; g < Groups.Length; g++)
            {
                DataGridView grid = g % 2 == 0 ? _gridLeft : _gridRight;

                // заголовок группы собираем руками без шаблона чекбокса — null в нём даёт DataError
                DataGridViewRow header = new DataGridViewRow();
                DataGridViewTextBoxCell headerText = new DataGridViewTextBoxCell();
                headerText.Value = Groups[g].Label;
                headerText.Style = new DataGridViewCellStyle(grid.DefaultCellStyle);
                headerText.Style.Font = new System.Drawing.Font(grid.Font, System.Drawing.FontStyle.Bold);
                headerText.Style.ForeColor = Themes.ThemeManager.GetColorWinForms("GridTextColor");
                DataGridViewTextBoxCell headerStub = new DataGridViewTextBoxCell();
                headerStub.Value = "";
                header.Cells.Add(headerText);
                header.Cells.Add(headerStub);
                header.ReadOnly = true;
                grid.Rows.Add(header);

                for (int i = 0; i < Groups[g].Items.Length; i++)
                {
                    MarketItem item = Groups[g].Items[i];

                    DataGridViewRow row = new DataGridViewRow();
                    row.CreateCells(grid, "   " + item.Label, _selected.Contains(item.Key));

                    // явный стиль ячейки из темы — наследование от шаблона колонки ненадёжно
                    DataGridViewCellStyle itemStyle = new DataGridViewCellStyle(grid.DefaultCellStyle);
                    itemStyle.ForeColor = Themes.ThemeManager.GetColorWinForms("GridTextColor");
                    row.Cells[0].Style = itemStyle;

                    row.Tag = item.Key;
                    grid.Rows.Add(row);
                }
            }
        }

        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<string> pairs = new List<string>();
                CollectChecked(_gridLeft, pairs);
                CollectChecked(_gridRight, pairs);

                ResultPairs = string.Join(";", pairs);
                UserAcceptAction = true;

                // окно немодальное: применяем и закрываемся
                AcceptEvent?.Invoke(ResultPairs);
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CollectChecked(DataGridView grid, List<string> pairs)
        {
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                DataGridViewRow row = grid.Rows[i];

                if (row.Tag == null)
                {
                    continue;
                }

                if (row.Cells[1].Value is bool isChecked && isChecked)
                {
                    pairs.Add((string)row.Tag);
                }
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UserAcceptAction = false;
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void FinamGrpcMarketsUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                ButtonAccept.Click -= ButtonAccept_Click;
                ButtonCancel.Click -= ButtonCancel_Click;

                CleanGrid(HostLeft, ref _gridLeft);
                CleanGrid(HostRight, ref _gridRight);

                Closing -= FinamGrpcMarketsUi_Closing;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CleanGrid(WindowsFormsHost host, ref DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            host.Child = null;
            grid.DataError -= Grid_DataError;

            DataGridFactory.ClearLinks(grid);

            grid.Rows.Clear();
            grid.Columns.Clear();
            grid.DataSource = null;
            grid.Dispose();
            grid = null;
        }

        private sealed class MarketGroup
        {
            public string Label;
            public MarketItem[] Items;
        }

        private sealed class MarketItem
        {
            public string Label;
            public string Key;
        }

        private static MarketItem Item(string eng, string ru, string key)
        {
            return new MarketItem
            {
                Label = OsLocalization.ConvertToLocString("Eng:" + eng + "_Ru:" + ru + "_"),
                Key = key
            };
        }

        private static readonly MarketGroup[] Groups = new MarketGroup[]
        {
            new MarketGroup
            {
                Label = OsLocalization.ConvertToLocString("Eng:Russian market_Ru:Рынок РФ_"),
                Items = new MarketItem[]
                {
                    Item("Stocks MOEX", "Акции MOEX", "MISX|Stock"),
                    Item("Stocks RTS (RUSX)", "Акции RTS (RUSX)", "RUSX|Stock"),
                    Item("Futures MOEX (RTSX)", "Фьючерсы MOEX (RTSX)", "RTSX|Futures"),
                    Item("Options MOEX (RTSX)", "Опционы MOEX (RTSX)", "RTSX|Option"),
                    Item("Futures RTS (RUSX)", "Фьючерсы RTS (RUSX)", "RUSX|Futures"),
                    Item("Bonds MOEX", "Облигации MOEX", "MISX|Bond"),
                    Item("Currencies MOEX", "Валюты MOEX", "MISX|CurrencyPair"),
                    Item("Funds and ETF RF", "Фонды и ETF РФ", "MISX|Fund"),
                    Item("Indices MOEX", "Индексы MOEX", "MISX|Index")
                }
            },
            new MarketGroup
            {
                Label = OsLocalization.ConvertToLocString("Eng:SPB exchange_Ru:СПБ биржа_"),
                Items = new MarketItem[]
                {
                    Item("Stocks SPB", "Акции SPB", "_SPBZ|Stock")
                }
            },
            new MarketGroup
            {
                Label = OsLocalization.ConvertToLocString("Eng:USA_Ru:США_"),
                Items = new MarketItem[]
                {
                    Item("Stocks NYSE", "Акции NYSE", "XNYS|Stock"),
                    Item("Stocks NASDAQ", "Акции NASDAQ", "XNMS|Stock"),
                    Item("Stocks ARCA", "Акции ARCA", "ARCX|Stock"),
                    Item("Stocks BATS", "Акции BATS", "BATS|Stock"),
                    Item("Stocks NASDAQ Capital", "Акции NASDAQ Capital", "XNCM|Stock"),
                    Item("Funds and ETF USA (ARCA)", "Фонды и ETF США (ARCA)", "ARCX|Fund"),
                    Item("Options USA (OPRA)", "Опционы США (OPRA)", "OPRA|Option"),
                    Item("Futures NYMEX", "Фьючерсы NYMEX", "XNYM|Futures"),
                    Item("Futures CME", "Фьючерсы CME", "XCME|Futures"),
                    Item("Futures CBOT", "Фьючерсы CBOT", "XCBT|Futures"),
                    Item("Futures ICE US", "Фьючерсы ICE US", "IFUS|Futures")
                }
            },
            new MarketGroup
            {
                Label = OsLocalization.ConvertToLocString("Eng:Europe_Ru:Европа_"),
                Items = new MarketItem[]
                {
                    Item("Stocks XETRA", "Акции XETRA", "XETR|Stock"),
                    Item("Stocks Tradegate", "Акции Tradegate", "XGAT|Stock"),
                    Item("Stocks London (XLON)", "Акции Лондон (XLON)", "XLON|Stock"),
                    Item("Stocks London (XLOM)", "Акции Лондон (XLOM)", "XLOM|Stock"),
                    Item("Stocks Frankfurt", "Акции Франкфурт", "XFRA|Stock"),
                    Item("Stocks Paris", "Акции Париж", "XPAR|Stock"),
                    Item("Stocks Madrid", "Акции Мадрид", "XMAD|Stock"),
                    Item("Stocks Amsterdam", "Акции Амстердам", "XAMS|Stock"),
                    Item("Stocks Brussels", "Акции Брюссель", "XBRU|Stock"),
                    Item("Funds and ETF XETRA", "Фонды и ETF XETRA", "XETR|Fund"),
                    Item("Futures ICE Europe", "Фьючерсы ICE Europe", "IFEU|Futures"),
                    Item("Futures London Metals", "Фьючерсы London Metals", "XLME|Futures")
                }
            },
            new MarketGroup
            {
                Label = OsLocalization.ConvertToLocString("Eng:Canada_Ru:Канада_"),
                Items = new MarketItem[]
                {
                    Item("Stocks Toronto", "Акции Торонто", "XTSE|Stock"),
                    Item("Futures Toronto", "Фьючерсы Торонто", "XTSE|Futures")
                }
            },
            new MarketGroup
            {
                Label = OsLocalization.ConvertToLocString("Eng:Asia_Ru:Азия_"),
                Items = new MarketItem[]
                {
                    Item("Stocks Hong Kong", "Акции Гонконг", "XHKG|Stock"),
                    Item("Stocks Tokyo", "Акции Токио", "XTKS|Stock"),
                    Item("Stocks Shanghai", "Акции Шанхай", "XSHG|Stock"),
                    Item("Stocks Shenzhen", "Акции Шэньчжэнь", "XSHE|Stock"),
                    Item("Stocks Australia", "Акции Австралия", "XASX|Stock"),
                    Item("Stocks India", "Акции Индия", "XBOM|Stock"),
                    Item("Futures Hong Kong", "Фьючерсы Гонконг", "XHKG|Futures"),
                    Item("Futures Tokyo", "Фьючерсы Токио", "XTKS|Futures")
                }
            },
            new MarketGroup
            {
                Label = OsLocalization.ConvertToLocString("Eng:Crypto_Ru:Крипта_"),
                Items = new MarketItem[]
                {
                    Item("Crypto currencies", "Крипто-валюты", "_CRYP|CurrencyPair"),
                    Item("Crypto options", "Крипто-опционы", "_CRYP|Option"),
                    Item("Binance crypto", "Крипта Binance", "_BNCC|CurrencyPair")
                }
            },
            new MarketGroup
            {
                Label = OsLocalization.ConvertToLocString("Eng:OTC and other_Ru:Внебиржевые и прочие_"),
                Items = new MarketItem[]
                {
                    Item("Stocks OTC (OOTC)", "Акции OTC (OOTC)", "OOTC|Stock"),
                    Item("Stocks Pink Sheets (PINX)", "Акции Pink Sheets (PINX)", "PINX|Stock"),
                    Item("Stocks OTCQB", "Акции OTCQB", "OTCB|Stock"),
                    Item("Stocks OTCQX", "Акции OTCQX", "OTCQ|Stock"),
                    Item("Stocks OTC Markets", "Акции OTC Markets", "OTCM|Stock")
                }
            }
        };
    }
}
