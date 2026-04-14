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
using System.Windows.Forms.DataVisualization.Charting;

namespace OsEngine.OsOptimizer
{
    /// <summary>
    /// Interaction logic for OptimizerProfitStagesSaveUi.xaml
    /// </summary>
    public partial class OptimizerProfitStagesSaveUi : Window
    {
        public OptimizerProfitStagesSaveUi(Chart chartSeriesResult)
        {
            InitializeComponent();
        }
    }
}
