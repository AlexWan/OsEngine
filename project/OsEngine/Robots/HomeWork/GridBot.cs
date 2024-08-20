using OsEngine;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Language;
using System.Threading;

[Bot("GridBot")]
public class GridBot : BotPanel
{
    #region Parameters and service

    public override string GetNameStrategyType()
    {
        return "GridBot";
    }

    public override void ShowIndividualSettingsDialog()
    {

    }

    public GridBot(string name, StartProgram startProgram)
          : base(name, startProgram)
    {
        Regime = CreateParameter(_language.Regime, "Off", new string[] { "Off", "On" }, _language.BaseSettingsTab);
        MaxOpenOrdersInMarket = CreateParameter(_language.MaxOpenOrdersInMarket, 2, 1, 10, 1, _language.BaseSettingsTab);
        RegimeLogicEntry = CreateParameter(_language.RegimeLogicEntry, "On new trade", new string[] { "On new trade", "Once per second" }, _language.BaseSettingsTab);

        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        _tab.ManualPositionSupport.SecondToOpenIsOn = false;
        _tab.ManualPositionSupport.SecondToCloseIsOn = false;

        LoadSettings();
        LoadLines();

        // customization param ui

        this.ParamGuiSettings.Title = _language.BotSettingTabWindows;
        this.ParamGuiSettings.Height = 800;
        this.ParamGuiSettings.Width = 780;

        // grid to orders

        CustomTabToParametersUi customTabOrderGrid = ParamGuiSettings.CreateCustomTab(_language.OrderGridTab);
        CreateColumnsTable();
        CreateRowsTable();
        customTabOrderGrid.AddChildren(_hostGrid);

        // non trade periods

        NonTradePeriod1OnOff
            = CreateParameter(_language.NonTradePeriodsParamOnOff + "1",
            "Off", new string[] { "Off", "On" }, _language.NonTradePeriodsTab);
        NonTradePeriod1Start = CreateParameterTimeOfDay(_language.NonTradePeriodsParamStart + "1", 9, 0, 0, 0, _language.NonTradePeriodsTab);
        NonTradePeriod1End = CreateParameterTimeOfDay(_language.NonTradePeriodsParamEnd + "1", 10, 5, 0, 0, _language.NonTradePeriodsTab);

        NonTradePeriod2OnOff
            = CreateParameter(_language.NonTradePeriodsParamOnOff + "2",
            "Off", new string[] { "Off", "On" }, _language.NonTradePeriodsTab);
        NonTradePeriod2Start = CreateParameterTimeOfDay(_language.NonTradePeriodsParamStart + "2", 13, 55, 0, 0, _language.NonTradePeriodsTab);
        NonTradePeriod2End = CreateParameterTimeOfDay(_language.NonTradePeriodsParamEnd + "2", 14, 5, 0, 0, _language.NonTradePeriodsTab);

        NonTradePeriod3OnOff
            = CreateParameter(_language.NonTradePeriodsParamOnOff + "3",
            "Off", new string[] { "Off", "On" }, _language.NonTradePeriodsTab);
        NonTradePeriod3Start = CreateParameterTimeOfDay(_language.NonTradePeriodsParamStart + "3", 18, 40, 0, 0, _language.NonTradePeriodsTab);
        NonTradePeriod3End = CreateParameterTimeOfDay(_language.NonTradePeriodsParamEnd + "3", 19, 5, 0, 0, _language.NonTradePeriodsTab);

        NonTradePeriod4OnOff
            = CreateParameter(_language.NonTradePeriodsParamOnOff + "4",
            "Off", new string[] { "Off", "On" }, _language.NonTradePeriodsTab);
        NonTradePeriod4Start = CreateParameterTimeOfDay(_language.NonTradePeriodsParamStart + "4", 23, 40, 0, 0, _language.NonTradePeriodsTab);
        NonTradePeriod4End = CreateParameterTimeOfDay(_language.NonTradePeriodsParamEnd + "4", 23, 59, 0, 0, _language.NonTradePeriodsTab);

        // events

        _gridDataGrid.CellClick += EventСlickOnButtonCreatingLine;
        this.DeleteEvent += Strategy_DeleteEvent;
        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        _tab.NewTickEvent += _tab_NewTickEvent;
        _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccesEvent;
        _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
        _tab.ManualPositionSupport.DisableManualSupport();
        this.ParametrsChangeByUser += GridBot_ParametrsChangeByUser;

        Thread worker = new Thread(WorkerThreadArea);
        worker.Start();
    }

    private LanguageBase _language = new LanguageBase();

    private BotTabSimple _tab;

    public StrategyParameterString Regime;

    public StrategyParameterString RegimeLogicEntry;

    public StrategyParameterInt MaxOpenOrdersInMarket;

    public bool LineIsOn;

    public Side GridSide;

    public decimal FirstPrice;

    public int LineCountStart;

    public decimal LineStep;

    public Type_Volume TypeVolume;

    public decimal StartVolume;

    public decimal ProfitPercent;

    public List<GridBotLine> Lines = new List<GridBotLine>();

