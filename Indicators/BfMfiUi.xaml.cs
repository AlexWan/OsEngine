﻿/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using TextBox = System.Windows.Forms.TextBox;

namespace OsEngine.Charts.CandleChart.Indicators
{
    /// <summary>
    /// Логика взаимодействия для BfMfiUi.xaml
    /// </summary>
    public partial class BfMfiUi
    {
        private BfMfi _mfi;
        public BfMfiUi(BfMfi mfi) // конструктор
        {
            InitializeComponent();
            _mfi = mfi;
            ShowSettingsOnForm();
        }

        private void ShowSettingsOnForm()// выгрузить настройки на форму
        {
            HostColorUp.Child = new TextBox();
            HostColorUp.Child.BackColor = _mfi.ColorBase;

        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e) // принять
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