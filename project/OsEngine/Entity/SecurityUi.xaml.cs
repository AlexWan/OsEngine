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

            TextBoxGoPersent.Text = (security.Go).ToString(culture);
            TextBoxLot.Text = security.Lot.ToString(culture);
            TextBoxStep.Text = security.PriceStep.ToString(culture);
            TextBoxStepCost.Text = security.PriceStepCost.ToString(culture);
            TextBoxVolumeDecimals.Text = security.DecimalsVolume.ToString(culture);

            Title = OsLocalization.Entity.TitleSecurityUi;
            SecuritiesColumn3.Content = OsLocalization.Entity.SecuritiesColumn3;
            SecuritiesColumn4.Content = OsLocalization.Entity.SecuritiesColumn4;
            SecuritiesColumn5.Content = OsLocalization.Entity.SecuritiesColumn5;
            SecuritiesColumn6.Content = OsLocalization.Entity.SecuritiesColumn6;
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
            decimal go;
            decimal lot;
            decimal step;
            decimal stepCost;
            int volDecimals;

            try
            {
                go = TextBoxGoPersent.Text.ToDecimal();
                lot = TextBoxLot.Text.ToDecimal();
                step = TextBoxStep.Text.ToDecimal();
                stepCost = TextBoxStepCost.Text.ToDecimal();
                volDecimals = Convert.ToInt32(TextBoxVolumeDecimals.Text);
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Entity.ErrorSave);
                return;
            }

            _security.Go = go;
            _security.Lot = lot;
            _security.PriceStep = step;
            _security.PriceStepCost = stepCost;
            _security.DecimalsVolume = volDecimals;
            IsChanged = true;
            Close();
        }
    }
}
