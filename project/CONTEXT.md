# OsEngine — Контекст разработки

## 0. Карта контекста

Этот проект имеет несколько файлов контекста. Загружай нужный по задаче:

| Задача пользователя | Какой файл подключить |
|---------------------|----------------------|
| «Напиши робота / стратегию / скринер» | `CONTEXT.md` + `CONTEXT_ROBOTS.md` |
| «Напиши индикатор / переделай индикатор» | `CONTEXT.md` + `CONTEXT_INDICATORS.md` |
| «Индекс / спред / арбитраж против индекса» | `CONTEXT.md` + `CONTEXT_INDEX_AND_SPREAD.md` |
| «Парный арбитраж / фьючерсы / контанго» | `CONTEXT.md` + `CONTEXT_PAIRS_AND_FUTURES.md` |
| «Сеточная стратегия / маркет-мейкинг» | `CONTEXT.md` + `CONTEXT_GRIDS.md` |
| «HFT / торговля по стакану / лента сделок» | `CONTEXT.md` + `CONTEXT_HIGH_FREQUENCY.md` |
| «Стопы / тейки / трейлинг / риск-менеджмент» | `CONTEXT.md` + `CONTEXT_POSITIONS_AND_RISK.md` |
| «Какой таб выбрать / базовая архитектура» | Только `CONTEXT.md` |

**Базовая архитектура** (BotPanel, табы, параметры, события) — всегда в `CONTEXT.md`.
**Каталог готовых роботов** (~100 примеров) — в `CONTEXT_ROBOTS.md`.
**Индикаторы** (Aindicator, Samples, Scripts) — в `CONTEXT_INDICATORS.md`.
**Продвинутые темы** (парный арбитраж, ротация фьючерсов, контанго) — в `CONTEXT_PAIRS_AND_FUTURES.md`.
**Индексные и спредовые стратегии** (`BotTabIndex`, корреляция, коинтеграция) — в `CONTEXT_INDEX_AND_SPREAD.md`.
**Сеточные стратегии** (маркет-мейкинг, накопление позиции) — в `CONTEXT_GRIDS.md`.
**Высокочастотная торговля** (стакан, тики, перестановка ордеров) — в `CONTEXT_HIGH_FREQUENCY.md`.
**Управление позициями и риск** (стопы, тейки, трейлинг, объём, cooldown) — в `CONTEXT_POSITIONS_AND_RISK.md`.

---

## 1. Метаданные проекта

| Параметр | Значение |
|----------|----------|
| **Название** | OsEngine |
| **Платформа** | .NET 9 (`net9.0-windows`) |
| **Тип приложения** | WPF Desktop |
| **Назначение** | Платформа для алгоритмической торговли на бирже |
| **Язык** | C# |
| **Репозиторий** | https://github.com/AlexWan/OsEngine |
| **Сборка** | `dotnet build OsEngine.sln` (Visual Studio или Rider) |

---

## 2. Архитектура роботов

### 2.1 Базовый класс: `BotPanel`

Все роботы наследуются от `BotPanel` и помечаются атрибутом `[Bot("ИмяРобота")]`:

```csharp
[Bot("MyRobot")]
public class MyRobot : BotPanel
{
    public MyRobot(string name, StartProgram startProgram) : base(name, startProgram)
    {
        // Инициализация
    }
}
```

#### Важные замечания для разработки:

1. **Уникальное имя робота.** При создании нового робота всегда давайте ему уникальное имя:
   - Имя указывается в атрибуте `[Bot("ИмяРобота")]`
   - Имя должно быть уникальным во всём репозитории (проверьте через поиск по папке `Robots/`)
   - Не используйте имена существующих роботов (`EnvelopTrend`, `PinBarTrade`, `SmaScreener` и т.д.)
   - Рекомендуется использовать префикс или уникальное название стратегии

2. **Папка для новых роботов.** Перед созданием файла ОБЯЗАТЕЛЬНО спросить пользователя:
   - «В какую папку сохранить робота? По умолчанию: `Robots/MyBots/НазваниеРобота/`»
   - ЗАПРЕЩЕНО самостоятельно выбирать системные папки (`Robots/Trend/`, `Robots/CounterTrend/`, `Robots/Screeners/` и т.д.)
   - Ждать ответа пользователя. Если молчит — предложить `Robots/MyBots/НазваниеРобота/НазваниеРобота.cs`
   - namespace в файле должен соответствовать папке: `namespace OsEngine.Robots.MyBots`

