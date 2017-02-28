/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;

namespace OsEngine.OsTrader.RiskManager
{
    /// <summary>
    /// Окно Риск Менеджера
    /// </summary>
    public partial class RiskManagerUi
    {
        /// <summary>
        /// риск менеджер
        /// </summary>
        private RiskManager _riskManager;
        public RiskManagerUi(RiskManager riskManager)
        {
            try
            {
                _riskManager = riskManager;
                InitializeComponent();
                LoadDateOnForm();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        /// <summary>
        /// загрузить данные на форму
        /// </summary>
        private void LoadDateOnForm()
        {
            CheckBoxIsOn.IsChecked = _riskManager.IsActiv;
            TextBoxOpenMaxDd.Text = _riskManager.MaxDrowDownToDayPersent.ToString(new CultureInfo("ru-RU"));

            ComboBoxReaction.Items.Add(RiskManagerReactionType.CloseAndOff);
            ComboBoxReaction.Items.Add(RiskManagerReactionType.ShowDialog);
            ComboBoxReaction.Items.Add(RiskManagerReactionType.None);

            ComboBoxReaction.Text = _riskManager.ReactionType.ToString();

        }

        /// <summary>
        /// нажали кнопку принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Convert.ToDecimal(TextBoxOpenMaxDd.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("В одном из полей недопустимые значения. Процесс сохранения прерван");
                return;
            }


           _riskManager.IsActiv =  CheckBoxIsOn.IsChecked.Value;
           _riskManager.MaxDrowDownToDayPersent = Convert.ToDecimal(TextBoxOpenMaxDd.Text);

           Enum.TryParse(ComboBoxReaction.Text,false,out _riskManager.ReactionType);
           _riskManager.Save();
            Close();
        }
    }
}
