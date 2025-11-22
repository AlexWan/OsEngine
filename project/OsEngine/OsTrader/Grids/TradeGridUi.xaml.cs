/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace OsEngine.OsTrader.Grids
{
    /// <summary>
    /// Interaction logic for TradeGridUi.xaml
    /// </summary>
    public partial class TradeGridUi : System.Windows.Window
    {
        public TradeGridUi(TradeGrid tradeGrid)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);

            TradeGrid = tradeGrid;
            TradeGrid.RePaintSettingsEvent += TradeGrid_RePaintSettingsEvent;
            TradeGrid.FullRePaintGridEvent += TradeGrid_FullRePaintGridEvent;
            Number = TradeGrid.Number;

            Closed += TradeGridUi_Closed;

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "TradeGridUi" + tradeGrid.Number + tradeGrid.Tab.TabName);

            // settings prime

            ComboBoxGridType.Items.Add(TradeGridPrimeType.MarketMaking.ToString());
            ComboBoxGridType.Items.Add(TradeGridPrimeType.OpenPosition.ToString());
            ComboBoxGridType.SelectedItem = tradeGrid.GridType.ToString();
            ComboBoxGridType.SelectionChanged += ComboBoxGridType_SelectionChanged;

            ComboBoxRegime.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxRegime.Items.Add(TradeGridRegime.OffAndCancelOrders.ToString());
            ComboBoxRegime.Items.Add(TradeGridRegime.On.ToString());
            ComboBoxRegime.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxRegime.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxRegime.SelectedItem = tradeGrid.Regime.ToString();
            ComboBoxRegime.SelectionChanged += ComboBoxRegime_SelectionChanged;

            ComboBoxRegimeLogicEntry.Items.Add(TradeGridLogicEntryRegime.OnTrade.ToString());
            ComboBoxRegimeLogicEntry.Items.Add(TradeGridLogicEntryRegime.OncePerSecond.ToString());
            ComboBoxRegimeLogicEntry.SelectedItem = tradeGrid.RegimeLogicEntry.ToString();
            if (tradeGrid.StartProgram == StartProgram.IsTester
               || tradeGrid.StartProgram == StartProgram.IsOsOptimizer)
            {
                ComboBoxRegimeLogicEntry.SelectedItem = TradeGridLogicEntryRegime.OnTrade.ToString();
                ComboBoxRegimeLogicEntry.IsEnabled = false;
            }
            ComboBoxRegimeLogicEntry.SelectionChanged += ComboBoxRegimeLogicEntry_SelectionChanged;

            ComboBoxAutoClearJournal.Items.Add("True");
            ComboBoxAutoClearJournal.Items.Add("False");
            ComboBoxAutoClearJournal.SelectedItem = tradeGrid.AutoClearJournalIsOn.ToString();

            if (tradeGrid.StartProgram == StartProgram.IsTester
             || tradeGrid.StartProgram == StartProgram.IsOsOptimizer)
            {
                ComboBoxAutoClearJournal.SelectedItem = "False";
                ComboBoxAutoClearJournal.IsEnabled = false;
            }

            ComboBoxAutoClearJournal.SelectionChanged += ComboBoxAutoClearJournal_SelectionChanged;

            TextBoxMaxClosePositionsInJournal.Text = tradeGrid.MaxClosePositionsInJournal.ToString();
            TextBoxMaxClosePositionsInJournal.TextChanged += TextBoxMaxClosePositionsInJournal_TextChanged;

            TextBoxMaxOpenOrdersInMarket.Text = tradeGrid.MaxOpenOrdersInMarket.ToString();
            TextBoxMaxOpenOrdersInMarket.TextChanged += TextBoxMaxOrdersInMarket_TextChanged;

            TextBoxMaxCloseOrdersInMarket.Text = tradeGrid.MaxCloseOrdersInMarket.ToString();
            TextBoxMaxCloseOrdersInMarket.TextChanged += TextBoxMaxCloseOrdersInMarket_TextChanged;

            if (tradeGrid.StartProgram == StartProgram.IsTester
                || tradeGrid.StartProgram == StartProgram.IsOsOptimizer)
            {
                TextBoxDelayInReal.Text = "0";
                LabelDelayInReal.IsEnabled = false;
                TextBoxDelayInReal.IsEnabled = false;
            }
            else
            {
                TextBoxDelayInReal.Text = tradeGrid.DelayInReal.ToString();
                TextBoxDelayInReal.TextChanged += TextBoxDelayInReal_TextChanged;
            }

            ComboBoxCheckMicroVolumes.Items.Add("True");
            ComboBoxCheckMicroVolumes.Items.Add("False");
            ComboBoxCheckMicroVolumes.SelectedItem = tradeGrid.CheckMicroVolumes.ToString();
            ComboBoxCheckMicroVolumes.SelectionChanged += ComboBoxCheckMicroVolumes_SelectionChanged;

            TextBoxMaxDistanceToOrdersPercent.Text = tradeGrid.MaxDistanceToOrdersPercent.ToString();
            TextBoxMaxDistanceToOrdersPercent.TextChanged += TextBoxMaxDistanceToOrdersPercent_TextChanged;

            // other settings

            ComboBoxNonTradePeriod1Regime.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxNonTradePeriod1Regime.Items.Add(TradeGridRegime.OffAndCancelOrders.ToString());
            ComboBoxNonTradePeriod1Regime.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxNonTradePeriod1Regime.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxNonTradePeriod1Regime.SelectedItem = tradeGrid.NonTradePeriods.NonTradePeriod1Regime.ToString();
            ComboBoxNonTradePeriod1Regime.SelectionChanged += ComboBoxNonTradePeriod1Regime_SelectionChanged;

            ComboBoxOpenOrdersMakerOnly.Items.Add(true.ToString());
            ComboBoxOpenOrdersMakerOnly.Items.Add(false.ToString());
            ComboBoxOpenOrdersMakerOnly.SelectedItem = tradeGrid.OpenOrdersMakerOnly.ToString();
            ComboBoxOpenOrdersMakerOnly.SelectionChanged += ComboBoxOpenOrdersMakerOnly_SelectionChanged;


            // stop grid by event

            CheckBoxStopGridByMoveUpIsOn.IsChecked = tradeGrid.StopBy.StopGridByMoveUpIsOn;
            CheckBoxStopGridByMoveUpIsOn.Checked += CheckBoxStopGridByMoveUpIsOn_Checked;
            CheckBoxStopGridByMoveUpIsOn.Unchecked += CheckBoxStopGridByMoveUpIsOn_Checked;
            TextBoxStopGridByMoveUpValuePercent.Text = tradeGrid.StopBy.StopGridByMoveUpValuePercent.ToString();
            TextBoxStopGridByMoveUpValuePercent.TextChanged += TextBoxStopGridByMoveUpValuePercent_TextChanged;
            ComboBoxStopGridByMoveUpReaction.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxStopGridByMoveUpReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxStopGridByMoveUpReaction.Items.Add(TradeGridRegime.OffAndCancelOrders.ToString());
            ComboBoxStopGridByMoveUpReaction.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxStopGridByMoveUpReaction.SelectedItem = tradeGrid.StopBy.StopGridByMoveUpReaction.ToString();
            ComboBoxStopGridByMoveUpReaction.SelectionChanged += ComboBoxStopGridByMoveUpReaction_SelectionChanged;

            CheckBoxStopGridByMoveDownIsOn.IsChecked = tradeGrid.StopBy.StopGridByMoveDownIsOn;
            CheckBoxStopGridByMoveDownIsOn.Checked += CheckBoxStopGridByMoveDownIsOn_Checked;
            CheckBoxStopGridByMoveDownIsOn.Unchecked += CheckBoxStopGridByMoveDownIsOn_Checked;
            TextBoxStopGridByMoveDownValuePercent.Text = tradeGrid.StopBy.StopGridByMoveDownValuePercent.ToString();
            TextBoxStopGridByMoveDownValuePercent.TextChanged += TextBoxStopGridByMoveDownValuePercent_TextChanged;
            ComboBoxStopGridByMoveDownReaction.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxStopGridByMoveDownReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxStopGridByMoveDownReaction.Items.Add(TradeGridRegime.OffAndCancelOrders.ToString());
            ComboBoxStopGridByMoveDownReaction.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxStopGridByMoveDownReaction.SelectedItem = tradeGrid.StopBy.StopGridByMoveDownReaction.ToString();
            ComboBoxStopGridByMoveDownReaction.SelectionChanged += ComboBoxStopGridByMoveDownReaction_SelectionChanged;

            CheckBoxStopGridByPositionsCountIsOn.IsChecked = tradeGrid.StopBy.StopGridByPositionsCountIsOn;
            CheckBoxStopGridByPositionsCountIsOn.Checked += CheckBoxStopGridByPositionsCountIsOn_Checked;
            CheckBoxStopGridByPositionsCountIsOn.Unchecked += CheckBoxStopGridByPositionsCountIsOn_Checked;
            TextBoxStopGridByPositionsCountValue.Text = tradeGrid.StopBy.StopGridByPositionsCountValue.ToString();
            TextBoxStopGridByPositionsCountValue.TextChanged += TextBoxStopGridByPositionsCountValue_TextChanged;
            ComboBoxStopGridByPositionsCountReaction.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxStopGridByPositionsCountReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxStopGridByPositionsCountReaction.Items.Add(TradeGridRegime.OffAndCancelOrders.ToString());
            ComboBoxStopGridByPositionsCountReaction.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxStopGridByPositionsCountReaction.SelectedItem = tradeGrid.StopBy.StopGridByPositionsCountReaction.ToString();
            ComboBoxStopGridByPositionsCountReaction.SelectionChanged += ComboBoxStopGridByPositionsCountReaction_SelectionChanged;

            CheckBoxStopGridByLifeTimeIsOn.IsChecked = tradeGrid.StopBy.StopGridByLifeTimeIsOn;
            CheckBoxStopGridByLifeTimeIsOn.Checked += CheckBoxStopGridByLifeTimeIsOn_Checked;
            CheckBoxStopGridByLifeTimeIsOn.Unchecked += CheckBoxStopGridByLifeTimeIsOn_Checked;
            TextBoxStopGridByLifeTimeSecondsToLife.Text = tradeGrid.StopBy.StopGridByLifeTimeSecondsToLife.ToString();
            TextBoxStopGridByLifeTimeSecondsToLife.TextChanged += TextBoxStopGridByLifeTimeSecondsToLife_TextChanged;
            ComboBoxStopGridByLifeTimeReaction.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxStopGridByLifeTimeReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxStopGridByLifeTimeReaction.Items.Add(TradeGridRegime.OffAndCancelOrders.ToString());
            ComboBoxStopGridByLifeTimeReaction.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxStopGridByLifeTimeReaction.SelectedItem = tradeGrid.StopBy.StopGridByLifeTimeReaction.ToString();
            ComboBoxStopGridByLifeTimeReaction.SelectionChanged += ComboBoxStopGridByLifeTimeReaction_SelectionChanged;

            CheckBoxStopGridByTimeOfDayIsOn.IsChecked = tradeGrid.StopBy.StopGridByTimeOfDayIsOn;
            CheckBoxStopGridByTimeOfDayIsOn.Checked += CheckBoxStopGridByTimeOfDayIsOn_Checked;
            CheckBoxStopGridByTimeOfDayIsOn.Unchecked += CheckBoxStopGridByTimeOfDayIsOn_Checked;
            TextBoxStopGridByTimeOfDayHour.Text = tradeGrid.StopBy.StopGridByTimeOfDayHour.ToString();
            TextBoxStopGridByTimeOfDayHour.TextChanged += TextBoxStopGridByTimeOfDayHour_TextChanged;
            TextBoxStopGridByTimeOfDayMinute.Text = tradeGrid.StopBy.StopGridByTimeOfDayMinute.ToString();
            TextBoxStopGridByTimeOfDayMinute.TextChanged += TextBoxStopGridByTimeOfDayMinute_TextChanged;
            TextBoxStopGridByTimeOfDaySecond.Text = tradeGrid.StopBy.StopGridByTimeOfDaySecond.ToString();
            TextBoxStopGridByTimeOfDaySecond.TextChanged += TextBoxStopGridByTimeOfDaySecond_TextChanged;

            ComboBoxStopGridByTimeOfDayReaction.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxStopGridByTimeOfDayReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxStopGridByTimeOfDayReaction.Items.Add(TradeGridRegime.OffAndCancelOrders.ToString());
            ComboBoxStopGridByTimeOfDayReaction.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxStopGridByTimeOfDayReaction.SelectedItem = tradeGrid.StopBy.StopGridByTimeOfDayReaction.ToString();
            ComboBoxStopGridByTimeOfDayReaction.SelectionChanged += ComboBoxStopGridByTimeOfDayReaction_SelectionChanged;

            // grid lines creation

            ComboBoxGridSide.Items.Add(Side.Buy.ToString());
            ComboBoxGridSide.Items.Add(Side.Sell.ToString());
            ComboBoxGridSide.SelectedItem = tradeGrid.GridCreator.GridSide.ToString();
            ComboBoxGridSide.SelectionChanged += ComboBoxGridSide_SelectionChanged;

            TextBoxFirstPrice.Text = tradeGrid.GridCreator.FirstPrice.ToString();
            TextBoxFirstPrice.TextChanged += TextBoxFirstPrice_TextChanged;

            TextBoxLineCountStart.Text = tradeGrid.GridCreator.LineCountStart.ToString();
            TextBoxLineCountStart.TextChanged += TextBoxLineCountStart_TextChanged;

            ComboBoxTypeStep.Items.Add(TradeGridValueType.Percent.ToString());
            ComboBoxTypeStep.Items.Add(TradeGridValueType.Absolute.ToString());
            ComboBoxTypeStep.SelectedItem = tradeGrid.GridCreator.TypeStep.ToString();
            ComboBoxTypeStep.SelectionChanged += ComboBoxTypeStep_SelectionChanged;

            TextBoxLineStep.Text = tradeGrid.GridCreator.LineStep.ToString();
            TextBoxLineStep.TextChanged += TextBoxLineStep_TextChanged;

            TextBoxStepMultiplicator.Text = tradeGrid.GridCreator.StepMultiplicator.ToString();
            TextBoxStepMultiplicator.TextChanged += TextBoxStepMultiplicator_TextChanged;

            ComboBoxTypeProfit.Items.Add(TradeGridValueType.Percent.ToString());
            ComboBoxTypeProfit.Items.Add(TradeGridValueType.Absolute.ToString());
            ComboBoxTypeProfit.SelectedItem = tradeGrid.GridCreator.TypeProfit.ToString();
            ComboBoxTypeProfit.SelectionChanged += ComboBoxTypeProfit_SelectionChanged;

            TextBoxProfitStep.Text = tradeGrid.GridCreator.ProfitStep.ToString();
            TextBoxProfitStep.TextChanged += TextBoxProfitStep_TextChanged;

            TextBoxProfitMultiplicator.Text = tradeGrid.GridCreator.ProfitMultiplicator.ToString();
            TextBoxProfitMultiplicator.TextChanged += TextBoxProfitMultiplicator_TextChanged;

            ComboBoxTypeVolume.Items.Add(TradeGridVolumeType.Contracts.ToString());
            ComboBoxTypeVolume.Items.Add(TradeGridVolumeType.ContractCurrency.ToString());
            ComboBoxTypeVolume.Items.Add(TradeGridVolumeType.DepositPercent.ToString());
            ComboBoxTypeVolume.SelectedItem = tradeGrid.GridCreator.TypeVolume.ToString();
            ComboBoxTypeVolume.SelectionChanged += ComboBoxTypeVolume_SelectionChanged;

            TextBoxStartVolume.Text = tradeGrid.GridCreator.StartVolume.ToString();
            TextBoxStartVolume.TextChanged += TextBoxStartVolume_TextChanged;

            TextBoxMartingaleMultiplicator.Text = tradeGrid.GridCreator.MartingaleMultiplicator.ToString();
            TextBoxMartingaleMultiplicator.TextChanged += TextBoxMartingaleMultiplicator_TextChanged;

            TextBoxTradeAssetInPortfolio.Text = tradeGrid.GridCreator.TradeAssetInPortfolio;
            TextBoxTradeAssetInPortfolio.TextChanged += TextBoxTradeAssetInPortfolio_TextChanged;

            // stop and profit 

            ComboBoxProfitRegime.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxProfitRegime.Items.Add(TradeGridRegime.On.ToString());
            ComboBoxProfitRegime.SelectedItem = tradeGrid.StopAndProfit.ProfitRegime.ToString();
            ComboBoxProfitRegime.SelectionChanged += ComboBoxProfitRegime_SelectionChanged;

            ComboBoxProfitValueType.Items.Add(TradeGridValueType.Percent.ToString());
            ComboBoxProfitValueType.Items.Add(TradeGridValueType.Absolute.ToString());
            ComboBoxProfitValueType.SelectedItem = tradeGrid.StopAndProfit.ProfitValueType.ToString();
            ComboBoxProfitValueType.SelectionChanged += ComboBoxProfitValueType_SelectionChanged;

            TextBoxProfitValue.Text = tradeGrid.StopAndProfit.ProfitValue.ToString();
            TextBoxProfitValue.TextChanged += TextBoxProfitValue_TextChanged;

            ComboBoxStopRegime.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxStopRegime.Items.Add(TradeGridRegime.On.ToString());
            ComboBoxStopRegime.SelectedItem = tradeGrid.StopAndProfit.StopRegime.ToString();
            ComboBoxStopRegime.SelectionChanged += ComboBoxStopRegime_SelectionChanged;

            ComboBoxStopValueType.Items.Add(TradeGridValueType.Percent.ToString());
            ComboBoxStopValueType.Items.Add(TradeGridValueType.Absolute.ToString());
            ComboBoxStopValueType.SelectedItem = tradeGrid.StopAndProfit.StopValueType.ToString();
            ComboBoxStopValueType.SelectionChanged += ComboBoxStopValueType_SelectionChanged;

            TextBoxStopValue.Text = tradeGrid.StopAndProfit.StopValue.ToString();
            TextBoxStopValue.TextChanged += TextBoxStopValue_TextChanged;

            ComboBoxTrailStopRegime.Items.Add(TradeGridRegime.Off.ToString()); 
            ComboBoxTrailStopRegime.Items.Add(TradeGridRegime.On.ToString());
            ComboBoxTrailStopRegime.SelectedItem = tradeGrid.StopAndProfit.TrailStopRegime.ToString();
            ComboBoxTrailStopRegime.SelectionChanged += ComboBoxTrailStopRegime_SelectionChanged;

            ComboBoxTrailStopValueType.Items.Add(TradeGridValueType.Percent.ToString());
            ComboBoxTrailStopValueType.Items.Add(TradeGridValueType.Absolute.ToString());
            ComboBoxTrailStopValueType.SelectedItem = tradeGrid.StopAndProfit.TrailStopValueType.ToString();
            ComboBoxTrailStopValueType.SelectionChanged += ComboBoxTrailStopValueType_SelectionChanged;

            TextBoxTrailStopValue.Text = tradeGrid.StopAndProfit.TrailStopValue.ToString(); 
            TextBoxTrailStopValue.TextChanged += TextBoxTrailStopValue_TextChanged;

            // auto start

            ComboBoxAutoStartRegime.Items.Add(TradeGridAutoStartRegime.Off.ToString());
            ComboBoxAutoStartRegime.Items.Add(TradeGridAutoStartRegime.LowerOrEqual.ToString());
            ComboBoxAutoStartRegime.Items.Add(TradeGridAutoStartRegime.HigherOrEqual.ToString());
            ComboBoxAutoStartRegime.SelectedItem = tradeGrid.AutoStarter.AutoStartRegime.ToString();
            ComboBoxAutoStartRegime.SelectionChanged += ComboBoxAutoStartRegime_SelectionChanged;
            TextBoxAutoStartPrice.Text = tradeGrid.AutoStarter.AutoStartPrice.ToString();
            TextBoxAutoStartPrice.TextChanged += TextBoxAutoStartPrice_TextChanged;

            ComboBoxRebuildGridRegime.Items.Add(OnOffRegime.Off.ToString());
            ComboBoxRebuildGridRegime.Items.Add(OnOffRegime.On.ToString());
            ComboBoxRebuildGridRegime.SelectedItem = tradeGrid.AutoStarter.RebuildGridRegime.ToString();
            ComboBoxRebuildGridRegime.SelectionChanged += ComboBoxRebuildGridRegime_SelectionChanged;
            TextBoxShiftFirstPrice.Text = tradeGrid.AutoStarter.ShiftFirstPrice.ToString();
            TextBoxShiftFirstPrice.TextChanged += TextBoxShiftFirstPrice_TextChanged;

            CheckBoxStartGridByTimeOfDayIsOn.IsChecked = tradeGrid.AutoStarter.StartGridByTimeOfDayIsOn;
            CheckBoxStartGridByTimeOfDayIsOn.Checked += CheckBoxStartGridByTimeOfDayIsOn_Checked;
            CheckBoxStartGridByTimeOfDayIsOn.Unchecked += CheckBoxStartGridByTimeOfDayIsOn_Checked;
            TextBoxStartGridByTimeOfDayHour.Text = tradeGrid.AutoStarter.StartGridByTimeOfDayHour.ToString();
            TextBoxStartGridByTimeOfDayHour.TextChanged += TextBoxStartGridByTimeOfDayHour_TextChanged;
            TextBoxStartGridByTimeOfDayMinute.Text = tradeGrid.AutoStarter.StartGridByTimeOfDayMinute.ToString();
            TextBoxStartGridByTimeOfDayMinute.TextChanged += TextBoxStartGridByTimeOfDayMinute_TextChanged;
            TextBoxStartGridByTimeOfDaySecond.Text = tradeGrid.AutoStarter.StartGridByTimeOfDaySecond.ToString();
            TextBoxStartGridByTimeOfDaySecond.TextChanged += TextBoxStartGridByTimeOfDaySecond_TextChanged;

            // error reaction

            CheckBoxFailOpenOrdersReactionIsOn.IsChecked = tradeGrid.ErrorsReaction.FailOpenOrdersReactionIsOn;
            CheckBoxFailOpenOrdersReactionIsOn.Checked += CheckBoxFailOpenOrdersReactionIsOn_Checked;
            CheckBoxFailOpenOrdersReactionIsOn.Unchecked += CheckBoxFailOpenOrdersReactionIsOn_Checked;
            ComboBoxFailOpenOrdersReaction.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxFailOpenOrdersReaction.Items.Add(TradeGridRegime.On.ToString());
            ComboBoxFailOpenOrdersReaction.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxFailOpenOrdersReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxFailOpenOrdersReaction.SelectedItem = tradeGrid.ErrorsReaction.FailOpenOrdersReaction.ToString();
            ComboBoxFailOpenOrdersReaction.SelectionChanged += ComboBoxFailOpenOrdersReaction_SelectionChanged;
            TextBoxFailOpenOrdersCountToReaction.Text = tradeGrid.ErrorsReaction.FailOpenOrdersCountToReaction.ToString();
            TextBoxFailOpenOrdersCountToReaction.TextChanged += TextBoxFailOpenOrdersCountToReaction_TextChanged;
            TextBoxFailOpenOrdersCountFact.Text = tradeGrid.ErrorsReaction.FailOpenOrdersCountFact.ToString();

            CheckBoxFailCancelOrdersReactionIsOn.IsChecked = tradeGrid.ErrorsReaction.FailCancelOrdersReactionIsOn;
            CheckBoxFailCancelOrdersReactionIsOn.Checked += CheckBoxFailCancelOrdersReactionIsOn_Checked;
            CheckBoxFailCancelOrdersReactionIsOn.Unchecked += CheckBoxFailCancelOrdersReactionIsOn_Checked;
            ComboBoxFailCancelOrdersReaction.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxFailCancelOrdersReaction.Items.Add(TradeGridRegime.On.ToString());
            ComboBoxFailCancelOrdersReaction.Items.Add(TradeGridRegime.CloseForced.ToString()); 
            ComboBoxFailCancelOrdersReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxFailCancelOrdersReaction.SelectedItem = tradeGrid.ErrorsReaction.FailCancelOrdersReaction.ToString();
            ComboBoxFailCancelOrdersReaction.SelectionChanged += ComboBoxFailCancelOrdersReaction_SelectionChanged;
            TextBoxFailCancelOrdersCountToReaction.Text = tradeGrid.ErrorsReaction.FailCancelOrdersCountToReaction.ToString();
            TextBoxFailCancelOrdersCountToReaction.TextChanged += TextBoxFailCancelOrdersCountToReaction_TextChanged;
            TextBoxFailCancelOrdersCountFact.Text = tradeGrid.ErrorsReaction.FailCancelOrdersCountFact.ToString();

            CheckBoxWaitOnStartConnectorIsOn.IsChecked = tradeGrid.ErrorsReaction.WaitOnStartConnectorIsOn;
            CheckBoxWaitOnStartConnectorIsOn.Checked += CheckBoxWaitOnStartConnectorIsOn_Checked;
            CheckBoxWaitOnStartConnectorIsOn.Unchecked += CheckBoxWaitOnStartConnectorIsOn_Checked;

            TextBoxWaitSecondsOnStartConnector.Text = tradeGrid.ErrorsReaction.WaitSecondsOnStartConnector.ToString();
            TextBoxWaitSecondsOnStartConnector.TextChanged += TextBoxWaitSecondsOnStartConnector_TextChanged; 

            // trailing up / down

            CheckBoxTrailingUpIsOn.IsChecked = tradeGrid.TrailingUp.TrailingUpIsOn;
            CheckBoxTrailingUpIsOn.Checked += CheckBoxTrailingUpIsOn_Checked;
            CheckBoxTrailingUpIsOn.Unchecked += CheckBoxTrailingUpIsOn_Checked;

            TextBoxTrailingUpStep.Text = tradeGrid.TrailingUp.TrailingUpStep.ToString();
            TextBoxTrailingUpStep.TextChanged += TextBoxTrailingUpStep_TextChanged;

            TextBoxTrailingUpLimit.Text = tradeGrid.TrailingUp.TrailingUpLimit.ToString();
            TextBoxTrailingUpLimit.TextChanged += TextBoxTrailingUpLimit_TextChanged;

            CheckBoxTrailingUpCanMoveExitOrder.IsChecked = tradeGrid.TrailingUp.TrailingUpCanMoveExitOrder;
            CheckBoxTrailingUpCanMoveExitOrder.Checked += CheckBoxTrailingUpCanMoveExitOrder_Checked;
            CheckBoxTrailingUpCanMoveExitOrder.Unchecked += CheckBoxTrailingUpCanMoveExitOrder_Checked;

            CheckBoxTrailingDownIsOn.IsChecked = tradeGrid.TrailingUp.TrailingDownIsOn;
            CheckBoxTrailingDownIsOn.Checked += CheckBoxTrailingDownIsOn_Checked; 
            CheckBoxTrailingDownIsOn.Unchecked += CheckBoxTrailingDownIsOn_Checked;

            TextBoxTrailingDownStep.Text = tradeGrid.TrailingUp.TrailingDownStep.ToString();
            TextBoxTrailingDownStep.TextChanged += TextBoxTrailingDownStep_TextChanged; 

            TextBoxTrailingDownLimit.Text = tradeGrid.TrailingUp.TrailingDownLimit.ToString();
            TextBoxTrailingDownLimit.TextChanged += TextBoxTrailingDownLimit_TextChanged;

            CheckBoxTrailingDownCanMoveExitOrder.IsChecked = tradeGrid.TrailingUp.TrailingDownCanMoveExitOrder;
            CheckBoxTrailingDownCanMoveExitOrder.Checked += CheckBoxTrailingDownCanMoveExitOrder_Checked;
            CheckBoxTrailingDownCanMoveExitOrder.Unchecked += CheckBoxTrailingDownCanMoveExitOrder_Checked;

            Localization();

            // grid table

            CreateGridTable();
            RePaintGridTable();

            CheckEnabledItems();

            Thread worker = new Thread(TableUpdateThread);
            worker.Start();
        }

        private void Localization()
        {
            Title = OsLocalization.Trader.Label444 + " # " + TradeGrid.Tab.TabName + " # " + TradeGrid.Number ;

            // settings prime

            LabelGridType.Content = OsLocalization.Trader.Label445;
            LabelRegime.Content = OsLocalization.Trader.Label448;
            LabelRegimeLogicEntry.Content = OsLocalization.Trader.Label449;
            LabelAutoClearJournal.Content = OsLocalization.Trader.Label451;
            LabelMaxClosePositionsInJournal.Content = OsLocalization.Trader.Label452;
            ButtonLoad.Content = OsLocalization.Trader.Label453;
            ButtonSave.Content = OsLocalization.Trader.Label454;
            ButtonStart.Content = OsLocalization.Trader.Label455;
            ButtonStop.Content = OsLocalization.Trader.Label456;
            ButtonClose.Content = OsLocalization.Trader.Label457;
            LabelMaxOrdersInMarket.Content = OsLocalization.Trader.Label488;
            LabelMaxOpenOrdersInMarket.Content = OsLocalization.Trader.Label508;
            LabelMaxCloseOrdersInMarket.Content = OsLocalization.Trader.Label509;
            LabelDelayInReal.Content = OsLocalization.Trader.Label569;
            LabelCheckMicroVolumes.Content = OsLocalization.Trader.Label572;
            LabelMaxDistanceToOrdersPercent.Content = OsLocalization.Trader.Label581;

            // tab controls

            TabItemBaseSettings.Header = OsLocalization.Trader.Label458;
            TabItemGridCreation.Header = OsLocalization.Trader.Label459;
            TabItemNonTradePeriods.Header = OsLocalization.Trader.Label633;
            TabItemStopTrading.Header = OsLocalization.Trader.Label463;
            TabItemStopAndProfit.Header = OsLocalization.Trader.Label464;
            TabItemGridLinesTable.Header = OsLocalization.Trader.Label465;
            TabItemAutoStart.Header = OsLocalization.Trader.Label472;
            TabItemError.Header = OsLocalization.Trader.Label537;
            TabItemTrailingUp.Header = OsLocalization.Trader.Label544;


            // trade days 
            LabelNoTradePeriod1Regime.Content = OsLocalization.Trader.Label506;
            ButtonSetNonTradePeriods.Content = OsLocalization.Trader.Label632;
            LabelOpenOrdersMakerOnly.Content = OsLocalization.Trader.Label635;

            // stop grid by event
            CheckBoxStopGridByMoveUpIsOn.Content = OsLocalization.Trader.Label481;
            LabelStopGridByMoveUpValuePercentReaction.Content = OsLocalization.Trader.Label484;
            CheckBoxStopGridByMoveDownIsOn.Content = OsLocalization.Trader.Label482;
            LabelStopGridByMoveDownValuePercentReaction.Content = OsLocalization.Trader.Label484;
            CheckBoxStopGridByPositionsCountIsOn.Content = OsLocalization.Trader.Label483;
            LabelStopGridByPositionsCountIsOnReaction.Content = OsLocalization.Trader.Label484;
            CheckBoxStopGridByLifeTimeIsOn.Content = OsLocalization.Trader.Label525; 
            LabelStopGridByLifeTimeOnReaction.Content = OsLocalization.Trader.Label484;
            CheckBoxStopGridByTimeOfDayIsOn.Content = OsLocalization.Trader.Label526;
            LabelStopGridByTimeOfDayReaction.Content = OsLocalization.Trader.Label484;
            LabelStopGridByTimeOfDayHour.Content = OsLocalization.Trader.Label527 + ":";
            LabelStopGridByTimeOfDayMinute.Content = OsLocalization.Trader.Label528 + ":";
            LabelStopGridByTimeOfDaySecond.Content = OsLocalization.Trader.Label529 + ":";

            // grid lines creation

            LabelGridSide.Content = OsLocalization.Trader.Label485;
            LabelFirstPrice.Content = OsLocalization.Trader.Label486;
            LabelLinesCount.Content = OsLocalization.Trader.Label487;
            
            LabelStep.Content = OsLocalization.Trader.Label489;
            LabelProfit.Content = OsLocalization.Trader.Label490;
            LabelVolume.Content = OsLocalization.Trader.Label491;
            LabelAsset.Content = OsLocalization.Trader.Label492;

            ButtonCreateGrid.Content = OsLocalization.Trader.Label493;
            ButtonDeleteGrid.Content = OsLocalization.Trader.Label494;
            ButtonNewLevel.Content = OsLocalization.Trader.Label495;
            ButtonRemoveSelected.Content = OsLocalization.Trader.Label496;
            LabelSelectOffToUse.Content = OsLocalization.Trader.Label530;

            // stop and profit 

            LabelProfitRegime.Content = OsLocalization.Trader.Label497;
            LabelProfitValueType.Content = OsLocalization.Trader.Label498;
            LabelProfitValue.Content = OsLocalization.Trader.Label499;

            LabelStopRegime.Content = OsLocalization.Trader.Label500;
            LabelStopValueType.Content = OsLocalization.Trader.Label498;
            LabelStopValue.Content = OsLocalization.Trader.Label499;

            LabelTrailStopRegime.Content = OsLocalization.Trader.Label531;
            LabelTrailStopValueType.Content = OsLocalization.Trader.Label498;
            LabelTrailStopValue.Content = OsLocalization.Trader.Label499;

            LabelMiddleEntryPrice.Content = OsLocalization.Trader.Label532;

            // auto start

            LabelAutoStartRegime.Content = OsLocalization.Trader.Label504;
            LabelAutoStartPrice.Content = OsLocalization.Trader.Label505;
            LabelRebuildGridRegime.Content = OsLocalization.Trader.Label535;
            LabelShiftFirstPrice.Content = OsLocalization.Trader.Label536;

            CheckBoxStartGridByTimeOfDayIsOn.Content = OsLocalization.Trader.Label634;
            LabelStartGridByTimeOfDayHour.Content = OsLocalization.Trader.Label527 + ":";
            LabelStartGridByTimeOfDayMinute.Content = OsLocalization.Trader.Label528 + ":";
            LabelStartGridByTimeOfDaySecond.Content = OsLocalization.Trader.Label529 + ":";
            // errors

            CheckBoxFailOpenOrdersReactionIsOn.Content = OsLocalization.Trader.Label538; 
            LabelFailOpenOrdersReaction.Content = OsLocalization.Trader.Label99;
            LabelFailOpenOrdersCountToReaction.Content = OsLocalization.Trader.Label539;
            LabelFailOpenOrdersCountFact.Content = OsLocalization.Trader.Label540;

            CheckBoxFailCancelOrdersReactionIsOn.Content = OsLocalization.Trader.Label541;
            LabelFailCancelOrdersReaction.Content = OsLocalization.Trader.Label99;
            LabelFailCancelOrdersCountToReaction.Content = OsLocalization.Trader.Label542;
            LabelFailCancelOrdersCountFact.Content = OsLocalization.Trader.Label543;

            CheckBoxWaitOnStartConnectorIsOn.Content = OsLocalization.Trader.Label582;
            LabelWaitSecondsOnStartConnector.Content = OsLocalization.Trader.Label583;

            // trailing up

            CheckBoxTrailingUpIsOn.Content = OsLocalization.Trader.Label545;
            LabelTrailingUpStep.Content = OsLocalization.Trader.Label549;
            LabelTrailingUpLimit.Content = OsLocalization.Trader.Label547;
            CheckBoxTrailingUpCanMoveExitOrder.Content = OsLocalization.Trader.Label571;

            CheckBoxTrailingDownIsOn.Content = OsLocalization.Trader.Label546;
            LabelTrailingDownStep.Content = OsLocalization.Trader.Label549;
            LabelTrailingDownLimit.Content = OsLocalization.Trader.Label547;
            CheckBoxTrailingDownCanMoveExitOrder.Content = OsLocalization.Trader.Label571;
        }

        private void CheckEnabledItems()
        {
            if (_gridDataGrid.InvokeRequired)
            {
                _gridDataGrid.Invoke(new Action(CheckEnabledItems));
                return;
            }

            if (TradeGrid.Regime != TradeGridRegime.Off)
            {
                ComboBoxGridType.IsEnabled = false;
                ComboBoxRegimeLogicEntry.IsEnabled = false;

                ComboBoxGridSide.IsEnabled = false;
                TextBoxFirstPrice.IsEnabled = false;
                TextBoxLineCountStart.IsEnabled = false;
                ComboBoxTypeStep.IsEnabled = false;
                TextBoxLineStep.IsEnabled = false;
                TextBoxStepMultiplicator.IsEnabled = false;
                ComboBoxTypeProfit.IsEnabled = false;
                TextBoxProfitStep.IsEnabled = false;
                TextBoxProfitMultiplicator.IsEnabled = false;
                ComboBoxTypeVolume.IsEnabled = false;
                TextBoxStartVolume.IsEnabled = false;
                TextBoxMartingaleMultiplicator.IsEnabled = false;
                TextBoxTradeAssetInPortfolio.IsEnabled = false;
                ButtonCreateGrid.IsEnabled = false;
                ButtonDeleteGrid.IsEnabled = false;
                LabelSelectOffToUse.Visibility = Visibility.Visible;
            }
            else
            { // trade regime
                ComboBoxGridType.IsEnabled = true;

                if (TradeGrid.StartProgram == StartProgram.IsTester
                   || TradeGrid.StartProgram == StartProgram.IsOsOptimizer)
                {
                    ComboBoxRegimeLogicEntry.IsEnabled = false;
                }
                else
                {
                    ComboBoxRegimeLogicEntry.IsEnabled = true;
                }
                   

                ComboBoxGridSide.IsEnabled = true;
                TextBoxFirstPrice.IsEnabled = true;
                TextBoxLineCountStart.IsEnabled = true;
                ComboBoxTypeStep.IsEnabled = true;
                TextBoxLineStep.IsEnabled = true;
                TextBoxStepMultiplicator.IsEnabled = true;
                ComboBoxTypeProfit.IsEnabled = true;
                TextBoxProfitStep.IsEnabled = true;
                TextBoxProfitMultiplicator.IsEnabled = true;
                ComboBoxTypeVolume.IsEnabled = true;
                TextBoxStartVolume.IsEnabled = true;
                TextBoxMartingaleMultiplicator.IsEnabled = true;
                TextBoxTradeAssetInPortfolio.IsEnabled = true;
                ButtonCreateGrid.IsEnabled = true;
                ButtonDeleteGrid.IsEnabled = true;
                LabelSelectOffToUse.Visibility = Visibility.Hidden;
            }

            if(TradeGrid.GridType == TradeGridPrimeType.MarketMaking)
            {
                TabItemStopAndProfit.IsEnabled = false;

                if(TabControlSecond.SelectedIndex == 2)
                {
                    TabControlSecond.SelectedIndex = 0;
                }

                CheckBoxStopGridByPositionsCountIsOn.IsEnabled = true;
                TextBoxStopGridByPositionsCountValue.IsEnabled = true;
                ComboBoxStopGridByPositionsCountReaction.IsEnabled = true;
            }
            else if(TradeGrid.GridType == TradeGridPrimeType.OpenPosition)
            {
                TabItemStopAndProfit.IsEnabled = true;

                CheckBoxStopGridByPositionsCountIsOn.IsEnabled = false;
                TextBoxStopGridByPositionsCountValue.IsEnabled = false;
                ComboBoxStopGridByPositionsCountReaction.IsEnabled = false;
            }
        }

        private void TradeGridUi_Closed(object sender, EventArgs e)
        {
            _guiIsClosed = true;

            TradeGrid.RePaintSettingsEvent -= TradeGrid_RePaintSettingsEvent;
            TradeGrid.FullRePaintGridEvent -= TradeGrid_FullRePaintGridEvent;
            TradeGrid = null;

            try
            {
                ComboBoxGridType.SelectionChanged -= ComboBoxGridType_SelectionChanged;
                ComboBoxRegime.SelectionChanged -= ComboBoxRegime_SelectionChanged;
                ComboBoxRegimeLogicEntry.SelectionChanged -= ComboBoxRegimeLogicEntry_SelectionChanged;
                ComboBoxAutoClearJournal.SelectionChanged -= ComboBoxAutoClearJournal_SelectionChanged;
                TextBoxMaxClosePositionsInJournal.TextChanged -= TextBoxMaxClosePositionsInJournal_TextChanged;
                TextBoxMaxOpenOrdersInMarket.TextChanged -= TextBoxMaxOrdersInMarket_TextChanged;
                TextBoxMaxCloseOrdersInMarket.TextChanged -= TextBoxMaxCloseOrdersInMarket_TextChanged;

                CheckBoxStopGridByMoveUpIsOn.Checked -= CheckBoxStopGridByMoveUpIsOn_Checked;
                TextBoxStopGridByMoveUpValuePercent.TextChanged -= TextBoxStopGridByMoveUpValuePercent_TextChanged;
                CheckBoxStopGridByMoveDownIsOn.Checked -= CheckBoxStopGridByMoveDownIsOn_Checked;
                TextBoxStopGridByMoveDownValuePercent.TextChanged -= TextBoxStopGridByMoveDownValuePercent_TextChanged;
                CheckBoxStopGridByPositionsCountIsOn.Checked -= CheckBoxStopGridByPositionsCountIsOn_Checked;
                TextBoxStopGridByPositionsCountValue.TextChanged -= TextBoxStopGridByPositionsCountValue_TextChanged;
                ComboBoxGridSide.SelectionChanged -= ComboBoxGridSide_SelectionChanged;
                TextBoxFirstPrice.TextChanged -= TextBoxFirstPrice_TextChanged;
                TextBoxLineCountStart.TextChanged -= TextBoxLineCountStart_TextChanged;

                CheckBoxStopGridByLifeTimeIsOn.Checked -= CheckBoxStopGridByLifeTimeIsOn_Checked;
                TextBoxStopGridByLifeTimeSecondsToLife.TextChanged -= TextBoxStopGridByLifeTimeSecondsToLife_TextChanged;
                ComboBoxStopGridByLifeTimeReaction.SelectionChanged -= ComboBoxStopGridByLifeTimeReaction_SelectionChanged;

                CheckBoxStopGridByTimeOfDayIsOn.Checked -= CheckBoxStopGridByTimeOfDayIsOn_Checked;
                TextBoxStopGridByTimeOfDayHour.TextChanged -= TextBoxStopGridByTimeOfDayHour_TextChanged;
                TextBoxStopGridByTimeOfDayMinute.TextChanged -= TextBoxStopGridByTimeOfDayMinute_TextChanged;
                TextBoxStopGridByTimeOfDaySecond.TextChanged -= TextBoxStopGridByTimeOfDaySecond_TextChanged;
                ComboBoxStopGridByTimeOfDayReaction.SelectionChanged -= ComboBoxStopGridByTimeOfDayReaction_SelectionChanged;

                ComboBoxTypeStep.SelectionChanged -= ComboBoxTypeStep_SelectionChanged;
                TextBoxLineStep.TextChanged -= TextBoxLineStep_TextChanged;
                TextBoxStepMultiplicator.TextChanged -= TextBoxStepMultiplicator_TextChanged;
                ComboBoxTypeProfit.SelectionChanged -= ComboBoxTypeProfit_SelectionChanged;
                TextBoxProfitStep.TextChanged -= TextBoxProfitStep_TextChanged;
                TextBoxProfitMultiplicator.TextChanged -= TextBoxProfitMultiplicator_TextChanged;
                TextBoxMartingaleMultiplicator.TextChanged -= TextBoxMartingaleMultiplicator_TextChanged;
                TextBoxTradeAssetInPortfolio.TextChanged -= TextBoxTradeAssetInPortfolio_TextChanged;

                ComboBoxProfitRegime.SelectionChanged -= ComboBoxProfitRegime_SelectionChanged;
                ComboBoxProfitValueType.SelectionChanged -= ComboBoxProfitValueType_SelectionChanged;
                TextBoxProfitValue.TextChanged -= TextBoxProfitValue_TextChanged;
                ComboBoxStopRegime.SelectionChanged -= ComboBoxStopRegime_SelectionChanged;
                ComboBoxStopValueType.SelectionChanged -= ComboBoxStopValueType_SelectionChanged;
                TextBoxStopValue.TextChanged -= TextBoxStopValue_TextChanged;

                ComboBoxAutoStartRegime.SelectionChanged -= ComboBoxAutoStartRegime_SelectionChanged;
                TextBoxAutoStartPrice.TextChanged -= TextBoxAutoStartPrice_TextChanged;

                CheckBoxFailOpenOrdersReactionIsOn.Checked -= CheckBoxFailOpenOrdersReactionIsOn_Checked;
                CheckBoxFailOpenOrdersReactionIsOn.Unchecked -= CheckBoxFailOpenOrdersReactionIsOn_Checked;
                ComboBoxFailOpenOrdersReaction.SelectionChanged -= ComboBoxFailOpenOrdersReaction_SelectionChanged;
                TextBoxFailOpenOrdersCountToReaction.TextChanged -= TextBoxFailOpenOrdersCountToReaction_TextChanged;
                CheckBoxFailCancelOrdersReactionIsOn.Checked -= CheckBoxFailCancelOrdersReactionIsOn_Checked;
                CheckBoxFailCancelOrdersReactionIsOn.Unchecked -= CheckBoxFailCancelOrdersReactionIsOn_Checked;
                ComboBoxFailCancelOrdersReaction.SelectionChanged -= ComboBoxFailCancelOrdersReaction_SelectionChanged;
                TextBoxFailCancelOrdersCountToReaction.TextChanged -= TextBoxFailCancelOrdersCountToReaction_TextChanged;

                CheckBoxTrailingUpIsOn.Checked -= CheckBoxTrailingUpIsOn_Checked;
                CheckBoxTrailingUpIsOn.Unchecked -= CheckBoxTrailingUpIsOn_Checked;
                TextBoxTrailingUpStep.TextChanged -= TextBoxTrailingUpStep_TextChanged;
                TextBoxTrailingUpLimit.TextChanged -= TextBoxTrailingUpLimit_TextChanged;
                CheckBoxTrailingDownIsOn.Checked -= CheckBoxTrailingDownIsOn_Checked;
                CheckBoxTrailingDownIsOn.Unchecked -= CheckBoxTrailingDownIsOn_Checked;
                TextBoxTrailingDownStep.TextChanged -= TextBoxTrailingDownStep_TextChanged;
                TextBoxTrailingDownLimit.TextChanged -= TextBoxTrailingDownLimit_TextChanged;
            }
            catch
            {
                // ignore
            }

            try
            {
                if (_gridDataGrid != null)
                {
                    HostGridTable.Child = null;

                    DataGridFactory.ClearLinks(_gridDataGrid);
                    _gridDataGrid.DataError -= _gridDataGrid_DataError;
                    _gridDataGrid.CellValueChanged -= EventChangeValueInTable;
                    _gridDataGrid.CellClick -= _gridDataGrid_CellClick;
                    _gridDataGrid.Rows.Clear();
                    _gridDataGrid = null;
                    HostGridTable = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        private void TradeGrid_RePaintSettingsEvent()
        {
            try
            {
                if (_gridDataGrid.InvokeRequired)
                {
                    _gridDataGrid.Invoke(new Action(TradeGrid_RePaintSettingsEvent));
                    return;
                }

                ComboBoxRegime.SelectionChanged -= ComboBoxRegime_SelectionChanged;
                ComboBoxRegime.SelectedItem = TradeGrid.Regime.ToString();
                ComboBoxRegime.SelectionChanged += ComboBoxRegime_SelectionChanged;

                ComboBoxAutoStartRegime.SelectionChanged -= ComboBoxAutoStartRegime_SelectionChanged;
                ComboBoxAutoStartRegime.SelectedItem = TradeGrid.AutoStarter.AutoStartRegime.ToString();
                ComboBoxAutoStartRegime.SelectionChanged += ComboBoxAutoStartRegime_SelectionChanged;

                CheckEnabledItems();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TradeGrid_FullRePaintGridEvent()
        {
            RePaintGridTable();
        }

        private bool _guiIsClosed;

        public TradeGrid TradeGrid;

        public int Number;

        #region Errors reaction

        private void CheckBoxFailOpenOrdersReactionIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.ErrorsReaction.FailOpenOrdersReactionIsOn = CheckBoxFailOpenOrdersReactionIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxFailOpenOrdersReaction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxFailOpenOrdersReaction.SelectedItem.ToString(), out TradeGrid.ErrorsReaction.FailOpenOrdersReaction);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxFailOpenOrdersCountToReaction_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxFailOpenOrdersCountToReaction.Text))
                {
                    return;
                }

                TradeGrid.ErrorsReaction.FailOpenOrdersCountToReaction = Convert.ToInt32(TextBoxFailOpenOrdersCountToReaction.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxFailCancelOrdersReactionIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.ErrorsReaction.FailCancelOrdersReactionIsOn = CheckBoxFailCancelOrdersReactionIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxFailCancelOrdersReaction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxFailCancelOrdersReaction.SelectedItem.ToString(), out TradeGrid.ErrorsReaction.FailCancelOrdersReaction);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxFailCancelOrdersCountToReaction_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxFailCancelOrdersCountToReaction.Text))
                {
                    return;
                }

                TradeGrid.ErrorsReaction.FailCancelOrdersCountToReaction = Convert.ToInt32(TextBoxFailCancelOrdersCountToReaction.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxWaitOnStartConnectorIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.ErrorsReaction.WaitOnStartConnectorIsOn = CheckBoxWaitOnStartConnectorIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxWaitSecondsOnStartConnector_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxWaitSecondsOnStartConnector.Text))
                {
                    return;
                }

                TradeGrid.ErrorsReaction.WaitSecondsOnStartConnector = Convert.ToInt32(TextBoxWaitSecondsOnStartConnector.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Trailing Up Down

        private int _trailingUpErrorsCountLimit;

        private int _trailingUpErrorsCountStep;

        private void CheckBoxTrailingUpIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                bool value = CheckBoxTrailingUpIsOn.IsChecked.Value;

                if(value == true)
                {
                    bool haveBadFields = false;
                    if(TradeGrid.TrailingUp.TrailingUpLimit == 0)
                    {
                        TextBoxTrailingUpLimit.Text = OsLocalization.Trader.Label551;
                        haveBadFields = true;
                        _trailingUpErrorsCountLimit++;
                    }
                    if (TradeGrid.TrailingUp.TrailingUpStep == 0)
                    {
                        TextBoxTrailingUpStep.Text = OsLocalization.Trader.Label551;
                        haveBadFields = true;
                        _trailingUpErrorsCountStep++;
                    }
                    if(haveBadFields == true)
                    {
                        CheckBoxTrailingUpIsOn.IsChecked = false;

                        if(_trailingUpErrorsCountLimit > 2
                            || _trailingUpErrorsCountStep > 2)
                        {
                            _trailingUpErrorsCountStep = 0;
                            _trailingUpErrorsCountLimit = 0;

                            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label552);
                            ui.ShowDialog();
                        }

                        return;
                    }
                }

                TradeGrid.TrailingUp.TrailingUpIsOn = value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxTrailingUpStep_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxTrailingUpStep.Text))
                {
                    return;
                }

                TradeGrid.TrailingUp.TrailingUpStep = TextBoxTrailingUpStep.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxTrailingUpLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxTrailingUpLimit.Text)) 
                {
                    return;
                }

                TradeGrid.TrailingUp.TrailingUpLimit = TextBoxTrailingUpLimit.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTrailingDownCanMoveExitOrder_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                bool value = CheckBoxTrailingDownCanMoveExitOrder.IsChecked.Value;

                TradeGrid.TrailingUp.TrailingDownCanMoveExitOrder = value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private int _trailingDownErrorsCountLimit;

        private int _trailingDownErrorsCountStep;

        private void CheckBoxTrailingDownIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                bool value = CheckBoxTrailingDownIsOn.IsChecked.Value;

                if (value == true)
                {
                    bool haveBadFields = false;
                    if (TradeGrid.TrailingUp.TrailingDownLimit == 0)
                    {
                        TextBoxTrailingDownLimit.Text = OsLocalization.Trader.Label551;
                        haveBadFields = true;
                        _trailingDownErrorsCountLimit++;
                    }
                    if (TradeGrid.TrailingUp.TrailingDownStep == 0)
                    {
                        TextBoxTrailingDownStep.Text = OsLocalization.Trader.Label551;
                        haveBadFields = true;
                        _trailingDownErrorsCountStep++;
                    }
                    if (haveBadFields == true)
                    {
                        CheckBoxTrailingDownIsOn.IsChecked = false;

                        if (_trailingDownErrorsCountLimit > 2
                            || _trailingDownErrorsCountStep > 2)
                        {
                            _trailingDownErrorsCountStep = 0;
                            _trailingDownErrorsCountLimit = 0;

                            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label552);
                            ui.ShowDialog();
                        }

                        return;
                    }
                }

                TradeGrid.TrailingUp.TrailingDownIsOn = value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxTrailingDownStep_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxTrailingDownStep.Text))
                {
                    return;
                }

                TradeGrid.TrailingUp.TrailingDownStep = TextBoxTrailingDownStep.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxTrailingDownLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxTrailingDownLimit.Text))
                {
                    return;
                }

                TradeGrid.TrailingUp.TrailingDownLimit = TextBoxTrailingDownLimit.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTrailingUpCanMoveExitOrder_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                bool value = CheckBoxTrailingUpCanMoveExitOrder.IsChecked.Value;

                TradeGrid.TrailingUp.TrailingUpCanMoveExitOrder = value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Stop and profit 

        private void ComboBoxProfitRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxProfitRegime.SelectedItem.ToString(), out TradeGrid.StopAndProfit.ProfitRegime);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxProfitValueType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxProfitValueType.SelectedItem.ToString(), out TradeGrid.StopAndProfit.ProfitValueType);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxProfitValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxProfitValue.Text))
                {
                    return;
                }

                TradeGrid.StopAndProfit.ProfitValue = TextBoxProfitValue.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxStopRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if(ComboBoxStopRegime.SelectedItem.ToString() != TradeGrid.StopAndProfit.StopRegime.ToString())
                {
                    Enum.TryParse(ComboBoxStopRegime.SelectedItem.ToString(), out TradeGrid.StopAndProfit.StopRegime);
                    TradeGrid.Save();

                    if (TradeGrid.StopAndProfit.StopRegime != OnOffRegime.Off)
                    {
                        ComboBoxTrailStopRegime.SelectedItem = OnOffRegime.Off.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxStopValueType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxStopValueType.SelectedItem.ToString(), out TradeGrid.StopAndProfit.StopValueType);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxStopValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopValue.Text))
                {
                    return;
                }

                TradeGrid.StopAndProfit.StopValue = TextBoxStopValue.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxTrailStopValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxTrailStopValue.Text))
                {
                    return;
                }

                TradeGrid.StopAndProfit.TrailStopValue = TextBoxTrailStopValue.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxTrailStopValueType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxTrailStopValueType.SelectedItem.ToString(), out TradeGrid.StopAndProfit.TrailStopValueType);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxTrailStopRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if(ComboBoxTrailStopRegime.SelectedItem.ToString() != TradeGrid.StopAndProfit.TrailStopRegime.ToString())
                {
                    Enum.TryParse(ComboBoxTrailStopRegime.SelectedItem.ToString(), out TradeGrid.StopAndProfit.TrailStopRegime);
                    TradeGrid.Save();


                    if (TradeGrid.StopAndProfit.TrailStopRegime != OnOffRegime.Off)
                    {
                        ComboBoxStopRegime.SelectedItem = OnOffRegime.Off.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Grid lines creation

        private void ComboBoxGridSide_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxGridSide.SelectedItem.ToString(), out TradeGrid.GridCreator.GridSide);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxFirstPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxFirstPrice.Text))
                {
                    return;
                }

                TradeGrid.GridCreator.FirstPrice = TextBoxFirstPrice.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxLineCountStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxLineCountStart.Text))
                {
                    return;
                }

                TradeGrid.GridCreator.LineCountStart = Convert.ToInt32(TextBoxLineCountStart.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxTypeStep_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxTypeStep.SelectedItem.ToString(), out TradeGrid.GridCreator.TypeStep);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxLineStep_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxLineStep.Text))
                {
                    return;
                }

                TradeGrid.GridCreator.LineStep = TextBoxLineStep.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStepMultiplicator_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStepMultiplicator.Text))
                {
                    return;
                }

                TradeGrid.GridCreator.StepMultiplicator = TextBoxStepMultiplicator.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxTypeProfit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxTypeProfit.SelectedItem.ToString(), out TradeGrid.GridCreator.TypeProfit);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxProfitStep_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxProfitStep.Text))
                {
                    return;
                }

                TradeGrid.GridCreator.ProfitStep = TextBoxProfitStep.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxProfitMultiplicator_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxProfitMultiplicator.Text))
                {
                    return;
                }

                TradeGrid.GridCreator.ProfitMultiplicator = TextBoxProfitMultiplicator.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxTypeVolume_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxTypeVolume.SelectedItem.ToString(), out TradeGrid.GridCreator.TypeVolume);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxStartVolume_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStartVolume.Text))
                {
                    return;
                }

                TradeGrid.GridCreator.StartVolume = TextBoxStartVolume.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxMartingaleMultiplicator_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMartingaleMultiplicator.Text))
                {
                    return;
                }

                TradeGrid.GridCreator.MartingaleMultiplicator = TextBoxMartingaleMultiplicator.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxTradeAssetInPortfolio_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxTradeAssetInPortfolio.Text))
                {
                    return;
                }

                TradeGrid.GridCreator.TradeAssetInPortfolio = TextBoxTradeAssetInPortfolio.Text;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ButtonCreateGrid_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.CreateNewGridSafe();
                RePaintGridTable();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonDeleteGrid_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(TradeGrid.GridCreator.Lines == null 
                    || TradeGrid.GridCreator.Lines.Count == 0)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label550);

                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                TradeGrid.DeleteGrid();
                RePaintGridTable();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonNewLevel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.CreateNewLine();
                RePaintGridTable();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<int> numbers = new List<int>();

                for (int i = 0; i < _gridDataGrid.Rows.Count; i++)
                {
                    if (_gridDataGrid.Rows[i].Cells[7].Value.ToString() != "Unchecked")
                    {
                        numbers.Add(i);
                    }
                }

                if (numbers.Count == 0)
                {
                    return;
                }

                TradeGrid.RemoveSelected(numbers);
                RePaintGridTable();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Grid paint in table

        private DataGridView _gridDataGrid;

        private void TableUpdateThread()
        {
            while(true)
            {
                try
                {
                    if (_guiIsClosed == true)
                    {
                        return;
                    }

                    Thread.Sleep(3000);
                    TryUpdateGridTable();
                }
                catch (Exception ex)
                {
                    TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                }
            }
        }

        private void CreateGridTable()
        {
            try
            {
                if (MainWindow.GetDispatcher.CheckAccess() == false)
                {
                    MainWindow.GetDispatcher.Invoke(new Action(CreateGridTable));
                    return;
                }

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
                newColumn0.HeaderText = "#";
                _gridDataGrid.Columns.Add(newColumn0);
                newColumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn1 = new DataGridViewColumn();
                newColumn1.CellTemplate = cellParam0;
                newColumn1.HeaderText = OsLocalization.Trader.Label20;
                _gridDataGrid.Columns.Add(newColumn1);
                newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn2 = new DataGridViewColumn();
                newColumn2.CellTemplate = cellParam0;
                newColumn2.HeaderText = OsLocalization.Trader.Label400;
                _gridDataGrid.Columns.Add(newColumn2);
                newColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn3 = new DataGridViewColumn();
                newColumn3.CellTemplate = cellParam0;
                newColumn3.HeaderText = OsLocalization.Trader.Label401;
                _gridDataGrid.Columns.Add(newColumn3);
                newColumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn4 = new DataGridViewColumn();
                newColumn4.CellTemplate = cellParam0;
                newColumn4.HeaderText = OsLocalization.Trader.Label491;
                _gridDataGrid.Columns.Add(newColumn4);
                newColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn5 = new DataGridViewColumn();
                newColumn5.CellTemplate = cellParam0;
                newColumn5.HeaderText = OsLocalization.Trader.Label403;
                _gridDataGrid.Columns.Add(newColumn5);
                newColumn5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn6 = new DataGridViewColumn();
                newColumn6.CellTemplate = cellParam0;
                newColumn6.HeaderText = OsLocalization.Trader.Label485;
                _gridDataGrid.Columns.Add(newColumn6);
                newColumn6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn7 = new DataGridViewColumn();
                newColumn7.CellTemplate = cellParam0;
                _gridDataGrid.Columns.Add(newColumn7);
                newColumn7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                HostGridTable.Child = _gridDataGrid;

                _gridDataGrid.DataError += _gridDataGrid_DataError;
                _gridDataGrid.CellValueChanged += EventChangeValueInTable;
                _gridDataGrid.CellClick += _gridDataGrid_CellClick;
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void RePaintGridTable()
        {
            try
            {
                if(_gridDataGrid == null)
                {
                    return;
                }

                if (_gridDataGrid.InvokeRequired)
                {
                    _gridDataGrid.Invoke(new Action(RePaintGridTable));
                    return;
                }

                _tableIsRePainted = true;

                _gridDataGrid.CellValueChanged -= EventChangeValueInTable;
                _gridDataGrid.Rows.Clear();

                for (int i = 0; i < TradeGrid.GridCreator.Lines.Count; i++)
                {
                    TradeGridLine curLine = TradeGrid.GridCreator.Lines[i];

                    DataGridViewRow rowLine = new DataGridViewRow();

                    Position curPosition = curLine.Position;

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[0].Value = i + 1;
                    rowLine.Cells[0].ReadOnly = true;

                    if(curPosition == null)
                    {
                        rowLine.Cells.Add(new DataGridViewTextBoxCell());
                        rowLine.Cells[1].Value = "_";
                        rowLine.Cells[1].ReadOnly = true;
                    }
                    else
                    {
                        DataGridViewButtonCell buttonCell = new DataGridViewButtonCell();
                        buttonCell.Value = curPosition.Number;
                        rowLine.Cells.Add(buttonCell);
                    }

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[2].Value = Math.Round(curLine.PriceEnter, 10);
                    rowLine.Cells[2].ReadOnly = false;

                    if(TradeGrid.GridType == TradeGridPrimeType.MarketMaking)
                    {
                        rowLine.Cells.Add(new DataGridViewTextBoxCell());
                        rowLine.Cells[3].Value = Math.Round(curLine.PriceExit, 10);
                        rowLine.Cells[3].ReadOnly = false;
                    }
                    else if(TradeGrid.GridType == TradeGridPrimeType.OpenPosition)
                    {
                        rowLine.Cells.Add(new DataGridViewTextBoxCell());
                        rowLine.Cells[3].Value = "_";
                        rowLine.Cells[3].ReadOnly = true;
                    }

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[4].Value = Math.Round(curLine.Volume, 10);
                    rowLine.Cells[4].ReadOnly = false;

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());

                    if(curPosition != null)
                    {
                        rowLine.Cells[5].Value = curPosition.OpenVolume;
                        rowLine.Cells[5].Style.ForeColor = Color.Green;
                    }
                    else
                    {
                        rowLine.Cells[5].Value = "0";
                    }

                    rowLine.Cells[5].ReadOnly = true;

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[6].Value = curLine.Side;
                    rowLine.Cells[6].ReadOnly = true;

                    rowLine.Cells.Add(new DataGridViewCheckBoxCell());
                    rowLine.Cells[7].Value = CheckState.Unchecked;
                    rowLine.Cells[7].ReadOnly = false;

                    _gridDataGrid.Rows.Add(rowLine);
                }

                _gridDataGrid.CellValueChanged += EventChangeValueInTable;
                
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
            _tableIsRePainted = false;
        }

        private bool _tableIsRePainted = false;

        private void TryUpdateGridTable()
        {
            try
            {
                if (_tableIsRePainted)
                {
                    return;
                }

                if (_gridDataGrid == null)
                {
                    return;
                }


                if (_gridDataGrid.InvokeRequired)
                {
                    _gridDataGrid.Invoke(new Action(TryUpdateGridTable));
                    return;
                }

                TextBoxFailOpenOrdersCountFact.Text = TradeGrid.ErrorsReaction.FailOpenOrdersCountFact.ToString();
                TextBoxFailCancelOrdersCountFact.Text = TradeGrid.ErrorsReaction.FailCancelOrdersCountFact.ToString();


                if (TradeGrid.GridType == TradeGridPrimeType.OpenPosition)
                {
                    if(TradeGrid.Regime != TradeGridRegime.Off)
                    {
                        decimal middleEntryPrice = TradeGrid.MiddleEntryPrice;

                        middleEntryPrice = Math.Round(middleEntryPrice, 8);

                        TextBoxMiddleEntryPrice.Text = middleEntryPrice.ToString();
                    }
                    else if(TradeGrid.Regime == TradeGridRegime.Off)
                    {
                        TextBoxMiddleEntryPrice.Text = "0";
                    }
                }

                List<TradeGridLine> lines = TradeGrid.GridCreator.Lines;

                if (lines.Count != _gridDataGrid.Rows.Count)
                {
                    return;
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    TradeGridLine curLine = lines[i];

                    DataGridViewRow rowLine = _gridDataGrid.Rows[i];

                    Position curPosition = curLine.Position;

                    if (curPosition == null)
                    {
                        if (rowLine.Cells[1].Value.ToString() != "_")
                        {
                            rowLine.Cells[1] = new DataGridViewTextBoxCell();
                            rowLine.Cells[1].Value = "_";
                        }
                    }
                    else
                    {
                        if (rowLine.Cells[1].Value.ToString() != curPosition.Number.ToString())
                        {
                            rowLine.Cells[1] = new DataGridViewButtonCell();
                            
                            rowLine.Cells[1].Value = curPosition.Number.ToString();
                        }
                    }

                    if (TradeGrid.GridType == TradeGridPrimeType.MarketMaking
                        && rowLine.Cells[3].Value.ToString() == "_")
                    {
                        rowLine.Cells[3].Value = Math.Round(curLine.PriceExit, 10);
                        rowLine.Cells[3].ReadOnly = false;
                    }
                    else if (TradeGrid.GridType == TradeGridPrimeType.OpenPosition
                        && rowLine.Cells[3].Value.ToString() != "_")
                    {
                        rowLine.Cells[3].Value = "_";
                        rowLine.Cells[3].ReadOnly = true;
                    }

                    if (curPosition == null)
                    {
                        if (rowLine.Cells[5].Value.ToString() != "0")
                        {
                            rowLine.Cells[5].Style.ForeColor = rowLine.Cells[0].Style.ForeColor;
                            rowLine.Cells[5].Value = "0";
                        }
                    }
                    else
                    {
                        if (rowLine.Cells[5].Value.ToString() != curPosition.OpenVolume.ToString())
                        {
                            rowLine.Cells[5].Value = curPosition.OpenVolume.ToString();

                            if(curPosition.OpenVolume != 0)
                            {
                                rowLine.Cells[5].Style.ForeColor = Color.Green;
                            }
                            else
                            {
                                rowLine.Cells[5].Style.ForeColor = rowLine.Cells[0].Style.ForeColor;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TradeGrid?.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void EventChangeValueInTable(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                List<TradeGridLine> Lines = TradeGrid.GridCreator.Lines;

                bool needToSave = false;

                for (int i = 0; i < Lines.Count; i++)
                {
                    decimal priceEntry = _gridDataGrid.Rows[i].Cells[2].Value.ToString().ToDecimal();

                    if(Lines[i].PriceEnter != priceEntry)
                    {
                        Lines[i].PriceEnter = priceEntry;
                        needToSave = true;
                    }
                    
                    if(_gridDataGrid.Rows[i].Cells[3].Value.ToString() != "_")
                    {
                        decimal priceExit = _gridDataGrid.Rows[i].Cells[3].Value.ToString().ToDecimal();

                        if (Lines[i].PriceExit != priceExit)
                        {
                            Lines[i].PriceExit = priceExit;
                            Lines[i].CanReplaceExitOrder = true;
                            needToSave = true;
                        }
                    }

                    decimal volume = _gridDataGrid.Rows[i].Cells[4].Value.ToString().ToDecimal();

                    if(Lines[i].Volume != volume)
                    {
                        Lines[i].Volume = volume;
                        needToSave = true;
                    }
                }

                if(needToSave == true)
                {
                    TradeGrid.Save();
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }

            TradeGrid.Save();
        }

        private void _gridDataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            TradeGrid.SendNewLogMessage(e.Exception.ToString(), OsEngine.Logging.LogMessageType.Error);
        }

        private void _gridDataGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int row = e.RowIndex;
                int column = e.ColumnIndex;

                if(row >= _gridDataGrid.Rows.Count)
                {
                    return;
                }

                if (column == 1)
                {
                    if (_gridDataGrid.Rows[row].Cells[column].Value == null)
                    {
                        return;
                    }

                    if(_gridDataGrid.Rows[row].Cells[column].Value.ToString() == "_")
                    {
                        return;
                    }

                    int number = Convert.ToInt32(_gridDataGrid.Rows[row].Cells[column].Value.ToString());

                    Position pos = TradeGrid.Tab._journal.GetPositionForNumber(number);

                    if (pos != null)
                    {
                        PositionUi ui = new PositionUi(pos, TradeGrid.StartProgram);
                        ui.ShowDialog();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Stop grid by event

        private void CheckBoxStopGridByMoveUpIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.StopBy.StopGridByMoveUpIsOn = CheckBoxStopGridByMoveUpIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByMoveUpValuePercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByMoveUpValuePercent.Text))
                {
                    return;
                }

                TradeGrid.StopBy.StopGridByMoveUpValuePercent = TextBoxStopGridByMoveUpValuePercent.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxStopGridByMoveUpReaction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxStopGridByMoveUpReaction.SelectedItem.ToString(), out TradeGrid.StopBy.StopGridByMoveUpReaction);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxStopGridByMoveDownIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.StopBy.StopGridByMoveDownIsOn = CheckBoxStopGridByMoveDownIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByMoveDownValuePercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByMoveDownValuePercent.Text))
                {
                    return;
                }

                TradeGrid.StopBy.StopGridByMoveDownValuePercent = TextBoxStopGridByMoveDownValuePercent.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxStopGridByMoveDownReaction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxStopGridByMoveDownReaction.SelectedItem.ToString(), out TradeGrid.StopBy.StopGridByMoveDownReaction);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxStopGridByPositionsCountIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.StopBy.StopGridByPositionsCountIsOn = CheckBoxStopGridByPositionsCountIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByPositionsCountValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByPositionsCountValue.Text))
                {
                    return;
                }

                TradeGrid.StopBy.StopGridByPositionsCountValue = Convert.ToInt32(TextBoxStopGridByPositionsCountValue.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxStopGridByPositionsCountReaction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxStopGridByPositionsCountReaction.SelectedItem.ToString(), out TradeGrid.StopBy.StopGridByPositionsCountReaction);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxStopGridByLifeTimeIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.StopBy.StopGridByLifeTimeIsOn = CheckBoxStopGridByLifeTimeIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByLifeTimeSecondsToLife_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByLifeTimeSecondsToLife.Text))
                {
                    return;
                }

                TradeGrid.StopBy.StopGridByLifeTimeSecondsToLife = Convert.ToInt32(TextBoxStopGridByLifeTimeSecondsToLife.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxStopGridByLifeTimeReaction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxStopGridByLifeTimeReaction.SelectedItem.ToString(), out TradeGrid.StopBy.StopGridByLifeTimeReaction);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxStopGridByTimeOfDayIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.StopBy.StopGridByTimeOfDayIsOn = CheckBoxStopGridByTimeOfDayIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByTimeOfDayHour_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByTimeOfDayHour.Text))
                {
                    return;
                }

                TradeGrid.StopBy.StopGridByTimeOfDayHour = Convert.ToInt32(TextBoxStopGridByTimeOfDayHour.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByTimeOfDayMinute_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByTimeOfDayMinute.Text))
                {
                    return;
                }

                TradeGrid.StopBy.StopGridByTimeOfDayMinute = Convert.ToInt32(TextBoxStopGridByTimeOfDayMinute.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStopGridByTimeOfDaySecond_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStopGridByTimeOfDaySecond.Text))
                {
                    return;
                }

                TradeGrid.StopBy.StopGridByTimeOfDaySecond = Convert.ToInt32(TextBoxStopGridByTimeOfDaySecond.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxStopGridByTimeOfDayReaction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxStopGridByTimeOfDayReaction.SelectedItem.ToString(), out TradeGrid.StopBy.StopGridByTimeOfDayReaction);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Auto start

        private void TextBoxAutoStartPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxAutoStartPrice.Text))
                {
                    return;
                }

                TradeGrid.AutoStarter.AutoStartPrice = TextBoxAutoStartPrice.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxAutoStartRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if(ComboBoxAutoStartRegime.SelectedItem.ToString() != TradeGrid.AutoStarter.AutoStartRegime.ToString())
                {
                    Enum.TryParse(ComboBoxAutoStartRegime.SelectedItem.ToString(), out TradeGrid.AutoStarter.AutoStartRegime);
                    TradeGrid.Save();
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxShiftFirstPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxShiftFirstPrice.Text))
                {
                    return;
                }

                TradeGrid.AutoStarter.ShiftFirstPrice = TextBoxShiftFirstPrice.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxRebuildGridRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ComboBoxRebuildGridRegime.SelectedItem.ToString() != TradeGrid.AutoStarter.RebuildGridRegime.ToString())
                {
                    Enum.TryParse(ComboBoxRebuildGridRegime.SelectedItem.ToString(), out TradeGrid.AutoStarter.RebuildGridRegime);
                    TradeGrid.Save();
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxStartGridByTimeOfDayIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.AutoStarter.StartGridByTimeOfDayIsOn = CheckBoxStartGridByTimeOfDayIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStartGridByTimeOfDayHour_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStartGridByTimeOfDayHour.Text))
                {
                    return;
                }

                TradeGrid.AutoStarter.StartGridByTimeOfDayHour = Convert.ToInt32(TextBoxStartGridByTimeOfDayHour.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStartGridByTimeOfDayMinute_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStartGridByTimeOfDayMinute.Text))
                {
                    return;
                }

                TradeGrid.AutoStarter.StartGridByTimeOfDayMinute = Convert.ToInt32(TextBoxStartGridByTimeOfDayMinute.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxStartGridByTimeOfDaySecond_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxStartGridByTimeOfDaySecond.Text))
                {
                    return;
                }

                TradeGrid.AutoStarter.StartGridByTimeOfDaySecond = Convert.ToInt32(TextBoxStartGridByTimeOfDaySecond.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Non trade periods

        private void ButtonSetNonTradePeriods_Click(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriods.ShowDialog();
        }

        private void ComboBoxNonTradePeriod1Regime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxNonTradePeriod1Regime.SelectedItem.ToString(), out TradeGrid.NonTradePeriods.NonTradePeriod1Regime);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxOpenOrdersMakerOnly_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                TradeGrid.OpenOrdersMakerOnly = Convert.ToBoolean(ComboBoxOpenOrdersMakerOnly.SelectedItem.ToString());
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Regime Tab

        private void TextBoxMaxClosePositionsInJournal_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMaxClosePositionsInJournal.Text))
                {
                    return;
                }

                TradeGrid.MaxClosePositionsInJournal = Convert.ToInt32(TextBoxMaxClosePositionsInJournal.Text);
                TradeGrid.Save();
            }
            catch
            {
               // ignore
            }
        }

        private void ComboBoxAutoClearJournal_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                TradeGrid.AutoClearJournalIsOn = Convert.ToBoolean(ComboBoxAutoClearJournal.SelectedItem.ToString());
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxCheckMicroVolumes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                TradeGrid.CheckMicroVolumes = Convert.ToBoolean(ComboBoxCheckMicroVolumes.SelectedItem.ToString());
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxRegimeLogicEntry_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxRegimeLogicEntry.SelectedItem.ToString(), out TradeGrid.RegimeLogicEntry);
                TradeGrid.Save();

                if(TradeGrid.StartProgram == StartProgram.IsOsTrader
                    && TradeGrid.RegimeLogicEntry == TradeGridLogicEntryRegime.OnTrade)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label534);
                    ui.ShowDialog();

                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if(ComboBoxRegime.SelectedItem.ToString() != TradeGrid.Regime.ToString())
                {
                    TradeGridRegime regime;

                    if(Enum.TryParse(ComboBoxRegime.SelectedItem.ToString(), out regime))
                    {
                        TradeGrid.Regime = regime;
                        TradeGrid.Save();
                        TradeGrid.RePaintGrid();
                        CheckEnabledItems();
                    }
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxGridType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ComboBoxGridType.SelectedItem.ToString() != TradeGrid.GridType.ToString())
            {
                Enum.TryParse(ComboBoxGridType.SelectedItem.ToString(), out TradeGrid.GridType);
                TradeGrid.Save();
                TradeGrid.RePaintGrid();
                CheckEnabledItems();
            }
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(TradeGrid.Regime != TradeGridRegime.On)
                {
                    TradeGrid.SendNewLogMessage("User start grid manually. Regime ON", Logging.LogMessageType.User);

                    ComboBoxRegime.SelectedItem = TradeGridRegime.On.ToString();
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TradeGrid.Regime != TradeGridRegime.Off)
                {
                    TradeGrid.SendNewLogMessage("User stop grid manually. Regime Off", Logging.LogMessageType.User);

                    ComboBoxRegime.SelectedItem = TradeGridRegime.Off.ToString();
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TradeGrid.Regime != TradeGridRegime.CloseForced)
                {
                    TradeGrid.SendNewLogMessage("User close grid manually. Regime CloseForced", Logging.LogMessageType.User);

                    ComboBoxRegime.SelectedItem = TradeGridRegime.CloseForced.ToString();
                }
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxMaxOrdersInMarket_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMaxOpenOrdersInMarket.Text))
                {
                    return;
                }

                TradeGrid.MaxOpenOrdersInMarket = Convert.ToInt32(TextBoxMaxOpenOrdersInMarket.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxMaxCloseOrdersInMarket_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMaxCloseOrdersInMarket.Text))
                {
                    return;
                }

                TradeGrid.MaxCloseOrdersInMarket = Convert.ToInt32(TextBoxMaxCloseOrdersInMarket.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                saveFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";

                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(saveFileDialog.FileName))
                {
                    return;
                }

                string filePath = saveFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    using (FileStream stream = File.Create(filePath))
                    {
                        // do nothin
                    }
                }

                try
                {
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        TradeGridRegime regime = TradeGrid.Regime;

                        if(TradeGrid.Regime != TradeGridRegime.Off)
                        {
                            TradeGrid.Regime = TradeGridRegime.Off;
                        }

                        writer.WriteLine(TradeGrid.GetSaveString());

                        if (TradeGrid.Regime != regime)
                        {
                            TradeGrid.Regime = regime;
                        }
                    }
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();
                }
            }
            catch
            {
                // ignore
            }
        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.InitialDirectory = System.Windows.Forms.Application.StartupPath;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.ShowDialog();

                if (string.IsNullOrEmpty(openFileDialog.FileName))
                {
                    return;
                }

                string filePath = openFileDialog.FileName;

                if (File.Exists(filePath) == false)
                {
                    return;
                }

                try
                {
                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        string fileStr = reader.ReadToEnd();

                        int gridNumber = TradeGrid.Number;

                        TradeGrid.LoadFromString(fileStr);
                        TradeGrid.Number = gridNumber;

                        TradeGrid.Save();
                    }
                }
                catch (Exception error)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(error.ToString());
                    ui.ShowDialog();

                    return;
                }

                CustomMessageBoxUi uiDialog = new CustomMessageBoxUi(OsLocalization.Trader.Label553);
                uiDialog.ShowDialog();
                Close();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxDelayInReal_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxDelayInReal.Text))
                {
                    return;
                }

                TradeGrid.DelayInReal = Convert.ToInt32(TextBoxDelayInReal.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxMaxDistanceToOrdersPercent_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxMaxDistanceToOrdersPercent.Text))
                {
                    return;
                }

                TradeGrid.MaxDistanceToOrdersPercent = TextBoxMaxDistanceToOrdersPercent.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

    }
}