3. **Конструктор робота.** Конструктор всегда должен иметь сигнатуру:
   ```csharp
   public RobotName(string name, StartProgram startProgram) : base(name, startProgram)
   ```
   - `name` — экземплярное имя робота
   - `startProgram` — тип запуска (`IsOsTrader`, `IsTester`, `IsOptimizer`)

4. **Создание табов только в конструкторе.** Все табы должны создаваться в конструкторе:
   - `TabCreate(BotTabType.Simple)`
   - Ссылки на табы сохраняются в поля класса: `_tab = TabsSimple[0]`
   - Подписка на события табов также выполняется в конструкторе

5. **Проверка типа запуска через `StartProgram`.**
   - `StartProgram.IsOsTrader` — реальная торговля (требует проверки на ошибки)
   - `StartProgram.IsTester` — тестирование на истории
   - `StartProgram.IsOptimizer` — оптимизация (избегайте тяжёлых вычислений, логирования)

6. **Жизненный цикл робота:**
   ```
   Конструктор → (создание табов, параметров, индикаторов)
        ↓
   Запуск → (подписка на события, начало торговли)
        ↓
   События → (CandleFinishedEvent, PositionOpeningSuccesEvent, и т.д.)
        ↓
   Остановка → (закрытие позиций, сохранение настроек)
        ↓
   Удаление → (очистка ресурсов)
   ```

7. **Потокобезопасность.** События табов могут приходить из разных потоков:
   - Не используйте общие переменные без блокировок (`lock`)
   - Избегайте длительных операций в обработчиках событий
   - Обновление UI только через `Dispatcher` или в главном потоке

8. **Обработка ошибок.** Всегда оборачивайте торговую логику в `try-catch`:
   ```csharp
   private void _tab_CandleFinishedEvent(List<Candle> candles)
   {
       try { /* Торговая логика */ }
       catch (Exception error)
       {
           SendNewLogMessage(error.ToString(), LogMessageType.Error);
       }
   }
   ```

**Полный шаблон нового робота:**

```csharp
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Logging;
using System.Collections.Generic;

[Bot("MyUniqueRobot")]
public class MyUniqueRobot : BotPanel
{
    // 1. Объявление полей
    private BotTabSimple _tab;
    private StrategyParameterString _regime;
    private StrategyParameterInt _period;
    
    // 2. Конструктор
    public MyUniqueRobot(string name, StartProgram startProgram) : base(name, startProgram)
    {
        _regime = CreateParameter("Regime", "On", new[] { "On", "Off" });
        _period = CreateParameter("Period", 14, 5, 100, 1);
        
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        
        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
    }
    
    // 3. Обработчик событий
    private void _tab_CandleFinishedEvent(List<Candle> candles)
    {
        try
        {
            if (_regime.ValueString == "Off") return;
            if (this.StartProgram == StartProgram.IsOptimizer) return;
            
            // Торговая логика
        }
        catch (Exception error)
        {
            SendNewLogMessage(error.ToString(), LogMessageType.Error);
        }
    }
}
```

**Структура файлов робота:**
```
Robots/MyBots/MyUniqueRobot/
├── MyUniqueRobot.cs          # Основной класс робота
├── MyUniqueRobotUi.xaml      # (опционально) WPF интерфейс настроек
└── MyUniqueRobotUi.xaml.cs   # (опционально) код интерфейса
```

---

### 2.2 Типы торговых табов (`BotTabType`)

`iBotTab` — это базовый интерфейс торгового источника. Это источник всех данных (свечи, тики, стакан, позиции) и единственный способ совершать торговые операции.

**Жизненный цикл таба в роботе:**
```csharp
TabCreate(BotTabType.Simple);
BotTabSimple _tab = TabsSimple[0];
_tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
```

**Типы табов:**

| Тип | Класс | Назначение |
|-----|-------|-----------|
| `Simple` | `BotTabSimple` | Один инструмент, основной тип для торговли |
| `Screener` | `BotTabScreener` | Несколько инструментов |
| `Index` | `BotTabIndex` | Пользовательский индекс |
| `Pair` | `BotTabPair` | Парный трейдинг |
| `Options` | `BotTabOptions` | Опционы |
| `Cluster` | `BotTabCluster` | Кластерный график |
| `Polygon` | `BotTabPolygon` | Валютный арбитраж |
| `News` | `BotTabNews` | Лента новостей |
| `SyntheticBond` | `BotTabSyntheticBond` | Синтетические облигации |

