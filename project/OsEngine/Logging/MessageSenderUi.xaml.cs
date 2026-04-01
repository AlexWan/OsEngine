/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using System.Windows.Threading;
using OsEngine.Instructions;
using OsEngine.Language;
using OsEngine.Market;

namespace OsEngine.Logging
{
    /// <summary>
    /// Messaging settings window
    /// Окно настроек рассылки сообщений
    /// </summary>
    public partial class MessageSenderUi
    {
        /// <summary>
        /// distribution manager
        /// менедженр рассылки
        /// </summary>
        private readonly MessageSender _sender;

        public MessageSenderUi(MessageSender sender) // constructor / конструктор
        {
            _sender = sender;
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            LoadDateOnForm();

            Title = OsLocalization.Logging.TitleMessageSenderUi;

            Label3.Content = OsLocalization.Logging.Label3;
            Label4.Content = OsLocalization.Logging.Label32;
            Label19.Content = OsLocalization.Logging.Label19;
            Label22.Content = OsLocalization.Logging.Label22;
            Label5.Content = OsLocalization.Logging.Label5;
            Label52.Content = OsLocalization.Logging.Label5;
            Label53.Content = OsLocalization.Logging.Label5;
            Label54.Content = OsLocalization.Logging.Label5;
            ButtonAccept.Content = OsLocalization.Logging.Button1;
            ButtonSmsGlobeSet.Content = OsLocalization.Logging.Button2;
            ButtonVkGlobeSet.Content = OsLocalization.Logging.Button4;
            ButtonWebhookGlobeSet.Content = OsLocalization.Logging.Button2;
            ButtonTelegramGlobeSet.Content = OsLocalization.Logging.Button3;

            CheckBoxSmsSignal.Content = OsLocalization.Logging.Label6;
            CheckBoxSmsTrade.Content = OsLocalization.Logging.Label7;
            CheckBoxSmsError.Content = OsLocalization.Logging.Label8;
            CheckBoxSmsSystem.Content = OsLocalization.Logging.Label9;
            CheckBoxSmsConnect.Content = OsLocalization.Logging.Label10;

            CheckBoxVkSignal.Content = OsLocalization.Logging.Label6;
            CheckBoxVkUser.Content = OsLocalization.Logging.Label26;
            CheckBoxVkTrade.Content = OsLocalization.Logging.Label7;
            CheckBoxVkSystem.Content = OsLocalization.Logging.Label9;
            CheckBoxVkError.Content = OsLocalization.Logging.Label8;
            CheckBoxVkConnect.Content = OsLocalization.Logging.Label10;

            CheckBoxWebhookSignal.Content = OsLocalization.Logging.Label6;
            CheckBoxWebhookTrade.Content = OsLocalization.Logging.Label7;
            CheckBoxWebhookError.Content = OsLocalization.Logging.Label8;
            CheckBoxWebhookSystem.Content = OsLocalization.Logging.Label9;
            CheckBoxWebhookConnect.Content = OsLocalization.Logging.Label10;

            CheckBoxTelegramSignal.Content = OsLocalization.Logging.Label6;
            CheckBoxTelegramTrade.Content = OsLocalization.Logging.Label7;
            CheckBoxTelegramError.Content = OsLocalization.Logging.Label8;
            CheckBoxTelegramSystem.Content = OsLocalization.Logging.Label9;
            CheckBoxTelegramConnect.Content = OsLocalization.Logging.Label10;
            CheckBoxTelegramUser.Content = OsLocalization.Logging.Label26;

            this.Activate();
            this.Focus();

            if (InteractiveInstructions.MessageSenderPosts.AllInstructionsInClass == null
            || InteractiveInstructions.MessageSenderPosts.AllInstructionsInClass.Count == 0)
            {
                ButtonPostsMessageSender.Visibility = Visibility.Hidden;
            }
            else
            {
                ButtonPostsMessageSender.Click += ButtonPostsMessageSender_Click;
            }

            StartButtonBlinkAnimation();
        }

