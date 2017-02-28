/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;

namespace OsEngine.Logging
{
    /// <summary>
    /// Окно настроек сервера почтовой рассылки
    /// </summary>
    public partial class ServerMailDeliveryUi
    {
         public ServerMailDeliveryUi() // конструктор
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

            if (serverMail.Smtp == "smtp.yandex.ru")
            {
                ComboBoxMyMaster.SelectedItem = "Яндекс";
            }
            else
            {
                ComboBoxMyMaster.SelectedItem = "Гугл";
            }
        }

        private void buttonAccept_Click(object sender, RoutedEventArgs e) // принять
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

            if (ComboBoxMyMaster.SelectedItem.ToString() == "Яндекс")
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