**Ключевые примеры выбора таба:**

| Запрос пользователя | Тип таба | Где смотреть пример |
|---------------------|----------|---------------------|
| Торговля по одному инструменту | `BotTabSimple` | `CONTEXT_ROBOTS.md` → 5.1, 5.2, 5.3 |
| Скринер по множеству инструментов | `BotTabScreener` | `CONTEXT_ROBOTS.md` → 5.4 |
| Парный арбитраж (2 инструмента) | `BotTabPair` | `CONTEXT_PAIRS_AND_FUTURES.md` → парный арбитраж |
| Кластерный анализ объёмов | `BotTabCluster` + `BotTabSimple` | `CONTEXT_ROBOTS.md` → 5.2.3 |
| Несколько таймфреймов | `BotTabSimple` ×2 | `CONTEXT_ROBOTS.md` → 5.1.4 |
| Ротация фьючерсов | `BotTabScreener` | `CONTEXT_PAIRS_AND_FUTURES.md` → фьючерсы |
| Контанго-арбитраж | `BotTabScreener` + `BotTabSimple` | `CONTEXT_PAIRS_AND_FUTURES.md` → контанго |

**Правила выбора типа таба:**
```
Вопрос 1: Сколько инструментов торгуем?
  ├─ Один → BotTabSimple
  ├─ Несколько (один индикатор на все) → BotTabScreener
  ├─ Два коррелирующих → BotTabPair
  └─ Корзина/Индекс → BotTabIndex

Вопрос 2: Какой тип данных нужен?
  ├─ Только свечи → BotTabSimple / BotTabScreener
  ├─ Свечи + объёмы по ценам → BotTabCluster
  ├─ Свечи + новости → BotTabNews + BotTabSimple
  ├─ Опционы → BotTabOptions
  └─ Валютные пары → BotTabPolygon
```

**Частые ошибки при выборе таба:**

| Ошибка | Почему неправильно | Как правильно |
|--------|-------------------|---------------|
| Скринер на `BotTabSimple` | Придётся создавать N табов вручную | `BotTabScreener` — один обработчик на все инструменты |
| Парный арбитраж на двух `BotTabSimple` | Нет авто-расчёта корреляции | `BotTabPair` — события `CorrelationChangeEvent` |
| Кластеры на `BotTabSimple` | Нет доступа к кластерным данным | `BotTabCluster` для анализа + `BotTabSimple` для торговли |
| Индикатор без привязки к табу | Индикатор не получит данные | Всегда создавать через `_tab.CreateCandleIndicator()` |

---

### 2.3 События таба и торговые операции

**Основные события `BotTabSimple`:**

| Событие | Сигнатура | Когда срабатывает | Когда использовать |
|---------|-----------|-------------------|-------------------|
| `CandleFinishedEvent` | `Action<List<Candle>>` | Закрылась свеча | Основная логика входа/выхода |
| `CandleUpdateEvent` | `Action<List<Candle>>` | Обновление текущей свечи | Обновление индикатора "на лету" |
| `NewTickEvent` | `Action<Trade>` | Новый тик | Тиковые стратегии, тиковый трейлинг |
| `MarketDepthUpdateEvent` | `Action<MarketDepth>` | Обновление стакана | HFT, торговля по стакану |
| `PositionOpeningSuccesEvent` | `Action<Position>` | Позиция полностью открыта | Сразу ставить SL/TP |
| `PositionOpenFailEvent` | `Action<Position>` | Ошибка открытия | Повторить вход или залогировать |
| `PositionClosingSuccesEvent` | `Action<Position>` | Позиция закрыта | Обновить статистику, сбросить флаги |
| `PositionClosingFailEvent` | `Action<Position>` | Ошибка закрытия | Принудительное `CloseAtMarket` |
| `MyTradeEvent` | `Action<MyTrade>` | Исполнение заявки | Точный контроль частичного исполнения |
| `OrderUpdateEvent` | `Action<Order>` | Любое изменение ордера | Отслеживание статуса отложенных ордеров |

