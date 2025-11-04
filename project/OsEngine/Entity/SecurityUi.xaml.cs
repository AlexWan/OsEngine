/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Language;

namespace OsEngine.Entity
{
    public partial class SecurityUi
    {
        private Security _security;

        public bool IsChanged;

        public SecurityUi(Security security)
        {
            _security = security;
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            CultureInfo culture = new CultureInfo("ru-RU");

            TextBoxGoPrice.Text = (security.MarginBuy).ToString(culture);
            TextBoxMarginSell.Text = (security.MarginSell).ToString(culture);
            TextBoxLot.Text = security.Lot.ToString(culture);
            TextBoxStep.Text = security.PriceStep.ToString(culture);
            TextBoxStepCost.Text = security.PriceStepCost.ToString(culture);
            TextBoxVolumeDecimals.Text = security.DecimalsVolume.ToString(culture);
            TextBoxExpiration.Text = security.Expiration.ToString(culture);

            Title = OsLocalization.Entity.TitleSecurityUi;
            SecuritiesColumn3.Content = OsLocalization.Entity.SecuritiesColumn3;
            SecuritiesColumn4.Content = OsLocalization.Entity.SecuritiesColumn4;
            SecuritiesColumn5.Content = OsLocalization.Entity.SecuritiesColumn5;
            SecuritiesColumn6.Content = OsLocalization.Entity.SecuritiesColumn6;
            LabelSecuritiesMarginSell.Content = OsLocalization.Entity.SecuritiesColumn21;
            SecuritiesExpiration.Content = OsLocalization.Entity.SecuritiesColumn18;

            SecuritiesVolumeDecimals.Content = OsLocalization.Entity.SecuritiesColumn7;
            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;

            LabelName.Content = security.Name;

            this.Activate();
            this.Focus();

            Closed += SecurityUi_Closed;
        }

        private void SecurityUi_Closed(object sender, EventArgs e)
        {
            _security = null;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            decimal marginBuy;
            decimal marginSell;
            decimal lot;
            decimal step;
            decimal stepCost;
            int volDecimals;
            DateTime expiration;

            try
            {
                marginBuy = TextBoxGoPrice.Text.ToDecimal();
                marginSell = TextBoxMarginSell.Text.ToDecimal();
                lot = TextBoxLot.Text.ToDecimal();
                step = TextBoxStep.Text.ToDecimal();
                stepCost = TextBoxStepCost.Text.ToDecimal();
                volDecimals = Convert.ToInt32(TextBoxVolumeDecimals.Text);
                expiration = Convert.ToDateTime(TextBoxExpiration.Text);

                string message = OsLocalization.Message.HintMessageError5 + "\n";

                int index = 0;

                if (step < 0)
                {
                    message += index + 1 + ") " + OsLocalization.Message.HintMessageError0 + "\n";
                    index++;
                }

                if (stepCost < 0)
                {
                    message += index + 1 + ") " + OsLocalization.Message.HintMessageError1 + "\n";
                    index++;
                }

                if (marginBuy < 0
                    || marginSell < 0)
                {
                    message += index + 1 + ") " + OsLocalization.Message.HintMessageError2 + "\n";
                    index++;
                }

                if (lot < 0)
                {
                    message += index + 1 + ") " + OsLocalization.Message.HintMessageError3 + "\n";
                    index++;
                }

                if (volDecimals < 0)
                {
                    message += index + 1 + ") " + OsLocalization.Message.HintMessageError4 + "\n";
                    index++;
                }

                if (message != OsLocalization.Message.HintMessageError5 + "\n")
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(message);
                    ui.ShowDialog();
                    return;
                }
            }
            catch (Exception)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Message.HintMessageError5);
                ui.ShowDialog();
                return;
            }

            _security.MarginBuy = marginBuy;
            _security.MarginSell = marginSell;
            _security.Lot = lot;
            _security.PriceStep = step;
            _security.PriceStepCost = stepCost;
            _security.DecimalsVolume = volDecimals;
            _security.Expiration = expiration;
            IsChanged = true;
            Close();
        }

        private void ButtonInfoPriceStep_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Message.HintMessageLabel0);
            ui.ShowDialog();
        }

        private void ButtonInfoPriceStepPrice_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Message.HintMessageLabel1);
            ui.ShowDialog();
        }

        private void ButtonInfoLotPrice_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Message.HintMessageLabel2);
            ui.ShowDialog();
        }

        private void ButtonInfoMarginSell_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Message.HintMessageLabel2);
            ui.ShowDialog();
        }

        private void ButtonInfoLot_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Message.HintMessageLabel3);
            ui.ShowDialog();
        }

        private void ButtonInfoVolume_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Message.HintMessageLabel4);
            ui.ShowDialog();
        }

        private void ButtonInfoExpiration_Click(object sender, RoutedEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Message.HintMessageLabel8);
            ui.ShowDialog();
        }
    }
}