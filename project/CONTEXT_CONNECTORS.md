# CONTEXT_CONNECTORS — Коннекторы OsEngine

> Архитектура коннекторов (биржевых серверов) OsEngine и правила их написания. Для ИИ-агентов, создающих новый коннектор или правящих существующий. Читать перед любой работой в `OsEngine/Market/Servers/`.
>
> Связанные документы: `CONTEXT_CODING_GUIDELINES.md` (общие правила кода), `CONTEXT.md` (карта проекта).

---

## 1. Архитектура коннектора

### 1.1. Триада: `AServer` / `IServerRealization` / `IServerPermission`

Каждый коннектор состоит из трёх частей:

1. **Обёртка `AServer`** (`OsEngine/Market/Servers/AServer.cs`) — наследник `AServer`, обычно маленький класс вида `XServer : AServer`. Его задачи:
   - в конструкторе создать реализацию и присвоить в свойство `ServerRealization` (это запускает инициализацию базового класса: подписки на события, создание стандартных параметров);
   - объявить кастомные параметры коннектора через `CreateParameter*`;
   - всю остальную работу (ордера, хранилища тиков/свечей, сравнение позиций, потоки) делает сам `AServer` — в обёртку его не дублируем.

```csharp
public class BcsServer : AServer
{
    public BcsServer(int uniqueNumber)
    {
        ServerNum = uniqueNumber;
        BcsServerRealization realization = new BcsServerRealization();
        ServerRealization = realization;

        CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");
        CreateParameterBoolean(OsLocalization.Market.UseStock, true);
        // ...
    }
}
```

2. **Реализация `IServerRealization`** (`OsEngine/Market/Servers/IServerRealization.cs`) — класс `XServerRealization`, в котором живёт вся работа с API биржи: подключение, бумаги, портфели, данные, ордера. Это самый большой файл коннектора. Интерфейс задаёт полный контракт: `Connect(WebProxy)`, `Dispose()`, `GetSecurities()`, `GetPortfolios()`, `Subscribe(Security)`, `GetLastCandleHistory(...)` / `GetCandleDataToSecurity(...)` / `GetTickDataToSecurity(...)`, `SendOrder` / `ChangeOrderPrice` / `CancelOrder` / `CancelAllOrders` / `GetAllActivOrders` / `GetOrderStatus` / `GetActiveOrders` / `GetHistoricalOrders` и события (см. 1.3). Метод `Unsubscribe(Security)` имеет пустую реализацию по умолчанию — переопределять не обязательно.

3. **Permission `IServerPermission`** (`OsEngine/Market/Servers/IServerPermission.cs`) — класс `XServerPermission`, декларация возможностей коннектора: какие таймфреймы данных можно качать, поддерживаются ли маркет-ордера, смена цены ордера и т.д. По нему движок включает/выключает целые подсистемы (см. главу 9).

### 1.2. Жизненный цикл

- **Создание.** `ServerMaster` создаёт обёртку `new XServer(uniqueNum)`. В конструкторе обязательно: `ServerNum = ...`, затем `ServerRealization = realization` (сеттер `AServer.cs:161` подписывается на все события реализации и создаёт стандартные параметры), затем кастомные параметры.
- **Connect.** Движок вызывает `Connect(WebProxy proxy)` реализации. Реализация читает значения параметров из `ServerParameters`, устанавливает соединение (REST-клиент, токены, сокеты) и по готовности выставляет `ServerStatus = ServerConnectStatus.Connect` и вызывает `ConnectEvent()`. До этого момента `SecurityEvent`/`PortfolioEvent` не вызываются.
- **Работа.** Движок дёргает `GetSecurities()`, `GetPortfolios()`, подписывается на бумаги, шлёт ордера.
- **Disconnect.** При потере соединения реализация сама выставляет `ServerStatus = Disconnect` и вызывает `DisconnectEvent()`. Переподключение инициирует движок, повторно вызывая `Connect`.
- **Dispose/Delete.** `Dispose()` реализации обязан остановить соединение, отписаться от событий сокетов, закрыть клиенты и выставить `Disconnect`. `AServer.Delete()` отписывается от событий реализации и вызывает `Dispose`.

### 1.3. Событийная модель

Реализация общается с движком **только событиями** (никаких прямых вызовов в `AServer`):

| Событие | Когда вызывать |
|---------|----------------|
| `ConnectEvent` / `DisconnectEvent` | смена состояния соединения |
| `SecurityEvent(List<Security>)` | список бумаг загружен |
| `PortfolioEvent(List<Portfolio>)` | обновление портфелей |
| `NewTradesEvent(Trade)` | новый тик (публичная сделка) |
| `MarketDepthEvent(MarketDepth)` | обновление стакана |
| `MyOrderEvent(Order)` | любое изменение моего ордера |
| `MyTradeEvent(MyTrade)` | моя сделка |
| `LogMessageEvent(string, LogMessageType)` | все логи — только сюда |
| `NewsEvent`, `FundingUpdateEvent`, `Volume24hUpdateEvent`, `AdditionalMarketDataEvent` | опционально; если не поддерживается — пустые add/remove `{ }` |

