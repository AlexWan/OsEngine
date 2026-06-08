# CONTEXT — Управление позициями и риск-менеджмент

> **Область**: Открытие/закрытие позиций, стоп-лоссы, тейк-профиты, трейлинг-стопы, расчёт объёма, ограничения по риску.
> **Ключевое правило**: Перед любым действием с позицией проверяй `position.State == PositionStateType.Open`. Иначе робот будет ставить стопы на ещё не открытые позиции или пытаться закрыть уже закрывающуюся.

---

## 1.1 Состояния позиции (`PositionStateType`)

Перед вызовом `CloseAtStop`, `CloseAtProfit`, модификацией ордера или добавлением к позиции **обязательно проверяй состояние**.

| Состояние | Что означает | Можно ли ставить стоп/тейк | Можно ли закрывать |
|-----------|--------------|---------------------------|-------------------|
| `None` | Позиция только создана | Нет | Нет |
| `Opening` | Заявка на вход выставлена, ждём исполнения | **Нет** | Нет |
| `Open` | Позиция полностью открыта | **Да** | Да |
| `Done` | Позиция закрыта | Нет | Нет |
| `OpeningFail` | Ошибка при открытии | Нет | Нет |
| `Closing` | Заявка на закрытие выставлена | Нет | **Нет!** |
| `ClosingFail` | Ошибка при закрытии | Нет | Можно повторить |
| `ClosingSurplus` | Избыточное закрытие | Нет | Нет |
| `Deleted` | Позиция удалена | Нет | Нет |

**Золотая проверка:**

```csharp
if (position.State != PositionStateType.Open)
{
    return; // или continue в цикле
}
```

**Защита от повторного закрытия:**

```csharp
if (position.State == PositionStateType.Closing)
{
    return; // Уже выставлена заявка на закрытие
}
```

---

## 1.2 События позиций (`BotTabSimple`)

Подписывайтесь на события в конструкторе. Основное — `PositionOpeningSuccesEvent` для моментальной установки стопа/тейка.

| Событие | Сигнатура | Типичное использование |
|---------|-----------|------------------------|
| `PositionOpeningSuccesEvent` | `Action<Position>` | Поставить SL/TP сразу при открытии |
| `PositionOpenFailEvent` | `Action<Position>` | Залогировать, повторить вход |
| `PositionClosingSuccesEvent` | `Action<Position>` | Обновить статистику, сбросить флаги |
| `PositionClosingFailEvent` | `Action<Position>` | Принудительное `CloseAtMarket` |
| `MyTradeEvent` | `Action<MyTrade>` | Тиковый контроль исполнения |

**Пример подписки:**

```csharp
public MyRobot(string name, StartProgram startProgram) : base(name, startProgram)
{
    TabCreate(BotTabType.Simple);
    _tab = TabsSimple[0];

    _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
    _tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;
    _tab.PositionClosingFailEvent += _tab_PositionClosingFailEvent;
}

private void _tab_PositionOpeningSuccesEvent(Position position)
{
    // Сразу ставим стоп и тейк
    if (position.Direction == Side.Buy)
    {
        decimal stopPrice = position.EntryPrice - position.EntryPrice * 0.01m;
        _tab.CloseAtStopMarket(position, stopPrice, "StopLoss");

        decimal profitPrice = position.EntryPrice + position.EntryPrice * 0.02m;
        _tab.CloseAtProfitMarket(position, profitPrice, "TakeProfit");
    }
}

private void _tab_PositionClosingFailEvent(Position position)
{
    if (position.CloseActive)
        return;
    _tab.CloseAtMarket(position, position.OpenVolume);
}
```

---

## 1.3 Методы входа (Opening)

Все методы — на `BotTabSimple`. Для `BotTabScreener` торговля идёт через `_screener.Tabs[i].BuyAtMarket(...)`.

| Метод | Описание | Когда использовать |
|-------|----------|-------------------|
| `BuyAtMarket(decimal volume)` | Рыночный лонг | Точный сигнал, нужна гарантия входа |
| `BuyAtMarket(decimal volume, string signalType)` | Рыночный лонг с комментарием | Для журнала и фильтрации |
| `BuyAtLimit(decimal price, decimal volume)` | Лимитный лонг | Лучшая цена, но риск неисполнения |
| `BuyAtStop(decimal volume, decimal priceLimit, decimal priceRedLine, ...)` | Стоп-ордер на вход | Пробой уровня |
| `BuyAtStopMarket(decimal volume, decimal priceRedLine, ...)` | Стоп-маркет на вход | Пробой с гарантией входа |
| `BuyAtStopCancel()` | Отмена всех отложенных Buy-ордеров | Перед сменой направления |
| `BuyAtIceberg(...)` | Айсберг-заявка | Большой объём без проскальзывания |

