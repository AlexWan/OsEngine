using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System;


namespace OsEngine.Robots.TechSapmles
{
    [Bot("CustomParamsUseBotSample")]
    public class CustomParamsUseBotSample : BotPanel
    {
        public CustomParamsUseBotSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            Regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" }, "Base");

            //делаем простотую надпись, которая будет разделять параметры в таблице
            CreateParameterLabel("labelSample", "Base", "Params", 30, 15, System.Drawing.Color.White, "Base");

            AtrCountToInter = CreateParameter("Atr count", 4m, 1, 10, 1, "Base");

            Volume = CreateParameter("Volume", 1, 1, 100000, 1m, "Base");

            FastSmaLen = CreateParameter("Fast sma len", 15, 5, 50, 1, "Indicators");

            SlowSmaLen = CreateParameter("Slow sma len", 50, 5, 50, 1, "Indicators");

            AtrLen = CreateParameter("Atr len", 50, 5, 50, 1, "Indicators");

            StopLenPercent = CreateParameter("Stop percent", 0.5m, 1, 10, 1, "Exit settings");

            ProfitLenPercent = CreateParameter("Profit percent", 0.5m, 1, 10, 1, "Exit settings");

            _fastSma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _fastSma = (Aindicator)_tab.CreateCandleIndicator(_fastSma, "Prime");
            _fastSma.ParametersDigit[0].Value = FastSmaLen.ValueInt;
            _fastSma.Save();

            _slowSma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaSlow", false);
            _slowSma = (Aindicator)_tab.CreateCandleIndicator(_slowSma, "Prime");
            _slowSma.ParametersDigit[0].Value = SlowSmaLen.ValueInt;
            _slowSma.Save();

            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "art", false);
            _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "AtrArea");
            _atr.ParametersDigit[0].Value = AtrLen.ValueInt;
            _atr.Save();

            ParametrsChangeByUser += CustomParamsUseBotSample_ParametrsChangeByUser;

            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;

            // customization param ui

            this.ParamGuiSettings.Title = "Custom param gui sample";
            this.ParamGuiSettings.Height = 800;
            this.ParamGuiSettings.Width = 600;

            // 1 tab creation
            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Indicators values");

            // 2 add on custom tab children control
            // customTab.GridToPaint - it`s a control for your children controls
            CreateTable();
            customTab.AddChildren(_host);
        }

        #region work with grid

        WindowsFormsHost _host;

        private void CreateTable()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateTable));
                return;
            }

            _host = new WindowsFormsHost();

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, 
                DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "Time";
            colum0.ReadOnly = true;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewColumn colum01 = new DataGridViewColumn();
            colum01.CellTemplate = cell0;
            colum01.HeaderText = "Slow Sma";
            colum01.ReadOnly = true;
            colum01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum01);

            DataGridViewColumn colum02 = new DataGridViewColumn();
            colum02.CellTemplate = cell0;
            colum02.HeaderText = "Fast Sma";
            colum02.ReadOnly = true;
            colum02.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum02);

            DataGridViewColumn colum03 = new DataGridViewColumn();
            colum03.CellTemplate = cell0;
            colum03.HeaderText = "Atr";
            colum03.ReadOnly = true;
            colum03.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum03);

            DataGridViewColumn colum04 = new DataGridViewColumn();
            colum04.CellTemplate = cell0;
            colum04.HeaderText = "Len in Atr";
            colum04.ReadOnly = true;
            colum04.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum04);

            _host.Child = newGrid;
            _grid = newGrid;
        }

        private DataGridView _grid;

        private void PaintTable(List<Candle> candles)
        {
            if(_grid.InvokeRequired)
            {
                _grid.Invoke(new Action<List<Candle>>(PaintTable), candles);
                return;
            }

            _grid.Rows.Clear();

            List<decimal> smaFast = _fastSma.DataSeries[0].Values;
            List<decimal> smaSlow = _slowSma.DataSeries[0].Values;
            List<decimal> atr = _atr.DataSeries[0].Values;


            for (int i = 0;i < candles.Count;i++)
            {
                _grid.Rows.Add(GetRow(candles[i], smaSlow[i], smaFast[i], atr[i]));

            }

        }

        private DataGridViewRow GetRow(Candle candle, decimal slowSma, decimal fastSma, decimal atr)
        {
            DataGridViewRow row = new DataGridViewRow();
            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = candle.TimeStart.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[1].Value = slowSma.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[2].Value = fastSma.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[3].Value = atr.ToString();

            row.Cells.Add(new DataGridViewTextBoxCell());

            if(atr != 0)
            {
                row.Cells[4].Value = (fastSma - slowSma) / atr;
            }
            

            return row;
        }

        #endregion 

        private void CustomParamsUseBotSample_ParametrsChangeByUser()
        {
            _fastSma.ParametersDigit[0].Value = FastSmaLen.ValueInt;
            _slowSma.ParametersDigit[0].Value = SlowSmaLen.ValueInt;
            _atr.ParametersDigit[0].Value = AtrLen.ValueInt;

            _fastSma.Reload();
            _slowSma.Reload();
            _atr.Reload();
        }

        BotTabSimple _tab;

        StrategyParameterString Regime;

        StrategyParameterDecimal AtrCountToInter;

        StrategyParameterDecimal Volume;

        StrategyParameterInt FastSmaLen;

        StrategyParameterInt SlowSmaLen;

        StrategyParameterInt AtrLen;

        StrategyParameterDecimal StopLenPercent;

        StrategyParameterDecimal ProfitLenPercent;

        Aindicator _fastSma;

        Aindicator _slowSma;

        Aindicator _atr;

        public override string GetNameStrategyType()
        {
            return "CustomParamsUseBotSample";
        }

        public override void ShowIndividualSettingsDialog()
        {
           
        }

        // trade logic

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            PaintTable(candles);

            if (Regime.ValueString =="Off")
            {
                return;
            }

            if(candles.Count < AtrLen.ValueInt ||
                candles.Count < FastSmaLen.ValueInt ||
                candles.Count < SlowSmaLen.ValueInt)
            {
                return;
            }

            List<Position> positions = _tab.PositionsOpenAll;

            if(positions.Count == 0)
            {
                // openPos logic
                OpenPosLogic(candles);
            }
            else
            {
                // close logic
                ClosePosLogic(candles, positions[0]);
            }
        }

        private void OpenPosLogic(List<Candle> candles)
        {
            decimal lastFastSma = _fastSma.DataSeries[0].Last;
            decimal lastSlowSma = _slowSma.DataSeries[0].Last;

            if(lastFastSma < lastSlowSma)
            {
                return;
            }

            decimal lastAtr = _atr.DataSeries[0].Last;
            decimal len = lastFastSma - lastSlowSma;

            decimal lenCount = len / lastAtr;

            if(lenCount < AtrCountToInter.ValueDecimal)
            {
                return;
            }

            _tab.BuyAtMarket(Volume.ValueDecimal);
        }

        private void ClosePosLogic(List<Candle> candles, Position pos)
        {
            if(pos.State != PositionStateType.Open)
            {
                return;
            }

            if(pos.StopOrderIsActiv == true ||
                pos.ProfitOrderIsActiv == true)
            {
                return;
            }

            decimal profitActivation = pos.EntryPrice + pos.EntryPrice * ProfitLenPercent.ValueDecimal / 100;

            _tab.CloseAtProfit(pos, profitActivation, profitActivation);

            decimal stopActivation = pos.EntryPrice - pos.EntryPrice * StopLenPercent.ValueDecimal / 100;

            _tab.CloseAtStop(pos, stopActivation, stopActivation);
        }
    }
}