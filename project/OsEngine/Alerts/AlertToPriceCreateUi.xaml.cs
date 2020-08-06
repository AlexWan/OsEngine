/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Alerts
{
    /// <summary>
    /// Interaction logic for PriceAlertCreateUi.xaml
    /// Логика взаимодействия для PriceAlertCreateUi.xaml
    /// </summary>
    public partial class AlertToPriceCreateUi
    {
        public AlertToPriceCreateUi(AlertToPrice alert)
        {
            InitializeComponent();
            MyAlert = alert;

            CheckBoxOnOff.IsChecked = MyAlert.IsOn;

            ComboBoxActivationType.Items.Add(PriceAlertTypeActivation.PriceLowerOrEqual);
            ComboBoxActivationType.Items.Add(PriceAlertTypeActivation.PriceHigherOrEqual);
            ComboBoxActivationType.SelectedItem = MyAlert.TypeActivation;

            TextBoxPriceActivation.Text = MyAlert.PriceActivation.ToString(new CultureInfo("RU-ru"));

            ComboBoxSignalType.Items.Add(SignalType.None);
            ComboBoxSignalType.Items.Add(SignalType.Buy);
            ComboBoxSignalType.Items.Add(SignalType.Sell);
            ComboBoxSignalType.Items.Add(SignalType.CloseAll);
            ComboBoxSignalType.Items.Add(SignalType.CloseOne);
            ComboBoxSignalType.Items.Add(SignalType.Modificate);
            ComboBoxSignalType.Items.Add(SignalType.OpenNew);
            ComboBoxSignalType.Items.Add(SignalType.ReloadProfit);
            ComboBoxSignalType.Items.Add(SignalType.ReloadStop);
            ComboBoxSignalType.SelectedItem = MyAlert.SignalType;

            ComboBoxOrderType.Items.Add(OrderPriceType.Limit);
            ComboBoxOrderType.Items.Add(OrderPriceType.Market);
            ComboBoxOrderType.SelectedItem = MyAlert.OrderPriceType;

            TextBoxVolumeReaction.Text = MyAlert.VolumeReaction.ToString();
            TextBoxSlippage.Text = MyAlert.Slippage.ToString(new CultureInfo("RU-ru"));
            TextBoxClosePosition.Text = MyAlert.NumberClosePosition.ToString();

            CheckBoxWindow.IsChecked = MyAlert.MessageIsOn;
            TextBoxAlertMessage.Text = MyAlert.Message;

            ComboBoxMusic.Items.Add(AlertMusic.Bird);
            ComboBoxMusic.Items.Add(AlertMusic.Duck);
            ComboBoxMusic.Items.Add(AlertMusic.Wolf);
            ComboBoxMusic.SelectedItem = MyAlert.MusicType;

            LabelOsa.MouseDown += LabelOsa_MouseDown;
            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;

            LabelOsa.MouseDown += LabelOsa_MouseDown;
        }

        private void ChangeText()
        {
            Title = OsLocalization.Alerts.TitleAlertToChartCreateUi;
            CheckBoxOnOff.Content = OsLocalization.Alerts.Label1;
            LabelActivation.Content = OsLocalization.Alerts.Label18;

            LabelTrade.Content = OsLocalization.Alerts.Label3;
            LabelReactionType.Content = OsLocalization.Alerts.Label4;
            LabelOrderType.Content = OsLocalization.Alerts.Label5;
            LabelVolume.Content = OsLocalization.Alerts.Label6;
            LabelSlippage.Content = OsLocalization.Alerts.Label7;
            LabelNumClosedPos.Content = OsLocalization.Alerts.Label8;
            LabelFireworks.Content = OsLocalization.Alerts.Label9;
            CheckBoxMusicAlert.Content = OsLocalization.Alerts.Label10;
            CheckBoxWindow.Content = OsLocalization.Alerts.Label16;
            ButtonSave.Content = OsLocalization.Alerts.Label17;
        }

        void LabelOsa_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("http://o-s-a.net");
        }

        public AlertToPrice MyAlert;

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {

            if (CheckBoxOnOff.IsChecked.HasValue)
            {
                MyAlert.IsOn = CheckBoxOnOff.IsChecked.Value;
            }
 
            Enum.TryParse(ComboBoxActivationType.SelectedItem.ToString(), out MyAlert.TypeActivation);

            MyAlert.PriceActivation = Convert.ToDecimal(TextBoxPriceActivation.Text);

            Enum.TryParse(ComboBoxSignalType.SelectedItem.ToString(), out MyAlert.SignalType);

            Enum.TryParse(ComboBoxOrderType.SelectedItem.ToString(), out MyAlert.OrderPriceType);

            MyAlert.VolumeReaction = TextBoxVolumeReaction.Text.ToDecimal();

            MyAlert.Slippage = Convert.ToDecimal(TextBoxSlippage.Text);

            MyAlert.NumberClosePosition = Convert.ToInt32(TextBoxClosePosition.Text);

            if (CheckBoxWindow.IsChecked.HasValue)
            {
                MyAlert.MessageIsOn = CheckBoxWindow.IsChecked.Value;
            }

            MyAlert.Message = TextBoxAlertMessage.Text;
            Enum.TryParse(ComboBoxMusic.SelectedItem.ToString(), out MyAlert.MusicType);

            Close();
        }
    }
}