`AServer` подписан на эти события и дальше раздаёт их роботам, хранилищам и журналам. Правила потоков из `CONTEXT_CODING_GUIDELINES.md` (5.6): обработчики должны быть короткими — тяжёлый парсинг выноси в свои потоки-ридеры (см. 3.3).

### 1.4. Где лежат коннекторы, структура папки

```
OsEngine/Market/Servers/<Имя>/
  XServer.cs              # обёртка AServer + реализация (может быть одним файлом)
  XServerPermission.cs    # permission
  Entity/                 # классы под JSON ответов API
    XxxResponse.cs
```

Имена: папка и классы — по имени биржи/брокера (`BCS/BcsServer.cs`, `TInvest/TInvestServer.cs`). Namespace = путь (`OsEngine.Market.Servers.BCS`).

### 1.5. Регистрация нового сервера

Новый коннектор регистрируется в `OsEngine/Market/ServerMaster.cs` в **трёх** местах (пропуск любого — сервер не появится в UI или упадёт):

1. **enum `ServerType`** (`ServerMaster.cs:2203`) — добавить значение.
2. **Списки типов** — методы, формирующие `serverTypes` (пример: `serverTypes.Add(ServerType.BCS);` ~`:391`, `:524`): в общий список и в список торговых серверов.
3. **Фабрика `CreateServer`** (~`:777-923`): `else if (type == ServerType.X) { newServer = new XServer(uniqueNum); }`.
4. **`GetServerPermission`** (~`:1585-1743`): `else if (type == ServerType.X) { serverPermission = new XServerPermission(); }`.

---

## 2. Система параметров сервера

### 2.1. Кастомные параметры коннектора

Создаются в конструкторе обёртки **после** присваивания `ServerRealization`. Типы (`AServer.cs:500-687`): `CreateParameterString`, `CreateParameterInt`, `CreateParameterEnum`, `CreateParameterDecimal`, `CreateParameterBoolean`, `CreateParameterPassword`, `CreateParameterPath`, `CreateParameterButton`.

Порядок создания = порядок индексов в `ServerParameters`. Реализация читает значения по индексам (`((ServerParameterBool)ServerParameters[1]).Value`) — комментируй, какой индекс чему соответствует, и не меняй порядок создания без правки всех чтений.

Параметру можно задать `Comment` (кнопка «?» в UI) и подписаться на `ValueChange` (например, перезагрузка бумаг при смене галки класса — `TInvestServer.cs:56-59, 68-71`).

### 2.2. Стандартные параметры `AServer`

Сеттер `ServerRealization` (`AServer.cs:184-274`) создаёт стандартные параметры **сам**, до кастомных:

| # | Параметр | Назначение |
|---|----------|-----------|
| 0 | `ServerParam1` | сохранять тики |
| 1 | `ServerParam2` | глубина хранения тиков, дней |
| 2 | `ServerParam5` | сохранять свечи |
| 3 | `ServerParam6` | сколько свечей грузить |
| 4 | `ServerParam7` | Bid/Ask в трейды |
| 5 | `ServerParam8` | удалять трейды из памяти |
| 6 | `ServerParam9` | удалять свечи из памяти |
| 7 | `ServerParam10` | **полный стакан** |
| 8 | `ServerParam11` | только трейды с новой ценой |
| 9 | `ServerParam12` | кнопка «Бумаги» |
| 10 | `ServerParam14` | кнопка «Неторговые периоды» |
| +2 | прокси (enum + string) | если `IsSupports_ProxyFor_MultipleInstances` |
| +1 | кнопка плеча | если `Leverage_IsSupports` |
| +1 | CheckDataFeed | если `IsSupports_CheckDataFeedLogic` |

### 2.3. Механика `Insert`: раскладка индексов

Ключевой и неочевидный механизм (`AServer.cs:507-514` и далее): после того как сервер создан (`_serverIsCreated == true`), каждый `CreateParameter*` делает **не `Add`, а `Insert(Count - _serverStandardParamsCount, ...)`** — кастомный параметр вставляется **перед** стандартным блоком.

Итоговая раскладка списка `ServerParameters`:

```
[кастомные параметры коннектора: 0..N-1] + [стандартные: N..]
```

Следствия:

- Кастомные индексы стабильны: `[0]` — первый созданный в конструкторе параметр (обычно токен).
- Стандартные доступны абсолютным индексом `N + <номер из таблицы 2.2>`. Пример (BCS): 10 кастомных → стандартный `ServerParam10` (полный стакан) = `[10 + 7] = [17]`.
- Для доступа к стандартным предпочитай `GetStandardServerParameter(index)` (`AServer.cs:487`) — он считает с хвоста и не зависит от числа кастомных параметров.
- `_serverStandardParamsCount` выставляется фактическим (`AServer.cs:276`) — опциональные параметры (прокси/плечо/CheckDataFeed) входят в счёт.

