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
    /// <summary>
    /// Paper setup window
    /// Окно настроек бумаги
    /// </summary>
    public partial class SecurityUi
    {
        /// <summary>
        /// security
        /// бумага
        /// </summary>
        private Security _security;

        /// <summary>
        /// if the settings have changed.
        /// изменились ли настройки
        /// </summary>
        public bool IsChanged;

        public SecurityUi(Security security)
        {
            _security = security;
            InitializeComponent();

            CultureInfo culture = new CultureInfo("ru-RU");

            TextBoxGoPersent.Text = (security.Go * 100).ToString(culture);
            TextBoxLot.Text = security.Lot.ToString(culture);
            TextBoxStep.Text = security.PriceStep.ToString(culture);
            TextBoxStepCost.Text = security.PriceStepCost.ToString(culture);

            Title = OsLocalization.Entity.TitleSecurityUi;
            SecuritiesColumn3.Content = OsLocalization.Entity.SecuritiesColumn3;
            SecuritiesColumn4.Content = OsLocalization.Entity.SecuritiesColumn4;
            SecuritiesColumn5.Content = OsLocalization.Entity.SecuritiesColumn5;
            SecuritiesColumn6.Content = OsLocalization.Entity.SecuritiesColumn6;
            ButtonAccept.Content = OsLocalization.Entity.ButtonAccept;

        }

        /// <summary>
        /// accept button 
        /// кнопка принять 
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            decimal go;
            decimal lot;
            decimal step;
            decimal stepCost;

            try
            {
                go = TextBoxGoPersent.Text.ToDecimal();
                lot = TextBoxLot.Text.ToDecimal();
                step = TextBoxStep.Text.ToDecimal();
                stepCost = TextBoxStepCost.Text.ToDecimal();

            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Entity.ErrorSave);
                return;
            }

            if (go < 1 || go > 100)
            {
                MessageBox.Show(OsLocalization.Entity.ErrorSave);
                return;
            }

            _security.Go = go/100;
            _security.Lot = lot;
            _security.PriceStep = step;
            _security.PriceStepCost = stepCost;
            IsChanged = true;
            Close();
        }
    }
}
