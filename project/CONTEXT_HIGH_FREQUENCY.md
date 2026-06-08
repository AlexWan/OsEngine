# CONTEXT_HIGH_FREQUENCY — Высокочастотная торговля и стакан в OsEngine

Роботы, работающие на событиях изменения стакана (`MarketDepthUpdateEvent`) и ленты сделок (`NewTickEvent`). Эти стратегии требуют особого подхода к потокобезопасности, throttling и управлению ордерами.

Базовая архитектура — в `CONTEXT.md`, каталог остальных роботов — в `CONTEXT_ROBOTS.md`.

---

## 1. Общие принципы HFT в OsEngine

### 1.1 Скорость событий

| Событие | Частота | Когда приходит | Для чего |
|---------|---------|----------------|----------|
| `CandleFinishedEvent` | Редко (раз в минуту/час) | Закрылась свеча | Стандартные стратегии |
| `MarketDepthUpdateEvent` | Часто (сотни раз в секунду) | Изменился стакан | HFT, анализ плотности, перестановка ордеров |
| `NewTickEvent` | Очень часто (несколько раз в секунду) | Новая сделка (тик) | Лента сделок, трейлинг-стоп, детекция пампа |

**Важно:** `MarketDepthUpdateEvent` и `NewTickEvent` приходят из других потоков. Всегда используйте `try-catch` и избегайте долгих операций в обработчиках.

### 1.2 Throttling — защита от флуда

В реальной торговле (`StartProgram.IsOsTrader`) события идут слишком быстро. Нужно ограничивать частоту обработки:

```csharp
private DateTime _lastCheckTime = DateTime.MinValue;

void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
{
    // В реальной торговле проверяем стакан не чаще 1 раза в секунду
    if (StartProgram == StartProgram.IsOsTrader &&
        _lastCheckTime.AddSeconds(1) > DateTime.Now)
    {
        return;
    }
    _lastCheckTime = DateTime.Now;
    // ... логика ...
}
```

### 1.3 Отключение ручного сопровождения

HFT-роботы управляют ордерами автоматически. Ручное вмешательство пользователя может мешать:

```csharp
_tab.ManualPositionSupport.DisableManualSupport();
```

### 1.4 Потокобезопасность

События стакана и тиков приходят из сетевых потоков. Правила:
- Не используйте общие переменные без `lock`
- Для тяжёлых операций используйте фоновые `Thread` или `Task`
- Всегда оборачивайте логику в `try-catch`

---

## 2. Быстрые шаблоны

### 2.1 Робот на `MarketDepthUpdateEvent`

```csharp
[Bot("MyHftBot")]
public class MyHftBot : BotPanel
{
    private BotTabSimple _tab;
    private DateTime _lastCheckTime = DateTime.MinValue;

    public MyHftBot(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        _tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
        _tab.ManualPositionSupport.DisableManualSupport();
    }

    void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
    {
        try
        {
            // Throttling: не чаще 1 раза в секунду в реальной торговле
            if (StartProgram == StartProgram.IsOsTrader &&
                _lastCheckTime.AddSeconds(1) > DateTime.Now)
                return;
            _lastCheckTime = DateTime.Now;

            // Защита от пустого стакана
            if (marketDepth.Asks == null || marketDepth.Asks.Count == 0 ||
                marketDepth.Bids == null || marketDepth.Bids.Count == 0)
                return;

            // Получаем копию стакана (чтобы данные не изменились во время чтения)
            MarketDepth md = marketDepth.GetCopy();

            decimal bestBid = md.Bids[0].Price.ToDecimal();
            decimal bestAsk = md.Asks[0].Price.ToDecimal();
            decimal bestBidVolume = md.Bids[0].Bid.ToDecimal();

            // Торговая логика...
        }
        catch (Exception error)
        {
            SendNewLogMessage(error.ToString(), LogMessageType.Error);
        }
    }
}
```

### 2.2 Робот на `NewTickEvent`

