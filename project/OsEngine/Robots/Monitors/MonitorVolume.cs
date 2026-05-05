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
using OsEngine.Indicators;
using System.Linq;
using System.Drawing;

namespace OsEngine.Robots.Monitors
{
    /*

MonitorVolume
Монитор для анализа перемещения бумаг по ренкингу объёмов за N свечек
Содержит в себе богатую визуальную часть, в которой видно таблицу движений по выбранным активам

При фиксации определённого движения вверх или вниз, поддерживает:
1) Сигналы визуальные и звуковые.
2) Автоматическое открытие позиций. С закрытием по профиту и стопу. Или закрытие по времени

*/
    [Bot("MonitorVolume")]
    public class MonitorVolume : BotPanel
    {
        private BotTabScreener _tabScreener;

        // Prime settings
        private StrategyParameterString _regime;
        private StrategyParameterInt _candlesLookBackMoveInRanking;
        private StrategyParameterInt _candlesLookBackVolume;
        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;
        private StrategyParameterInt _keltnerAtrLength;
        private StrategyParameterInt _keltnerEmaLength;
        private StrategyParameterDecimal _keltnerDeviation;

        // Long
        private StrategyParameterBool _longIsOn;
        private StrategyParameterDecimal _longPercentMove;
        private StrategyParameterInt _longMaxPositions;
        private StrategyParameterDecimal _longStopPercent;
        private StrategyParameterDecimal _longProfitPercent;
        private StrategyParameterInt _longMaxCandlesInPositions;
        private StrategyParameterString _longVolumeType;
        private StrategyParameterDecimal _longVolume;
        private StrategyParameterString _longTradeAssetInPortfolio;

        // Short
        private StrategyParameterBool _shortIsOn;
        private StrategyParameterDecimal _shortPercentMove;
        private StrategyParameterInt _shortMaxPositions;
        private StrategyParameterDecimal _shortStopPercent;
        private StrategyParameterDecimal _shortProfitPercent;
        private StrategyParameterInt _shortMaxCandlesInPositions;
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

        public MonitorVolume(string name, StartProgram startProgram) : base(name, startProgram)
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
            _tabScreener.CandlesSyncFinishedEvent += _tabScreener_CandlesSyncFinishedEvent;
            _tabScreener.PositionOpeningSuccesEvent += _tabScreener_PositionOpeningSuccesEvent;

            // Prime settings
            _regime = CreateParameter("Regime", "Off", new[] { "Off", "On" }, "Prime settings");
            _candlesLookBackVolume = CreateParameter("Lookback volume. Hours", 24, 0, 20, 1, "Prime settings");
            _candlesLookBackMoveInRanking = CreateParameter("Lookback move in ranking. Candles", 10, 0, 20, 1, "Prime settings");

            _keltnerEmaLength = CreateParameter("Keltner ema Length", 150, 20, 300, 10, "Prime settings");
            _keltnerAtrLength = CreateParameter("Keltner atr Length", 24, 20, 300, 10, "Prime settings");
            _keltnerDeviation = CreateParameter("Keltner deviation", 3.5m, 1, 4, 0.1m, "Prime settings");

            _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods", "Prime settings");
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;

            _tabScreener.CreateCandleIndicator(1, "Volume", new List<string>() { }, "Second");

            _tabScreener.CreateCandleIndicator(2, "KeltnerChannel", new List<string>() {
                _keltnerEmaLength.ValueInt.ToString(),
                _keltnerAtrLength.ValueInt.ToString(),
                _keltnerAtrLength.ValueInt.ToString(),
                _keltnerDeviation.ValueDecimal.ToString(),
                "Typical"
            }, "Prime");

            // Long
            _longIsOn = CreateParameter("Long Is On", false, "Long");
            _longPercentMove = CreateParameter("Long move to entry", 3m, 0.1m, 1, 0.1m, "Long");
            _longMaxPositions = CreateParameter("Long max positions count", 3, 0, 20, 1, "Long");
            _longStopPercent = CreateParameter("Long stop percent", 0m, 0.1m, 1, 0.1m, "Long");
            _longProfitPercent = CreateParameter("Long profit percent", 0m, 0.1m, 1, 0.1m, "Long");
            _longMaxCandlesInPositions = CreateParameter("Long candles in position", 150, 0, 300, 1, "Long");
            _longVolumeType = CreateParameter("Long volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Long");
            _longVolume = CreateParameter("Long volume", 30, 1.0m, 50, 4, "Long");
            _longTradeAssetInPortfolio = CreateParameter("Long asset in portfolio", "Prime", "Long");

