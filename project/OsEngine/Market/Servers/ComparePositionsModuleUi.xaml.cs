/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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

namespace OsEngine.Market.Servers
{
    /// <summary>
    /// Interaction logic for ComparePositionsModuleUi.xaml
    /// </summary>
    public partial class ComparePositionsModuleUi : Window
    {
        ComparePositionsModule _comparePositionsModule;

        public ComparePositionsModuleUi(ComparePositionsModule comparePositionsModule)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _comparePositionsModule = comparePositionsModule;
        }
    }
}
