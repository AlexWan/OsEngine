/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using Google.Protobuf.WellKnownTypes;
using Google.Type;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Tradeapi.V1;
using Grpc.Tradeapi.V1.Accounts;
using Grpc.Tradeapi.V1.Assets;
using Grpc.Tradeapi.V1.Auth;
using Grpc.Tradeapi.V1.Marketdata;
using Grpc.Tradeapi.V1.Orders;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.FinamGrpc.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Candle = OsEngine.Entity.Candle;
using DateTime = System.DateTime;
using FAsset = Grpc.Tradeapi.V1.Assets.Asset;
using FOrder = Grpc.Tradeapi.V1.Orders.Order;
using FPosition = Grpc.Tradeapi.V1.Accounts.Position;
using FSide = Grpc.Tradeapi.V1.Side;
using FTimeFrame = Grpc.Tradeapi.V1.Marketdata.TimeFrame;
using FTrade = Grpc.Tradeapi.V1.Marketdata.Trade;
using GDecimal = Google.Type.Decimal;
using Order = OsEngine.Entity.Order;
using Portfolio = OsEngine.Entity.Portfolio;
using Security = OsEngine.Entity.Security;
using Side = OsEngine.Entity.Side;
using TimeFrame = OsEngine.Entity.TimeFrame;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers.FinamGrpc
{
    public class FinamGrpcServer : AServer
    {
        public FinamGrpcServer(int uniqueId)
        {
            ServerNum = uniqueId;

            _realization = new FinamGrpcServerRealization(uniqueId);
            ServerRealization = _realization;

            CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");

            ServerParameterButton marketsButton = CreateParameterButton(OsLocalization.ConvertToLocString(
                "Eng:Markets and security classes_Ru:Рынки и классы бумаг_"));
            marketsButton.UserClickButton += MarketsButton_UserClickButton;

            CreateParameterBoolean(OsLocalization.Market.IgnoreMorningAuctionTrades, false);
            CreateParameterBoolean(OsLocalization.Market.FullLogConnector, false);
        }

        private FinamGrpcServerRealization _realization;

        private FinamGrpcMarketsUi _marketsUi;

        private void MarketsButton_UserClickButton()
        {
            try
            {
                if (_marketsUi != null)
                {
                    _marketsUi.Activate();
                    return;
                }

                _marketsUi = new FinamGrpcMarketsUi(_realization.GetEnabledPairsString());
                _marketsUi.AcceptEvent += MarketsUi_AcceptEvent;
                _marketsUi.Closed += MarketsUi_Closed;
                _marketsUi.Show();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void MarketsUi_AcceptEvent(string pairs)
        {
            try
            {
                _realization.SaveEnabledPairs(pairs);
                _realization.RequestSecuritiesUpdate();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void MarketsUi_Closed(object sender, EventArgs e)
        {
            try
            {
                if (_marketsUi != null)
                {
                    _marketsUi.AcceptEvent -= MarketsUi_AcceptEvent;
                    _marketsUi.Closed -= MarketsUi_Closed;
                    _marketsUi = null;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }

    public class FinamGrpcServerRealization : IServerRealization
    {
        #region Constructor, Status, Connection

        public FinamGrpcServerRealization(int uniqueId)
        {
            _uniquePrefix = "f" + uniqueId.ToString("x8", CultureInfo.InvariantCulture);
            ServerTime = DateTime.UtcNow;

            StartWorker(StreamRecoverySupervisor, "StreamRecoveryFinamGrpc" + uniqueId);
            StartWorker(ClockWorker, "ClockFinamGrpc" + uniqueId);
            StartWorker(PortfolioWorker, "PortfoliosFinamGrpc" + uniqueId);
            StartWorker(TokenFallbackWorker, "TokenFallbackFinamGrpc" + uniqueId);
            StartWorker(IssLoaderThread, "IssLoaderFinamGrpc" + uniqueId);
            StartWorker(EnrichmentWarmerThread, "EnrichmentWarmerFinamGrpc" + uniqueId);
            StartWorker(ReconciliationWorker, "ReconciliationFinamGrpc" + uniqueId);
        }

        public void Connect(WebProxy proxy)
        {
            lock (_connectionLocker)
            {
                try
                {
                    CloseSession();
                    ServerStatus = ServerConnectStatus.Disconnect;

                    _secret = ((ServerParameterPassword)ServerParameters[0]).Value;

                    if (string.IsNullOrWhiteSpace(_secret))
                    {
                        SendLogMessage("Finam API token is required.", LogMessageType.Error);
                        return;
                    }

                    CreateChannel(proxy);
                    _proxy = proxy;
                    AuthenticateInitial();

                    if (_accountIds.Count == 0)
                    {
                        throw new InvalidOperationException("The Finam token has no selected trading accounts.");
                    }

                    ValidateAccounts();
                    LoadEnrichmentCache();
                    long generation = _generation;

                    if (StartStream(FinamStreamKind.JwtRenewal, "jwt", null, null) == false)
                    {
                        throw new InvalidOperationException("Finam JWT renewal stream creation failed.");
                    }

                    for (int i = 0; i < _accountIds.Count; i++)
                    {
                        string accountId = _accountIds[i];

                        if (StartStream(FinamStreamKind.Account, accountId, null, accountId) == false
                            || StartStream(FinamStreamKind.Orders, accountId, null, accountId) == false
                            || StartStream(FinamStreamKind.Trades, accountId, null, accountId) == false)
                        {
                            throw new InvalidOperationException("Finam private stream creation failed for the selected account.");
                        }
                    }

                    if (generation != _generation || _rootCancellation == null)
                    {
                        throw new InvalidOperationException("Finam connection session changed during activation.");
                    }

                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent?.Invoke();

                    if (_quotaWarningShown == false)
                    {
                        _quotaWarningShown = true;

                        // квоты Finam привязаны к токену: несколько инстансов OsEngine на одном токене делят их между собой
                        SendLogMessage("Finam API quotas are per method (200 req/min) and shared by token. Multiple OsEngine instances on one token share the quota; use separate tokens if you hit 429 errors.", LogMessageType.System);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    CloseSession();
                }
            }
        }

        public void Dispose()
        {
            lock (_connectionLocker)
            {
                CloseSession();
            }
        }

        private void FinalCleanup()
        {
            lock (_connectionLocker)
            {
                _finalCleanupCompleted = true;
                CloseSession();
            }

            _workersStopped.Set();
            _streamWake.Set();
            _reconciliationWake.Set();

            bool allWorkersStopped = true;

            for (int i = 0; i < _workerThreads.Count; i++)
            {
                if (_workerThreads[i].IsAlive && _workerThreads[i].Join(TimeSpan.FromSeconds(2)) == false)
                {
                    allWorkersStopped = false;
                }
            }

            lock (_dedupLocker)
            {
                _publicTradeDedup.Clear();
                _privateTradeDedup.Clear();
                _privateTradeStates.Clear();
                _publicTradeStates.Clear();
                _orderDedup.Clear();
                _orderProgress.Clear();
                _tradeRecoveryWatermarks.Clear();
                _privateRecoveryFailures.Clear();
            }

            lock (_orderMappingLocker)
            {
                _numberUserByClientId.Clear();
                _clientIdByNumberUser.Clear();
                _orderTimeInForce.Clear();
                _orderTypes.Clear();
            }

            lock (_quoteVolumeLocker)
            {
                _quoteVolumeLastSent.Clear();
            }

            lock (_securitiesLocker)
            {
                _allSecurities.Clear();
                _securities.Clear();
                _securityAliases.Clear();
                _securityByNameClass.Clear();
                _securityAssetAliases.Clear();
            }

            lock (_enrichLocker)
            {
                _securityEnrichLocks.Clear();
                _priceTypes.Clear();
                _enrichedSecurities.Clear();
            }

            if (allWorkersStopped)
            {
                foreach (KeyValuePair<string, RateGate> pair in _rateGates)
                {
                    pair.Value.Dispose();
                }

                _rateGates.Clear();
                _streamWake.Dispose();
                _warmerWake.Dispose();
                _reconciliationWake.Dispose();
                _enrichmentSemaphore.Dispose();
                _workersStopped.Dispose();
            }
            else
            {
                SendLogMessage("Finam workers did not stop within the final cleanup timeout.", LogMessageType.System);
            }

            _secret = "";
        }

        private void CloseSession()
        {
            List<FinamStreamLease> leases = new List<FinamStreamLease>();
            CancellationTokenSource root;
            GrpcChannel channel;
            HttpClient httpClient;

            lock (_sessionLocker)
            {
                _generation++;
                root = _rootCancellation;
                _rootCancellation = null;
                channel = _channel;
                _channel = null;
                httpClient = _httpClient;
                _httpClient = null;

                // клиенты не обнуляем: залётный вызов во время реконнекта получит gRPC-ошибку
                // от disposed-канала (ловится и логируется), а не NRE; результаты старой сессии
                // всё равно отбрасываются проверками генерации
            }

            lock (_streamLocker)
            {
                foreach (KeyValuePair<string, FinamStreamLease> pair in _streams)
                {
                    leases.Add(pair.Value);
                }

                _streams.Clear();
            }

            if (root != null)
            {
                try
                {
                    root.Cancel();
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }

            for (int i = 0; i < leases.Count; i++)
            {
                CleanupLease(leases[i]);
            }

            if (root != null)
            {
                root.Dispose();
            }

            channel?.Dispose();
            httpClient?.Dispose();

            lock (_tokenLocker)
            {
                _jwt = "";
                _tokenExpiryUtc = DateTime.MinValue;
                _readOnly = false;
                _mdPermissions = new List<MDPermission>();
            }

            _proxy = null;

            lock (_streamLocker)
            {
                _subscribedSecurities.Clear();
                _quoteSyncDirty = false;
                _quoteActiveSignature = null;
            }

            lock (_reconciliationLocker)
            {
                _reconciliationQueue.Clear();
                _reconciliationPending.Clear();
            }

            _orderBooks.Clear();

            // ордерный дедуп сбрасываем при реконнекте: реконсиляция должна переэмитить активные ордера,
            // иначе движок их не переузнает (тест O8/O9, «ORDER LOST после 5 запросов»)
            lock (_dedupLocker)
            {
                _orderDedup.Clear();
                _orderProgress.Clear();
                _privateRecoveryFailures.Clear();
            }

            lock (_enrichLocker)
            {
                if (_enrichmentCacheDirty)
                {
                    SaveEnrichmentCache();
                }
            }

            lock (_depthTimeLocker)
            {
                _lastDepthTimes.Clear();
            }

            lock (_portfolioLocker)
            {
                _portfolios.Clear();
                _loggedMctAccounts.Clear();
            }

            _accountIds = new List<string>();
            SetDisconnected();
        }

        private void SetDisconnected()
        {
            lock (_connectionLocker)
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent?.Invoke();
                }
            }
        }

        private void ClockWorker()
        {
            try
            {
                while (IsCompletelyDeleted == false)
                {
                    try
                    {
                        if (_workersStopped.WaitOne(15000))
                        {
                            break;
                        }

                        if (ServerStatus != ServerConnectStatus.Connect || _assetsClient == null)
                        {
                            continue;
                        }

                        WaitRateGate("Clock");
                        ClockResponse response = _assetsClient.Clock(new ClockRequest(), CreateMetadata(), DateTime.UtcNow.AddSeconds(30));
                        ServerTime = ToMoscow(response.Timestamp);

                        SweepFinalizedOrderMappings();
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        private bool _isCompletelyDeleted;

        private bool _finalCleanupCompleted;

        private bool _quotaWarningShown;

        private readonly List<Thread> _workerThreads = new List<Thread>();

        private readonly object _connectionLocker = new object();

        private readonly object _sessionLocker = new object();

        private readonly ManualResetEvent _workersStopped = new ManualResetEvent(false);

        private long _generation;

        private CancellationTokenSource _rootCancellation;

        #endregion

        #region Properties

        public ServerType ServerType => ServerType.FinamGrpc;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public DateTime ServerTime { get; set; }

        public List<IServerParameter> ServerParameters { get; set; }

        public bool IsCompletelyDeleted
        {
            get { return _isCompletelyDeleted; }
            set
            {
                _isCompletelyDeleted = value;

                if (value && _finalCleanupCompleted == false)
                {
                    FinalCleanup();
                }
            }
        }

        #endregion

        #region Securities

        public event Action<List<Security>> SecurityEvent;

        public void GetSecurities()
        {
            try
            {
                List<Security> securities;
                long generation;

                lock (_securitiesLoadLocker)
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        return;
                    }

                    generation = _generation;

                    bool needReload;

                    lock (_securitiesLocker)
                    {
                        // список качаем один раз за сессию; смена галок классов — только перефильтрация в памяти
                        needReload = _allSecurities.Count == 0;
                    }

                    if (needReload)
                    {
                        try
                        {
                            ReloadSecuritiesSnapshot(generation);
                        }
                        catch (Exception error)
                        {
                            SendLogMessage(error.ToString(), LogMessageType.Error);

                            lock (_securitiesLocker)
                            {
                                if (_allSecurities.Count == 0)
                                {
                                    return;
                                }
                            }
                        }
                    }

                    securities = ApplySecurityFilters();
                }

                if (generation != _generation)
                {
                    return;
                }

                SecurityEvent?.Invoke(new List<Security>(securities));
                _warmerWake.Set();

                if (TryReserveInitialPrivateDataReconciliation(generation))
                {
                    ReconcileInitialPrivateData();
                }

                SendLogMessage("Finam securities loaded. Count: " + securities.Count, LogMessageType.System);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void RequestSecuritiesUpdate()
        {
            if (IsCompletelyDeleted == false)
            {
                _warmerWake.Set();
                GetSecurities();
            }
        }

        private void ReloadSecuritiesSnapshot(long generation)
        {
            List<Security> snapshot = new List<Security>();
            Dictionary<string, List<string>> assetAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            WaitRateGate("Assets");

            // Assets отдаёт весь список торгуемых инструментов одним ответом,
            // AllAssets с курсорной пагинацией заметно медленнее и нужен только для архивных бумаг
#pragma warning disable CS0612
            AssetsResponse response = _assetsClient.Assets(new AssetsRequest(), CreateMetadata(), DateTime.UtcNow.AddSeconds(30));
#pragma warning restore CS0612

            for (int i = 0; i < response.Assets.Count; i++)
            {
                if (generation != _generation)
                {
                    return;
                }

                FAsset asset = response.Assets[i];
                Security security = ConvertSecurity(asset);

                // снапшот содержит все классы; фильтрация по галкам — в ApplySecurityFilters, без сети
                if (security == null)
                {
                    continue;
                }

                snapshot.Add(security);
                List<string> aliases = new List<string>();

                if (string.IsNullOrEmpty(asset.Id) == false)
                {
                    aliases.Add(asset.Id);
                }

                if (string.IsNullOrEmpty(asset.Isin) == false)
                {
                    aliases.Add(asset.Isin);
                }

                assetAliases[security.NameId] = aliases;
            }

            lock (_securitiesLocker)
            {
                if (generation != _generation || ServerStatus != ServerConnectStatus.Connect)
                {
                    return;
                }

                _allSecurities = snapshot;
                _securityAssetAliases = assetAliases;
            }
        }

        private List<Security> ApplySecurityFilters()
        {
            lock (_securitiesLocker)
            {
                HashSet<string> enabledPairs = GetEnabledPairs();
                List<Security> securities = new List<Security>();
                Dictionary<string, Security> aliases = new Dictionary<string, Security>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, Security> byNameClass = new Dictionary<string, Security>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < _allSecurities.Count; i++)
                {
                    Security security = _allSecurities[i];
                    aliases[security.NameId] = security;
                    AddSecurityAlias(aliases, security.Name, security);
                    AddSecurityAlias(aliases, BareSymbol(security.NameId), security);
                    AddSecurityPair(byNameClass, security);
                    List<string> assetAliases;

                    if (_securityAssetAliases.TryGetValue(security.NameId, out assetAliases))
                    {
                        for (int j = 0; j < assetAliases.Count; j++)
                        {
                            AddSecurityAlias(aliases, assetAliases[j], security);
                        }
                    }

                    if (IsEnabledPair(security, enabledPairs))
                    {
                        securities.Add(security);
                    }
                }

                _securities = securities;
                _securityAliases = aliases;
                _securityByNameClass = byNameClass;
                return new List<Security>(securities);
            }
        }

        private void AddSecurityPair(Dictionary<string, Security> byNameClass, Security security)
        {
            if (string.IsNullOrEmpty(security.Name) || string.IsNullOrEmpty(security.NameClass))
            {
                return;
            }

            string pairKey = security.Name + "|" + security.NameClass;
            Security existing;

            if (byNameClass.TryGetValue(pairKey, out existing) == false)
            {
                byNameClass.Add(pairKey, security);
                return;
            }

            if (existing.NameId != security.NameId
                && GetMicPriority(security.NameId) < GetMicPriority(existing.NameId))
            {
                byNameClass[pairKey] = security;
            }
        }

        public const string DefaultEnabledPairs = "MISX|Stock;RTSX|Futures";

        private readonly string _marketsSettingsPath = @"Engine\FinamGrpcMarketsSettings.txt";

        private HashSet<string> _enabledPairs;

        public string GetEnabledPairsString()
        {
            return string.Join(";", GetEnabledPairs());
        }

        public void SaveEnabledPairs(string pairs)
        {
            HashSet<string> updated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(pairs) == false)
            {
                string[] parts = pairs.Split(';');

                for (int i = 0; i < parts.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(parts[i]) == false)
                    {
                        updated.Add(parts[i].Trim());
                    }
                }
            }

            _enabledPairs = updated;

            try
            {
                File.WriteAllText(_marketsSettingsPath, pairs);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private HashSet<string> GetEnabledPairs()
        {
            if (_enabledPairs == null)
            {
                _enabledPairs = LoadEnabledPairsFromFile();
            }

            return _enabledPairs;
        }

        private HashSet<string> LoadEnabledPairsFromFile()
        {
            HashSet<string> pairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string content = File.Exists(_marketsSettingsPath) ? File.ReadAllText(_marketsSettingsPath) : DefaultEnabledPairs;
                string[] parts = content.Split(';');

                for (int i = 0; i < parts.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(parts[i]) == false)
                    {
                        pairs.Add(parts[i].Trim());
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                pairs.Add("MISX|Stock");
                pairs.Add("RTSX|Futures");
            }

            return pairs;
        }

        private bool IsEnabledPair(Security security, HashSet<string> enabledPairs)
        {
            string key = security.Exchange + "|" + security.SecurityType;
            return enabledPairs.Contains(key);
        }

        private Security ConvertSecurity(FAsset asset)
        {
            string type = (asset.Type ?? "").ToLowerInvariant();
            SecurityType securityType = SecurityType.None;

            if (type == "equities" || type == "stock")
            {
                securityType = SecurityType.Stock;
            }
            else if (type == "futures" || type == "spreads")
            {
                securityType = SecurityType.Futures;
            }
            else if (type == "currencies")
            {
                securityType = SecurityType.CurrencyPair;
            }
            else if (type == "bonds")
            {
                securityType = SecurityType.Bond;
            }
            else if (type == "funds")
            {
                securityType = SecurityType.Fund;
            }
            else if (type == "options")
            {
                securityType = SecurityType.Option;
            }
            else if (type == "indices")
            {
                securityType = SecurityType.Index;
            }

            if (securityType == SecurityType.None || string.IsNullOrEmpty(asset.Symbol))
            {
                return null;
            }

            Security security = new Security();
            security.Name = string.IsNullOrEmpty(asset.Ticker) ? BareSymbol(asset.Symbol) : asset.Ticker;
            security.NameId = asset.Symbol;
            security.NameFull = string.IsNullOrEmpty(asset.Name) ? security.Name : asset.Name;
            security.NameClass = BuildNameClass(asset);
            security.Exchange = asset.Mic;
            security.SecurityType = securityType;
            security.PriceStep = 1m;
            security.PriceStepCost = 1m;
            security.VolumeStep = 1m;
            security.Lot = 1m;
            security.MinTradeAmount = 1m;
            security.MinTradeAmountType = MinTradeAmountType.Contract;
            security.State = SecurityStateType.Activ;
            security.UsePriceStepCostToCalculateVolume = securityType == SecurityType.Futures || securityType == SecurityType.Option;
            ApplyCachedEnrichment(security);
            return security;
        }

        private string BuildNameClass(FAsset asset)
        {
            // календарные спреды — отдельный класс от обычных фьючерсов
            if (string.Equals(asset.Type, "spreads", StringComparison.OrdinalIgnoreCase))
            {
                return "Futures Spreads";
            }

            string typeName = GetSecurityTypeName(asset);
            string group = GetMarketGroup(asset.Mic);

            // иностранные акции на MOEX выделяем в отдельный класс
            if (group == "MOEX" && typeName == "Stock" && IsForeignAsset(asset))
            {
                return "Stock MOEX-F";
            }

            return typeName + " " + group;
        }

        private bool IsForeignAsset(FAsset asset)
        {
            if (string.IsNullOrEmpty(asset.Isin) == false
                && asset.Isin.StartsWith("RU", StringComparison.OrdinalIgnoreCase) == false)
            {
                return true;
            }

            if (string.IsNullOrEmpty(asset.Ticker) == false
                && asset.Ticker.EndsWith("-RM", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private string GetSecurityTypeName(FAsset asset)
        {
            string type = (asset.Type ?? "").ToLowerInvariant();

            if (type == "equities" || type == "stock")
            {
                return "Stock";
            }

            if (type == "futures" || type == "spreads")
            {
                return "Futures";
            }

            if (type == "currencies")
            {
                return "CurrencyPair";
            }

            if (type == "bonds")
            {
                return "Bond";
            }

            if (type == "funds")
            {
                return "Fund";
            }

            if (type == "options")
            {
                return "Option";
            }

            if (type == "indices")
            {
                return "Index";
            }

            return "Unknown";
        }

        private string GetMarketGroup(string mic)
        {
            if (string.Equals(mic, "MISX", StringComparison.OrdinalIgnoreCase)) return "MOEX";
            if (string.Equals(mic, "RTSX", StringComparison.OrdinalIgnoreCase)) return "RTSX";
            if (string.Equals(mic, "RUSX", StringComparison.OrdinalIgnoreCase)) return "RUSX";
            if (string.Equals(mic, "_SPBZ", StringComparison.OrdinalIgnoreCase)) return "SPB";

            // акции США
            if (string.Equals(mic, "XNYS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XNMS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "ARCX", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "BATS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XNCM", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XNAS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "IFUS", StringComparison.OrdinalIgnoreCase))
            {
                return "USA";
            }

            // товарные фьючерсы США
            if (string.Equals(mic, "XNYM", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XCME", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XCBT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XCEC", StringComparison.OrdinalIgnoreCase))
            {
                return "CME";
            }

            if (string.Equals(mic, "XETR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XGAT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XLON", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XLOM", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XPAR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XMAD", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XAMS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XBRU", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XBUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XFRA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XLDN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XLME", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "IFEU", StringComparison.OrdinalIgnoreCase))
            {
                return "Europe";
            }

            if (string.Equals(mic, "XTSE", StringComparison.OrdinalIgnoreCase)) return "Canada";
            if (string.Equals(mic, "XHKG", StringComparison.OrdinalIgnoreCase)) return "HK";

            if (string.Equals(mic, "XTKS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XSHE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XSHG", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XASX", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "XBOM", StringComparison.OrdinalIgnoreCase))
            {
                return "Asia";
            }

            if (string.Equals(mic, "_CRYP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mic, "_BNCC", StringComparison.OrdinalIgnoreCase))
            {
                return "Crypto";
            }

            // внебиржевые/служебные площадки Finam
            if (string.IsNullOrEmpty(mic) == false && (mic.StartsWith("_") || mic.StartsWith("#")))
            {
                return "OTC";
            }

            // неизвестная площадка — показываем mic как есть
            return mic;
        }

        private void EnrichSecurity(Security security, bool refilter = true)
        {
            if (security == null || string.IsNullOrEmpty(security.NameId))
            {
                return;
            }

            object symbolLock;

            lock (_enrichLocker)
            {
                if (_enrichedSecurities.Contains(security.NameId))
                {
                    return;
                }

                CachedEnrichment cached;

                // свежая запись файлового кэша от Finam уже применена в ConvertSecurity — сеть не нужна;
                // запись от ISS не считается авторитетной и не блокирует обогащение через Finam
                if (_enrichmentCache.TryGetValue(security.NameId, out cached)
                    && cached.Source == "Finam"
                    && DateTime.UtcNow - cached.UpdatedUtc < TimeSpan.FromHours(24))
                {
                    _enrichedSecurities.Add(security.NameId);
                    return;
                }

                if (_securityEnrichLocks.TryGetValue(security.NameId, out symbolLock) == false)
                {
                    symbolLock = new object();
                    _securityEnrichLocks.Add(security.NameId, symbolLock);
                }
            }

            long generation;

            lock (symbolLock)
            {
                generation = _generation;
                string accountId = _accountIds.Count == 1 ? _accountIds[0] : "";
                _enrichmentSemaphore.Wait();

                try
                {
                    WaitRateGate("GetAsset");
                    GetAssetResponse asset = _assetsClient.GetAsset(new GetAssetRequest
                    {
                        AccountId = accountId,
                        Symbol = security.NameId
                    }, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));

                    if (IsFullLog())
                    {
                        SendLogMessage("Finam GetAsset " + security.NameId + ": " + asset.ToString(), LogMessageType.System);
                    }

                    if (generation != _generation)
                    {
                        return;
                    }

                    int decimals = Math.Max(0, asset.Decimals);
                    decimal scale = DecimalPower(decimals);
                    decimal priceStep = asset.MinStep > 0 ? asset.MinStep / scale : 1m / scale;

                    if (priceStep <= 0)
                    {
                        priceStep = 1m / scale;
                    }

                    decimal lot = ToDecimal(asset.LotSize);
                    WaitRateGate("GetAssetParams");
                    GetAssetParamsResponse parameters = _assetsClient.GetAssetParams(new GetAssetParamsRequest
                    {
                        AccountId = accountId,
                        Symbol = security.NameId
                    }, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));

                    if (IsFullLog())
                    {
                        SendLogMessage("Finam GetAssetParams " + security.NameId + ": " + parameters.ToString(), LogMessageType.System);
                    }

                    if (generation != _generation)
                    {
                        return;
                    }

                    lock (_enrichLocker)
                    {
                        if (_finalCleanupCompleted)
                        {
                            return;
                        }

                        security.PriceStep = priceStep;
                        security.Decimals = decimals;
                        security.PriceStepCost = priceStep;

                        if (lot > 0)
                        {
                            security.Lot = lot;
                        }

                        if (asset.FutureDetails != null)
                        {
                            ApplyFutureDetails(security, asset.FutureDetails);
                        }
                        else if (asset.OptionDetails != null)
                        {
                            ApplyOptionDetails(security, asset.OptionDetails);
                        }
                        else if (asset.BondDetails != null)
                        {
                            decimal faceValue = ToDecimal(asset.BondDetails.BondFaceValue);

                            if (faceValue > 0)
                            {
                                security.NominalCurrent = faceValue;
                            }
                        }

                        if (parameters.IsTradable != null && parameters.IsTradable.Value == false)
                        {
                            security.State = SecurityStateType.Close;
                        }
                        else
                        {
                            security.State = SecurityStateType.Activ;
                        }

                        if (parameters.TradeLotSize > 0)
                        {
                            security.Lot = parameters.TradeLotSize;
                        }

                        // ГО по конвенции проекта только у деривативов; у акций/фондов это не ГО, а маржинальное обеспечение
                        if (security.SecurityType == SecurityType.Futures || security.SecurityType == SecurityType.Option)
                        {
                            security.MarginBuy = MoneyToDecimal(parameters.LongInitialMargin);
                            security.MarginSell = MoneyToDecimal(parameters.ShortInitialMargin);
                        }
                        else
                        {
                            security.MarginBuy = 0;
                            security.MarginSell = 0;
                        }

                        CacheEnrichment(security, parameters.PriceType);
                        _enrichedSecurities.Add(security.NameId);
                        UpdateEnrichmentCache(security, parameters.PriceType);
                    }
                }
                catch (Exception error)
                {
                    bool hasStale;

                    lock (_enrichLocker)
                    {
                        hasStale = _enrichmentCache.ContainsKey(security.NameId);

                        if (hasStale)
                        {
                            _enrichedSecurities.Add(security.NameId);
                        }
                    }

                    if (hasStale)
                    {
                        // gRPC недоступен — едем на протухшей записи из файлового кэша
                        SendLogMessage("Finam enrichment failed for " + security.NameId + ", using the stale cache record. " + error.Message, LogMessageType.System);
                    }
                    else
                    {
                        throw;
                    }
                }
                finally
                {
                    _enrichmentSemaphore.Release();
                }
            }

            if (refilter && generation == _generation)
            {
                ApplySecurityFilters();
            }
        }

        private void CacheEnrichment(Security security, PriceType priceType)
        {
            Security cachedSecurity = security;

            lock (_securitiesLocker)
            {
                for (int i = 0; i < _allSecurities.Count; i++)
                {
                    if (string.Equals(_allSecurities[i].NameId, security.NameId, StringComparison.OrdinalIgnoreCase))
                    {
                        cachedSecurity = _allSecurities[i];
                        break;
                    }
                }
            }

            CopyEnrichedFields(security, cachedSecurity);
            _priceTypes[security.NameId] = priceType;
        }

        private void CopyEnrichedFields(Security source, Security target)
        {
            if (source == null || target == null || source == target)
            {
                return;
            }

            target.PriceStep = source.PriceStep;
            target.Decimals = source.Decimals;
            target.PriceStepCost = source.PriceStepCost;
            target.Lot = source.Lot;
            target.Expiration = source.Expiration;
            target.Strike = source.Strike;
            target.NominalCurrent = source.NominalCurrent;
            target.State = source.State;
            target.MarginBuy = source.MarginBuy;
            target.MarginSell = source.MarginSell;
        }

        private void ApplyFutureDetails(Security security, GetAssetResponse.Types.FutureDetails details)
        {
            if (details.ExpirationDate != null)
            {
                security.Expiration = ToMoscow(details.ExpirationDate).Date;
            }

            decimal contractSize = ToDecimal(details.ContractSize);

            if (contractSize > 0)
            {
                security.PriceStepCost = security.PriceStep * contractSize;
            }
        }

        private void ApplyOptionDetails(Security security, GetAssetResponse.Types.OptionDetails details)
        {
            if (details.ExpirationDate != null)
            {
                security.Expiration = ToMoscow(details.ExpirationDate).Date;
            }

            security.Strike = ToDecimal(details.Strike);
            decimal contractSize = ToDecimal(details.ContractSize);

            if (contractSize > 0)
            {
                security.PriceStepCost = security.PriceStep * contractSize;
            }
        }

        private Security ResolveSecurity(string symbol, string nameClass)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return null;
            }

            Security security;

            // сначала точная пара имя+класс — однозначно различает дубли тикеров на разных площадках
            if (string.IsNullOrEmpty(nameClass) == false
                && _securityByNameClass.TryGetValue(symbol + "|" + nameClass, out security))
            {
                return security;
            }

            return ResolveSecurity(symbol);
        }

        private Security ResolveSecurity(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return null;
            }

            Security security;
            Dictionary<string, Security> aliases = _securityAliases;

            if (aliases.TryGetValue(symbol, out security))
            {
                return security;
            }

            string bare = BareSymbol(symbol);
            aliases.TryGetValue(bare, out security);
            return security;
        }

        private void AddSecurityAlias(Dictionary<string, Security> aliases, string alias, Security security)
        {
            if (string.IsNullOrEmpty(alias) || security == null)
            {
                return;
            }

            Security existing;

            if (aliases.TryGetValue(alias, out existing) == false)
            {
                aliases.Add(alias, security);
                return;
            }

            if (existing != null && existing.NameId != security.NameId)
            {
                int existingPriority = GetMicPriority(existing.NameId);
                int newPriority = GetMicPriority(security.NameId);

                if (newPriority < existingPriority)
                {
                    aliases[alias] = security;
                }
                else if (newPriority == existingPriority)
                {
                    aliases[alias] = null;
                }

                // один раз за сессию на пару, иначе спам при каждой перефильтрации
                string conflictKey = alias + "|" + existing.NameId + "|" + security.NameId;

                lock (_dedupLocker)
                {
                    if (_loggedAliasConflicts.Add(conflictKey) == false)
                    {
                        return;
                    }
                }

                SendLogMessage("Finam security alias conflict: " + alias + " (" + existing.NameId + " vs " + security.NameId + "), kept: "
                    + (newPriority < existingPriority ? security.NameId : newPriority > existingPriority ? existing.NameId : "none"), LogMessageType.System);
            }
        }

        private int GetMicPriority(string nameId)
        {
            if (nameId.EndsWith("@MISX", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (nameId.EndsWith("@RTSX", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (nameId.EndsWith("@RUSX", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (nameId.EndsWith("@_SPBZ", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            return 4;
        }

        private string BareSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return "";
            }

            int separator = symbol.IndexOf('@');
            return separator > 0 ? symbol.Substring(0, separator) : symbol;
        }

        private readonly object _securitiesLocker = new object();

        private readonly object _securitiesLoadLocker = new object();

        private readonly object _enrichLocker = new object();

        private readonly Dictionary<string, object> _securityEnrichLocks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private readonly SemaphoreSlim _enrichmentSemaphore = new SemaphoreSlim(2, 2);

        private List<Security> _allSecurities = new List<Security>();

        private Dictionary<string, List<string>> _securityAssetAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        private List<Security> _securities = new List<Security>();

        private Dictionary<string, Security> _securityAliases = new Dictionary<string, Security>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, Security> _securityByNameClass = new Dictionary<string, Security>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, PriceType> _priceTypes = new Dictionary<string, PriceType>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _enrichedSecurities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, CachedEnrichment> _enrichmentCache = new Dictionary<string, CachedEnrichment>(StringComparer.OrdinalIgnoreCase);

        private bool _enrichmentCacheDirty;

        private DateTime _enrichmentCacheLastSaveUtc = DateTime.MinValue;

        private readonly string _enrichmentCachePath = @"Engine\FinamGrpcEnrichmentCache.txt";

        private sealed class CachedEnrichment
        {
            public decimal PriceStep;
            public int Decimals;
            public decimal Lot;
            public decimal PriceStepCost;
            public DateTime Expiration;
            public decimal Strike;
            public decimal NominalCurrent;
            public decimal MarginBuy;
            public decimal MarginSell;
            public SecurityStateType State;
            public PriceType PriceType;
            public string Source;
            public DateTime UpdatedUtc;
        }

        private void LoadEnrichmentCache()
        {
            lock (_enrichLocker)
            {
                _enrichmentCache.Clear();
                _enrichmentCacheDirty = false;

                try
                {
                    if (File.Exists(_enrichmentCachePath) == false)
                    {
                        return;
                    }

                    string[] lines = File.ReadAllLines(_enrichmentCachePath);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        string[] parts = lines[i].Split('|');

                        // 13 частей — старый формат без источника, 14 — с источником
                        if (parts.Length != 13 && parts.Length != 14)
                        {
                            continue;
                        }

                        CachedEnrichment record = new CachedEnrichment();
                        record.PriceStep = parts[1].ToDecimal();
                        record.Decimals = int.Parse(parts[2], CultureInfo.InvariantCulture);
                        record.Lot = parts[3].ToDecimal();
                        record.PriceStepCost = parts[4].ToDecimal();
                        record.Expiration = new DateTime(long.Parse(parts[5], CultureInfo.InvariantCulture));
                        record.Strike = parts[6].ToDecimal();
                        record.NominalCurrent = parts[7].ToDecimal();
                        record.MarginBuy = parts[8].ToDecimal();
                        record.MarginSell = parts[9].ToDecimal();
                        System.Enum.TryParse(parts[10], out record.State);
                        System.Enum.TryParse(parts[11], out record.PriceType);
                        record.Source = parts.Length == 14 ? parts[13] : "Finam";
                        record.UpdatedUtc = new DateTime(long.Parse(parts[12], CultureInfo.InvariantCulture), DateTimeKind.Utc);
                        _enrichmentCache[parts[0]] = record;
                    }

                    SendLogMessage("Finam enrichment cache loaded. Count: " + _enrichmentCache.Count, LogMessageType.System);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    _enrichmentCache.Clear();
                }
            }
        }

        private void SaveEnrichmentCache()
        {
            try
            {
                DateTime threshold = DateTime.UtcNow.AddDays(-30);
                List<string> lines = new List<string>();

                foreach (KeyValuePair<string, CachedEnrichment> pair in _enrichmentCache)
                {
                    CachedEnrichment record = pair.Value;

                    if (record.UpdatedUtc < threshold)
                    {
                        continue;
                    }

                    lines.Add(string.Join("|",
                        pair.Key,
                        record.PriceStep.ToString(CultureInfo.InvariantCulture),
                        record.Decimals.ToString(CultureInfo.InvariantCulture),
                        record.Lot.ToString(CultureInfo.InvariantCulture),
                        record.PriceStepCost.ToString(CultureInfo.InvariantCulture),
                        record.Expiration.Ticks.ToString(CultureInfo.InvariantCulture),
                        record.Strike.ToString(CultureInfo.InvariantCulture),
                        record.NominalCurrent.ToString(CultureInfo.InvariantCulture),
                        record.MarginBuy.ToString(CultureInfo.InvariantCulture),
                        record.MarginSell.ToString(CultureInfo.InvariantCulture),
                        record.State.ToString(),
                        record.PriceType.ToString(),
                        record.UpdatedUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                        record.Source ?? "Finam"));
                }

                File.WriteAllLines(_enrichmentCachePath, lines);
                _enrichmentCacheDirty = false;
                _enrichmentCacheLastSaveUtc = DateTime.UtcNow;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ApplyCachedEnrichment(Security security)
        {
            lock (_enrichLocker)
            {
                CachedEnrichment cached;

                if (_enrichmentCache.TryGetValue(security.NameId, out cached) == false)
                {
                    return;
                }

                security.PriceStep = cached.PriceStep;
                security.Decimals = cached.Decimals;
                security.PriceStepCost = cached.PriceStepCost;

                if (cached.Lot > 0)
                {
                    security.Lot = cached.Lot;
                }

                if (cached.Expiration != DateTime.MinValue)
                {
                    security.Expiration = cached.Expiration;
                }

                security.Strike = cached.Strike;

                if (cached.NominalCurrent > 0)
                {
                    security.NominalCurrent = cached.NominalCurrent;
                }

                // ГО только у деривативов, остальным — ноль
                if (security.SecurityType == SecurityType.Futures || security.SecurityType == SecurityType.Option)
                {
                    security.MarginBuy = cached.MarginBuy;
                    security.MarginSell = cached.MarginSell;
                }
                else
                {
                    security.MarginBuy = 0;
                    security.MarginSell = 0;
                }

                security.State = cached.State;
                _priceTypes[security.NameId] = cached.PriceType;
            }
        }

        private void UpdateEnrichmentCache(Security security, PriceType priceType)
        {
            CachedEnrichment record;

            if (_enrichmentCache.TryGetValue(security.NameId, out record) == false)
            {
                record = new CachedEnrichment();
                _enrichmentCache.Add(security.NameId, record);
            }

            record.PriceStep = security.PriceStep;
            record.Decimals = security.Decimals;
            record.Lot = security.Lot;
            record.PriceStepCost = security.PriceStepCost;
            record.Expiration = security.Expiration;
            record.Strike = security.Strike;
            record.NominalCurrent = security.NominalCurrent;
            record.MarginBuy = security.MarginBuy;
            record.MarginSell = security.MarginSell;
            record.State = security.State;
            record.PriceType = priceType;
            record.Source = "Finam";
            record.UpdatedUtc = DateTime.UtcNow;
            _enrichmentCacheDirty = true;

            if (DateTime.UtcNow - _enrichmentCacheLastSaveUtc > TimeSpan.FromSeconds(60))
            {
                SaveEnrichmentCache();
            }
        }

        #endregion

        #region Portfolios

        public event Action<List<Portfolio>> PortfolioEvent;

        public void GetPortfolios()
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            List<string> accounts = new List<string>(_accountIds);
            long generation = _generation;

            for (int i = 0; i < accounts.Count; i++)
            {
                try
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    WaitRateGate("GetAccount");
                    GetAccountResponse response = _accountsClient.GetAccount(new GetAccountRequest
                    {
                        AccountId = accounts[i]
                    }, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));
                    UpdatePortfolio(response, generation);
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private void PortfolioWorker()
        {
            try
            {
                while (IsCompletelyDeleted == false)
                {
                    try
                    {
                        if (_workersStopped.WaitOne(30000))
                        {
                            break;
                        }

                        if (ServerStatus == ServerConnectStatus.Connect)
                        {
                            GetPortfolios();
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UpdatePortfolio(GetAccountResponse response, long? generation = null)
        {
            if (response == null || string.IsNullOrEmpty(response.AccountId))
            {
                return;
            }

            if (generation.HasValue && generation.Value != _generation)
            {
                return;
            }

            for (int i = 0; i < response.Positions.Count; i++)
            {
                Security security = ResolveSecurity(response.Positions[i].Symbol);

                if (security != null)
                {
                    EnrichSecurity(security);
                }
            }

            if (generation.HasValue)
            {
                lock (_sessionLocker)
                {
                    if (generation.Value != _generation || _rootCancellation == null)
                    {
                        return;
                    }

                    UpdatePortfolioSnapshot(response);
                }
            }
            else
            {
                UpdatePortfolioSnapshot(response);
            }
        }

        private void UpdatePortfolioSnapshot(GetAccountResponse response)
        {
            lock (_portfolioLocker)
            {
                Portfolio previous = _portfolios.Find(FindPortfolioPredicate(response.AccountId));
                Portfolio updated = new Portfolio();
                updated.Number = response.AccountId;
                updated.ServerType = ServerType.FinamGrpc;
                updated.ValueCurrent = ToDecimal(response.Equity);
                updated.ValueBegin = previous != null ? previous.ValueBegin : updated.ValueCurrent;
                updated.UnrealizedPnl = ToDecimal(response.UnrealizedProfit);
                updated.PositionOnBoard = new List<PositionOnBoard>();

                if (response.PortfolioMc != null)
                {
                    updated.ValueBlocked = ToDecimal(response.PortfolioMc.InitialMargin);
                    updated.ValueLiquidCash = ToDecimal(response.PortfolioMc.AvailableCash);
                }
                else if (response.PortfolioForts != null)
                {
                    updated.ValueBlocked = ToDecimal(response.PortfolioForts.MoneyReserved);
                    updated.ValueLiquidCash = ToDecimal(response.PortfolioForts.AvailableCash);
                }
                else if (response.PortfolioMct != null)
                {
                    // MCT — счёт американских рынков; сообщение в proto пустое,
                    // маржа и доступный кэш через API не отдаются, ValueBlocked/ValueLiquidCash остаются нулевыми
                    if (_loggedMctAccounts.Add(response.AccountId))
                    {
                        SendLogMessage("Finam MCT account " + response.AccountId
                            + ": margin and available cash are not provided by the API, ValueBlocked/ValueLiquidCash will be zero.", LogMessageType.System);
                    }
                }

                for (int i = 0; i < response.Cash.Count; i++)
                {
                    Money money = response.Cash[i];
                    PositionOnBoard position = new PositionOnBoard();
                    position.PortfolioName = response.AccountId;
                    position.SecurityNameCode = money.CurrencyCode;
                    position.ValueCurrent = MoneyToDecimal(money);
                    position.ValueBegin = PreviousPositionBegin(previous, position.SecurityNameCode, position.ValueCurrent);
                    updated.SetNewPosition(position);
                }

                for (int i = 0; i < response.Positions.Count; i++)
                {
                    FPosition source = response.Positions[i];
                    Security security = ResolveSecurity(source.Symbol);
                    PositionOnBoard position = new PositionOnBoard();
                    position.PortfolioName = response.AccountId;
                    position.SecurityNameCode = security != null ? security.Name : source.Symbol;
                    position.SecurityNameClass = security != null ? security.NameClass : null;
                    position.ValueCurrent = ToDecimal(source.Quantity) / LotDivisor(security);
                    position.ValueBegin = PreviousPositionBegin(previous, position.SecurityNameCode, position.ValueCurrent);
                    position.UnrealizedPnl = ToDecimal(source.UnrealizedPnl);
                    updated.SetNewPosition(position);
                }

                int index = _portfolios.FindIndex(FindPortfolioPredicate(response.AccountId));

                if (index >= 0)
                {
                    _portfolios[index] = updated;
                }
                else
                {
                    _portfolios.Add(updated);
                }

                if (ServerStatus == ServerConnectStatus.Connect && SecuritiesLoaded())
                {
                    // до загрузки бумаг позиции резолвятся в сырой символ (VTBR@MISX) — движок мержит и не удаляет, не эмитим
                    PortfolioEvent?.Invoke(ClonePortfolios(_portfolios));
                }
            }
        }

        private Predicate<Portfolio> FindPortfolioPredicate(string accountId)
        {
            return delegate(Portfolio portfolio) { return portfolio.Number == accountId; };
        }

        private bool SecuritiesLoaded()
        {
            lock (_securitiesLocker)
            {
                return _allSecurities.Count > 0;
            }
        }

        private decimal PreviousPositionBegin(Portfolio portfolio, string securityName, decimal current)
        {
            if (portfolio == null || portfolio.PositionOnBoard == null)
            {
                return current;
            }

            for (int i = 0; i < portfolio.PositionOnBoard.Count; i++)
            {
                PositionOnBoard position = portfolio.PositionOnBoard[i];

                if (position.SecurityNameCode == securityName || IsSameSecurity(position.SecurityNameCode, securityName))
                {
                    return position.ValueBegin;
                }
            }

            return current;
        }

        private bool IsSameSecurity(string first, string second)
        {
            Security firstSecurity = ResolveSecurity(first);
            Security secondSecurity = ResolveSecurity(second);
            return firstSecurity != null && secondSecurity != null && firstSecurity.NameId == secondSecurity.NameId;
        }

        private List<Portfolio> ClonePortfolios(List<Portfolio> source)
        {
            List<Portfolio> result = new List<Portfolio>();

            for (int i = 0; i < source.Count; i++)
            {
                Portfolio portfolio = source[i];
                Portfolio clone = new Portfolio();
                clone.Number = portfolio.Number;
                clone.ValueBegin = portfolio.ValueBegin;
                clone.ValueCurrent = portfolio.ValueCurrent;
                clone.ValueBlocked = portfolio.ValueBlocked;
                clone.ValueLiquidCash = portfolio.ValueLiquidCash;
                clone.UnrealizedPnl = portfolio.UnrealizedPnl;
                clone.ServerType = portfolio.ServerType;
                clone.ServerUniqueName = portfolio.ServerUniqueName;
                clone.PositionOnBoard = new List<PositionOnBoard>();

                for (int j = 0; j < portfolio.PositionOnBoard.Count; j++)
                {
                    PositionOnBoard position = portfolio.PositionOnBoard[j];
                    PositionOnBoard positionClone = new PositionOnBoard();
                    positionClone.PortfolioName = position.PortfolioName;
                    positionClone.SecurityNameCode = position.SecurityNameCode;
                    positionClone.SecurityNameClass = position.SecurityNameClass;
                    positionClone.ValueBegin = position.ValueBegin;
                    positionClone.ValueCurrent = position.ValueCurrent;
                    positionClone.ValueBlocked = position.ValueBlocked;
                    positionClone.UnrealizedPnl = position.UnrealizedPnl;
                    clone.PositionOnBoard.Add(positionClone);
                }

                result.Add(clone);
            }

            return result;
        }

        private readonly object _portfolioLocker = new object();

        private List<Portfolio> _portfolios = new List<Portfolio>();

        private readonly HashSet<string> _loggedMctAccounts = new HashSet<string>(StringComparer.Ordinal);

        #endregion

        #region Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            if (security == null || timeFrameBuilder == null || candleCount <= 0)
            {
                return null;
            }

            FTimeFrame frame = ConvertTimeFrame(timeFrameBuilder.TimeFrame);

            if (frame == FTimeFrame.Unspecified)
            {
                return null;
            }

            int bufferedCount = candleCount + Math.Max(10, candleCount / 2);
            DateTime end = MoscowNow();
            DateTime earliest = end - HistoryDepth(frame);
            DateTime start = end - TimeSpan.FromTicks(timeFrameBuilder.TimeFrameTimeSpan.Ticks * bufferedCount);

            if (start < earliest)
            {
                start = earliest;
            }

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, start, end, start);

            if ((candles == null || candles.Count < candleCount) && start > earliest)
            {
                start = earliest;
                candles = GetCandleDataToSecurity(security, timeFrameBuilder, start, end, start);
            }

            if (candles == null)
            {
                return null;
            }

            int skip = Math.Max(0, candles.Count - candleCount);
            return candles.GetRange(skip, candles.Count - skip);
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            try
            {
                if (security == null || timeFrameBuilder == null || ServerStatus != ServerConnectStatus.Connect)
                {
                    return null;
                }

                long generation = _generation;
                FTimeFrame frame = ConvertTimeFrame(timeFrameBuilder.TimeFrame);

                if (frame == FTimeFrame.Unspecified)
                {
                    return null;
                }

                if (IsFullLog())
                {
                    SendLogMessage("Finam candle request: " + security.NameId + " " + frame + " start=" + startTime + " end=" + endTime + " actual=" + actualTime, LogMessageType.System);
                }

                if (startTime != actualTime)
                {
                    startTime = actualTime;
                }

                if (endTime <= startTime)
                {
                    return null;
                }

                Security resolved = ResolveSecurity(security.NameId) ?? security;
                EnrichSecurity(resolved);

                // реконнект обнуляет клиентов вместе со сменой генерации — тихий выход до первого чанка
                if (generation != _generation)
                {
                    return null;
                }

                Dictionary<DateTime, Candle> unique = new Dictionary<DateTime, Candle>();
                DateTime chunkStart = startTime;
                TimeSpan depth = HistoryChunkDepth(frame);
                int chunkCount = 0;
                int emptyChunks = 0;

                while (chunkStart < endTime)
                {
                    DateTime chunkEnd = chunkStart + depth;

                    if (chunkEnd > endTime)
                    {
                        chunkEnd = endTime;
                    }

                    chunkCount++;
                    BarsResponse response = LoadBars(resolved, frame, chunkStart, chunkEnd);

                    if (generation != _generation)
                    {
                        return null;
                    }

                    if (response.Bars.Count == 0)
                    {
                        emptyChunks++;
                    }

                    for (int i = 0; i < response.Bars.Count; i++)
                    {
                        Bar bar = response.Bars[i];
                        Candle candle = ConvertCandle(bar, resolved, timeFrameBuilder.TimeFrameTimeSpan);

                        if (candle.TimeStart >= startTime && candle.TimeStart < endTime)
                        {
                            unique[candle.TimeStart] = candle;
                        }
                    }

                    chunkStart = chunkEnd;
                }

                if (generation != _generation)
                {
                    return null;
                }

                if (emptyChunks > 0)
                {
                    // Finam отдаёт пустой ответ вместо ошибки, если период за границей глубины истории
                    SendLogMessage("Finam returned no bars for " + emptyChunks + " of " + chunkCount + " chunks for "
                        + resolved.NameId + " (" + frame + "). The period may exceed the available history depth or contain no trading.",
                        LogMessageType.System);
                }

                List<Candle> candles = new List<Candle>(unique.Values);
                candles.Sort(CompareCandles);

                if (IsFullLog())
                {
                    SendLogMessage("Finam candle result: " + resolved.NameId + " " + frame + " count=" + candles.Count, LogMessageType.System);
                }

                return candles.Count == 0 ? null : candles;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private int CompareCandles(Candle first, Candle second)
        {
            return first.TimeStart.CompareTo(second.TimeStart);
        }

        private BarsResponse LoadBars(Security security, FTimeFrame frame, DateTime start, DateTime end)
        {
            BarsRequest request = new BarsRequest
            {
                Symbol = security.NameId,
                Timeframe = frame,
                Interval = new Interval
                {
                    StartTime = Timestamp.FromDateTime(ToUtc(start)),
                    EndTime = Timestamp.FromDateTime(ToUtc(end))
                }
            };

            for (int attempt = 0; attempt < 2; attempt++)
            {
                WaitRateGate("Bars");

                // клиент обнуляется в CloseSession при реконнекте — захватываем и проверяем
                MarketDataService.MarketDataServiceClient client = _marketDataClient;

                if (client == null)
                {
                    throw new InvalidOperationException("Finam market data client is not connected.");
                }

                AsyncUnaryCall<BarsResponse> call = client.BarsAsync(request, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));

                try
                {
                    BarsResponse response = call.ResponseAsync.GetAwaiter().GetResult();

                    if (response == null)
                    {
                        throw new InvalidOperationException("Finam returned an empty bars response for " + security.NameId + ".");
                    }

                    return response;
                }
                catch (RpcException error) when (error.StatusCode == StatusCode.ResourceExhausted && attempt == 0)
                {
                    // 429: ретрай только по приглашению сервера (grpc-retry-pushback-ms), как в официальном SDK
                    int pushbackMs = GetRetryPushbackMs(call);

                    if (pushbackMs < 0)
                    {
                        throw;
                    }

                    Thread.Sleep(pushbackMs);
                }
            }

            throw new InvalidOperationException("Finam bars request failed after the pushback retry for " + security.NameId + ".");
        }

        private int GetRetryPushbackMs(AsyncUnaryCall<BarsResponse> call)
        {
            try
            {
                Metadata trailers = call.GetTrailers();

                if (trailers == null)
                {
                    return -1;
                }

                for (int i = 0; i < trailers.Count; i++)
                {
                    if (trailers[i].Key == "grpc-retry-pushback-ms")
                    {
                        int value;

                        if (int.TryParse(trailers[i].Value, out value))
                        {
                            return value;
                        }
                    }
                }
            }
            catch
            {
                // трейлеры недоступны — ретрая не будет
            }

            return -1;
        }

        private Candle ConvertCandle(Bar bar, Security security, TimeSpan timeFrame)
        {
            Candle candle = new Candle();
            candle.TimeStart = ToMoscow(bar.Timestamp);
            candle.Open = ToDecimal(bar.Open);
            candle.High = ToDecimal(bar.High);
            candle.Low = ToDecimal(bar.Low);
            candle.Close = ToDecimal(bar.Close);
            candle.Volume = ToDecimal(bar.Volume) / LotDivisor(security);

            // снапшотная свеча у Финама предварительная (дорасчёт, поздние сделки) — честно помечаем формирующейся,
            // иначе она заморозится в серии: тики обновляют только Started-свечи
            candle.State = bar.IsDataSnapshot || candle.TimeStart + timeFrame > MoscowNow() ? CandleState.Started : CandleState.Finished;
            return candle;
        }

        private TimeSpan HistoryDepth(FTimeFrame frame)
        {
            if (frame == FTimeFrame.M1)
            {
                return TimeSpan.FromDays(7);
            }

            if (frame == FTimeFrame.D)
            {
                return TimeSpan.FromDays(365);
            }

            return TimeSpan.FromDays(30);
        }

        private TimeSpan HistoryChunkDepth(FTimeFrame frame)
        {
            if (frame == FTimeFrame.M1)
            {
                return TimeSpan.FromDays(7) - TimeSpan.FromHours(1);
            }

            if (frame == FTimeFrame.D)
            {
                return TimeSpan.FromDays(365) - TimeSpan.FromDays(1);
            }

            return TimeSpan.FromDays(30) - TimeSpan.FromHours(1);
        }

        private FTimeFrame ConvertTimeFrame(TimeFrame frame)
        {
            switch (frame)
            {
                case TimeFrame.Min1: return FTimeFrame.M1;
                case TimeFrame.Min5: return FTimeFrame.M5;
                case TimeFrame.Min15: return FTimeFrame.M15;
                case TimeFrame.Min30: return FTimeFrame.M30;
                case TimeFrame.Hour1: return FTimeFrame.H1;
                case TimeFrame.Hour2: return FTimeFrame.H2;
                case TimeFrame.Hour4: return FTimeFrame.H4;
                case TimeFrame.Day: return FTimeFrame.D;
                default: return FTimeFrame.Unspecified;
            }
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region gRPC connection and authentication

        private void CreateChannel(WebProxy proxy)
        {
            SocketsHttpHandler handler = new SocketsHttpHandler();
            handler.UseProxy = proxy != null;
            handler.Proxy = proxy;
            handler.KeepAlivePingDelay = TimeSpan.FromSeconds(30);
            handler.KeepAlivePingTimeout = TimeSpan.FromSeconds(15);
            handler.KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always;
            handler.EnableMultipleHttp2Connections = true;
            handler.PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2);

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = Timeout.InfiniteTimeSpan;
            _channel = GrpcChannel.ForAddress(_host, new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.SecureSsl,
                HttpClient = _httpClient
            });

            _authClient = new AuthService.AuthServiceClient(_channel);
            _assetsClient = new AssetsService.AssetsServiceClient(_channel);
            _accountsClient = new AccountsService.AccountsServiceClient(_channel);
            _ordersClient = new OrdersService.OrdersServiceClient(_channel);
            _marketDataClient = new MarketDataService.MarketDataServiceClient(_channel);
            _rootCancellation = new CancellationTokenSource();
            _generation++;
        }

        private void AuthenticateInitial()
        {
            lock (_tokenLocker)
            {
                WaitRateGate("Auth");
                AuthResponse auth = _authClient.Auth(new AuthRequest
                {
                    Secret = _secret,
                    SourceAppId = "OsEngine"
                }, headers: null, deadline: DateTime.UtcNow.AddSeconds(30));

                if (auth == null || string.IsNullOrEmpty(auth.Token))
                {
                    throw new InvalidOperationException("Finam authentication returned an empty JWT.");
                }

                WaitRateGate("TokenDetails");
                TokenDetailsResponse details = _authClient.TokenDetails(new TokenDetailsRequest
                {
                    Token = auth.Token
                }, headers: null, deadline: DateTime.UtcNow.AddSeconds(30));
                ApplyTokenDetails(details, true);
                _jwt = auth.Token;
            }
        }

        private void ApplyTokenDetails(TokenDetailsResponse details, bool selectAccounts)
        {
            if (details == null || details.ExpiresAt == null)
            {
                throw new InvalidOperationException("Finam token details are incomplete.");
            }

            _tokenExpiryUtc = details.ExpiresAt.ToDateTime();
            _readOnly = details.Readonly;
            _mdPermissions = new List<MDPermission>(details.MdPermissions);

            if (selectAccounts == false)
            {
                return;
            }

            List<string> accounts = new List<string>();

            for (int i = 0; i < details.AccountIds.Count; i++)
            {
                if (string.IsNullOrEmpty(details.AccountIds[i]) == false
                    && accounts.Contains(details.AccountIds[i]) == false)
                {
                    accounts.Add(details.AccountIds[i]);
                }
            }

            _accountIds = accounts;
        }

        private void ValidateAccounts()
        {
            for (int i = 0; i < _accountIds.Count; i++)
            {
                WaitRateGate("GetAccount");
                GetAccountResponse response = _accountsClient.GetAccount(new GetAccountRequest
                {
                    AccountId = _accountIds[i]
                }, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));
                UpdatePortfolio(response);
            }
        }

        private void UpdateRenewedToken(long generation, string token)
        {
            if (string.IsNullOrEmpty(token) || generation != _generation || _authClient == null)
            {
                return;
            }

            try
            {
                WaitRateGate("TokenDetails");
                TokenDetailsResponse details = _authClient.TokenDetails(new TokenDetailsRequest
                {
                    Token = token
                }, headers: null, deadline: DateTime.UtcNow.AddSeconds(30));

                lock (_tokenLocker)
                {
                    if (generation == _generation)
                    {
                        ApplyTokenDetails(details, false);
                        _jwt = token;
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TokenFallbackWorker()
        {
            try
            {
                while (IsCompletelyDeleted == false)
                {
                    try
                    {
                        if (_workersStopped.WaitOne(5000))
                        {
                            break;
                        }

                        if (ServerStatus != ServerConnectStatus.Connect || _authClient == null)
                        {
                            continue;
                        }

                        DateTime expiry;
                        long generation;

                        lock (_tokenLocker)
                        {
                            expiry = _tokenExpiryUtc;
                        }

                        generation = _generation;

                        if (expiry == DateTime.MinValue || expiry > DateTime.UtcNow.AddSeconds(60))
                        {
                            continue;
                        }

                        RefreshTokenNearExpiry(generation);
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void RefreshTokenNearExpiry(long generation)
        {
            lock (_tokenLocker)
            {
                if (generation != _generation || _authClient == null || _tokenExpiryUtc > DateTime.UtcNow.AddSeconds(60))
                {
                    return;
                }

                WaitRateGate("Auth");
                AuthResponse auth = _authClient.Auth(new AuthRequest
                {
                    Secret = _secret,
                    SourceAppId = "OsEngine"
                }, headers: null, deadline: DateTime.UtcNow.AddSeconds(30));

                if (auth == null || string.IsNullOrEmpty(auth.Token))
                {
                    throw new InvalidOperationException("Finam fallback authentication returned an empty JWT.");
                }

                WaitRateGate("TokenDetails");
                TokenDetailsResponse details = _authClient.TokenDetails(new TokenDetailsRequest
                {
                    Token = auth.Token
                }, headers: null, deadline: DateTime.UtcNow.AddSeconds(30));
                ApplyTokenDetails(details, false);
                _jwt = auth.Token;
            }
        }

        private Metadata CreateMetadata()
        {
            string token;

            lock (_tokenLocker)
            {
                token = _jwt;
            }

            Metadata metadata = new Metadata();
            metadata.Add("x-app-name", "OsEngine");
            metadata.Add("Authorization", "Bearer " + token);
            return metadata;
        }

        private bool HasMarketDataAccess(string mic, bool needOrderBook)
        {
            List<MDPermission> permissions;

            lock (_tokenLocker)
            {
                permissions = _mdPermissions;
            }

            if (permissions == null || permissions.Count == 0)
            {
                return true;
            }

            MDPermission matched = null;
            MDPermission worldwide = null;

            for (int i = 0; i < permissions.Count; i++)
            {
                MDPermission permission = permissions[i];

                if (permission.ConditionCase == MDPermission.ConditionOneofCase.Mic
                    && string.Equals(permission.Mic, mic, StringComparison.OrdinalIgnoreCase))
                {
                    matched = permission;
                    break;
                }

                if (permission.ConditionCase == MDPermission.ConditionOneofCase.Worldwide
                    && permission.Worldwide)
                {
                    worldwide = permission;
                }
            }

            MDPermission effective = matched ?? worldwide;

            if (effective == null)
            {
                return true;
            }

            if (effective.QuoteLevel == MDPermission.Types.QuoteLevel.AccessForbidden)
            {
                return false;
            }

            if (needOrderBook
                && effective.QuoteLevel != MDPermission.Types.QuoteLevel.DepthOfMarket
                && effective.QuoteLevel != MDPermission.Types.QuoteLevel.DepthOfBook)
            {
                return false;
            }

            return true;
        }

        private readonly string _host = "https://api.finam.ru:443";

        private readonly object _tokenLocker = new object();

        private List<string> _accountIds = new List<string>();

        private string _secret = "";

        private string _jwt = "";

        private DateTime _tokenExpiryUtc = DateTime.MinValue;

        private bool _readOnly;

        private List<MDPermission> _mdPermissions = new List<MDPermission>();

        private GrpcChannel _channel;

        private HttpClient _httpClient;

        private WebProxy _proxy;

        private long _issCompletedGeneration = -1;

        private readonly AutoResetEvent _warmerWake = new AutoResetEvent(false);

        private AuthService.AuthServiceClient _authClient;

        private AssetsService.AssetsServiceClient _assetsClient;

        private AccountsService.AccountsServiceClient _accountsClient;

        private OrdersService.OrdersServiceClient _ordersClient;

        private MarketDataService.MarketDataServiceClient _marketDataClient;

        #endregion

        #region Security subscribe

        public void Subscribe(Security security)
        {
            try
            {
                if (security == null || ServerStatus != ServerConnectStatus.Connect)
                {
                    return;
                }

                Security resolved = ResolveSecurity(security.NameId) ?? ResolveSecurity(security.Name, security.NameClass) ?? security;
                EnrichSecurity(resolved);

                if (resolved.State == SecurityStateType.Close)
                {
                    // неторгуемая бумага — штатная ситуация, не ошибка
                    SendLogMessage("Finam security is not tradable, skipped: " + resolved.NameId, LogMessageType.System);
                    return;
                }

                lock (_streamLocker)
                {
                    if (_subscribedSecurities.ContainsKey(resolved.NameId))
                    {
                        return;
                    }

                    _subscribedSecurities.Add(resolved.NameId, resolved);
                }

                bool tradesAllowed = HasMarketDataAccess(resolved.Exchange, false);
                bool bookAllowed = HasMarketDataAccess(resolved.Exchange, true);

                if (tradesAllowed == false && bookAllowed == false)
                {
                    SendLogMessage("Finam market data access is forbidden for " + resolved.Exchange + ": " + resolved.NameId, LogMessageType.System);

                    lock (_streamLocker)
                    {
                        _subscribedSecurities.Remove(resolved.NameId);
                    }

                    return;
                }

                bool tradesStarted = tradesAllowed == false
                    || StartStream(FinamStreamKind.LatestTrades, resolved.NameId, resolved, null);
                bool bookStarted = bookAllowed == false
                    || StartStream(FinamStreamKind.OrderBook, resolved.NameId, resolved, null);

                if (tradesStarted == false || bookStarted == false)
                {
                    Unsubscribe(resolved);
                    throw new InvalidOperationException("Finam public stream creation failed for " + resolved.NameId + ".");
                }

                // Quote-стрим мультисимвольный: состав зафиксирован при открытии, пересобираем его отложенно
                RequestQuoteSync();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Unsubscribe(Security security)
        {
            try
            {
                if (security == null)
                {
                    return;
                }

                Security resolved = ResolveSecurity(security.NameId) ?? ResolveSecurity(security.Name, security.NameClass) ?? security;
                string key = resolved.NameId;

                lock (_streamLocker)
                {
                    _subscribedSecurities.Remove(key);
                }

                StopStream(FinamStreamKind.LatestTrades, key);
                StopStream(FinamStreamKind.OrderBook, key);
                FinamOrderBook removed;

                if (_orderBooks.TryRemove(key, out removed))
                {
                    removed.Reset();
                }

                lock (_depthTimeLocker)
                {
                    _lastDepthTimes.Remove(key);
                }

                RequestQuoteSync();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private readonly Dictionary<string, Security> _subscribedSecurities = new Dictionary<string, Security>();

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region Stream lifecycle and recovery

        private string StreamId(FinamStreamKind kind, string key)
        {
            return kind.ToString() + ":" + key;
        }

        private bool StartStream(FinamStreamKind kind, string key, Security security, string accountId, long? requiredGeneration = null)
        {
            string id = StreamId(kind, key);
            FinamStreamLease lease;

            if (requiredGeneration.HasValue && requiredGeneration.Value != _generation)
            {
                return false;
            }

            lock (_streamLocker)
            {
                FinamStreamLease existing;

                if (_streams.TryGetValue(id, out existing)
                    && existing.RestartRequested == false
                    && existing.Generation == _generation)
                {
                    return true;
                }

                CancellationTokenSource root = _rootCancellation;

                if (root == null)
                {
                    return false;
                }

                CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(root.Token);
                lease = new FinamStreamLease(kind, key, requiredGeneration ?? _generation, linked);
                lease.Security = security;
                lease.AccountId = accountId;
                _streams[id] = lease;
            }

            try
            {
                if (kind == FinamStreamKind.OrderBook)
                {
                    FinamOrderBook book = _orderBooks.GetOrAdd(key, CreateOrderBook);
                    book.Reset();
                }

                WaitRateGate(kind.ToString(), 120);

                // пейсинг открытия публичных стримов: у Finam есть недокументированный burst-лимитер
                if (kind == FinamStreamKind.LatestTrades || kind == FinamStreamKind.OrderBook)
                {
                    ThrottlePublicStreamOpen();
                }

                int rateLimitRetries = 0;

                while (true)
                {
                    try
                    {
                        if (kind == FinamStreamKind.JwtRenewal)
                        {
                            lease.Call = _authClient.SubscribeJwtRenewal(new SubscribeJwtRenewalRequest
                            {
                                Secret = _secret,
                                SourceAppId = "OsEngine"
                            }, headers: null, cancellationToken: lease.Cancellation.Token);
                        }
                        else if (kind == FinamStreamKind.Account)
                        {
                            lease.Call = _accountsClient.SubscribeAccount(new GetAccountRequest { AccountId = accountId }, CreateMetadata(), null, lease.Cancellation.Token);
                        }
                        else if (kind == FinamStreamKind.Orders)
                        {
                            lease.Call = _ordersClient.SubscribeOrders(new SubscribeOrdersRequest { AccountId = accountId }, CreateMetadata(), null, lease.Cancellation.Token);
                        }
                        else if (kind == FinamStreamKind.Trades)
                        {
                            lease.Call = _ordersClient.SubscribeTrades(new SubscribeTradesRequest { AccountId = accountId }, CreateMetadata(), null, lease.Cancellation.Token);
                        }
                        else if (kind == FinamStreamKind.LatestTrades)
                        {
                            lease.Call = _marketDataClient.SubscribeLatestTrades(new SubscribeLatestTradesRequest { Symbol = key }, CreateMetadata(), null, lease.Cancellation.Token);
                        }
                        else if (kind == FinamStreamKind.OrderBook)
                        {
                            lease.Call = _marketDataClient.SubscribeOrderBook(new SubscribeOrderBookRequest { Symbol = key }, CreateMetadata(), null, lease.Cancellation.Token);
                        }
                        else if (kind == FinamStreamKind.Quote)
                        {
                            List<string> quoteSymbols = GetQuoteDesiredSymbols();

                            if (quoteSymbols.Count == 0)
                            {
                                throw new InvalidOperationException("Finam quote stream has no symbols to subscribe.");
                            }

                            SubscribeQuoteRequest quoteRequest = new SubscribeQuoteRequest();
                            quoteRequest.Symbols.AddRange(quoteSymbols);
                            lease.Call = _marketDataClient.SubscribeQuote(quoteRequest, CreateMetadata(), null, lease.Cancellation.Token);
                        }

                        break;
                    }
                    catch (RpcException error) when (IsLocalRateLimited(error) && rateLimitRetries < 3)
                    {
                        // локальный лимитер Finam: ждём с нарастающей паузой и повторяем
                        rateLimitRetries++;
                        Thread.Sleep(1000 * rateLimitRetries);
                    }
                }

                if (lease.Cancellation.IsCancellationRequested || lease.Generation != _generation)
                {
                    CleanupLease(lease, false);
                    return false;
                }

                if (kind == FinamStreamKind.JwtRenewal)
                {
                    lease.Reader = ReadJwtStreamAsync(lease, (AsyncServerStreamingCall<SubscribeJwtRenewalResponse>)lease.Call);
                }
                else if (kind == FinamStreamKind.Account)
                {
                    lease.Reader = ReadAccountStreamAsync(lease, (AsyncServerStreamingCall<GetAccountResponse>)lease.Call);
                }
                else if (kind == FinamStreamKind.Orders)
                {
                    lease.Reader = ReadOrdersStreamAsync(lease, (AsyncServerStreamingCall<SubscribeOrdersResponse>)lease.Call);
                }
                else if (kind == FinamStreamKind.Trades)
                {
                    lease.Reader = ReadPrivateTradesStreamAsync(lease, (AsyncServerStreamingCall<SubscribeTradesResponse>)lease.Call);
                }
                else if (kind == FinamStreamKind.LatestTrades)
                {
                    lease.Reader = ReadPublicTradesStreamAsync(lease, (AsyncServerStreamingCall<SubscribeLatestTradesResponse>)lease.Call);
                }
                else if (kind == FinamStreamKind.OrderBook)
                {
                    lease.Reader = ReadOrderBookStreamAsync(lease, (AsyncServerStreamingCall<SubscribeOrderBookResponse>)lease.Call);
                }
                else if (kind == FinamStreamKind.Quote)
                {
                    lease.Reader = ReadQuoteStreamAsync(lease, (AsyncServerStreamingCall<SubscribeQuoteResponse>)lease.Call);
                }

                return true;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);

                lock (_streamLocker)
                {
                    FinamStreamLease current;

                    if (_streams.TryGetValue(id, out current) && current == lease)
                    {
                        _streams.Remove(id);
                    }
                }

                CleanupLease(lease, false);
                return false;
            }
        }

        private void StreamRecoverySupervisor()
        {
            try
            {
                while (IsCompletelyDeleted == false)
                {
                    try
                    {
                        _streamWake.WaitOne(1000);

                        if (_rootCancellation == null)
                        {
                            continue;
                        }

                        List<FinamStreamLease> leases = new List<FinamStreamLease>();

                        lock (_streamLocker)
                        {
                            foreach (KeyValuePair<string, FinamStreamLease> pair in _streams)
                            {
                                if (pair.Value.RestartRequested)
                                {
                                    leases.Add(pair.Value);
                                }
                            }
                        }

                        for (int i = 0; i < leases.Count; i++)
                        {
                            FinamStreamLease lease = leases[i];

                            if (lease.LastMessageUtc > DateTime.UtcNow)
                            {
                                continue;
                            }

                            RestartStream(lease);
                        }

                        bool quoteSyncDue;

                        lock (_streamLocker)
                        {
                            quoteSyncDue = _quoteSyncDirty && DateTime.UtcNow >= _quoteSyncNotBefore;
                        }

                        if (quoteSyncDue)
                        {
                            SyncQuoteStream();
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void RestartStream(FinamStreamLease lease)
        {
            string id = StreamId(lease.Kind, lease.Key);

            lock (_streamLocker)
            {
                FinamStreamLease current;

                if (_streams.TryGetValue(id, out current) == false || current != lease || lease.Generation != _generation)
                {
                    return;
                }

                _streams.Remove(id);
            }

            CleanupLease(lease);

            bool started = StartStream(lease.Kind, lease.Key, lease.Security, lease.AccountId, lease.Generation);

            if (started == false)
            {
                FinamStreamLease retry = CreateRetryLease(lease);

                if (retry == null)
                {
                    return;
                }

                lock (_streamLocker)
                {
                    if (_generation == lease.Generation && _rootCancellation != null && _streams.ContainsKey(id) == false)
                    {
                        _streams[id] = retry;
                    }
                }

                if (retry.FirstRestartFailureUtc != DateTime.MinValue
                    && DateTime.UtcNow - retry.FirstRestartFailureUtc > TimeSpan.FromSeconds(30)
                    && IsMaintenanceWindow() == false)
                {
                    if (IsPrivateStreamKind(lease.Kind))
                    {
                        // приватные стримы и JWT жизненно необходимы — их смерть = смерть соединения
                        SetDisconnected();
                    }
                    else if (retry.RestartAttempts >= 8)
                    {
                        // публичный стрим не поднимается 8 попыток — сдаёмся по символу, сервер не роняем
                        SurrenderPublicStream(retry, "restart attempts exceeded");
                    }

                    return;
                }
            }

            if (lease.Kind == FinamStreamKind.Orders || lease.Kind == FinamStreamKind.Trades)
            {
                // реконсиляция — в отдельном воркере: она сетевя и долгая, супервизор не должен простаивать
                QueuePrivateRecoveryReconciliation(lease.AccountId, lease.Generation);
            }
        }

        private FinamStreamLease CreateRetryLease(FinamStreamLease source)
        {
            CancellationTokenSource linked;

            // root читаем и линкуем под _sessionLocker: CloseSession обнуляет его под тем же локом,
            // а dispose делает после — значит, ненулевой root здесь гарантированно жив
            lock (_sessionLocker)
            {
                if (source.Generation != _generation || _rootCancellation == null)
                {
                    // сессия закрывается — ретрай бессмысленен
                    return null;
                }

                linked = CancellationTokenSource.CreateLinkedTokenSource(_rootCancellation.Token);
            }

            FinamStreamLease retry = new FinamStreamLease(source.Kind, source.Key, source.Generation, linked);
            retry.Security = source.Security;
            retry.AccountId = source.AccountId;
            retry.RestartRequested = true;
            retry.RestartAttempts = source.RestartAttempts + 1;
            retry.FirstRestartFailureUtc = source.FirstRestartFailureUtc;

            // экспоненциальный backoff с джиттером: 5с, 10с, 20с, ... максимум 60с
            double delaySeconds = Math.Min(60, 5 * Math.Pow(2, retry.RestartAttempts - 1));
            delaySeconds *= 0.75 + _jitterRandom.NextDouble() * 0.5;
            retry.LastMessageUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
            return retry;
        }

        private bool IsMaintenanceWindow()
        {
            // ежедневное технологическое окно Finam API: 05.00-06.15 мск
            DateTime moscowNow = MoscowNow();
            return moscowNow.Hour == 5 || (moscowNow.Hour == 6 && moscowNow.Minute < 15);
        }

        private bool IsFatalStreamError(StatusCode code)
        {
            // нетранзиентные ошибки: ретраи их не лечат никогда
            return code == StatusCode.PermissionDenied
                || code == StatusCode.InvalidArgument
                || code == StatusCode.NotFound
                || code == StatusCode.Unimplemented;
        }

        private bool IsPrivateStreamKind(FinamStreamKind kind)
        {
            return kind == FinamStreamKind.JwtRenewal
                || kind == FinamStreamKind.Account
                || kind == FinamStreamKind.Orders
                || kind == FinamStreamKind.Trades;
        }

        private void SurrenderPublicStream(FinamStreamLease lease, string reason)
        {
            // фатальная ошибка публичного стрима (нет прав на данные и т.п.) — сдаёмся по символу,
            // не роняя сервер: торговля и остальные бумаги живут, CheckDataFeedLogic подстрахует
            SendLogMessage("Finam public stream surrendered: " + lease.Kind + ":" + lease.Key + ". " + reason, LogMessageType.Error);

            StopStream(lease.Kind, lease.Key);

            if (lease.Kind == FinamStreamKind.Quote)
            {
                lock (_streamLocker)
                {
                    _quoteActiveSignature = null;
                }

                return;
            }

            Security security = lease.Security;

            if (security == null || string.IsNullOrEmpty(security.NameId))
            {
                return;
            }

            bool removed;

            lock (_streamLocker)
            {
                removed = _subscribedSecurities.Remove(security.NameId);
            }

            if (removed == false)
            {
                return;
            }

            // оба публичных стрима символа бессмысленны без подписки
            StopStream(FinamStreamKind.LatestTrades, security.NameId);
            StopStream(FinamStreamKind.OrderBook, security.NameId);

            FinamOrderBook removedBook;

            if (_orderBooks.TryRemove(security.NameId, out removedBook))
            {
                removedBook.Reset();
            }

            lock (_depthTimeLocker)
            {
                _lastDepthTimes.Remove(security.NameId);
            }

            RequestQuoteSync();
        }

        private bool IsLeaseCurrent(FinamStreamLease lease)
        {
            return lease != null
                && lease.Generation == _generation
                && _rootCancellation != null
                && lease.Cancellation.IsCancellationRequested == false;
        }

        private void RequestStreamRestart(FinamStreamLease lease)
        {
            if (lease == null || lease.Cancellation.IsCancellationRequested || lease.Generation != _generation)
            {
                return;
            }

            lock (_streamLocker)
            {
                FinamStreamLease current;

                if (_streams.TryGetValue(StreamId(lease.Kind, lease.Key), out current) == false || current != lease)
                {
                    return;
                }

                if (lease.RestartRequested == false)
                {
                    lease.FirstRestartFailureUtc = DateTime.UtcNow;
                }

                lease.RestartRequested = true;
                lease.LastMessageUtc = DateTime.UtcNow;
            }

            _streamWake.Set();
        }

        private void StopStream(FinamStreamKind kind, string key)        {
            FinamStreamLease lease = null;

            lock (_streamLocker)
            {
                string id = StreamId(kind, key);

                if (_streams.TryGetValue(id, out lease))
                {
                    _streams.Remove(id);
                }
            }

            if (lease != null)
            {
                CleanupLease(lease);
            }
        }

        private void CleanupLease(FinamStreamLease lease, bool waitForReader = true)
        {
            if (lease == null)
            {
                return;
            }

            try
            {
                lease.Cancellation.Cancel();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            try
            {
                lease.Call?.Dispose();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            try
            {
                if (waitForReader && lease.Reader != null && lease.Reader.IsCompleted == false)
                {
                    lease.Reader.Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }

            lease.Call = null;

            if (lease.Reader != null && lease.Reader.IsCompleted == false)
            {
                lease.Reader.ContinueWith(DisposeLeaseCancellation, lease.Cancellation, TaskContinuationOptions.ExecuteSynchronously);
                SendLogMessage("Finam stream reader did not stop within the cleanup timeout: " + lease.Kind + ":" + lease.Key, LogMessageType.System);
            }
            else
            {
                lease.Cancellation.Dispose();
            }
        }

        private void DisposeLeaseCancellation(Task task, object state)
        {
            CancellationTokenSource cancellation = (CancellationTokenSource)state;
            cancellation.Dispose();
        }

        private readonly object _streamLocker = new object();

        private readonly object _streamOpenThrottleLocker = new object();

        private DateTime _lastPublicStreamOpenUtc = DateTime.MinValue;

        private bool IsLocalRateLimited(RpcException error)
        {
            return error.StatusCode == StatusCode.Unavailable
                && error.Status.Detail != null
                && error.Status.Detail.Contains("local_rate_limited");
        }

        private void ThrottlePublicStreamOpen()
        {
            lock (_streamOpenThrottleLocker)
            {
                DateTime now = DateTime.UtcNow;
                TimeSpan elapsed = now - _lastPublicStreamOpenUtc;

                if (elapsed.TotalMilliseconds < 300)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(300) - elapsed);
                }

                _lastPublicStreamOpenUtc = DateTime.UtcNow;
            }
        }

        private readonly Random _jitterRandom = new Random();

        private readonly AutoResetEvent _streamWake = new AutoResetEvent(false);

        private readonly AutoResetEvent _reconciliationWake = new AutoResetEvent(false);

        private readonly object _reconciliationLocker = new object();

        private readonly Queue<KeyValuePair<string, long>> _reconciliationQueue = new Queue<KeyValuePair<string, long>>();

        private readonly HashSet<string> _reconciliationPending = new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<string, FinamStreamLease> _streams = new Dictionary<string, FinamStreamLease>();

        #endregion

        #region Reading messages from data streams

        private async Task ReadJwtStreamAsync(FinamStreamLease lease, AsyncServerStreamingCall<SubscribeJwtRenewalResponse> call)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(lease.Cancellation.Token).ConfigureAwait(false))
                {
                    if (IsLeaseCurrent(lease) == false)
                    {
                        break;
                    }

                    lease.LastMessageUtc = DateTime.UtcNow;
                    UpdateRenewedToken(lease.Generation, call.ResponseStream.Current.Token);
                }
            }
            catch (OperationCanceledException error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            finally
            {
                RequestStreamRestart(lease);
            }
        }

        private async Task ReadAccountStreamAsync(FinamStreamLease lease, AsyncServerStreamingCall<GetAccountResponse> call)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(lease.Cancellation.Token).ConfigureAwait(false))
                {
                    lease.LastMessageUtc = DateTime.UtcNow;
                    UpdatePortfolio(call.ResponseStream.Current, lease.Generation);
                }
            }
            catch (OperationCanceledException error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            finally
            {
                RequestStreamRestart(lease);
            }
        }

        private async Task ReadOrdersStreamAsync(FinamStreamLease lease, AsyncServerStreamingCall<SubscribeOrdersResponse> call)
        {
            DateTime streamStartMoscow = MoscowNow();

            try
            {
                while (await call.ResponseStream.MoveNext(lease.Cancellation.Token).ConfigureAwait(false))
                {
                    List<Order> ordersToEmit = new List<Order>();

                    lock (_sessionLocker)
                    {
                        if (IsLeaseCurrent(lease) == false)
                        {
                            break;
                        }

                        lease.LastMessageUtc = DateTime.UtcNow;
                        SubscribeOrdersResponse response = call.ResponseStream.Current;

                        for (int i = 0; i < response.Orders.Count; i++)
                        {
                            // снапшот при старте стрима тащит старые финальные заявки — пропускаем их
                            if (response.Orders[i].Status != null && IsFinalOrderStatus(response.Orders[i])
                                && response.Orders[i].TransactAt != null
                                && ToMoscow(response.Orders[i].TransactAt) < streamStartMoscow)
                            {
                                continue;
                            }

                            Order order = ConvertOrder(response.Orders[i]);

                            if (order != null && EmitOrderCore(order))
                            {
                                ordersToEmit.Add(order);
                            }
                        }
                    }

                    for (int i = 0; i < ordersToEmit.Count; i++)
                    {
                        MyOrderEvent?.Invoke(ordersToEmit[i]);
                    }
                }
            }
            catch (OperationCanceledException error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            finally
            {
                RequestStreamRestart(lease);
            }
        }

        private async Task ReadPrivateTradesStreamAsync(FinamStreamLease lease, AsyncServerStreamingCall<SubscribeTradesResponse> call)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(lease.Cancellation.Token).ConfigureAwait(false))
                {
                    List<MyTrade> tradesToEmit = new List<MyTrade>();

                    lock (_sessionLocker)
                    {
                        if (IsLeaseCurrent(lease) == false)
                        {
                            break;
                        }

                        lease.LastMessageUtc = DateTime.UtcNow;
                        SubscribeTradesResponse response = call.ResponseStream.Current;

                        for (int i = 0; i < response.Trades.Count; i++)
                        {
                            MyTrade trade = EmitMyTradeCore(response.Trades[i], lease.AccountId);

                            if (trade != null)
                            {
                                tradesToEmit.Add(trade);
                            }
                        }
                    }

                    for (int i = 0; i < tradesToEmit.Count; i++)
                    {
                        MyTradeEvent?.Invoke(tradesToEmit[i]);
                    }
                }
            }
            catch (OperationCanceledException error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            finally
            {
                RequestStreamRestart(lease);
            }
        }

        private async Task ReadPublicTradesStreamAsync(FinamStreamLease lease, AsyncServerStreamingCall<SubscribeLatestTradesResponse> call)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(lease.Cancellation.Token).ConfigureAwait(false))
                {
                    List<Trade> tradesToEmit = new List<Trade>();

                    lock (_sessionLocker)
                    {
                        if (IsLeaseCurrent(lease) == false)
                        {
                            break;
                        }

                        lease.LastMessageUtc = DateTime.UtcNow;
                        SubscribeLatestTradesResponse response = call.ResponseStream.Current;

                        if (string.IsNullOrEmpty(response.Symbol) == false
                            && string.Equals(response.Symbol, lease.Key, StringComparison.OrdinalIgnoreCase) == false)
                        {
                            continue;
                        }

                        for (int i = 0; i < response.Trades.Count; i++)
                        {
                            Trade trade = BuildPublicTrade(response.Trades[i], lease.Security);

                            if (trade != null)
                            {
                                tradesToEmit.Add(trade);
                            }
                        }
                    }

                    for (int i = 0; i < tradesToEmit.Count; i++)
                    {
                        NewTradesEvent?.Invoke(tradesToEmit[i]);
                    }
                }
            }
            catch (OperationCanceledException error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            catch (RpcException error) when (lease.Cancellation.IsCancellationRequested == false && IsFatalStreamError(error.StatusCode))
            {
                SurrenderPublicStream(lease, error.StatusCode + ": " + error.Message);
            }
            catch (Exception error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            finally
            {
                RequestStreamRestart(lease);
            }
        }

        private async Task ReadOrderBookStreamAsync(FinamStreamLease lease, AsyncServerStreamingCall<SubscribeOrderBookResponse> call)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(lease.Cancellation.Token).ConfigureAwait(false))
                {
                    List<MarketDepth> depthsToEmit = new List<MarketDepth>();
                    bool failed = false;

                    lock (_sessionLocker)
                    {
                        if (IsLeaseCurrent(lease) == false)
                        {
                            break;
                        }

                        lease.LastMessageUtc = DateTime.UtcNow;
                        SubscribeOrderBookResponse response = call.ResponseStream.Current;

                        for (int i = 0; i < response.OrderBook.Count; i++)
                        {
                            StreamOrderBook source = response.OrderBook[i];

                            if (string.Equals(source.Symbol, lease.Key, StringComparison.OrdinalIgnoreCase) == false)
                            {
                                continue;
                            }

                            bool applyFailed;
                            MarketDepth depth = ApplyOrderBook(source, lease.Security, out applyFailed);

                            if (applyFailed)
                            {
                                failed = true;
                                break;
                            }

                            if (depth != null)
                            {
                                depthsToEmit.Add(depth);
                            }
                        }
                    }

                    if (failed)
                    {
                        return;
                    }

                    for (int i = 0; i < depthsToEmit.Count; i++)
                    {
                        MarketDepthEvent?.Invoke(depthsToEmit[i]);
                    }
                }
            }
            catch (OperationCanceledException error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            catch (RpcException error) when (lease.Cancellation.IsCancellationRequested == false && IsFatalStreamError(error.StatusCode))
            {
                SurrenderPublicStream(lease, error.StatusCode + ": " + error.Message);
            }
            catch (Exception error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            finally
            {
                RequestStreamRestart(lease);
            }
        }

        #region Quote stream (L1, volumes, option greeks)

        private const string QuoteStreamKey = "quotes";

        private bool _quoteSyncDirty;

        private DateTime _quoteSyncNotBefore = DateTime.MinValue;

        private string _quoteActiveSignature;

        private string _lastQuoteStreamError;

        private readonly object _quoteVolumeLocker = new object();

        private readonly Dictionary<string, DateTime> _quoteVolumeLastSent = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private void RequestQuoteSync()
        {
            lock (_streamLocker)
            {
                // дебаунс: при массовой подписке пересобираем стрим один раз после затихания
                _quoteSyncDirty = true;
                _quoteSyncNotBefore = DateTime.UtcNow.AddMilliseconds(1500);
            }

            _streamWake.Set();
        }

        private void SyncQuoteStream()
        {
            try
            {
                if (_rootCancellation == null)
                {
                    return;
                }

                List<string> symbols = GetQuoteDesiredSymbols();
                string signature = string.Join(";", symbols);

                lock (_streamLocker)
                {
                    if (_quoteSyncDirty == false)
                    {
                        return;
                    }

                    if (string.Equals(_quoteActiveSignature, signature, StringComparison.Ordinal))
                    {
                        FinamStreamLease existing;
                        string existingId = StreamId(FinamStreamKind.Quote, QuoteStreamKey);

                        if (symbols.Count == 0
                            || (_streams.TryGetValue(existingId, out existing) && existing.RestartRequested == false))
                        {
                            _quoteSyncDirty = false;
                            return;
                        }
                    }

                    _quoteSyncDirty = false;
                    _quoteActiveSignature = signature;
                }

                StopStream(FinamStreamKind.Quote, QuoteStreamKey);

                if (symbols.Count > 0 && StartStream(FinamStreamKind.Quote, QuoteStreamKey, null, null) == false)
                {
                    // не удалось открыть — пробуем ещё раз с паузой, не роняя сервер
                    lock (_streamLocker)
                    {
                        _quoteActiveSignature = null;
                        _quoteSyncDirty = true;
                        _quoteSyncNotBefore = DateTime.UtcNow.AddSeconds(15);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<string> GetQuoteDesiredSymbols()
        {
            List<string> symbols = new List<string>();

            lock (_streamLocker)
            {
                foreach (KeyValuePair<string, Security> pair in _subscribedSecurities)
                {
                    if (HasMarketDataAccess(pair.Value.Exchange, false))
                    {
                        symbols.Add(pair.Key);
                    }
                }
            }

            symbols.Sort(StringComparer.Ordinal);
            return symbols;
        }

        private async Task ReadQuoteStreamAsync(FinamStreamLease lease, AsyncServerStreamingCall<SubscribeQuoteResponse> call)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(lease.Cancellation.Token).ConfigureAwait(false))
                {
                    List<SecurityVolumes> volumesToEmit = new List<SecurityVolumes>();
                    List<OptionMarketDataForConnector> optionsToEmit = new List<OptionMarketDataForConnector>();
                    List<MarketDepth> depthsToEmit = new List<MarketDepth>();

                    lock (_sessionLocker)
                    {
                        if (IsLeaseCurrent(lease) == false)
                        {
                            break;
                        }

                        lease.LastMessageUtc = DateTime.UtcNow;
                        SubscribeQuoteResponse response = call.ResponseStream.Current;

                        // ошибки приходят внутри потока, без обрыва RPC — логируем один раз на текст
                        if (response.Error != null)
                        {
                            string errorText = response.Error.Code + ": " + response.Error.Description;

                            if (string.Equals(_lastQuoteStreamError, errorText, StringComparison.Ordinal) == false)
                            {
                                _lastQuoteStreamError = errorText;
                                SendLogMessage("Finam quote stream error: " + errorText, LogMessageType.Error);
                            }
                        }

                        for (int i = 0; i < response.Quote.Count; i++)
                        {
                            ProcessQuote(response.Quote[i], volumesToEmit, optionsToEmit, depthsToEmit);
                        }
                    }

                    for (int i = 0; i < volumesToEmit.Count; i++)
                    {
                        Volume24hUpdateEvent?.Invoke(volumesToEmit[i]);
                    }

                    for (int i = 0; i < optionsToEmit.Count; i++)
                    {
                        AdditionalMarketDataEvent?.Invoke(optionsToEmit[i]);
                    }

                    for (int i = 0; i < depthsToEmit.Count; i++)
                    {
                        MarketDepthEvent?.Invoke(depthsToEmit[i]);
                    }
                }
            }
            catch (OperationCanceledException error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            catch (RpcException error) when (lease.Cancellation.IsCancellationRequested == false && IsFatalStreamError(error.StatusCode))
            {
                SurrenderPublicStream(lease, error.StatusCode + ": " + error.Message);
            }
            catch (Exception error)
            {
                if (lease.Cancellation.IsCancellationRequested == false)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
            finally
            {
                RequestStreamRestart(lease);
            }
        }

        private void ProcessQuote(Quote quote, List<SecurityVolumes> volumesToEmit,
            List<OptionMarketDataForConnector> optionsToEmit, List<MarketDepth> depthsToEmit)
        {
            if (quote == null || string.IsNullOrEmpty(quote.Symbol))
            {
                return;
            }

            Security security = ResolveSecurity(quote.Symbol);

            if (security == null)
            {
                return;
            }

            DateTime quoteTime = quote.Timestamp != null ? ToMoscow(quote.Timestamp) : MoscowNow();

            // дневной оборот для скринеров, не чаще раза в секунду на бумагу
            if (quote.Turnover != null && string.IsNullOrEmpty(quote.Turnover.Value) == false)
            {
                bool volumeDue;

                lock (_quoteVolumeLocker)
                {
                    DateTime lastSent;
                    volumeDue = _quoteVolumeLastSent.TryGetValue(security.NameId, out lastSent) == false
                        || DateTime.UtcNow - lastSent >= TimeSpan.FromSeconds(1);

                    if (volumeDue)
                    {
                        _quoteVolumeLastSent[security.NameId] = DateTime.UtcNow;
                    }
                }

                if (volumeDue)
                {
                    SecurityVolumes volumes = new SecurityVolumes();
                    volumes.SecurityNameCode = security.Name;
                    volumes.Volume24h = ToDecimal(quote.Turnover);
                    volumes.TimeUpdate = quoteTime;
                    volumesToEmit.Add(volumes);
                }
            }

            // греки и IV по опционам
            if (security.SecurityType == SecurityType.Option && quote.Option != null)
            {
                OptionMarketDataForConnector data = new OptionMarketDataForConnector();
                data.SecurityName = security.Name;
                data.UnderlyingAsset = security.UnderlyingAsset;
                data.Delta = quote.Option.Delta != null ? quote.Option.Delta.Value : null;
                data.Gamma = quote.Option.Gamma != null ? quote.Option.Gamma.Value : null;
                data.Vega = quote.Option.Vega != null ? quote.Option.Vega.Value : null;
                data.Theta = quote.Option.Theta != null ? quote.Option.Theta.Value : null;
                data.Rho = quote.Option.Rho != null ? quote.Option.Rho.Value : null;
                data.MarkIV = quote.Option.ImpliedVolatility != null ? quote.Option.ImpliedVolatility.Value : null;
                data.MarkPrice = quote.Option.TheoreticalPrice != null ? quote.Option.TheoreticalPrice.Value : null;
                data.OpenInterest = quote.Option.OpenInterest != null
                    ? quote.Option.OpenInterest.Value
                    : quote.OpenInterest != null ? quote.OpenInterest.Value : null;
                data.TimeCreate = quoteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                optionsToEmit.Add(data);
            }

            // L1-фолбэк: прав на полный стакан у токена нет — синтезируем один уровень из котировки
            if (HasMarketDataAccess(security.Exchange, true) == false)
            {
                decimal bid = ToDecimal(quote.Bid);
                decimal ask = ToDecimal(quote.Ask);

                // 0 означает отсутствие стороны (по доке API) — такой стакан не отдаём
                if (bid > 0 && ask > 0)
                {
                    decimal lotDivisor = LotDivisor(security);

                    MarketDepth depth = new MarketDepth();
                    depth.SecurityNameCode = security.Name;
                    depth.Time = MonotonicDepthTime(security.NameId, quoteTime);

                    MarketDepthLevel bidLevel = new MarketDepthLevel();
                    bidLevel.Price = (double)bid;
                    bidLevel.Bid = (double)(ToDecimal(quote.BidSize) / lotDivisor);
                    depth.Bids.Add(bidLevel);

                    MarketDepthLevel askLevel = new MarketDepthLevel();
                    askLevel.Price = (double)ask;
                    askLevel.Ask = (double)(ToDecimal(quote.AskSize) / lotDivisor);
                    depth.Asks.Add(askLevel);

                    depthsToEmit.Add(depth);
                }
            }
        }

        private DateTime MonotonicDepthTime(string nameId, DateTime time)
        {
            lock (_depthTimeLocker)
            {
                DateTime previous;

                if (_lastDepthTimes.TryGetValue(nameId, out previous) && time <= previous)
                {
                    time = previous.AddTicks(1);
                }

                _lastDepthTimes[nameId] = time;
                return time;
            }
        }

        #endregion

        private Trade BuildPublicTrade(FTrade source, Security security)
        {
            if (source == null || security == null || source.Price == null || source.Timestamp == null)
            {
                return null;
            }

            // снапшот исторических сделок при старте стрима в движок не отдаём
            if (source.IsDataSnapshot)
            {
                return null;
            }

            DateTime tradeTime = ToMoscow(source.Timestamp);

            if (IsMorningAuctionFiltered(security, tradeTime))
            {
                return null;
            }

            decimal size = ToDecimal(source.Size);
            string fingerprint = security.NameId + "|" + source.Timestamp.ToDateTime().Ticks.ToString(CultureInfo.InvariantCulture)
                + "|" + ToDecimal(source.Price).ToString(CultureInfo.InvariantCulture)
                + "|" + size.ToString(CultureInfo.InvariantCulture) + "|" + source.Side;
            string id;

            if (TryPassPublicTrade(security.NameId, fingerprint, source.TradeId, out id) == false)
            {
                return null;
            }

            Trade trade = new Trade();
            trade.SecurityNameCode = security.Name;
            trade.Price = ToDecimal(source.Price);
            trade.Volume = size / LotDivisor(security);
            trade.Time = tradeTime;
            trade.Id = id;
            trade.Side = ConvertSide(source.Side);
            return trade;
        }

        private bool IsMorningAuctionFiltered(Security security, DateTime tradeTime)
        {
            if (ServerParameters == null || ServerParameters.Count <= 2
                || ((ServerParameterBool)ServerParameters[2]).Value == false)
            {
                return false;
            }

            if (security.SecurityType == SecurityType.Stock)
            {
                return tradeTime.Hour < 7;
            }

            if (security.SecurityType == SecurityType.Futures
                || security.SecurityType == SecurityType.Option)
            {
                return tradeTime.Hour < 9;
            }

            return false;
        }

        private bool TryPassPublicTrade(string symbol, string fingerprint, string tradeId, out string outputId)
        {
            lock (_dedupLocker)
            {
                string canonicalKey = symbol + "|fingerprint|" + fingerprint;
                TradeOccurrenceState state;

                if (_publicTradeStates.TryGetValue(canonicalKey, out state) == false)
                {
                    state = new TradeOccurrenceState();
                    _publicTradeStates.Add(canonicalKey, state);
                }

                state.UpdatedUtc = DateTime.UtcNow;
                TrimTradeStates(_publicTradeStates);
                return TryPassOccurrenceTrade(_publicTradeDedup, symbol, fingerprint, tradeId, state, out outputId);
            }
        }

        private MarketDepth ApplyOrderBook(StreamOrderBook source, Security security, out bool failed)
        {
            failed = false;

            FinamOrderBook book = _orderBooks.GetOrAdd(security.NameId, CreateOrderBook);
            List<FinamOrderBookRow> rows = new List<FinamOrderBookRow>();
            DateTime latest = DateTime.MinValue;

            for (int i = 0; i < source.Rows.Count; i++)
            {
                StreamOrderBook.Types.Row row = source.Rows[i];

                if (row.Price == null || row.SideCase == StreamOrderBook.Types.Row.SideOneofCase.None)
                {
                    failed = true;
                    return null;
                }

                FinamOrderBookRow converted = new FinamOrderBookRow();
                converted.Price = ToDecimal(row.Price);
                converted.IsBid = row.SideCase == StreamOrderBook.Types.Row.SideOneofCase.BuySize;
                converted.Size = converted.IsBid ? ToDecimal(row.BuySize) : ToDecimal(row.SellSize);
                converted.Action = ConvertBookAction(row.Action);
                converted.Mpid = row.Mpid;
                rows.Add(converted);

                if (row.Timestamp != null)
                {
                    DateTime rowTime = ToMoscow(row.Timestamp);

                    if (rowTime > latest)
                    {
                        latest = rowTime;
                    }
                }
            }

            bool applied;

            if (source.IsDataSnapshot)
            {
                applied = book.ApplySnapshot(rows);
            }
            else
            {
                applied = book.ApplyDelta(rows);
            }

            if (applied == false)
            {
                failed = true;
                return null;
            }

            if (latest == DateTime.MinValue)
            {
                latest = MoscowNow();
            }

            latest = MonotonicDepthTime(security.NameId, latest);

            MarketDepth depth = book.CreateDepth(security.Name, latest, LotDivisor(security), IsFullDepth());

            // пустые стаканы не отдаём (гайдлайн 7.3)
            if (depth.Bids == null || depth.Asks == null
                || depth.Bids.Count == 0 || depth.Asks.Count == 0)
            {
                return null;
            }

            return depth;
        }

        private FinamOrderBook CreateOrderBook(string key)
        {
            return new FinamOrderBook();
        }

        private FinamOrderBookAction ConvertBookAction(StreamOrderBook.Types.Row.Types.Action action)
        {
            if (action == StreamOrderBook.Types.Row.Types.Action.Remove)
            {
                return FinamOrderBookAction.Remove;
            }

            if (action == StreamOrderBook.Types.Row.Types.Action.Add)
            {
                return FinamOrderBookAction.Add;
            }

            if (action == StreamOrderBook.Types.Row.Types.Action.Update)
            {
                return FinamOrderBookAction.Update;
            }

            return FinamOrderBookAction.Unspecified;
        }

        private bool _fullLogFlagFileExists;

        private DateTime _fullLogFlagCheckUtc = DateTime.MinValue;

        private bool IsFullLog()
        {
            // флаг-файл надёжнее параметра: имя параметра локализовано и через MCP не задаётся.
            // File.Exists — syscall, на горячем пути (каждый тик/ордер) дёргаем его не чаще раза в 5 секунд
            if (DateTime.UtcNow - _fullLogFlagCheckUtc > TimeSpan.FromSeconds(5))
            {
                _fullLogFlagCheckUtc = DateTime.UtcNow;
                _fullLogFlagFileExists = File.Exists(@"Engine\FinamGrpcFullLog.flag");
            }

            if (_fullLogFlagFileExists)
            {
                return true;
            }

            if (ServerParameters != null && ServerParameters.Count > 3)
            {
                return ((ServerParameterBool)ServerParameters[3]).Value;
            }

            return false;
        }

        private bool IsFullDepth()
        {
            if (ServerParameters != null)
            {
                IServerParameter parameter = ServerParameters.Find(FindFullDepthParameter);

                if (parameter is ServerParameterBool)
                {
                    return ((ServerParameterBool)parameter).Value;
                }
            }

            return true;
        }

        private bool FindFullDepthParameter(IServerParameter parameter)
        {
            return parameter.Name == OsLocalization.Market.ServerParam10;
        }

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        private readonly object _depthTimeLocker = new object();

        private readonly ConcurrentDictionary<string, FinamOrderBook> _orderBooks = new ConcurrentDictionary<string, FinamOrderBook>();

        private readonly Dictionary<string, DateTime> _lastDepthTimes = new Dictionary<string, DateTime>();

        #endregion

        #region Trade

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public void SendOrder(Order order)
        {
            if (order == null)
            {
                SendLogMessage("Finam order is null.", LogMessageType.Error);
                return;
            }

            string clientOrderId = null;
            long generation = _generation;

            try
            {
                string validation = ValidateOrder(order);

                if (validation != null)
                {
                    SendLogMessage(validation, LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    EmitOrder(order, generation);
                    return;
                }

                Security security = ResolveSecurity(order.SecurityNameCode);
                string accountId = ResolveOrderAccount(order.PortfolioNumber);
                order.PortfolioNumber = accountId;
                clientOrderId = GetClientOrderId(order.NumberUser);

                // запоминаем TIF, который реально уйдёт на биржу; маркет шлём с Unspecified — не запоминаем,
                // чтобы ResolveTimeInForce честно вернул Unspecified
                if (order.TypeOrder != OrderPriceType.Market)
                {
                    _orderTimeInForce[accountId + "|" + clientOrderId] = order.OrderTypeTime;
                }

                _orderTypes[accountId + "|" + clientOrderId] = order.TypeOrder;
                FOrder request = CreateOrderRequest(order, security, accountId, clientOrderId);

                WaitRateGate("PlaceOrder");

                if (IsFullLog())
                {
                    SendLogMessage("Finam PlaceOrder request: " + request.ToString(), LogMessageType.System);
                }

                OrderState response = _ordersClient.PlaceOrder(request, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));

                if (IsFullLog())
                {
                    SendLogMessage("Finam PlaceOrder response: " + (response == null ? "null" : response.ToString()), LogMessageType.System);
                }

                if (response == null)
                {
                    ReconcileAmbiguousSubmission(order, accountId, clientOrderId, generation);
                    return;
                }

                EmitOrder(response, generation);
            }
            catch (RpcException error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);

                if (IsDefinitiveRejection(error))
                {
                    order.State = OrderStateType.Fail;
                    EmitOrder(order, generation);
                }
                else
                {
                    string accountId = ResolveOrderAccount(order.PortfolioNumber);
                    ReconcileAmbiguousSubmission(order, accountId, clientOrderId ?? GetClientOrderId(order.NumberUser), generation);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);

                if (clientOrderId == null)
                {
                    order.State = OrderStateType.Fail;
                    EmitOrder(order, generation);
                }
                else
                {
                    string accountId = ResolveOrderAccount(order.PortfolioNumber);
                    ReconcileAmbiguousSubmission(order, accountId, clientOrderId, generation);
                }
            }
        }

        private FOrder CreateOrderRequest(Order order, Security security, string accountId, string clientOrderId)
        {
            FOrder request = new FOrder();
            request.AccountId = accountId;
            request.Symbol = security.NameId;
            request.Quantity = new GDecimal { Value = (order.Volume * LotDivisor(security)).ToString(CultureInfo.InvariantCulture) };
            request.Side = ConvertSide(order.Side);
            request.ClientOrderId = clientOrderId;

            if (order.TypeOrder == OrderPriceType.Limit)
            {
                request.Type = OrderType.Limit;
                request.LimitPrice = new GDecimal { Value = order.Price.ToString(CultureInfo.InvariantCulture) };
                request.TimeInForce = ConvertTimeInForce(order.OrderTypeTime);
            }
            else
            {
                // маркет-заявке TIF не задаём: на FORTS Финам конвертирует MARKET+DAY в лимитку
                // с защитной ценой, а GTC для маркета бессмыслен — Unspecified даёт дефолт брокера
                request.Type = OrderType.Market;
                request.TimeInForce = Grpc.Tradeapi.V1.Orders.TimeInForce.Unspecified;
            }

            return request;
        }

        private Grpc.Tradeapi.V1.Orders.TimeInForce ConvertTimeInForce(OrderTypeTime orderTypeTime)
        {
            if (orderTypeTime == OrderTypeTime.GTC)
            {
                return Grpc.Tradeapi.V1.Orders.TimeInForce.GoodTillCancel;
            }

            if (orderTypeTime == OrderTypeTime.Day)
            {
                return Grpc.Tradeapi.V1.Orders.TimeInForce.Day;
            }

            return Grpc.Tradeapi.V1.Orders.TimeInForce.Unspecified;
        }

        private string ValidateOrder(Order order)
        {
            if (order == null)
            {
                return "Finam order is null.";
            }

            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return "Finam order rejected because the server is disconnected.";
            }

            if (_readOnly)
            {
                return "Finam order rejected because the token is readonly.";
            }

            if (order.NumberUser <= 0 || order.Volume <= 0)
            {
                return "Finam order rejected because its number or volume is invalid.";
            }

            if (order.TypeOrder != OrderPriceType.Market && order.TypeOrder != OrderPriceType.Limit)
            {
                return "Finam supports only market and limit orders.";
            }

            if (order.Side != Side.Buy && order.Side != Side.Sell)
            {
                return "Finam order side is invalid.";
            }

            Security security = ResolveSecurity(order.SecurityNameCode, order.SecurityClassCode);

            if (security == null)
            {
                return "Finam order security was not found: " + order.SecurityNameCode + ". Check that its class is enabled in the server parameters.";
            }

            EnrichSecurity(security);

            if (order.TypeOrder == OrderPriceType.Limit && IsLimitPriceValid(security, order.Price) == false)
            {
                return "Finam limit order price is not allowed for this security.";
            }

            if (security.State == SecurityStateType.Close)
            {
                return "Finam order security is not tradable.";
            }

            if (order.Volume * LotDivisor(security) <= 0)
            {
                return "Finam order quantity is invalid.";
            }

            string account = ResolveOrderAccount(order.PortfolioNumber);

            if (account == null)
            {
                return "Finam order account is not selected.";
            }

            return null;
        }

        private bool IsLimitPriceValid(Security security, decimal price)
        {
            lock (_enrichLocker)
            {
                PriceType priceType;

                if (_priceTypes.TryGetValue(security.NameId, out priceType) == false)
                {
                    priceType = PriceType.Unknown;
                }

                if (priceType == PriceType.Any)
                {
                    return true;
                }

                if (priceType == PriceType.NonNegative)
                {
                    return price >= 0;
                }

                return price > 0;
            }
        }

        private string ResolveOrderAccount(string portfolioNumber)
        {
            List<string> accounts = _accountIds;

            if (string.IsNullOrEmpty(portfolioNumber) && accounts.Count == 1)
            {
                return accounts[0];
            }

            for (int i = 0; i < accounts.Count; i++)
            {
                if (accounts[i] == portfolioNumber)
                {
                    return portfolioNumber;
                }
            }

            return null;
        }

        private string GetClientOrderId(int numberUser)
        {
            lock (_orderMappingLocker)
            {
                string existing;

                if (_clientIdByNumberUser.TryGetValue(numberUser, out existing))
                {
                    return existing;
                }

                string id = _uniquePrefix + numberUser.ToString(CultureInfo.InvariantCulture);

                if (id.Length > 20)
                {
                    throw new InvalidOperationException("Finam client order ID exceeds 20 characters.");
                }

                _clientIdByNumberUser[numberUser] = id;
                return id;
            }
        }

        private bool IsDefinitiveRejection(RpcException error)
        {
            return error.StatusCode == StatusCode.InvalidArgument
                || error.StatusCode == StatusCode.FailedPrecondition
                || error.StatusCode == StatusCode.PermissionDenied
                || error.StatusCode == StatusCode.NotFound
                || error.StatusCode == StatusCode.OutOfRange;
        }

        private void ReconcileAmbiguousSubmission(Order original, string accountId, string clientOrderId, long generation)
        {
            OrderState found = null;

            try
            {
                if (accountId != null && clientOrderId != null)
                {
                    List<OrderState> states = GetOrderStates(accountId);

                    if (generation != _generation)
                    {
                        return;
                    }

                    for (int i = 0; i < states.Count; i++)
                    {
                        if (states[i].Order != null && states[i].Order.ClientOrderId == clientOrderId)
                        {
                            found = states[i];
                            break;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            if (found != null)
            {
                EmitOrder(found, generation);
                return;
            }

            SendLogMessage("Finam order transport result is unresolved. Order left Pending and was not resubmitted. Client ID: " + clientOrderId, LogMessageType.Error);
            original.PortfolioNumber = accountId;
            original.State = OrderStateType.Pending;
            EmitOrder(original, generation);
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                if (order == null || string.IsNullOrEmpty(order.NumberMarket))
                {
                    return false;
                }

                string accountId = ResolveOrderAccount(order.PortfolioNumber);

                if (accountId == null)
                {
                    return false;
                }

                long generation = _generation;
                WaitRateGate("CancelOrder");
                OrderState response = _ordersClient.CancelOrder(new CancelOrderRequest
                {
                    AccountId = accountId,
                    OrderId = GetExchangeOrderId(order.NumberMarket)
                }, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));

                if (response == null)
                {
                    return false;
                }

                EmitOrder(response, generation);
                return true;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllActiveOrdersInternal();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (IsCancelable(orders[i]))
                {
                    CancelOrder(orders[i]);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            if (security == null)
            {
                return;
            }

            List<Order> orders = GetAllActiveOrdersInternal();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                Security resolved = ResolveSecurity(orders[i].SecurityNameCode);

                if (IsCancelable(orders[i]) && resolved != null
                    && (resolved.NameId == security.NameId || resolved.Name == security.Name))
                {
                    CancelOrder(orders[i]);
                }
            }
        }

        private bool IsCancelable(Order order)
        {
            return order != null && (order.State == OrderStateType.Active
                || order.State == OrderStateType.Partial
                || order.State == OrderStateType.Pending);
        }

        public void GetAllActivOrders()
        {
            long generation = _generation;
            List<Order> orders = GetAllActiveOrdersInternal();

            if (orders == null)
            {
                return;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (IsCancelable(orders[i]))
                {
                    EmitOrder(orders[i], generation);
                }
            }
        }

        private List<Order> GetAllActiveOrdersInternal()
        {
            try
            {
                List<Order> result = new List<Order>();
                List<string> accounts = new List<string>(_accountIds);
                long generation = _generation;

                for (int i = 0; i < accounts.Count; i++)
                {
                    List<OrderState> states = GetOrderStates(accounts[i]);

                    if (generation != _generation)
                    {
                        return null;
                    }

                    for (int j = 0; j < states.Count; j++)
                    {
                        Order order = ConvertOrder(states[j]);

                        if (IsCancelable(order))
                        {
                            result.Add(order);
                        }
                    }
                }

                result.Sort(CompareOrdersDescending);
                return result;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private int CompareOrdersDescending(Order first, Order second)
        {
            return second.TimeCallBack.CompareTo(first.TimeCallBack);
        }

        private bool IsFinalOrderStatus(OrderState state)
        {
            OrderStateType converted = ConvertOrderState(state.Status, ResolveTimeInForce(state.Order));
            return converted == OrderStateType.Done || converted == OrderStateType.Cancel || converted == OrderStateType.Fail;
        }

        private List<OrderState> GetOrderStates(string accountId)
        {
            WaitRateGate("GetOrders");
            OrdersResponse response = _ordersClient.GetOrders(new OrdersRequest
            {
                AccountId = accountId
            }, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));

            List<OrderState> result = new List<OrderState>();

            if (response != null)
            {
                for (int i = 0; i < response.Orders.Count; i++)
                {
                    result.Add(response.Orders[i]);
                }
            }

            return result;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            try
            {
                if (order == null)
                {
                    return OrderStateType.None;
                }

                string accountId = ResolveOrderAccount(order.PortfolioNumber);

                if (accountId == null)
                {
                    return OrderStateType.None;
                }

                string exchangeOrderId = GetExchangeOrderId(order.NumberMarket);

                if (string.IsNullOrEmpty(exchangeOrderId))
                {
                    // хаб не видел Active-событие и не знает биржевой номер — ищем по нашему clientOrderId
                    exchangeOrderId = FindExchangeOrderIdByUserNumber(accountId, order.NumberUser);

                    if (exchangeOrderId == null)
                    {
                        return OrderStateType.None;
                    }
                }

                long generation = _generation;
                WaitRateGate("GetOrder");
                OrderState response = _ordersClient.GetOrder(new GetOrderRequest
                {
                    AccountId = accountId,
                    OrderId = exchangeOrderId
                }, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));

                if (generation != _generation)
                {
                    return OrderStateType.None;
                }

                Order converted = ConvertOrder(response);

                if (converted != null)
                {
                    // явный запрос статуса от хаба — эмитим всегда, минуя дедуп (хаб реагирует только на события)
                    EmitOrderForce(converted, generation);
                    ReconcileTrades(accountId, exchangeOrderId, order.TimeCreate, generation, true);
                    return converted.State;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        private string FindExchangeOrderIdByUserNumber(string accountId, int numberUser)
        {
            string clientId;

            lock (_orderMappingLocker)
            {
                if (_clientIdByNumberUser.TryGetValue(numberUser, out clientId) == false)
                {
                    return null;
                }
            }

            List<OrderState> states = GetOrderStates(accountId);

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].Order != null && states[i].Order.ClientOrderId == clientId)
                {
                    return states[i].OrderId;
                }
            }

            return null;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            if (startIndex < 0 || count <= 0)
            {
                return new List<Order>();
            }

            List<Order> orders = GetAllActiveOrdersInternal();

            if (orders == null || startIndex >= orders.Count)
            {
                return new List<Order>();
            }

            int available = Math.Min(count, orders.Count - startIndex);
            return orders.GetRange(startIndex, available);
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        private Order ConvertOrder(OrderState source)
        {
            if (source == null || source.Order == null)
            {
                return null;
            }

            FOrder finamOrder = source.Order;

            if (finamOrder.Type != OrderType.Market && finamOrder.Type != OrderType.Limit)
            {
                return null;
            }

            int numberUser;

            if (ResolveNumberUser(finamOrder.AccountId, finamOrder.ClientOrderId, out numberUser) == false)
            {
                return null;
            }

            Security security = ResolveSecurity(finamOrder.Symbol);

            if (security == null)
            {
                return null;
            }

            Order order = new Order();
            order.NumberUser = numberUser;
            order.NumberMarket = GetMarketOrderId(finamOrder.AccountId, source.OrderId);
            order.PortfolioNumber = finamOrder.AccountId;
            order.SecurityNameCode = security.Name;
            order.SecurityClassCode = security.NameClass;
            order.ServerType = ServerType.FinamGrpc;
            order.Volume = ToDecimal(finamOrder.Quantity) / LotDivisor(security);
            order.VolumeExecute = ToDecimal(source.ExecutedQuantity) / LotDivisor(security);
            order.Side = ConvertSide(finamOrder.Side);
            order.State = ConvertOrderState(source.Status, ResolveTimeInForce(finamOrder));
            order.TypeOrder = ResolveOrderType(finamOrder);
            order.Price = ToDecimal(finamOrder.LimitPrice);

            if (source.TransactAt != null)
            {
                order.TimeCreate = ToMoscow(source.TransactAt);
                order.TimeCallBack = order.TimeCreate;
            }

            if (source.AcceptAt != null)
            {
                order.TimeCallBack = ToMoscow(source.AcceptAt);
            }

            if (source.WithdrawAt != null)
            {
                order.TimeCancel = ToMoscow(source.WithdrawAt);
                order.TimeCallBack = order.TimeCancel;
            }

            if (order.State == OrderStateType.Fail && source.StatusDescription != null)
            {
                SendLogMessage("Finam order failed: " + source.StatusDescription, LogMessageType.Error);
            }

            return order;
        }

        private bool ResolveNumberUser(string accountId, string clientOrderId, out int numberUser)
        {
            numberUser = 0;

            if (string.IsNullOrEmpty(clientOrderId))
            {
                return false;
            }

            string key = (accountId ?? "") + "|" + clientOrderId;

            lock (_orderMappingLocker)
            {
                if (_numberUserByClientId.TryGetValue(key, out numberUser))
                {
                    return true;
                }

                if (clientOrderId.StartsWith(_uniquePrefix, StringComparison.Ordinal))
                {
                    string suffix = clientOrderId.Substring(_uniquePrefix.Length);

                    if (int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out numberUser) && numberUser > 0)
                    {
                        _numberUserByClientId[key] = numberUser;
                        _clientIdByNumberUser[numberUser] = clientOrderId;
                        return true;
                    }

                    return false;
                }

                int legacyNumber;

                if (int.TryParse(clientOrderId, NumberStyles.Integer, CultureInfo.InvariantCulture, out legacyNumber) && legacyNumber > 0)
                {
                    // внешняя заявка (терминал Финама): даём ей номер из верхнего диапазона вниз,
                    // чтобы не пересечься с нумерацией движка (NumberGen: «1000-деньГода» + счётчик, до ~10^9)
                    numberUser = _nextLegacyNumberUser--;
                    _numberUserByClientId[key] = numberUser;
                    _clientIdByNumberUser[numberUser] = clientOrderId;
                    return true;
                }

                return false;
            }
        }

        private Order EmitOrder(OrderState source, long? generation = null)
        {
            Order order = ConvertOrder(source);

            if (order != null)
            {
                EmitOrder(order, generation);
            }

            return order;
        }

        private void EmitOrder(Order order, long? generation = null)
        {
            bool shouldEmit;

            if (generation.HasValue)
            {
                lock (_sessionLocker)
                {
                    if (generation.Value != _generation)
                    {
                        return;
                    }

                    shouldEmit = EmitOrderCore(order);
                }
            }
            else
            {
                shouldEmit = EmitOrderCore(order);
            }

            if (shouldEmit)
            {
                MyOrderEvent?.Invoke(order);
            }
        }

        private void EmitOrderForce(Order order, long? generation = null)
        {
            if (order == null)
            {
                return;
            }

            if (generation.HasValue)
            {
                lock (_sessionLocker)
                {
                    if (generation.Value != _generation)
                    {
                        return;
                    }
                }
            }

            if (IsFullLog())
            {
                SendLogMessage("Finam emit order (forced): NumUser=" + order.NumberUser + " NumMarket=" + order.NumberMarket
                    + " Type=" + order.TypeOrder + " State=" + order.State + " Sec=" + order.SecurityNameCode, LogMessageType.System);
            }

            MyOrderEvent?.Invoke(order);
        }

        private bool EmitOrderCore(Order order)
        {
            if (order == null || order.NumberUser <= 0)
            {
                return false;
            }

            if (IsFullLog())
            {
                SendLogMessage("Finam emit order: NumUser=" + order.NumberUser + " NumMarket=" + order.NumberMarket
                    + " Type=" + order.TypeOrder + " State=" + order.State + " Sec=" + order.SecurityNameCode, LogMessageType.System);
            }

            string market = order.NumberMarket ?? "";
            string key = (order.PortfolioNumber ?? "") + "|client|" + GetClientOrderId(order.NumberUser);
            string fingerprint = market + "|" + order.State + "|" + order.VolumeExecute.ToString(CultureInfo.InvariantCulture)
                + "|" + order.TimeCreate.Ticks + "|" + order.TimeCallBack.Ticks + "|" + order.TimeCancel.Ticks + "|" + order.TimeDone.Ticks;

            lock (_dedupLocker)
            {
                if (IsStaleOrder(key, order))
                {
                    return false;
                }

                if (TryPassDedup(_orderDedup, key, fingerprint) == false)
                {
                    return false;
                }

                OrderProgress progress = new OrderProgress
                {
                    State = order.State,
                    VolumeExecute = order.VolumeExecute
                };

                if (order.State == OrderStateType.Done
                    || order.State == OrderStateType.Cancel
                    || order.State == OrderStateType.Fail)
                {
                    progress.FinalizedUtc = DateTime.UtcNow;
                }

                _orderProgress[key] = progress;
            }

            return true;
        }

        private DateTime _lastOrderMappingsSweepUtc = DateTime.MinValue;

        private void SweepFinalizedOrderMappings()
        {
            // раз в час вычищаем записи заявок, финализированных более суток назад:
            // живые GTC-заявки (включая DoneForDay→Active) не трогаем, их маппинги нужны завтра утром
            if (DateTime.UtcNow - _lastOrderMappingsSweepUtc < TimeSpan.FromHours(1))
            {
                return;
            }

            _lastOrderMappingsSweepUtc = DateTime.UtcNow;
            DateTime threshold = DateTime.UtcNow.AddDays(-1);
            List<string> finalizedKeys = new List<string>();

            lock (_dedupLocker)
            {
                foreach (KeyValuePair<string, OrderProgress> pair in _orderProgress)
                {
                    if (pair.Value.FinalizedUtc != DateTime.MinValue && pair.Value.FinalizedUtc < threshold)
                    {
                        finalizedKeys.Add(pair.Key);
                    }
                }

                for (int i = 0; i < finalizedKeys.Count; i++)
                {
                    _orderProgress.Remove(finalizedKeys[i]);
                }
            }

            lock (_orderMappingLocker)
            {
                for (int i = 0; i < finalizedKeys.Count; i++)
                {
                    // ключ прогресса: account|client|clientOrderId; ключи маппингов: account|clientOrderId
                    string mappingKey = finalizedKeys[i].Replace("|client|", "|");
                    int numberUser;

                    if (_numberUserByClientId.TryGetValue(mappingKey, out numberUser))
                    {
                        _numberUserByClientId.Remove(mappingKey);
                        _clientIdByNumberUser.Remove(numberUser);

                        OrderTypeTime removedTimeInForce;
                        _orderTimeInForce.TryRemove(mappingKey, out removedTimeInForce);

                        OrderPriceType removedType;
                        _orderTypes.TryRemove(mappingKey, out removedType);
                    }
                }
            }

            if (finalizedKeys.Count > 0)
            {
                SendLogMessage("Finam order mappings sweep: removed " + finalizedKeys.Count + " finalized order records.", LogMessageType.System);
            }
        }

        private bool IsStaleOrder(string key, Order order)
        {
            OrderProgress previous;

            if (_orderProgress.TryGetValue(key, out previous) == false)
            {
                return false;
            }

            if (order.VolumeExecute < previous.VolumeExecute)
            {
                return true;
            }

            bool previousFinal = previous.State == OrderStateType.Done
                || previous.State == OrderStateType.Cancel
                || previous.State == OrderStateType.Fail;

            if (previousFinal && order.State != previous.State)
            {
                return true;
            }

            if (previous.State == OrderStateType.Partial
                && (order.State == OrderStateType.Active || order.State == OrderStateType.Pending)
                && order.VolumeExecute <= previous.VolumeExecute)
            {
                return true;
            }

            return false;
        }

        private OrderStateType ConvertOrderState(OrderStatus status, Grpc.Tradeapi.V1.Orders.TimeInForce timeInForce)
        {
            switch (status)
            {
                case OrderStatus.New:
                case OrderStatus.Suspended:
                case OrderStatus.Watching:
                    return OrderStateType.Active;
                case OrderStatus.PendingNew:
                case OrderStatus.PendingCancel:
                case OrderStatus.Forwarding:
                case OrderStatus.Wait:
                case OrderStatus.LinkWait:
                case OrderStatus.SlGuardTime:
                case OrderStatus.SlForwarding:
                case OrderStatus.TpGuardTime:
                case OrderStatus.TpCorrection:
                case OrderStatus.TpForwarding:
                case OrderStatus.TpCorrGuardTime:
                    return OrderStateType.Pending;
                case OrderStatus.PartiallyFilled:
                    return OrderStateType.Partial;
                case OrderStatus.Filled:
                case OrderStatus.Executed:
                case OrderStatus.SlExecuted:
                case OrderStatus.TpExecuted:
                    return OrderStateType.Done;
                case OrderStatus.Canceled:
                case OrderStatus.Expired:
                case OrderStatus.Replaced:
                case OrderStatus.Disabled:
                    return OrderStateType.Cancel;
                case OrderStatus.DoneForDay:
                    // GTC-заявка в конце сессии не умирает: утром биржа реактивирует её с тем же order_id.
                    // Cancel хоронил бы живую заявку, а IsStaleOrder потом отбрасывал бы её реактивацию
                    if (timeInForce == Grpc.Tradeapi.V1.Orders.TimeInForce.GoodTillCancel)
                    {
                        return OrderStateType.Active;
                    }

                    return OrderStateType.Cancel;
                case OrderStatus.Rejected:
                case OrderStatus.DeniedByBroker:
                case OrderStatus.RejectedByExchange:
                case OrderStatus.Failed:
                    return OrderStateType.Fail;
                default:
                    return OrderStateType.None;
            }
        }

        private OrderPriceType ResolveOrderType(FOrder finamOrder)
        {
            // стрим Финама репортит срочные маркет-заявки как LIMIT (эмуляция защитной ценой на FORTS) —
            // для своих заявок тип берём из того, что реально отправляли; чужим верим эху
            OrderPriceType remembered;

            if (_orderTypes.TryGetValue((finamOrder.AccountId ?? "") + "|" + (finamOrder.ClientOrderId ?? ""), out remembered))
            {
                return remembered;
            }

            return finamOrder.Type == OrderType.Market ? OrderPriceType.Market : OrderPriceType.Limit;
        }

        private Grpc.Tradeapi.V1.Orders.TimeInForce ResolveTimeInForce(FOrder finamOrder)
        {
            if (finamOrder == null)
            {
                return Grpc.Tradeapi.V1.Orders.TimeInForce.Unspecified;
            }

            if (finamOrder.TimeInForce != Grpc.Tradeapi.V1.Orders.TimeInForce.Unspecified)
            {
                return finamOrder.TimeInForce;
            }

            // эха TIF от Финама нет — берём то время жизни, что отправляли сами при PlaceOrder
            OrderTypeTime remembered;

            if (_orderTimeInForce.TryGetValue((finamOrder.AccountId ?? "") + "|" + (finamOrder.ClientOrderId ?? ""), out remembered))
            {
                return ConvertTimeInForce(remembered);
            }

            return Grpc.Tradeapi.V1.Orders.TimeInForce.Unspecified;
        }

        private void EmitMyTrade(AccountTrade source, string accountId, long? generation = null, bool force = false)
        {
            MyTrade trade;

            if (generation.HasValue)
            {
                lock (_sessionLocker)
                {
                    if (generation.Value != _generation)
                    {
                        return;
                    }

                    trade = force ? EmitMyTradeForce(source, accountId) : EmitMyTradeCore(source, accountId);
                }
            }
            else
            {
                trade = force ? EmitMyTradeForce(source, accountId) : EmitMyTradeCore(source, accountId);
            }

            if (trade != null)
            {
                MyTradeEvent?.Invoke(trade);
            }
        }

        // явный запрос от хаба — эмитим без дедупа
        private MyTrade EmitMyTradeForce(AccountTrade source, string accountId)
        {
            if (source == null || source.Timestamp == null || string.IsNullOrEmpty(source.OrderId))
            {
                return null;
            }

            string outputId = string.IsNullOrEmpty(source.TradeId) == false
                ? source.TradeId
                : source.OrderId + "|" + source.Timestamp.ToDateTime().Ticks.ToString(CultureInfo.InvariantCulture);

            return BuildMyTrade(source, accountId, outputId);
        }

        private MyTrade EmitMyTradeCore(AccountTrade source, string accountId)
        {
            if (source == null || source.Timestamp == null || string.IsNullOrEmpty(source.OrderId))
            {
                return null;
            }

            string account = string.IsNullOrEmpty(source.AccountId) ? accountId : source.AccountId;
            decimal size = ToDecimal(source.Size);
            string fingerprint = account + "|" + source.OrderId + "|" + source.Timestamp.ToDateTime().Ticks
                + "|" + ToDecimal(source.Price).ToString(CultureInfo.InvariantCulture)
                + "|" + size.ToString(CultureInfo.InvariantCulture) + "|" + source.Side;
            string outputId;

            if (TryPassPrivateTrade(account, fingerprint, source.TradeId, out outputId) == false)
            {
                return null;
            }

            return BuildMyTrade(source, accountId, outputId);
        }

        private MyTrade BuildMyTrade(AccountTrade source, string accountId, string outputId)
        {
            string account = string.IsNullOrEmpty(source.AccountId) ? accountId : source.AccountId;
            Security security = ResolveSecurity(source.Symbol);

            if (security == null)
            {
                return null;
            }

            decimal size = ToDecimal(source.Size);
            MyTrade trade = new MyTrade();
            trade.NumberTrade = account + "|" + outputId;
            trade.NumberOrderParent = GetMarketOrderId(account, source.OrderId);
            trade.SecurityNameCode = security.Name;
            trade.Price = ToDecimal(source.Price);
            trade.Volume = size / LotDivisor(security);
            trade.Side = ConvertSide(source.Side);
            trade.Time = ToMoscow(source.Timestamp);
            return trade;
        }

        private bool TryPassPrivateTrade(string account, string fingerprint, string tradeId, out string outputId)
        {
            lock (_dedupLocker)
            {
                string canonicalKey = account + "|fingerprint|" + fingerprint;
                TradeOccurrenceState state;

                if (_privateTradeStates.TryGetValue(canonicalKey, out state) == false)
                {
                    state = new TradeOccurrenceState();
                    _privateTradeStates.Add(canonicalKey, state);
                }

                state.UpdatedUtc = DateTime.UtcNow;
                TrimTradeStates(_privateTradeStates);
                return TryPassOccurrenceTrade(_privateTradeDedup, account, fingerprint, tradeId, state, out outputId);
            }
        }

        private string GetMarketOrderId(string accountId, string orderId)
        {
            return accountId + "|" + orderId;
        }

        private string GetExchangeOrderId(string marketOrderId)
        {
            if (string.IsNullOrEmpty(marketOrderId))
            {
                return marketOrderId;
            }

            int separator = marketOrderId.LastIndexOf('|');
            return separator >= 0 ? marketOrderId.Substring(separator + 1) : marketOrderId;
        }

        private readonly string _uniquePrefix;

        private readonly object _orderMappingLocker = new object();

        private readonly Dictionary<string, int> _numberUserByClientId = new Dictionary<string, int>();

        private readonly Dictionary<int, string> _clientIdByNumberUser = new Dictionary<int, string>();

        // TIF по "accountId|clientOrderId": Финам может не возвращать time_in_force эхом,
        // а для статуса DoneForDay он критичен (GTC переживает конец сессии)
        private readonly ConcurrentDictionary<string, OrderTypeTime> _orderTimeInForce = new ConcurrentDictionary<string, OrderTypeTime>(StringComparer.Ordinal);

        // отправленный тип заявки по "accountId|clientOrderId": стрим Финама репортит срочные
        // маркет-заявки как LIMIT (эмуляция защитной ценой на FORTS) — своим заявкам верим мы, а не эху
        private readonly ConcurrentDictionary<string, OrderPriceType> _orderTypes = new ConcurrentDictionary<string, OrderPriceType>(StringComparer.Ordinal);

        private int _nextLegacyNumberUser = int.MaxValue;

        private readonly Dictionary<string, OrderProgress> _orderProgress = new Dictionary<string, OrderProgress>(StringComparer.Ordinal);

        #endregion

        #region Private data reconciliation

        private bool TryReserveInitialPrivateDataReconciliation(long generation)
        {
            lock (_sessionLocker)
            {
                if (generation != _generation
                    || ServerStatus != ServerConnectStatus.Connect
                    || _initialPrivateDataReconciledGeneration == generation)
                {
                    return false;
                }

                _initialPrivateDataReconciledGeneration = generation;
                return true;
            }
        }

        private void ReconcileInitialPrivateData()
        {
            List<string> accounts = new List<string>(_accountIds);
            bool allSucceeded = true;

            for (int i = 0; i < accounts.Count; i++)
            {
                try
                {
                    ReconcileAccountData(accounts[i]);
                }
                catch (Exception error)
                {
                    allSucceeded = false;
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }

            if (allSucceeded == false)
            {
                lock (_sessionLocker)
                {
                    _initialPrivateDataReconciledGeneration = -1;
                }
            }
        }

        private void QueuePrivateRecoveryReconciliation(string accountId, long generation)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return;
            }

            lock (_reconciliationLocker)
            {
                // Orders и Trades рестартуются парой — дедуп схлопывает двойной триггер одного счёта
                if (_reconciliationPending.Add(accountId + "|" + generation) == false)
                {
                    return;
                }

                _reconciliationQueue.Enqueue(new KeyValuePair<string, long>(accountId, generation));
            }

            _reconciliationWake.Set();
        }

        private void ReconciliationWorker()
        {
            try
            {
                while (IsCompletelyDeleted == false)
                {
                    try
                    {
                        _reconciliationWake.WaitOne(1000);

                        while (true)
                        {
                            KeyValuePair<string, long> request;

                            lock (_reconciliationLocker)
                            {
                                if (_reconciliationQueue.Count == 0)
                                {
                                    break;
                                }

                                request = _reconciliationQueue.Dequeue();
                                _reconciliationPending.Remove(request.Key + "|" + request.Value);
                            }

                            if (request.Value != _generation)
                            {
                                continue;
                            }

                            ReconcileAfterPrivateRecovery(request.Key, request.Value);
                        }
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ReconcileAfterPrivateRecovery(string accountId, long generation)
        {
            bool reconciled = false;

            try
            {
                ReconcileAccountData(accountId, generation);
                reconciled = true;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);

                int failures;

                lock (_dedupLocker)
                {
                    _privateRecoveryFailures.TryGetValue(accountId, out failures);
                    failures++;
                    _privateRecoveryFailures[accountId] = failures;
                }

                // серия неудач подряд — откладываем рестарт приватных стримов на 5 минут,
                // чтобы не крутить цикл «рестарт → та же ошибка → рестарт»
                int delaySeconds = failures >= 3 ? 300 : 0;

                RequestPrivateRecovery(accountId, generation, delaySeconds);
            }
            finally
            {
                if (reconciled && generation == _generation)
                {
                    lock (_dedupLocker)
                    {
                        _privateRecoveryFailures.Remove(accountId);
                    }

                    ForceCheckOrdersAfterReconnectEvent?.Invoke();
                }
            }
        }

        private void RequestPrivateRecovery(string accountId, long generation, int delaySeconds = 0)
        {
            if (generation != _generation)
            {
                return;
            }

            FinamStreamKind[] kinds = { FinamStreamKind.Orders, FinamStreamKind.Trades };

            for (int i = 0; i < kinds.Length; i++)
            {
                FinamStreamLease lease;

                lock (_streamLocker)
                {
                    if (_streams.TryGetValue(StreamId(kinds[i], accountId), out lease) == false)
                    {
                        continue;
                    }

                    RequestStreamRestart(lease);

                    if (delaySeconds > 0)
                    {
                        // супервизор не трогает lease, пока LastMessageUtc в будущем — откладываем рестарт
                        lease.LastMessageUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
                    }
                }
            }
        }

        private readonly Dictionary<string, int> _privateRecoveryFailures = new Dictionary<string, int>(StringComparer.Ordinal);

        private void ReconcileAccountData(string accountId, long? generation = null)
        {
            long currentGeneration = generation ?? _generation;
            List<OrderState> states = GetOrderStates(accountId);

            if (currentGeneration != _generation)
            {
                return;
            }

            DateTime oldest = MoscowNow().AddDays(-7);

            for (int i = 0; i < states.Count; i++)
            {
                // на реконнекте переэмитим только нефинальные ордера:
                // история (Done/Cancel/Fail) движку не нужна и флудит старыми заявками
                OrderStateType state = ConvertOrderState(states[i].Status, ResolveTimeInForce(states[i].Order));

                if (state == OrderStateType.Active || state == OrderStateType.Partial || state == OrderStateType.Pending)
                {
                    EmitOrder(states[i], currentGeneration);
                }

                if (states[i].TransactAt != null)
                {
                    DateTime orderTime = ToMoscow(states[i].TransactAt);

                    if (orderTime < oldest)
                    {
                        oldest = orderTime;
                    }
                }
            }

            lock (_dedupLocker)
            {
                DateTime watermark;

                if (_tradeRecoveryWatermarks.TryGetValue(accountId, out watermark) && watermark < oldest)
                {
                    oldest = watermark;
                }
            }

            ReconcileTrades(accountId, null, oldest, currentGeneration);
        }

        private void ReconcileTrades(string accountId, string orderId, DateTime from, long? generation = null, bool forceEmit = false)
        {
            long currentGeneration = generation ?? _generation;
            DateTime start = from == DateTime.MinValue ? MoscowNow().AddDays(-1) : from.AddMinutes(-1);
            DateTime end = MoscowNow().AddMinutes(1);
            List<AccountTrade> trades = LoadAccountTrades(accountId, orderId, start, end);

            if (currentGeneration != _generation)
            {
                return;
            }

            trades.Sort(CompareAccountTrades);

            for (int i = 0; i < trades.Count; i++)
            {
                if (currentGeneration != _generation)
                {
                    return;
                }

                EmitMyTrade(trades[i], accountId, currentGeneration, forceEmit);
            }

            if (orderId == null && currentGeneration == _generation)
            {
                lock (_dedupLocker)
                {
                    _tradeRecoveryWatermarks[accountId] = end;
                }
            }
        }

        private List<AccountTrade> LoadAccountTrades(string accountId, string orderId, DateTime start, DateTime end)
        {
            List<AccountTrade> result = new List<AccountTrade>();

            if (end <= start)
            {
                return result;
            }

            WaitRateGate("AccountTrades");
            TradesResponse response = _accountsClient.Trades(new TradesRequest
            {
                AccountId = accountId,
                Limit = 100,
                Interval = new Interval
                {
                    StartTime = Timestamp.FromDateTime(ToUtc(start)),
                    EndTime = Timestamp.FromDateTime(ToUtc(end))
                }
            }, CreateMetadata(), DateTime.UtcNow.AddSeconds(30));

            if (response == null)
            {
                return result;
            }

            if (response.Trades.Count >= 100 && end - start > TimeSpan.FromSeconds(1))
            {
                DateTime middle = new DateTime(start.Ticks + (end.Ticks - start.Ticks) / 2, start.Kind);
                result.AddRange(LoadAccountTrades(accountId, orderId, start, middle));
                result.AddRange(LoadAccountTrades(accountId, orderId, middle, end));
                return result;
            }

            if (response.Trades.Count >= 100)
            {
                // дно биссекции: больше 100 сделок в секунду через это API не достать (курсора нет).
                // Не бросаем исключение — иначе реконсиляция зацикливается на «отравленном» окне;
                // отдаём собранное (дедуп отсечёт повторы) и честно фиксируем неполноту в лог
                SendLogMessage("Finam account trades reached the 100-item limit at the minimum recovery interval for "
                    + accountId + " [" + start.ToString("yyyy-MM-dd HH:mm:ss") + " - " + end.ToString("yyyy-MM-dd HH:mm:ss")
                    + "]. Returning partial data, reconciliation may be incomplete for this window.", LogMessageType.Error);
            }

            for (int i = 0; i < response.Trades.Count; i++)
            {
                if (orderId == null || response.Trades[i].OrderId == orderId)
                {
                    result.Add(response.Trades[i]);
                }
            }

            return result;
        }

        private int CompareAccountTrades(AccountTrade first, AccountTrade second)
        {
            DateTime firstTime = first.Timestamp == null ? DateTime.MinValue : first.Timestamp.ToDateTime();
            DateTime secondTime = second.Timestamp == null ? DateTime.MinValue : second.Timestamp.ToDateTime();
            return firstTime.CompareTo(secondTime);
        }

        public event Action ForceCheckOrdersAfterReconnectEvent;

        private readonly Dictionary<string, DateTime> _tradeRecoveryWatermarks = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        private long _initialPrivateDataReconciledGeneration = -1;

        #endregion

        #region Helpers and bounded deduplication

        private void StartWorker(ThreadStart method, string name)
        {
            Thread worker = new Thread(method);
            worker.Name = name;
            worker.IsBackground = true;
            _workerThreads.Add(worker);
            worker.Start();
        }

        private void WaitRateGate(string operation)
        {
            WaitRateGate(operation, 30);
        }

        private void WaitRateGate(string operation, int timeoutSeconds)
        {
            string service = GetRateGateService(operation);
            RateGate gate = _rateGates.GetOrAdd(service, CreateRateGate);

            if (gate.WaitToProceed(TimeSpan.FromSeconds(timeoutSeconds)) == false)
            {
                throw new TimeoutException("Finam rate gate timeout for " + service + ".");
            }
        }

        private string GetRateGateService(string operation)
        {
            if (operation == "Auth" || operation == "TokenDetails" || operation == "JwtRenewal")
            {
                return "Auth";
            }

            if (operation == "AllAssets" || operation == "GetAsset" || operation == "GetAssetParams" || operation == "Clock")
            {
                return "Assets";
            }

            if (operation == "GetAccount" || operation == "AccountTrades" || operation == "Account")
            {
                return "Accounts";
            }

            if (operation == "PlaceOrder" || operation == "CancelOrder" || operation == "GetOrders"
                || operation == "GetOrder" || operation == "Orders" || operation == "Trades")
            {
                return "Orders";
            }

            // квоты у Finam раздельные по методам (200/мин на метод, источник истины — UsageMetricsService/GetUsageMetrics),
            // поэтому стримы гейтим по своим методам; группы методов на одном гейте — сознательно консервативны
            if (operation == "LatestTrades" || operation == "OrderBook" || operation == "Bars")
            {
                return operation;
            }

            return "MarketData";
        }

        private RateGate CreateRateGate(string operation)
        {
            // Bars чуть консервативнее — меньше шансов дойти до потолка при массовой подписке
            if (operation == "Bars")
            {
                return new RateGate(180, TimeSpan.FromMinutes(1));
            }

            return new RateGate(200, TimeSpan.FromMinutes(1));
        }

        private decimal LotDivisor(Security security)
        {
            lock (_enrichLocker)
            {
                return security != null && security.Lot > 0m ? security.Lot : 1m;
            }
        }

        private decimal ToDecimal(GDecimal value)
        {
            if (value == null || string.IsNullOrEmpty(value.Value))
            {
                return 0m;
            }

            return value.Value.ToDecimal();
        }

        private decimal MoneyToDecimal(Money money)
        {
            if (money == null)
            {
                return 0m;
            }

            return money.Units + money.Nanos / 1000000000m;
        }

        private decimal DecimalPower(int decimals)
        {
            decimal value = 1m;

            for (int i = 0; i < decimals; i++)
            {
                value *= 10m;
            }

            return value;
        }

        private Side ConvertSide(FSide side)
        {
            if (side == FSide.Buy)
            {
                return Side.Buy;
            }

            if (side == FSide.Sell)
            {
                return Side.Sell;
            }

            return Side.None;
        }

        private FSide ConvertSide(Side side)
        {
            if (side == Side.Buy)
            {
                return FSide.Buy;
            }

            if (side == Side.Sell)
            {
                return FSide.Sell;
            }

            return FSide.Unspecified;
        }

        private DateTime ToMoscow(Timestamp timestamp)
        {
            if (timestamp == null)
            {
                return DateTime.MinValue;
            }

            return TimeZoneInfo.ConvertTimeFromUtc(timestamp.ToDateTime(), _moscowTimeZone);
        }

        private DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            DateTime unspecified = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, _moscowTimeZone);
        }

        private DateTime MoscowNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _moscowTimeZone);
        }

        private bool TryPassDedup(Dictionary<string, DedupEntry> entries, string key, string fingerprint)
        {
            lock (_dedupLocker)
            {
                DateTime now = DateTime.UtcNow;
                DedupEntry previous;

                if (entries.TryGetValue(key, out previous) && previous.Fingerprint == fingerprint)
                {
                    previous.UpdatedUtc = now;
                    return false;
                }

                entries[key] = new DedupEntry { Fingerprint = fingerprint, UpdatedUtc = now };

                if (entries.Count > 4096)
                {
                    List<string> expired = new List<string>();

                    foreach (KeyValuePair<string, DedupEntry> pair in entries)
                    {
                        if (now - pair.Value.UpdatedUtc > TimeSpan.FromHours(48))
                        {
                            expired.Add(pair.Key);
                        }
                    }

                    for (int i = 0; i < expired.Count; i++)
                    {
                        entries.Remove(expired[i]);
                    }

                    while (entries.Count > 4096)
                    {
                        string oldestKey = null;
                        DateTime oldest = DateTime.MaxValue;

                        foreach (KeyValuePair<string, DedupEntry> pair in entries)
                        {
                            if (pair.Value.UpdatedUtc < oldest)
                            {
                                oldest = pair.Value.UpdatedUtc;
                                oldestKey = pair.Key;
                            }
                        }

                        if (oldestKey == null)
                        {
                            break;
                        }

                        entries.Remove(oldestKey);
                    }
                }

                return true;
            }
        }

        private bool TryPassOccurrenceTrade(
            Dictionary<string, DedupEntry> rawEntries,
            string owner,
            string fingerprint,
            string tradeId,
            TradeOccurrenceState state,
            out string outputId)
        {
            if (string.IsNullOrEmpty(tradeId) == false)
            {
                // trade_id по контракту proto заполнен всегда — дедуп строго по нему
                string rawKey = owner + "|id|" + tradeId;

                if (rawEntries.ContainsKey(rawKey))
                {
                    outputId = tradeId;
                    return false;
                }

                TryPassDedup(rawEntries, rawKey, "");
                outputId = tradeId;
                return true;
            }

            // недокументированная аномалия — сделка без id: пропускаем каждое вхождение
            // со своим порядковым номером, чтобы настоящие сделки не терялись
            state.EmptyObserved++;
            outputId = fingerprint + "#" + state.EmptyObserved.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private void TrimTradeStates(Dictionary<string, TradeOccurrenceState> states)
        {
            if (states.Count <= 4096)
            {
                return;
            }

            DateTime threshold = DateTime.UtcNow.AddHours(-48);
            List<string> expired = new List<string>();

            foreach (KeyValuePair<string, TradeOccurrenceState> pair in states)
            {
                if (pair.Value.UpdatedUtc < threshold)
                {
                    expired.Add(pair.Key);
                }
            }

            for (int i = 0; i < expired.Count; i++)
            {
                states.Remove(expired[i]);
            }

            while (states.Count > 4096)
            {
                string oldestKey = null;
                DateTime oldest = DateTime.MaxValue;

                foreach (KeyValuePair<string, TradeOccurrenceState> pair in states)
                {
                    if (pair.Value.UpdatedUtc < oldest)
                    {
                        oldest = pair.Value.UpdatedUtc;
                        oldestKey = pair.Key;
                    }
                }

                if (oldestKey == null)
                {
                    break;
                }

                states.Remove(oldestKey);
            }
        }

        public void SetLeverage(Security security, decimal leverage) { }

        private sealed class TradeOccurrenceState
        {
            public int EmptyObserved { get; set; }

            public DateTime UpdatedUtc { get; set; }
        }

        private sealed class OrderProgress
        {
            public OrderStateType State { get; set; }

            public decimal VolumeExecute { get; set; }

            public DateTime FinalizedUtc { get; set; }
        }

        private sealed class DedupEntry
        {
            public string Fingerprint { get; set; }

            public DateTime UpdatedUtc { get; set; }
        }

        private readonly TimeZoneInfo _moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");

        private readonly object _dedupLocker = new object();

        private readonly ConcurrentDictionary<string, RateGate> _rateGates = new ConcurrentDictionary<string, RateGate>();

        private readonly Dictionary<string, DedupEntry> _publicTradeDedup = new Dictionary<string, DedupEntry>();

        private readonly HashSet<string> _loggedAliasConflicts = new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<string, DedupEntry> _privateTradeDedup = new Dictionary<string, DedupEntry>();

        private readonly Dictionary<string, TradeOccurrenceState> _privateTradeStates = new Dictionary<string, TradeOccurrenceState>(StringComparer.Ordinal);

        private readonly Dictionary<string, TradeOccurrenceState> _publicTradeStates = new Dictionary<string, TradeOccurrenceState>(StringComparer.Ordinal);

        private readonly Dictionary<string, DedupEntry> _orderDedup = new Dictionary<string, DedupEntry>();

        #endregion

        #region MoexIss bulk enrichment and warmer

        private void IssLoaderThread()
        {
            try
            {
                while (IsCompletelyDeleted == false)
                {
                    try
                    {
                        if (_workersStopped.WaitOne(5000))
                        {
                            break;
                        }

                        if (ServerStatus != ServerConnectStatus.Connect || _assetsClient == null)
                        {
                            continue;
                        }

                        long generation = _generation;

                        if (_issCompletedGeneration == generation)
                        {
                            continue;
                        }

                        bool securitiesLoaded;

                        lock (_securitiesLocker)
                        {
                            securitiesLoaded = _allSecurities.Count > 0;
                        }

                        // ждём список бумаг — матчить строки ISS не с чем, пока он пуст
                        if (securitiesLoaded == false)
                        {
                            continue;
                        }

                        RunIssEnrichment(generation);
                        _issCompletedGeneration = generation;
                    }
                    catch (Exception error)
                    {
                        SendLogMessage("Finam ISS loader thread: " + error.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage("Finam ISS loader thread: " + error.ToString(), LogMessageType.Error);
            }
        }

        private void RunIssEnrichment(long generation)
        {
            SendLogMessage("Finam ISS enrichment started.", LogMessageType.System);
            Dictionary<string, Security> moexLookup = BuildMoexSecuritiesLookup();
            SendLogMessage("Finam ISS MOEX lookup built: " + moexLookup.Count, LogMessageType.System);
            int applied = 0;

            applied += LoadIssBoardWithRetries(generation, "shares", moexLookup,
                "https://iss.moex.com/iss/engines/stock/markets/shares/boards/TQBR/securities.json?iss.meta=off&iss.only=securities&securities.columns=SECID,MINSTEP,LOTSIZE,DECIMALS,STEPPRICE",
                "MISX");

            applied += LoadIssBoardWithRetries(generation, "futures", moexLookup,
                "https://iss.moex.com/iss/engines/futures/markets/forts/securities.json?iss.meta=off&iss.only=securities&securities.columns=SECID,MINSTEP,LOTSIZE,DECIMALS,STEPPRICE,INITIALMARGIN",
                "RTSX");

            applied += LoadIssBoardWithRetries(generation, "bonds", moexLookup,
                "https://iss.moex.com/iss/engines/stock/markets/bonds/boards/TQOB/securities.json?iss.meta=off&iss.only=securities&securities.columns=SECID,MINSTEP,LOTSIZE,DECIMALS,FACEVALUE",
                "MISX");

            SendLogMessage("Finam ISS enrichment applied to " + applied + " MOEX securities.", LogMessageType.System);

            if (applied > 0 && generation == _generation && ServerStatus == ServerConnectStatus.Connect)
            {
                List<Security> securities = ApplySecurityFilters();
                SecurityEvent?.Invoke(new List<Security>(securities));

                lock (_enrichLocker)
                {
                    SaveEnrichmentCache();
                }
            }
        }

        private Dictionary<string, Security> BuildMoexSecuritiesLookup()
        {
            lock (_securitiesLocker)
            {
                Dictionary<string, Security> lookup = new Dictionary<string, Security>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < _allSecurities.Count; i++)
                {
                    Security security = _allSecurities[i];

                    if (security.Exchange == "MISX" || security.Exchange == "RTSX")
                    {
                        lookup[security.Name + "|" + security.Exchange] = security;
                    }
                }

                return lookup;
            }
        }

        private int LoadIssBoardWithRetries(long generation, string boardName, Dictionary<string, Security> moexLookup, string baseUrl, string mic)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (generation != _generation || IsCompletelyDeleted)
                {
                    return 0;
                }

                try
                {
                    int applied = LoadIssBoard(generation, moexLookup, baseUrl, mic);
                    SendLogMessage("Finam ISS " + boardName + ": applied " + applied, LogMessageType.System);
                    return applied;
                }
                catch (Exception error)
                {
                    SendLogMessage("Finam ISS " + boardName + " load failed, attempt " + (attempt + 1) + ": " + error.ToString(), LogMessageType.System);

                    if (_workersStopped.WaitOne(TimeSpan.FromMinutes(5)))
                    {
                        return 0;
                    }
                }
            }

            return 0;
        }

        private int LoadIssBoard(long generation, Dictionary<string, Security> moexLookup, string baseUrl, string mic)
        {
            HttpClientHandler handler = new HttpClientHandler();

            if (_proxy != null)
            {
                handler.Proxy = _proxy;
                handler.UseProxy = true;
            }
            else
            {
                handler.UseProxy = false;
            }

            int applied = 0;

            using (HttpClient client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromSeconds(15);

                int start = 0;

                while (true)
                {
                    if (generation != _generation || IsCompletelyDeleted)
                    {
                        return applied;
                    }

                    // тот же приём, что в BcsServer для MOEX ISS: GetStringAsync().Result
                    string json = client.GetStringAsync(baseUrl + "&securities.limit=1000&start=" + start).Result;

                    FinamIssResponse parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<FinamIssResponse>(json);

                    if (parsed == null || parsed.securities == null || parsed.securities.data == null)
                    {
                        break;
                    }

                    applied += ApplyIssRows(parsed.securities, moexLookup, mic);

                    if (parsed.securities.data.Count < 1000)
                    {
                        break;
                    }

                    start += parsed.securities.data.Count;
                }
            }

            return applied;
        }

        private int ApplyIssRows(FinamIssBlock block, Dictionary<string, Security> moexLookup, string mic)
        {
            int secidIndex = -1;
            int minStepIndex = -1;
            int lotSizeIndex = -1;
            int decimalsIndex = -1;
            int stepPriceIndex = -1;
            int marginIndex = -1;
            int faceValueIndex = -1;

            for (int i = 0; i < block.columns.Count; i++)
            {
                string column = block.columns[i];

                if (column == "SECID") secidIndex = i;
                else if (column == "MINSTEP") minStepIndex = i;
                else if (column == "LOTSIZE") lotSizeIndex = i;
                else if (column == "DECIMALS") decimalsIndex = i;
                else if (column == "STEPPRICE") stepPriceIndex = i;
                else if (column == "INITIALMARGIN") marginIndex = i;
                else if (column == "FACEVALUE") faceValueIndex = i;
            }

            if (secidIndex < 0 || minStepIndex < 0)
            {
                return 0;
            }

            // LOTSIZE есть не у всех досок (у FORTS отсутствует) — тогда лот не трогаем
            bool lotRequired = lotSizeIndex >= 0;
            int applied = 0;

            for (int i = 0; i < block.data.Count; i++)
            {
                object[] row = block.data[i];
                string secid = IssCell(row, secidIndex);
                decimal minStep = IssDecimal(row, minStepIndex);
                decimal lot = IssDecimal(row, lotSizeIndex);
                int decimals = (int)IssDecimal(row, decimalsIndex);

                // валидация: мусорная строка пропускается, доска не валится
                if (string.IsNullOrEmpty(secid) || minStep <= 0 || decimals < 0 || decimals > 10
                    || (lotRequired && lot <= 0))
                {
                    continue;
                }

                Security security;

                if (moexLookup.TryGetValue(secid + "|" + mic, out security) == false)
                {
                    continue;
                }

                if (ApplyIssValues(security, minStep, lot, decimals,
                    IssDecimal(row, stepPriceIndex), IssDecimal(row, marginIndex), IssDecimal(row, faceValueIndex)))
                {
                    applied++;
                }
            }

            return applied;
        }

        private bool ApplyIssValues(Security security, decimal minStep, decimal lot, int decimals, decimal stepPrice, decimal margin, decimal faceValue)
        {
            lock (_enrichLocker)
            {
                CachedEnrichment existing;

                // запись от Finam авторитетна и ISS не затирается
                if (_enrichmentCache.TryGetValue(security.NameId, out existing)
                    && existing.Source == "Finam")
                {
                    return false;
                }

                security.PriceStep = minStep;
                security.Decimals = decimals;
                security.PriceStepCost = stepPrice > 0 ? stepPrice : minStep;

                if (lot > 0)
                {
                    security.Lot = lot;
                }

                if (margin > 0)
                {
                    security.MarginBuy = margin;
                    security.MarginSell = margin;
                }

                if (faceValue > 0)
                {
                    security.NominalCurrent = faceValue;
                }

                if (existing == null)
                {
                    existing = new CachedEnrichment();
                    _enrichmentCache.Add(security.NameId, existing);
                }

                existing.PriceStep = security.PriceStep;
                existing.Decimals = security.Decimals;
                existing.Lot = security.Lot;
                existing.PriceStepCost = security.PriceStepCost;
                existing.MarginBuy = security.MarginBuy;
                existing.MarginSell = security.MarginSell;
                existing.NominalCurrent = security.NominalCurrent;
                existing.State = security.State;
                existing.Source = "Iss";
                existing.UpdatedUtc = DateTime.UtcNow;
                _enrichmentCacheDirty = true;
                return true;
            }
        }

        private string IssCell(object[] row, int index)
        {
            if (index < 0 || index >= row.Length || row[index] == null)
            {
                return "";
            }

            return Convert.ToString(row[index], CultureInfo.InvariantCulture);
        }

        private decimal IssDecimal(object[] row, int index)
        {
            string value = IssCell(row, index);

            if (string.IsNullOrEmpty(value))
            {
                return 0m;
            }

            return value.ToDecimal();
        }

        private void EnrichmentWarmerThread()
        {
            try
            {
                while (IsCompletelyDeleted == false)
                {
                    try
                    {
                        _warmerWake.WaitOne(5000);

                        if (ServerStatus != ServerConnectStatus.Connect || IsCompletelyDeleted)
                        {
                            continue;
                        }

                        RunWarmerPass();

                        // проход закончен — следующий через полчаса или по сигналу (смена галок классов)
                        _warmerWake.WaitOne(TimeSpan.FromMinutes(30));
                    }
                    catch (Exception error)
                    {
                        SendLogMessage(error.ToString(), LogMessageType.Error);
                        Thread.Sleep(5000);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void RunWarmerPass()
        {
            long generation = _generation;
            List<Security> queue = BuildWarmerQueue();
            int enriched = 0;

            for (int i = 0; i < queue.Count; i++)
            {
                if (IsCompletelyDeleted || ServerStatus != ServerConnectStatus.Connect || generation != _generation)
                {
                    return;
                }

                Security security = queue[i];
                bool alreadyFresh;

                lock (_enrichLocker)
                {
                    alreadyFresh = _enrichedSecurities.Contains(security.NameId);
                }

                if (alreadyFresh)
                {
                    continue;
                }

                try
                {
                    EnrichSecurity(security, false);
                    enriched++;
                }
                catch (Exception error)
                {
                    SendLogMessage("Finam warmer enrichment failed for " + security.NameId + ": " + error.Message, LogMessageType.System);
                }

                Thread.Sleep(700);
            }

            if (enriched > 0 && generation == _generation && ServerStatus == ServerConnectStatus.Connect)
            {
                List<Security> securities = ApplySecurityFilters();
                SecurityEvent?.Invoke(new List<Security>(securities));

                lock (_enrichLocker)
                {
                    SaveEnrichmentCache();
                }
            }

            SendLogMessage("Finam warmer pass done. Enriched: " + enriched + ", queue: " + queue.Count, LogMessageType.System);
        }

        private List<Security> BuildWarmerQueue()
        {
            lock (_securitiesLocker)
            {
                HashSet<string> enabledPairs = GetEnabledPairs();
                List<Security> queue = new List<Security>();

                for (int i = 0; i < _allSecurities.Count; i++)
                {
                    Security security = _allSecurities[i];

                    if (IsEnabledPair(security, enabledPairs) == false
                        || security.SecurityType == SecurityType.Index
                        || security.State != SecurityStateType.Activ)
                    {
                        continue;
                    }

                    queue.Add(security);
                }

                queue.Sort(CompareByMicPriority);
                return queue;
            }
        }

        private int CompareByMicPriority(Security first, Security second)
        {
            return GetMicPriority(first.NameId).CompareTo(GetMicPriority(second.NameId));
        }

        #endregion

        #region Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion

    }
}