            // Short
            _shortIsOn = CreateParameter("Short Is On", false, "Short");
            _shortPercentMove = CreateParameter("Short move to entry", 3m, 0.1m, 1, 0.1m, "Short");
            _shortMaxPositions = CreateParameter("Short max positions count", 3, 0, 20, 1, "Short");
            _shortStopPercent = CreateParameter("Short stop percent", 0m, 0.1m, 1, 0.1m, "Short");
            _shortProfitPercent = CreateParameter("Short profit percent", 0m, 0.1m, 1, 0.1m, "Short");
            _shortMaxCandlesInPositions = CreateParameter("Short candles in position", 50, 0, 20, 1, "Short");
            _shortVolumeType = CreateParameter("Short volume type", "Deposit percent", new[] { "Contracts", "Contract currency", "Deposit percent" }, "Short");
            _shortVolume = CreateParameter("Short volume", 20, 1.0m, 50, 4, "Short");
            _shortTradeAssetInPortfolio = CreateParameter("Short asset in portfolio", "Prime", "Short");

            // Signals
            _upSignalsIsOn = CreateParameter("Up signals is on", false, "Signals");
            _upSignalsPercentMove = CreateParameter("Up signals move", 4m, 0.1m, 1, 0.1m, "Signals");
            _upSignalsMusic = CreateParameter("Up signals music", "Duck", new[] { "Duck", "Wolf" }, "Signals");
            _upSignalsErrorLogIsOn = CreateParameter("Up signals error log is on", true, "Signals");

            _downSignalsIsOn = CreateParameter("Down signals is on", false, "Signals");
            _downSignalsPercentMove = CreateParameter("Down signals move", -4m, 0.1m, 1, 0.1m, "Signals");
            _downSignalsMusic = CreateParameter("Down signals music", "Wolf", new[] { "Duck", "Wolf" }, "Signals");
            _downSignalsErrorLogIsOn = CreateParameter("Down signals error log is on", true, "Signals");

            // Monitor

            this.ParamGuiSettings.Height = 800;
            this.ParamGuiSettings.Width = 780;

            CustomTabToParametersUi customTabOrderGrid = ParamGuiSettings.CreateCustomTab(" Monitor ");
            CreateColumnsTable();
            customTabOrderGrid.AddChildren(_hostTable);

            Description = OsLocalization.ConvertToLocString(
            "Eng:Monitor for tracking the movement of assets on the market over a certain number of candles by volume ranking. With the ability to send alerts and trade_" +
            "Ru:Монитор для слежения за движением активов на рынке за определённое кол-во свечей по ренкинку объёма. С возможность выбрасывать алерты и торговать_");

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

            this.ParametrsChangeByUser += MonitorVolume_ParametrsChangeByUser;
        }

        private void MonitorVolume_ParametrsChangeByUser()
        {
            _tabScreener._indicators[0].Parameters
                 = new List<string>()
                {
                _keltnerEmaLength.ValueInt.ToString(),
                _keltnerAtrLength.ValueInt.ToString(),
                _keltnerAtrLength.ValueInt.ToString(),
                _keltnerDeviation.ValueDecimal.ToString(),
                "Typical"
                };

            _tabScreener.UpdateIndicatorsParameters();


        }

