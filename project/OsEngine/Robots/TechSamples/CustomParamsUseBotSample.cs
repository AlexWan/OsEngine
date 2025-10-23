/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

/* Description
TechSample robot for OsEngine

This is an example of working with custom settings for the design of the Options window.
 */

namespace OsEngine.Robots.TechSamples
{
    [Bot("CustomParamsUseBotSample")] // We create an attribute so that we don't write anything to the BotFactory
    public class CustomParamsUseBotSample : BotPanel
    {
        // Simple tabs
        private BotTabSimple _tab;

        // Basic settings
        private StrategyParameterString _regime;
        private StrategyParameterCheckBox _isOn;

        // Enter logic
        private StrategyParameterDecimal _atrCountToInter;

        // GetVolume settings
        private StrategyParameterString _volumeType;
        private StrategyParameterDecimal _volume;
        private StrategyParameterString _tradeAssetInPortfolio;

        // Indicator settings
        private StrategyParameterInt _fastSmaLen;
        private StrategyParameterInt _slowSmaLen;
        private StrategyParameterInt _atrLen;

        // Indicator
        private Aindicator _fastSma;
        private Aindicator _slowSma;
        private Aindicator _atr;

        // Exit settings
        private StrategyParameterDecimal _stopLenPercent;
        private StrategyParameterDecimalCheckBox _profitLenPercent;

        public CustomParamsUseBotSample(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // Create tabs
            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];

            // Basic settings
            _isOn = CreateParameterCheckBox("On/Off", true, "Base");
            _regime = CreateParameter("Regime", "Off", new string[] { "Off", "On" }, "Base");

            // we make a simple label that will separate the parameters in the table
            CreateParameterLabel("labelSample", "Base", "Params", 30, 15, System.Drawing.Color.White, "Base");

            // Enter logic
            _atrCountToInter = CreateParameter("Atr count", 4m, 1, 10, 1, "Base");

            // GetVolume settings
            _volumeType = CreateParameter("Volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" });
            _volume = CreateParameter("Volume", 20, 1.0m, 50, 4);
            _tradeAssetInPortfolio = CreateParameter("Asset in portfolio", "Prime");

            // Indicator settings
            _fastSmaLen = CreateParameter("Fast sma len", 15, 5, 50, 1, "Indicators");
            _slowSmaLen = CreateParameter("Slow sma len", 50, 5, 50, 1, "Indicators");
            _atrLen = CreateParameter("Atr len", 50, 5, 50, 1, "Indicators");

            // Exit settings
            _stopLenPercent = CreateParameter("Stop percent", 0.5m, 1, 10, 1, "Exit settings");
            _profitLenPercent = CreateParameterDecimalCheckBox("Profit percent", 0.5m, 1, 10, 1, true, "Exit settings");

            // Create indicator FastSma
            _fastSma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "Sma", false);
            _fastSma = (Aindicator)_tab.CreateCandleIndicator(_fastSma, "Prime");
            _fastSma.ParametersDigit[0].Value = _fastSmaLen.ValueInt;
            _fastSma.Save();

            // Create indicator SlowSma
            _slowSma = IndicatorsFactory.CreateIndicatorByName("Sma", name + "SmaSlow", false);
            _slowSma = (Aindicator)_tab.CreateCandleIndicator(_slowSma, "Prime");
            _slowSma.ParametersDigit[0].Value = _slowSmaLen.ValueInt;
            _slowSma.Save();

            // Create indicator ATR
            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", name + "art", false);
            _atr = (Aindicator)_tab.CreateCandleIndicator(_atr, "AtrArea");
            _atr.ParametersDigit[0].Value = _atrLen.ValueInt;
            _atr.Save();

            // Subscribe to the indicator update event
            ParametrsChangeByUser += CustomParamsUseBotSample_ParametrsChangeByUser;

            // Subscribe to the candle finished event
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

            Description = OsLocalization.Description.DescriptionLabel103;
        }

        #region work with grid

        WindowsFormsHost _host;

        // Create table
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

        // Paint table
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
            _fastSma.ParametersDigit[0].Value = _fastSmaLen.ValueInt;
            _slowSma.ParametersDigit[0].Value = _slowSmaLen.ValueInt;
            _atr.ParametersDigit[0].Value = _atrLen.ValueInt;

            _fastSma.Reload();
            _slowSma.Reload();
            _atr.Reload();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "CustomParamsUseBotSample";
        }

        // Show settings GUI
        public override void ShowIndividualSettingsDialog()
        {
           
        }

        // Trade logic
        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            PaintTable(candles);

            if (_isOn.CheckState == CheckState.Unchecked)
            {
                return;
            }

            if (_regime.ValueString =="Off")
            {
                return;
            }

            if(candles.Count < _atrLen.ValueInt ||
                candles.Count < _fastSmaLen.ValueInt ||
                candles.Count < _slowSmaLen.ValueInt)
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

        // Opening position logic
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

            if(lenCount < _atrCountToInter.ValueDecimal)
            {
                return;
            }

            _tab.BuyAtMarket(GetVolume(_tab));
        }

        // Close position logic
        private void ClosePosLogic(List<Candle> candles, Position pos)
        {
            if(pos.State != PositionStateType.Open)
            {
                return;
            }

            if(pos.StopOrderIsActive == true ||
                pos.ProfitOrderIsActive == true)
            {
                return;
            }

            if(_profitLenPercent.CheckState == CheckState.Checked)
            {
                decimal profitActivation = pos.EntryPrice + pos.EntryPrice * _profitLenPercent.ValueDecimal / 100;
                _tab.CloseAtProfit(pos, profitActivation, profitActivation);
            }

            decimal stopActivation = pos.EntryPrice - pos.EntryPrice * _stopLenPercent.ValueDecimal / 100;

            _tab.CloseAtStop(pos, stopActivation, stopActivation);
        }

        // Method for calculating the volume of entry into a position
        private decimal GetVolume(BotTabSimple tab)
        {
            decimal volume = 0;

            if (_volumeType.ValueString == "Contracts")
            {
                volume = _volume.ValueDecimal;
            }
            else if (_volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volume = _volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volume = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volume = Math.Round(volume, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volume = Math.Round(volume, 6);
                }
            }
            else if (_volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (_tradeAssetInPortfolio.ValueString == "Prime")
                {
                    portfolioPrimeAsset = myPortfolio.ValueCurrent;
                }
                else
                {
                    List<PositionOnBoard> positionOnBoard = myPortfolio.GetPositionOnBoard();

                    if (positionOnBoard == null)
                    {
                        return 0;
                    }

                    for (int i = 0; i < positionOnBoard.Count; i++)
                    {
                        if (positionOnBoard[i].SecurityNameCode == _tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + _tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (_volume.ValueDecimal / 100);

                decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

                if (tab.StartProgram == StartProgram.IsOsTrader)
                {
                    if (tab.Security.UsePriceStepCostToCalculateVolume == true
                     && tab.Security.PriceStep != tab.Security.PriceStepCost
                     && tab.PriceBestAsk != 0
                     && tab.Security.PriceStep != 0
                     && tab.Security.PriceStepCost != 0)
                    {// расчёт количества контрактов для фьючерсов и опционов на Мосбирже
                        qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
                    }
                    qty = Math.Round(qty, tab.Security.DecimalsVolume);
                }
                else
                {
                    qty = Math.Round(qty, 7);
                }

                return qty;
            }

            return volume;
        }
    }
}