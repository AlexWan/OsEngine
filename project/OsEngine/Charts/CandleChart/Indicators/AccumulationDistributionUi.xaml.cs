/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для AccumulationDistributionUi.xaml
    /// </summary>
    public partial class AccumulationDistributionUi
    { /// <summary>
        /// индикатор
        /// </summary>
        private AccumulationDistribution _ad;

        /// <summary>
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        public AccumulationDistributionUi(AccumulationDistribution ad)
        {
            InitializeComponent();
            _ad = ad;


            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _ad.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _ad.PaintOn;
        }

        /// <summary>
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