### 2.4. Сохранение и загрузка

- Значения сохраняются в `Engine\<ServerNameUnique>Params.txt` автоматически (`SaveParam`, `AServer.cs:692`) при любом `ValueChange` — руками ничего сохранять не надо.
- При создании параметра значение подхватывается из файла через `LoadParam` — поэтому конструктор всегда передаёт дефолты, а не прошлые значения.
- `Comment` — локализованная строка через `OsLocalization` (помни правило 9.1 гайдлайнов: в тексте нельзя `:` и `_`).

---

## 3. Потоки и фоновые задачи

### 3.1. Обязательный `IsBackground = true`

Каждый поток, запускаемый в коннекторе, — `IsBackground = true`. Иначе процесс OsEngine не завершится штатно при закрытии.

```csharp
Thread worker = new Thread(ConnectionCheckThread);
worker.Name = "CheckAliveXServer";
worker.IsBackground = true;
worker.Start();
```

### 3.2. Именование и try-catch

- Имя потока — с суффиксом сервера (`"BcsDataMessageReader"`, `"PortfolioMessageReaderTInvest"`) — удобно в отладчике и дампах.
- Метод потока — `while (true)` с `try-catch` вокруг тела итерации и `Thread.Sleep` в ветках ожидания/ошибки (правило 5.3 гайдлайнов). Необработанное исключение в фоновом потоке падает молча.

### 3.3. Очереди сообщений и потоки-ридеры

Штатный паттерн разбора сообщений сокетов: событие `OnMessage` только кладёт строку в `ConcurrentQueue<string>`, а отдельный поток-ридер достаёт и парсит. Так обработчик сокета остаётся мгновенным (правило 5.6), а парсинг не блокирует приём.

```csharp
private ConcurrentQueue<string> WebSocketDataMessage = new ConcurrentQueue<string>();

private void WebSocketData_MessageReceived(object sender, MessageEventArgs e)
{
    // только проверки и Enqueue — без парсинга
    WebSocketDataMessage.Enqueue(e.Data);
}

private void DataMessageReader()
{
    Thread.Sleep(1000);

    while (true)
    {
        try
        {
            if (WebSocketDataMessage.IsEmpty)
            {
                Thread.Sleep(1);
                continue;
            }

            string message;
            WebSocketDataMessage.TryDequeue(out message);

            // парсинг и раздача событий
        }
        catch (Exception ex)
        {
            SendLogMessage(ex.ToString(), LogMessageType.Error);
            Thread.Sleep(5000);
        }
    }
}
```

Ридеры запускаются в конструкторе реализации (с `IsBackground = true`). Очереди пересоздавай в `Connect` — после реконнекта в них не должно остаться старья.

### 3.4. `RateGate`: стандартные лимиты

Все обращения к REST API — через `RateGate` (`Market/Servers/Entity/RateGate.cs`), конструктор `RateGate(int occurrences, TimeSpan timeUnit)`, вызов `WaitToProceed()` перед запросом. Заводится по одному на тип операций с понятным именем:

```csharp
private RateGate _rateGateCandles = new RateGate(1, TimeSpan.FromMilliseconds(500));
private RateGate _rateGateOrdersOperations = new RateGate(1, TimeSpan.FromMilliseconds(100));
```

Лимит — по документации биржи с запасом. `RateGate` реализует `IDisposable` — при `Dispose` коннектора освобождай.

### 3.5. async/await в коннекторах

- Общее правило проекта (гайдлайн 5.1–5.2): async — подозрителен, предпочтительны синхронные вызовы из фоновых потоков. Существующие коннекторы делают HTTP-запросы синхронно через `.Result` — копировать этот стиль допустимо для консистентности, но новый код лучше писать синхронно честно (`HttpClient.Send`).
- `async void` — запрещён.
- Штатная обёртка `WebSocket` асинхронна внутри (`ConnectAsync`, `SendAsync`) — это норма, снаружи вызывается без await.
- Для отправки ордеров есть штатный `AServerAsyncOrderSender` — включается permission `IsSupports_AsyncOrderSending` (+ `AsyncOrderSending_RateGateLimitMls`), сам коннектор ничего делать не должен.

---

## 4. Подключение и сессия

### 4.1. Токены и ключи

- Токен/ключ — параметр `CreateParameterPassword` (индекс `[0]` по традиции).
- Если у API два токена (refresh/access, как у БКС): refresh хранится в параметре, access перевыпускается в `Connect` и далее фоновым потоком за N минут до истечения (`CheckLifetimeToken`). О неизлечимой ошибке перевыпуска — `Disconnect` + сообщение в лог.
- При `Connect` сбрасывай и access-токен, и время истечения — иначе повторный вход пойдёт с мёртвым токеном (ошибка BCS).
- Предупреждение о скором истечении долгоживущего токена — в лог один раз, не спамом.

