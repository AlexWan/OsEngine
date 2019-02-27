/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Interaction logic  for PriceOscillatorUi.xaml
    /// Логика взаимодействия для PriceOscillatorUi.xaml
    /// </summary>
    public partial class PriceOscillatorUi
    {
        /// <summary>
        /// indicator
        /// индикатор
        /// </summary>
        private PriceOscillator _pO;

        /// <summary>
        /// whether indicator settings have been changed
        /// изменялись ли настройки индикатора
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public PriceOscillatorUi(PriceOscillator pO)
        {
            InitializeComponent();
            _pO = pO;

            HostColorBase.Child = new TextBox();
            HostColorBase.Child.BackColor = _pO.ColorBase;
            CheckBoxPaintOnOff.IsChecked = _pO.PaintOn;

            ComboBoxTypeSerch.Items.Add(PriceOscillatorSerchType.Persent);
            ComboBoxTypeSerch.Items.Add(PriceOscillatorSerchType.Punkt);
            ComboBoxTypeSerch.SelectedItem = _pO.TypeSerch;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorType.Content = OsLocalization.Charts.LabelIndicatorType;
        }

        /// <summary>
        /// accept button
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            _pO.ColorBase = HostColorBase.Child.BackColor;
            _pO.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            IsChange = true;

            PriceOscillatorSerchType type;

            if (Enum.TryParse(ComboBoxTypeSerch.SelectedItem.ToString(), out type))
            {
                _pO.TypeSerch = type;
            }
            _pO.Save();
            Close();
        }

        /// <summary>
        /// color setting button
        /// кнопка настроить цвет
        /// </summary>
        private void ButtonColorAdx_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorBase.Child.BackColor;
            dialog.ShowDialog();
            HostColorBase.Child.BackColor = dialog.Color;
        }

        private void ButtonMa1_Click(object sender, RoutedEventArgs e)
        {
            _pO.ShowMaShortDialog();
        }

        private void ButtonMa2_Click(object sender, RoutedEventArgs e)
        {
            _pO.ShowMaLongDialog();
        }
    }
}