```csharp
[Bot("MyTickBot")]
public class MyTickBot : BotPanel
{
    private BotTabSimple _tab;

    public MyTickBot(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        _tab.NewTickEvent += _tab_NewTickEvent;
    }

    private void _tab_NewTickEvent(Trade trade)
    {
        try
        {
            Position pos = _tab.PositionsOpenAll[0];
            if (pos == null) return;

            // Трейлинг-стоп по цене последней сделки
            decimal stopPrice, orderPrice;
            if (pos.Direction == Side.Buy)
            {
                stopPrice = trade.Price - (trade.Price * 0.005m);
                orderPrice = stopPrice - _tab.Security.PriceStep;
            }
            else
            {
                stopPrice = trade.Price + (trade.Price * 0.005m);
                orderPrice = stopPrice + _tab.Security.PriceStep;
            }

            _tab.CloseAtTrailingStop(pos, stopPrice, orderPrice);
        }
        catch (Exception error)
        {
            SendNewLogMessage(error.ToString(), LogMessageType.Error);
        }
    }
}
```

### 2.3 Робот с фоновым `Thread`

```csharp
[Bot("MyThreadBot")]
public class MyThreadBot : BotPanel
{
    private BotTabSimple _tab;
    private bool _isDisposed = false;

    public MyThreadBot(string name, StartProgram startProgram) : base(name, startProgram)
    {
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        Thread worker = new Thread(Logic);
        worker.IsBackground = true;
        worker.Start();

        DeleteEvent += MyThreadBot_DeleteEvent;
    }

    private void MyThreadBot_DeleteEvent()
    {
        _isDisposed = true;
    }

    private void Logic()
    {
        while (true)
        {
            try
            {
                Thread.Sleep(1000); // пауза между итерациями

                if (_isDisposed) return;

                // Читаем текущие цены
                decimal bestBid = _tab.PriceBestBid;
                decimal bestAsk = _tab.PriceBestAsk;

                // Торговая логика...
            }
            catch (Exception e)
            {
                _tab.SetNewLogMessage(e.ToString(), LogMessageType.Error);
                Thread.Sleep(5000);
            }
        }
    }
}
```

---

## 3. Справочник API стакана и тиков

### 3.1 `MarketDepth` — стакан

```csharp
// Получение копии (важно!)
MarketDepth md = marketDepth.GetCopy();

// Лучшие цены
decimal bestBidPrice = md.Bids[0].Price.ToDecimal();
decimal bestBidVolume = md.Bids[0].Bid.ToDecimal();
decimal bestAskPrice = md.Asks[0].Price.ToDecimal();
decimal bestAskVolume = md.Asks[0].Ask.ToDecimal();

// Перебор уровней
for (int i = 0; i < md.Bids.Count && i < maxLevels; i++)
{
    decimal price = md.Bids[i].Price.ToDecimal();
    decimal volume = md.Bids[i].Bid.ToDecimal();
}
```

### 3.2 `Trade` — тик (сделка)

```csharp
private void _tab_NewTickEvent(Trade trade)
{
    decimal price = trade.Price;
    decimal volume = trade.Volume;
    Side side = trade.Side; // Buy / Sell
    DateTime time = trade.Time;
}
```

### 3.3 `ChangeOrderPrice` — перестановка цены ордера

```csharp
_tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;

private void _tab_MarketDepthUpdateEvent(MarketDepth md)
{
    Position pos = _tab.PositionsOpenAll[0];
    if (pos == null || pos.State != PositionStateType.Opening) return;

    // Не чаще раз в N секунд
    if (_lastChangeTime.AddSeconds(_seconds.ValueInt) > DateTime.Now) return;

    // Новая цена на основе текущего стакана
    decimal newPrice = _tab.PriceBestBid - _tab.PriceBestBid * (_slippagePercent.ValueDecimal / 100);
    newPrice = Math.Round(newPrice, _tab.Security.Decimals);

    _tab.ChangeOrderPrice(pos.OpenOrders[0], newPrice);
    _lastChangeTime = DateTime.Now;
}
```

### 3.4 `Task` для асинхронных операций

```csharp
// Очередь позиций для закрытия
private List<Position> _positionsToClose = new List<Position>();

// Запуск фонового Task в конструкторе
Task task = new Task(ClosePositionThreadArea);
task.Start();

private void ClosePositionThreadArea()
{
    while (true)
    {
        try
        {
            Thread.Sleep(200);

            if (_positionsToClose.Count == 0) continue;

            Position pos = _positionsToClose[0];
            _positionsToClose.RemoveAt(0);

            // Закрываем позицию
            _tab.CloseAllOrderToPosition(pos);
        }
        catch (Exception error)
        {
            SendNewLogMessage(error.ToString(), LogMessageType.Error);
            Thread.Sleep(5000);
        }
    }
}
```

---

## 4. Каталог HFT-роботов

### 4.1 `Fisher` — отклонение от края стакана (фоновый Thread)

