 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OsEngine.Charts.CandleChart.Indicators;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System.Threading;
using System.ComponentModel;

namespace OsEngine.Robots.MoiRoboti
{
    /// <summary>
    /// Логика взаимодействия для Breakdown_Ui.xaml
    /// </summary>
    public partial class Breakdown_Ui : Window
    {
        
        public Breakdown _bot;
    
        public Breakdown_Ui(Breakdown bot)
        {
            InitializeComponent();
            _bot = bot;
            DataContext = bot;

        }

        public void Button_Click(object sender, RoutedEventArgs e)
        {
            
        }
   
    }
}
