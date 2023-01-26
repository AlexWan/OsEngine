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

namespace OsEngine.Entity
{
    /// <summary>
    /// Логика взаимодействия для AwaitUi.xaml
    /// </summary>
    public partial class AwaitUi : Window
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="label">подпись на окне</param>
        /// <param name="externalManagement">true - нужно управлять бегунком снаружи. false - неизвестно сколько осталось время до конца</param>
        public AwaitUi(string label, bool externalManagement)
        {
            InitializeComponent();

            LabelAwaitString.Content = label;


        }

        public int maxValue;

        public int curValue;

        private void PaintThreadArea()
        {



        }


    }
}
