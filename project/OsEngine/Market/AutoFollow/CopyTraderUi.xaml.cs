/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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

namespace OsEngine.Market.AutoFollow
{
    /// <summary>
    /// Interaction logic for CopyTraderUi.xaml
    /// </summary>
    public partial class CopyTraderUi : Window
    {
        public CopyTrader CopyTraderClass;

        public CopyTraderUi(CopyTrader copyTrader)
        {
            InitializeComponent();

            CopyTraderClass = copyTrader;
            CopyTraderClass.DeleteEvent += CopyTraderClass_DeleteEvent;

            this.Closed += CopyTraderUi_Closed;
        }

        private void CopyTraderUi_Closed(object sender, EventArgs e)
        {
            CopyTraderClass.DeleteEvent -= CopyTraderClass_DeleteEvent;
            CopyTraderClass = null;
        }

        private void CopyTraderClass_DeleteEvent()
        {
            Close();
        }  

        
    }
}