        private void Server_TestingStartEvent()
        {
            _volumesRanking.Clear();
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        #region Ranking

        private VolumesRanking _volumesRanking = new VolumesRanking();

        private void ProcessRanking()
        {
            _volumesRanking.Process(_tabScreener.Tabs,
                _candlesLookBackMoveInRanking,
                _candlesLookBackVolume);
        }

        #endregion

        #region Trade logic entry

        private void _tabScreener_CandlesSyncFinishedEvent(List<BotTabSimple> tabs)
        {
            if (_regime.ValueString == "Off")
            {
                return;
            }

            ProcessRanking();
            TryUpdateTable();

            for (int i = 0;i < tabs.Count;i++)
            {
                BotTabSimple tab = tabs[i];
                List<Candle> candles = tab.CandlesFinishedOnly;

                if(candles == null || candles.Count == 0)
                {
                    continue;
                }

                MainLogic(candles, tab);
            }
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

                DataGridViewColumn newColumn01 = new DataGridViewColumn();
                newColumn01.CellTemplate = cellParam0;
                newColumn01.HeaderText = "Volume index";
                _tableDataGrid.Columns.Add(newColumn01);
                newColumn01.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn1 = new DataGridViewColumn();
                newColumn1.CellTemplate = cellParam0;
                newColumn1.HeaderText = "Volume value";
                _tableDataGrid.Columns.Add(newColumn1);
                newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn2 = new DataGridViewColumn();
                newColumn2.CellTemplate = cellParam0;
                newColumn2.HeaderText = "Move";
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

                if (row < 0
                    || row > _tableDataGrid.Rows.Count)
                {
                    return;
                }

                string secName = _tableDataGrid.Rows[row].Cells[0].Value.ToString();

                BotTabSimple tab = _tabScreener.Tabs.Find(t => t.Connector.SecurityName == secName);

                if (tab == null)
                {
                    return;
                }

                if (column == 4)
                { // Chart

                    int tabNumber = -1;

                    for (int i = 0; i < _tabScreener.Tabs.Count; i++)
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
                else if (column == 5)
                { // Open pos

                    tab.ShowOpenPositionDialog();
                }
                else if (column == 7)
                { // Close

                    List<Position> openPoses = tab.PositionsOpenAll;

                    if (openPoses.Count != 0)
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
            // 1 Volume index
            // 2 Volume value
            // 3 Move
            // 4 Chart
            // 5 Open
            // 6 Pos
            // 7 Close

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

                _tableDataGrid.Rows.Clear();

                if (_volumesRanking.Stages.Count > 0)
                {
                    try
                    {
                        for (int i = 0; i < _volumesRanking.Stages.Count; i++)
                        {
                            VolumeRankingValue data = _volumesRanking.Stages[i];

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
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private DataGridViewRow GetRow(VolumeRankingValue data)
        {
            // 0 Security
            // 1 Volume index
            // 2 Volume value
            // 3 Move
            // 4 Chart
            // 5 Open
            // 6 Pos
            // 7 Close

            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = data.SecurityName;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = data.SecurityRankingNum;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = data.SummVolume;

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = data.SecurityRankingMove;

            if(data.SecurityRankingMove < 0)
            {
                row.Cells[^1].Style.ForeColor = Color.DarkRed;
            }
            else if(data.SecurityRankingMove> 0)
            {
                row.Cells[^1].Style.ForeColor = Color.Green;
            }

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = "Chart";

            row.Cells.Add(new DataGridViewButtonCell());
            row.Cells[^1].ReadOnly = true;
            row.Cells[^1].Value = "Open";

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[^1].ReadOnly = true;

            if (data.Tab.PositionsOpenAll.Count == 0)
            {
                row.Cells[^1].Value = data.Tab.PositionsOpenAll.Count;
            }
            else
            {
                Position pos = data.Tab.PositionsOpenAll[0];

                if (pos.Direction == Side.Buy)
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

            VolumeRankingValue myData = _volumesRanking.GetRankingValueBySecurityName(tab.Connector.SecurityName);

            if (myData == null)
            {
                return;
            }

            bool haveEntryMove = false;

            if (_longPercentMove.ValueDecimal > 0)
            {
                decimal move = myData.SecurityRankingMove;

                if (move > _longPercentMove.ValueDecimal)
                {
                    haveEntryMove = true;
                }
            }
            if (_longPercentMove.ValueDecimal < 0)
            {
                decimal movePercent = myData.SecurityRankingMove;

                if (movePercent < _longPercentMove.ValueDecimal)
                {
                    haveEntryMove = true;
                }
            }

            if(haveEntryMove == true)
            {
                Aindicator keltner = (Aindicator)tab.Indicators[1];

                if (keltner.DataSeries[1].Values.Count == 0
                    || keltner.DataSeries[1].Last == 0)
                {
                    return;
                }
                decimal futuresLastPrice = candles[^1].Close;

                // 2 проверяем условия 
                if (futuresLastPrice > keltner.DataSeries[1].Last)
                {
                    tab.BuyAtMarket(GetVolume(tab, _longVolumeType, _longVolume, _longTradeAssetInPortfolio));
                }
            }
        }

        private void TryCloseLongPosition(List<Candle> candles, BotTabSimple tab, Position position)
        {
            if (_longMaxCandlesInPositions.ValueInt == 0)
            {
                return;
            }

            int candlesInPosition = Convert.ToInt32((tab.TimeServerCurrent - position.TimeOpen).TotalMinutes / tab.Connector.TimeFrameTimeSpan.TotalMinutes);

            if (candlesInPosition > _longMaxCandlesInPositions.ValueInt)
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
                if(_longStopPercent.ValueDecimal != 0)
                {
                    decimal stopPrice = position.EntryPrice - position.EntryPrice * (_longStopPercent.ValueDecimal / 100);
                    tab.CloseAtStopMarket(position, stopPrice);
                }

                if(_longProfitPercent.ValueDecimal != 0)
                {
                    decimal profitOrderPrice = position.EntryPrice + position.EntryPrice * (_longProfitPercent.ValueDecimal / 100);
                    tab.CloseAtProfitMarket(position, profitOrderPrice);
                }
            }
            else if (position.Direction == Side.Sell)
            {
                if(_shortProfitPercent.ValueDecimal != 0)
                {
                    decimal profitOrderPrice = position.EntryPrice - position.EntryPrice * (_shortProfitPercent.ValueDecimal / 100);
                    tab.CloseAtProfitMarket(position, profitOrderPrice);
                }

                if(_shortStopPercent.ValueDecimal != 0)
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

            VolumeRankingValue myData = _volumesRanking.GetRankingValueBySecurityName(tab.Connector.SecurityName);

            if (myData == null)
            {
                return;
            }

            bool haveEntryMove = false;

            if (_shortPercentMove.ValueDecimal < 0)
            {
                decimal movePercent = myData.SecurityRankingMove;

                if (movePercent < _shortPercentMove.ValueDecimal)
                {
                    haveEntryMove = true;
                }
            }
            if (_shortPercentMove.ValueDecimal > 0)
            {
                decimal movePercent = myData.SecurityRankingMove;

                if (movePercent > _shortPercentMove.ValueDecimal)
                {
                    haveEntryMove = true;
                }
            }

            if(haveEntryMove == true)
            {
                Aindicator keltner = (Aindicator)tab.Indicators[1];

                if (keltner.DataSeries[2].Values.Count == 0
                    || keltner.DataSeries[2].Last == 0)
                {
                    return;
                }
                decimal futuresLastPrice = candles[^1].Close;

                // 2 проверяем условия 
                if (futuresLastPrice < keltner.DataSeries[2].Last)
                {
                    tab.SellAtMarket(GetVolume(tab, _shortVolumeType, _shortVolume, _shortTradeAssetInPortfolio));
                }
            }
        }

        private void TryCloseShortPosition(List<Candle> candles, BotTabSimple tab, Position position)
        {
            if (_shortMaxCandlesInPositions.ValueInt == 0)
            {
                return;
            }

            int candlesInPosition = Convert.ToInt32((tab.TimeServerCurrent - position.TimeOpen).TotalMinutes / tab.Connector.TimeFrameTimeSpan.TotalMinutes);

            if (candlesInPosition > _shortMaxCandlesInPositions.ValueInt)
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

        private void TrySendSignal(BotTabSimple tab, List<Candle> candles)
        {
            if (_downSignalsIsOn.ValueBool == false
                && _upSignalsIsOn.ValueBool == false)
            {
                return;
            }

            if (_upSignalsIsOn.ValueBool == true)
            {
                VolumeRankingValue myData = _volumesRanking.GetRankingValueBySecurityName(tab.Connector.SecurityName);

                if(myData == null)
                {
                    return;
                }

                if (myData.SecurityRankingMove >= _upSignalsPercentMove.ValueDecimal)
                {
                    // нужно отправлять сигнал что вырасло вверх

                    DropSignal(myData, "Up signal", _upSignalsMusic.ValueString, _upSignalsErrorLogIsOn.ValueBool, candles[^1].TimeStart);
                }
            }

            if (_downSignalsIsOn.ValueBool == true)
            {
                VolumeRankingValue myData = _volumesRanking.GetRankingValueBySecurityName(tab.Connector.SecurityName);

                if (myData == null)
                {
                    return;
                }

                if (myData.SecurityRankingMove < _downSignalsPercentMove.ValueDecimal)
                {
                    // нужно отправлять сигнал что вырасло вверх

                    DropSignal(myData, "Down signal", _downSignalsMusic.ValueString, _downSignalsErrorLogIsOn.ValueBool, candles[^1].TimeStart);
                }
            }
        }

        private void DropSignal(VolumeRankingValue myData, string signalName, string soundType, bool errorLogTo, DateTime time)
        {
            // "Duck", "Wolf"

            PlaySound(soundType);

            string messageValue = signalName + " " + myData.Tab.Connector.SecurityName + "\n";
            messageValue += "Candle time: " + time.ToString() + "\n";

            if (signalName == "Up signal")
            {
                messageValue += "Move Up positions: " + myData.SecurityRankingMove + "\n";
            }
            if (signalName == "Down signal")
            {
                messageValue += "Move Down positions: " + myData.SecurityRankingMove + "\n";
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

    public class VolumesRanking
    {
        public void Process(List<BotTabSimple> tabs, 
        StrategyParameterInt candlesLookBackMoveInRanking,
        StrategyParameterInt candlesLookBackVolume)
        {
            // 1 считаем текущее положение бумаг и ренкинг

            List<VolumeRankingValue> resultArrayNow = new List<VolumeRankingValue>();

            for (int i = 0; i < tabs.Count; i++)
            {
                VolumeRankingValue newValue = new VolumeRankingValue();
                BotTabSimple tab = tabs[i];

                if (tab.CandlesAll == null
                    || tab.CandlesAll.Count < 10)
                {
                    continue;
                }

                newValue.Tab = tab;
                newValue.SecurityName = tab.Connector.SecurityName;

                newValue.SummVolume = GetSummVolume(tab, candlesLookBackVolume.ValueInt, 0);

                resultArrayNow.Add(newValue);
            }

            resultArrayNow = resultArrayNow.OrderBy(x => x.SummVolume).ToList();

            if (resultArrayNow.Count > 1)
            {
                resultArrayNow.Reverse();
            }

            for(int i = 0;i < resultArrayNow.Count;i++)
            {
                resultArrayNow[i].SecurityRankingNum = i + 1;
            }

            // 2 считаем прошлое положение бумаг и ренкинг

            List<VolumeRankingValue> resultArrayHistory = new List<VolumeRankingValue>();

            for (int i = 0; i < tabs.Count; i++)
            {
                VolumeRankingValue newValue = new VolumeRankingValue();
                BotTabSimple tab = tabs[i];

                if (tab.CandlesAll == null
                    || tab.CandlesAll.Count < 10)
                {
                    continue;
                }

                newValue.Tab = tab;
                newValue.SecurityName = tab.Connector.SecurityName;

                newValue.SummVolume = GetSummVolume(tab, candlesLookBackVolume.ValueInt, candlesLookBackMoveInRanking.ValueInt);

                resultArrayHistory.Add(newValue);
            }

            resultArrayHistory = resultArrayHistory.OrderBy(x => x.SummVolume).ToList();

            if (resultArrayHistory.Count > 1)
            {
                resultArrayHistory.Reverse();
            }

            for (int i = 0; i < resultArrayHistory.Count; i++)
            {
                resultArrayHistory[i].SecurityRankingNum = i + 1;
            }

            // 3 считаем как сдвинулись ренкинги от прошлого к будущему

            for(int i = 0;i < resultArrayHistory.Count;i++)
            {
                VolumeRankingValue valueHistory = resultArrayHistory[i];

                VolumeRankingValue valueNow = resultArrayNow.Find(val => val.SecurityName == valueHistory.SecurityName);

                if(valueNow == null)
                {
                    continue;
                }

                int move = valueHistory.SecurityRankingNum - valueNow.SecurityRankingNum;

                valueNow.SecurityRankingMove = move;
            }

            // 4 сохраняем в общий массив 

            Stages = resultArrayNow;

        }

        private decimal GetSummVolume(BotTabSimple tab, int hoursCountMax, int index)
        {
            // 1 берём свечки за последнюю неделю

            List<Candle> allCandles = tab.CandlesAll;

            Security security = tab.Security;

            if (allCandles == null || allCandles.Count == 0)
            {
                return 0;
            }

            List<Candle> candlesToVol = new List<Candle>();

            DateTime startTime = allCandles[allCandles.Count - 1].TimeStart;

            int hourCount = 0;

            for (int i = allCandles.Count - 1 - index; i > 0; i--)
            {
                candlesToVol.Add(allCandles[i]);

                if (startTime.Hour != allCandles[i].TimeStart.Hour)
                {
                    hourCount++;
                    startTime = allCandles[i].TimeStart;
                }

                if (hourCount >= hoursCountMax)
                {
                    break;
                }
            }

            // 2 считаем в них объём и складываем в переменную

            decimal allVolume = 0;

            for (int i = 0; i < candlesToVol.Count; i++)
            {
                allVolume += candlesToVol[i].Center * candlesToVol[i].Volume;
            }

            if (security.Lot > 1)
            {
                allVolume = allVolume * security.Lot;
            }

            decimal summVolume = allVolume;

            return Math.Round(summVolume,1);
        }

        public void Clear()
        {
            Stages?.Clear();
        }

        public List<VolumeRankingValue> Stages = new List<VolumeRankingValue>();

        public VolumeRankingValue GetRankingValueBySecurityName(string securityName)
        {
            return Stages.Find(value => value.SecurityName == securityName);
        }
    }

    public class VolumeRankingValue
    {
        public BotTabSimple Tab;

        public string SecurityName;

        public decimal SummVolume;

        public int SecurityRankingNum;

        public int SecurityRankingMove;

    }

}