> **Скринер** (`BotTabScreener`): события `CandleFinishedEvent` и `PositionOpeningSuccesEvent` имеют **дополнительный параметр** `BotTabSimple tab`:
> ```csharp
> void _screener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab) { }
> void _screener_PositionOpeningSuccesEvent(Position pos, BotTabSimple tab) { }
> ```

### 2.4 Необязательные члены `BotPanel`

Следующие методы можно **не переопределять**, если нет явной необходимости:

| Член | Зачем нужен | Когда переопределять |
|------|-------------|----------------------|
| `GetNameStrategyType()` | Возвращает имя робота в списке OsEngine | Только если нужно динамическое имя. По умолчанию берётся из атрибута `[Bot("Name")]`. |
| `ShowIndividualSettingsDialog()` | Открывает кастомное WPF-окно настроек | Только для сложного UI. В 95% случаев достаточно авто-панели параметров `CreateParameter`. |
| `Description` | Описание в интерфейсе | Можно опустить. |

> **Правило для ИИ**: Не генерировать `GetNameStrategyType()` и `ShowIndividualSettingsDialog()` без прямой просьбы пользователя.

**Основные торговые методы `BotTabSimple`:**

```csharp
// Вход в позицию
_tab.BuyAtMarket(volume);
_tab.SellAtMarket(volume);
_tab.BuyAtLimit(volume, price);
_tab.SellAtLimit(volume, price);
_tab.BuyAtStop(volume, price, orderPrice, StopActivateType.HigherOrEqual, slippage);
_tab.SellAtStop(volume, price, orderPrice, StopActivateType.LowerOrEqual, slippage);

// Выход из позиции
_tab.CloseAtMarket(position, volume);
_tab.CloseAtLimit(position, price, volume);
_tab.CloseAtStop(position, price, slippage);
_tab.CloseAtProfit(position, price, slippage);
_tab.CloseAtTrailingStop(position, stopPrice, orderPrice);

// Добавление к позиции
_tab.BuyAtMarketToPosition(position, volume);
_tab.BuyAtLimitToPositionUnsafe(position, price, volume);

// Отмена ордеров
_tab.CloseAllOrderToPosition(position);
```

**Работа с позициями:**
```csharp
List<Position> openPositions = _tab.PositionsOpenAll;
List<Position> closedPositions = _tab.PositionsClosedAll;
PositionStateType state = position.State; // Open, Opening, Closing, Closed
decimal pnl = position.ProfitPortfolioPunkt;
Side direction = position.Direction; // Buy / Sell
```

---

## 3. Папки проекта

### 3.1 Корневая структура

```
OsEngine/
├── Robots/              # Торговые роботы (основная папка для разработки)
│   ├── MyBots/          # Папка по умолчанию для новых роботов
│   ├── Trend/           # Трендовые стратегии
│   ├── CounterTrend/    # Контртрендовые стратегии
│   ├── Patterns/        # Свечные паттерны и уровни
│   ├── Screeners/       # Скринеры
│   ├── PositionsMicromanagement/  # Управление позициями
│   ├── PairArbitrage/   # Парный арбитраж
│   ├── Grids/           # Сеточные стратегии
│   ├── TechSamples/     # Технические примеры API
│   ├── FuturesTrend/    # Перекладывание фьючерсов
│   ├── FuturesStart/    # Контанго-арбитраж
│   └── BotsFromStartLessons/  # Учебные роботы
├── Indicators/          # Индикаторы (Aindicator, Samples, Scripts)
├── Entity/              # Сущности (свечи, позиции, ордера)
├── Market/              # Работа с биржами
│   └── Servers/         # Коннекторы (Binance, MOEX, Tinkoff и др.)
├── Charts/              # Графики и визуальные элементы
├── Journal/             # Журнал сделок
├── OsTrader/            # Торговый терминал
├── OsOptimizer/         # Оптимизатор стратегий
├── OsData/              # Работа с данными
└── OsConverter/         # Конвертер данных
```

### 3.2 Карта навигации: от задачи к папке

