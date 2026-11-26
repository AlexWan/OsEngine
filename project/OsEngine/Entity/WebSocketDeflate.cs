/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Entity.WebSocketOsEngine
{
    /// <summary>
    /// Клиент WebSocket поверх SslStream с permessage-deflate (RFC 6455 + RFC 7692).
    ///
    /// Нужен потому, что ClientWebSocket входящие сжатые фреймы отбрасывает с WebSocketException,
    /// а для части площадок расширение уже обязательно (XT Futures: приватный канал отвечает 426
    /// «permessage-deflate is required», публичный — с 2026-12-10). Через ClientWebSocket такое
    /// подключение невозможно целиком, ниже его транспорта фреймы не видны.
    ///
    /// Разжимание входящих включается ТОЛЬКО если сервер подтвердил расширение в 101-ответе;
    /// без подтверждения соединение работает несжатым. Подтверждение без server_no_context_takeover
    /// не принимается: пословный DeflateStream при context takeover молча искажал бы данные.
    ///
    /// Исходящие сообщения никогда не сжимаются — RFC 7692 это разрешает, и XT такой обмен
    /// принимает (проверено на живом приватном канале: ping/pong проходит).
    /// </summary>
    public class WebSocketDeflate : IDisposable
    {
        #region Настройка

        public WebSocketDeflate(string url)
        {
            _url = url;
            ReadyState = WebSocketState.Closed;
        }

        /// <summary>Как в обёртке WebSocket: Connecting не выставляется.</summary>
        public WebSocketState ReadyState;

        /// <summary>Как в обёртке: входящий текст ping/Ping получает pong и не уходит в OnMessage.</summary>
        public bool EmitOnPing = false;

        public void SetProxy(IWebProxy proxy)
        {
            _proxy = proxy;
        }

        private readonly string _url;

        private string _targetHost;

        private IWebProxy _proxy;

        #endregion

        #region Константы протокола и границ

        private const string _extensionOffer = "permessage-deflate";

        private const string _webSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private const int _opcodeContinue = 0x0;
        private const int _opcodeText = 0x1;
        private const int _opcodeBinary = 0x2;
        private const int _opcodeClose = 0x8;
        private const int _opcodePing = 0x9;
        private const int _opcodePong = 0xA;

        // Таймаут handshake в документации XT не описан; 30 с — практика других коннекторов (Bybit, BCS)
        private const int _handshakeTimeoutMs = 30000;

        // Запись с таймаутом: зависший пир не должен держать лок отправителя (у XT он с TryEnter 2 с)
        private const int _writeTimeoutMs = 2000;

        private const int _keepAliveIntervalMs = 30000;

        // Мёртвый пир: сервер не ответил ни на что за это время. Протокольный ping живёт независимо от рыночных данных
        private const int _keepAliveDeadMs = 90000;

        private const int _keepAliveStepMs = 10000;

        // Один и тот же потолок для кадра, для сообщения и для результата распаковки (защита от decompression bomb)
        private const int _maxBytes = 8 * 1024 * 1024;

        private const int _maxControlBytes = 125;

        private const int _maxHandshakeBytes = 16384;

        #endregion

        #region Подключение

        public async Task Connect(TimeSpan? timeout = null, HttpClient httpClient = null)
        {
            try
            {
                if (string.IsNullOrEmpty(_url))
                {
                    throw new InvalidOperationException("URL must be set before connecting.");
                }

                Uri uri = new Uri(_url);
                _targetHost = uri.Host;

                lock (_stateLocker)
                {
                    CancelCtsLocked();
                    _cts = new CancellationTokenSource();
                }

                using (CancellationTokenSource timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromMilliseconds(_handshakeTimeoutMs)))
                using (CancellationTokenSource handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token))
                {
                    Stream transport = await OpenTransportAsync(uri, handshakeCts.Token);

                    try
                    {
                        await HandshakeAsync(transport, uri, handshakeCts.Token);
                    }
                    catch
                    {
                        DisposeQuietly(transport);
                        throw;
                    }

                    _stream = transport;
                }

                Interlocked.Exchange(ref _lastIncomingTicks, DateTime.UtcNow.Ticks);
                ReadyState = WebSocketState.Open;

                if (OnOpen != null)
                {
                    OnOpen(this, EventArgs.Empty);
                }

                CancellationToken token = _cts.Token;
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_stream, token));
                _keepAliveTask = Task.Run(() => KeepAliveLoopAsync(token));
            }
            catch (Exception ex)
            {
                // Как в обёртке: ошибка в OnError, состояние Closed. Коллер сам решает, считать ли это отказом
                ReadyState = WebSocketState.Closed;
                CloseTransport();
                RaiseError(ex);
            }
        }

        public void ConnectAsync(TimeSpan? timeout = null, HttpClient httpClient = null)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Connect(timeout, httpClient);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private async Task<Stream> OpenTransportAsync(Uri uri, CancellationToken token)
        {
            int targetPort = uri.Port > 0 ? uri.Port : 443;

            Uri proxyUri = _proxy == null ? null : _proxy.GetProxy(uri);

            // Bypass или прокси не задан → идём напрямую
            bool direct = proxyUri == null
                || (proxyUri.Port == uri.Port
                    && string.Equals(proxyUri.Host, uri.Host, StringComparison.OrdinalIgnoreCase));

            if (direct == false
                && proxyUri.Scheme != Uri.UriSchemeHttp
                && proxyUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new NotSupportedException($"XT WebSocket: proxy scheme '{proxyUri.Scheme}' is not supported, only HTTP CONNECT");
            }

            string connectHost = direct ? _targetHost : proxyUri.Host;
            int connectPort = direct ? targetPort : (proxyUri.Port > 0 ? proxyUri.Port : 80);

            TcpClient tcp = new TcpClient();
            tcp.SendTimeout = _writeTimeoutMs;

            try
            {
                await tcp.ConnectAsync(connectHost, connectPort).WaitAsync(token);

                Stream stream = tcp.GetStream();

                if (direct == false)
                {
                    stream = await OpenProxyTunnelAsync(proxyUri, uri, targetPort, stream, token);
                }

                _tcp = tcp;

                // Валидация сертификата стандартная: цепочка и имя по _targetHost, кастомных callbacks нет
                SslStream ssl = new SslStream(stream, false);
                ssl.WriteTimeout = _writeTimeoutMs;

                SslClientAuthenticationOptions options = new SslClientAuthenticationOptions();
                options.TargetHost = _targetHost;
                options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

                await ssl.AuthenticateAsClientAsync(options).WaitAsync(token);

                return ssl;
            }
            catch
            {
                DisposeQuietly(tcp);
                throw;
            }
        }

        /// <summary>
        /// Туннель HTTP CONNECT. shortcut: поддерживается только http/https прокси; для остальных схем
        /// соединение не открывается вовсе, а не идёт напрямую. Поддержка SOCKS — по запросу.
        /// </summary>
        private async Task<Stream> OpenProxyTunnelAsync(Uri proxyUri, Uri uri, int targetPort, Stream stream, CancellationToken token)
        {
            StringBuilder request = new StringBuilder();
            request.Append($"CONNECT {_targetHost}:{targetPort} HTTP/1.1\r\n");
            request.Append($"Host: {_targetHost}:{targetPort}\r\n");

            NetworkCredential credential = GetProxyCredential(uri);

            if (credential != null && string.IsNullOrEmpty(credential.UserName) == false)
            {
                request.Append($"Proxy-Authorization: Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(credential.UserName + ":" + credential.Password))}\r\n");
            }

            request.Append("\r\n");

            byte[] requestBytes = Encoding.UTF8.GetBytes(request.ToString());
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length, token);
            await stream.FlushAsync(token);

            // после ответа на CONNECT до нашего ClientHello данных быть не может, поэтому лишнее чтение здесь безопасно
            string status = await ReadHandshakeHeadAsync(stream, token);

            if (status.StartsWith("HTTP/1.1 200", StringComparison.OrdinalIgnoreCase) == false
                && status.StartsWith("HTTP/1.0 200", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new IOException($"XT WebSocket: proxy {proxyUri.Host} refused CONNECT, response head: {TrimHead(status)}");
            }

            return stream;
        }

        /// <summary>
        /// Учётные данные прокси для Proxy-Authorization. IWebProxyCredentials отдаёт их только
        /// через GetCredential по схеме, поэтому пробуем настроенные нами же схемы и прямой NetworkCredential.
        /// </summary>
        private NetworkCredential GetProxyCredential(Uri uri)
        {
            ICredentials credentials = _proxy.Credentials;

            if (credentials == null)
            {
                return null;
            }

            if (credentials is NetworkCredential direct)
            {
                return direct;
            }

            return credentials.GetCredential(uri, "PreAuthenticate") ?? credentials.GetCredential(uri, "Basic");
        }

        #endregion

        #region Handshake

        private async Task HandshakeAsync(Stream stream, Uri uri, CancellationToken token)
        {
            string path = uri.PathAndQuery;

            if (string.IsNullOrEmpty(path))
            {
                path = "/";
            }

            byte[] keyBytes = RandomNumberGenerator.GetBytes(16);
            string secKey = Convert.ToBase64String(keyBytes);

            StringBuilder request = new StringBuilder();
            request.Append($"GET {path} HTTP/1.1\r\n");
            request.Append($"Host: {_targetHost}{PortSuffix(uri)}\r\n");
            request.Append("Upgrade: websocket\r\n");
            request.Append("Connection: Upgrade\r\n");
            request.Append($"Sec-WebSocket-Key: {secKey}\r\n");
            request.Append("Sec-WebSocket-Version: 13\r\n");
            request.Append($"Sec-WebSocket-Extensions: {_extensionOffer}\r\n");
            request.Append("\r\n");

            byte[] requestBytes = Encoding.UTF8.GetBytes(request.ToString());
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length, token);
            await stream.FlushAsync(token);

            string all = await ReadHandshakeHeadAsync(stream, token);
            int headEnd = all.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string head = headEnd >= 0 ? all.Substring(0, headEnd) : all;
            string afterHead = headEnd >= 0 ? all.Substring(headEnd + 4) : "";

            string[] lines = head.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            int status = ParseStatusCode(lines.Length > 0 ? lines[0] : "");

            if (status != 101)
            {
                string body = string.IsNullOrEmpty(afterHead)
                    ? await ReadBodyFragmentAsync(stream, token)
                    : TrimHead(afterHead);

                throw new WebSocketException($"XT WebSocket: server returned status code '{status}' when status code '101' was expected. Body: {body}");
            }

            string upgrade = FindHeader(lines, "Upgrade");
            string connection = FindHeader(lines, "Connection");
            string accept = FindHeader(lines, "Sec-WebSocket-Accept");
            string extensions = FindHeader(lines, "Sec-WebSocket-Extensions");

            if (upgrade == null
                || string.Equals(upgrade.Trim(), "websocket", StringComparison.OrdinalIgnoreCase) == false)
            {
                throw new WebSocketException($"XT WebSocket: 101 without header 'Upgrade: websocket'. Head: {TrimHead(head)}");
            }

            if (connection == null
                || connection.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new WebSocketException($"XT WebSocket: 101 without header 'Connection: upgrade'. Head: {TrimHead(head)}");
            }

            string expectedAccept = ComputeAccept(secKey);

            if (string.Equals((accept ?? "").Trim(), expectedAccept, StringComparison.Ordinal) == false)
            {
                throw new WebSocketException($"XT WebSocket: invalid Sec-WebSocket-Accept header. Head: {TrimHead(head)}");
            }

            bool offered = extensions != null && extensions.IndexOf(_extensionOffer, StringComparison.OrdinalIgnoreCase) >= 0;

            if (offered
                && extensions.IndexOf("server_no_context_takeover", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // fail-closed: с context takeover нужен словарь на всё соединение, а пословный inflate тихо исказил бы данные
                throw new WebSocketException($"XT WebSocket: server confirmed permessage-deflate without server_no_context_takeover. Head: {TrimHead(head)}");
            }

            if (string.IsNullOrEmpty(afterHead) == false)
            {
                // до подписки XT ничего не шлёт; молча выбросить кадр = тихая порча данных, поэтому громко
                throw new WebSocketException($"XT WebSocket: server sent data together with the 101 response: {TrimHead(afterHead)}");
            }

            _incomingCompressed = offered;
        }

        private static string PortSuffix(Uri uri)
        {
            bool defaultPort = (uri.Scheme == Uri.UriSchemeWss && uri.Port == 443)
                || (uri.Scheme == Uri.UriSchemeWs && uri.Port == 80);

            if (defaultPort || uri.Port <= 0)
            {
                return "";
            }

            return ":" + uri.Port;
        }

        private static int ParseStatusCode(string statusLine)
        {
            string[] parts = statusLine.Split(' ');

            if (parts.Length < 2)
            {
                return -1;
            }

            int code;

            return int.TryParse(parts[1], out code) ? code : -1;
        }

        private static string FindHeader(string[] lines, string name)
        {
            for (int i = 1; i < lines.Length; i++)
            {
                int separator = lines[i].IndexOf(':');

                if (separator <= 0)
                {
                    continue;
                }

                if (string.Equals(lines[i].Substring(0, separator).Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return lines[i].Substring(separator + 1);
                }
            }

            return null;
        }

        private static string ComputeAccept(string secKey)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                return Convert.ToBase64String(sha1.ComputeHash(Encoding.ASCII.GetBytes(secKey + _webSocketGuid)));
            }
        }

        /// <summary>
        /// Всё, что сервер прислал на handshake, включая байты после пустой строки:
        /// они могут быть телом отказа или, наоборот, раньше времени присланным кадром.
        /// </summary>
        private static async Task<string> ReadHandshakeHeadAsync(Stream stream, CancellationToken token)
        {
            byte[] buffer = new byte[2048];
            StringBuilder text = new StringBuilder();
            int total = 0;

            while (true)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);

                if (read == 0)
                {
                    break;
                }

                total += read;

                if (total > _maxHandshakeBytes)
                {
                    throw new WebSocketException("XT WebSocket: handshake headers exceed 16 KB");
                }

                text.Append(Encoding.UTF8.GetString(buffer, 0, read));

                if (text.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0)
                {
                    break;
                }
            }

            return text.ToString();
        }

        private static async Task<string> ReadBodyFragmentAsync(Stream stream, CancellationToken token)
        {
            try
            {
                byte[] buffer = new byte[512];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);

                if (read <= 0)
                {
                    return "";
                }

                return TrimHead(Encoding.UTF8.GetString(buffer, 0, read));
            }
            catch (Exception ex) when (ex is IOException || ex is OperationCanceledException)
            {
                // тело отказ уже не важен — в исключении есть код статуса
                return "";
            }
        }

        private static string TrimHead(string head)
        {
            if (string.IsNullOrEmpty(head))
            {
                return "";
            }

            string one = head.Replace("\r", " ").Replace("\n", " ").Trim();

            return one.Length <= 300 ? one : one.Substring(0, 300);
        }

        #endregion

        #region Приём и разбор фреймов

        private async Task ReceiveLoopAsync(Stream stream, CancellationToken token)
        {
            MemoryStream message = null;
            bool messageCompressed = false;
            int messageOpcode = -1;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    ReceivedFrame frame = await ReadFrameAsync(stream, token);

                    Interlocked.Exchange(ref _lastIncomingTicks, DateTime.UtcNow.Ticks);

                    if (frame.Opcode == _opcodePing)
                    {
                        // pong обязателен и немедлен, payload копируется как есть
                        SendFrame(_opcodePong, frame.Payload, true);
                        continue;
                    }

                    if (frame.Opcode == _opcodePong)
                    {
                        continue;
                    }

                    if (frame.Opcode == _opcodeClose)
                    {
                        int code = frame.Payload.Length >= 2 ? (frame.Payload[0] << 8) | frame.Payload[1] : 0;
                        string reason = frame.Payload.Length > 2 ? Encoding.UTF8.GetString(frame.Payload, 2, frame.Payload.Length - 2) : "";

                        SendFrame(_opcodeClose, new byte[0], true);
                        ReadyState = WebSocketState.Closed;

                        CloseEventArgs close = new CloseEventArgs();
                        close.Code = code == 0 ? "" : code.ToString();
                        close.Reason = reason;

                        message = null;

                        if (OnClose != null)
                        {
                            OnClose(this, close);
                        }

                        return;
                    }

                    if (frame.Opcode == _opcodeContinue)
                    {
                        if (message == null)
                        {
                            throw new WebSocketException("XT WebSocket: continuation frame without start of message");
                        }

                        if (frame.Rsv1 || frame.RsvOther)
                        {
                            throw new WebSocketException("XT WebSocket: RSV bits on continuation frame");
                        }
                    }
                    else
                    {
                        if (message != null)
                        {
                            throw new WebSocketException("XT WebSocket: new data frame inside unfinished message");
                        }

                        if (frame.RsvOther)
                        {
                            throw new WebSocketException("XT WebSocket: unsupported RSV bits");
                        }

                        if (frame.Rsv1 && _incomingCompressed == false)
                        {
                            throw new WebSocketException("XT WebSocket: compressed frame but permessage-deflate was not negotiated");
                        }

                        message = new MemoryStream();
                        messageCompressed = frame.Rsv1;
                        messageOpcode = frame.Opcode;
                    }

                    if (message.Length + frame.Payload.Length > _maxBytes)
                    {
                        throw new WebSocketException($"XT WebSocket: message exceeds {_maxBytes} bytes");
                    }

                    message.Write(frame.Payload, 0, frame.Payload.Length);

                    if (frame.Fin == false)
                    {
                        continue;
                    }

                    byte[] inflated = messageCompressed
                        ? Inflate(message.GetBuffer(), (int)message.Length)
                        : message.ToArray();

                    int deliveredOpcode = messageOpcode;

                    message.Dispose();
                    message = null;
                    messageOpcode = -1;

                    await DeliverAsync(inflated, deliveredOpcode == _opcodeText, token);
                }
            }
            catch (OperationCanceledException)
            {
                // штатное закрытие
            }
            catch (Exception ex)
            {
                ReadyState = WebSocketState.Closed;
                RaiseError(ex);
            }
            finally
            {
                ReadyState = WebSocketState.Closed;

                if (message != null)
                {
                    DisposeQuietly(message);
                }

                // Как в обёртке: любое завершение цикла, кроме явного закрытия, даёт OnClose с кодом Finally
                if (token.IsCancellationRequested == false)
                {
                    CloseEventArgs close = new CloseEventArgs();
                    close.Code = "Finally socket closed";

                    if (OnClose != null)
                    {
                        OnClose(this, close);
                    }
                }
            }
        }

        private async Task DeliverAsync(byte[] payload, bool isText, CancellationToken token)
        {
            if (isText)
            {
                string text = Encoding.UTF8.GetString(payload);

                if (EmitOnPing == true
                    && (text == "ping" || text == "Ping"))
                {
                    await Send("pong");
                    return;
                }

                MessageEventArgs message = new MessageEventArgs();
                message.Data = text;
                message.IsText = true;

                if (OnMessage != null)
                {
                    OnMessage(this, message);
                }
            }
            else
            {
                MessageEventArgs message = new MessageEventArgs();
                message.RawData = payload;
                message.IsBinary = true;

                if (OnMessage != null)
                {
                    OnMessage(this, message);
                }
            }
        }

        private struct ReceivedFrame
        {
            public bool Fin;
            public bool Rsv1;
            public bool RsvOther;
            public int Opcode;
            public byte[] Payload;
        }

        private static async Task<ReceivedFrame> ReadFrameAsync(Stream stream, CancellationToken token)
        {
            byte[] head = new byte[2];
            await stream.ReadExactlyAsync(new Memory<byte>(head), token);

            ReceivedFrame frame = new ReceivedFrame();
            frame.Fin = (head[0] & 0x80) != 0;
            frame.Rsv1 = (head[0] & 0x40) != 0;
            frame.RsvOther = (head[0] & 0x30) != 0;
            frame.Opcode = head[0] & 0x0F;

            if ((head[1] & 0x80) != 0)
            {
                throw new WebSocketException("XT WebSocket: server sent a masked frame");
            }

            long length = head[1] & 0x7F;

            if (length == 126)
            {
                byte[] two = new byte[2];
                await stream.ReadExactlyAsync(new Memory<byte>(two), token);
                length = (two[0] << 8) | two[1];

                if (length < 126)
                {
                    throw new WebSocketException("XT WebSocket: frame length not minimally encoded");
                }
            }
            else if (length == 127)
            {
                byte[] eight = new byte[8];
                await stream.ReadExactlyAsync(new Memory<byte>(eight), token);
                length = 0;

                for (int i = 0; i < 8; i++)
                {
                    length = (length << 8) | eight[i];
                }

                if (length < 65536)
                {
                    throw new WebSocketException("XT WebSocket: frame length not minimally encoded");
                }
            }

            bool isControl = frame.Opcode >= _opcodeClose;

            if (isControl)
            {
                if (length > _maxControlBytes)
                {
                    throw new WebSocketException($"XT WebSocket: control frame payload of {length} bytes");
                }

                if (frame.Fin == false)
                {
                    throw new WebSocketException("XT WebSocket: fragmented control frame");
                }
            }

            if (length > _maxBytes)
            {
                throw new WebSocketException($"XT WebSocket: frame of {length} bytes exceeds the limit");
            }

            if (frame.Opcode >= 0x3 && frame.Opcode <= 0x7)
            {
                throw new WebSocketException($"XT WebSocket: unknown opcode {frame.Opcode}");
            }

            if (frame.Opcode == 0xB || frame.Opcode == 0xC || frame.Opcode == 0xF)
            {
                throw new WebSocketException($"XT WebSocket: reserved opcode {frame.Opcode}");
            }

            frame.Payload = new byte[length];

            if (length > 0)
            {
                await stream.ReadExactlyAsync(new Memory<byte>(frame.Payload), token);
            }

            if (frame.Opcode == _opcodeClose && length == 1)
            {
                throw new WebSocketException("XT WebSocket: close frame with payload of 1 byte");
            }

            return frame;
        }

        /// <summary>
        /// RFC 7692: входящее сообщение — сырой DEFLATE, оканчивающийся маркером 00 00 FF FF.
        /// Маркер не вырезается: для DEFLATE-декодера это пустой stored-блок, он читается на месте.
        /// Вырезать его можно было бы только с хвоста, поэтому вырезания нет вовсе — так маркер
        /// внутри потока (sync flush посреди сообщения) гарантированно не трогается.
        /// </summary>
        private static byte[] Inflate(byte[] payload, int length)
        {
            using (MemoryStream compressed = new MemoryStream(payload, 0, length, false))
            using (DeflateStream deflate = new DeflateStream(compressed, CompressionMode.Decompress))
            using (MemoryStream result = new MemoryStream())
            {
                byte[] buffer = new byte[16384];

                while (true)
                {
                    int read;

                    try
                    {
                        read = deflate.Read(buffer, 0, buffer.Length);
                    }
                    catch (InvalidDataException)
                    {
                        // поток обрывается на sync-flush без финального блока DEFLATE — это конец сообщения
                        break;
                    }

                    if (read <= 0)
                    {
                        break;
                    }

                    if (result.Length + read > _maxBytes)
                    {
                        throw new WebSocketException($"XT WebSocket: inflated message exceeds {_maxBytes} bytes");
                    }

                    result.Write(buffer, 0, read);
                }

                if (result.Length == 0 && length > 4)
                {
                    // декодер не дал ничего на заметном входе — выдавать пустоту вместо ошибки нельзя
                    throw new WebSocketException("XT WebSocket: permessage-deflate payload did not inflate");
                }

                return result.ToArray();
            }
        }

        private async Task KeepAliveLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(_keepAliveStepMs, token);

                    long idleMs = (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastIncomingTicks)) / TimeSpan.TicksPerMillisecond;

                    if (idleMs > _keepAliveDeadMs)
                    {
                        throw new WebSocketException($"XT WebSocket: no data from server for {idleMs / 1000} seconds");
                    }

                    if (idleMs > _keepAliveIntervalMs)
                    {
                        SendFrame(_opcodePing, new byte[0], true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // штатное закрытие
            }
            catch (Exception ex)
            {
                // Мёртвый пир извне невидим: только закрытие транспорта разбудит читателя в ReceiveLoopAsync
                ReadyState = WebSocketState.Closed;
                CloseTransport();
                RaiseError(ex);
            }
        }

        #endregion

        #region Отправка

        public void SendAsync(string message)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Send(message);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public Task SendSync(string message)
        {
            Send(message);
            return Task.CompletedTask;
        }

        private Task Send(string message)
        {
            if (ReadyState != WebSocketState.Open)
            {
                // как в обёртке: не Open — тихий no-op
                return Task.CompletedTask;
            }

            try
            {
                SendFrame(_opcodeText, Encoding.UTF8.GetBytes(message), true);
            }
            catch (Exception ex)
            {
                RaiseError(ex);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Кадр клиента: RSV=0 (исходящие не сжимаем), маска из CSPRNG на каждый кадр,
        /// запись под общим локом — у приватного канала несколько потоков-источников.
        /// </summary>
        private void SendFrame(int opcode, byte[] payload, bool fin)
        {
            Stream stream = _stream;

            if (stream == null)
            {
                return;
            }

            if (payload == null)
            {
                payload = new byte[0];
            }

            byte[] mask = RandomNumberGenerator.GetBytes(4);
            byte[] frame = BuildFrame(opcode, payload, fin, mask);

            lock (_sendLocker)
            {
                stream.Write(frame, 0, frame.Length);
                stream.Flush();
            }
        }

        private static byte[] BuildFrame(int opcode, byte[] payload, bool fin, byte[] mask)
        {
            int length = payload.Length;
            int headLength = length < 126 ? 2 : (length <= ushort.MaxValue ? 4 : 10);

            byte[] frame = new byte[headLength + 4 + length];
            frame[0] = (byte)((fin ? 0x80 : 0) | opcode);
            frame[1] = 0x80;

            if (length < 126)
            {
                frame[1] |= (byte)length;
            }
            else if (length <= ushort.MaxValue)
            {
                frame[1] |= 126;
                frame[2] = (byte)(length >> 8);
                frame[3] = (byte)length;
            }
            else
            {
                frame[1] |= 127;

                for (int i = 0; i < 8; i++)
                {
                    frame[2 + i] = (byte)((long)length >> (8 * (7 - i)));
                }
            }

            Buffer.BlockCopy(mask, 0, frame, headLength, 4);

            for (int i = 0; i < length; i++)
            {
                frame[headLength + 4 + i] = (byte)(payload[i] ^ mask[i & 3]);
            }

            return frame;
        }

        #endregion

        #region Закрытие и события

        public void CloseAsync()
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Close();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        private async Task Close()
        {
            try
            {
                lock (_stateLocker)
                {
                    if (_cts != null)
                    {
                        _cts.Cancel();
                    }
                }

                if (_receiveTask != null)
                {
                    try
                    {
                        await Task.WhenAny(_receiveTask, Task.Delay(TimeSpan.FromSeconds(5)));
                    }
                    catch (Exception ex) when (ex is AggregateException || ex is OperationCanceledException)
                    {
                        // закрытие идемпотентное: читатель мог умереть по сети
                    }
                }

                if (ReadyState == WebSocketState.Open)
                {
                    try
                    {
                        SendFrame(_opcodeClose, new byte[0], true);
                    }
                    catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is SocketException)
                    {
                        // транспорт уже мёртв — подтверждение close не отправить
                    }
                }
            }
            catch (Exception ex)
            {
                // закрытие не должно бросать в коллера: фиксируем причину в состоянии, коллер увидит Closed
                RaiseError(ex);
            }

            CloseTransport();
            ReadyState = WebSocketState.Closed;
        }

        private void CloseTransport()
        {
            Stream stream = _stream;
            _stream = null;

            if (stream != null)
            {
                DisposeQuietly(stream);
            }

            TcpClient tcp = _tcp;
            _tcp = null;

            if (tcp != null)
            {
                DisposeQuietly(tcp);
            }
        }

        private static void DisposeQuietly(IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is SocketException)
            {
                // освобождаем то, что открыли; повторное освобождение не ошибка
            }
        }

        private void CancelCtsLocked()
        {
            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // уже освобождён предыдущим закрытием
                }

                DisposeQuietly(_cts);
                _cts = null;
            }
        }

        public void Dispose()
        {
            try
            {
                Close().GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is SocketException || ex is InvalidOperationException)
            {
                // Dispose не бросает
            }

            CloseTransport();
        }

        private void RaiseError(Exception ex)
        {
            ErrorEventArgs eventArgs = new ErrorEventArgs();
            eventArgs.Exception = ex;

            if (OnError != null)
            {
                OnError(this, eventArgs);
            }
        }

        public event Action<object, EventArgs> OnOpen;

        public event Action<object, CloseEventArgs> OnClose;

        public event Action<object, MessageEventArgs> OnMessage;

        public event Action<object, ErrorEventArgs> OnError;

        #endregion

        #region Состояние соединения

        private readonly object _stateLocker = new object();

        private readonly object _sendLocker = new object();

        private CancellationTokenSource _cts;

        private Stream _stream;

        private TcpClient _tcp;

        private Task _receiveTask;

        private Task _keepAliveTask;

        private bool _incomingCompressed;

        private long _lastIncomingTicks;

        #endregion
    }
}
