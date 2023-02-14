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
using OsEngine.Language;

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
        public AwaitUi(AwaitObject master)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _master = master;

            LabelAwaitString.Content = _master.Label;
            ProgressBarAwait.Maximum = Convert.ToDouble(_master.ValueMaximum);
            ProgressBarAwait.Value = Convert.ToDouble(_master.ValueCurrent);

            _master.LabelChangedEvent += _master_LabelChangedEvent;
            _master.ValueCurrentChangedEvent += _master_ValueCurrentChangedEvent;
            _master.ValueMaximumChangedEvent += _master_ValueMaximumChangedEvent;
            _master.DisposedEvent += _master_DisposedEvent;

            Title = OsLocalization.Entity.AwaitUiLabel1;
        }

        AwaitObject _master;

        private void _master_DisposedEvent()
        {
            try
            {
                if (LabelAwaitString.Dispatcher.CheckAccess() == false)
                {
                    LabelAwaitString.Dispatcher.Invoke(new Action(_master_DisposedEvent));
                    return;
                }

                _master.LabelChangedEvent -= _master_LabelChangedEvent;
                _master.ValueCurrentChangedEvent -= _master_ValueCurrentChangedEvent;
                _master.ValueMaximumChangedEvent -= _master_ValueMaximumChangedEvent;
                _master.DisposedEvent -= _master_DisposedEvent;
                _master = null;

                Close();
            }
            catch
            {
                // ignore
            }

        }

        private void _master_ValueMaximumChangedEvent(decimal value)
        {
            try
            {
                if (LabelAwaitString.Dispatcher.CheckAccess() == false)
                {
                    LabelAwaitString.Dispatcher.Invoke(new Action<decimal>(_master_ValueMaximumChangedEvent), value);
                    return;
                }

                ProgressBarAwait.Maximum = Convert.ToDouble(_master.ValueMaximum);
            }
            catch
            {
                // ignore
            }


        }

        private void _master_ValueCurrentChangedEvent(decimal value)
        {
            try
            {
                if (LabelAwaitString.Dispatcher.CheckAccess() == false)
                {
                    LabelAwaitString.Dispatcher.Invoke(new Action<decimal>(_master_ValueCurrentChangedEvent), value);
                    return;
                }
                ProgressBarAwait.Value = Convert.ToDouble(_master.ValueCurrent);
            }
            catch
            {
                // ignore
            }

        }

        private void _master_LabelChangedEvent(string value)
        {
            try
            {
                if (LabelAwaitString.Dispatcher.CheckAccess() == false)
                {
                    LabelAwaitString.Dispatcher.Invoke(new Action<string>(_master_LabelChangedEvent), value);
                    return;
                }
                LabelAwaitString.Content = _master.Label;
            }
            catch
            {
                // ignore
            }


        }
    }
}