Для шорта аналогично: `SellAtMarket`, `SellAtLimit`, `SellAtStop`, `SellAtStopCancel`.

> **Важно**: `BuyAtStopCancel()` / `SellAtStopCancel()` отменяют **все** отложенные ордера на вход. Вызывайте их перед перевыставлением, иначе старый и новый ордер будут конкурировать.

---

## 1.4 Методы выхода (Closing)

| Метод | Описание | Параметры |
|-------|----------|-----------|
| `CloseAtMarket(Position, decimal volume)` | Закрыть по рынку | позиция, объём |
| `CloseAtMarket(Position, decimal volume, string signalType)` | Закрыть по рынку с комментарием | + комментарий |
| `CloseAtLimit(Position, decimal price, decimal volume)` | Лимитное закрытие | позиция, цена, объём |
| `CloseAtLimitUnsafe(Position, decimal price, decimal volume)` | Лимитное закрытие без защиты | для дробного закрытия |
| `CloseAtStop(Position, decimal activationPrice, decimal orderPrice)` | Стоп-ордер | цена активации, цена ордера |
| `CloseAtStop(Position, ..., string signalType)` | Стоп-ордер с комментарием | |
| `CloseAtStopMarket(Position, decimal activationPrice)` | Стоп-маркет | только цена активации |
| `CloseAtTrailingStop(Position, decimal activationPrice, decimal orderPrice)` | Трейлинг-стоп | двигается только в плюс |
| `CloseAtTrailingStopMarket(Position, decimal activationPrice)` | Трейлинг-маркет | |
| `CloseAtProfit(Position, decimal activationPrice, decimal orderPrice)` | Тейк-профит лимиткой | |
| `CloseAtProfitMarket(Position, decimal activationPrice)` | Тейк-маркет | |
| `CloseAllAtMarket()` | Закрыть ВСЕ позиции по рынку | без параметров |
| `CloseAllAtMarket(string signalType)` | Закрыть все с комментарием | |
| `CloseAllOrderToPosition(Position)` | Отменить все ордера на позицию | полезно при перевыставлении |

> **Критически важно**: `CloseAtTrailingStop` **двигает стоп только в сторону прибыли**. Если передать цену хуже текущего стопа — метод молча проигнорирует вызов.

---

## 1.5 Стоп-лосс: паттерны и примеры

### Паттерн A — Фиксированный % от цены входа

```csharp
// Из PriceChannelCounterTrend / HighFrequencyTrader
if (position.Direction == Side.Buy)
{
    decimal stopPrice = position.EntryPrice - position.EntryPrice * (_stopPercent.ValueDecimal / 100);
    _tab.CloseAtStopMarket(position, stopPrice, "StopLoss");
}
else // Sell
{
    decimal stopPrice = position.EntryPrice + position.EntryPrice * (_stopPercent.ValueDecimal / 100);
    _tab.CloseAtStopMarket(position, stopPrice, "StopLoss");
}
```

### Паттерн B — По экстремуму предыдущей свечи

```csharp
// Из PriceChannelBreak
if (position.Direction == Side.Buy)
{
    decimal lowCandle = _tab.CandlesAll[_tab.CandlesAll.Count - 2].Low;
    decimal orderPrice = lowCandle - _slippage.ValueInt * _tab.Security.PriceStep;
    _tab.CloseAtStop(position, lowCandle, orderPrice);
}
else
{
    decimal highCandle = _tab.CandlesAll[_tab.CandlesAll.Count - 2].High;
    decimal orderPrice = highCandle + _slippage.ValueInt * _tab.Security.PriceStep;
    _tab.CloseAtStop(position, highCandle, orderPrice);
}
```

### Паттерн C — По границе индикатора

