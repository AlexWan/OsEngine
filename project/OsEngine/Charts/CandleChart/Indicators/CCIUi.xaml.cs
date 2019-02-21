/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для MovingAverageUi.xaml
    /// </summary>
    public partial class CciUi
    {
        /// <summary>
        /// индикатор который мы настраиваем
        /// </summary>
        private Cci _mA;

        /// <summary>
        /// изменился ли индикатор
        /// </summary>
        public bool IsChange;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="mA">индикатор для настройки</param>
        public CciUi(Cci mA)
        {
            InitializeComponent();
            _mA = mA;

            TextBoxLenght.Text = _mA.Lenght.ToString();
            HostColor.Child = new TextBox();
            HostColor.Child.BackColor = _mA.ColorBase;

            CheckBoxPaintOnOff.IsChecked = _mA.PaintOn;

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;
            LabelIndicatorPeriod.Content = OsLocalization.Charts.LabelIndicatorPeriod;
        }


        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxLenght.Text) <= 0)
                {
                    throw new Exception("error");
                }
                //Enum.TryParse(ComboBoxMovingType.SelectedItem.ToString(), true, out _mA.TypeCalculationAverage);
            }
            catch (Exception)
            {
                MessageBox.Show("Процесс сохранения прерван. В одном из полей недопустимые значения");
                return;
            }

            _mA.ColorBase = HostColor.Child.BackColor;
            _mA.Lenght = Convert.ToInt32(TextBoxLenght.Text);
            _mA.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            //Enum.TryParse(ComboBoxPriceField.SelectedItem.ToString(), true, out _mA.TypePointsToSearch);

            _mA.Save();
            IsChange = true;
            Close();
        }

        /// <summary>
        /// кнопка изменить цвет
        /// </summary>
        private void ButtonColor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColor.Child.BackColor;
            dialog.ShowDialog();

            HostColor.Child.BackColor = dialog.Color;
        }

    }
}
