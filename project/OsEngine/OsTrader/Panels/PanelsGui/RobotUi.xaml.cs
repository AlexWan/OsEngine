/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;


namespace OsEngine.OsTrader.Panels.PanelsGui
{
    /// <summary>
    /// Логика взаимодействия для RobotUi.xaml
    /// </summary>
    public partial class RobotUi
    {
        private Robot _robot;
        public RobotUi(Robot robot)
        {
            InitializeComponent();
            _robot = robot;

            VolumeBox.Text = _robot.Volume.ToString();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _robot.Volume = Convert.ToDecimal(VolumeBox.Text);
            Close();
        }
    }
}