```csharp
// Из BreakLinearRegressionChannel
if (position.Direction == Side.Buy)
{
    decimal extPrice = _linearRegression.DataSeries[2].Values[...]; // нижняя граница
    decimal slip = _slippage.ValueDecimal * extPrice / 100;
    _tab.CloseAtStop(position, extPrice, extPrice - slip);
}
else
{
    decimal extPrice = _linearRegression.DataSeries[0].Values[...]; // верхняя граница
    decimal slip = _slippage.ValueDecimal * extPrice / 100;
    _tab.CloseAtStop(position, extPrice, extPrice + slip);
}
```

### Паттерн D — Фиксированный отступ в пунктах (`PriceStep`)

```csharp
// Из HighFrequencyTrader
if (position.Direction == Side.Buy)
{
    decimal stop = position.EntryPrice - _stop.ValueInt * _tab.Security.PriceStep;
    _tab.CloseAtStop(position, stop, stop); // activation == order price
}
```

---

## 1.6 Тейк-профит: паттерны и примеры

### Паттерн A — Фиксированный % от цены входа

```csharp
// Из PriceChannelCounterTrend — два разных тейка для двух сигналов
if (position.SignalTypeOpen == "First")
{
    decimal price = position.EntryPrice + position.EntryPrice * (_profitOrderOnePercent.ValueDecimal / 100);
    _tab.CloseAtLimit(position, price, position.OpenVolume);
}
if (position.SignalTypeOpen == "Second")
{
    decimal price = position.EntryPrice + position.EntryPrice * (_profitOrderTwoPercent.ValueDecimal / 100);
    _tab.CloseAtLimit(position, price, position.OpenVolume);
}
```

### Паттерн B — По ширине канала

```csharp
// Из PriceChannelBreak
if (position.Direction == Side.Buy)
{
    decimal tpPrice = _lastPrice + (_lastPcUp - _lastPcDown); // ширина канала
    decimal orderPrice = tpPrice - _slippage.ValueInt * _tab.Security.PriceStep;
    _tab.CloseAtProfit(position, tpPrice, orderPrice);
}
```

### Паттерн C — Фиксированный в пунктах

```csharp
// Аналогично HighFrequencyTrader SL
if (position.Direction == Side.Buy)
{
    decimal profit = position.EntryPrice + _profit.ValueInt * _tab.Security.PriceStep;
    _tab.CloseAtProfit(position, profit, profit);
}
```

---

## 1.7 Трейлинг-стоп: 5 реальных паттернов

### Паттерн 1 — По индикатору (Bollinger Bands)

```csharp
// Из BollingerTrailing — вызывается из CandleFinishedEvent И PositionOpeningSuccesEvent
private void ReloadTrailingPosition(Position position)
{
    List<Position> openPositions = _tab.PositionsOpenAll;
    for (int i = 0; openPositions != null && i < openPositions.Count; i++)
    {
        if (openPositions[i].Direction == Side.Buy)
        {
            decimal valueDown = _bollinger.DataSeries[1].Values[_bollinger.DataSeries[1].Values.Count - 1];
            _tab.CloseAtTrailingStop(openPositions[i], valueDown,
                valueDown - _slippage.ValueInt * _tab.Security.PriceStep);
        }
        else
        {
            decimal valueUp = _bollinger.DataSeries[0].Values[_bollinger.DataSeries[0].Values.Count - 1];
            _tab.CloseAtTrailingStop(openPositions[i], valueUp,
                valueUp + _slippage.ValueInt * _tab.Security.PriceStep);
        }
    }
}
```

> **Особенность**: Трейлинг вызывается и из `CandleFinishedEvent`, и из `PositionOpeningSuccesEvent` — чтобы стоп поставился сразу при открытии.

### Паттерн 2 — Процентный трейлинг

```csharp
// Из MacdTrail
private void LogicClosePosition(List<Candle> candles, Position position)
{
    if (position.Direction == Side.Buy)
    {
        decimal stop = _lastClose - _lastClose * _trailStop.ValueDecimal / 100;
        _tab.CloseAtTrailingStop(position, stop, stop);
    }
    else
    {
        decimal stop = _lastClose + _lastClose * _trailStop.ValueDecimal / 100;
        _tab.CloseAtTrailingStop(position, stop, stop);
    }
}
```

### Паттерн 3 — По индикатору + проскальзывание (Envelops)

