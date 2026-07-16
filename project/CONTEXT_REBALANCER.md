# CONTEXT_REBALANCER — Роботы-ребалансировщики в OsEngine

> Постоянный контекст для ИИ-агентов, работающих с роботами-ребалансировщиками в OsEngine. Здесь объяснена архитектура, общие паттерны и правила создания новых ребалансеров.

---

## Введение

Ребалансировщик — это робот, который периодически перераспределяет капитал между несколькими инструментами по заданному расписанию и правилам. В OsEngine ребалансеры обычно работают через `BotTabScreener` (набор акций или других инструментов) и один/несколько `BotTabSimple` (защитные активы: LQDT, золото, облигации).

Основная цель — автоматически переключаться между агрессивной частью портфеля (акции с сигналом) и защитной частью (денежный эквивалент), чтобы снижать просадку в неблагоприятные периоды.

---

## 1. Примеры роботов

### 1.1. RebalancerByDividendQuality

- **Файл:** `OsEngine/Robots/Rebalancers/RebalancerByDividendQuality.cs`
- **Направление:** только Long.
- **Агрессивная часть:** акции из скринера с ближайшим дивидендом в окне `Lookahead days` и доходностью выше `Min dividend %`.
- **Защитная часть:** LQDT.
- **Расписание:** еженедельно в выбранный день недели (`Monday`/`Tuesday`/`Wednesday`) в заданное время.
- **Особенности:**
  - если кандидатов нет — капитал переходит в LQDT;
  - LQDT не перезакупается каждую неделю, только при смене года, чтобы фиксировать прибыль для налогового учёта;
  - в реальном режиме поддерживается автообновление базы дивидендов `Wiki/Dividends` утром в день ребалансировки;
  - параметры разбиты на вкладки **Base** и **Update**.

### 1.2. RebalancerByMomentum

- **Файл:** `OsEngine/Robots/Rebalancers/RebalancerByMomentum.cs`
- **Направление:** только Long.
- **Агрессивная часть:** акции из скринера с лучшим моментумом.
- **Защитная часть:** LQDTMOEX и золото.
- **Расписание:** еженедельно/ежедневно/ежемесячно в заданный день недели и время.
- **Особенности:**
  - отбор топ-N акций по моментуму;
  - фильтр по минимальному моментуму и проценту роста;
  - отдельные параметры для акций, золота и LQDT;
  - использование индикаторов Keltner Channel и Momentum на скринере;
  - поддержка айсберг-заявок для крупных входов.

### 1.3 RebalancerClassicDividend

- **Файл:** `OsEngine/Robots/Rebalancers/RebalancerClassicDividend.cs`
- **Направление:** только Long.
- **Агрессивная часть:** дивидендные акции из скринера.
- **Защитная часть:** золото.
- **Расписание:** еженедельно или ежемесячно в выбранный день недели и время.
- **Особенности:**
  - два независимых режима: **классический** (фиксированное соотношение акции/золото) и **дивидендный** (весь депозит в акции с ближайшими дивидендами);
  - дивидендный режим имеет приоритет: если в окне поиска есть акции с дивидендами — робот переходит в него;
  - классический режим использует фильтр по SMA (`Off` / `OnBuyUpperSma` / `OnBuyBelowSma`) с настраиваемым периодом;
  - если в классическом режиме ни одна акция не прошла фильтр — весь депозит идёт в золото;
  - в тестере при отсутствии дивидендных данных используется fallback-набор тикеров: **SBER / SBERP / GAZP / LKOH / VTBR**;
  - два режима ребалансировки:
    - `OnCloseAllAtRebalance` — закрыть все позиции и открыть новые (для тестера/агрессивного ребаланса);
    - `OnRealRebalance` — умный ребаланс: закрыть только лишние позиции, открыть недостающие, привести объёмы существующих позиций через `BuyAtMarketToPosition` / `CloseAtMarket` (для реального трейдинга);
  - параметры разбиты на вкладки **Base**, **Classic**, **Dividend**, **Update**;
  - поддержка автообновления базы дивидендов `Wiki/Dividends` утром в день ребалансировки и кнопка ручного обновления.

---

## 2. Архитектура ребалансера

### 2.1. Типичная структура

```csharp
[Bot("MyRebalancer")]
public class MyRebalancer : BotPanel
{
    private BotTabScreener _tabScreenerStocks;  // набор инструментов
    private BotTabSimple _tabLqdt;              // защитный актив

    private StrategyParameterString _regime;
    private StrategyParameterString _rebalanceDayOfWeek;
    private StrategyParameterTimeOfDay _rebalanceTime;

    // ...
}
```

### 2.2. Основные компоненты

