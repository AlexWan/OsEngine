using OsEngine.Layout;
using OsEngine.Market.Servers.BingX.BingXFutures.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace OsEngine.OsTrader.Grids
{
    /// <summary>
    /// Interaction logic for TradeGridUi.xaml
    /// </summary>
    public partial class TradeGridUi : Window
    {
        public TradeGridUi(TradeGrid tradeGrid)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);

            TradeGrid = tradeGrid;
            Number = TradeGrid.Number;

            Title = Title += " Grid " + tradeGrid.Number;

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
            ComboBoxRegimeLogicEntry.SelectionChanged += ComboBoxRegimeLogicEntry_SelectionChanged;

            ComboBoxRegimeLogging.Items.Add(TradeGridLoggingRegime.Standard.ToString());
            ComboBoxRegimeLogging.Items.Add(TradeGridLoggingRegime.Debug.ToString());
            ComboBoxRegimeLogging.SelectedItem = tradeGrid.RegimeLogging.ToString();
            ComboBoxRegimeLogging.SelectionChanged += ComboBoxRegimeLogging_SelectionChanged;

            ComboBoxAutoClearJournal.Items.Add("True");
            ComboBoxAutoClearJournal.Items.Add("False");
            ComboBoxAutoClearJournal.SelectedItem = tradeGrid.AutoClearJournalIsOn.ToString();
            ComboBoxAutoClearJournal.SelectionChanged += ComboBoxAutoClearJournal_SelectionChanged;

            TextBoxMaxClosePositionsInJournal.Text = tradeGrid.MaxClosePositionsInJournal.ToString();
            TextBoxMaxClosePositionsInJournal.TextChanged += TextBoxMaxClosePositionsInJournal_TextChanged;

            // non trade periods

            CheckBoxNonTradePeriod1OnOff.IsChecked = tradeGrid.NonTradePeriod1OnOff;
            CheckBoxNonTradePeriod1OnOff.Checked += CheckBoxNonTradePeriod1OnOff_Checked;

            CheckBoxNonTradePeriod2OnOff.IsChecked = tradeGrid.NonTradePeriod2OnOff;
            CheckBoxNonTradePeriod2OnOff.Checked += CheckBoxNonTradePeriod2OnOff_Checked;

            CheckBoxNonTradePeriod3OnOff.IsChecked = tradeGrid.NonTradePeriod3OnOff;
            CheckBoxNonTradePeriod3OnOff.Checked += CheckBoxNonTradePeriod3OnOff_Checked;

            CheckBoxNonTradePeriod4OnOff.IsChecked = tradeGrid.NonTradePeriod4OnOff;
            CheckBoxNonTradePeriod4OnOff.Checked += CheckBoxNonTradePeriod4OnOff_Checked; 

            CheckBoxNonTradePeriod5OnOff.IsChecked = tradeGrid.NonTradePeriod5OnOff;
            CheckBoxNonTradePeriod5OnOff.Checked += CheckBoxNonTradePeriod5OnOff_Checked;

            TextBoxNonTradePeriod1Start.Text = tradeGrid.NonTradePeriod1Start.ToString();
            TextBoxNonTradePeriod1Start.TextChanged += TextBoxNonTradePeriod1Start_TextChanged;

            TextBoxNonTradePeriod2Start.Text = tradeGrid.NonTradePeriod2Start.ToString();
            TextBoxNonTradePeriod2Start.TextChanged += TextBoxNonTradePeriod2Start_TextChanged;

            TextBoxNonTradePeriod3Start.Text = tradeGrid.NonTradePeriod3Start.ToString();
            TextBoxNonTradePeriod3Start.TextChanged += TextBoxNonTradePeriod3Start_TextChanged; 

            TextBoxNonTradePeriod4Start.Text = tradeGrid.NonTradePeriod4Start.ToString();
            TextBoxNonTradePeriod4Start.TextChanged += TextBoxNonTradePeriod4Start_TextChanged; 

            TextBoxNonTradePeriod5Start.Text = tradeGrid.NonTradePeriod5Start.ToString();
            TextBoxNonTradePeriod5Start.TextChanged += TextBoxNonTradePeriod5Start_TextChanged;

            TextBoxNonTradePeriod1End.Text = tradeGrid.NonTradePeriod1End.ToString();
            TextBoxNonTradePeriod1End.TextChanged += TextBoxNonTradePeriod1End_TextChanged;

            TextBoxNonTradePeriod2End.Text = tradeGrid.NonTradePeriod2End.ToString();
            TextBoxNonTradePeriod2End.TextChanged += TextBoxNonTradePeriod2End_TextChanged;

            TextBoxNonTradePeriod3End.Text = tradeGrid.NonTradePeriod3End.ToString();
            TextBoxNonTradePeriod3End.TextChanged += TextBoxNonTradePeriod3End_TextChanged;

            TextBoxNonTradePeriod4End.Text = tradeGrid.NonTradePeriod4End.ToString();
            TextBoxNonTradePeriod4End.TextChanged += TextBoxNonTradePeriod4End_TextChanged;

            TextBoxNonTradePeriod5End.Text = tradeGrid.NonTradePeriod5End.ToString();
            TextBoxNonTradePeriod5End.TextChanged += TextBoxNonTradePeriod5End_TextChanged;
            /*

             // non trade periods
             result += NonTradePeriod1OnOff + "@";
             result += NonTradePeriod1Start + "@";
             result += NonTradePeriod1End + "@";
             result += NonTradePeriod2OnOff + "@";
             result += NonTradePeriod2Start + "@";
             result += NonTradePeriod2End + "@";
             result += NonTradePeriod3OnOff + "@";
             result += NonTradePeriod3Start + "@";
             result += NonTradePeriod3End + "@";
             result += NonTradePeriod4OnOff + "@";
             result += NonTradePeriod4Start + "@";
             result += NonTradePeriod4End + "@";
             result += NonTradePeriod5OnOff + "@";
             result += NonTradePeriod5Start + "@";
             result += NonTradePeriod5End + "@";

             // trade days 
             result += TradeInMonday + "@";
             result += TradeInTuesday + "@";
             result += TradeInWednesday + "@";
             result += TradeInThursday + "@";
             result += TradeInFriday + "@";
             result += TradeInSaturday + "@";
             result += TradeInSunday + "@";

             // stop grid by event
             result += StopGridByPositionsCountIsOn + "@";
             result += StopGridByPositionsCountValue + "@";
             result += StopGridByProfitIsOn + "@";
             result += StopGridByProfitValuePercent + "@";
             result += StopGridByStopIsOn + "@";
             result += StopGridByStopValuePercent + "@";

             // grid lines creation and storage
             result += GridSide + "@";
             result += FirstPrice + "@";
             result += LineCountStart + "@";
             result += LineStep + "@";
             result += TypeVolume + "@";
             result += TypeProfit + "@";
             result += TypeStep + "@";
             result += StartVolume + "@";
             result += TradeAssetInPortfolio + "@";
             result += ProfitStep + "@";
             result += ProfitMultiplicator + "@";
             result += StepMultiplicator + "@";
             result += MartingaleMultiplicator + "@";

             // stop and profit 
             result += ProfitRegime + "@";
             result += ProfitValueType + "@";
             result += ProfitValue + "@";
             result += StopRegime + "@";
             result += StopValueType + "@";
             result += StopValue + "@";*/

        }

        #region Non trade periods

        private void CheckBoxNonTradePeriod1OnOff_Checked(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriod1OnOff = CheckBoxNonTradePeriod1OnOff.IsChecked.Value;
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

                TradeGrid.NonTradePeriod1Start.LoadFromString(TextBoxNonTradePeriod1Start.Text);
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

                TradeGrid.NonTradePeriod1End.LoadFromString(TextBoxNonTradePeriod1End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod2OnOff_Checked(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriod2OnOff = CheckBoxNonTradePeriod2OnOff.IsChecked.Value;
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

                TradeGrid.NonTradePeriod2Start.LoadFromString(TextBoxNonTradePeriod2Start.Text);
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

                TradeGrid.NonTradePeriod2End.LoadFromString(TextBoxNonTradePeriod2End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod3OnOff_Checked(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriod3OnOff = CheckBoxNonTradePeriod3OnOff.IsChecked.Value;
            TradeGrid.Save();
        }

        private void TextBoxNonTradePeriod3Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod3Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod3Start.LoadFromString(TextBoxNonTradePeriod3Start.Text);
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

                TradeGrid.NonTradePeriod3End.LoadFromString(TextBoxNonTradePeriod3End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod4OnOff_Checked(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriod4OnOff = CheckBoxNonTradePeriod4OnOff.IsChecked.Value;
            TradeGrid.Save();
        }

        private void TextBoxNonTradePeriod4Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod4Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod4Start.LoadFromString(TextBoxNonTradePeriod4Start.Text);
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

                TradeGrid.NonTradePeriod4End.LoadFromString(TextBoxNonTradePeriod4End.Text);
                TradeGrid.Save();
            }
            catch
            {
                // ignore
            }
        }

        private void CheckBoxNonTradePeriod5OnOff_Checked(object sender, RoutedEventArgs e)
        {
            TradeGrid.NonTradePeriod5OnOff = CheckBoxNonTradePeriod5OnOff.IsChecked.Value;
            TradeGrid.Save();
        }

        private void TextBoxNonTradePeriod5Start_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxNonTradePeriod5Start.Text))
                {
                    return;
                }

                TradeGrid.NonTradePeriod5Start.LoadFromString(TextBoxNonTradePeriod5Start.Text);
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

                TradeGrid.NonTradePeriod5End.LoadFromString(TextBoxNonTradePeriod5End.Text);
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
                Enum.TryParse(ComboBoxAutoClearJournal.SelectedItem.ToString(), out TradeGrid.AutoClearJournalIsOn);
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

        #endregion

        private void TradeGridUi_Closed(object sender, EventArgs e)
        {
            TradeGrid = null;

            ComboBoxGridType.SelectionChanged -= ComboBoxGridType_SelectionChanged;
            TextBoxClosePositionNumber.TextChanged -= TextBoxClosePositionNumber_TextChanged;
            ComboBoxRegime.SelectionChanged -= ComboBoxRegime_SelectionChanged;
            ComboBoxRegimeLogicEntry.SelectionChanged -= ComboBoxRegimeLogicEntry_SelectionChanged;
            ComboBoxRegimeLogging.SelectionChanged -= ComboBoxRegimeLogging_SelectionChanged;
            ComboBoxAutoClearJournal.SelectionChanged -= ComboBoxAutoClearJournal_SelectionChanged;
            TextBoxMaxClosePositionsInJournal.TextChanged -= TextBoxMaxClosePositionsInJournal_TextChanged;

        }

        public TradeGrid TradeGrid;

        public int Number;

    }
}