**Сигнал:** цена отклонилась от лучшего бида/аска на заданный `%` (`PersentFromBorder`).  
**Выход:** откат на 50% или более от движения входа.  
**Фильтр:** SMA — торгуем только если цена в пределах ±1% от SMA.

**Ключевые паттерны:**
- Фоновый `Thread worker` (`IsBackground = true`) с `while(true)` и `Thread.Sleep(TimeRebuildOrder * 1000)`
- Жёсткий жизненный цикл: `CanselAllOrders()` → `CloseAllPositions()` → `OpenOrders()`
- Индикатор SMA через `_tab.CreateCandleIndicator()`

**Изучать:** `Fisher.cs` — базовый HFT на фоновом потоке.

### 4.2 `HighFrequencyTrader` — анализ плотности стакана (MarketDepthUpdateEvent)

**Сигнал:** находит уровень с максимальным объёмом среди топ-N бидов/асков и ставит лимитный ордер на один `PriceStep` лучше.  
**Выход:** стоп и профит в шагах цены (`Stop`, `Profit`).  
**Особенности:** одновременно держит два ордера (Buy и Sell).

**Ключевые паттерны:**
- `MarketDepthUpdateEvent` — основная логика
- Throttling 1 секунда в реальном режиме (`_lastCheckTime.AddSeconds(1) > DateTime.Now`)
- `Task` + очередь `_positionsToClose` для безопасного снятия ордеров
- `PositionOpeningSuccesEvent` — выставление стоп/профит
- `PositionClosingFailEvent` — аварийное закрытие по рынку

**Изучать:** `HighFrequencyTrader.cs` — событийно-ориентированный HFT.

### 4.3 `MarketDepthScreener` — детекция «плиты» на скринере (фоновый Thread)

**Сигнал:** двухшаговый вход:
1. Momentum ниже минимального значения → идём дальше
2. Объём лучшего бида мал относительно остальных (ratio < порога) → ждём «плиты»  
**Выход:** стоп/профит + время жизни ордера (`OrderLifeTime` в мс).

**Ключевые паттерны:**
- `BotTabScreener` + фоновый `Thread` с `Thread.Sleep(100)`
- Перебор всех вкладок: `for (int i = 0; i < _tabScreener.Tabs.Count; i++)`
- `tab.MarketDepth.GetCopy()` — чтение стакана каждые 100 мс
- `ManualPositionSupport.DisableManualSupport()`
- Проверка времени жизни ордера: `order.TimeCreate.AddMilliseconds(OrderLifeTime) < tab.TimeServerCurrent`

**Изучать:** `MarketDepthScreener.cs` — скринер со стаканом.

### 4.4 `PlateDetectorScreener` — детекция «плиты» через событие (MarketDepthUpdateEvent на скринере)

**Сигнал:** соотношение объёма лучшего бида к остальным бидом выше порога.  
**Вход:** лимитный ордер на `PriceStep` выше лучшего бида.  
**Выход:** стоп/профит.

**Ключевые паттерны:**
- `MarketDepthUpdateEvent` на уровне скринера (`_tabScreener.MarketDepthUpdateEvent`)
- Обработчик получает `MarketDepth` и `BotTabSimple tab`

**Изучать:** `PlateDetectorScreener.cs` — событийный скринер.

### 4.5 `PumpDetectorScreener` — детекция пампа (NewTickEvent на скринере)

**Сигнал:** цена изменилась на `MoveToEntry` за `SecondsToAnalyze`.  
**Вход:** при превышении порога — вход в позицию.  
**Выход:** стоп/профит.

**Ключевые паттерны:**
- `NewTickEvent` на уровне скринера
- Быстрый вход при аномальном движении

**Изучать:** `PumpDetectorScreener.cs` — детекция пампа по тикам.

### 4.6 `StopByTradeFeedSample` — трейлинг-стоп по ленте сделок (NewTickEvent)

**Сигнал:** вход по пробою `PriceChannel`.  
**Выход:** трейлинг-стоп обновляется при каждом новом тике (`NewTickEvent`) на основе цены последней сделки.

**Ключевые паттерны:**
- `NewTickEvent` для обновления стопа
- Расчёт стопа от цены трейда: `stopPrice = trade.Price - (trade.Price * _trailStopPercent / 100)`

**Изучать:** `StopByTradeFeedSample.cs` — трейлинг по ленте сделок.

