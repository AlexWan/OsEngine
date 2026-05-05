/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Properties;
using OsEngine.Language;

namespace OsEngine.Robots.Monitors
{
    /*

    MonitorImpulse
    Монитор для анализа движений вниз и вверх по отдельным активам за N свечек
    Содержит в себе богатую визуальную часть, в которой видно таблицу движений по выбранным активам

    При фиксации определённого движения вверх или вниз, поддерживает:
    1) Сигналы визуальные и звуковые.
    2) Автоматическое открытие позиций. С закрытием по профиту и стопу. Или закрытие по времени

    */

    [Bot("MonitorImpulse")]
    public class MonitorImpulse : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Prime settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _candlesToAnalyze;
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        // Long
        private StrategyParameterBool _longIsOn;
        private StrategyParameterDecimal _longPercentMove;
        private StrategyParameterInt _longMaxPositions;
        private StrategyParameterDecimal _longStopPercent;
        private StrategyParameterDecimal _longProfitPercent;
        private StrategyParameterInt _longMaxSecondsInPositions;
        private StrategyParameterString _longVolumeType;
        private StrategyParameterDecimal _longVolume;
        private StrategyParameterString _longTradeAssetInPortfolio;

        // Short
        private StrategyParameterBool _shortIsOn;
        private StrategyParameterDecimal _shortPercentMove;
        private StrategyParameterInt _shortMaxPositions;
        private StrategyParameterDecimal _shortStopPercent;
        private StrategyParameterDecimal _shortProfitPercent;
        private StrategyParameterInt _shortMaxSecondsInPositions;
        private StrategyParameterString _shortVolumeType;
        private StrategyParameterDecimal _shortVolume;
        private StrategyParameterString _shortTradeAssetInPortfolio;

        // Signals
        private StrategyParameterBool _upSignalsIsOn;
        private StrategyParameterDecimal _upSignalsPercentMove;
        private StrategyParameterString _upSignalsMusic;
        private StrategyParameterBool _upSignalsErrorLogIsOn;
        private StrategyParameterBool _downSignalsIsOn;
        private StrategyParameterDecimal _downSignalsPercentMove;
        private StrategyParameterString _downSignalsMusic;
        private StrategyParameterBool _downSignalsErrorLogIsOn;

        public MonitorImpulse(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];
            _tabScreener.CandleUpdateEvent += _tabScreener_CandleUpdateEvent;
            _tabScreener.CandleFinishedEvent += _tabScreener_CandleFinishedEvent;
            _tabScreener.PositionOpeningSuccesEvent += _tabScreener_PositionOpeningSuccesEvent;

            // Prime settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "OnCandleUpdate", "OnCandleFinish" }, "Prime settings");
            _candlesToAnalyze = CreateParameter("Candles to analyze", 10, 0, 20, 1, "Prime settings");
            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Prime settings");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            // Long
            _longIsOn = CreateParameter("Long Is On", false, "Long");
            _longPercentMove = CreateParameter("Long move to entry", 1.4m, 0.1m, 1, 0.1m, "Long");
            _longMaxPositions = CreateParameter("Long max positions count", 3, 0, 20, 1, "Long");
            _longStopPercent = CreateParameter("Long stop percent", 0.8m, 0.1m, 1, 0.1m, "Long");
            _longProfitPercent = CreateParameter("Long profit percent", 0.8m, 0.1m, 1, 0.1m, "Long");
            _longMaxSecondsInPositions = CreateParameter("Long seconds in position", 0, 0, 20, 1, "Long");
            _longVolumeType = CreateParameter("Long volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Long");
            _longVolume = CreateParameter("Long volume", 20, 1.0m, 50, 4, "Long");
            _longTradeAssetInPortfolio = CreateParameter("Long asset in portfolio", "Prime", "Long");