    private void Strategy_DeleteEvent()
    {
        if (File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
        {
            File.Delete(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt");
        }

        if (File.Exists(@"Engine\" + NameStrategyUniq + @"Lines.txt"))
        {
            File.Delete(@"Engine\" + NameStrategyUniq + @"Lines.txt");
        }
    }

    public void SaveSettings()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt", false)
            )
            {
                writer.WriteLine(LineIsOn);
                writer.WriteLine(GridSide);
                writer.WriteLine(FirstPrice);
                writer.WriteLine(LineCountStart);
                writer.WriteLine(LineStep);
                writer.WriteLine(TypeVolume);
                writer.WriteLine(StartVolume);
                writer.WriteLine(ProfitPercent);
                writer.Close();
            }
        }
        catch (Exception)
        {
            // отправить в лог
        }
    }

    private void LoadSettings()
    {
        if (!File.Exists(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
        {
            return;
        }

        try
        {
            using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"SettingsBot.txt"))
            {

                LineIsOn = Convert.ToBoolean(reader.ReadLine());
                Enum.TryParse(reader.ReadLine(), out GridSide);
                FirstPrice = reader.ReadLine().ToDecimal();
                LineCountStart = Convert.ToInt32(reader.ReadLine());
                LineStep = reader.ReadLine().ToDecimal();
                Enum.TryParse(reader.ReadLine(), out TypeVolume);
                StartVolume = reader.ReadLine().ToDecimal();
                ProfitPercent = reader.ReadLine().ToDecimal();

                reader.Close();
            }
        }
        catch (Exception)
        {
            // отправить в лог
        }
    }

    public void SaveLines()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(@"Engine\" + NameStrategyUniq + @"Lines.txt", false)
            )
            {

                for (int i = 0; i < Lines.Count; i++)
                {
                    writer.WriteLine(Lines[i].GetSaveStr());
                }
                writer.Close();
            }
        }
        catch (Exception)
        {
            // отправить в лог
        }
    }