| Компонент | Назначение |
|-----------|-----------|
| `BotTabScreener` | Набор инструментов, по которым отбираются кандидаты. |
| `BotTabSimple` | Одиночный инструмент (LQDT, золото, облигации). |
| `StrategyParameterString` | Строковые параметры: режим, день недели, период. |
| `StrategyParameterTimeOfDay` | Время ребалансировки внутри дня. |
| `StrategyParameterInt` / `Decimal` | Числовые параметры: лимиты, пороги, lookback. |
| `StrategyParameterButton` | Кнопки для ручных действий (например, обновление данных). |

### 2.3. Группировка параметров по вкладкам

Вкладки в окне параметров создаются автоматически по полю `TabName` параметра. Параметры с одинаковым `TabName` попадают на одну вкладку. Если `TabName` не задан — используется первая вкладка (`Base`).

```csharp
_regime = CreateParameter("Regime", "On", new[] { "On", "Off" }, "Base");
_stockRebalanceOn = CreateParameter("Stock rebalance on", true, "Stock");
_goldRebalanceOn = CreateParameter("Gold rebalance on", true, "Gold");
```

### 2.4. Поддержка оптимизатора

Если ребалансер должен работать в оптимизаторе, нужно подписаться на событие `EndNextMinuteWithCandlesEvent` сервера оптимизатора. Пример см. в `RebalancerByDividendQuality`:

```csharp
private bool _optimizerEventSubscribed = false;

private void _tabScreenerStocks_CandleFinishedEvent(List<Candle> candles, BotTabSimple source)
{
    if (source.Connector.ServerType != ServerType.Optimizer)
        return;

    if (_optimizerEventSubscribed)
        return;

    _optimizerEventSubscribed = true;

    OptimizerServer server = source.Connector.MyServer as OptimizerServer;
    server.EndNextMinuteWithCandlesEvent += Server_EndNextMinuteWithCandlesEvent;
}

private void Server_EndNextMinuteWithCandlesEvent()
{
    _tabScreenerStocks_CandlesSyncFinishedEvent(_tabScreenerStocks.Tabs);
}
```

---

## 3. Паттерны ребалансировки

### 3.1. Триггер по времени

Проверяйте день недели и время по `TimeServer` и свече:

```csharp
private bool IsRebalanceTime(DateTime serverTime, Candle candle)
{
    if (candle == null)
        return false;

    if (candle.TimeStart.TimeOfDay.Hours != _rebalanceTime.Value.TimeSpan.Hours
        || candle.TimeStart.TimeOfDay.Minutes != _rebalanceTime.Value.TimeSpan.Minutes)
    {
        return false;
    }

    DayOfWeek targetDay = ParseDayOfWeek(_rebalanceDayOfWeek.ValueString);
    return serverTime.DayOfWeek == targetDay;
}
```

### 3.2. Отбор кандидатов

Пройдите по вкладкам скринера и отберите инструменты по вашему правилу:

```csharp
private List<BotTabSimple> GetCandidates()
{
    List<BotTabSimple> result = new List<BotTabSimple>();
    List<BotTabSimple> tabs = _tabScreenerStocks.Tabs;
    DateTime currentTime = TimeServer;

    for (int i = 0; tabs != null && i < tabs.Count; i++)
    {
        BotTabSimple tab = tabs[i];

        if (tab?.Security == null || string.IsNullOrWhiteSpace(tab.Security.Name))
            continue;

        // ваш фильтр
        if (IsCandidate(tab, currentTime))
        {
            result.Add(tab);
        }
    }

    return result;
}
```

### 3.3. Закрытие старых позиций

Перед открытием новых позиций закройте старые рыночными заявками:

```csharp
private void CloseAllStockPositions()
{
    List<BotTabSimple> tabs = _tabScreenerStocks.Tabs;

    for (int i = 0; tabs != null && i < tabs.Count; i++)
    {
        List<Position> positions = tabs[i].PositionsOpenAll;

        for (int i2 = 0; i2 < positions.Count; i2++)
        {
            tabs[i].CloseAtMarket(positions[i2], positions[i2].OpenVolume);
        }
    }
}
```

### 3.4. Распределение капитала

Капитал берётся из `Portfolio.ValueCurrent` (или `ValueBegin`, если текущий равен 0):

```csharp
private decimal GetCurrentCapital()
{
    decimal capital = _tabLqdt.Portfolio.ValueCurrent;

    if (capital == 0m)
        capital = _tabLqdt.Portfolio.ValueBegin;

    return capital;
}
```

Для равномерного распределения между кандидатами:

```csharp
decimal availableCapital = GetCurrentCapital() * _maxStocksDepositPercent.ValueDecimal / 100m;
decimal moneyOnOne = availableCapital / candidates.Count;
```

### 3.5. Переход в защитный актив

Если кандидатов нет — закрываем акции и переводим капитал в защитный актив:

