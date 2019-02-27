/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Language;

namespace OsEngine.Logging
{
    /// <summary>
    /// window of mailing server settings
    /// Окно настроек сервера почтовой рассылки
    /// </summary>
    public partial class ServerMailDeliveryUi
    {
         public ServerMailDeliveryUi() // constructor / конструктор
        {
            InitializeComponent();

             ServerMail serverMail = ServerMail.GetServer();

            TextBoxMyAdress.Text = serverMail.MyAdress;
            TextBoxPassword.Text = serverMail.MyPassword;
            TextBoxAdress.Text = "";

            if (serverMail.Adress != null)
            {
                for (int i = 0; i < serverMail.Adress.Length; i++)
                {
                    TextBoxAdress.Text += serverMail.Adress[i] + "\r\n";
                }
            }

            ComboBoxMyMaster.Items.Add("Yandex");
            ComboBoxMyMaster.Items.Add("Google");

            if (serverMail.Smtp == "smtp.yandex.ru")
            {
                ComboBoxMyMaster.SelectedItem = "Yandex";
            }
            else
            {
                ComboBoxMyMaster.SelectedItem = "Google";
            }

            Title = OsLocalization.Logging.TitleEmailServer;
            ButtonAccept.Content = OsLocalization.Logging.Button1;
            Label11.Content = OsLocalization.Logging.Label11;
            Label12.Content = OsLocalization.Logging.Label12;
            Label13.Content = OsLocalization.Logging.Label13;
            Label14.Content = OsLocalization.Logging.Label14;
        }

        private void buttonAccept_Click(object sender, RoutedEventArgs e) // accept / принять
        {
            ServerMail serverMail = ServerMail.GetServer();
            serverMail.MyAdress = TextBoxMyAdress.Text;
            serverMail.MyPassword = TextBoxPassword.Text;
            string[] lockal = TextBoxAdress.Text.Split('\r');


            string shit = "";

            for (int i = 0; i < lockal.Length; i++)
            {
                shit += lockal[i];
            }
            lockal = shit.Split('\n');
            string[] lockal2 = null;
            for (int i = 0, ii = 0; i < lockal.Length; i++)
            {
                if (lockal[i] != "")
                {
                    if (lockal2 == null)
                    {
                        lockal2 = new string[1];
                        lockal2[0] = lockal[i];
                        ii++;
                    }
                    else
                    {
                        string[] newLock = new string[lockal2.Length + 1];
                        for (int iii = 0; iii < lockal2.Length; iii++)
                        {
                            newLock[iii] = lockal2[iii];
                        }
                        newLock[ii] = lockal[i];
                        lockal2 = newLock;
                        ii++;
                    }
                }
            }


            serverMail.Adress = lockal2;

            if (ComboBoxMyMaster.SelectedItem.ToString() == "Yandex")
            {
                serverMail.Smtp = "smtp.yandex.ru";
            }
            else
            {
                serverMail.Smtp = "smtp.gmail.com";
            }
            serverMail.Save();
            Close();
        }
    }
}