### 4.2. WebSocket-подключения

- Используй штатный `WebSocket` (см. 10.1). Авторизация — `SetHeader("Authorization", "Bearer " + token)`, пинги — `EmitOnPing = true` (если биржа требует), подписка на все четыре события: `OnOpen`, `OnClose`, `OnMessage`, `OnError`.
- Публичные сокеты (рыночные данные) и приватные (портфель, ордера, сделки) — раздельно.
- `OnClose` при активном статусе = потеря соединения → `ServerStatus = Disconnect` + `DisconnectEvent()`. В `OnError` фильтруй штатные ошибки закрытия («The remote party closed the WebSocket connection»), остальное — в лог.

#### 4.2.1. Лимит подписок на сокет и пул сокетов

У многих бирж есть ограничение на количество подписок (стакан + лента сделок) в рамках одного WebSocket-соединения. Поэтому коннекторы держат **пул публичных сокетов** (`List<WebSocket>`): пока подписок мало — работает один сокет, при превышении порога создаётся новый, и последующие подписки уходят в него.

Каноническая реализация — `BitGetServerFutures.cs:1663-1687` (и скопировано оттуда в BCS `:1663-1688`, BingX, Bitfinex, BitMart и др.): при `Count % 30 == 0` создаётся новый сокет, до 10 секунд ждём его открытия, добавляем в пул, подписку отправляем в последний сокет списка:

```csharp
WebSocket webSocketPublic = _webSocketPublicList[_webSocketPublicList.Count - 1];

if (webSocketPublic.ReadyState == WebSocketState.Open
    && _subscribedSecurities.Count != 0
    && _subscribedSecurities.Count % 30 == 0)
{
    WebSocket newSocket = CreateNewSocketMarketData();

    DateTime timeEnd = DateTime.Now.AddSeconds(10);

    while (newSocket.ReadyState != WebSocketState.Open)
    {
        Thread.Sleep(1000);

        if (timeEnd < DateTime.Now)
        {
            break;
        }
    }

    if (newSocket.ReadyState == WebSocketState.Open)
    {
        _webSocketPublicList.Add(newSocket);
        webSocketPublic = newSocket;
    }
}

// подписка уходит в webSocketPublic (последний живой сокет пула)
```

Правила для пула:

- Порог — из документации биржи с запасом (30 — типовое значение в проекте).
- При `Dispose`/реконнекте закрывается **весь пул** с отпиской событий каждого сокета, список очищается — иначе дубли (см. 4.4 и 11.6).
- Если биржа вместо лимита присылает ошибку «превышено число подписок» — логируй и прекращай новые подписки (пример: `_hasLimitReached` в BCS).

Заметь: не везде пул нужен. **Alor** (`AlorServer.cs:1566-1699`) обходится одним публичным сокетом, зато ведёт **реестр подписок по guid** (`_subscriptionsData`: guid → `AlorSocketSubscription{SubType, ServiceInfo, Guid}`) — это даёт точечную `Unsubscribe(security)` по каждой бумаге и типу подписки. Если у биржи нет лимита на подписки, но есть отписки по id — бери алоровский паттерн; если лимит есть — пул в духе BitGet; при необходимости они комбинируются.

### 4.3. Активация соединения

`ConnectEvent` вызываем только когда реально всё живо: токен получен, все сокеты открыты. Штатный приём — флаги активации каждого сокета и общая проверка (`CheckActivationSockets` у BCS): каждый `OnOpen` ставит свой флаг и вызывает проверку; когда все флаги true (под lock) — статус Connect + `ConnectEvent()`. Не вызывай `ConnectEvent` из `Connect` до фактической готовности — движок сразу начнёт слать запросы.

### 4.4. Реконнект: что чистить обязательно

При повторном `Connect` (и в `Dispose`) обнуляй состояние полностью, иначе дубли:

- списки/словари бумаг и подписок (`_securities`, `_subscribedSecurities`);
- **пул сокетов** — старые сокеты отписать от событий и закрыть (`DeleteWebSocketConnection`); забытый пул = дубли подписок и событий (ошибка BCS);
- очереди сообщений (`ConcurrentQueue`) — пересоздать;
- токены и их expire-время, кэши портфелей, маппинги номеров ордеров;
- временные метки дедупликации (`_lastMdTime` и т.п.).

### 4.5. Прокси

Инфраструктура прокси уже сделана движком: при `IsSupports_ProxyFor_MultipleInstances = true` в permission `AServer` создаёт стандартные параметры (тип None/Auto/Manual + адрес), строит `WebProxy` через `ProxyMaster` (`AServer.cs:1141`, режимы Auto/Manual — `ProxyMaster.cs:116, 171`) и передаёт его в `Connect(WebProxy proxy)` (`AServer.cs:1252`).

От коннектора требуется только принять и применить — **во всех** сетевых клиентах без исключения:

