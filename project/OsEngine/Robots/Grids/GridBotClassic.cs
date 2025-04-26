﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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
using System.Threading;
using System.Linq;

[Bot("GridBotClassic")]
public class GridBotClassic : BotPanel
{
    #region Parameters and service

    public override string GetNameStrategyType()
    {
        return "GridBotClassic";
    }

    public override void ShowIndividualSettingsDialog()
    {

    }

    public GridBotClassic(string name, StartProgram startProgram)
          : base(name, startProgram)
    {
        Regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" }, " Base settings ");
        MaxOpenOrdersInMarket = CreateParameter("Max orders in market", 5, 1, 20, 1, " Base settings ");
        RegimeLogicEntry = CreateParameter("Regime logic entry", "On new trade", new string[] { "On new trade", "Once per second" }, " Base settings ");

        AutoClearJournalIsOn = CreateParameter("Auto clear journal is on", true, " Base settings ");
        MaxClosePositionsInJournal = CreateParameter("Max close positions in journal", 50, 1, 10, 1, " Base settings ");

        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        _tab.ManualPositionSupport.SecondToOpenIsOn = false;
        _tab.ManualPositionSupport.SecondToCloseIsOn = false;

        LoadSettings();
        LoadLines();

        // customization param ui

        this.ParamGuiSettings.Title = " Bot settings ";
        this.ParamGuiSettings.Height = 800;
        this.ParamGuiSettings.Width = 780;

        // grid to orders

        CustomTabToParametersUi customTabOrderGrid = ParamGuiSettings.CreateCustomTab(" Grid settings ");
        CreateGrid();
        UpdateAllTable();
        customTabOrderGrid.AddChildren(_hostGrid);

        // non trade periods

        NonTradePeriod1OnOff
            = CreateParameter("Block trade. Period " + "1",
            "Off", new string[] { "Off", "On" }, "Non trade periods");
        NonTradePeriod1Start = CreateParameterTimeOfDay("Start period " + "1", 9, 0, 0, 0, "Non trade periods");
        NonTradePeriod1End = CreateParameterTimeOfDay("End period " + "1", 10, 5, 0, 0, "Non trade periods");

        NonTradePeriod2OnOff
            = CreateParameter("Block trade. Period " + "2",
            "Off", new string[] { "Off", "On" }, "Non trade periods");
        NonTradePeriod2Start = CreateParameterTimeOfDay("Start period " + "2", 13, 55, 0, 0, "Non trade periods");
        NonTradePeriod2End = CreateParameterTimeOfDay("End period " + "2", 14, 5, 0, 0, "Non trade periods");

        NonTradePeriod3OnOff
            = CreateParameter("Block trade. Period " + "3",
            "Off", new string[] { "Off", "On" }, "Non trade periods");
        NonTradePeriod3Start = CreateParameterTimeOfDay("Start period " + "3", 18, 40, 0, 0, "Non trade periods");
        NonTradePeriod3End = CreateParameterTimeOfDay("End period " + "3", 19, 5, 0, 0, "Non trade periods");

        NonTradePeriod4OnOff
            = CreateParameter("Block trade. Period " + "4",
            "Off", new string[] { "Off", "On" }, "Non trade periods");
        NonTradePeriod4Start = CreateParameterTimeOfDay("Start period " + "4", 23, 40, 0, 0, "Non trade periods");
        NonTradePeriod4End = CreateParameterTimeOfDay("End period " + "4", 23, 59, 0, 0, "Non trade periods");

        // events

        _gridDataGrid.CellClick += EventСlickOnButtonCreatingLine;
        _gridDataGrid.DataError += _gridDataGrid_DataError;
        this.DeleteEvent += Strategy_DeleteEvent;
        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        _tab.NewTickEvent += _tab_NewTickEvent;
        _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccessEvent;
        _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
        _tab.ManualPositionSupport.DisableManualSupport();
        this.ParametrsChangeByUser += GridBot_ParametrsChangeByUser;

        Thread worker = new Thread(WorkerThreadArea);
        worker.Start();

        Description = "An example of a grid robot with rich customizations.";
    }

    private BotTabSimple _tab;

    public StrategyParameterString Regime;

