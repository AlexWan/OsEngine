/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OsEngine.Market.Servers.Finam
{
    /// <summary>
    /// Единая точка для формата JWT (Finam): поиск строки токена, разбор <c>exp</c> из payload.
    /// </summary>
    internal static class FinamJwt
    {
        /// <summary>
        /// Три сегмента в Base64URL, первый обычно начинается с <c>eyJ</c> (JSON header). Без якорей — поиск внутри URI/HTML.
        /// </summary>
        public static readonly Regex JwtRegex = new Regex(
            @"eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+",
            RegexOptions.Compiled);

        /// <summary>
        /// Заголовок <c>Authorization: Bearer &lt;jwt&gt;</c>; группа 1 — токен.
        /// </summary>
        public static readonly Regex BearerAuthorizationJwtRegex = new Regex(
            @"Bearer\s+([A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Если <c>exp</c> не удалось прочитать — срок кэша от текущего UTC.
        /// </summary>
        public const int DefaultExpiryFallbackMinutes = 50;

        /// <summary>
        /// UTC истечения: из claim <c>exp</c> или <see cref="DateTime.UtcNow"/> + fallback минут.
        /// </summary>
        public static DateTime GetExpiryUtcOrFallback(string jwt, int fallbackMinutes = DefaultExpiryFallbackMinutes)
        {
            return TryParseExpiryUtc(jwt, out DateTime utc) ? utc : DateTime.UtcNow.AddMinutes(fallbackMinutes);
        }

        /// <summary>
        /// Читает <c>exp</c> (Unix seconds UTC) из payload после проверки формы токена.
        /// </summary>
        public static bool TryParseExpiryUtc(string jwt, out DateTime expiryUtc)
        {
            expiryUtc = default;

            if (string.IsNullOrWhiteSpace(jwt))
            {
                return false;
            }

            Match m = JwtRegex.Match(jwt.Trim());
            if (!m.Success)
            {
                return false;
            }

            string token = m.Value;
            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            try
            {
                string json = DecodeBase64UrlUtf8(parts[1]);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("exp", out JsonElement expEl)
                        && expEl.ValueKind == JsonValueKind.Number
                        && expEl.TryGetInt64(out long expSec))
                    {
                        expiryUtc = DateTimeOffset.FromUnixTimeSeconds(expSec).UtcDateTime;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Finam JWT exp parse: {ex}");
            }

            return false;
        }

        /// <summary>
        /// Base64URL → UTF-8 JSON payload. Перед <see cref="Convert.FromBase64String"/> длина дополняется до кратной 4 (RFC 4648).
        /// </summary>
        private static string DecodeBase64UrlUtf8(string segment)
        {
            if (string.IsNullOrEmpty(segment))
            {
                throw new FormatException("JWT payload segment is empty.");
            }

            string base64 = segment.Replace('-', '+').Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
                case 1:
                    throw new FormatException("Invalid Base64URL segment length.");
            }

            byte[] bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