        private void StartButtonBlinkAnimation()
        {
            try
            {
                DispatcherTimer timer = new DispatcherTimer();
                int blinkCount = 0;
                bool isGreenVisible = true;

                timer.Interval = TimeSpan.FromMilliseconds(300);
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (blinkCount >= 20)
                        {
                            timer.Stop();
                            GreenCollectionMessageSender.Opacity = 1;
                            WhiteCollectionMessageSender.Opacity = 0;
                            return;
                        }

                        if (isGreenVisible)
                        {
                            GreenCollectionMessageSender.Opacity = 0;
                            WhiteCollectionMessageSender.Opacity = 1;
                        }
                        else
                        {
                            GreenCollectionMessageSender.Opacity = 1;
                            WhiteCollectionMessageSender.Opacity = 0;
                        }

                        isGreenVisible = !isGreenVisible;
                        blinkCount++;
                    }
                    catch (Exception ex)
                    {
                        ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                        timer.Stop();
                    }
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        /// <summary>
        /// upload settings to the form
        /// выгрузить настройки на форму
        /// </summary>
        private void LoadDateOnForm()
        {
            // Webhook settings
            ComboBoxModeWebhook.Items.Add(OsLocalization.Logging.Label1);
            ComboBoxModeWebhook.Items.Add(OsLocalization.Logging.Label2);

            if (_sender.WebhookSendOn)
            {
                ComboBoxModeWebhook.Text = OsLocalization.Logging.Label1;
            }
            else
            {
                ComboBoxModeWebhook.Text = OsLocalization.Logging.Label2;
            }

            CheckBoxWebhookSignal.IsChecked = _sender.WebhookSignalSendOn;
            CheckBoxWebhookTrade.IsChecked = _sender.WebhookTradeSendOn;
            CheckBoxWebhookError.IsChecked = _sender.WebhookErrorSendOn;
            CheckBoxWebhookSystem.IsChecked = _sender.WebhookSystemSendOn;
            CheckBoxWebhookConnect.IsChecked = _sender.WebhookConnectSendOn;

            // Telegram settings
            ComboBoxModeTelegram.Items.Add(OsLocalization.Logging.Label1);
            ComboBoxModeTelegram.Items.Add(OsLocalization.Logging.Label2);

            if (_sender.TelegramSendOn)
            {
                ComboBoxModeTelegram.Text = OsLocalization.Logging.Label1;
            }
            else
            {
                ComboBoxModeTelegram.Text = OsLocalization.Logging.Label2;
            }

            CheckBoxTelegramSignal.IsChecked = _sender.TelegramSignalSendOn;
            CheckBoxTelegramTrade.IsChecked = _sender.TelegramTradeSendOn;
            CheckBoxTelegramError.IsChecked = _sender.TelegramErrorSendOn;
            CheckBoxTelegramSystem.IsChecked = _sender.TelegramSystemSendOn;
            CheckBoxTelegramConnect.IsChecked = _sender.TelegramConnectSendOn;
            CheckBoxTelegramUser.IsChecked = _sender.TelegramUserSendOn;

            // SMS settings
            ComboBoxModeSms.Items.Add(OsLocalization.Logging.Label1);
            ComboBoxModeSms.Items.Add(OsLocalization.Logging.Label2);

            if (_sender.SmsSendOn)
            {
                ComboBoxModeSms.Text = OsLocalization.Logging.Label1;
            }
            else
            {
                ComboBoxModeSms.Text = OsLocalization.Logging.Label2;
            }

            CheckBoxSmsSignal.IsChecked = _sender.SmsSignalSendOn;
            CheckBoxSmsTrade.IsChecked = _sender.SmsTradeSendOn;
            CheckBoxSmsError.IsChecked = _sender.SmsErrorSendOn;
            CheckBoxSmsSystem.IsChecked = _sender.SmsSystemSendOn;
            CheckBoxSmsConnect.IsChecked = _sender.SmsConnectSendOn;

            // VK settings (replaced Mail settings)
            ComboBoxModeVk.Items.Add(OsLocalization.Logging.Label1);
            ComboBoxModeVk.Items.Add(OsLocalization.Logging.Label2);

            if (_sender.VkSendOn)
            {
                ComboBoxModeVk.Text = OsLocalization.Logging.Label1;
            }
            else
            {
                ComboBoxModeVk.Text = OsLocalization.Logging.Label2;
            }

            CheckBoxVkSignal.IsChecked = _sender.VkSignalSendOn;
            CheckBoxVkTrade.IsChecked = _sender.VkTradeSendOn;
            CheckBoxVkError.IsChecked = _sender.VkErrorSendOn;
            CheckBoxVkSystem.IsChecked = _sender.VkSystemSendOn;
            CheckBoxVkConnect.IsChecked = _sender.VkConnectSendOn;
            CheckBoxVkUser.IsChecked = _sender.VkUserSendOn;
        }

        /// <summary>
        /// save
        /// сохранить
        /// </summary>
        private void Save()
        {
            // Webhook save
            if (ComboBoxModeWebhook.Text == OsLocalization.Logging.Label1)
            {
                _sender.WebhookSendOn = true;
            }
            else
            {
                _sender.WebhookSendOn = false;
            }

            _sender.WebhookSignalSendOn = CheckBoxWebhookSignal.IsChecked.Value;
            _sender.WebhookTradeSendOn = CheckBoxWebhookTrade.IsChecked.Value;
            _sender.WebhookErrorSendOn = CheckBoxWebhookError.IsChecked.Value;
            _sender.WebhookSystemSendOn = CheckBoxWebhookSystem.IsChecked.Value;
            _sender.WebhookConnectSendOn = CheckBoxWebhookConnect.IsChecked.Value;

            // Telegram save
            if (ComboBoxModeTelegram.Text == OsLocalization.Logging.Label1)
            {
                _sender.TelegramSendOn = true;
            }
            else
            {
                _sender.TelegramSendOn = false;
            }

            _sender.TelegramSignalSendOn = CheckBoxTelegramSignal.IsChecked.Value;
            _sender.TelegramTradeSendOn = CheckBoxTelegramTrade.IsChecked.Value;
            _sender.TelegramErrorSendOn = CheckBoxTelegramError.IsChecked.Value;
            _sender.TelegramSystemSendOn = CheckBoxTelegramSystem.IsChecked.Value;
            _sender.TelegramConnectSendOn = CheckBoxTelegramConnect.IsChecked.Value;
            _sender.TelegramUserSendOn = CheckBoxTelegramUser.IsChecked.Value;

            // SMS save
            if (ComboBoxModeSms.Text == OsLocalization.Logging.Label1)
            {
                _sender.SmsSendOn = true;
            }
            else
            {
                _sender.SmsSendOn = false;
            }

            _sender.SmsSignalSendOn = CheckBoxSmsSignal.IsChecked.Value;
            _sender.SmsTradeSendOn = CheckBoxSmsTrade.IsChecked.Value;
            _sender.SmsErrorSendOn = CheckBoxSmsError.IsChecked.Value;
            _sender.SmsSystemSendOn = CheckBoxSmsSystem.IsChecked.Value;
            _sender.SmsConnectSendOn = CheckBoxSmsConnect.IsChecked.Value;

            // VK save 
            if (ComboBoxModeVk.Text == OsLocalization.Logging.Label1)
            {
                _sender.VkSendOn = true;
            }
            else
            {
                _sender.VkSendOn = false;
            }

            _sender.VkSignalSendOn = CheckBoxVkSignal.IsChecked.Value;
            _sender.VkTradeSendOn = CheckBoxVkTrade.IsChecked.Value;
            _sender.VkErrorSendOn = CheckBoxVkError.IsChecked.Value;
            _sender.VkSystemSendOn = CheckBoxVkSystem.IsChecked.Value;
            _sender.VkConnectSendOn = CheckBoxVkConnect.IsChecked.Value;
            _sender.VkUserSendOn = CheckBoxVkUser.IsChecked.Value;

            _sender.Save();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e) // accept button / кнопка принять
        {
            Save();
            Close();
        }

        private void ButtonWebhookGlobeSet_Click(object sender, RoutedEventArgs e) // button to configure the webhook server / кнопка настроить сервер рассылки сообщений через вебхуки
        {
            ServerWebhook.GetServer().ShowDialog();
        }

        private void ButtonSmsGlobeSet_Click(object sender, RoutedEventArgs e) // button to configure the SMS messaging server / кнопка настроить сервер Смс рассылки
        {
            ServerSms.GetSmsServer().ShowDialog();
        }

        private void ButtonTelegramGlobeSet_Click(object sender, RoutedEventArgs e) // button to configure the Telegram server / кнопка настроить сервер Telegram
        {
            ServerTelegram.GetServer().ShowDialog();
        }

        private void ButtonVkGlobeSet_Click(object sender, RoutedEventArgs e) // button to configure the VK server / кнопка настроить сервер VK
        {
            ServerVk.GetServer().ShowDialog();
        }

        #region Posts collection

        private InstructionsUi _instructionsUi;

        private void ButtonPostsMessageSender_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_instructionsUi == null)
                {
                    _instructionsUi = new InstructionsUi(
                        InteractiveInstructions.MessageSenderPosts.AllInstructionsInClass, InteractiveInstructions.MessageSenderPosts.AllInstructionsInClassDescription);
                    _instructionsUi.Show();
                    _instructionsUi.Closed += _instructionsUi_Closed;
                }
                else
                {
                    if (_instructionsUi.WindowState == WindowState.Minimized)
                    {
                        _instructionsUi.WindowState = WindowState.Normal;
                    }
                    _instructionsUi.Activate();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void _instructionsUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _instructionsUi.Closed -= _instructionsUi_Closed;
                _instructionsUi = null;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}