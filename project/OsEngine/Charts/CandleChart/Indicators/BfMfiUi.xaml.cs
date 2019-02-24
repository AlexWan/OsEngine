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
    /// Interaction logic  for  BfMfiUi.xaml
    /// Логика взаимодействия для BfMfiUi.xaml
    /// </summary>
    public partial class BfMfiUi
    {
        private BfMfi _mfi;
        public BfMfiUi(BfMfi mfi) //constructor/конструктор
        {
            InitializeComponent();
            _mfi = mfi;
            ShowSettingsOnForm();

            ButtonColor.Content = OsLocalization.Charts.LabelButtonIndicatorColor;
            CheckBoxPaintOnOff.Content = OsLocalization.Charts.LabelPaintIntdicatorIsVisible;
            ButtonAccept.Content = OsLocalization.Charts.LabelButtonIndicatorAccept;

        }

        private void ShowSettingsOnForm()// upload settings to form/выгрузить настройки на форму
        {
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _mfi.ColorBase;

        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e) // accept/принять
        {
            _mfi.ColorBase = HostColorUp.Child.BackColor;
            if (CheckBoxPaintOnOff.IsChecked.HasValue)
            {
                _mfi.PaintOn = CheckBoxPaintOnOff.IsChecked.Value;
            }
            
            _mfi.Save();
            IsChange = true;
            Close();
        }

        private void ButtonColorUp_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = HostColorUp.Child.BackColor;
            dialog.ShowDialog();

            HostColorUp.Child.BackColor = dialog.Color;
        }

        public bool IsChange;
    }
}