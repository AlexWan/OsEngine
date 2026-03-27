/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace OsEngine.Market.Servers.Finam
{
    public partial class FinamAuthWindow : Window
    {
        public string CapturedToken { get; private set; }

        private bool _completed;
        private bool _exportSubmitAutoClicked;
        private bool _cookieBannerDismissed;
        private int _exportAutoClickBusy;

        /// <summary>
        /// Клик по баннеру cookies: <c>#cookie-accept-btn</c> («Принять»), пока не начато скачивание через «Получить файл».
        /// </summary>
        private const string ClickCookieAcceptScript =
            "(function(){try{var b=document.getElementById(\"cookie-accept-btn\")||document.querySelector(\"#cookie-accept-btn\");if(b){b.click();return true;}return false;}catch(e){return false;}})()";

        /// <summary>
        /// Клик по кнопке «Получить файл»: <c>button.button-generic.button-yellow[type=submit]</c> с подписью «Получить файл».
        /// </summary>
        private const string ClickFinamExportSubmitScript =
            "(function(){try{var n=document.querySelectorAll(\"button.button-generic.button-yellow[type='submit']\");for(var i=0;i<n.length;i++){var t=(n[i].textContent||\"\").replace(/\\s+/g,\" \").trim();if(t.indexOf(\"Получить файл\")>=0){n[i].click();return true;}}return false;}catch(e){return false;}})()";

        private readonly bool _clearCookiesBeforeNavigate;

        /// <param name="clearCookiesBeforeNavigate">
        /// Удалить все cookies профиля WebView2 перед открытием страницы (чистая сессия, снова баннер и логин).
        /// </param>
        public FinamAuthWindow(bool clearCookiesBeforeNavigate = false)
        {
            _clearCookiesBeforeNavigate = clearCookiesBeforeNavigate;
            InitializeComponent();
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        /// <summary>
        /// Стереть cookies текущего WebView2 (после <see cref="WebView.CoreWebView2"/> уже создан).
        /// </summary>
        public static Task ClearWebViewCookiesAsync(CoreWebView2 core)
        {
            if (core?.CookieManager == null)
            {
                return Task.CompletedTask;
            }

            core.CookieManager.DeleteAllCookies();
            return Task.CompletedTask;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await WebView.EnsureCoreWebView2Async();
                CoreWebView2 core = WebView.CoreWebView2;

                if (_clearCookiesBeforeNavigate)
                {
                    await ClearWebViewCookiesAsync(core);
                }

                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                core.WebResourceRequested += OnWebResourceRequested;
                core.WebResourceResponseReceived += OnResponseReceived;
                core.NewWindowRequested += OnNewWindowRequested;
                core.NavigationCompleted += OnNavigationCompleted;

                core.Navigate("https://www.finam.ru/quote/moex/gazp/export/");
            }
            catch
            {
                // ignore
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (WebView?.CoreWebView2 != null)
            {
                CoreWebView2 core = WebView.CoreWebView2;
                core.WebResourceRequested -= OnWebResourceRequested;
                core.WebResourceResponseReceived -= OnResponseReceived;
                core.NewWindowRequested -= OnNewWindowRequested;
                core.NavigationCompleted -= OnNavigationCompleted;
                try
                {
                    core.RemoveWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                }
                catch
                {
                    // ignore
                }
            }
        }

        private async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (_completed || e.IsSuccess == false || _exportSubmitAutoClicked)
            {
                return;
            }

            CoreWebView2 core = WebView?.CoreWebView2;
            if (core == null)
            {
                return;
            }

            string uri = core.Source;
            if (ShouldAutoClickFinamExportPage(uri) == false)
            {
                return;
            }

            if (Interlocked.Exchange(ref _exportAutoClickBusy, 1) != 0)
            {
                return;
            }

            try
            {
                const int maxAttempts = 12;
                const int delayMs = 500;

                if (_cookieBannerDismissed == false && _completed == false)
                {
                    for (int attempt = 0; attempt < maxAttempts && _completed == false && _cookieBannerDismissed == false; attempt++)
                    {
                        if (attempt > 0)
                        {
                            await Task.Delay(delayMs).ConfigureAwait(true);
                        }

                        if (WebView?.CoreWebView2 == null || _completed)
                        {
                            return;
                        }

                        string cookieJson = await WebView.CoreWebView2.ExecuteScriptAsync(ClickCookieAcceptScript).ConfigureAwait(true);

                        if (IsJsonBooleanTrue(cookieJson))
                        {
                            _cookieBannerDismissed = true;
                            await Task.Delay(400).ConfigureAwait(true);
                            break;
                        }
                    }

                    if (_cookieBannerDismissed == false)
                    {
                        _cookieBannerDismissed = true;
                    }
                }

                for (int attempt = 0; attempt < maxAttempts && _completed == false && _exportSubmitAutoClicked == false; attempt++)
                {
                    if (attempt > 0)
                    {
                        await Task.Delay(delayMs).ConfigureAwait(true);
                    }

                    if (WebView?.CoreWebView2 == null || _completed)
                    {
                        return;
                    }

                    string jsonResult = await WebView.CoreWebView2.ExecuteScriptAsync(ClickFinamExportSubmitScript).ConfigureAwait(true);

                    if (IsJsonBooleanTrue(jsonResult))
                    {
                        _exportSubmitAutoClicked = true;
                        return;
                    }
                }
            }
            catch
            {
                // ignore script / navigation races
            }
            finally
            {
                Interlocked.Exchange(ref _exportAutoClickBusy, 0);
            }
        }

        private static bool ShouldAutoClickFinamExportPage(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                return false;
            }

            if (uri.Contains("finam.ru", StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            if (uri.Contains("/export", StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            if (uri.Contains("export9", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool IsJsonBooleanTrue(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            return string.Equals(json.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            if (_completed)
            {
                return;
            }

            e.Handled = true;

            if (string.IsNullOrWhiteSpace(e.Uri) == false && WebView?.CoreWebView2 != null)
            {
                TryCaptureToken(e.Uri, null);
                if (_completed == false)
                {
                    WebView.CoreWebView2.Navigate(e.Uri);
                }
            }
        }

        /// <summary>
        /// Запросы к finam (в т.ч. переход на export с finam_token в query, реже — Bearer).
        /// </summary>
        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (_completed)
            {
                return;
            }

            CoreWebView2WebResourceRequest req = e.Request;
            string uri = req?.Uri;
            if (string.IsNullOrEmpty(uri) || uri.Contains("finam", StringComparison.OrdinalIgnoreCase) == false)
            {
                return;
            }

            TryCaptureToken(uri, req);
        }

        private async void OnResponseReceived(object sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            if (_completed)
            {
                return;
            }

            string reqUri = e.Request?.Uri;
            if (string.IsNullOrEmpty(reqUri)
                || (reqUri.Contains("export", StringComparison.OrdinalIgnoreCase) == false
                    && reqUri.Contains("finam_token", StringComparison.OrdinalIgnoreCase) == false))
            {
                return;
            }

            TryCaptureToken(reqUri, e.Request);

            if (_completed)
            {
                return;
            }

            try
            {
                using (Stream stream = await e.Response.GetContentAsync().ConfigureAwait(true))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string body = await reader.ReadToEndAsync().ConfigureAwait(true);
                    string token = ExtractJwtFromBody(body);
                    if (string.IsNullOrEmpty(token) == false)
                    {
                        CompleteWithToken(token);
                    }
                }
            }
            catch
            {
                // ignore read errors
            }
        }

        private void TryCaptureToken(string uri, CoreWebView2WebResourceRequest request)
        {
            if (_completed)
            {
                return;
            }

            string token = ExtractTokenFromUriString(uri);
            if (string.IsNullOrEmpty(token) && request != null)
            {
                token = ExtractBearerToken(request);
            }

            if (string.IsNullOrEmpty(token) == false)
            {
                CompleteWithToken(token);
            }
        }

        private void CompleteWithToken(string token)
        {
            try
            {
                if (_completed || string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                _completed = true;
                CapturedToken = token;

                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        DialogResult = true;
                        Close();
                    }
                    catch
                    {
                        // ignore
                    }
                });
            }
            catch
            {
                // ignore
            }
        }

        private static string ExtractTokenFromUriString(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                return null;
            }

            Match queryMatch = Regex.Match(uri, @"(?:\?|&)finam_token=([^&]+)", RegexOptions.IgnoreCase);
            if (queryMatch.Success)
            {
                return Uri.UnescapeDataString(queryMatch.Groups[1].Value);
            }

            Match jwtInPath = FinamJwt.JwtRegex.Match(uri);
            if (jwtInPath.Success)
            {
                return jwtInPath.Value;
            }

            return null;
        }

        private static string ExtractBearerToken(CoreWebView2WebResourceRequest request)
        {
            if (request == null)
            {
                return null;
            }

            try
            {
                string authHeader = request.Headers.GetHeader("Authorization");
                if (string.IsNullOrWhiteSpace(authHeader))
                {
                    return null;
                }

                Match bearerMatch = FinamJwt.BearerAuthorizationJwtRegex.Match(authHeader);
                if (bearerMatch.Success)
                {
                    return bearerMatch.Groups[1].Value;
                }
            }
            catch
            {
                // ignore missing headers
            }

            return null;
        }

        private static string ExtractJwtFromBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            Match match = FinamJwt.JwtRegex.Match(body);
            if (match.Success)
            {
                return match.Value;
            }

            try
            {
                JsonDocument doc = JsonDocument.Parse(body);
                string[] fields = { "token", "jwt", "access_token", "finam_token" };

                for (int i = 0; i < fields.Length; i++)
                {
                    if (doc.RootElement.TryGetProperty(fields[i], out JsonElement value))
                    {
                        string token = value.GetString();
                        if (string.IsNullOrWhiteSpace(token) == false)
                        {
                            return token;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}