- `HttpClientHandler.Proxy = proxy` + `UseProxy = true` (иначе `UseProxy = false`);
- `webSocket.SetProxy(proxy)` — у каждого сокета пула;
- сторонние REST-клиенты, если есть (например `RestClient.Proxy` — у BCS это клиент перевыпуска токена, его легко забыть).

Всегда проверяй `if (proxy != null)` перед установкой. Типичный баг — прокси применён не везде: сокеты ходят через прокси, а REST мимо (или наоборот), и коннектор «полуработает».

---

## 5. Бумаги (Securities)

### 5.1. Загрузка и маппинг типов

`GetSecurities()` вызывается движком после Connect. Читаем галки классов (`UseStock`, `UseFutures`, ... — стандартные локализованные имена из `OsLocalization.Market`), грузим по каждому включённому типу, заполняем `_securities`, в конце — `SecurityEvent?.Invoke(_securities)`.

Тип бумаги биржи маппится в `SecurityType` (`OsEngine/Entity/Security.cs:415`): `Stock`, `Futures`, `Option`, `Bond`, `Fund`, `Index`, `CurrencyPair`, `Commodities`. Неизвестный тип — `SecurityType.None` и бумагу пропускаем.

`NameClass` — строка класса/режима, по ней UI группирует бумаги в комбобоксах. Соглашения по проекту:

- TInvest: `<SecurityType> <currency>` для спота (`Stock rub`), `Futures` для фьючерсов MOEX, `FuturesNeoSpb` для нео-активов СПБ;
- BCS: `<TYPE>-<classCode>` (`STOCK-TQBR`);
- класс должен однозначно определять режим торгов: из него потом достаётся classCode биржи (BCS: `NameClass.Split('-')[1]`).

### 5.2. Обязательные поля `Security`

| Поле | Комментарий |
|------|-------------|
| `Name` | тикер, как в API |
| `NameId` | уникальный id (isin/uid); не должен повторяться |
| `NameFull` | человеческое имя |
| `NameClass` | см. 5.1 |
| `SecurityType` | см. 5.1 |
| `Exchange` | строка биржи из API |
| `Lot` | размер лота; для фьючерсов/опционов обычно 1 |
| `PriceStep` / `Decimals` | шаг цены и его разрядность |
| `PriceStepCost` | стоимость шага; если API не отдаёт — = `PriceStep` |
| `VolumeStep` / `DecimalsVolume` | шаг объёма |
| `State` | `SecurityStateType.Activ` |

Дополнительно по типам: облигации — `NominalCurrent`, `AciValue`, `PlacementDate`, `MaturityDate`; фьючерсы/опционы — `Expiration`, `UsePriceStepCostToCalculateVolume = true`; опционы — `Strike`, `OptionType`, `UnderlyingAsset`.

Типичные ошибки: `Lot = 0` (деление на ноль при пересчёте объёмов), `PriceStep = 0`, нулевые значения, если поле не пришло из API — всегда ставь дефолт (1).

### 5.3. Подписка/отписка

`Subscribe(Security)` — движок зовёт на каждую бумагу, которую торгуют роботы. Правила:

- дедупликация по имени бумаги до отправки;
- через `RateGate`;
- подписки на трейды и стакан — отдельными сообщениями; глубина стакана — из стандартного параметра «полный стакан» + кастомного enum уровней;
- список `_subscribedSecurities` — единственный источник правды, по нему делается `UnsubscribeAllSecurities` в `Dispose`;
- `Unsubscribe(Security)` — по умолчанию пустой; реализуй, если API умеет отписку по одной бумаге и это нужно (массовые подписки).

---

## 6. Портфели и позиции

### 6.1. Маппинг в `Portfolio` / `PositionOnBoard`

`GetPortfolios()` — после загрузки бумаг. По каждому счёту — `Portfolio` с `Number`, `ValueBegin`/`ValueCurrent`/`ValueBlocked`, `UnrealizedPnl`. Позиции — `PositionOnBoard`: `SecurityNameCode` (тикер), `ValueBegin`/`ValueCurrent`/`ValueBlocked`, `UnrealizedPnl`, `PortfolioName`.

Валютные позиции (рубли и т.п.) приходят тем же списком — имена-исключения для закрытия позиций на доске объявляются в permission (`ManuallyClosePositionOnBoard_ExceptionPositionNames`, пример BCS: `RUB`, `CNY`).

### 6.2. Обновления

- Первичная загрузка — REST; дальше — сокет (приватный поток портфеля) в ту же функцию обновления (`UpdateMyPortfolio`).
- При обновлении создавай недостающие `PositionOnBoard` и **удаляй исчезнувшие** (закрытые) позиции — иначе доска будет врать.
- `PortfolioEvent` — после каждого осмысленного обновления, не чаще.

---

## 7. Рыночные данные

### 7.1. Свечи