```csharp
CloseAllStockPositions();

if (_tabLqdt.PositionsOpenAll.Count == 0)
{
    decimal capital = GetCurrentCapital();
    decimal availableCapital = capital * _maxLqdtDepositPercent.ValueDecimal / 100m;
    EntryInPosition(_tabLqdt, availableCapital);
}
```

### 3.6. Не перезакупать защитный актив каждую неделю

Чтобы не создавать лишних сделок и не портить налоговый учёт, защитный актив можно перезакупать только при смене года:

```csharp
private void TryResetLqdtByYear()
{
    List<Position> positions = _tabLqdt.PositionsOpenAll;

    if (positions.Count == 0)
        return;

    if (TimeServer.Year <= positions[0].TimeOpen.Year)
        return;

    _tabLqdt.CloseAtMarket(positions[0], positions[0].OpenVolume);
    EntryInPosition(_tabLqdt, GetCurrentCapital() * _maxLqdtDepositPercent.ValueDecimal / 100m);
}
```

### 3.7. Расчёт объёма по деньгам

```csharp
private decimal CalculateVolumeForMoney(BotTabSimple tab, decimal money)
{
    decimal price = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;

    if (price == 0)
        return 0m;

    decimal lot = tab.Security.Lot;
    if (lot == 0)
        lot = 1m;

    decimal volume = money / (price * lot);
    int decimals = Math.Max(0, tab.Security.DecimalsVolume);
    decimal multiplier = (decimal)Math.Pow(10, decimals);

    return Math.Floor(volume * multiplier) / multiplier;
}
```

---

## 4. Автообновление данных в реальном режиме

Если ребалансер зависит от внешних данных (например, дивидендов), имеет смысл обновлять базу автоматически в день ребалансировки:

- запускать проверку в заданное время (например, 08:00) в день ребалансировки;
- определять возраст базы по `Directory.GetLastWriteTime`;
- если база устарела — запускать `WikiMaster.UpdateDividendsBase()` через `Task.Run`;
- не блокировать основной поток и торговлю;
- если в момент ребалансировки обновление ещё идёт — откладывать ребалансировку до завершения (с таймаутом, например 10 минут);
- добавлять кнопку ручного запуска обновления с проверками (реальный режим, не запущено, папка существует).

---

## 5. Ограничения и особенности

1. **Только лонг.** Оба существующих ребалансера работают только в лонг; для шорта потребуется другая логика риск-менеджмента.
2. **Без плеча.** Расчёт капитала ведётся от текущей стоимости портфеля.
3. **Один раз в период.** Ребалансировка должна срабатывать не чаще одного раза за торговый день — храните `_lastRebalanceDate`.
4. **Свечной триггер.** Время проверяйте по `candle.TimeStart`, чтобы не запускать логику на каждом тике.
5. **Защитный актив ликвидный.** LQDT, LQDTMOEX, золото — инструменты с низкой волатильностью и высокой ликвидностью.
6. **Используйте `try-catch` в событиях.** Любое необработанное исключение в `CandleFinishedEvent` может сломать работу робота.
7. **Параметры на вкладках.** Группируйте параметры по смыслу через `TabName`, чтобы UI был удобным.

---

## 6. Чек-лист: создание робота-ребалансировщика

1. Создать класс, наследовать `BotPanel`, добавить `[Bot("Name")]`.
2. Добавить `BotTabScreener` для набора инструментов и `BotTabSimple` для защитных активов.
3. Добавить параметры:
   - `Regime` — On/Off;
   - `Rebalance day of week` / `Rebalance time` — расписание;
   - параметры отбора кандидатов;
   - лимиты депозита под каждую группу активов;
   - при необходимости — параметры защитного актива и автообновления данных.
4. Реализовать `IsRebalanceTime` с проверкой дня недели и времени.
5. Реализовать `GetCandidates` — отбор инструментов из скринера.
6. Реализовать закрытие всех открытых позиций перед ребалансировкой.
7. Реализовать распределение капитала между кандидатами или перевод в защитный актив.
8. Добавить защиту от повторной ребалансировки в тот же день (`_lastRebalanceDate`).
9. При необходимости добавить поддержку оптимизатора через `EndNextMinuteWithCandlesEvent`.
10. Собрать решение (`dotnet build OsEngine.sln`).
11. Проверить в тестере на исторических данных.

---

## 7. Полезные ссылки

- `CONTEXT_ROBOTS.md` — каталог готовых роботов.
- `CONTEXT_ROBOTS_ARCHITECTURE.md` — архитектура роботов в OsEngine.
- `CONTEXT_DIVIDENDS.md` — работа с дивидендами через `WikiMaster`.
- `CONTEXT_CODING_GUIDELINES.md` — стиль кода.
