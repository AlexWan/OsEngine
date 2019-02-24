/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic for AccumulationDistributionUi.xaml
    /// Логика взаимодействия для AccumulationDistributionUi.xaml
    /// </summary>
    public partial class AccumulationDistributionUi
    { /// <summary>
        /// Indicator/индикатор
        /// </summary>
        private AccumulationDistribution _ad;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public AccumulationDistributionUi(AccumulationDistribution ad)
        {
            InitializeComponent();
            _ad = ad;


            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _ad.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _ad.PaintOn;

            ButtonColorAdx.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {


            _ad.ColorBase = HostColorBase.Child.BackColor;

            _ad.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;

            _ad.Save();

            IsChange = true;
            Close();
        }

        /// <summary>
        /// color setting button
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
