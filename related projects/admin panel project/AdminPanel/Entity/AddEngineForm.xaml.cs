using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using AdminPanel.Language;
using AdminPanel.ViewModels;

namespace AdminPanel.Entity
{
    /// <summary>
    /// Логика взаимодействия для AddEngineForm.xaml
    /// </summary>
    public partial class AddEngineForm : Window
    {
        EngineViewModel _engine;
        private List<EngineViewModel> _engineViewModels;

        public AddEngineForm(string title, EngineViewModel engine, List<EngineViewModel> engineViewModels)
        {
            InitializeComponent();
            Title = title + " os engine";
            BtnAccept.Content = OsLocalization.Entity.ButtonAccept;

            if (engine == null)
            {
                engine = new EngineViewModel();
            }

            DataContext = engine;
            _engine = engine;
            _engineViewModels = engineViewModels;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!Validate())
            {
                return;
            }
            DialogResult = true;
            Close();
        }

        private bool Validate()
        {
            var ch = _engine.EngineName[0];
            if (Int32.TryParse(ch.ToString(), out var _))
            {
                MessageBox.Show(OsLocalization.MainWindow.Label13);
                return false;
            }
            if (string.IsNullOrEmpty(_engine.EngineName) ||
                !Int32.TryParse(_engine.Port, out _) ||
                string.IsNullOrEmpty(_engine.Token) ||
                _engine.RebootRam <= 0)
            {
                MessageBox.Show(OsLocalization.MainWindow.Label13);
                return false;
            }

            if (_engineViewModels == null)
            {
                return true;
            }
            if (_engineViewModels.Find(e => e.EngineName == _engine.EngineName) != null)
            {
                MessageBox.Show(OsLocalization.MainWindow.Label14);
                return false;
            }
            return true;
        }
    }
}