    public void LoadLines()
    {
        if (!File.Exists(@"Engine\" + NameStrategyUniq + @"Lines.txt"))
        {
            return;
        }

        try
        {
            using (StreamReader reader = new StreamReader(@"Engine\" + NameStrategyUniq + @"Lines.txt"))
            {
                while (reader.EndOfStream == false)
                {
                    GridBotLine newLine = new GridBotLine();
                    newLine.SetFromStr(reader.ReadLine());
                    Lines.Add(newLine);
                }
                reader.Close();
            }
        }
        catch (Exception)
        {
            // отправить в лог
        }
    }

    #endregion

    #region Non trade periods

    public StrategyParameterString NonTradePeriod1OnOff;
    public StrategyParameterTimeOfDay NonTradePeriod1Start;
    public StrategyParameterTimeOfDay NonTradePeriod1End;

    public StrategyParameterString NonTradePeriod2OnOff;
    public StrategyParameterTimeOfDay NonTradePeriod2Start;
    public StrategyParameterTimeOfDay NonTradePeriod2End;

    public StrategyParameterString NonTradePeriod3OnOff;
    public StrategyParameterTimeOfDay NonTradePeriod3Start;
    public StrategyParameterTimeOfDay NonTradePeriod3End;

    public StrategyParameterString NonTradePeriod4OnOff;
    public StrategyParameterTimeOfDay NonTradePeriod4Start;
    public StrategyParameterTimeOfDay NonTradePeriod4End;

    private bool IsBlockNonTradePeriods(DateTime curTime)
    {
        if (NonTradePeriod1OnOff.ValueString == "On")
        {
            if (NonTradePeriod1Start.Value < curTime
             && NonTradePeriod1End.Value > curTime)
            {
                return true;
            }
        }

        if (NonTradePeriod2OnOff.ValueString == "On")
        {
            if (NonTradePeriod2Start.Value < curTime
             && NonTradePeriod2End.Value > curTime)
            {
                return true;
            }
        }

        if (NonTradePeriod3OnOff.ValueString == "On")
        {
            if (NonTradePeriod3Start.Value < curTime
             && NonTradePeriod3End.Value > curTime)
            {
                return true;
            }
        }

        if (NonTradePeriod4OnOff.ValueString == "On")
        {
            if (NonTradePeriod4Start.Value < curTime
             && NonTradePeriod4End.Value > curTime)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Data Grid Prime

    private DataGridView _gridDataGrid;

    WindowsFormsHost _hostGrid;

    private void CreateColumnsTable()
    {
        try
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateColumnsTable));
                return;
            }

            _hostGrid = new WindowsFormsHost();

            _gridDataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                   DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
            _gridDataGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _gridDataGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridDataGrid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            DataGridViewTextBoxCell cellParam0 = new DataGridViewTextBoxCell();
            cellParam0.Style = _gridDataGrid.DefaultCellStyle;
            cellParam0.Style.WrapMode = DataGridViewTriState.True;

            DataGridViewColumn newColumn0 = new DataGridViewColumn();
            newColumn0.CellTemplate = cellParam0;
            newColumn0.HeaderText = _language.GridHeaderOrder0;
            _gridDataGrid.Columns.Add(newColumn0);
            newColumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn1 = new DataGridViewColumn();
            newColumn1.CellTemplate = cellParam0;
            newColumn1.HeaderText = _language.GridHeaderOrder1;
            _gridDataGrid.Columns.Add(newColumn1);
            newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn2 = new DataGridViewColumn();
            newColumn2.CellTemplate = cellParam0;
            newColumn2.HeaderText = _language.GridHeaderOrder2;
            _gridDataGrid.Columns.Add(newColumn2);
            newColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn3 = new DataGridViewColumn();
            newColumn3.CellTemplate = cellParam0;
            newColumn3.HeaderText = _language.GridHeaderOrder3;
            _gridDataGrid.Columns.Add(newColumn3);
            newColumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn4 = new DataGridViewColumn();
            newColumn4.CellTemplate = cellParam0;
            newColumn4.HeaderText = _language.GridHeaderOrder4;
            _gridDataGrid.Columns.Add(newColumn4);
            newColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn5 = new DataGridViewColumn();
            newColumn5.CellTemplate = cellParam0;
            newColumn5.HeaderText = _language.GridHeaderOrder5;
            _gridDataGrid.Columns.Add(newColumn5);
            newColumn5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn6 = new DataGridViewColumn();
            newColumn6.CellTemplate = cellParam0;
            newColumn6.HeaderText = _language.GridHeaderOrder6;
            _gridDataGrid.Columns.Add(newColumn6);
            newColumn6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn7 = new DataGridViewColumn();
            newColumn7.CellTemplate = cellParam0;
            newColumn7.HeaderText = _language.GridHeaderOrder7;
            _gridDataGrid.Columns.Add(newColumn7);
            newColumn7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _hostGrid.Child = _gridDataGrid;

            _gridDataGrid.CellValueChanged += EventChangeValueInTable;
        }
        catch (Exception ex)
        {
            _tab.SetNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
        }
    }

    private void CreateRowsTable()
    {
        if (_gridDataGrid.InvokeRequired)
        {
            _gridDataGrid.Invoke(new Action(CreateRowsTable));
            return;
        }

        try
        {
            _gridDataGrid.CellValueChanged -= EventChangeValueInTable;
            _gridDataGrid.Rows.Clear();

            _gridDataGrid.Rows.Add(CreateRowParametersGrid());
            _gridDataGrid.Rows.Add(CreateSpaceRow());
            _gridDataGrid.Rows.Add(CreateRowButton());
            _gridDataGrid.Rows.Add(CreateSpaceRow());
            _gridDataGrid.Rows.Add(CreateRowHeaderTableLine());

            CreateAndPaintLineTable();

            _gridDataGrid.CellValueChanged += EventChangeValueInTable;
        }
        catch (Exception ex)
        {
            _tab.SetNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
        }
    }

    private DataGridViewRow CreateRowParametersGrid()
    {
        DataGridViewRow row = new DataGridViewRow();

        DataGridViewComboBoxCell regimeIsOnBox = new DataGridViewComboBoxCell();
        regimeIsOnBox.Items.Add("True");
        regimeIsOnBox.Items.Add("False");
        row.Cells.Add(regimeIsOnBox);

        DataGridViewComboBoxCell sideBox = new DataGridViewComboBoxCell();
        sideBox.Items.Add("Buy");
        sideBox.Items.Add("Sell");
        row.Cells.Add(sideBox);

        DataGridViewTextBoxCell firstOrderPriceTextBox = new DataGridViewTextBoxCell();
        row.Cells.Add(firstOrderPriceTextBox);

        DataGridViewTextBoxCell lineCountTextBox = new DataGridViewTextBoxCell();
        row.Cells.Add(lineCountTextBox);

        DataGridViewTextBoxCell lineStepTextBox = new DataGridViewTextBoxCell();
        row.Cells.Add(lineStepTextBox);

        DataGridViewComboBoxCell volumeTypeTextBox = new DataGridViewComboBoxCell();
        volumeTypeTextBox.Items.Add(_language.TypeVolumeNumberOfContracts);
        volumeTypeTextBox.Items.Add(_language.TypeVolumeContractCurrency);
        row.Cells.Add(volumeTypeTextBox);

        DataGridViewTextBoxCell startVolumeTextBox = new DataGridViewTextBoxCell();
        row.Cells.Add(startVolumeTextBox);

        DataGridViewTextBoxCell profitPercentTextBox = new DataGridViewTextBoxCell();
        row.Cells.Add(profitPercentTextBox);

        try
        {
            regimeIsOnBox.Value = LineIsOn.ToString();
            sideBox.Value = GridSide == OsEngine.Entity.Side.Buy ? "Buy" : "Sell";
            firstOrderPriceTextBox.Value = FirstPrice.ToString();
            lineCountTextBox.Value = LineCountStart.ToString();
            lineStepTextBox.Value = LineStep.ToString();
            volumeTypeTextBox.Value = TypeVolume == Type_Volume.ContractCurrency ? _language.TypeVolumeContractCurrency : _language.TypeVolumeNumberOfContracts;
            startVolumeTextBox.Value = StartVolume.ToString();
            profitPercentTextBox.Value = ProfitPercent.ToString();
        }
        catch
        {

        }

        return row;
    }

    private DataGridViewRow CreateRowButton()
    {
        DataGridViewRow row = new DataGridViewRow();

        row.Cells.Add(new DataGridViewButtonCell());
        row.Cells[0].Value = _language.ButtonCreateTable;
        row.Cells.Add(new DataGridViewTextBoxCell());

        row.Cells.Add(new DataGridViewButtonCell());
        row.Cells[2].Value = _language.ButtonDeleteAllLine;
        row.Cells.Add(new DataGridViewTextBoxCell());

        row.Cells.Add(new DataGridViewButtonCell());
        row.Cells[4].Value = _language.ButtonCreateLine;
        row.Cells.Add(new DataGridViewTextBoxCell());

        row.Cells.Add(new DataGridViewButtonCell());
        row.Cells[6].Value = _language.ButtonDeleteSelectLine;
        row.Cells.Add(new DataGridViewTextBoxCell());


        row.Cells[1].Value = " \n" +
            " \n" +
            " \n";

        row.ReadOnly = true;

        return row;
    }

    private DataGridViewRow CreateSpaceRow()
    {
        DataGridViewRow row = new DataGridViewRow();

        for (int i = 0; i < _gridDataGrid.Columns.Count; i++)
        {
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[i].Value = "      ------      ";
        }

        row.ReadOnly = true;

        return row;
    }

    private DataGridViewRow CreateRowHeaderTableLine()
    {
        DataGridViewRow row = new DataGridViewRow();

        DataGridViewTextBoxCell newCell = new DataGridViewTextBoxCell();
        newCell.Value = _language.GridLineHeader0;
        row.Cells.Add(newCell);
        newCell.ReadOnly = true;

        DataGridViewTextBoxCell newCell1 = new DataGridViewTextBoxCell();
        newCell1.Value = _language.GridLineHeader1;
        row.Cells.Add(newCell1);
        newCell1.ReadOnly = true;

        DataGridViewTextBoxCell newCell2 = new DataGridViewTextBoxCell();
        newCell2.Value = _language.GridLineHeader2;
        row.Cells.Add(newCell2);
        newCell2.ReadOnly = true;

        DataGridViewTextBoxCell newCell3 = new DataGridViewTextBoxCell();
        newCell3.Value = _language.GridLineHeader3;
        row.Cells.Add(newCell3);
        newCell3.ReadOnly = true;

        DataGridViewTextBoxCell newCell4 = new DataGridViewTextBoxCell();
        newCell4.Value = _language.GridLineHeader4;
        row.Cells.Add(newCell4);
        newCell4.ReadOnly = true;

        DataGridViewTextBoxCell newCell5 = new DataGridViewTextBoxCell();
        newCell5.Value = _language.GridLineHeader5;
        row.Cells.Add(newCell5);
        newCell5.ReadOnly = true;

        DataGridViewTextBoxCell newCell6 = new DataGridViewTextBoxCell();
        newCell6.Value = _language.GridLineHeader6;
        row.Cells.Add(newCell6);
        newCell6.ReadOnly = true;

        return row;
    }

    private void CreateAndPaintLineTable()
    {
        if (_gridDataGrid.InvokeRequired)
        {
            _gridDataGrid.Invoke(new Action(CreateAndPaintLineTable));
            return;
        }
        for (int i = 0; i < Lines.Count; i++)
        {
            DataGridViewRow rowLine = new DataGridViewRow();

            rowLine.Cells.Add(new DataGridViewTextBoxCell());
            rowLine.Cells[0].Value = i + 1;

            DataGridViewComboBoxCell cell1 = new DataGridViewComboBoxCell();
            cell1.Items.Add(true.ToString());
            cell1.Items.Add(false.ToString());
            cell1.Value = Lines[i].IsOn.ToString();
            rowLine.Cells.Add(cell1);

            rowLine.Cells.Add(new DataGridViewTextBoxCell());
            rowLine.Cells[2].Value = Math.Round(Lines[i].PriceEnter, 10);

            rowLine.Cells.Add(new DataGridViewTextBoxCell());
            rowLine.Cells[3].Value = Math.Round(Lines[i].PriceExit, 10);

            rowLine.Cells.Add(new DataGridViewTextBoxCell());
            rowLine.Cells[4].Value = Math.Round(Lines[i].Volume, 10);

            rowLine.Cells.Add(new DataGridViewTextBoxCell());
            rowLine.Cells[5].Value = Lines[i].Side;

            rowLine.Cells.Add(new DataGridViewCheckBoxCell());
            rowLine.Cells[6].Value = Lines[i].checkStateLine;

            rowLine.ReadOnly = true;

            rowLine.Cells[6].ReadOnly = false;
            _gridDataGrid.Rows.Add(rowLine);
            rowLine.Cells[1].ReadOnly = false;
        }

    }

    private int _lastClickOrderLineRowNum;

    private void EventСlickOnButtonCreatingLine(object sender, DataGridViewCellEventArgs clickCelll)
    {
        try
        {
            if (clickCelll.ColumnIndex == 0 && clickCelll.RowIndex == 2)
            {
                if (ProfitPercent <= 0)
                {
                    SendNewLogMessage(_language.Message1,
                        OsEngine.Logging.LogMessageType.Error);
                    return;
                }
                if (FirstPrice <= 0)
                {
                    SendNewLogMessage(_language.Message2,
                        OsEngine.Logging.LogMessageType.Error);
                    return;
                }
                if (LineCountStart <= 0)
                {
                    SendNewLogMessage(_language.Message3,
                        OsEngine.Logging.LogMessageType.Error);
                    return;
                }
                if (LineStep <= 0)
                {
                    SendNewLogMessage(_language.Message4,
                        OsEngine.Logging.LogMessageType.Error);
                    return;
                }

                if (Lines != null
                    && Lines.Count > 0)
                {
                    AcceptDialogUi ui = new AcceptDialogUi(_language.Message5);
                    ui.ShowDialog();
                    if (ui.UserAcceptActioin == false)
                    {
                        return;
                    }
                }

                DeleteTable();
                CreateNewTable();
                CreateRowsTable();
            }
            else if (clickCelll.ColumnIndex == 2 && clickCelll.RowIndex == 2)
            {
                AcceptDialogUi ui
                    = new AcceptDialogUi(_language.Message6);
                ui.ShowDialog();
                if (ui.UserAcceptActioin == false)
                {
                    return;
                }

                DeleteTable();
                CreateRowsTable();
            }
            else if (clickCelll.ColumnIndex == 4 && clickCelll.RowIndex == 2)
            {
                CreateNewLine();
                CreateRowsTable();
            }
            else if (clickCelll.ColumnIndex == 6 && clickCelll.RowIndex == 2)
            {
                AcceptDialogUi ui
                = new AcceptDialogUi(_language.Message7);
                ui.ShowDialog();
                if (ui.UserAcceptActioin == false)
                {
                    return;
                }

                DeleteSelectedLines(_lastClickOrderLineRowNum);
                CreateRowsTable();
                _lastClickOrderLineRowNum = -1;
            }

            if (clickCelll.RowIndex > 2)
            {
                _lastClickOrderLineRowNum = clickCelll.RowIndex;
            }

            SaveSettings();
        }
        catch (Exception e)
        {
            _tab.SetNewLogMessage(e.ToString(), OsEngine.Logging.LogMessageType.Error);
        }
    }

    private void EventChangeValueInTable(object sender, DataGridViewCellEventArgs e)
    {
        LineIsOn = Convert.ToBoolean(_gridDataGrid.Rows[0].Cells[0].Value.ToString());

        Enum.TryParse(_gridDataGrid.Rows[0].Cells[1].Value.ToString(), out GridSide);

        FirstPrice = GetDecimal(FirstPrice, _gridDataGrid.Rows[0].Cells[2]);

        LineCountStart = GetInt(LineCountStart, _gridDataGrid.Rows[0].Cells[3]);

        LineStep = GetDecimal(LineStep, _gridDataGrid.Rows[0].Cells[4]);

        if (_gridDataGrid.Rows[0].Cells[5].Value.ToString() == _language.TypeVolumeNumberOfContracts)
            TypeVolume = Type_Volume.NumberOfContracts;
        else
            TypeVolume = Type_Volume.ContractCurrency;


        StartVolume = GetDecimal(StartVolume, _gridDataGrid.Rows[0].Cells[6]);

        ProfitPercent = GetDecimal(ProfitPercent, _gridDataGrid.Rows[0].Cells[7]);

        SaveSettings();

        try
        {
            int findRowIndex = 5;

            for (int i = 0; i < Lines.Count; i++)
            {
                Lines[i].IsOn = Convert.ToBoolean(_gridDataGrid.Rows[i + findRowIndex].Cells[1].Value.ToString().ToLower());
                Lines[i].PriceEnter = _gridDataGrid.Rows[i + findRowIndex].Cells[2].Value.ToString().ToDecimal();
                Lines[i].PriceExit = _gridDataGrid.Rows[i + findRowIndex].Cells[3].Value.ToString().ToDecimal();
                Lines[i].Volume = _gridDataGrid.Rows[i + findRowIndex].Cells[4].Value.ToString().ToDecimal();

                if (_gridDataGrid.Rows[i + findRowIndex].Cells[6].Value.ToString() == "Checked" || _gridDataGrid.Rows[i + findRowIndex].Cells[6].Value.ToString() == "True")
                {
                    Lines[i].checkStateLine = CheckState.Checked;
                }
                else
                    Lines[i].checkStateLine = CheckState.Unchecked;
            }
            SaveLines();
        }
        catch
        {

        }

    }

    private int GetInt(int oldValue, DataGridViewCell dataGridCell)
    {
        string newValueStr = dataGridCell.Value.ToString();

        if (newValueStr == "" ||
            newValueStr == "0," ||
            newValueStr == "0.")
        {
            return oldValue;
        }

        int value = 0;

        try
        {
            value = Convert.ToInt32(newValueStr);
        }
        catch
        {
            return oldValue;
        }

        return value;
    }

    private decimal GetDecimal(decimal oldValue, DataGridViewCell dataGridCell)
    {
        string newValueStr = dataGridCell.Value.ToString();



        if (newValueStr == "" ||
            newValueStr == "0," ||
            newValueStr == "0.")
        {
            return oldValue;
        }

        decimal value = 0;

        try
        {
            value = newValueStr.ToDecimal();
        }
        catch
        {
            return oldValue;
        }

        return value;
    }

    public void DeleteTable()
    {
        Lines = new List<GridBotLine>();

        List<Position> positions = _tab.PositionsOpenAll;

        for (int i = positions.Count - 1; i >= 0; i--)
        {
            if (positions[i].State == PositionStateType.Opening
                && positions[i].Comment != "canceled")
            {
                positions[i].Comment = "canceled";
                _tab.CloseAllOrderToPosition(positions[i]);
            }
        }

        SaveLines();
    }

    public void CreateNewTable()
    {
        if (FirstPrice == 0)
        {
            return;
        }

        Lines.Clear();

        decimal priceCurrent = FirstPrice;

        decimal volumeCurrent = StartVolume;

        for (int i = 0; i < LineCountStart; i++)
        {
            GridBotLine newLine = new GridBotLine();
            newLine.PriceEnter = priceCurrent;
            newLine.Side = GridSide;
            newLine.IsOn = LineIsOn;
            newLine.Volume = volumeCurrent;

            Lines.Add(newLine);

            if (GridSide == Side.Buy)
            {
                newLine.PriceExit = newLine.PriceEnter + (newLine.PriceEnter * ProfitPercent / 100);
                priceCurrent -= LineStep;
            }
            else if (GridSide == Side.Sell)
            {
                newLine.PriceExit = newLine.PriceEnter - (newLine.PriceEnter * ProfitPercent / 100);
                priceCurrent += LineStep;
            }

            //volumeCurrent += Math.Round(volumeCurrent / 100, VolumeDecimals);
        }

        SaveLines();
    }

    public void CreateNewLine()
    {
        GridBotLine newLine = new GridBotLine();
        newLine.PriceEnter = FirstPrice;
        newLine.Volume = StartVolume;
        newLine.Side = GridSide;
        newLine.IsOn = LineIsOn;
        if (GridSide == Side.Buy)
        {
            newLine.PriceExit = newLine.PriceEnter + (newLine.PriceEnter * ProfitPercent / 100);
        }
        else if (GridSide == Side.Sell)
        {
            newLine.PriceExit = newLine.PriceEnter - (newLine.PriceEnter * ProfitPercent / 100);
        }
        Lines.Add(newLine);
        SaveLines();
    }

    public void DeleteSelectedLines(int index)
    {
        index = index - 5;
        if (index < 0
            || index >= Lines.Count)
        {
            return;
        }

        decimal linePrice = Lines[index].PriceEnter;

        Lines.RemoveAt(index);

        SaveLines();

        // отзыв ордера по линии, если линия уже открывалась

        if (_tab.IsConnected == false)
        {
            return;
        }

        List<Position> positions = _tab.PositionsOpenAll;

        for (int i = 0; i < positions.Count; i++)
        {
            if (positions[i].State != PositionStateType.Opening)
            {
                continue;
            }

            if (positions[i].OpenOrders == null ||
                positions[i].OpenOrders.Count == 0)
            {
                continue;
            }

            Order openOrder = positions[i].OpenOrders[0];

            if (openOrder.Price == linePrice)
            {
                _tab.CloseAllOrderToPosition(positions[i]);
                return;
            }
        }
    }

    #endregion

    #region Trade logic

    private void WorkerThreadArea()
    {
        while (true)
        {
            try
            {
                Thread.Sleep(1000);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (RegimeLogicEntry.ValueString != "Once per second")
                {
                    continue;
                }

                if (Regime.ValueString == "Off"
                || Regime.ValueString == "Выключен")
                {
                    continue;
                }

                if (_tab.PositionsOpenAll.Count != 0)
                {
                    CloseLogic();
                }

                TradeLogic();
            }
            catch (Exception e)
            {
                _tab.SetNewLogMessage(e.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }
    }

    private void GridBot_ParametrsChangeByUser()
    {
        if (_tab.IsReadyToTrade == false ||
          _tab.IsConnected == false)
        {
            return;
        }

        List<Position> positions = _tab.PositionsOpenAll;

        if (Regime.ValueString == "Off"
            || Regime.ValueString == "Выключен")
        {
            for (int i = positions.Count - 1; i >= 0; i--)
            {
                if (positions[i].State == PositionStateType.Opening
                    && positions[i].Comment != "canceled")
                {
                    positions[i].Comment = "canceled";
                    _tab.CloseAllOrderToPosition(positions[i]);
                }
            }
            return;
        }
    }

    private void _tab_PositionOpeningSuccesEvent(Position obj)
    {
        if (RegimeLogicEntry.ValueString != "On new trade")
        {
            return;
        }

        CloseLogic();
    }

    private void _tab_PositionClosingSuccesEvent(Position obj)
    {
        if (RegimeLogicEntry.ValueString != "On new trade")
        {
            return;
        }

        TradeLogic();
    }

    void _tab_CandleFinishedEvent(List<Candle> obj)
    {
        if (RegimeLogicEntry.ValueString != "On new trade")
        {
            return;
        }

        TradeLogic();
    }

    void _tab_NewTickEvent(Trade trade)
    {
        if (Regime.ValueString == "Off"
            || Regime.ValueString == "Выключен")
        {
            return;
        }

        if (RegimeLogicEntry.ValueString != "On new trade")
        {
            return;
        }

        if (_lastTradePrice == trade.Price)
        {
            return;
        }

        _lastTradePrice = trade.Price;

        if (_tab.PositionsOpenAll.Count != 0)
        {
            CloseLogic();
        }

        TradeLogic();
    }

    private decimal _lastTradePrice = 0;

    void TradeLogic()
    {
        if (_tab.IsReadyToTrade == false ||
           _tab.IsConnected == false)
        {
            return;
        }

        if (IsBlockNonTradePeriods(_tab.TimeServerCurrent))
        {
            return;
        }

        // 1 отзыв ордеров на случай отключения сетки

        List<Position> positions = _tab.PositionsOpenAll;

        if (Regime.ValueString == "Off"
            || Regime.ValueString == "Выключен")
        {
            for (int i = positions.Count - 1; i >= 0; i--)
            {
                if (positions[i].State == PositionStateType.Opening
                    && positions[i].Comment != "canceled")
                {
                    positions[i].Comment = "canceled";
                    _tab.CloseAllOrderToPosition(positions[i]);
                }
            }
            return;
        }
        if (_tab.IsConnected == false)
        {
            return;
        }

        // 2 отзыв ордеров на случай отключения отдельных линий

        for (int i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].IsOn == true)
            {
                continue;
            }

            for (int j = positions.Count - 1; j >= 0; j--)
            {
                if (positions[j].OpenOrders[0].Price != Lines[i].PriceEnter)
                {
                    continue;
                }

                if (positions[j].State == PositionStateType.Opening
                    && positions[j].Comment != "canceled")
                {
                    positions[j].Comment = "canceled";
                    _tab.CloseAllOrderToPosition(positions[j]);
                }
            }
        }

        // 3 логика открытия позиции

        for (int i = 0; i < Lines.Count; i++)
        {
            if (Lines[i].IsOn == false)
            {
                continue;
            }

            decimal entryPrice = Lines[i].PriceEnter;

            if (GridSide == Side.Buy &&
                entryPrice > _tab.PriceBestAsk)
            {
                continue;
            }

            if (GridSide == Side.Sell &&
                entryPrice < _tab.PriceBestBid)
            {
                continue;
            }

            if (positions.Find(p => p.OpenOrders[0].Price == entryPrice) != null)
            {
                continue;
            }

            bool isClosestToMarket = true;

            if (GridSide == Side.Buy
                && positions.Find(p => p.OpenOrders[0].Price > entryPrice) != null)
            {
                if (positions.FindAll(p => p.OpenOrders[0].Price > entryPrice).Count >= MaxOpenOrdersInMarket.ValueInt)
                {
                    isClosestToMarket = false;
                }
            }
            if (GridSide == Side.Sell
                && positions.Find(p => p.OpenOrders[0].Price < entryPrice) != null)
            {
                if (positions.FindAll(p => p.OpenOrders[0].Price < entryPrice).Count >= MaxOpenOrdersInMarket.ValueInt)
                {
                    isClosestToMarket = false;
                }
            }

            if (isClosestToMarket == false)
            {
                // Если ордер в стороне от рынка, то проверяем на кол-во ордеров в рынке
                List<Position> openingPoses = positions.FindAll(p => p.State == PositionStateType.Opening);

                if (openingPoses != null &&
                    openingPoses.Count == MaxOpenOrdersInMarket.ValueInt)
                {
                    continue;
                }

                if (openingPoses != null &&
                    openingPoses.Count > MaxOpenOrdersInMarket.ValueInt)
                {
                    Position posToCancel = null;

                    for (int j = 0; j < openingPoses.Count; j++)
                    {
                        if (posToCancel == null)
                        {
                            posToCancel = openingPoses[j];
                            continue;
                        }

                        if (GridSide == Side.Buy
                            && openingPoses[j].OpenOrders[0].Price < posToCancel.OpenOrders[0].Price)
                        {
                            posToCancel = openingPoses[j];
                        }
                        if (GridSide == Side.Sell
                           && openingPoses[j].OpenOrders[0].Price > posToCancel.OpenOrders[0].Price)
                        {
                            posToCancel = openingPoses[j];
                        }
                    }

                    if (posToCancel != null
                        && posToCancel.Comment != "canceled")
                    {
                        posToCancel.Comment = "canceled";
                        _tab.CloseAllOrderToPosition(posToCancel);
                    }

                    continue;
                }
            }

            if (GridSide == Side.Buy)
            {
                _tab.BuyAtLimit(GetVolume(Lines[i]), entryPrice, entryPrice.ToString());
            }
            else if (GridSide == Side.Sell)
            {
                _tab.SellAtLimit(GetVolume(Lines[i]), entryPrice, entryPrice.ToString());
            }
        }

        // 4 Проверяем лишние ордера, если пользователь переставил сетку

        for (int i = 0; i < positions.Count; i++)
        {
            Position curPosition = positions[i];

            if (curPosition.State != PositionStateType.Opening)
            {
                continue;
            }

            if (curPosition.OpenOrders == null ||
                curPosition.OpenOrders.Count == 0)
            {
                continue;
            }

            Order openOrder = curPosition.OpenOrders[0];

            bool isInArray = false;

            for (int j = 0; j < Lines.Count; j++)
            {
                if (Lines[j].PriceEnter == openOrder.Price)
                {
                    isInArray = true;
                    break;
                }
            }
            if (isInArray == false
                && curPosition.Comment != "canceled")
            {
                curPosition.Comment = "canceled";
                _tab.CloseAllOrderToPosition(curPosition);
            }
        }
    }

    void CloseLogic()
    {
        if (Regime.ValueString == "Off"
            || Regime.ValueString == "Выключен")
        {
            return;
        }

        if (IsBlockNonTradePeriods(_tab.TimeServerCurrent))
        {
            return;
        }

        List<Position> positions = _tab.PositionsOpenAll;

        try
        {
            for (int i = 0; i < positions.Count; i++)
            {
                if (positions[i].State != PositionStateType.Open &&
                    positions[i].State != PositionStateType.ClosingFail)
                {
                    continue;
                }

                if (positions[i].WaitVolume != 0)
                {
                    continue;
                }

                decimal priceExit = 0;

                if (positions[i].Direction == Side.Buy)
                {
                    priceExit = positions[i].EntryPrice + (ProfitPercent * positions[i].EntryPrice / 100);
                }
                else
                {
                    priceExit = positions[i].EntryPrice - (ProfitPercent * positions[i].EntryPrice / 100);
                }

                if (positions[i].CloseActiv)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(positions[i].SignalTypeClose) == false)
                {
                    try
                    {
                        DateTime lastCloseTime = Convert.ToDateTime(positions[i].SignalTypeClose);

                        if (lastCloseTime.AddSeconds(30) > DateTime.Now)
                        {
                            continue;
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _tab.CloseAtLimit(positions[i], priceExit, positions[i].OpenVolume, DateTime.Now.ToString());
            }
        }
        catch (Exception e)
        {
            return;
        }
    }

    private decimal GetVolume(GridBotLine paramFromLine)
    {
        decimal volume = 0;
        decimal volumeFromLine = paramFromLine.Volume;
        decimal priceEnterForLine = paramFromLine.PriceEnter;

        if (TypeVolume == Type_Volume.ContractCurrency) // "Валюта контракта"
        {
            decimal contractPrice = priceEnterForLine;
            volume = Math.Round(volumeFromLine / contractPrice, _tab.Securiti.DecimalsVolume);
            return volume;
        }
        else// "Кол-во контрактов
        {
            return paramFromLine.Volume;
        }

    }

    #endregion
}

public class GridBotLine
{
    public bool IsOn;

    public decimal PriceEnter;

    public decimal Volume;

    public Side Side;

    public CheckState checkStateLine;

    public decimal PriceExit;

    public string GetSaveStr()
    {
        string result = "";

        result += IsOn + "|";
        result += PriceEnter + "|";
        result += Volume + "|";
        result += Side + "|";
        result += PriceExit + "|";

        return result;
    }

    public void SetFromStr(string str)
    {
        string[] saveArray = str.Split('|');

        IsOn = Convert.ToBoolean(saveArray[0]);
        PriceEnter = saveArray[1].ToDecimal();
        Volume = saveArray[2].ToDecimal();
        Enum.TryParse(saveArray[3], out Side);
        PriceExit = saveArray[4].ToDecimal();
    }
}

public class LanguageBase
{
    public string CurLocalizationCode
    {
        get
        {
            if (_curLocalizationCode == null)
            {
                _curLocalizationCode = OsLocalization.CurLocalizationCode;
            }
            return _curLocalizationCode;
        }
    }

    private string _curLocalizationCode;

    //--------------
    public string GridHeaderOrder0
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Ордер активен?";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Order is active?";
            }
        }
    }

    public string GridHeaderOrder1
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Направление сделки";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Order direction";
            }
        }
    }

    public string GridHeaderOrder2
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Цена первого ордера";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "First order price";
            }
        }
    }

    public string GridHeaderOrder3
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Кол-во ордеров в сетке";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Orders count";
            }
        }
    }

    public string GridHeaderOrder4
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Шаг ордера";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Orders step";
            }
        }
    }

    public string GridHeaderOrder5
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Тип объема";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Volume type";
            }
        }
    }

    public string GridHeaderOrder6
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Объем ордера";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Volume in order";
            }
        }
    }

    public string GridHeaderOrder7
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Профит от цены ордера в %";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Profit percent";
            }
        }
    }

    //--------------

    public string GridLineHeader0
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Номер";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Number";
            }
        }
    }

    public string GridLineHeader1
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Ордер активен?";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Order is active?";
            }
        }
    }

    public string GridLineHeader2
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Цена входа";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Entry price";
            }
        }
    }

    public string GridLineHeader3
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Цена выхода";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Exit price";
            }
        }
    }

    public string GridLineHeader4
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Объём";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Volume";
            }
        }
    }

    public string GridLineHeader5
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Направление сделки";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Direction";
            }
        }
    }

    public string GridLineHeader6
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Выбрать";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Select";
            }
        }
    }

    //--------------

    public string ButtonCreateTable
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Создать сетку ордеров";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Create Grid";
            }
        }
    }

    public string ButtonDeleteAllLine
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Удалить сетку целиком";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Delete Grid";
            }
        }
    }

    public string ButtonCreateLine
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Создать уровень";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Create level in grid";
            }
        }
    }

    public string ButtonDeleteSelectLine
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Удалить выбранный уровень";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Remove selected level";
            }
        }
    }

    //--------------

    public string BotSettingTabWindows
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return " Настройки бота ";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return " Bot settings ";
            }
        }
    }

    public string OrderGridTab
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return " Настройка сетки ордеров ";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return " Grid settings ";
            }
        }
    }

    public string BaseSettingsTab
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return " Основные настройки ";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return " Base settings ";
            }
        }
    }

    //--------------

    public string TypeVolumeContractCurrency
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Валюта контракта \n";
            }
            else //if(CurLocalizationCode== "en-US")
            {
                return "Contract currency \n";
            }
        }
    }

    public string TypeVolumeNumberOfContracts
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Количество контрактов \n";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Contract count \n";
            }
        }
    }

    //--------------

    public string Regime
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Режим работы";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Regime";
            }
        }
    }

    public string RegimeLogicEntry
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Режим входа в логику";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Regime logic entry";
            }
        }
    }

    public string MaxOpenOrdersInMarket
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Макс. ордеров в рынке";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Max orders in market";
            }
        }
    }

    //--------------

    public string NonTradePeriodsTab
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Не торговые периоды";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Non trade periods";
            }
        }
    }

    public string NonTradePeriodsParamOnOff
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Блокировка торгов. Период ";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Block trade. Period ";
            }
        }
    }

    public string NonTradePeriodsParamStart
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Старт периода ";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Start period ";
            }
        }
    }

    public string NonTradePeriodsParamEnd
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Конец периода ";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "End period ";
            }
        }
    }

    //--------------

    public string Message1
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Сетка не создана. Профит не выставлен!";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "The grid has not been created. Profit is not set!";
            }
        }
    }

    public string Message2
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Сетка не создана. Первая цена не указана!";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "The grid has not been created. First price is not specified!";
            }
        }
    }

    public string Message3
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Сетка не создана. Кол-во ордеров в сетке не указано!";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "The grid has not been created. The number of orders in the grid is not specified!";
            }
        }
    }

    public string Message4
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Сетка не создана. Шаг ордера не указан!";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "The grid has not been created. Price step is not specified!";
            }
        }
    }

    public string Message5
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Вы уверены что хотите новую сетку? Старая будет уничтожена!";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Are you sure you want a new grid? The old one will be destroyed!";
            }
        }
    }

    public string Message6
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Вы уверены что хотите удалить сетку? Данные будут уничтожены, все открытые ранее позиции надо будет закрыть вручную!";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Are you sure you want to delete the grid? The data will be destroyed, all previously opened positions will have to be closed manually!";
            }
        }
    }

    public string Message7
    {
        get
        {
            if (CurLocalizationCode == "ru-RU")
            {
                return "Вы уверены что хотите удалить уровень сетки? Это изменение не обратимо!";
            }
            else //if(CurLocalizationCode == "en-US")
            {
                return "Are you sure you want to remove the grid level? This change is not reversible!";
            }
        }
    }

}

public enum Type_Volume
{
    ContractCurrency,
    NumberOfContracts,
}