Два метода: `GetLastCandleHistory` (N последних свечей для старта робота) и `GetCandleDataToSecurity` (диапазон для OsData). Общая логика:

- Маппинг `TimeFrameBuilder.TimeFrameTimeSpan` → строка таймфрейма API; неподдержанный ТФ — `null`, и в permission этот ТФ должен быть выключен (см. 9).
- Пагинация по лимиту API (BCS: по 1440 свечей на запрос), склейка с дедупликацией граничной свечи.
- `RateGate` на каждый запрос.
- Время свечей — в московском времени (или зоне биржи): конвертация из UTC через `TimeZoneInfo`. Не смешивай локальное время и `UtcNow` в проверках (ошибка BCS `CheckTime`).
- Дневная свеча «сегодня» обычно недоступна — отсекай `endTime` до начала текущих суток.
- `Candle.State = CandleState.Finished` для исторических.

### 7.2. Тики/трейды

- Поток: из сообщений сокета формируй `Trade` (`SecurityNameCode`, `Time`, `Price`, `Volume`, `Side`, `Id` = `Time.Ticks`) → `NewTradesEvent`. Ищи бумагу по словарю, а не линейным поиском на каждый тик (ошибка BCS).
- Фильтр утреннего аукциона (параметр `IgnoreMorningAuctionTrades`): акции — игнор до 7:00 мск, фьючерсы — до 9:00 мск. Фильтруй по типу конкретной бумаги сделки.
- `GetTickDataToSecurity` — история тиков для OsData; если API не отдаёт — `return null` и `DataFeedTfTickCanLoad = false` в permission.

### 7.3. Стакан

- `MarketDepth`: `SecurityNameCode`, `Time`, `Bids`/`Asks` (`MarketDepthLevel` с `Price`, `Bid`/`Ask`).
- Пропускай пустые стаканы (0 бидов или 0 асков).
- Время стакана — монотонно: если биржа прислала время ≤ предыдущего, сдвигай на тик (`_lastMdTime`, пример BCS `:1969-1975`).
- Единицы объёма (лоты vs штуки) — из полей бумаги, не магическими константами (антипример: `/10` в BCS).

---

## 8. Ордера и сделки

### 8.1. Отправка/отмена/изменение

- `SendOrder(Order)`: собери запрос из полей ордера; **перед отправкой проверь, что бумага найдена и объём/цена ненулевые** — ордер с нулевым количеством на биржу не уходит никогда (ошибка BCS). При любой ошибке — `order.State = OrderStateType.Fail` + `MyOrderEvent(order)` + лог (штатный приём `CreateOrderFail`).
- `CancelOrder(Order)` → `bool`; `ChangeOrderPrice(Order, newPrice)` — если биржа умеет (permission `IsCanChangeOrderPrice`).
- `CancelAllOrders`, `CancelAllOrdersToSecurity` — через список активных с биржи.
- Все операции — через общий `_rateGateOrdersOperations`.

### 8.2. Номера ордеров

Движок оперирует `NumberUser` (int), биржа — своими id (`NumberMarket`, clientOrderId). Держи двунаправленный маппинг под lock (пример BCS: `_guidByNumberOrders` / `_numberByGuidOrders` / `_userNumberByOrderId`) с ограничением размера (очередь на удаление старых). Если биржа при смене цены меняет номер ордера — храни цепочку номеров (`_changedOrderNumsMarket` в BCS), в событиях отдавай **первоначальный** `NumberMarket`, иначе движок не сопоставит ордер.

### 8.3. Статусы

Маппинг строковых кодов биржи в `OrderStateType` (`Active`, `Partial`, `Done`, `Cancel`, `Fail`, `Pending`, `None`) — одной функцией `GetOrderState(string status)`. Коды — строго по документации API, без «похожих» строк: опечатка вроде `" 1"` с пробелом превращает типы ордеров в мусор (ошибка BCS). Неизвестный статус — `None` + лог.

### 8.4. `MyTrade`

Поля: `SecurityNameCode`, `NumberTrade`, `NumberOrderParent`, `Time`, `Price`, `Volume`, `Side`. Объём — в тех же единицах, что и в ордерах (лоты/штуки — единообразно во всех методах коннектора; BCS делит на `Lot` в сокете и не делит в истории — так нельзя). Комиссию клади в `MyTrade`, если API отдаёт.

### 8.5. История и активные ордера, дедупликация

- `GetActiveOrders(startIndex, count)` / `GetHistoricalOrders(startIndex, count)` — постраничная выгрузка из API (пагинация, сортировка по времени убыв.), маппинг в `Order` через общий конвертер.
- `GetAllActivOrders` — для восстановления после реконнекта (permission `CanQueryOrdersAfterReconnect`).
- `GetOrderStatus(Order)` — только запрос и возврат статуса; **не вызывай из него `MyOrderEvent`** — опрос статуса не должен порождать событие (дубли в роботах, ошибка BCS). События — только из сокета исполнения.

