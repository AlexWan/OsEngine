using System;
using System.Windows;
using OsEngine.Language;
using OsEngine.Logging;

namespace OsEngine.OsTrader.Gui
{
    public partial class BotsMigrationUi
    {
        public BotsMigrationUi(OsTraderMaster master)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _master = master;

            Title = OsLocalization.Trader.Label762;
            TextBoxDescription.Text = OsLocalization.Trader.Label763;
            TabItemSave.Header = OsLocalization.Trader.Label766;
            TabItemLoad.Header = OsLocalization.Trader.Label767;
            TextBlockPrefix.Text = OsLocalization.Trader.Label768;
            ButtonSaveBots.Content = OsLocalization.Trader.Label748;
            ButtonLoadBots.Content = OsLocalization.Trader.Label749;
            ButtonClose.Content = OsLocalization.Trader.Label764;

            ButtonSaveBots.Click += ButtonSaveBots_Click;
            ButtonLoadBots.Click += ButtonLoadBots_Click;
            ButtonClose.Click += ButtonClose_Click;
            ButtonHint.Click += ButtonHint_Click;
            Closed += BotsMigrationUi_Closed;

            this.Activate();
            this.Focus();
        }

        private OsTraderMaster _master;

        private void ButtonSaveBots_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string prefix = TextBoxPrefix.Text;

                if (prefix != null)
                {
                    prefix = prefix.Trim();
                }

                if (string.IsNullOrEmpty(prefix) == false
                    && (prefix.Contains("@") || prefix.Contains(":")))
                {
                    Entity.CustomMessageBoxUi uiBadPrefix = new Entity.CustomMessageBoxUi(OsLocalization.Trader.Label769);
                    uiBadPrefix.ShowDialog();
                    return;
                }

                System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Filter = "Txt files|*.txt";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _master.SaveBotsPreset(dialog.FileName, prefix);
                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonLoadBots_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
                dialog.Filter = "Txt files|*.txt";

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _master.LoadBotsPreset(dialog.FileName);
                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonHint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Entity.CustomMessageBoxUi ui = new Entity.CustomMessageBoxUi(OsLocalization.Trader.Label770);
                ui.TextBoxMessage.TextAlignment = TextAlignment.Left;
                ui.Height = 290;
                ui.ShowDialog();
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();

                if (Owner != null)
                {
                    Owner.Activate();
                }
            }
            catch (Exception error)
            {
                _master.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void BotsMigrationUi_Closed(object sender, EventArgs e)
        {
            try
            {
                ButtonSaveBots.Click -= ButtonSaveBots_Click;
                ButtonLoadBots.Click -= ButtonLoadBots_Click;
                ButtonClose.Click -= ButtonClose_Click;
                ButtonHint.Click -= ButtonHint_Click;
                Closed -= BotsMigrationUi_Closed;

                _master = null;
            }
            catch (Exception error)
            {
                Market.ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}
