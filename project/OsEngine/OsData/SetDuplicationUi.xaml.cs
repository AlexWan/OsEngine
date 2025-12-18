/*
 *Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Market;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;


namespace OsEngine.OsData
{
    public partial class SetDuplicationUi : Window
    {
        private OsDataSet _set;

        private string _newPathForCopy;

        public SetDuplicationUi(OsDataSet set)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _set = set;

            if (_set.IsThereDublicate && _set.Dublicator != null)
            {
                ComboBoxRegime.Text = _set.Dublicator.Regime;
                PathForSetTextBox.Text = _set.Dublicator.PathForDublicate;
                _newPathForCopy = _set.Dublicator.PathForDublicate;
                ComboBoxPeriods.Text = _set.Dublicator.UpdatePeriod.Minutes.ToString();
            }
            else
            {
                _newPathForCopy = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                PathForSetTextBox.Text = _newPathForCopy;

            }

            Title = OsLocalization.Data.Label86;
            RegimeLabel.Content = OsLocalization.Data.Label20;
            StatusLabel.Content = "";
            NewPathButton.Content = OsLocalization.Data.Label80;
            CurFolderLabel.Content = OsLocalization.Data.Label81;
            NowButton.Content = OsLocalization.Data.Label82;
            EveryLabel.Content = OsLocalization.Data.Label83;
            MinutsLabel.Content = OsLocalization.Data.Label84;

            Activate();
            Focus();

            Closed += SetDuplicationUi_Closed;
        }

        private void SetDuplicationUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _set = null;
                _newPathForCopy = null;
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void NewPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fullPath = Path.GetFullPath(_newPathForCopy + "\\" + _set.SetName);

                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.SelectedPath = fullPath;
                    dialog.Description = OsLocalization.Data.Label85;
                    dialog.ShowNewFolderButton = true;

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        PathForSetTextBox.Text = dialog.SelectedPath;
                        _newPathForCopy = dialog.SelectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void NowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(PathForSetTextBox.Text) || string.IsNullOrEmpty(_newPathForCopy))
                {
                    ServerMaster.Log?.ProcessMessage(OsLocalization.Data.Label91, Logging.LogMessageType.Error);
                    return;
                }

                if (!Directory.Exists(_newPathForCopy + "\\" + _set.SetName))
                {
                    Directory.CreateDirectory(_newPathForCopy + "\\" + _set.SetName);
                }

                bool succes = TryCopyDirectory("Data\\" + _set.SetName, _newPathForCopy + "\\" + _set.SetName);

                if (succes)
                {
                    StatusLabel.Content = OsLocalization.Data.Label87;

                    SetDublicator dublicator = new SetDublicator();

                    int min = int.Parse(ComboBoxPeriods.Text);

                    dublicator.Regime = ComboBoxRegime.Text;
                    dublicator.PathForDublicate = PathForSetTextBox.Text;
                    dublicator.UpdatePeriod = new TimeSpan(0, min, 0);
                    dublicator.TimeWriteOriginalSet = Directory.GetLastWriteTime("Data\\" + _set.SetName);

                    _set.Dublicator = dublicator;
                    _set.IsThereDublicate = true;

                    _set.Dublicator.SaveDublicateSettings("Data\\" + _set.SetName + @"\\DublicateSettings.txt");
                }
                else
                {
                    throw new Exception(OsLocalization.Data.Label90);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private bool TryCopyDirectory(string sourceDir, string destinationDir)
        {
            if (_set.IsThereDublicate && _set.Dublicator != null)
            {
                _set.Dublicator.UpdateDublicate(_set.SetName);

                return true;
            }

            Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(sourceDir, destinationDir, true);

            long sizeAfterCopy = GetDirectorySize(destinationDir);

            if (sizeAfterCopy > 0)
                return true;
            else
                return false;
        }

        private long GetDirectorySize(string directoryPath)
        {
            string[] files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);

            if (files == null || files.Length == 0)
                return 0;

            long size = 0;

            for (int i = 0; i < files.Length; i++)
            {
                size += files[i].Length;
            }

            return size;
        }

        private void RegimeChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_set == null || _set.IsThereDublicate == false)
                    return;

                if (!(ComboBoxRegime.SelectedItem is ComboBoxItem selectedItem))
                    return;

                string selectedValue = selectedItem.Content.ToString();

                if (selectedValue == "On") // переключились на On
                {
                    _set.Dublicator.Regime = "On";

                    int min = int.Parse(ComboBoxPeriods.Text);

                    _set.Dublicator.UpdatePeriod = new TimeSpan(0, min, 0);
                }
                else
                {
                    _set.Dublicator.Regime = "Off";
                }

                _set.Dublicator.SaveDublicateSettings("Data\\" + _set.SetName + @"\\DublicateSettings.txt");

            }
            catch (Exception ex)
            {
                ServerMaster.Log?.ProcessMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }
    }
}
