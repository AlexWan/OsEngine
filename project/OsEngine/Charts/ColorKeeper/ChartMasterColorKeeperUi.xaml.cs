/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace OsEngine.Charts.ColorKeeper
{
    /// <summary>
    /// Логика взаимодействия для ChartMasterColorKeeperUi.xaml
    /// </summary>
    public partial class ChartMasterColorKeeperUi 
    {

        /// <summary>
        /// хранилище цветов
        /// </summary>
        private ChartMasterColorKeeper _colorKeeper;

        /// <summary>
        /// конструктор
        /// </summary>
        public ChartMasterColorKeeperUi(ChartMasterColorKeeper keeper)
        {
            _colorKeeper = keeper;
            InitializeComponent();
            HostChart.Child = new Panel();
            HostChart.Child.Show();
            HostCursor.Child = new Panel();
            HostDownBody.Child = new Panel();
            HostDownBorder.Child = new Panel();
            HostSecondTable.Child = new Panel();
            HostUpBody.Child = new Panel();
            HostUpBorder.Child = new Panel();
            HostText.Child = new Panel();

            HostChart.Child.BackColor = _colorKeeper.ColorBackChart;
            HostCursor.Child.BackColor = _colorKeeper.ColorBackCursor;
            HostDownBody.Child.BackColor = _colorKeeper.ColorDownBodyCandle;
            HostDownBorder.Child.BackColor = _colorKeeper.ColorDownBorderCandle;
            HostSecondTable.Child.BackColor = _colorKeeper.ColorBackSecond;
            HostUpBody.Child.BackColor = _colorKeeper.ColorUpBodyCandle;
            HostUpBorder.Child.BackColor = _colorKeeper.ColorUpBorderCandle;
            HostText.Child.BackColor = _colorKeeper.ColorText;
        }

        /// <summary>
        /// кнопка загрузить стандартные цвета
        /// </summary>
        private void ButtonStandart_Click(object sender, RoutedEventArgs e)
        {
            HostUpBody.Child.BackColor = Color.DeepSkyBlue;
            HostUpBorder.Child.BackColor = Color.Blue;

            HostDownBody.Child.BackColor = Color.DarkRed;
            HostDownBorder.Child.BackColor = Color.Red;

            HostSecondTable.Child.BackColor = Color.Black;
            HostChart.Child.BackColor = Color.FromArgb(-15395563);

            HostCursor.Child.BackColor = Color.DarkOrange;

            HostText.Child.BackColor = Color.DimGray;

            Save();
            _colorKeeper.Save();
        }

        /// <summary>
        /// сохранить
        /// </summary>
        private void Save() 
        {
            _colorKeeper.ColorBackChart = HostChart.Child.BackColor;
            _colorKeeper.ColorBackCursor = HostCursor.Child.BackColor;
            _colorKeeper.ColorDownBodyCandle = HostDownBody.Child.BackColor;
            _colorKeeper.ColorDownBorderCandle = HostDownBorder.Child.BackColor;
            _colorKeeper.ColorBackSecond = HostSecondTable.Child.BackColor;
            _colorKeeper.ColorUpBodyCandle = HostUpBody.Child.BackColor;
            _colorKeeper.ColorUpBorderCandle = HostUpBorder.Child.BackColor;
            _colorKeeper.ColorText = HostText.Child.BackColor;
        }

// вызов различных цветовых настроек

        private void ButtonColorSecondTable_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();
            ui.Color = _colorKeeper.ColorBackSecond;
            ui.ShowDialog();
            HostSecondTable.Child.BackColor = ui.Color;
            _colorKeeper.ColorBackSecond = ui.Color;
            _colorKeeper.Save();
        }

        private void ButtonColorChart_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();
            ui.Color = _colorKeeper.ColorBackChart;
            ui.ShowDialog();
            HostChart.Child.BackColor = ui.Color;
            _colorKeeper.ColorBackChart = ui.Color;
            _colorKeeper.Save();
        }

        private void ButtonColorCursor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();
            ui.Color = _colorKeeper.ColorBackCursor;
            ui.ShowDialog();
            HostCursor.Child.BackColor = ui.Color;
            _colorKeeper.ColorBackCursor = ui.Color;
            _colorKeeper.Save();
        }

        private void ButtonColorUpCandleBorder_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();
            ui.Color = _colorKeeper.ColorUpBorderCandle;
            ui.ShowDialog();
            HostUpBorder.Child.BackColor = ui.Color;
            _colorKeeper.ColorUpBorderCandle = ui.Color;
            _colorKeeper.Save();
        }

        private void ButtonColorUpCandleBody_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();
            ui.Color = _colorKeeper.ColorUpBodyCandle;
            ui.ShowDialog();
            HostUpBody.Child.BackColor = ui.Color;
            _colorKeeper.ColorUpBodyCandle = ui.Color;
            _colorKeeper.Save();
        }

        private void ButtonColorDownCandleBorder_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();
            ui.Color = _colorKeeper.ColorDownBorderCandle;
            ui.ShowDialog();
            HostDownBorder.Child.BackColor = ui.Color;
            _colorKeeper.ColorDownBorderCandle = ui.Color;
            _colorKeeper.Save();
        }

        private void ButtonColorDownCandleBody_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();
            ui.Color = _colorKeeper.ColorDownBodyCandle;
            ui.ShowDialog();
            HostDownBody.Child.BackColor = ui.Color;
            _colorKeeper.ColorDownBodyCandle = ui.Color;
            _colorKeeper.Save();
        }

        private void ButtonColorText_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog ui = new ColorDialog();
            ui.Color = _colorKeeper.ColorText;
            ui.ShowDialog();
            HostText.Child.BackColor = ui.Color;
            _colorKeeper.ColorText = ui.Color;
            _colorKeeper.Save();
        }

    }
}