---

## 9. Permission-файл

### 9.1. DataFeedPermissions

Что может качать OsData из этого коннектора: `DataFeedTf*CanLoad` по таймфреймам (секунды, тики, стакан) и минуты/часы/дни. Правило: включён только тот ТФ, который реально отдаёт реализация (`GetCandleTimeFrame`/тики/стакан). Пример рассинхрона: у BCS `TradeTimeFramePermission.Hour2 = true`, а `GetCandleTimeFrame` H2 не умеет — пользователь видит ТФ, но данных не получает.

### 9.2. TradePermissions и TimeFramePermission

- `MarketOrdersIsSupport` — есть ли маркет-ордера.
- `IsCanChangeOrderPrice` — умеет ли биржа изменение цены ордера.
- `UseStandardCandlesStarter` — штатный механизм подкачки свечей (почти всегда true).
- `IsUseLotToCalculateProfit` — профит через стоимость шага (фондовый/срочный рынок РФ — обычно true).
- `HaveOnlyMakerLimitsRealization` — ставь true **только** если коннектор реально шлёт лимиты как maker/post-only. У BCS стоит true при обычных лимитах — permission врёт движку.
- `ManuallyClosePositionOnBoard_ExceptionPositionNames` — «бумаги»-валюты, которые не закрывать при ручном закрытии позиций.
- `TradeTimeFramePermission` — ТФ, доступные роботам при торговле через этот коннектор. Синхронизируй с 7.1: чего реализация не умеет — здесь должно быть false.

### 9.3. Permission не должен врать

Permission — контракт с движком: по нему включаются подсистемы (асинхронная отправка ордеров, CheckDataFeed, плечо, прокси). Правила:

- включённая capability должна иметь реализацию (`IsSupports_AsyncCandlesStarter`, `IsSupports_AsyncOrderSending` — работают сами через AServer, остальное — руками);
- выключенный в permission ТФ не должен появляться в реализации и наоборот;
- при сомнении — false: движок аккуратно скроет функцию, а врётство в true приводит к падениям в бою.

---

## 10. Штатные классы-хелперы

Чтобы коннекторы были консистентными, внутри них используются только штатные классы проекта, а не свои велосипеды.

### 10.1. `WebSocket` — штатная обёртка

`OsEngine/Entity/WebSocketOsEngine.cs` (namespace `OsEngine.Entity.WebSocketOsEngine`). Собственная обёртка проекта над клиентским WebSocket.

```csharp
WebSocket ws = new WebSocket("wss://...");
ws.SetHeader("Authorization", "Bearer " + token);
ws.SetHeader("sec-websocket-protocol", "...");
if (_myProxy != null) ws.SetProxy(_myProxy);
ws.EmitOnPing = true;                    // пробрасывать ping в OnMessage
ws.OnOpen += Ws_Opened;
ws.OnClose += Ws_Closed;                 // CloseEventArgs
ws.OnMessage += Ws_MessageReceived;      // MessageEventArgs.Data
ws.OnError += Ws_Error;                  // ErrorEventArgs.Exception
ws.ConnectAsync(TimeSpan.FromSeconds(30), _httpClient);
ws.SendAsync("{\"subscribe\": ...}");
// состояние: ws.ReadyState == WebSocketState.Open
// закрытие: отписаться от событий, ws.CloseAsync()
```

Перед закрытием всегда отписывай обработчики (правило 5.4) и проверяй `ReadyState == WebSocketState.Open` перед `CloseAsync`.

### 10.2. `RateGate` — штатный ограничитель частоты

`OsEngine/Market/Servers/Entity/RateGate.cs`. `new RateGate(occurrences, timeUnit)` + `WaitToProceed()` перед каждым вызовом API. Один экземпляр на тип операций. `IDisposable`.

### 10.3. `TimeManager` — время

`OsEngine/Market/Servers/Entity/TimeManager.cs`. Статические методы: `GetExchangeTime(timeZone)`, `GetDateTimeFromTimeStamp(мс)`, `GetDateTimeFromTimeStampSeconds`, `GetUnixTimeStampSeconds/Milliseconds`, конвертации DateTime ↔ timestamp. Не пиши свои конвертеры эпохи.

### 10.4. `ServerParameter*` — типы параметров

`OsEngine/Market/Servers/Entity/ServerParameter.cs`: `ServerParameterBool/Int/String/Enum/Decimal/Password/Path/Button`. Общее: `Name`, `Value`, `Comment`, `ValueChange()`, `GetStringToSave()`. Кастомные параметры создавай только через `CreateParameter*` обёртки — руками в `ServerParameters` не добавляй.

### 10.5. `Extensions` — конвертации

`OsEngine/Entity/Extensions.cs`: `ToDecimal()` (точка и запятая), `ToDouble()` и др. Любой парсинг чисел из JSON/строк — только через них (правило 7.5 гайдлайнов). Все Entity-классы коннекторов держат числа строками и конвертируются через `ToDecimal()` — это норма проекта.