### 4.7 `ChangePriceBotExtStopMarket` — перестановка цены ордера (MarketDepthUpdateEvent)

**Сигнал:** кнопка запуска.  
**Логика:** при обновлении стакана переставляет цену открывающего ордера на `%` от лучшего бида/аска. При открытии — стоп-маркет и профит-маркет.

**Ключевые паттерны:**
- `MarketDepthUpdateEvent` + throttling по времени
- `ChangeOrderPrice(order, newPrice)` — перестановка цены активного ордера
- `ManualPositionSupport.DisableManualSupport()`
- Обработка `PositionOpeningSuccesEvent` и `PositionClosingSuccesEvent`

**Изучать:** `ChangePriceBotExtStopMarket.cs` — управление ценой ордера.

---

## 5. Примеры кода из реальных роботов

### 5.1 Работа со стаканом (из `HighFrequencyTrader`)

```csharp
void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
{
    // Защита от пустого стакана
    if (marketDepth.Asks == null || marketDepth.Asks.Count == 0 ||
        marketDepth.Bids == null || marketDepth.Bids.Count == 0)
        return;

    // Throttling в реальном режиме
    if (StartProgram == StartProgram.IsOsTrader &&
        _lastCheckTime.AddSeconds(1) > DateTime.Now)
        return;
    _lastCheckTime = DateTime.Now;

    // Поиск уровня с максимальным объёмом среди бидов
    decimal buyPrice = 0;
    int lastVolume = 0;
    for (int i = 0; i < marketDepth.Bids.Count && i < _maxLevelsInMarketDepth.ValueInt; i++)
    {
        if (marketDepth.Bids[i].Bid > lastVolume)
        {
            buyPrice = marketDepth.Bids[i].Price.ToDecimal() + _tab.Security.PriceStep;
            lastVolume = Convert.ToInt32(marketDepth.Bids[i].Bid);
        }
    }

    // Перестановка ордера, если цена изменилась
    Position positionBuy = _tab.PositionsOpenAll.Find(pos => pos.Direction == Side.Buy);
    if (positionBuy != null && positionBuy.OpenOrders[0].Price != buyPrice)
    {
        _tab.ChangeOrderPrice(positionBuy.OpenOrders[0], buyPrice);
    }
}
```

### 5.2 Перестановка ордера по стакану (из `ChangePriceBotExtStopMarket`)

```csharp
private void _tab_MarketDepthUpdateEvent(MarketDepth marketDepth)
{
    if (marketDepth.Asks == null || marketDepth.Asks.Count == 0 ||
        marketDepth.Bids == null || marketDepth.Bids.Count == 0)
        return;

    if (_lastCheckTime.AddSeconds(_secondsOnReplaceOrderPrice.ValueInt) > DateTime.Now)
        return;
    _lastCheckTime = DateTime.Now;

    Position pos = _tab.PositionsOpenAll.Find(p => p.State == PositionStateType.Opening);
    if (pos == null) return;

    decimal newPrice;
    if (SideParam.ValueString == "Buy")
    {
        decimal priceStep = _tab.Security.PriceStep;
        decimal priceNow = _tab.PriceBestAsk + priceStep;
        decimal priceMin = _tab.PriceBestAsk + priceStep * 10;
        newPrice = priceNow - (priceMin * AntiSlippagePercent.ValueDecimal / 100);
    }
    else
    {
        // Аналогично для Sell
    }

    newPrice = Math.Round(newPrice, _tab.Security.Decimals);
    _tab.ChangeOrderPrice(pos.OpenOrders[0], newPrice);
}
```

### 5.3 Трейлинг по тикам (из `StopByTradeFeedSample`)

```csharp
private void _tab_NewTickEvent(Trade trade)
{
    Position myPos = _tab.PositionsOpenAll[0];
    if (myPos == null) return;

    decimal stopPrice, orderPrice;
    if (myPos.Direction == Side.Buy)
    {
        stopPrice = trade.Price - (trade.Price * (_trailStopPercent.ValueDecimal / 100));
        orderPrice = stopPrice - _slippage.ValueInt * _tab.Security.PriceStep;
    }
    else
    {
        stopPrice = trade.Price + (trade.Price * (_trailStopPercent.ValueDecimal / 100));
        orderPrice = stopPrice + _slippage.ValueInt * _tab.Security.PriceStep;
    }

    _tab.CloseAtTrailingStop(myPos, stopPrice, orderPrice);
}
```