```csharp
// Из EnvelopTrend — вызывается ТОЛЬКО из PositionOpeningSuccesEvent
private void _tab_PositionOpeningSuccesEvent(Position position)
{
    _tab.BuyAtStopCancel();
    _tab.SellAtStopCancel();

    if (position.Direction == Side.Buy)
    {
        decimal activation = _envelop.DataSeries[0].Last -
            _envelop.DataSeries[0].Last * (_trailStop.ValueDecimal / 100);
        decimal orderPrice = activation - _tab.Security.PriceStep * _slippage.ValueInt;
        _tab.CloseAtTrailingStop(position, activation, orderPrice);
    }
    // аналогично для Sell...
}
```

### Паттерн 4 — Тиковый трейлинг

```csharp
// Из StopByTradeFeedSample — обновляется на КАЖДОМ тике
private void _tab_NewTickEvent(Trade trade)
{
    Position myPos = _tab.PositionsOpenAll[0];
    if (myPos.State != PositionStateType.Open)
        return;

    decimal stopPrice = 0;
    decimal orderPrice = 0;

    if (myPos.Direction == Side.Buy)
    {
        stopPrice = trade.Price - trade.Price * (_trailStopPercent.ValueDecimal / 100);
        orderPrice = stopPrice - _slippage.ValueInt * _tab.Security.PriceStep;
    }
    else
    {
        stopPrice = trade.Price + trade.Price * (_trailStopPercent.ValueDecimal / 100);
        orderPrice = stopPrice + _slippage.ValueInt * _tab.Security.PriceStep;
    }

    _tab.CloseAtTrailingStop(myPos, stopPrice, orderPrice);
}
```

> **Важно**: Тиковый трейлинг даёт максимально близкий стоп, но создаёт высокую нагрузку. Используйте только если это критично для стратегии.

### Паттерн 5 — `CloseAtTrailingStopMarket` по PriceChannel

```csharp
// Из TwoEntrySample
private void LogicClosePriceChannel(Position position)
{
    if (position.State != PositionStateType.Open)
        return;

    decimal downChannel = _pc.DataSeries[1].Values[_pc.DataSeries[1].Values.Count - 1];
    _tab.CloseAtTrailingStopMarket(position, downChannel);
}
```

> `CloseAtTrailingStopMarket` — при активации закрывает по рынку, а не лимиткой. Полезно когда важнее гарантия выхода, чем цена.

---

## 1.8 Риск-менеджмент

### 1.8.1 Ограничение количества позиций

```csharp
// Глобальный лимит на все позиции
if (_tab.PositionsOpenAll.Count >= _maxPositionsCount.ValueInt)
{
    return;
}

// Лимит только на лонги (в скринере)
List<Position> longPositions = _tabScreener.PositionsOpenAll.FindAll(p => p.Direction == Side.Buy);
if (longPositions.Count >= _longMaxPositions.ValueInt)
{
    return;
}
```

### 1.8.2 Ограничение по времени в позиции

```csharp
// Закрытие по количеству свечей (MonitorVolume)
int candlesInPosition = Convert.ToInt32(
    (tab.TimeServerCurrent - position.TimeOpen).TotalMinutes
    / tab.Connector.TimeFrameTimeSpan.TotalMinutes);

if (candlesInPosition > _maxCandlesInPositions.ValueInt)
{
    tab.CloseAtMarket(position, position.OpenVolume);
}

// Закрытие по секундам (MonitorImpulse)
int secondsInPosition = Convert.ToInt32(
    (tab.TimeServerCurrent - position.TimeOpen).TotalSeconds);

if (secondsInPosition > _maxSecondsInPositions.ValueInt)
{
    tab.CloseAtMarket(position, position.OpenVolume);
}
```

### 1.8.3 Диверсификация по времени (cooldown)

**Паттерн A — Пауза между сделками одной пары**

```csharp
// Из FuturesStart1Bollinger
DateTime lastPairTradeTime = GetLastLogicEntryTime(security.Name);
if (lastPairTradeTime.AddMinutes(1) > DateTime.Now)
{
    return; // Не прошла минута с последней сделки
}
SetLastLogicEntryTime(security.Name, DateTime.Now);
```

**Паттерн B — Пауза между итерациями DCA**

```csharp
// Из DcaTimeBot
if (_lastScenarioOrderTime.Add(_interval) < DateTime.Now)
{
    // Прошло достаточно времени → следующий ордер
    _tab.BuyAtMarket(volume);
    _lastScenarioOrderTime = DateTime.Now;
}
```

**Паттерн C — Фиксированное время удержания позиции**