            // Short
            _shortIsOn = CreateParameter("Short Is On", false, "Short");
            _shortPercentMove = CreateParameter("Short move to entry", -1.4m, 0.1m, 1, 0.1m, "Short");
            _shortMaxPositions = CreateParameter("Short max positions count", 3, 0, 20, 1, "Short");
            _shortStopPercent = CreateParameter("Short stop percent", 0.8m, 0.1m, 1, 0.1m, "Short");
            _shortProfitPercent = CreateParameter("Short profit percent", 0.8m, 0.1m, 1, 0.1m, "Short");
            _shortMaxSecondsInPositions = CreateParameter("Short seconds in position", 0, 0, 20, 1, "Short");
            _shortVolumeType = CreateParameter("Short volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Short");
            _shortVolume = CreateParameter("Short volume", 20, 1.0m, 50, 4, "Short");
            _shortTradeAssetInPortfolio = CreateParameter("Short asset in portfolio", "Prime", "Short");

            // Signals
            _upSignalsIsOn = CreateParameter("Up signals is on", false, "Signals");
            _upSignalsPercentMove = CreateParameter("Up signals percent move", 0.7m, 0.1m, 1, 0.1m, "Signals");
            _upSignalsMusic = CreateParameter("Up signals music", "Duck", new[] { "Duck", "Wolf" }, "Signals");
            _upSignalsErrorLogIsOn = CreateParameter("Up signals error log is on", true, "Signals");

            _downSignalsIsOn = CreateParameter("Down signals is on", false, "Signals");
            _downSignalsPercentMove = CreateParameter("Down signals percent move", -0.7m, 0.1m, 1, 0.1m, "Signals");
            _downSignalsMusic = CreateParameter("Down signals music", "Wolf", new[] { "Duck", "Wolf" }, "Signals");
            _downSignalsErrorLogIsOn = CreateParameter("Down signals error log is on", true, "Signals");

            // Monitor

            this.ParamGuiSettings.Height = 800;
            this.ParamGuiSettings.Width = 780;

            CustomTabToParametersUi customTabOrderGrid = ParamGuiSettings.CreateCustomTab(" Monitor ");
            CreateColumnsTable();
            customTabOrderGrid.AddChildren(_hostTable);

            Description = OsLocalization.ConvertToLocString(
            "Eng:Monitor for tracking the movement of assets on the market over a certain number of candles. With the ability to send alerts and trade_" +
            "Ru:Монитор для слежения за движением активов на рынке за определённое кол-во свечей. С возможность выбрасывать алерты и торговать_");

            if (startProgram == StartProgram.IsTester)
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

        private void Server_TestingStartEvent()
        {
            _checkMoveTimes.Clear();
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        #region Move array

        private Dictionary<string, MoveData> _checkMoveTimes = new Dictionary<string, MoveData>();

        private void UpdateMoveData(List<Candle> candles, BotTabSimple tab)
        {
            MoveData myData = new MoveData();

            if (_checkMoveTimes.TryGetValue(tab.Connector.SecurityName, out myData) == false)
            {
                myData = new MoveData();
                myData.SecurityName = tab.Connector.SecurityName;
                myData.Tab = tab;
                _checkMoveTimes.Add(tab.Connector.SecurityName, myData);
            }

            myData.Time = candles[^1].TimeStart;

            decimal maxPrice = decimal.MinValue;
            decimal minPrice = decimal.MaxValue;
            decimal currentPrice = candles[^1].Close;

            for (int i = candles.Count - 1; i >= 0 && i > candles.Count - 1 - _candlesToAnalyze.ValueInt; i--)
            {
                Candle currentCandle = candles[i];

                if(currentCandle.High > maxPrice)
                {
                    maxPrice = currentCandle.High;
                }
                if(currentCandle.Low < minPrice)
                {
                    minPrice = currentCandle.Low;
                }
            }

            if (maxPrice == decimal.MinValue
                || minPrice == decimal.MaxValue)
            {
                return;
            }

            decimal movePercentUp =  (currentPrice - minPrice) / (minPrice / 100);
            decimal movePercentDown = (maxPrice - currentPrice) / (maxPrice / 100);

            myData.MoveUp = Math.Round(movePercentUp,2);
            myData.MoveDown = Math.Round(-movePercentDown,2);
        }

        #endregion

        #region Trade logic entry

        private void _tabScreener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            // "Off", "OnCandleUpdate", "OnCandleFinish"
            if (_regime.ValueString == "Off")
            {
                return;
            }

            MainLogic(candles, tab);
        }