| Какая задача? | Куда лезть |
|---------------|------------|
| Торговля по тренду (пробой, следование) | `Robots/Trend/` |
| Торговля против тренда (отскок) | `Robots/CounterTrend/` |
| Торговля по свечным паттернам | `Robots/Patterns/` |
| Торговля по множеству инструментов | `Robots/Screeners/` |
| Сложное управление позицией | `Robots/PositionsMicromanagement/` |
| Парный арбитраж | `Robots/PairArbitrage/` + `CONTEXT_PAIRS_AND_FUTURES.md` |
| Сетка ордеров | `Robots/Grids/` |
| Ротация фьючерсов (экспирация) | `Robots/FuturesTrend/` + `CONTEXT_PAIRS_AND_FUTURES.md` |
| Контанго-арбитраж | `Robots/FuturesStart/` + `CONTEXT_PAIRS_AND_FUTURES.md` |
| Изучение API OsEngine | `Robots/TechSamples/` |
| Написание индикатора | `OsEngine/Indicators/Scripts/` + `CONTEXT_INDICATORS.md` |
| Новый робот (по умолчанию) | `Robots/MyBots/` |

---

## 4. Рекомендации по разработке

### 4.1 DO
- ✅ Всегда проверять `_regime.ValueString == "On"` перед торговлей
- ✅ Проверять готовность индикатора (`candles.Count + N < indicator.Values.Count`)
- ✅ Использовать `GetVolume()` для расчёта объёма
- ✅ Отменять стоп-ордера при открытии позиции (`BuyAtStopCancel()`)
- ✅ Проверять `position.State == PositionStateType.Open`
- ✅ Сохранять и перезагружать индикаторы после изменения параметров
- ✅ Использовать `_slippage` для учёта проскальзывания
- ✅ Логировать ошибки через `SendNewLogMessage(error.ToString(), LogMessageType.Error)`

### 4.2 DON'T
- ❌ Не торговать без проверки режима робота
- ❌ Не открывать позицию, если уже есть открытая (проверяй `PositionsOpenAll.Count`)
- ❌ Не использовать хардкод для цен — всегда через параметры
- ❌ Не забывать про `PriceStep` при расчёте цен ордеров
- ❌ Не давать исключениям «падать» — это остановит робота
- ❌ Не создавать табы вне конструктора

### 4.3 Техническое задание на робота

Перед созданием робота составьте краткое ТЗ. Структура:

**1. Тип таба**
- Тип: `BotTabSimple` / `BotTabScreener` / `BotTabPair`
- Обоснование: один инструмент / скринер / пара

**2. Индикаторы**
- Название, параметры, область (`Prime` / `NewArea0`)

**3. Точки входа/выхода**
- Условие входа Long/Short
- Тип ордера (Market / Limit / Stop)
- Объём
- Стоп-лосс, тейк-профит, трейлинг-стоп

**4. Примеры для заимствования**
- Из `CONTEXT_ROBOTS.md` указать раздел и имя робота
- Что берём (структура, логика входа), что меняем

**5. Параметры**
- Regime, индикаторы, объём, риски

**6. Особые случаи**
- Проверка готовности индикатора
- Защита от повторного входа
- try-catch

---

## 5. Быстрые шаблоны

### 5.1 Минимальный робот на `BotTabSimple`

```csharp
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Logging;
using System.Collections.Generic;

[Bot("MySimpleBot")]
public class MySimpleBot : BotPanel
{
    private BotTabSimple _tab;
    private StrategyParameterString _regime;

    public MySimpleBot(string name, StartProgram startProgram) : base(name, startProgram)
    {
        _regime = CreateParameter("Regime", "On", new[] { "On", "Off", "OnlyLong", "OnlyShort" });
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
    }

    private void _tab_CandleFinishedEvent(List<Candle> candles)
    {
        try
        {
            if (_regime.ValueString == "Off") return;
            if (candles.Count < 10) return;
            if (_tab.PositionsOpenAll.Count > 0) return;

            decimal lastClose = candles[candles.Count - 1].Close;
            decimal prevClose = candles[candles.Count - 2].Close;

            if (lastClose > prevClose && _regime.ValueString != "OnlyShort")
            {
                _tab.BuyAtMarket(GetVolume(_tab));
            }
            else if (lastClose < prevClose && _regime.ValueString != "OnlyLong")
            {
                _tab.SellAtMarket(GetVolume(_tab));
            }
        }
        catch (Exception error)
        {
            SendNewLogMessage(error.ToString(), LogMessageType.Error);
        }
    }
}
```

### 5.2 Минимальный скринер на `BotTabScreener`

