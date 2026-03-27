/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using System;
using System.Threading;
using System.Windows;

namespace OsEngine.Market.Servers.Finam
{
    public class FinamTokenProvider
    {
        private string _token;
        private DateTime _expiry = DateTime.MinValue;
        private readonly FinamServerRealization _server;
        private readonly object _cacheSync = new object();
        private readonly object _authSync = new object();

        public FinamTokenProvider(FinamServerRealization server)
        {
            _server = server;
        }

        /// <summary>
        /// Текущий JWT, если ещё есть запас по exp (без UI и без обновления).
        /// </summary>
        public string TryGetCachedToken()
        {
            lock (_cacheSync)
            {
                if (string.IsNullOrEmpty(_token) == false && DateTime.UtcNow < _expiry.AddMinutes(-5))
                {
                    return _token;
                }
            }

            return null;
        }

        /// <summary>
        /// Валидный токен: из кэша или один показ окна логина.
        /// </summary>
        public string GetToken()
        {
            string cached = TryGetCachedToken();
            if (cached != null)
            {
                return cached;
            }

            lock (_authSync)
            {
                cached = TryGetCachedToken();
                if (cached != null)
                {
                    return cached;
                }

                string captured = null;

                try
                {
                    Application app = Application.Current;

                    if (app == null || app.Dispatcher == null)
                    {
                        return null;
                    }

                    if (app.Dispatcher.CheckAccess())
                    {
                        OpenAuth();
                    }
                    else
                    {
                        app.Dispatcher.Invoke(OpenAuth);
                    }

                    DateTime endAwaitTime = DateTime.Now.AddSeconds(20);

                    while(true)
                    {
                        if (DateTime.Now > endAwaitTime)
                        {
                            _server.SendLogMessage($"Finam. Ошибка авторизации. Нет ответа дольше 20 секунд. \n Сервера не доступны с IP за пределами РФ. Пробуйте отключать ВПН и прокси", LogMessageType.Error);
                            return null;
                        }

                        if(string.IsNullOrEmpty(_winUi.CapturedToken) == false)
                        {
                            captured = _winUi.CapturedToken;
                            break;
                        }

                        Thread.Sleep(1000);
                    }

                }
                catch (Exception ex)
                {
                    _server.SendLogMessage($"Finam token auth window error: {ex}", LogMessageType.Error);
                    return null;
                }

                if (string.IsNullOrEmpty(captured))
                {
                    _server.SendLogMessage("Finam: token not received. Login required.", LogMessageType.Error);
                    return null;
                }

                lock (_cacheSync)
                {
                    _token = captured;
                    _expiry = FinamJwt.GetExpiryUtcOrFallback(captured);
                    _server.SendLogMessage($"Finam: token received. Expiry UTC: {_expiry:yyyy-MM-dd HH:mm:ss}", LogMessageType.System);
                    return _token;
                }
            }
        }

        private FinamAuthWindow _winUi;

        private void OpenAuth()
        {
            _winUi = new FinamAuthWindow();
            _winUi.Show();
        }
    }
}