        private void _tabScreener_CandleUpdateEvent(List<Candle> candles, BotTabSimple tab)
        {
            // "Off", "OnCandleUpdate", "OnCandleFinish"
            if (_regime.ValueString != "OnCandleUpdate")
            {
                return;
            }

            MainLogic(candles, tab);
        }

        private void MainLogic(List<Candle> candles, BotTabSimple tab)
        {

            if (tab.IsConnected == false
                || tab.IsReadyToTrade == false)
            {
                return;
            }

            if (candles == null || candles.Count < 5)
            {
                return;
            }

            UpdateMoveData(candles, tab);
            TryUpdateTable();

            if (_tradePeriodsSettings.CanTradeThisTime(tab.TimeServerCurrent) == false)
            {
                return;
            }

            TrySendSignal(tab, candles);

            List<Position> positions = tab.PositionsOpenAll;

            if (positions.Count == 0)
            {
                if (_longIsOn.ValueBool == true)
                {
                    TryOpenLongPosition(candles, tab);
                }

                if (positions.Count == 0
                    && _shortIsOn.ValueBool == true)
                {
                    TryOpenShortPosition(candles, tab);
                }
            }
            else
            {
                Position pos = positions[0];

                if (pos.Direction == Side.Buy)
                {
                    TryCloseLongPosition(candles, tab, pos);
                }
                else if (pos.Direction == Side.Sell)
                {
                    TryCloseShortPosition(candles, tab, pos);
                }
            }
        }

