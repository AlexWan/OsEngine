/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;

namespace OsEngine.Entity
{
    /// <summary>
    /// Окно настроек бумаги
    /// </summary>
    public partial class SecurityUi
    {
        /// <summary>
        /// бумага
        /// </summary>
        private Security _security;

        /// <summary>
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
        }

        /// <summary>
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
                go = Convert.ToDecimal(TextBoxGoPersent.Text);
                lot = Convert.ToDecimal(TextBoxLot.Text);
                step = Convert.ToDecimal(TextBoxStep.Text);
                stepCost = Convert.ToDecimal(TextBoxStepCost.Text);

            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей не допустимое значение");
                return;
            }

            if (go < 1 || go > 100)
            {
                MessageBox.Show("Процесс сохранения прерван. ГО должно лежать в диапазоне от 1 до 100%");
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