### 5.4 Работа со стаканом на скринере (из `MarketDepthScreener`)

```csharp
private void TradeLogicEntry(BotTabSimple tab)
{
    // Проверка стакана
    MarketDepth md = tab.MarketDepth.GetCopy();
    if (md == null || md.Bids == null || md.Bids.Count < 2) return;

    decimal bestBidVolume = md.Bids[0].Bid.ToDecimal();
    decimal allVolume = 0;
    for (int i = 0; i < md.Bids.Count; i++)
        allVolume += md.Bids[i].Bid.ToDecimal();

    decimal ratio = bestBidVolume / allVolume * 100;
    if (ratio < _bestBidMinRatioToAll.ValueDecimal) return; // не "плита"

    // Вход
    tab.BuyAtLimit(GetVolume(tab), md.Bids[0].Price.ToDecimal() + tab.Security.PriceStep);
}
```

---

## 6. Частые ошибки при HFT

| Ошибка | Почему плохо | Как правильно |
|--------|-------------|---------------|
| **Нет throttling в реальном режиме** | Флуд биржи, бан API | `if (_lastCheckTime.AddSeconds(1) > DateTime.Now) return;` |
| **Не использовать `GetCopy()` для стакана** | Данные изменятся во время чтения из другого потока | `MarketDepth md = marketDepth.GetCopy();` |
| **Забыть `DisableManualSupport()`** | Пользователь может случайно закрыть позицию/ордер | `_tab.ManualPositionSupport.DisableManualSupport();` |
| **Не оборачивать в `try-catch`** | Одна ошибка остановит поток событий | Всегда `try-catch` в обработчиках |
| **Долгие операции в обработчике** | Пропуск событий, лаги | Вынести тяжёлую логику в `Thread`/`Task` |
| **Использовать `CandleFinishedEvent` вместо тиков** | Потеря скорости для HFT | Для HFT использовать `MarketDepthUpdateEvent`/`NewTickEvent` |
| **Не проверять `marketDepth.Asks/Bids` на null** | `NullReferenceException` | `if (marketDepth.Asks == null || marketDepth.Bids == null) return;` |
| **Работать с позициями без проверки `State`** | Перестановка цены закрытой позиции | `if (pos.State != PositionStateType.Opening) return;` |
| **Не использовать `lock` для общих коллекций** | Гонка данных между потоками | `lock (_positionsToClose) { ... }` |
| **Забыть останавливать `Thread` при удалении робота** | Утечка потока | `DeleteEvent += Robot_DeleteEvent; _isDisposed = true;` |

---

## 7. Быстрый справочник: от задачи к роботу

| Запрос пользователя | Событие | Робот-образец | Ключевой паттерн |
|---------------------|---------|---------------|------------------|
| «Торговля по стакану» | `MarketDepthUpdateEvent` | `HighFrequencyTrader` | Throttling 1 сек, поиск max объёма |
| «Детекция плиты в стакане» | `MarketDepthUpdateEvent` | `PlateDetectorScreener` | Соотношение best bid / все бид |
| «Детекция плиты на скринере» | Фоновый `Thread` | `MarketDepthScreener` | `Thread.Sleep(100)`, `GetCopy()` |
| «Трейлинг-стоп по тикам» | `NewTickEvent` | `StopByTradeFeedSample` | `trade.Price * (percent / 100)` |
| «Перестановка цены ордера» | `MarketDepthUpdateEvent` | `ChangePriceBotExtStopMarket` | `ChangeOrderPrice`, `DisableManualSupport` |
| «Детекция пампа» | `NewTickEvent` | `PumpDetectorScreener` | Изменение цены за N секунд |
| «Маркет-мейкинг в стакане» | Фоновый `Thread` | `Fisher` | `Thread.Sleep`, отмена/закрытие/открытие |
| «Выход по стакану с Task» | `MarketDepthUpdateEvent` | `HighFrequencyTrader` | `Task` + очередь `_positionsToClose` |

---

**Изучать:**
- `HighFrequencyTrader.cs` — классический HFT на стакане с throttling
- `Fisher.cs` — фоновый Thread для маркет-мейкинга
- `MarketDepthScreener.cs` — скринер + стакан + Thread
- `StopByTradeFeedSample.cs` — трейлинг-стоп по ленте сделок
- `ChangePriceBotExtStopMarket.cs` — перестановка цены ордера
- `PlateDetectorScreener.cs` — событийный анализ стакана на скринере