        private decimal GetVolume(BotTabSimple tab, 
            StrategyParameterString volumeType, 
            StrategyParameterDecimal volume,
            StrategyParameterString tradeAssetInPortfolio)
        {
            decimal volumeResult = 0;

            if (volumeType.ValueString == "Contracts")
            {
                volumeResult = volume.ValueDecimal;
            }
            else if (volumeType.ValueString == "Contract currency")
            {
                decimal contractPrice = tab.PriceBestAsk;
                volumeResult = volume.ValueDecimal / contractPrice;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    IServerPermission serverPermission = ServerMaster.GetServerPermission(tab.Connector.ServerType);

                    if (serverPermission != null &&
                        serverPermission.IsUseLotToCalculateProfit &&
                    tab.Security.Lot != 0 &&
                        tab.Security.Lot > 1)
                    {
                        volumeResult = volume.ValueDecimal / (contractPrice * tab.Security.Lot);
                    }

                    volumeResult = Math.Round(volumeResult, tab.Security.DecimalsVolume);
                }
                else // Tester or Optimizer
                {
                    volumeResult = Math.Round(volumeResult, 6);
                }
            }
            else if (volumeType.ValueString == "Deposit percent")
            {
                Portfolio myPortfolio = tab.Portfolio;

                if (myPortfolio == null)
                {
                    return 0;
                }

                decimal portfolioPrimeAsset = 0;

                if (tradeAssetInPortfolio.ValueString == "Prime")
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
                        if (positionOnBoard[i].SecurityNameCode == tradeAssetInPortfolio.ValueString)
                        {
                            portfolioPrimeAsset = positionOnBoard[i].ValueCurrent;
                            break;
                        }
                    }
                }

                if (portfolioPrimeAsset == 0)
                {
                    SendNewLogMessage("Can`t found portfolio " + tradeAssetInPortfolio.ValueString, Logging.LogMessageType.Error);
                    return 0;
                }

                decimal moneyOnPosition = portfolioPrimeAsset * (volume.ValueDecimal / 100);

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

            return volumeResult;
        }

        #endregion

        #region Monitor

        private WindowsFormsHost _hostTable;

        private DataGridView _tableDataGrid;

        private void CreateColumnsTable()
        {
            try
            {
                if (MainWindow.GetDispatcher.CheckAccess() == false)
                {
                    MainWindow.GetDispatcher.Invoke(new Action(CreateColumnsTable));
                    return;
                }

                _hostTable = new WindowsFormsHost();

                _tableDataGrid = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                       DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
                _tableDataGrid.ScrollBars = ScrollBars.Vertical;
                _tableDataGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                _tableDataGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                _tableDataGrid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                DataGridViewTextBoxCell cellParam0 = new DataGridViewTextBoxCell();
                cellParam0.Style = _tableDataGrid.DefaultCellStyle;
                cellParam0.Style.WrapMode = DataGridViewTriState.True;

                DataGridViewColumn newColumn0 = new DataGridViewColumn();
                newColumn0.CellTemplate = cellParam0;
                newColumn0.HeaderText = "Security";
                _tableDataGrid.Columns.Add(newColumn0);
                newColumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn1 = new DataGridViewColumn();
                newColumn1.CellTemplate = cellParam0;
                newColumn1.HeaderText = "Move % up";
                _tableDataGrid.Columns.Add(newColumn1);
                newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn2 = new DataGridViewColumn();
                newColumn2.CellTemplate = cellParam0;
                newColumn2.HeaderText = "Move % down";
                _tableDataGrid.Columns.Add(newColumn2);
                newColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn3 = new DataGridViewColumn();
                newColumn3.CellTemplate = cellParam0;
                newColumn3.HeaderText = "Chart";
                _tableDataGrid.Columns.Add(newColumn3);
                newColumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn4 = new DataGridViewColumn();
                newColumn4.CellTemplate = cellParam0;
                newColumn4.HeaderText = "Open";
                _tableDataGrid.Columns.Add(newColumn4);
                newColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn6 = new DataGridViewColumn();
                newColumn6.CellTemplate = cellParam0;
                newColumn6.HeaderText = "Pos";
                _tableDataGrid.Columns.Add(newColumn6);
                newColumn6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn7 = new DataGridViewColumn();
                newColumn7.CellTemplate = cellParam0;
                newColumn7.HeaderText = "Close";
                _tableDataGrid.Columns.Add(newColumn7);
                newColumn7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _tableDataGrid.DataError += _tableDataGrid_DataError;
                _tableDataGrid.CellClick += _tableDataGrid_CellClick;

                _hostTable.Child = _tableDataGrid;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _tableDataGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if(row < 0
                    || row > _tableDataGrid.Rows.Count)
                {
                    return;
                }

                string secName = _tableDataGrid.Rows[row].Cells[0].Value.ToString();

                BotTabSimple tab = _tabScreener.Tabs.Find(t => t.Connector.SecurityName == secName);

                if(tab == null)
                {
                    return;
                }

                if(column == 3)
                { // Chart

                    int tabNumber = -1;

                    for(int i = 0;i < _tabScreener.Tabs.Count;i++)
                    {
                        if (_tabScreener.Tabs[i].Connector.SecurityName == secName)
                        {
                            tabNumber = i;
                            break;
                        }
                    }

                    if (tabNumber == -1)
                    {
                        return;
                    }

                    _tabScreener.ShowChart(tabNumber);
                    return;
                }
                else if(column == 4)
                { // Open pos

                    tab.ShowOpenPositionDialog();
                }
                else if (column == 6)
                { // Close

                    List<Position> openPoses = tab.PositionsOpenAll;

                    if(openPoses.Count != 0)
                    {
                        tab.ShowClosePositionDialog(openPoses[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _tableDataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendNewLogMessage(sender.ToString(), Logging.LogMessageType.Error);
        }

        private DateTime _lastTimeUpdateTable = DateTime.MinValue;

        private void TryUpdateTable()
        {
            // 0 Security
            // 1 Move % up
            // 2 Move % down
            // 3 Chart
            // 4 Open
            // 5 Pos
            // 6 Close

            try
            {
                if (_tableDataGrid.InvokeRequired)
                {
                    _tableDataGrid.Invoke(new Action(TryUpdateTable));
                    return;
                }

                if (_lastTimeUpdateTable != DateTime.MinValue
                 && _lastTimeUpdateTable.AddSeconds(1) > DateTime.Now)
                {
                    return;
                }

                _lastTimeUpdateTable = DateTime.Now;

                if (_tableDataGrid.Rows.Count != _checkMoveTimes.Count)
                {
                    _tableDataGrid.Rows.Clear();

                    if(_checkMoveTimes.Values.Count > 0)
                    {
                        try
                        {
                            foreach (MoveData data in _checkMoveTimes.Values)
                            {
                                DataGridViewRow newRow = GetRow(data);
                                _tableDataGrid.Rows.Add(newRow);
                            }
                        }
                        catch
                        {
                            return;
                        }
                    }

                    return;
                }
                else
                {
                    for(int i = 0;i < _tableDataGrid.Rows.Count;i++)
                    {
                        DataGridViewRow currentRow = _tableDataGrid.Rows[i];

                        string securityName = currentRow.Cells[0].Value.ToString();

                        MoveData myData = new MoveData();

                        if (_checkMoveTimes.TryGetValue(securityName, out myData) == false)
                        {
                            continue;
                        }

                        DataGridViewRow row = GetRow(myData);

                        if (currentRow.Cells[1].Value == null 
                            || currentRow.Cells[1].Value.ToString() != row.Cells[1].Value.ToString())
                        {
                            currentRow.Cells[1].Value = row.Cells[1].Value;
                        }
                        if (currentRow.Cells[2].Value == null
                         || currentRow.Cells[2].Value.ToString() != row.Cells[2].Value.ToString())
                        {
                            currentRow.Cells[2].Value = row.Cells[2].Value;
                        }
                        if (currentRow.Cells[5].Value == null
                         || currentRow.Cells[5].Value.ToString() != row.Cells[5].Value.ToString())
                        {
                            currentRow.Cells[5].Value = row.Cells[5].Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetRow(MoveData data)
        {
            // 0 Security
            // 1 Move % up
            // 2 Move % down
            // 3 Chart
            // 4 Open
            // 5 Pos
            // 6 Close

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = data.SecurityName;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = data.MoveUp;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = data.MoveDown;

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = "Chart";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = "Open";

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;

            if(data.Tab.PositionsOpenAll.Count == 0)
            {
                row.Cells[^1].Value = data.Tab.PositionsOpenAll.Count;
            }
            else
            {
                Position pos = data.Tab.PositionsOpenAll[0];

                if(pos.Direction == Side.Buy)
                {
                    row.Cells[^1].Value = "Long";
                }
                else
                {
                    row.Cells[^1].Value = "Short";
                }
            }

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = "Close";

            return row;
        }

        #endregion

        #region Long positions

        private void TryOpenLongPosition(List<Candle> candles, BotTabSimple tab)
        {
            List<Position> positions = _tabScreener.PositionsOpenAll.FindAll(p => p.Direction == Side.Buy);

            if (positions.Count >= _longMaxPositions.ValueInt)
            {
                return;
            }

            MoveData myData = new MoveData();

            if (_checkMoveTimes.TryGetValue(tab.Connector.SecurityName, out myData) == false)
            {
                return;
            }

            if (_longPercentMove.ValueDecimal > 0)
            {
                decimal movePercent = myData.MoveUp;

                if (movePercent > _longPercentMove.ValueDecimal
                    && candles[^1].IsUp)
                {
                    tab.BuyAtMarket(GetVolume(tab, _longVolumeType, _longVolume, _longTradeAssetInPortfolio));
                }
            }
            if (_longPercentMove.ValueDecimal < 0)
            {
                decimal movePercent = myData.MoveDown;

                if (movePercent < _longPercentMove.ValueDecimal
                    && candles[^1].IsDown)
                {
                    tab.BuyAtMarket(GetVolume(tab, _longVolumeType, _longVolume, _longTradeAssetInPortfolio));
                }
            }
        }

        private void TryCloseLongPosition(List<Candle> candles, BotTabSimple tab, Position position)
        {
            if(_longMaxSecondsInPositions.ValueInt == 0)
            {
                return;
            }

            int secondsInPosition = Convert.ToInt32((tab.TimeServerCurrent - position.TimeOpen).TotalSeconds);

            if (secondsInPosition > _longMaxSecondsInPositions.ValueInt)
            {
                bool needToSendOrder = false;

                if (position.CloseOrders == null
                    || position.CloseOrders.Count == 0)
                {
                    tab.CloseAtMarket(position, position.OpenVolume);
                    return;
                }

                if (position.CloseOrders[^1].TypeOrder == OrderPriceType.Limit)
                {
                    needToSendOrder = true;
                }

                if (needToSendOrder == true)
                {
                    tab.CloseAtMarket(position, position.OpenVolume);
                }
            }
        }

        #endregion

        #region Set Stop and Profit

        private void _tabScreener_PositionOpeningSuccesEvent(Position position, BotTabSimple tab)
        {
            if (position.Direction == Side.Buy)
            {
                if (_longStopPercent.ValueDecimal != 0)
                {
                    decimal stopPrice = position.EntryPrice - position.EntryPrice * (_longStopPercent.ValueDecimal / 100);
                    tab.CloseAtStopMarket(position, stopPrice);
                }

                if (_longProfitPercent.ValueDecimal != 0)
                {
                    decimal profitOrderPrice = position.EntryPrice + position.EntryPrice * (_longProfitPercent.ValueDecimal / 100);
                    tab.CloseAtProfitMarket(position, profitOrderPrice);
                }
            }
            else if (position.Direction == Side.Sell)
            {
                if (_shortProfitPercent.ValueDecimal != 0)
                {
                    decimal profitOrderPrice = position.EntryPrice - position.EntryPrice * (_shortProfitPercent.ValueDecimal / 100);
                    tab.CloseAtProfitMarket(position, profitOrderPrice);
                }

                if (_shortStopPercent.ValueDecimal != 0)
                {
                    decimal stopPrice = position.EntryPrice + position.EntryPrice * (_shortStopPercent.ValueDecimal / 100);
                    tab.CloseAtStopMarket(position, stopPrice);
                }
            }
        }

        #endregion

        #region Short positions

        private void TryOpenShortPosition(List<Candle> candles, BotTabSimple tab)
        {
            List<Position> positions = _tabScreener.PositionsOpenAll.FindAll(p => p.Direction == Side.Sell);

            if (positions.Count >= _shortMaxPositions.ValueInt)
            {
                return;
            }

            MoveData myData = new MoveData();

            if (_checkMoveTimes.TryGetValue(tab.Connector.SecurityName, out myData) == false)
            {
                return;
            }

            if (_shortPercentMove.ValueDecimal < 0)
            {
                decimal movePercent = myData.MoveDown;

                if (movePercent < _shortPercentMove.ValueDecimal
                    && candles[^1].IsDown)
                {
                    tab.SellAtMarket(GetVolume(tab, _shortVolumeType, _shortVolume, _shortTradeAssetInPortfolio));
                }
            }
            if (_shortPercentMove.ValueDecimal > 0)
            {
                decimal movePercent = myData.MoveUp;

                if (movePercent > _shortPercentMove.ValueDecimal
                    && candles[^1].IsUp)
                {
                    tab.SellAtMarket(GetVolume(tab, _shortVolumeType, _shortVolume, _shortTradeAssetInPortfolio));
                }
            }
        }

        private void TryCloseShortPosition(List<Candle> candles, BotTabSimple tab, Position position)
        {
            if (_shortMaxSecondsInPositions.ValueInt == 0)
            {
                return;
            }

            int secondsInPosition = Convert.ToInt32((tab.TimeServerCurrent - position.TimeOpen).TotalSeconds);

            if (secondsInPosition > _shortMaxSecondsInPositions.ValueInt)
            {
                bool needToSendOrder = false;

                if (position.CloseOrders == null
                    || position.CloseOrders.Count == 0)
                {
                    tab.CloseAtMarket(position, position.OpenVolume);
                    return;
                }

                if (position.CloseOrders[^1].TypeOrder == OrderPriceType.Limit)
                {
                    needToSendOrder = true;
                }

                if (needToSendOrder == true)
                {
                    tab.CloseAtMarket(position, position.OpenVolume);
                }
            }
        }

        #endregion

        #region Signals

        private Dictionary<string, SignalData> _upSignals = new Dictionary<string, SignalData>();

        private Dictionary<string, SignalData> _downSignals = new Dictionary<string, SignalData>();

        private void TrySendSignal(BotTabSimple tab, List<Candle> candles)
        {
            if(_downSignalsIsOn.ValueBool == false 
                && _upSignalsIsOn.ValueBool == false)
            {
                return;
            }

            if(_upSignalsIsOn.ValueBool == true)
            {
                MoveData myData = new MoveData();

                if (_checkMoveTimes.TryGetValue(tab.Connector.SecurityName, out myData) == false)
                {
                    return;
                }

                if(myData.MoveUp > _upSignalsPercentMove.ValueDecimal)
                {
                    // нужно отправлять сигнал что вырасло вверх

                    SignalData mySignalData = new SignalData();

                    if (_upSignals.TryGetValue(tab.Connector.SecurityName, out mySignalData) == false)
                    {
                        mySignalData = new SignalData();
                        mySignalData.SecurityName = tab.Connector.SecurityName;
                        _upSignals.Add(tab.Connector.SecurityName, mySignalData);
                    }

                    if(mySignalData.Time == candles[^1].TimeStart)
                    {
                        return;
                    }

                    mySignalData.Time = candles[^1].TimeStart;

                    DropSignal(myData, "Up signal", _upSignalsMusic.ValueString,_upSignalsErrorLogIsOn.ValueBool);
                }
            }

            if(_downSignalsIsOn.ValueBool == true)
            {
                MoveData myData = new MoveData();

                if (_checkMoveTimes.TryGetValue(tab.Connector.SecurityName, out myData) == false)
                {
                    return;
                }

                if (myData.MoveDown < _downSignalsPercentMove.ValueDecimal)
                {
                    // нужно отправлять сигнал что вырасло вверх

                    SignalData mySignalData = new SignalData();

                    if (_downSignals.TryGetValue(tab.Connector.SecurityName, out mySignalData) == false)
                    {
                        mySignalData = new SignalData();
                        mySignalData.SecurityName = tab.Connector.SecurityName;
                        _downSignals.Add(tab.Connector.SecurityName, mySignalData);
                    }

                    if (mySignalData.Time == candles[^1].TimeStart)
                    {
                        return;
                    }

                    mySignalData.Time = candles[^1].TimeStart;

                    DropSignal(myData, "Down signal", _downSignalsMusic.ValueString, _downSignalsErrorLogIsOn.ValueBool);
                }
            }
        }

        private void DropSignal(MoveData myData, string signalName, string soundType, bool errorLogTo)
        {
            // "Duck", "Wolf"

            PlaySound(soundType);

            string messageValue = signalName + " " + myData.Tab.Connector.SecurityName + "\n";
            messageValue += "Time: " + myData.Time.ToString() + "\n";

            if (signalName == "Up signal")
            {
                messageValue += "Move percent now: " + myData.MoveUp + "\n";
            }
            if (signalName == "Down signal")
            {
                messageValue += "Move percent now: " + myData.MoveDown + "\n";
            }

            LogMessageType messageType = LogMessageType.User;

            if (errorLogTo)
            {
                messageType = LogMessageType.Error;
            }

            myData.Tab.SetNewLogMessage(messageValue, messageType);
        }

        private void PlaySound(string soundName)
        {
            try
            {
                UnmanagedMemoryStream stream = Resources.Bird;

                if (soundName == AlertMusic.Duck.ToString())
                {
                    stream = Resources.Duck;
                }
                if (soundName == AlertMusic.Wolf.ToString())
                {
                    stream = Resources.wolf01;
                }

                if (stream != null)
                {
                    SoundPlayer player = new SoundPlayer(stream);
                    player.Play();
                }
            }
            catch
            {
                // ignore
            }
        }

        #endregion
    }

    public class MoveData
    {
        public BotTabSimple Tab;

        public string SecurityName;

        public DateTime Time;

        public List<decimal> Values;

        public decimal MoveDown;

        public decimal MoveUp;
    }

    public class SignalData
    {
        public string SecurityName;

        public DateTime Time;
    }
}