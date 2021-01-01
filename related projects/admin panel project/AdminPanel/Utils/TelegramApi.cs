using AdminPanel.Entity;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TeleSharp.TL;
using TLSharp.Core;

namespace AdminPanel.Utils
{
    public class TelegramApi
    {
        private TelegramClient _client;
        private TLUser _user;
        private string _phone = "";
        
        public async Task<bool> LogIn(int key, string token, string phone)
        {
            try
            {
                if (_client == null)
                {
                    _client = new TelegramClient(key, token);
                    await _client.ConnectAsync();
                }
                if (_user != null)
                {
                    return true;
                }
                _phone = phone;

                var hash = await _client.SendCodeRequestAsync(_phone);

                string code = "";
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var input = new InputBox();
                    input.ShowDialog();
                    code = input.Code;
                });
                
                if (string.IsNullOrEmpty(code))
                {
                    MessageBox.Show("Empty code!");
                    return false;
                }

                _user = await _client.MakeAuthAsync(_phone, hash, code);
                if (_user != null)
                {
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                _client?.Dispose();
                _client = null;
                _user = null;
                return false;
            }
        }

        public void SendMessage(string message, string receiverPhone)
        {
            if (_user == null)
            {
                return;
            }
            try
            {
                var contacts = _client.GetContactsAsync().Result;

                var receiver = contacts.Users.Where(x => x.GetType() == typeof(TLUser)).Cast<TLUser>().FirstOrDefault(x => x.Phone == receiverPhone);

                if (receiver != null)
                {
                    var res = _client.SendMessageAsync(new TLInputPeerUser() { UserId = receiver.Id }, message).Result;
                }
            }
            catch (Exception e)
            {
                //ignore
            }
        }
    }
}