```csharp
// Из FakeOutExample
if (position.TimeOpen.AddMinutes(_minutsForExit.ValueInt) <= candles[candles.Count - 1].TimeStart)
{
    _tab.CloseAtMarket(position, position.OpenVolume);
}
```

### 1.8.4 Расчёт объёма (`GetVolume`)

```csharp
private decimal GetVolume(BotTabSimple tab)
{
    if (_volumeType.ValueString == "Contracts")
    {
        return _volume.ValueDecimal;
    }
    else if (_volumeType.ValueString == "Contract currency")
    {
        decimal contractPrice = tab.PriceBestAsk;
        decimal vol = _volume.ValueDecimal / contractPrice;

        // Учёт лота для акций с лотом > 1
        IServerPermission perm = ServerMaster.GetServerPermission(tab.Connector.ServerType);
        if (perm != null && perm.IsUseLotToCalculateProfit && tab.Security.Lot > 1)
        {
            vol = _volume.ValueDecimal / (contractPrice * tab.Security.Lot);
        }
        return Math.Round(vol, tab.Security.DecimalsVolume);
    }
    else if (_volumeType.ValueString == "Deposit percent")
    {
        Portfolio portfolio = tab.Portfolio;
        if (portfolio == null) return 0;

        decimal assetValue = _tradeAssetInPortfolio.ValueString == "Prime"
            ? portfolio.ValueCurrent
            : portfolio.GetPositionOnBoard().Find(p => p.SecurityNameCode == _tradeAssetInPortfolio.ValueString)?.ValueCurrent ?? 0;

        if (assetValue == 0) return 0;

        decimal moneyOnPosition = assetValue * (_volume.ValueDecimal / 100);
        decimal qty = moneyOnPosition / tab.PriceBestAsk / tab.Security.Lot;

        // Специальный расчёт для фьючерсов Мосбиржи
        if (tab.Security.UsePriceStepCostToCalculateVolume
            && tab.Security.PriceStep != tab.Security.PriceStepCost
            && tab.PriceBestAsk != 0 && tab.Security.PriceStep != 0)
        {
            qty = moneyOnPosition / (tab.PriceBestAsk / tab.Security.PriceStep * tab.Security.PriceStepCost);
        }

        return Math.Round(qty, tab.Security.DecimalsVolume);
    }
    return 0;
}
```

### 1.8.5 Отключение ручного управления

Если робот сам управляет стопами и тейками через код, отключите встроенную панель ручного управления:

```csharp
_tab.ManualPositionSupport.DisableManualSupport();
```

> Без этого пользователь может случайно поставить стоп через UI, который будет конфликтовать со стопами робота.

### 1.8.6 Дробное закрытие позиции

```csharp
// Из UnsafeLimitsClosingSample — закрываем 50% на первом тейке, остальное на втором
if (position.CloseActive == false)
{
    int executedCount = 0;
    for (int i = 0; position.CloseOrders != null && i < position.CloseOrders.Count; i++)
    {
        if (position.CloseOrders[i].State == OrderStateType.Done)
            executedCount++;
    }

    if (executedCount == 0)
    {
        // Первая половина
        _tab.CloseAtLimitUnsafe(position, firstOrderPrice, position.OpenVolume / 2);
        // Вторая половина
        _tab.CloseAtLimitUnsafe(position, secondOrderPrice, position.OpenVolume / 2);
    }
    else
    {
        // Первая уже исполнилась → остаток по второй цене
        _tab.CloseAtLimitUnsafe(position, secondOrderPrice, position.OpenVolume);
    }
}
```

---

## 1.9 Каталог роботов для заимствования (по управлению позициями)

| Робот | Путь | Чему учит |
|-------|------|-----------|
| `BollingerTrailing` | `OnScriptIndicators/` | Трейлинг по Bollinger + вызов из `PositionOpeningSuccesEvent` |
| `MacdTrail` | `OnScriptIndicators/` | Процентный трейлинг-стоп |
| `EnvelopTrend` | `Trend/` | Трейлинг по индикатору + `PriceStep * slippage` |
| `StopByTradeFeedSample` | `TechSamples/` | Тиковый трейлинг на `NewTickEvent` |
| `PriceChannelBreak` | `OnScriptIndicators/` | SL по свече + TP по ширине канала |
| `HighFrequencyTrader` | `High Frequency/` | SL/TP в пунктах `PriceStep`, `PositionClosingFailEvent` |
| `PriceChannelCounterTrend` | `PositionsMicromanagement/` | Два разных TP + % SL + `DisableManualSupport` |
| `UnsafeLimitsClosingSample` | `PositionsMicromanagement/` | Дробное закрытие через `CloseAtLimitUnsafe` |
| `TwoEntrySample` | `PositionsMicromanagement/` | `CloseAtTrailingStopMarket` |
| `MonitorVolume` | `Monitors/` | Лимиты позиций + время в позиции (свечи) |
| `MonitorImpulse` | `Monitors/` | Лимиты позиций + время в позиции (секунды) |
| `FakeOutExample` | `TechSamples/` | Закрытие по времени (`TimeOpen.AddMinutes`) |
| `DcaTimeBot` | `Helpers/` | Интервальный вход (`Add(interval)`) |

