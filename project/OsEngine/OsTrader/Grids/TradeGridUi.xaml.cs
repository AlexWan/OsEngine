/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Market.Servers.BingX.BingXFutures.Entity;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;


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
            Number = TradeGrid.Number;

            Closed += TradeGridUi_Closed;

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "TradeGridUi" + tradeGrid.Number + tradeGrid.Tab.TabName);

            // settings prime

            ComboBoxGridType.Items.Add(TradeGridPrimeType.MarketMaking.ToString());
            ComboBoxGridType.Items.Add(TradeGridPrimeType.OpenPosition.ToString());
            ComboBoxGridType.Items.Add(TradeGridPrimeType.ClosePosition.ToString());
            ComboBoxGridType.SelectedItem = tradeGrid.GridType.ToString();
            ComboBoxGridType.SelectionChanged += ComboBoxGridType_SelectionChanged;

            TextBoxClosePositionNumber.Text = tradeGrid.ClosePositionNumber.ToString();
            TextBoxClosePositionNumber.TextChanged += TextBoxClosePositionNumber_TextChanged;

            ComboBoxRegime.Items.Add(TradeGridRegime.Off.ToString());
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

            ComboBoxRegimeLogging.Items.Add(TradeGridLoggingRegime.Standard.ToString());
            ComboBoxRegimeLogging.Items.Add(TradeGridLoggingRegime.Debug.ToString());
            ComboBoxRegimeLogging.SelectedItem = tradeGrid.RegimeLogging.ToString();
            ComboBoxRegimeLogging.SelectionChanged += ComboBoxRegimeLogging_SelectionChanged;

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

            // non trade periods

            CheckBoxNonTradePeriod1OnOff.IsChecked = tradeGrid.NonTradePeriods.NonTradePeriod1OnOff;
            CheckBoxNonTradePeriod1OnOff.Checked += CheckBoxNonTradePeriod1OnOff_Checked;

            CheckBoxNonTradePeriod2OnOff.IsChecked = tradeGrid.NonTradePeriods.NonTradePeriod2OnOff;
            CheckBoxNonTradePeriod2OnOff.Checked += CheckBoxNonTradePeriod2OnOff_Checked;

            CheckBoxNonTradePeriod3OnOff.IsChecked = tradeGrid.NonTradePeriods.NonTradePeriod3OnOff;
            CheckBoxNonTradePeriod3OnOff.Checked += CheckBoxNonTradePeriod3OnOff_Checked;

            CheckBoxNonTradePeriod4OnOff.IsChecked = tradeGrid.NonTradePeriods.NonTradePeriod4OnOff;
            CheckBoxNonTradePeriod4OnOff.Checked += CheckBoxNonTradePeriod4OnOff_Checked; 

            CheckBoxNonTradePeriod5OnOff.IsChecked = tradeGrid.NonTradePeriods.NonTradePeriod5OnOff;
            CheckBoxNonTradePeriod5OnOff.Checked += CheckBoxNonTradePeriod5OnOff_Checked;

            TextBoxNonTradePeriod1Start.Text = tradeGrid.NonTradePeriods.NonTradePeriod1Start.ToString();
            TextBoxNonTradePeriod1Start.TextChanged += TextBoxNonTradePeriod1Start_TextChanged;

            TextBoxNonTradePeriod2Start.Text = tradeGrid.NonTradePeriods.NonTradePeriod2Start.ToString();
            TextBoxNonTradePeriod2Start.TextChanged += TextBoxNonTradePeriod2Start_TextChanged;

            TextBoxNonTradePeriod3Start.Text = tradeGrid.NonTradePeriods.NonTradePeriod3Start.ToString();
            TextBoxNonTradePeriod3Start.TextChanged += TextBoxNonTradePeriod3Start_TextChanged; 

            TextBoxNonTradePeriod4Start.Text = tradeGrid.NonTradePeriods.NonTradePeriod4Start.ToString();
            TextBoxNonTradePeriod4Start.TextChanged += TextBoxNonTradePeriod4Start_TextChanged; 

            TextBoxNonTradePeriod5Start.Text = tradeGrid.NonTradePeriods.NonTradePeriod5Start.ToString();
            TextBoxNonTradePeriod5Start.TextChanged += TextBoxNonTradePeriod5Start_TextChanged;

            TextBoxNonTradePeriod1End.Text = tradeGrid.NonTradePeriods.NonTradePeriod1End.ToString();
            TextBoxNonTradePeriod1End.TextChanged += TextBoxNonTradePeriod1End_TextChanged;

            TextBoxNonTradePeriod2End.Text = tradeGrid.NonTradePeriods.NonTradePeriod2End.ToString();
            TextBoxNonTradePeriod2End.TextChanged += TextBoxNonTradePeriod2End_TextChanged;

            TextBoxNonTradePeriod3End.Text = tradeGrid.NonTradePeriods.NonTradePeriod3End.ToString();
            TextBoxNonTradePeriod3End.TextChanged += TextBoxNonTradePeriod3End_TextChanged;

            TextBoxNonTradePeriod4End.Text = tradeGrid.NonTradePeriods.NonTradePeriod4End.ToString();
            TextBoxNonTradePeriod4End.TextChanged += TextBoxNonTradePeriod4End_TextChanged;

            TextBoxNonTradePeriod5End.Text = tradeGrid.NonTradePeriods.NonTradePeriod5End.ToString();
            TextBoxNonTradePeriod5End.TextChanged += TextBoxNonTradePeriod5End_TextChanged;

            // trade days 

            ComboBoxNonTradeDaysRegime.Items.Add(TradeGridRegime.Off.ToString());
            ComboBoxNonTradeDaysRegime.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxNonTradeDaysRegime.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxNonTradeDaysRegime.SelectedItem = tradeGrid.NonTradeDays.NonTradeDaysRegime.ToString();
            ComboBoxNonTradeDaysRegime.SelectionChanged += ComboBoxNonTradeDaysRegime_SelectionChanged;

            CheckBoxTradeInMonday.IsChecked = tradeGrid.NonTradeDays.TradeInMonday;
            CheckBoxTradeInMonday.Checked += CheckBoxTradeInMonday_Checked;

            CheckBoxTradeInTuesday.IsChecked = tradeGrid.NonTradeDays.TradeInTuesday;
            CheckBoxTradeInTuesday.Checked += CheckBoxTradeInTuesday_Checked;

            CheckBoxTradeInWednesday.IsChecked = tradeGrid.NonTradeDays.TradeInWednesday;
            CheckBoxTradeInWednesday.Checked += CheckBoxTradeInWednesday_Checked;

            CheckBoxTradeInThursday.IsChecked = tradeGrid.NonTradeDays.TradeInThursday;
            CheckBoxTradeInThursday.Checked += CheckBoxTradeInThursday_Checked; 
             
            CheckBoxTradeInFriday.IsChecked = tradeGrid.NonTradeDays.TradeInFriday;
            CheckBoxTradeInFriday.Checked += CheckBoxTradeInFriday_Checked;

            CheckBoxTradeInSaturday.IsChecked = tradeGrid.NonTradeDays.TradeInSaturday;
            CheckBoxTradeInSaturday.Checked += CheckBoxTradeInSaturday_Checked; 

            CheckBoxTradeInSunday.IsChecked = tradeGrid.NonTradeDays.TradeInSunday;
            CheckBoxTradeInSunday.Checked += CheckBoxTradeInSunday_Checked;

            // stop grid by event

            CheckBoxStopGridByMoveUpIsOn.IsChecked = tradeGrid.StopBy.StopGridByMoveUpIsOn;
            CheckBoxStopGridByMoveUpIsOn.Checked += CheckBoxStopGridByMoveUpIsOn_Checked;
            TextBoxStopGridByMoveUpValuePercent.Text = tradeGrid.StopBy.StopGridByMoveUpValuePercent.ToString();
            TextBoxStopGridByMoveUpValuePercent.TextChanged += TextBoxStopGridByMoveUpValuePercent_TextChanged;
            ComboBoxStopGridByMoveUpReaction.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxStopGridByMoveUpReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxStopGridByMoveUpReaction.SelectedItem = tradeGrid.StopBy.StopGridByMoveUpReaction.ToString();
            ComboBoxStopGridByMoveUpReaction.SelectionChanged += ComboBoxStopGridByMoveUpReaction_SelectionChanged;

            CheckBoxStopGridByMoveDownIsOn.IsChecked = tradeGrid.StopBy.StopGridByMoveDownIsOn;
            CheckBoxStopGridByMoveDownIsOn.Checked += CheckBoxStopGridByMoveDownIsOn_Checked;
            TextBoxStopGridByMoveDownValuePercent.Text = tradeGrid.StopBy.StopGridByMoveDownValuePercent.ToString();
            TextBoxStopGridByMoveDownValuePercent.TextChanged += TextBoxStopGridByMoveDownValuePercent_TextChanged;
            ComboBoxStopGridByMoveDownReaction.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxStopGridByMoveDownReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxStopGridByMoveDownReaction.SelectedItem = tradeGrid.StopBy.StopGridByMoveDownReaction.ToString();
            ComboBoxStopGridByMoveDownReaction.SelectionChanged += ComboBoxStopGridByMoveDownReaction_SelectionChanged;

            CheckBoxStopGridByPositionsCountIsOn.IsChecked = tradeGrid.StopBy.StopGridByPositionsCountIsOn;
            CheckBoxStopGridByPositionsCountIsOn.Checked += CheckBoxStopGridByPositionsCountIsOn_Checked;
            TextBoxStopGridByPositionsCountValue.Text = tradeGrid.StopBy.StopGridByPositionsCountValue.ToString();
            TextBoxStopGridByPositionsCountValue.TextChanged += TextBoxStopGridByPositionsCountValue_TextChanged;
            ComboBoxStopGridByPositionsCountReaction.Items.Add(TradeGridRegime.CloseForced.ToString());
            ComboBoxStopGridByPositionsCountReaction.Items.Add(TradeGridRegime.CloseOnly.ToString());
            ComboBoxStopGridByPositionsCountReaction.SelectedItem = tradeGrid.StopBy.StopGridByPositionsCountReaction.ToString();
            ComboBoxStopGridByPositionsCountReaction.SelectionChanged += ComboBoxStopGridByPositionsCountReaction_SelectionChanged;

            // grid lines creation

            ComboBoxGridSide.Items.Add(Side.Buy.ToString());
            ComboBoxGridSide.Items.Add(Side.Sell.ToString());
            ComboBoxGridSide.SelectedItem = tradeGrid.GridCreator.GridSide.ToString();
            ComboBoxGridSide.SelectionChanged += ComboBoxGridSide_SelectionChanged;

            TextBoxFirstPrice.Text = tradeGrid.GridCreator.FirstPrice.ToString();
            TextBoxFirstPrice.TextChanged += TextBoxFirstPrice_TextChanged;

            TextBoxLineCountStart.Text = tradeGrid.GridCreator.LineCountStart.ToString();
            TextBoxLineCountStart.TextChanged += TextBoxLineCountStart_TextChanged;

            TextBoxMaxOpenOrdersInMarket.Text = tradeGrid.MaxOpenOrdersInMarket.ToString();
            TextBoxMaxOpenOrdersInMarket.TextChanged += TextBoxMaxOrdersInMarket_TextChanged;

            TextBoxMaxCloseOrdersInMarket.Text = tradeGrid.MaxCloseOrdersInMarket.ToString();
            TextBoxMaxCloseOrdersInMarket.TextChanged += TextBoxMaxCloseOrdersInMarket_TextChanged;

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

            // trailing up / down

            CheckBoxTrailingUpIsOn.IsChecked = tradeGrid.Trailing.TrailingUpIsOn;
            CheckBoxTrailingUpIsOn.Checked += CheckBoxTrailingUpIsOn_Checked;
            TextBoxTrailingUpLimitValue.Text = tradeGrid.Trailing.TrailingUpLimitValue.ToString();
            TextBoxTrailingUpLimitValue.TextChanged += TextBoxTrailingUpLimitValue_TextChanged;

            CheckBoxTrailingDownIsOn.IsChecked = tradeGrid.Trailing.TrailingDownIsOn;
            CheckBoxTrailingDownIsOn.Checked += CheckBoxTrailingDownIsOn_Checked;
            TextBoxTrailingDownLimitValue.Text = tradeGrid.Trailing.TrailingDownLimitValue.ToString();
            TextBoxTrailingDownLimitValue.TextChanged += TextBoxTrailingDownLimitValue_TextChanged;

            // auto start

            ComboBoxAutoStartRegime.Items.Add(TradeGridAutoStartRegime.Off.ToString());
            ComboBoxAutoStartRegime.Items.Add(TradeGridAutoStartRegime.LowerOrEqual.ToString());
            ComboBoxAutoStartRegime.Items.Add(TradeGridAutoStartRegime.HigherOrEqual.ToString());
            ComboBoxAutoStartRegime.SelectedItem = tradeGrid.AutoStarter.AutoStartRegime.ToString();
            ComboBoxAutoStartRegime.SelectionChanged += ComboBoxAutoStartRegime_SelectionChanged;

            TextBoxAutoStartPrice.Text = tradeGrid.AutoStarter.AutoStartPrice.ToString();
            TextBoxAutoStartPrice.TextChanged += TextBoxAutoStartPrice_TextChanged;

            Localization();

            // grid table

            CreateGridTable();
            UpdateGridTable();
        }

        private void Localization()
        {
            Title = OsLocalization.Trader.Label444 + " # " + TradeGrid.Number ;

            // settings prime

            LabelGridType.Content = OsLocalization.Trader.Label445;
            LabelClosePositionNumber.Content = OsLocalization.Trader.Label446;
            ButtonSelectPositionToClose.Content = OsLocalization.Trader.Label447;
            LabelRegime.Content = OsLocalization.Trader.Label448;
            LabelRegimeLogicEntry.Content = OsLocalization.Trader.Label449;
            LabelRegimeLogging.Content = OsLocalization.Trader.Label450;
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

            // tab controls

            TabItemBaseSettings.Header = OsLocalization.Trader.Label458;
            TabItemGridCreation.Header = OsLocalization.Trader.Label459;
            TabItemTrailing.Header = OsLocalization.Trader.Label460;
            TabItemTradeDays.Header = OsLocalization.Trader.Label461;
            TabItemNonTradePeriods.Header = OsLocalization.Trader.Label462;
            TabItemStopTrading.Header = OsLocalization.Trader.Label463;
            TabItemStopAndProfit.Header = OsLocalization.Trader.Label464;
            TabItemGridLinesTable.Header = OsLocalization.Trader.Label465;
            TabItemGridLinesOnChart.Header = OsLocalization.Trader.Label466;
            TabItemAutoStart.Header = OsLocalization.Trader.Label472;

            // non trade periods

            CheckBoxNonTradePeriod1OnOff.Content = OsLocalization.Trader.Label473 + " 1";
            CheckBoxNonTradePeriod2OnOff.Content = OsLocalization.Trader.Label473 + " 2";
            CheckBoxNonTradePeriod3OnOff.Content = OsLocalization.Trader.Label473 + " 3";
            CheckBoxNonTradePeriod4OnOff.Content = OsLocalization.Trader.Label473 + " 4";
            CheckBoxNonTradePeriod5OnOff.Content = OsLocalization.Trader.Label473 + " 5";

            // trade days 
            LabelNonTradeDaysRegime.Content = OsLocalization.Trader.Label506;
            CheckBoxTradeInMonday.Content = OsLocalization.Trader.Label474;
            CheckBoxTradeInTuesday.Content = OsLocalization.Trader.Label475;
            CheckBoxTradeInWednesday.Content = OsLocalization.Trader.Label476;
            CheckBoxTradeInThursday.Content = OsLocalization.Trader.Label477;
            CheckBoxTradeInFriday.Content = OsLocalization.Trader.Label478;
            CheckBoxTradeInSaturday.Content = OsLocalization.Trader.Label479;
            CheckBoxTradeInSunday.Content = OsLocalization.Trader.Label480;

            // stop grid by event
            CheckBoxStopGridByMoveUpIsOn.Content = OsLocalization.Trader.Label481;
            LabelStopGridByMoveUpValuePercentReaction.Content = OsLocalization.Trader.Label484;
            CheckBoxStopGridByMoveDownIsOn.Content = OsLocalization.Trader.Label482;
            LabelStopGridByMoveDownValuePercentReaction.Content = OsLocalization.Trader.Label484;
            CheckBoxStopGridByPositionsCountIsOn.Content = OsLocalization.Trader.Label483;
            LabelStopGridByPositionsCountIsOnReaction.Content = OsLocalization.Trader.Label484;

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

            // stop and profit 

            LabelProfitRegime.Content = OsLocalization.Trader.Label497;
            LabelProfitValueType.Content = OsLocalization.Trader.Label498;
            LabelProfitValue.Content = OsLocalization.Trader.Label499;

            LabelStopRegime.Content = OsLocalization.Trader.Label500;
            LabelStopValueType.Content = OsLocalization.Trader.Label498;
            LabelStopValue.Content = OsLocalization.Trader.Label499;

            // trailing up / down

            CheckBoxTrailingUpIsOn.Content = OsLocalization.Trader.Label501;
            CheckBoxTrailingDownIsOn.Content = OsLocalization.Trader.Label502;
            LabelTrailingUpLimitValue.Content = OsLocalization.Trader.Label503;
            LabelTrailingDownLimitValue.Content = OsLocalization.Trader.Label503;

            // auto start

            LabelAutoStartRegime.Content = OsLocalization.Trader.Label504;
            LabelAutoStartPrice.Content = OsLocalization.Trader.Label505;
        }

        private void TradeGridUi_Closed(object sender, EventArgs e)
        {
            TradeGrid = null;

            try
            {
                ComboBoxGridType.SelectionChanged -= ComboBoxGridType_SelectionChanged;
                TextBoxClosePositionNumber.TextChanged -= TextBoxClosePositionNumber_TextChanged;
                ComboBoxRegime.SelectionChanged -= ComboBoxRegime_SelectionChanged;
                ComboBoxRegimeLogicEntry.SelectionChanged -= ComboBoxRegimeLogicEntry_SelectionChanged;
                ComboBoxRegimeLogging.SelectionChanged -= ComboBoxRegimeLogging_SelectionChanged;
                ComboBoxAutoClearJournal.SelectionChanged -= ComboBoxAutoClearJournal_SelectionChanged;
                TextBoxMaxClosePositionsInJournal.TextChanged -= TextBoxMaxClosePositionsInJournal_TextChanged;
                TextBoxMaxOpenOrdersInMarket.TextChanged -= TextBoxMaxOrdersInMarket_TextChanged;
                TextBoxMaxCloseOrdersInMarket.TextChanged -= TextBoxMaxCloseOrdersInMarket_TextChanged;

                CheckBoxNonTradePeriod1OnOff.Checked -= CheckBoxNonTradePeriod1OnOff_Checked;
                CheckBoxNonTradePeriod2OnOff.Checked -= CheckBoxNonTradePeriod2OnOff_Checked;
                CheckBoxNonTradePeriod3OnOff.Checked -= CheckBoxNonTradePeriod3OnOff_Checked;
                CheckBoxNonTradePeriod4OnOff.Checked -= CheckBoxNonTradePeriod4OnOff_Checked;
                CheckBoxNonTradePeriod5OnOff.Checked -= CheckBoxNonTradePeriod5OnOff_Checked;
                TextBoxNonTradePeriod1Start.TextChanged -= TextBoxNonTradePeriod1Start_TextChanged;
                TextBoxNonTradePeriod2Start.TextChanged -= TextBoxNonTradePeriod2Start_TextChanged;
                TextBoxNonTradePeriod3Start.TextChanged -= TextBoxNonTradePeriod3Start_TextChanged;
                TextBoxNonTradePeriod4Start.TextChanged -= TextBoxNonTradePeriod4Start_TextChanged;
                TextBoxNonTradePeriod5Start.TextChanged -= TextBoxNonTradePeriod5Start_TextChanged;
                TextBoxNonTradePeriod1End.TextChanged -= TextBoxNonTradePeriod1End_TextChanged;
                TextBoxNonTradePeriod2End.TextChanged -= TextBoxNonTradePeriod2End_TextChanged;
                TextBoxNonTradePeriod3End.TextChanged -= TextBoxNonTradePeriod3End_TextChanged;
                TextBoxNonTradePeriod4End.TextChanged -= TextBoxNonTradePeriod4End_TextChanged;
                TextBoxNonTradePeriod5End.TextChanged -= TextBoxNonTradePeriod5End_TextChanged;

                CheckBoxTradeInMonday.Checked -= CheckBoxTradeInMonday_Checked;
                CheckBoxTradeInTuesday.Checked -= CheckBoxTradeInTuesday_Checked;
                CheckBoxTradeInWednesday.Checked -= CheckBoxTradeInWednesday_Checked;
                CheckBoxTradeInThursday.Checked -= CheckBoxTradeInThursday_Checked;
                CheckBoxTradeInFriday.Checked -= CheckBoxTradeInFriday_Checked;
                CheckBoxTradeInSaturday.Checked -= CheckBoxTradeInSaturday_Checked;
                CheckBoxTradeInSunday.Checked -= CheckBoxTradeInSunday_Checked;

                CheckBoxStopGridByMoveUpIsOn.Checked -= CheckBoxStopGridByMoveUpIsOn_Checked;
                TextBoxStopGridByMoveUpValuePercent.TextChanged -= TextBoxStopGridByMoveUpValuePercent_TextChanged;
                CheckBoxStopGridByMoveDownIsOn.Checked -= CheckBoxStopGridByMoveDownIsOn_Checked;
                TextBoxStopGridByMoveDownValuePercent.TextChanged -= TextBoxStopGridByMoveDownValuePercent_TextChanged;
                CheckBoxStopGridByPositionsCountIsOn.Checked -= CheckBoxStopGridByPositionsCountIsOn_Checked;
                TextBoxStopGridByPositionsCountValue.TextChanged -= TextBoxStopGridByPositionsCountValue_TextChanged;
                ComboBoxGridSide.SelectionChanged -= ComboBoxGridSide_SelectionChanged;
                TextBoxFirstPrice.TextChanged -= TextBoxFirstPrice_TextChanged;
                TextBoxLineCountStart.TextChanged -= TextBoxLineCountStart_TextChanged;

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

        public TradeGrid TradeGrid;

        public int Number;

        #region Trailing up / down

        private void CheckBoxTrailingUpIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.Trailing.TrailingUpIsOn = CheckBoxTrailingUpIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxTrailingUpLimitValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxTrailingUpLimitValue.Text))
                {
                    return;
                }

                TradeGrid.Trailing.TrailingUpLimitValue = TextBoxTrailingUpLimitValue.Text.ToDecimal();
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTrailingDownIsOn_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.Trailing.TrailingDownIsOn = CheckBoxTrailingDownIsOn.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxTrailingDownLimitValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxTrailingDownLimitValue.Text))
                {
                    return;
                }

                TradeGrid.Trailing.TrailingDownLimitValue = TextBoxTrailingDownLimitValue.Text.ToDecimal();
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
                Enum.TryParse(ComboBoxStopRegime.SelectedItem.ToString(), out TradeGrid.StopAndProfit.StopRegime);
                TradeGrid.Save();
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
                UpdateGridTable();
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
                TradeGrid.DeleteGrid();
                UpdateGridTable();
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
                UpdateGridTable();
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
                    if (_gridDataGrid.Rows[i].Cells[6].Value.ToString() != "Unchecked")
                    {
                        numbers.Add(i);
                    }
                }

                if (numbers.Count == 0)
                {
                    return;
                }

                TradeGrid.RemoveSelected(numbers);
                UpdateGridTable();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Grid paint in table

        private DataGridView _gridDataGrid;

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
                newColumn0.HeaderText = "Number";
                _gridDataGrid.Columns.Add(newColumn0);
                newColumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn1 = new DataGridViewColumn();
                newColumn1.CellTemplate = cellParam0;
                newColumn1.HeaderText = "Is ON";
                _gridDataGrid.Columns.Add(newColumn1);
                newColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn2 = new DataGridViewColumn();
                newColumn2.CellTemplate = cellParam0;
                newColumn2.HeaderText = "Entry price";
                _gridDataGrid.Columns.Add(newColumn2);
                newColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn3 = new DataGridViewColumn();
                newColumn3.CellTemplate = cellParam0;
                newColumn3.HeaderText = "Exit price";
                _gridDataGrid.Columns.Add(newColumn3);
                newColumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn4 = new DataGridViewColumn();
                newColumn4.CellTemplate = cellParam0;
                newColumn4.HeaderText = "Volume";
                _gridDataGrid.Columns.Add(newColumn4);
                newColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn5 = new DataGridViewColumn();
                newColumn5.CellTemplate = cellParam0;
                newColumn5.HeaderText = "Direction";
                _gridDataGrid.Columns.Add(newColumn5);
                newColumn5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                DataGridViewColumn newColumn6 = new DataGridViewColumn();
                newColumn6.CellTemplate = cellParam0;
                newColumn6.HeaderText = "Select";
                _gridDataGrid.Columns.Add(newColumn6);
                newColumn6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                HostGridTable.Child = _gridDataGrid;

                _gridDataGrid.DataError += _gridDataGrid_DataError;
                _gridDataGrid.CellValueChanged += EventChangeValueInTable;
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void UpdateGridTable()
        {
            try
            {
                if (_gridDataGrid.InvokeRequired)
                {
                    _gridDataGrid.Invoke(new Action(UpdateGridTable));
                    return;
                }

                _gridDataGrid.CellValueChanged -= EventChangeValueInTable;
                _gridDataGrid.Rows.Clear();

                for (int i = 0; i < TradeGrid.GridCreator.Lines.Count; i++)
                {
                    TradeGridLine curLine = TradeGrid.GridCreator.Lines[i];

                    DataGridViewRow rowLine = new DataGridViewRow();

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[0].Value = i + 1;
                    rowLine.Cells[0].ReadOnly = true;

                    DataGridViewComboBoxCell cell1 = new DataGridViewComboBoxCell();
                    cell1.Items.Add(true.ToString());
                    cell1.Items.Add(false.ToString());
                    cell1.Value = curLine.IsOn.ToString();
                    cell1.ReadOnly = false;
                    rowLine.Cells.Add(cell1);

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[2].Value = Math.Round(curLine.PriceEnter, 10);
                    rowLine.Cells[2].ReadOnly = false;

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[3].Value = Math.Round(curLine.PriceExit, 10);
                    rowLine.Cells[3].ReadOnly = false;

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[4].Value = Math.Round(curLine.Volume, 10);
                    rowLine.Cells[4].ReadOnly = false;

                    rowLine.Cells.Add(new DataGridViewTextBoxCell());
                    rowLine.Cells[5].Value = curLine.Side;
                    rowLine.Cells[5].ReadOnly = true;

                    rowLine.Cells.Add(new DataGridViewCheckBoxCell());
                    rowLine.Cells[6].Value = CheckState.Unchecked;
                    rowLine.Cells[6].ReadOnly = false;

                    _gridDataGrid.Rows.Add(rowLine);
                }

                _gridDataGrid.CellValueChanged += EventChangeValueInTable;
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), OsEngine.Logging.LogMessageType.Error);
            }
        }

        private void EventChangeValueInTable(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                List<TradeGridLine> Lines = TradeGrid.GridCreator.Lines;

                for (int i = 0; i < Lines.Count; i++)
                {
                    Lines[i].IsOn = Convert.ToBoolean(_gridDataGrid.Rows[i].Cells[1].Value.ToString().ToLower());
                    Lines[i].PriceEnter = _gridDataGrid.Rows[i].Cells[2].Value.ToString().ToDecimal();
                    Lines[i].PriceExit = _gridDataGrid.Rows[i].Cells[3].Value.ToString().ToDecimal();
                    Lines[i].Volume = _gridDataGrid.Rows[i].Cells[4].Value.ToString().ToDecimal();
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

        #endregion

        #region Trade days 

        private void ComboBoxNonTradeDaysRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxNonTradeDaysRegime.SelectedItem.ToString(), out TradeGrid.NonTradeDays.NonTradeDaysRegime);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CheckBoxTradeInMonday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradeDays.TradeInMonday = CheckBoxTradeInMonday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInTuesday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradeDays.TradeInTuesday = CheckBoxTradeInTuesday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInWednesday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradeDays.TradeInWednesday = CheckBoxTradeInWednesday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInThursday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradeDays.TradeInThursday = CheckBoxTradeInThursday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInFriday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradeDays.TradeInFriday = CheckBoxTradeInFriday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInSaturday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradeDays.TradeInSaturday = CheckBoxTradeInSaturday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxTradeInSunday_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradeDays.TradeInSunday = CheckBoxTradeInSunday.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
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
                Enum.TryParse(ComboBoxAutoStartRegime.SelectedItem.ToString(), out TradeGrid.AutoStarter.AutoStartRegime);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Non trade periods

        private void CheckBoxNonTradePeriod1OnOff_Checked(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriods.NonTradePeriod1OnOff = CheckBoxNonTradePeriod1OnOff.IsChecked.Value;
            TradeGrid.Save();
        }

        private void TextBoxNonTradePeriod1Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod1Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod1Start.LoadFromString(TextBoxNonTradePeriod1Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod1End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod1End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod1End.LoadFromString(TextBoxNonTradePeriod1End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod2OnOff_Checked(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriods.NonTradePeriod2OnOff = CheckBoxNonTradePeriod2OnOff.IsChecked.Value;
            TradeGrid.Save();
        }

        private void TextBoxNonTradePeriod2Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod2Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod2Start.LoadFromString(TextBoxNonTradePeriod2Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod2End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod2End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod2End.LoadFromString(TextBoxNonTradePeriod2End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod3OnOff_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradePeriods.NonTradePeriod3OnOff = CheckBoxNonTradePeriod3OnOff.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod3Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod3Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod3Start.LoadFromString(TextBoxNonTradePeriod3Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod3End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod3End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod3End.LoadFromString(TextBoxNonTradePeriod3End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod4OnOff_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradePeriods.NonTradePeriod4OnOff = CheckBoxNonTradePeriod4OnOff.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod4Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod4Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod4Start.LoadFromString(TextBoxNonTradePeriod4Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod4End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod4End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod4End.LoadFromString(TextBoxNonTradePeriod4End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod5OnOff_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                TradeGrid.NonTradePeriods.NonTradePeriod5OnOff = CheckBoxNonTradePeriod5OnOff.IsChecked.Value;
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod5Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod5Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod5Start.LoadFromString(TextBoxNonTradePeriod5Start.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void TextBoxNonTradePeriod5End_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod5End.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriods.NonTradePeriod5End.LoadFromString(TextBoxNonTradePeriod5End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
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

        private void ComboBoxRegimeLogging_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Enum.TryParse(ComboBoxRegimeLogging.SelectedItem.ToString(), out TradeGrid.RegimeLogging);
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
                Enum.TryParse(ComboBoxRegime.SelectedItem.ToString(), out TradeGrid.Regime);
                TradeGrid.Save();
            }
            catch (Exception ex)
            {
                TradeGrid.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxClosePositionNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if(string.IsNullOrEmpty(TextBoxClosePositionNumber.Text)) 
                { 
                    return; 
                }

                TradeGrid.ClosePositionNumber = Convert.ToInt32(TextBoxClosePositionNumber.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void ComboBoxGridType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxGridType.SelectedItem.ToString(),out TradeGrid.GridType);
            TradeGrid.Save();
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonStop_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonLoad_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonSelectPositionToClose_Click(object sender, RoutedEventArgs e)
        {

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

                TradeGrid.MaxOpenOrdersInMarket = Convert.ToInt32(TextBoxMaxCloseOrdersInMarket.Text);
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
