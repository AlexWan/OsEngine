/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Language;

namespace OsEngine.Entity
{
    public partial class AwaitUi : Window
    {
        /// <summary>
        /// Creating a program standby window
        /// </summary>
        /// <param name="label">window message</param>
        /// <param name="externalManagement">
        /// true - you have to operate the slider from the outside. 
        /// false - unknown amount of time left until the end</param>
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

        public void Dispose()
        {
            _master.Dispose();
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

                if (_master == null) return;

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