---

## 1.10 Типичные ошибки при управлении позициями

| № | Ошибка | Причина | Решение |
|---|--------|---------|---------|
| 1 | `CloseAtStop` без проверки `State == Open` | Стоп выставляется на ещё открывающуюся позицию → игнор или ошибка | Проверять `position.State == PositionStateType.Open` |
| 2 | Трейлинг-стоп "не двигается" | Передали цену хуже текущего стопа | `CloseAtTrailingStop` двигает только в плюс |
| 3 | Двойной стоп/тейк | `PositionOpeningSuccesEvent` сработал, а затем логика в `CandleFinishedEvent` поставила второй | Использовать флаг `position.StopOrderPrice == 0` или отдельный флаг |
| 4 | `PositionClosingFailEvent` не обработан | Позиция не закрылась, робот "завис" | Вешать обработчик с `CloseAtMarket` |
| 5 | Забыли `slippage` при расчёте цены ордера | Лимитка/стоп не исполняется | Всегда отнимать/прибавлять `PriceStep * slippage` |
| 6 | `GetVolume` возвращает 0 | `Portfolio == null` или `PriceBestAsk == 0` | Проверять `volume > 0` перед выставлением |
| 7 | Повторный вход на той же свече | Нет флага `_lastCandleOpenPos` или проверки `TimeOpen` | Использовать флаг или проверять время последней позиции |
| 8 | `BuyAtStopCancel()` забыли вызвать | Старый отложенный ордер остался + новый → два входа | Отменять перед перевыставлением |
| 9 | `CloseAllAtMarket()` вместо `CloseAtMarket(pos)` | Закрыли все позиции, хотя нужна была только одна | Точечное закрытие по конкретной позиции |
| 10 | Нет проверки `position.State == Closing` | Пытаемся закрыть уже закрывающуюся позицию → дублирующий ордер | Проверять перед вызовом `CloseAt*` |

---

## 1.11 Шпаргалка

**Поставить стоп + тейк при открытии:**
```csharp
_tab.PositionOpeningSuccesEvent += (pos) => {
    if (pos.Direction == Side.Buy) {
        _tab.CloseAtStopMarket(pos, pos.EntryPrice * 0.99m);
        _tab.CloseAtProfitMarket(pos, pos.EntryPrice * 1.02m);
    }
};
```

**Проверить состояние позиции:**
```csharp
if (pos.State != PositionStateType.Open) return;
```

**Закрыть всё по рынку:**
```csharp
_tab.CloseAllAtMarket("EmergencyExit");
```

**Отменить отложенные ордера на вход:**
```csharp
_tab.BuyAtStopCancel();
_tab.SellAtStopCancel();
```

**Рассчитать время в позиции (свечи):**
```csharp
int candles = (int)((tab.TimeServerCurrent - pos.TimeOpen).TotalMinutes / tab.Connector.TimeFrameTimeSpan.TotalMinutes);
```

**Cool-down между сделками:**
```csharp
if (_lastTradeTime.AddMinutes(5) > DateTime.Now) return;
_lastTradeTime = DateTime.Now;
```

**Отключить ручное управление:**
```csharp
_tab.ManualPositionSupport.DisableManualSupport();
```

---

> **Связанные файлы контекста:**
> - `CONTEXT.md` — базовая архитектура, выбор типа таба
> - `CONTEXT_ROBOTS.md` — каталог готовых роботов (~100 примеров)
> - `CONTEXT_INDICATORS.md` — создание индикаторов
> - `CONTEXT_HIGH_FREQUENCY.md` — тиковая торговля и трейлинг
> - `CONTEXT_GRIDS.md` — сеточные стратегии (отдельная логика стопов)