    public StrategyParameterString RegimeLogicEntry;

    public StrategyParameterInt MaxOpenOrdersInMarket;

    public StrategyParameterBool AutoClearJournalIsOn;

    public StrategyParameterInt MaxClosePositionsInJournal;

    public bool LineIsOn;

    public Side GridSide;

    public decimal FirstPrice;

    public int LineCountStart;

    public decimal LineStep;

    public Type_Volume TypeVolume;

    public Type_Profit TypeProfit;

    public decimal StartVolume = 1;

    public decimal ProfitStep;

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
                writer.WriteLine(ProfitStep);
                writer.WriteLine(TypeProfit);
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
                ProfitStep = reader.ReadLine().ToDecimal();

                Enum.TryParse(reader.ReadLine(),out TypeProfit);

                reader.Close();
            }
        }
        catch
        {
            // ignore
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

    private WindowsFormsHost _hostGrid;

    private void CreateGrid()
    {
        try
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateGrid));
                return;
            }

            _hostGrid = new WindowsFormsHost();

            _gridDataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                   DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
            _gridDataGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _gridDataGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridDataGrid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _gridDataGrid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cellParam0 = new DataGridViewTextBoxCell();
            cellParam0.Style = _gridDataGrid.DefaultCellStyle;
            cellParam0.Style.WrapMode = DataGridViewTriState.True;

            DataGridViewColumn newColumn0 = new DataGridViewColumn();
            newColumn0.CellTemplate = cellParam0;
            newColumn0.HeaderText = "Order is active?";
            _gridDataGrid.Columns.Add(newColumn0);
            newColumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn1 = new DataGridViewColumn();
            newColumn1.CellTemplate = cellParam0;
            newColumn1.HeaderText = "Order direction";
            _gridDataGrid.Columns.Add(newColumn1);
            newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn2 = new DataGridViewColumn();
            newColumn2.CellTemplate = cellParam0;
            newColumn2.HeaderText = "First order price";
            _gridDataGrid.Columns.Add(newColumn2);
            newColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn3 = new DataGridViewColumn();
            newColumn3.CellTemplate = cellParam0;
            newColumn3.HeaderText = "Orders count";
            _gridDataGrid.Columns.Add(newColumn3);
            newColumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn4 = new DataGridViewColumn();
            newColumn4.CellTemplate = cellParam0;
            newColumn4.HeaderText = "Orders step";
            _gridDataGrid.Columns.Add(newColumn4);
            newColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn5 = new DataGridViewColumn();
            newColumn5.CellTemplate = cellParam0;
            newColumn5.HeaderText = "Volume type";
            _gridDataGrid.Columns.Add(newColumn5);
            newColumn5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn6 = new DataGridViewColumn();
            newColumn6.CellTemplate = cellParam0;
            newColumn6.HeaderText = "Volume in order";
            _gridDataGrid.Columns.Add(newColumn6);
            newColumn6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn7 = new DataGridViewColumn();
            newColumn7.CellTemplate = cellParam0;
            newColumn7.HeaderText = "Profit type";
            _gridDataGrid.Columns.Add(newColumn7);
            newColumn7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            DataGridViewColumn newColumn8 = new DataGridViewColumn();
            newColumn8.CellTemplate = cellParam0;
            newColumn8.HeaderText = "Profit";
            _gridDataGrid.Columns.Add(newColumn8);
            newColumn8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _hostGrid.Child = _gridDataGrid;

            _gridDataGrid.CellValueChanged += EventChangeValueInTable;
        }
        catch (Exception ex)
        {
            _tab.SetNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
        }
    }

    private void UpdateAllTable()
    {
        if (_gridDataGrid.InvokeRequired)
        {
            _gridDataGrid.Invoke(new Action(UpdateAllTable));
            return;
        }

        try
        {
            _gridDataGrid.CellValueChanged -= EventChangeValueInTable;
            _gridDataGrid.Rows.Clear();

            _gridDataGrid.Rows.Add(CreateFirstRow());
            _gridDataGrid.Rows.Add(CreateSpaceRow());
            _gridDataGrid.Rows.Add(CreateRowButton());
            _gridDataGrid.Rows.Add(CreateSpaceRow());
            _gridDataGrid.Rows.Add(CreateLineRowHeaders());

            CreateLineRows();

            _gridDataGrid.CellValueChanged += EventChangeValueInTable;
        }
        catch (Exception ex)
        {
            _tab.SetNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
        }
    }

    private DataGridViewRow CreateFirstRow()
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
        volumeTypeTextBox.Items.Add(Type_Volume.Currency.ToString());
        volumeTypeTextBox.Items.Add(Type_Volume.Contracts.ToString());
        row.Cells.Add(volumeTypeTextBox);

        DataGridViewTextBoxCell startVolumeTextBox = new DataGridViewTextBoxCell();
        row.Cells.Add(startVolumeTextBox);

        DataGridViewComboBoxCell profitTypeTextBox = new DataGridViewComboBoxCell();
        profitTypeTextBox.Items.Add(Type_Profit.Absolute.ToString());
        profitTypeTextBox.Items.Add(Type_Profit.Percent.ToString());
        row.Cells.Add(profitTypeTextBox);

        DataGridViewTextBoxCell profitPercentTextBox = new DataGridViewTextBoxCell();
        row.Cells.Add(profitPercentTextBox);

        try
        {
            regimeIsOnBox.Value = LineIsOn.ToString();
            sideBox.Value = GridSide == OsEngine.Entity.Side.Buy ? "Buy" : "Sell";
            firstOrderPriceTextBox.Value = FirstPrice.ToString();
            lineCountTextBox.Value = LineCountStart.ToString();
            lineStepTextBox.Value = LineStep.ToString();
            volumeTypeTextBox.Value = TypeVolume.ToString();
            startVolumeTextBox.Value = StartVolume.ToString();
            profitTypeTextBox.Value = TypeProfit.ToString();

            profitPercentTextBox.Value = ProfitStep.ToString();
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
        row.Cells[0].Value = "Create Grid";
        row.Cells.Add(new DataGridViewTextBoxCell());

        row.Cells.Add(new DataGridViewButtonCell());
        row.Cells[2].Value = "Delete Grid";
        row.Cells.Add(new DataGridViewTextBoxCell());

        row.Cells.Add(new DataGridViewButtonCell());
        row.Cells[4].Value = "Create level in grid";
        row.Cells.Add(new DataGridViewTextBoxCell());

        row.Cells.Add(new DataGridViewButtonCell());
        row.Cells[6].Value = "Remove selected level";
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

    private DataGridViewRow CreateLineRowHeaders()
    {
        DataGridViewRow row = new DataGridViewRow();

        DataGridViewTextBoxCell newCell = new DataGridViewTextBoxCell();
        newCell.Value = "Number";
        row.Cells.Add(newCell);
        newCell.ReadOnly = true;

        DataGridViewTextBoxCell newCell1 = new DataGridViewTextBoxCell();
        newCell1.Value = "Order is active?";
        row.Cells.Add(newCell1);
        newCell1.ReadOnly = true;

        DataGridViewTextBoxCell newCell2 = new DataGridViewTextBoxCell();
        newCell2.Value = "Entry price";
        row.Cells.Add(newCell2);
        newCell2.ReadOnly = true;

        DataGridViewTextBoxCell newCell3 = new DataGridViewTextBoxCell();
        newCell3.Value = "Exit price";
        row.Cells.Add(newCell3);
        newCell3.ReadOnly = true;

        DataGridViewTextBoxCell newCell4 = new DataGridViewTextBoxCell();
        newCell4.Value = "Volume";
        row.Cells.Add(newCell4);
        newCell4.ReadOnly = true;

        DataGridViewTextBoxCell newCell5 = new DataGridViewTextBoxCell();
        newCell5.Value = "Direction";
        row.Cells.Add(newCell5);
        newCell5.ReadOnly = true;

        DataGridViewTextBoxCell newCell6 = new DataGridViewTextBoxCell();
        newCell6.Value = "Select";
        row.Cells.Add(newCell6);
        newCell6.ReadOnly = true;

        return row;
    }

    private void CreateLineRows()
    {
        if (_gridDataGrid.InvokeRequired)
        {
            _gridDataGrid.Invoke(new Action(CreateLineRows));
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
            cell1.ReadOnly = false;
            rowLine.Cells.Add(cell1);

            rowLine.Cells.Add(new DataGridViewTextBoxCell());
            rowLine.Cells[2].Value = Math.Round(Lines[i].PriceEnter, 10);
            rowLine.Cells[2].ReadOnly = false;

            rowLine.Cells.Add(new DataGridViewTextBoxCell());
            rowLine.Cells[3].Value = Math.Round(Lines[i].PriceExit, 10);
            rowLine.Cells[3].ReadOnly = false;

            rowLine.Cells.Add(new DataGridViewTextBoxCell());
            rowLine.Cells[4].Value = Math.Round(Lines[i].Volume, 10);
            rowLine.Cells[4].ReadOnly = false;

            rowLine.Cells.Add(new DataGridViewTextBoxCell());
            rowLine.Cells[5].Value = Lines[i].Side;

            rowLine.Cells.Add(new DataGridViewCheckBoxCell());
            rowLine.Cells[6].Value = Lines[i].checkStateLine;
            rowLine.Cells[6].ReadOnly = false;

            _gridDataGrid.Rows.Add(rowLine);
          
        }
    }

    private void EventСlickOnButtonCreatingLine(object sender, DataGridViewCellEventArgs clickCell)
    {
        try
        {
            if (clickCell.ColumnIndex == 0 && clickCell.RowIndex == 2)
            {
                if (ProfitStep <= 0)
                {
                    SendNewLogMessage("The grid has not been created. Profit is not set!",
                        OsEngine.Logging.LogMessageType.Error);
                    return;
                }
                if (FirstPrice <= 0)
                {
                    SendNewLogMessage("The grid has not been created. First price is not specified!",
                        OsEngine.Logging.LogMessageType.Error);
                    return;
                }
                if (LineCountStart <= 0)
                {
                    SendNewLogMessage("The grid has not been created. The number of orders in the grid is not specified!",
                        OsEngine.Logging.LogMessageType.Error);
                    return;
                }
                if (LineStep <= 0)
                {
                    SendNewLogMessage("The grid has not been created. Price step is not specified!",
                        OsEngine.Logging.LogMessageType.Error);
                    return;
                }

                if (Lines != null
                    && Lines.Count > 0)
                {
                    AcceptDialogUi ui = new AcceptDialogUi("Are you sure you want a new grid? The old one will be destroyed!");
                    ui.ShowDialog();
                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }
                }

                DeleteTable();
                CreateNewTable();
                UpdateAllTable();
            }
            else if (clickCell.ColumnIndex == 2 && clickCell.RowIndex == 2)
            {
                AcceptDialogUi ui
                    = new AcceptDialogUi("Are you sure you want to delete the grid? The data will be destroyed, all previously opened positions will have to be closed manually!");
                ui.ShowDialog();
                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                DeleteTable();
                UpdateAllTable();
            }
            else if (clickCell.ColumnIndex == 4 && clickCell.RowIndex == 2)
            {
                CreateNewLine();
                UpdateAllTable();
            }
            else if (clickCell.ColumnIndex == 6 && clickCell.RowIndex == 2)
            {
                AcceptDialogUi ui
                = new AcceptDialogUi("Are you sure you want to remove the grid level? This change is not reversible!");
                ui.ShowDialog();
                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                List<int> linesSelected = GetSelectedLines();

                for (int i = 0; i < linesSelected.Count; i++)
                {
                    DeleteSelectedLines(linesSelected[i] - i);
                }
               
                UpdateAllTable();
            }

            SaveSettings();
        }
        catch (Exception e)
        {
            _tab.SetNewLogMessage(e.ToString(), OsEngine.Logging.LogMessageType.Error);
        }
    }

    private List<int> GetSelectedLines()
    {
        List<int> numbers = new List<int>();

        for(int i = 5;i < _gridDataGrid.Rows.Count;i++)
        {
            DataGridViewCheckBoxCell cell = (DataGridViewCheckBoxCell)_gridDataGrid.Rows[i].Cells[6];

            if(cell.Value.ToString() == "Unchecked")
            {
                continue;
            }
            numbers.Add(i);
        }

        return numbers;
    }

    private void EventChangeValueInTable(object sender, DataGridViewCellEventArgs e)
    {
        try
        {
            LineIsOn = Convert.ToBoolean(_gridDataGrid.Rows[0].Cells[0].Value.ToString());

            Enum.TryParse(_gridDataGrid.Rows[0].Cells[1].Value.ToString(), out GridSide);

            FirstPrice = GetDecimal(FirstPrice, _gridDataGrid.Rows[0].Cells[2]);

            if (_tab.Security != null
                && FirstPrice % _tab.Security.PriceStep != 0)
            {
                FirstPrice = Math.Round(FirstPrice, _tab.Security.Decimals);
                _gridDataGrid.Rows[0].Cells[2].Value = FirstPrice.ToString();
            }

            LineCountStart = GetInt(LineCountStart, _gridDataGrid.Rows[0].Cells[3]);

            LineStep = GetDecimal(LineStep, _gridDataGrid.Rows[0].Cells[4]);

            if (_tab.Security != null
                && LineStep < _tab.Security.PriceStep)
            {
                LineStep = _tab.Security.PriceStep;
                _gridDataGrid.Rows[0].Cells[4].Value = LineStep.ToString();
            }

            if (_tab.Security != null
                && LineStep % _tab.Security.PriceStep != 0)
            {
                LineStep = Math.Round(LineStep, _tab.Security.Decimals);
                _gridDataGrid.Rows[0].Cells[4].Value = LineStep.ToString();
            }

            Enum.TryParse(_gridDataGrid.Rows[0].Cells[5].Value.ToString(), out TypeVolume);

            StartVolume = GetDecimal(StartVolume, _gridDataGrid.Rows[0].Cells[6]);

            Enum.TryParse(_gridDataGrid.Rows[0].Cells[7].Value.ToString(), out TypeProfit);

            ProfitStep = GetDecimal(ProfitStep, _gridDataGrid.Rows[0].Cells[8]);

            if (TypeProfit == Type_Profit.Absolute
                && _tab.Security != null
                && ProfitStep % _tab.Security.PriceStep != 0)
            {
                ProfitStep = Math.Round(ProfitStep, _tab.Security.Decimals);
                _gridDataGrid.Rows[0].Cells[8].Value = ProfitStep.ToString();
            }

            if (TypeProfit == Type_Profit.Absolute
                && _tab.Security != null
               && ProfitStep < _tab.Security.PriceStep)
            {
                ProfitStep = _tab.Security.PriceStep;
                _gridDataGrid.Rows[0].Cells[8].Value = ProfitStep.ToString();
            }

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
            catch (Exception ex)
            {
                _tab.SetNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }
        catch(Exception ex)
        {
            _tab.SetNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
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
                if (TypeProfit == Type_Profit.Percent)
                {
                    newLine.PriceExit = newLine.PriceEnter + (newLine.PriceEnter * ProfitStep / 100);

                    if(_tab.Security != null)
                    {
                        newLine.PriceExit = Math.Round(newLine.PriceExit, _tab.Security.Decimals);
                    }
                }
                else if (TypeProfit == Type_Profit.Absolute)
                {
                    newLine.PriceExit = newLine.PriceEnter + ProfitStep;
                }

                priceCurrent -= LineStep;
            }
            else if (GridSide == Side.Sell)
            {
                if (TypeProfit == Type_Profit.Percent)
                {
                    newLine.PriceExit = newLine.PriceEnter - (newLine.PriceEnter * ProfitStep / 100);

                    if (_tab.Security != null)
                    {
                        newLine.PriceExit = Math.Round(newLine.PriceExit, _tab.Security.Decimals);
                    }
                }
                else if (TypeProfit == Type_Profit.Absolute)
                {
                    newLine.PriceExit = newLine.PriceEnter - ProfitStep;
                }

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
            newLine.PriceExit = newLine.PriceEnter + (newLine.PriceEnter * ProfitStep / 100);
        }
        else if (GridSide == Side.Sell)
        {
            newLine.PriceExit = newLine.PriceEnter - (newLine.PriceEnter * ProfitStep / 100);
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

    private void _gridDataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
    {
        _tab.SetNewLogMessage(e.Exception.ToString(), OsEngine.Logging.LogMessageType.Error);
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

                if (StartProgram == StartProgram.IsTester)
                {
                    _tab.SetNewLogMessage("Can use this bot in tester", OsEngine.Logging.LogMessageType.Error);
                    return;
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

        if (StartProgram == StartProgram.IsTester)
        {
            return;
        }

        CloseLogic();
    }

    private void _tab_PositionClosingSuccessEvent(Position obj)
    {
        JournalAutoClear();

        if (RegimeLogicEntry.ValueString != "On new trade")
        {
            return;
        }

        if (StartProgram == StartProgram.IsTester)
        {
            return;
        }

        TradeLogic();
    }

    private void _tab_CandleFinishedEvent(List<Candle> obj)
    {
        if (RegimeLogicEntry.ValueString != "On new trade")
        {
            return;
        }

        TradeLogic();
    }

    private void _tab_NewTickEvent(Trade trade)
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

        if (StartProgram == StartProgram.IsTester)
        {
            _tab.SetNewLogMessage("Can use this bot in tester", OsEngine.Logging.LogMessageType.Error);
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

    private void TradeLogic()
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

        if (Lines.Count == 0)
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
                    if (string.IsNullOrEmpty(positions[i].OpenOrders[0].NumberMarket) == false)
                    {
                        positions[i].Comment = "canceled";
                        _tab.CloseAllOrderToPosition(positions[i]);
                    }
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
                    if (string.IsNullOrEmpty(positions[j].OpenOrders[0].NumberMarket) == false)
                    {
                        positions[j].Comment = "canceled";
                        _tab.CloseAllOrderToPosition(positions[j]);
                    }
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
                if (positions.FindAll(p => p.OpenOrders[0].Price > entryPrice && p.State == PositionStateType.Opening).Count >= MaxOpenOrdersInMarket.ValueInt)
                {
                    isClosestToMarket = false;
                }
            }
            if (GridSide == Side.Sell
                && positions.Find(p => p.OpenOrders[0].Price < entryPrice && p.State == PositionStateType.Opening) != null)
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
                        if (string.IsNullOrEmpty(posToCancel.OpenOrders[0].NumberMarket) == false)
                        {
                            posToCancel.Comment = "canceled";
                            _tab.CloseAllOrderToPosition(posToCancel);
                        }
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
                if (string.IsNullOrEmpty(curPosition.OpenOrders[0].NumberMarket) == false)
                {
                    curPosition.Comment = "canceled";
                    _tab.CloseAllOrderToPosition(curPosition);
                }
            }
        }

        if (StartProgram == StartProgram.IsOsTrader)
        {
            // в реале, проверяем целостность сетки раз в три секунды
            if (_lastCheckOldOrders.AddSeconds(3) > DateTime.Now)
            {
                return;
            }
            _lastCheckOldOrders = DateTime.Now;
        }

        // 5 проверяем максимальное кол-во ордеров в рынке. Отзываем дальние

        if(positions.Count != 0)
        {
            List<Position> openingPoses = _tab.PositionsOpenAll.FindAll(p => p.State == PositionStateType.Opening);

            if(openingPoses.Count > 1)
            {
                if (openingPoses[0].Direction == Side.Buy)
                {
                    openingPoses = openingPoses.OrderBy(x => x.EntryPrice).ToList();
                    openingPoses.Reverse();
                }
                else if(openingPoses[0].Direction == Side.Sell)
                {
                    openingPoses = openingPoses.OrderBy(x => x.EntryPrice).ToList();
                    
                }
            }

            for (int i = MaxOpenOrdersInMarket.ValueInt; i < openingPoses.Count; i++)
            {
                Position curPosition = openingPoses[i];

                if (curPosition.Comment != "canceled")
                {
                    if (string.IsNullOrEmpty(curPosition.OpenOrders[0].NumberMarket) == false)
                    {
                        curPosition.Comment = "canceled";
                        _tab.CloseAllOrderToPosition(curPosition);
                    }
                }
            }
        }

        // 6 проверяем целостность сетки сверху вниз при Buy

        if (positions.Count != 0 &&
            Lines[0].Side == Side.Buy)
        {
            List<Position> openingPoses = positions.FindAll(p => p.State == PositionStateType.Opening);

            // A Мы отсортируем все позиции по цене открытия сверху вниз

            List<Position> sortPositions = new List<Position>();

            if (openingPoses != null &&
                openingPoses.Count > 0)
            {
                sortPositions.Add(openingPoses[0]);
            }

            for (int i = 1; i < openingPoses.Count; i++)
            {
                Position curPos = openingPoses[i];

                for (int j = 0; j < sortPositions.Count; j++)
                {
                    if (curPos.OpenOrders[0].Price > sortPositions[j].OpenOrders[0].Price)
                    {
                        sortPositions.Insert(j, curPos);
                        break;
                    }
                    else if (j + 1 == sortPositions.Count)
                    {
                        sortPositions.Add(curPos);
                        break;
                    }
                }
            }

            // B будем их смотреть по очереди. И если есть где-то пустота(с активной линией), снимаем всё что ниже пустоты

            if (sortPositions.Count != 0)
            {
                decimal upGridPrice = sortPositions[0].OpenOrders[0].Price;

                decimal allRemoveLessPrice = 0;

                for (int i = 1; i < sortPositions.Count; i++)
                {
                    decimal curOrderPrice = sortPositions[i].OpenOrders[0].Price;

                    if (upGridPrice - curOrderPrice == LineStep)
                    {
                        upGridPrice = curOrderPrice;
                        continue;
                    }

                    // C расстояние между ордерами больше. Ищем линии между

                    List<GridBotLine> linesBetwen = Lines.FindAll(l => l.PriceEnter < upGridPrice && l.PriceEnter > curOrderPrice);

                    // D если есть активные линии между ордерами. Ставим флаг об удалении всего что ниже
                    for (int j = 0; linesBetwen != null && j < linesBetwen.Count; j++)
                    {
                        if (linesBetwen[j].IsOn)
                        {
                            allRemoveLessPrice = upGridPrice;
                            break;
                        }
                    }

                    if (allRemoveLessPrice != 0)
                    {
                        break;
                    }
                    else
                    {
                        upGridPrice = curOrderPrice;
                    }
                }

                if (allRemoveLessPrice != 0)
                {
                    for (int i = 0; i < openingPoses.Count; i++)
                    {
                        Position curPosition = openingPoses[i];

                        if (curPosition.OpenOrders[0].Price >= allRemoveLessPrice)
                        {
                            continue;
                        }

                        if (curPosition.Comment != "canceled")
                        {
                            if (string.IsNullOrEmpty(curPosition.OpenOrders[0].NumberMarket) == false)
                            {
                                curPosition.Comment = "canceled";
                                _tab.CloseAllOrderToPosition(curPosition);
                            }
                        }
                    }
                }
            }
        }

        // 7 проверяем целостность сетки снизу вверх при Sell

        if (positions.Count != 0 &&
            Lines[0].Side == Side.Sell)
        {
            List<Position> openingPoses = positions.FindAll(p => p.State == PositionStateType.Opening);

            // A Мы отсортируем все позиции по цене открытия сверху вниз

            List<Position> sortPositions = new List<Position>();

            if (openingPoses != null &&
                openingPoses.Count > 0)
            {
                sortPositions.Add(openingPoses[0]);
            }

            for (int i = 1; i < openingPoses.Count; i++)
            {
                Position curPos = openingPoses[i];

                for (int j = 0; j < sortPositions.Count; j++)
                {
                    if (curPos.OpenOrders[0].Price < sortPositions[j].OpenOrders[0].Price)
                    {
                        sortPositions.Insert(j, curPos);
                        break;
                    }
                    else if (j + 1 == sortPositions.Count)
                    {
                        sortPositions.Add(curPos);
                        break;
                    }
                }
            }

            // B будем их смотреть по очереди. И если есть где-то пустота(с активной линией), снимаем всё что ниже пустоты

            if (sortPositions.Count != 0)
            {
                decimal lowGridPrice = sortPositions[0].OpenOrders[0].Price;

                decimal allRemoveUpPrice = 0;

                for (int i = 1; i < sortPositions.Count; i++)
                {
                    decimal curOrderPrice = sortPositions[i].OpenOrders[0].Price;

                    if (curOrderPrice - lowGridPrice == LineStep)
                    {
                        lowGridPrice = curOrderPrice;
                        continue;
                    }

                    // C расстояние между ордерами больше. Ищем линии между

                    List<GridBotLine> linesBetwen = Lines.FindAll(l => l.PriceEnter > lowGridPrice && l.PriceEnter < curOrderPrice);

                    // D если есть активные линии между ордерами. Ставим флаг об удалении всего что ниже
                    for (int j = 0; linesBetwen != null && j < linesBetwen.Count; j++)
                    {
                        if (linesBetwen[j].IsOn)
                        {
                            allRemoveUpPrice = lowGridPrice;
                            break;
                        }
                    }

                    if (allRemoveUpPrice != 0)
                    {
                        break;
                    }
                    else
                    {
                        lowGridPrice = curOrderPrice;
                    }
                }

                if (allRemoveUpPrice != 0)
                {
                    for (int i = 0; i < openingPoses.Count; i++)
                    {
                        Position curPosition = openingPoses[i];

                        if (curPosition.OpenOrders[0].Price <= allRemoveUpPrice)
                        {
                            continue;
                        }

                        if (curPosition.Comment != "canceled")
                        {
                            if (string.IsNullOrEmpty(curPosition.OpenOrders[0].NumberMarket) == false)
                            {
                                curPosition.Comment = "canceled";
                                _tab.CloseAllOrderToPosition(curPosition);
                            }
                        }
                    }
                }
            }
        }
    }

    private DateTime _lastCheckOldOrders = DateTime.MinValue;

    private void CloseLogic()
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

                if (positions[i].CloseActive)
                {
                    continue;
                }

                if (positions[i].EntryPrice == 0)
                {
                    continue;
                }

                decimal priceExit = 0;

                if (positions[i].Direction == Side.Buy)
                {
                    priceExit = positions[i].EntryPrice + (ProfitStep * positions[i].EntryPrice / 100);
                }
                else
                {
                    priceExit = positions[i].EntryPrice - (ProfitStep * positions[i].EntryPrice / 100);
                }

                decimal profitAbs = 0;

                if (positions[i].Direction == Side.Buy)
                {
                    profitAbs = priceExit - positions[i].EntryPrice;
                }
                else
                {
                    profitAbs = positions[i].EntryPrice - priceExit;
                }

                if (profitAbs <= 0 ||
                    profitAbs < _tab.Security.PriceStep)
                {
                    if (positions[i].Direction == Side.Buy)
                    {
                        priceExit = positions[i].EntryPrice + _tab.Security.PriceStep;
                    }
                    else
                    {
                        priceExit = positions[i].EntryPrice - _tab.Security.PriceStep;
                    }
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

        if (TypeVolume == Type_Volume.Currency) // "Валюта контракта"
        {
            decimal contractPrice = priceEnterForLine;
            volume = Math.Round(volumeFromLine / contractPrice, _tab.Security.DecimalsVolume);
            return volume;
        }
        else// "Кол-во контрактов
        {
            return paramFromLine.Volume;
        }

    }

    private void JournalAutoClear()
    {
        List<Position> positions = _tab.PositionsAll;

        // 1 удаляем позиции с OpeningFail без всяких условий

        for (int i = 0; i < positions.Count; i++)
        {
            Position pos = positions[i];
            if (pos.State == PositionStateType.OpeningFail)
            {
                _tab._journal.DeletePosition(pos);
                i--;
            }
        }

        if (AutoClearJournalIsOn.ValueBool == false)
        {
            return;
        }

        // 2 удаляем позиции со статусом Done, если пользователь это включил        

        int curDonePosInJournal = 0;

        for (int i = positions.Count - 1; i >= 0; i--)
        {
            Position pos = positions[i];

            if (pos.State != PositionStateType.Done)
            {
                continue;
            }

            curDonePosInJournal++;

            if (curDonePosInJournal > MaxClosePositionsInJournal.ValueInt)
            {
                _tab._journal.DeletePosition(pos);
                i--;
            }
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

public enum Type_Volume
{
    Contracts,
    Currency,
}

public enum Type_Profit
{
    Absolute,
    Percent,
}