/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для OnBalanceVolumeUi.xaml
    /// </summary>
    public partial class OnBalanceVolumeUi
    {
        /// <summary>
        /// индикатор
        /// </summary>
        private OnBalanceVolume _obv;

        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="obv">индикатор для настроек</param>
        public OnBalanceVolumeUi(OnBalanceVolume obv)
        {
            InitializeComponent();
            _obv = obv;

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _obv.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _obv.PaintOn;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {


            _obv.ColorBase = HostColorBase.Child.BackColor;

            _obv.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;



            _obv.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        /// кнопка настроить цвет
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }
    }
}