```csharp
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Logging;
using System.Collections.Generic;

[Bot("MyScreener")]
public class MyScreener : BotPanel
{
    private BotTabScreener _tabScreener;
    private StrategyParameterInt _maxPositions;

    public MyScreener(string name, StartProgram startProgram) : base(name, startProgram)
    {
        _maxPositions = CreateParameter("Max positions", 5, 1, 20, 1);
        TabCreate(BotTabType.Screener);
        _tabScreener = TabsScreener[0];
        _tabScreener.CandleFinishedEvent += _tabScreener_CandleFinishedEvent;
    }

    private void _tabScreener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
    {
        try
        {
            if (_tabScreener.PositionsOpenAll.Count >= _maxPositions.ValueInt) return;
            if (tab.PositionsOpenAll.Count > 0) return;

            // Логика входа на конкретном инструменте
            decimal lastClose = candles[candles.Count - 1].Close;
            // ... сигнал ...
            tab.BuyAtMarket(GetVolume(tab));
        }
        catch (Exception error)
        {
            SendNewLogMessage(error.ToString(), LogMessageType.Error);
        }
    }
}
```

### 5.3 Минимальный парный арбитраж на `BotTabPair`

```csharp
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Logging;
using System.Collections.Generic;

[Bot("MyPairBot")]
public class MyPairBot : BotPanel
{
    private BotTabPair _pairTrader;
    private StrategyParameterString _regime;

    public MyPairBot(string name, StartProgram startProgram) : base(name, startProgram)
    {
        _regime = CreateParameter("Regime", "On", new[] { "On", "Off" });
        TabCreate(BotTabType.Pair);
        _pairTrader = TabsPair[0];
        _pairTrader.CorrelationChangeEvent += _pairTrader_CorrelationChangeEvent;
    }

    private void _pairTrader_CorrelationChangeEvent(List<PairIndicatorValue> correlation, PairToTrade pair)
    {
        try
        {
            if (_regime.ValueString == "Off") return;
            if (pair.CorrelationLast < 0.9m) return;

            if (!pair.HavePositions && pair.SideCointegrationValue == CointegrationLineSide.Up)
            {
                pair.SellSec1BuySec2();
            }
            else if (!pair.HavePositions && pair.SideCointegrationValue == CointegrationLineSide.Down)
            {
                pair.BuySec1SellSec2();
            }
            else if (pair.HavePositions && pair.SideCointegrationValue != pair.LastEntryCointegrationSide)
            {
                pair.ClosePositions();
            }
        }
        catch (Exception error)
        {
            SendNewLogMessage(error.ToString(), LogMessageType.Error);
        }
    }
}
```

---

## 6. Индикаторы — краткая сводка

В OsEngine **две системы индикаторов**:
- **Устаревшие:** `OsEngine/Charts/CandleChart/Indicators/` — не трогать, не создавать новые
- **Актуальные:** наследники `Aindicator` в `OsEngine/Indicators/Scripts/` (~110 штук)

**Минимальный индикатор:**
```csharp
[Indicator("MyInd")]
public class MyInd : Aindicator
{
    public IndicatorParameterInt _length;
    public IndicatorDataSeries _series;

    public override void OnStateChange(IndicatorState state)
    {
        if (state == IndicatorState.Configure)
        {
            _length = CreateParameterInt("Length", 14);
            _series = CreateSeries("Line", Color.Red, IndicatorChartPaintType.Line, true);
        }
    }

    public override void OnProcess(List<Candle> source, int index)
    {
        if (index < _length.ValueInt) { _series.Values[index] = 0; return; }
        _series.Values[index] = source[index].Close;
    }
}
```

**Использование в роботе:**
```csharp
Aindicator sma = IndicatorsFactory.CreateIndicatorByName("Sma", "MySma", false);
sma = (Aindicator)_tab.CreateCandleIndicator(sma, "Prime");
decimal last = sma.DataSeries[0].Last;
```

**Подробности:** см. `CONTEXT_INDICATORS.md` — каталог из 30 индикаторов, типы параметров, встроенные индикаторы, привязка к табам.

---

**Изучать:**
- `CONTEXT.md` — эта база (архитектура, шаблоны, выбор таба)
- `CONTEXT_ROBOTS.md` — каталог готовых роботов для заимствования кода
- `CONTEXT_INDICATORS.md` — написание и использование индикаторов
- `CONTEXT_PAIRS_AND_FUTURES.md` — парный арбитраж, фьючерсы, контанго