### 10.6. Штатные паттерны BCL

- `ConcurrentQueue<string>` + поток-ридер — для сообщений сокетов (см. 3.3).
- `HttpClient` (+ `HttpClientHandler`) — основной REST-клиент новых коннекторов. `RestSharp` встречается в старых местах (перевыпуск токена BCS) — не разносить его на новый код.
- `lock` на строковых константах-локерах (`private string _locker = "...";`) — принятый в проекте стиль для мелких критических секций.

### 10.7. Что НЕ является штатным

- Свои обёртки над WebSocket/HTTP, свои retry-очереди, самодельные rate-лимитеры.
- `Console.WriteLine` — в десктопном приложении никуда не выводит; лог только через `SendLogMessage` (ошибка BCS).
- Прямые вызовы WPF/UI из реализации — реализация говорит с движком только событиями.

---

## 11. Типичные ошибки и антипаттерны

> Сборник из ревью коннекторов TInvest и BCS. Каждый пункт — реально встречавшийся баг.

### 11.1. Потоки без `IsBackground`

Процесс не завершится штатно. Встречалось во многих старых коннекторах (TInvest, Alor, BCS и др.) — исправлено по всему проекту, не возвращать.

### 11.2. Null-обращения при ненайденной бумаге

`security.Lot`, `security.PriceStep` без проверки: бумага не найдена (не подписана, реконнект, чужой classCode) → NRE. Перед использованием — `if (security == null)` с осмысленной веткой.

### 11.3. Нулевой объём/цена при маппинге

Бумага не найдена → `quantity = 0` → ордер с нулём уходит на биржу (BCS). Проверки до отправки обязательны. То же про `PriceStep = 0` / `Lot = 0` в бумагах.

### 11.4. Магические числа вместо полей бумаги

`/10` в объёмах стакана, хардкод-списки базовых активов опционов, захардкоженные коэффициенты. Единицы — из полей `Security`; списки — из API.

### 11.5. Опечатки в строковых кодах API

`record.orderType == " 1"` (с пробелом) — ветка никогда не срабатывает, маркет-ордера стали `Iceberg`. Строковые коды — выносить в константы и сверять с докой.

### 11.6. Неочищенное состояние при реконнекте

Пул сокетов не закрыт → дубли подписок и событий. Токен не сброшен → вход с мёртвым токеном. Очереди не пересозданы → старые сообщения. Чек-лист очистки — в 4.4.

### 11.7. `Console.WriteLine`, пустые catch, мёртвый код

`Console.WriteLine` в GUI невидим — только `SendLogMessage`. Пустой `catch { }` запрещён (гайдлайн 7.3). Мёртвые переменные и дважды перезаписываемые поля (`order.Volume` в BCS) — признак небрежной копипасты.

### 11.8. Смешение локального времени и UTC

`startTime >= DateTime.UtcNow` при локальном `startTime` — данные «за сегодня» отваливаются до 03:00 мск. В проекте рыночное время — московское (конвертация через `_mskTimeZone`), проверки делаются в той же зоне.

---

## 12. Чек-лист нового коннектора

### 12.1. Перед началом

1. Документация API: аутентификация, лимиты (rps), форматы (REST/WS/gRPC), коды статусов ордеров, единицы объёмов.
2. Тестовый доступ: токен/счёт песочницы или реальный счёт с минимальным объёмом.
3. Образец: за основу берётся ближайший по протоколу живой коннектор (REST+WS → BCS/крипта, gRPC → TInvest/FinamGrpc).

### 12.2. Минимальный каркас

1. `ServerType` + регистрация в `ServerMaster` (3 места, см. 1.5).
2. Обёртка с параметрами → реализация с `Connect`/`Dispose` → permission с честными false.
3. Бумаги (`GetSecurities` + `SecurityEvent`) → портфели → подписки (трейды/стакан) → свечи → ордера (отправка/статусы/события) → история ордеров.
4. Сборка после каждого этапа: `dotnet build OsEngine/OsEngine.csproj`.

### 12.3. Перед коммитом

- [ ] Сборка чистая, `IsBackground = true` у всех потоков, нет `Console.WriteLine` и пустых catch.
- [ ] Connect → бумаги → портфель → подписка (трейды + стакан идут в UI).
- [ ] Реconnect руками: нет дублей сокетов/подписок/событий.
- [ ] Лимит-ордер на тестовом/минимальном объёме: актив → смена цены → отмена → маркет-исполнение; статусы и `MyTrade` корректны, объёмы в лотах.
- [ ] Permission соответствует реализации (глава 9).
- [ ] Числа парсятся через `ToDecimal()`, время — в зоне биржи.

### 12.4. Документация

- Если появились новые штатные приёмы/классы — глава 10; новые антипаттерны — глава 11.
- Карта проекта `CONTEXT.md` и `AGENTS.md` — при изменении соглашений.

