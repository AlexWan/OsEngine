# OsEngine — Контекст разработки

## 1. Метаданные проекта

| Параметр | Значение |
|----------|----------|
| **Название** | OsEngine |
| **Платформа** | .NET 9 (`net9.0-windows`) |
| **Тип приложения** | WPF Desktop |
| **Назначение** | Платформа для алгоритмической торговли на бирже |
| **Язык** | C# |
| **Репозиторий** | https://github.com/AlexWan/OsEngine |

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

1. **Уникальное имя робота.** При создании нового робота всегда давайте ему уникальное имя, которого нет в проекте:
   - Имя указывается в атрибуте `[Bot("ИмяРобота")]`
   - Имя должно быть уникальным во всём репозитории (проверьте через поиск по папке `Robots/`)
   - Не используйте имена существующих роботов (`EnvelopTrend`, `PinBarTrade`, `SmaScreener` и т.д.)
   - Рекомендуется использовать префикс или уникальное название стратегии
   - Примеры уникальных имён: `[Bot("MyBollingerBreakout")]`, `[Bot("CryptoTrendFollower2025")]`

2. **Папка для новых роботов.** Если пользователь не указывает явно, создавайте новых роботов в папке `Robots/MyBots/`:
   - Эта папка предназначена для пользовательских роботов
   - Не создавайте роботов в корневой папке `Robots/` напрямую
   - Придерживайтесь структуры: `Robots/MyBots/НазваниеРобота/НазваниеРобота.cs`
   - Для роботов со стратегиями можно создавать подпапки: `Robots/MyBots/Trend/`, `Robots/MyBots/Screeners/`

3. **Переопределяемые методы.** Переопределять эти методы не обязательно:
   - `GetNameStrategyType()` — возвращает имя стратегии (обычно совпадает с именем класса)
   - `ShowIndividualSettingsDialog()` — открывает диалог настроек (может быть пустым, если настройки не нужны)
   - Без этих методов робот всё равно скомпилируется и будет работать

4. **Конструктор робота.** Конструктор всегда должен иметь сигнатуру:
   ```csharp
   public RobotName(string name, StartProgram startProgram) : base(name, startProgram)
   ```
   - `name` — экземплярное имя робота (может быть не уникальным, используется для отображения)
   - `startProgram` — тип запуска (`IsOsTrader`, `IsTester`, `IsOptimizer`, `IsScanner`)
   - Всегда передавайте параметры в базовый конструктор `base(name, startProgram)`

5. **Создание табов только в конструкторе.** Все табы должны создаваться в конструкторе:
   - Не создавайте табы в обработчиках событий или в торговой логике
   - Табы создаются через `TabCreate(BotTabType.Тип)`
   - Ссылки на табы сохраняются в поля класса: `_tab = TabsSimple[0]`
   - Подписка на события табов также выполняется в конструкторе

6. **Проверка типа запуска через `StartProgram`.** Используйте `this.StartProgram` для определения режима работы:
   - `StartProgram.IsOsTrader` — реальная торговля (требует проверки на ошибки, защиты от сбоев)
   - `StartProgram.IsTester` — тестирование на истории
   - `StartProgram.IsOptimizer` — оптимизация (избегайте тяжёлых вычислений, логирования)
   - Пример: `if (this.StartProgram == StartProgram.IsOsTrader) { /* только для реальной торговли */ }`

7. **Жизненный цикл робота.** OsEngine вызывает методы робота в определённом порядке:
   ```
   Конструктор → (создание табов, параметров, индикаторов)
        ↓
   Запуск → (подписка на события, начало торговли)
        ↓
   События → (CandleFinishedEvent, PositionOpeningSuccesEvent, и т.д.)
        ↓
   Остановка → (закрытие позиций, сохранение настроек)
        ↓
   Удаление → (очистка ресурсов, удаление файлов настроек)
   ```

8. **Потокобезопасность.** События табов могут приходить из разных потоков:
   - Не используйте общие переменные без блокировок (`lock`)
   - Избегайте длительных операций в обработчиках событий
   - Для тяжёлых вычислений используйте отдельные потоки (`Thread`, `Task`)
   - Обновление UI (графки, элементы) только через `Dispatcher` или в главном потоке

9. **Обработка ошибок.** Всегда оборачивайте торговую логику в `try-catch`:
    ```csharp
    private void _tab_CandleFinishedEvent(List<Candle> candles)
    {
        try
        {
            // Торговая логика
        }
        catch (Exception error)
        {
            SendNewLogMessage(error.ToString(), LogMessageType.Error);
        }
    }
    ```
    - Логируйте ошибки через `SendNewLogMessage(error.ToString(), LogMessageType.Error)`
    - Не давайте исключениям "падать" — это остановит робота
    - В реальном терминале (`IsOsTrader`) обработка ошибок критически важна
    
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
        // Создание параметров
        _regime = CreateParameter("Regime", "On", new[] { "On", "Off" });
        _period = CreateParameter("Period", 14, 5, 100, 1);
        
        // Создание таба
        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];
        
        // Подписка на события
        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
    }
    
    // 3. Обработчик событий
    private void _tab_CandleFinishedEvent(List<Candle> candles)
    {
        try
        {
            // Проверка режима
            if (_regime.ValueString == "Off") return;
            
            // Проверка типа запуска
            if (this.StartProgram == StartProgram.IsOptimizer) return;
            
            // Торговая логика
            // ...
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

**Проверка уникальности имени:**

Перед созданием робота выполните поиск по репозиторию:
```bash
# Поиск по атрибуту [Bot("...")]
grep -r '\[Bot("' Robots/ | grep -i "ИмяРобота"
```

Если найдены совпадения — выберите другое имя.

### 2.2 Типы торговых табов (`BotTabType`)

**Что такое iBotTab ("таб", "источник"):**

`iBotTab` — это базовый интерфейс торгового источника в архитектуре OsEngine. Пользователи могут называть это **"таб"** или **"источник"** (source). Это фундаментальный строительный блок любого робота.

**Важность для архитектуры робота:**

1. **Источник всех данных и событий.** Абсолютное большинство данных и событий приходит из источников:
   - Свечи (`CandleFinishedEvent`, `CandleUpdateEvent`)
   - Тики (`NewTickEvent`)
   - Стакан (`MarketDepthUpdateEvent`)
   - Позиции (`PositionOpeningSuccesEvent`, `PositionClosedEvent`)
   - Ордера (`OrderUpdateEvent`)
   - Новости (`NewsEvent`)
   
2. **Все торговые операции проходят через источники.** Нельзя открыть или закрыть позицию минуя таб:
   - Открытие: `_tab.BuyAtMarket()`, `_tab.SellAtLimit()`, `_tab.BuyAtStop()`
   - Закрытие: `_tab.CloseAtMarket()`, `_tab.CloseAtStop()`, `_tab.CloseAtTrailingStop()`
   - Добавление к позиции: `_tab.BuyAtMarketToPosition()`
   
3. **Индикаторы привязываются к источникам.** Индикатор не существует сам по себе — он создаётся на конкретном табе и получает данные из него:
   - `_tab.CreateCandleIndicator()` — создание индикатора на табе
   - `_tab.Indicators[0]` — доступ к индикаторам таба
   
4. **Один робот может иметь несколько источников.** Это позволяет:
   - Торговать множество инструментов одновременно (скринеры)
   - Использовать несколько таймфреймов (мульти-ТФ стратегии)
   - Торговать пары инструментов (парный арбитраж)
   - Анализировать разные типы данных (кластеры + свечи)

**Жизненный цикл таба в роботе:**

```csharp
// 1. Создание таба в конструкторе
TabCreate(BotTabType.Simple);

// 2. Получение ссылки на созданный таб
BotTabSimple _tab = TabsSimple[0];

// 3. Подписка на события таба
_tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
_tab.PositionOpeningSuccesEvent += _tab_PositionOpeningSuccesEvent;

// 4. Использование таба в торговой логике
private void _tab_CandleFinishedEvent(List<Candle> candles)
{
    // Получение данных из таба
    decimal price = _tab.PriceBestAsk;
    
    // Торговая операция через таб
    _tab.BuyAtMarket(volume);
    
    // Доступ к позициям таба
    List<Position> positions = _tab.PositionsOpenAll;
}
```

**Типы табов и их специализация:**

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

**Важные замечания для разработки:**

1. **Каждый тип таба имеет свои свойства и методы.** 
   - `BotTabSimple` — базовые торговые методы (`BuyAtMarket`, `SellAtLimit`, `CloseAtStop` и т.д.)
   - `BotTabScreener` — методы для работы с несколькими инструментами, доступ к табу конкретного инструмента через обработчик события
   - `BotTabPair` — методы для парной торговли (`BuySec1SellSec2`, `SellSec1BuySec2`, `ClosePositions`)
   - `BotTabCluster` — методы для работы с кластерными данными (`FindMaxVolumeCluster`)
   - Другие типы имеют свои специфические методы

2. **При создании роботов по запросу человеков, нужно искать примеры именно с тем типом источника, который нужен для конкретного робота.**
   - Если просят скринер → смотри раздел `5.4 Screeners` (роботы из папки `Robots/Screeners/`)
   - Если просят парный арбитраж → смотри раздел `5.7 PairArbitrage` (роботы из папки `Robots/PairArbitrage/`)
   - Если просят кластерный анализ → смотри `ClusterCountertrend` в разделе `5.2.3`
   - Если просят простой торговый робот на одном инструменте → смотри разделы `5.1 Trend`, `5.2 CounterTrend`, `5.3 Patterns`

3. **Индикаторы создаются для разных источников по-разному.**
   - **BotTabSimple**: `_indicator = (Aindicator)_tab.CreateCandleIndicator(_indicator, "Prime")`
   - **BotTabScreener**: `_screenerTab.CreateCandleIndicator(id, "IndicatorName", parameters, "Prime")` — требуется ID индикатора
   - **BotTabPair**: индикаторы создаются автоматически для каждой пары, доступ через события
   - Доступ к данным индикатора также отличается:
     - `Simple`: `_indicator.DataSeries[0].Last`
     - `Screener`: `tab.Indicators[0].DataSeries[0].Last` (где `tab` — конкретный инструмент в обработчике)

4. **Торговые методы у разных табов не совпадают.**
   - **BotTabSimple**: `BuyAtMarket`, `SellAtLimit`, `CloseAtStop`, `CloseAtTrailingStop`
   - **BotTabScreener**: те же методы, но вызываются на `tab` конкретного инструмента в обработчике `CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)`
   - **BotTabPair**: `BuySec1SellSec2()`, `SellSec1BuySec2()`, `ClosePositions()` — методы уровня пары
   - **BotTabCluster**: специфические методы для работы с объёмами
   - Нельзя использовать методы одного типа таба с другим типом

**Примеры выбора правильного типа таба:**

| № | Запрос пользователя | Тип таба | Раздел для примеров | Примечание |
|---|---------------------|----------|---------------------|------------|
| 1 | "Торговля по одному инструменту" | `BotTabSimple` | 5.1, 5.2, 5.3 | Базовый случай |
| 2 | "Скринер по множеству инструментов" | `BotTabScreener` | 5.4 | Один индикатор на все инструменты |
| 3 | "Парный арбитраж (2 коррелирующих инструмента)" | `BotTabPair` | 5.7 | Автоматический расчёт корреляции |
| 4 | "Кластерный анализ объёмов" | `BotTabCluster` + `BotTabSimple` | 5.2.3 | Cluster для анализа, Simple для торговли |
| 5 | "Несколько таймфреймов на одном инструменте" | `BotTabSimple` ×2 | 5.1.4 (TwoTimeFramesBot) | Один таб = один ТФ |
| 6 | "Торговля фьючерсами с автоматической ротацией серий" | `BotTabScreener` | 5.10 | Скринер для выбора серии по экспирации |
| 7 | "Ротация фьючерсов по раздвижке к споту (контанго)" | `BotTabScreener` + `BotTabSimple` ×10 | 5.11 | Скринер для фьючерсов, Simple для спота |
| 8 | "Торговля на новостях" | `BotTabNews` + `BotTabSimple` | 5.6.5 | News для ленты, Simple для торговли |
| 9 | "Опционная стратегия (спреды, стрэддлы)" | `BotTabOptions` | — | Специфичные методы для опционов |
| 10 | "Торговля пользовательским индексом (корзина акций)" | `BotTabIndex` | — | Агрегация данных по нескольким бумагам |
| 11 | "Синтетические облигации (ОФЗ + фьючерс)" | `BotTabSyntheticBond` | — | Специфичный расчёт доходности |
| 12 | "Валютный арбитраж (треугольный арбитраж)" | `BotTabPolygon` | — | Работа с валютными парами |
| 13 | "Сеточная стратегия на одном инструменте" | `BotTabSimple` | 5.5 | GridsMaster на Simple табе |
| 14 | "Сеточная стратегия на множестве инструментов" | `BotTabScreener` | 5.4 | Grid на каждом инструменте скринера |
| 15 | "Паттерн 'Пин-бар' на скринере" | `BotTabScreener` | 5.4.1 | PinBarScreener как пример |
| 16 | "Объёмный анализ с кластерами + торговля" | `BotTabCluster` + `BotTabSimple` | 5.2.3 | ClusterCountertrend как пример |
| 17 | "Корреляционная торговля (2 акции)" | `BotTabPair` | 5.7 | PairCorrelationTrader как пример |
| 18 | "Коинтеграция (статистический арбитраж)" | `BotTabPair` | 5.7 | PairCointegrationSideTrader как пример |
| 19 | "Мульти-инструментальный портфель (10 акций)" | `BotTabSimple` ×10 | 5.11 | По одному табу на инструмент |
| 20 | "Спред фьючерсов (покупка ближней, продажа дальней)" | `BotTabPair` или `BotTabSimple` ×2 | 5.7 | Pair для авто-расчёта, или 2 Simple |
| 21 | "Маркет-мейкинг на одном инструменте" | `BotTabSimple` | 5.5 | GridTwoSides как пример |
| 22 | "Маркет-мейкинг на множестве инструментов" | `BotTabScreener` | 5.4.3 | GridScreenerAdaptiveSoldiers как пример |
| 23 | "Пробой уровня (breakout)" | `BotTabSimple` | 5.1 | PriceChannelTrade как пример |
| 24 | "Возврат к среднему (mean reversion) на корзине" | `BotTabScreener` | 5.2 | BollingerMomentumScreener как пример |
| 25 | "Сезонные паттерны на фьючерсах" | `BotTabScreener` | 5.10, 5.11 | FuturesTrend как пример |
| 26 | "Торговля на отчётности (earnings) с новостями" | `BotTabNews` + `BotTabSimple` | 5.6.5 | News для событий, Simple для входа |
| 27 | "Волатильный арбитраж (IV vs HV)" | `BotTabPair` + `BotTabOptions` | 5.7 | Pair для сравнения, Options для IV |
| 28 | "Анализ стакана (Order Flow) с торговлей" | `BotTabSimple` | 5.4.3 | PlateDetectorScreener (MarketDepthUpdateEvent) |
| 29 | "Пирамидинг (докупка в прибыль) на одном инструменте" | `BotTabSimple` | 5.5 | AlligatorTrendAverage как пример |
| 30 | "Адаптивная стратегия с подстройкой под волатильность" | `BotTabScreener` | 5.4.1 | PinBarVolatilityScreener, ThreeSoldierAdaptiveScreener |
| 31 | "Торговля по индикатору RSI на одном инструменте" | `BotTabSimple` | 5.2.2 | RsiContrtrend как пример |
| 32 | "Торговля по индикатору MACD на скринере" | `BotTabScreener` | 5.4.2 | BollingerMomentumScreener (MACD + Momentum) |
| 33 | "Ребалансировка портфеля по расписанию" | `BotTabSimple` ×N | 5.11 | Multiple Simple tabs с таймером |
| 34 | "Торговля 'три солдата' с адаптацией" | `BotTabScreener` | 5.4.1 | ThreeSoldierAdaptiveScreener как пример |
| 35 | "Ложный пробой (fakeout) с визуализацией" | `BotTabSimple` | 5.6.2 | FakeOutExample с линиями на графике |
| 36 | "Торговля по Alligator + Fractal + AO" | `BotTabSimple` | 5.1.4 | StrategyBillWilliams как пример |
| 37 | "Трейлинг-стоп по Parabolic SAR" | `BotTabSimple` | 5.1.2 | ParabolicSarTrade как пример |
| 38 | "Торговля по Pivot Points (уровни)" | `BotTabSimple` | 5.3.2 | PivotPointsRobot как пример |
| 39 | "Обнаружение пампа (pump detection)" | `BotTabScreener` | 5.4.3 | PumpDetectorScreener как пример |
| 40 | "Внешнее управление через Telegram/VK" | `BotTabSimple` | 5.1.2 | ParabolicSarTrade (TelegramCommandEvent) |

**Правила выбора типа таба:**

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Вопрос 1: Сколько инструментов торгуем?                                │
│  ├─ Один → BotTabSimple                                                 │
│  ├─ Несколько (один индикатор на все) → BotTabScreener                 │
│  ├─ Два коррелирующих → BotTabPair                                      │
│  └─ Корзина/Индекс → BotTabIndex                                        │
└─────────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────────┐
│  Вопрос 2: Какой тип данных нужен?                                      │
│  ├─ Только свечи → BotTabSimple / BotTabScreener                        │
│  ├─ Свечи + объёмы по ценам → BotTabCluster                             │
│  ├─ Свечи + новости → BotTabNews + BotTabSimple                         │
│  ├─ Опционы → BotTabOptions                                             │
│  └─ Валютные пары → BotTabPolygon                                       │
└─────────────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────────────┐
│  Вопрос 3: Нужны ли особые условия?                                     │
│  ├─ Ротация фьючерсов → BotTabScreener (выбор серии)                   │
│  ├─ Контанго/бэквордация → BotTabScreener + BotTabSimple (спот)        │
│  ├─ Несколько таймфреймов → BotTabSimple ×N (один таб = один ТФ)       │
│  └─ Визуализация на графике → BotTabSimple (SetChartElement)           │
└─────────────────────────────────────────────────────────────────────────┘
```

**Частые ошибки при выборе таба:**

| Ошибка | Почему неправильно | Как правильно |
|--------|-------------------|---------------|
| "Скринер на BotTabSimple" | Придётся создавать N табов вручную, нет автоматической итерации | Использовать `BotTabScreener` — один обработчик на все инструменты |
| "Парный арбитраж на двух BotTabSimple" | Нет авто-расчёта корреляции и коинтеграции | Использовать `BotTabPair` — события `CorrelationChangeEvent`, методы `BuySec1SellSec2()` |
| "Кластеры на BotTabSimple" | Нет доступа к кластерным данным (HorizontalVolumeCluster) | Использовать `BotTabCluster` для анализа + `BotTabSimple` для торговли |
| "Новости на BotTabSimple" | Нет события `NewsEvent` | Добавить `BotTabNews` для ленты новостей |
| "Индикатор без привязки к табу" | Индикатор не получит данные | Всегда создавать через `_tab.CreateCandleIndicator()` |


## 3. Папки проекта

### 3.1 Корневая структура

```
OsEngine/
├── Robots/              # Торговые роботы (основная папка для разработки)
│   ├── BotsFromStartLessons/  # Учебные роботы (Lesson1–Lesson9)
│   ├── CounterTrend/    # Контртрендовые стратегии
│   ├── Engines/         # Торговые движки
│   ├── FuturesStart/    # Контанго-ротация фьючерсов (спот vs фьючерсы)
│   ├── FuturesTrend/    # Перекладывание экспирируемых фьючерсов
│   ├── Grids/           # Сеточные стратегии
│   ├── MyBots/          # Папка по умолчанию для новых роботов
│   ├── PairArbitrage/   # Парный арбитраж (BotTabPair)
│   ├── Patterns/        # Свечные паттерны и уровни
│   ├── PositionsMicromanagement/  # Управление позициями (усреднение, пирамидинг)
│   ├── Screeners/       # Скринеры (множество инструментов)
│   ├── TechSamples/     # Технические примеры (API, визуализация, события)
│   └── Trend/           # Трендовые стратегии
├── Indicators/          # Индикаторы, новый, рекомендуемый слой
├── Entity/              # Сущности (свечи, позиции, ордера, портфели)
├── Market/              # Работа с биржами
│   ├── Servers/         # Коннекторы к биржам (Binance, MOEX, Tinkoff и др.)
│   └── Connectors/      # Подключения и адаптеры
├── Charts/              # Графики и визуальные элементы
├── Journal/             # Журнал сделок и позиций
├── OsTrader/            # Торговый терминал
│   └── Panels/          # Панели роботов
├── OsOptimizer/         # Оптимизатор стратегий
├── OsData/              # Работа с данными (скачивание, хранение)
└── OsConverter/         # Конвертер данных
```

### 3.2 Подпапки `Robots/` — детальное описание

Каждая подпапка содержит готовые примеры роботов для определённого класса задач. При создании нового робота по умолчанию используйте `Robots/MyBots/`.

| Папка | Что внутри | Ключевые типы табов | Раздел CONTEXT.md |
|-------|-----------|---------------------|-------------------|
| `Trend/` | Трендовые стратегии (пробой каналов, Parabolic, комбинации индикаторов) | `BotTabSimple` | **5.1** |
| `CounterTrend/` | Контртрендовые стратегии (отскок от Bollinger, RSI, кластеры) | `BotTabSimple`, `BotTabCluster` | **5.2** |
| `Patterns/` | Свечные паттерны (пин-бар, три солдата, импульсы) и уровни (Pivot Points) | `BotTabSimple` | **5.3** |
| `Screeners/` | Торговля по множеству инструментов (паттерновые, индикаторные, стакан) | `BotTabScreener` | **5.4** |
| `PositionsMicromanagement/` | Сложное управление позицией: частичное закрытие, усреднение, пирамидинг, айсберги | `BotTabSimple` | **5.5** |
| `TechSamples/` | Технические демонстрации API: индикаторы, графика, параметры, события, OI | `BotTabSimple`, `BotTabScreener` | **5.6** |
| `PairArbitrage/` | Парный арбитраж: корреляция, коинтеграция, расхождение двух инструментов | `BotTabPair` | **5.7** |
| `Grids/` | Сеточные стратегии (маркет-мейкинг, накопление позиции) | `BotTabSimple`, `BotTabScreener` | **5.8** |
| `BotsFromStartLessons/` | Пошаговый курс C# для алготрейдера (Lesson1–Lesson9) | `BotTabSimple` | **5.9** |
| `FuturesTrend/` | Перекладывание фьючерсов при приближении экспирации | `BotTabScreener` | **5.10** |
| `FuturesStart/` | Контанго-ротация: спот vs фьючерсы, ранжирование по контанго | `BotTabScreener` + `BotTabSimple` | **5.11** |
| `Engines/` | Торговые движки (универсальные шаблоны) | Разные | — |
| `MyBots/` | **Папка по умолчанию для новых роботов** | Любые | — |

### 3.3 Карта навигации: от задачи к папке

```
┌─────────────────────────────────────────────────────────────────────┐
│  Какая задача?                                                      │
├─────────────────────────────────────────────────────────────────────┤
│  Торговля по тренду (пробой, следование)       → Robots/Trend/      │
│  Торговля против тренда (отскок, перекупленность) → Robots/CounterTrend/ │
│  Торговля по свечным паттернам                 → Robots/Patterns/   │
│  Торговля по множеству инструментов            → Robots/Screeners/  │
│  Сложное управление позицией (усреднение и т.д.) → Robots/PositionsMicromanagement/ │
│  Парный арбитраж (2 коррелирующих инструмента) → Robots/PairArbitrage/ │
│  Сетка ордеров (маркет-мейкинг)                → Robots/Grids/      │
│  Ротация фьючерсов (экспирация)                → Robots/FuturesTrend/ │
│  Контанго-арбитраж (спот vs фьючерсы)          → Robots/FuturesStart/ │
│  Изучение API OsEngine (примеры)               → Robots/TechSamples/ │
│  Обучение с нуля (курс)                        → Robots/BotsFromStartLessons/ │
│  Новый робот (по умолчанию)                    → Robots/MyBots/     │
└─────────────────────────────────────────────────────────────────────┘
```

---

### 3.4 Расположение индикаторов за пределами проекта

OsEngine поддерживает **две системы индикаторов**:

#### А) Устаревшие встроенные индикаторы

**Путь:** `OsEngine/Charts/CandleChart/Indicators/`

- Содержит ~60+ скомпилированных индикаторов (`Bollinger.cs`, `Rsi.cs`, `Sma.cs`, `ParabolicSAR.cs` и др.)
- Каждый индикатор — пара файлов: логика (`.cs`) + окно настроек (`Ui.xaml.cs`)
- Эти индикаторы являются **устаревшими** и оставлены для обратной совместимости
- Не рекомендуется создавать новые индикаторы внутри проекта по этому пути

#### Б) Актуальные скриптовые индикаторы

**Путь:** `Custom/Indicators/Scripts/` (рядом с `.exe`, вне исходного кода проекта)

- Индикаторы хранятся как текстовые файлы `.cs` или `.txt`
- Компилируются в runtime через **Roslyn** при первом обращении
- Кэшируются в памяти (`_compiledIndicatorTypesCache`) — повторная компиляция не требуется
- Поддержка внешних DLL: положите `.dll` в подпапку `Custom/Indicators/Scripts/Dlls/`

**Пример структуры:**
```
OsEngine.exe
Custom/
└── Indicators/
    └── Scripts/
        ├── MySma.cs          ← кастомный индикатор
        ├── MyRsi.txt         ← тоже поддерживается
        └── Dlls/
            └── MyMathLib.dll ← внешняя библиотека
```

**Где брать шаблон для нового индикатора:**
- `OsEngine/Indicators/Samples/Sample1Blank.cs` — минимальный пустой индикатор
- `OsEngine/Indicators/Samples/Sample2IndicatorParameters.cs` — работа с параметрами
- `OsEngine/Indicators/Samples/Sample3IndicatorDataSeries.cs` — работа с сериями данных
- `OsEngine/Indicators/Aindicator.cs` — абстрактный базовый класс

**Правило:** Брать примеры для написания новых индикаторов в папке Custom/Indicators/Scripts/

---

## 4. Рекомендации по разработке

### 4.1 DO:
- ✅ Всегда проверять `_regime.ValueString == "On"` перед торговлей
- ✅ Проверять готовность индикатора (`candles.Count + N < indicator.Values.Count`)
- ✅ Использовать `GetVolume()` для расчёта объёма
- ✅ Отменять стоп-ордера при открытии позиции (`BuyAtStopCancel()`)
- ✅ Проверять `position.State == PositionStateType.Open`
- ✅ Сохранять и перезагружать индикаторы после изменения параметров
- ✅ Использовать `_slippage` для учёта проскальзывания

### 4.2 DON'T:
- ❌ Не торговать без проверки режима робота
- ❌ Не открывать позицию, если уже есть открытая (проверяй `PositionsOpenAll.Count`)
- ❌ Не использовать хардкод для цен — всегда через параметры
- ❌ Не забывать про `PriceStep` при расчёте цен ордеров

---

### 4.3 Создание технического задания

**Важность написания ТЗ:**

Перед созданием торгового робота **обязательно составьте техническое задание (ТЗ)**. Это критически важно для:
- **Понимания задачи:** Чёткое описание стратегии исключает недопонимание между заказчиком и разработчиком (ИИ)
- **Выбора правильных компонентов:** Заранее определяется, какие табы, индикаторы и методы API нужны
- **Экономии времени:** Готовое ТЗ позволяет сразу писать код, а не уточнять детали в процессе
- **Тестирования:** По ТЗ можно проверить, соответствует ли готовый робот задуманной логике
- **Документирования:** ТЗ служит документацией для будущей поддержки и модификации робота

**Структура технического задания на создание торгового робота:**

#### 4.3.1 Выбор табов под задачу пользователя

| Вопрос | Варианты ответов | Пример |
|--------|-----------------|--------|
| Сколько инструментов торгуем? | Один / Несколько / Два коррелирующих | "Один инструмент — BTC/USDT" |
| Какой тип данных нужен? | Свечи / Свечи + объёмы / Свечи + новости / Опционы | "Только свечи" |
| Нужна ли ротация инструментов? | Да / Нет | "Нет, торгуем один инструмент постоянно" |
| Нужны ли особые условия? | Мульти-ТФ / Контанго / Визуализация | "Нет особых условий" |

**Решение:** На основе ответов выберите тип таба из таблицы в разделе **2.2**:
- Один инструмент → `BotTabSimple`
- Несколько инструментов (скринер) → `BotTabScreener`
- Два коррелирующих → `BotTabPair`
- Свечи + объёмы по ценам → `BotTabCluster` + `BotTabSimple`
- Несколько таймфреймов → `BotTabSimple` ×N

**Пример для ТЗ:**
```
Тип таба: BotTabSimple
Обоснование: Торговля по одному инструменту (BTC/USDT), без ротации, 
без особых условий. Все данные — свечи.
```

---

#### 4.3.2 Какие индикаторы нужно использовать

| Параметр | Описание | Пример |
|----------|----------|--------|
| Название индикатора | Как называется в OsEngine | `Bollinger`, `Sma`, `Rsi`, `PriceChannel` |
| Параметры индикатора | Периоды, отклонения, длины | `Length=20`, `Deviation=2.0` |
| Область отображения | `Prime` (основная) / `NewArea0`, `NewArea1` (отдельные) | `Prime` |
| Количество индикаторов | Сколько индикаторов нужно | 2 (Bollinger + SMA) |
| Фильтры | Дополнительные условия по индикаторам | "SMA как тренд-фильтр: вход только если цена выше SMA" |

**Пример для ТЗ:**
```
Индикатор 1:
  - Название: Bollinger
  - Параметры: Length=20, Deviation=2.0
  - Область: Prime

Индикатор 2:
  - Название: Sma
  - Параметры: Length=100
  - Область: Prime
  - Назначение: Тренд-фильтр (вход в лонг только если цена > SMA)
```

**Где смотреть примеры индикаторов:**
- Раздел **4.5** — популярные индикаторы OsEngine
- Раздел **5** — готовые роботы с использованием индикаторов

---

#### 4.3.3 Точки входа и выхода из позиции

**Вход в позицию:**

| Параметр | Описание | Пример |
|----------|----------|--------|
| Условие входа | Логическое условие для открытия | `Close > BollingerUpper && Close > SMA` |
| Направление | Long / Short / Оба | Long и Short |
| Тип ордера | Market / Limit / Stop | `BuyAtMarket` / `SellAtStop` |
| Объём | Как рассчитывается | `GetVolume()` — 2% от депозита |
| Фильтры входа | Дополнительные условия | "Не входить если уже есть открытая позиция" |

**Выход из позиции:**

| Параметр | Описание | Пример |
|----------|----------|--------|
| Условие выхода | Логическое условие для закрытия | `Close < BollingerMiddle` |
| Тип выхода | Market / Limit / Stop / Trailing | `CloseAtTrailingStop` |
| Стоп-лосс | Цена или % от входа | 2% от цены входа |
| Тейк-профит | Цена или % от входа | 4% от цены входа |
| Трейлинг-стоп | Параметры трейлинга | Activation=1%, OrderPrice=0.5% |
| Выход по времени | Закрывать в конце дня / недели | "Закрывать все позиции до 18:00" |

**Пример для ТЗ:**
```
ВХОД В LONG:
  - Условие: Close > BollingerUpper && Close > SMA(100)
  - Тип ордера: BuyAtMarket
  - Объём: 2% от депозита (через GetVolume)
  - Фильтр: Не входить если PositionsOpenAll.Count > 0

ВХОД В SHORT:
  - Условие: Close < BollingerLower && Close < SMA(100)
  - Тип ордера: SellAtMarket
  - Объём: 2% от депозита
  - Фильтр: Не входить если PositionsOpenAll.Count > 0

ВЫХОД:
  - Условие: Close < BollingerMiddle (для Long) / Close > BollingerMiddle (для Short)
  - Тип: CloseAtMarket
  - Стоп-лосс: 2% от цены входа (CloseAtStop)
  - Тейк-профит: 4% от цены входа (CloseAtProfit)
  - Трейлинг-стоп: Activation=1%, OrderPrice=0.5%
```

---

#### 4.3.4 Выбор примеров с работающими роботами

**Где искать код для заимствования:**

| Задача | Раздел CONTEXT.md | Примеры роботов |
|--------|------------------|-----------------|
| Пробой канала | 5.1.1 | `EnvelopTrend`, `PriceChannelTrade` |
| Отскок от Bollinger | 5.2.1 | `StrategyBollinger` |
| Скринер | 5.4 | `PinBarScreener`, `SmaScreener` |
| Парный арбитраж | 5.7 | `PairCorrelationTrader` |
| Управление позицией | 5.5 | `UnsafeLimitsClosingSample`, `AlligatorTrendAverage` |
| Индикаторы | 5.6.1 | `BlockIndicatorsSample` |
| Визуализация | 5.6.2 | `ElementsOnChartSampleBot` |
| Фьючерсы с ротацией | 5.10 | `FuturesTrendPriceChannel`, `FuturesTrendBollinger` |
| Контанго | 5.11 | `FuturesStart1Bollinger`, `FuturesStart2Keltner` |
| Сеточная стратегия | 5.8 | `GridBollinger`, `GridTwoSides` |
| Учебные примеры | 5.9 | `Lesson1`–`Lesson9` |

**Пример для ТЗ:**
```
Базовый робот для заимствования кода:
  - Раздел: 5.2.1
  - Робот: StrategyBollinger.cs
  - Что берём: 
    * Структура класса (объявление полей, конструктор)
    * Логика входа (Close < BollingerDown)
    * Логика выхода (Close > SMA)
    * Обработка ParametrsChangeByUser

  - Что меняем:
    * Условия входа (добавляем фильтр SMA)
    * Типы ордеров (Market вместо Limit)
    * Параметры стопов (2% вместо фиксированной цены)
```

---

#### 4.3.5. Параметры робота

| Параметр | Тип | Значение по умолчанию | Диапазон | Группа |
|----------|-----|----------------------|----------|--------|
| `Regime` | String | "On" | On/Off | Base |
| `BollingerLength` | Int | 20 | 5–300 | Indicators |
| `BollingerDeviation` | Decimal | 2.0 | 0.5–4.0 | Indicators |
| `SmaLength` | Int | 100 | 10–500 | Indicators |
| `VolumeType` | String | "Deposit percent" | Contracts/Contract currency/Deposit percent | Volume |
| `VolumeValue` | Decimal | 2 | 1–50 | Volume |
| `StopLossPercent` | Decimal | 2.0 | 0.5–10.0 | Risk |
| `TakeProfitPercent` | Decimal | 4.0 | 1.0–20.0 | Risk |

**Пример для ТЗ:**
```
Параметры робота:
  1. Regime (String): "On" / "Off" / "OnlyLong" / "OnlyShort"
  2. BollingerLength (Int): 20, диапазон 5–300, группа "Indicators"
  3. BollingerDeviation (Decimal): 2.0, диапазон 0.5–4.0, группа "Indicators"
  4. SmaLength (Int): 100, диапазон 10–500, группа "Indicators"
  5. VolumeType (String): "Deposit percent", группа "Volume"
  6. VolumeValue (Decimal): 2, диапазон 1–50, группа "Volume"
  7. StopLossPercent (Decimal): 2.0, диапазон 0.5–10.0, группа "Risk"
  8. TakeProfitPercent (Decimal): 4.0, диапазон 1.0–20.0, группа "Risk"
```

---

#### 4.3.6. Обработка особых случаев

| Ситуация | Действие | Пример кода |
|----------|----------|-------------|
| Недостаточно данных | Выход из функции | `if (candles.Count < 50) return;` |
| Индикатор не готов | Проверка Count Values | `if (candles.Count + 5 < indicator.Values.Count) return;` |
| Уже есть позиция | Проверка PositionsOpenAll | `if (PositionsOpenAll.Count > 0) return;` |
| Ошибка в логике | try-catch + логирование | `catch (Exception e) { SendNewLogMessage(e.ToString(), LogMessageType.Error); }` |
| Изменение параметров | Пересчёт индикаторов | `ParametrsChangeByUser += Robot_ParametrsChangeByUser;` |
| Удаление робота | Очистка файлов | `DeleteEvent += Robot_DeleteEvent;` |

**Пример для ТЗ:**
```
Обработка особых случаев:
  1. Проверка готовности индикатора: 
     if (candles.Count + 5 < _bollinger.DataSeries[0].Values.Count) return;

  2. Проверка режима:
     if (_regime.ValueString == "Off") return;
  
  3. Защита от повторного входа:
     if (_tab.PositionsOpenAll.Count > 0) return;
  
  4. Обработка ошибок:
     Обернуть всю торговую логику в try-catch с логированием ошибок.
```

---

#### 4.3.7. Таймфрейм и инструменты

| Параметр | Значение | Примечание |
|----------|----------|------------|
| Таймфрейм | M5, H1, D1 | На каком ТФ работает робот |
| Инструменты | BTC/USDT, ETH/USDT | Конкретные тикеры или класс инструментов |
| Режим работы | 24/7 / Только сессия | "Торговля круглосуточно" / "Только 10:00–18:00" |
| Биржа | Binance, MOEX | Где торгуем |

**Пример для ТЗ:**
```
Таймфрейм: H1
Инструменты: BTC/USDT (один инструмент)
Режим работы: 24/7 (криптовалютная биржа)
Биржа: Binance
```

---

#### 4.3.8. Логирование и отладка

| Тип сообщения | Когда используется | Пример |
|---------------|-------------------|--------|
| `LogMessageType.User` | Информационные сообщения | "Позиция открыта по цене X" |
| `LogMessageType.Error` | Ошибки | "Exception: NullReferenceException at..." |
| `LogMessageType.System` | Системные события | "Робот запущен", "Робот остановлен" |

**Пример для ТЗ:**
```
Логирование:
  - Открытие позиции: SendNewLogMessage($"Long opened at {price}", LogMessageType.User);
  - Закрытие позиции: SendNewLogMessage($"Position closed, PnL = {pnl}", LogMessageType.User);
  - Ошибки: SendNewLogMessage(error.ToString(), LogMessageType.Error);
```

---

#### 4.3.9. Шаблон технического задания

```markdown
# Техническое задание на создание торгового робота

## 1. Тип таба
- **Тип:** BotTabSimple
- **Обоснование:** Торговля по одному инструменту

## 2. Индикаторы
- **Индикатор 1:** Bollinger (Length=20, Deviation=2.0, Prime)
- **Индикатор 2:** Sma (Length=100, Prime, тренд-фильтр)

## 3. Точки входа/выхода
- **Вход Long:** Close > BollingerUpper && Close > SMA
- **Вход Short:** Close < BollingerLower && Close < SMA
- **Выход:** Close < BollingerMiddle (Long) / Close > BollingerMiddle (Short)
- **Стоп-лосс:** 2% от цены входа
- **Тейк-профит:** 4% от цены входа

## 4. Примеры для заимствования
- **Базовый робот:** StrategyBollinger.cs (раздел 5.2.1)
- **Что берём:** Структура, логика входа/выхода
- **Что меняем:** Добавляем SMA-фильтр, меняем параметры стопов

## 5. Параметры
- Regime, BollingerLength, BollingerDeviation, SmaLength, VolumeType, VolumeValue, StopLossPercent, TakeProfitPercent

## 6. Особые случаи
- Проверка готовности индикатора
- Защита от повторного входа
- try-catch для обработки ошибок

## 7. Таймфрейм и инструменты
- **ТФ:** H1
- **Инструмент:** BTC/USDT
- **Режим:** 24/7

## 8. Логирование
- Логировать все открытия/закрытия позиций
- Логировать ошибки с полным стек trace
```

**Изучать:** Перед написанием ТЗ просмотрите разделы **2.2** (выбор таба), **4.5** (индикаторы), **5** (примеры роботов). Используйте таблицы из этих разделов для обоснования выбора компонентов.

## 5. Примеры хороших роботов

### 5.1 Трендовые роботы (`Robots/Trend/`)

Стратегии, торгующие в направлении тренда. Все роботы используют `BotTabSimple` и работают на свечных событиях.

#### 5.1.1 Пробой каналов и полос

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `EnvelopTrend` | Пробой Envelops | `BuyAtStop`/`SellAtStop` на пробой границ; `PositionOpeningSuccesEvent` для установки `CloseAtTrailingStop` сразу после открытия; пересчёт цены активации трейлинг-стопа от границы канала; `ParametrsChangeByUser` с перезагрузкой индикатора |
| `PriceChannelTrade` | Пробой PriceChannel | Реверс позиции: при закрытии Long по пробою нижней границы сразу открывается Short (`SellAtLimit` внутри `LogicClosePosition`); проверка `_lastPriceH > _lastPriceChUp && _lastPriceL < _lastPriceChDown` — исключение противоречивого сигнала; ручное Save/Load настроек через `StreamWriter`/`StreamReader` |
| `BreakLinearRegressionChannel` | Пробой Linear Regression | `BuyAtMarket`/`SellAtMarket` при пробое; выход через `CloseAtStop` по противоположной границе канала; **SMA-фильтры** (`BuySignalIsFiltered`/`SellSignalIsFiltered`): фильтр по положению цены от SMA и по наклону SMA; отмена всех стопов при выходе за время торговли (`CancelStopsAndProfits`); группировка параметров через строку-третий аргумент `CreateParameter` |

#### 5.1.2 Parabolic-стратегии

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `ParabolicSarTrade` | Пробой Parabolic SAR | Реверс по Parabolic SAR: при пересечении SAR закрытие + открытие противоположной позиции; **внешнее управление**: обработчики `ServerTelegram.TelegramCommandEvent` и `ServerVk.VkCommandEvent` — удалённый старт/стоп бота, получение статуса; `Command`/`CommandVk` перечисления |
| `ParabolicPriceChannel` | Parabolic Price Channel | `BuyAtStop`/`SellAtStop` с `StopActivateType.HigherOrEqual`/`LowerOrEqual`; выход через `CloseAtTrailingStop` по Parabolic Stop; **SMA-фильтры** (положение + наклон); время торговли (`StrategyParameterTimeOfDay`); активация/деактивация индикатора (`_smaFilter.IsOn = false`/`true` + `Reload()`) |
| `ParabolicBollinger` | Parabolic Bollinger | Комбинация Bollinger + Parabolic Stop; `BuyAtStop`/`SellAtStop` на пробой Bollinger; `CloseAtTrailingStop` по Parabolic; **SMA-фильтры**; `StrategyParameterLabel` для визуального разделения групп параметров; `IndicatorParameterString` для строкового параметра индикатора (период волатильности) |

#### 5.1.3 Комбинации осцилляторов и скользящих

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `MomentumMacd` | MACD + Momentum | Два индикатора: `MacdLine` (2 DataSeries: up/down) + `Momentum`; вход при пересечении MACD + подтверждении Momentum (>100/<100); реверс позиции: внутри закрытия вызывается открытие противоположной; `DeleteEvent` для удаления файла настроек при удалении робота |
| `SmaStochastic` | SMA + Stochastic | Два индикатора в разных областях (`Prime` + `NewArea0`); вход при пересечении SMA со сдвигом (`_lastSma + Step`) + пересечении уровней Stochastic; **ручное сохранение/загрузка** всех настроек в текстовый файл (`Engine\NameSettingsBot.txt`); WPF-диалог настроек (`ShowIndividualSettingsDialog`) |

#### 5.1.4 Сложные и мульти-таймфреймовые системы

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `StrategyBillWilliams` | Alligator + Fractal + AO | Три индикатора Билла Вильямса: `Alligator` (3 линии), `Fractal` (поиск последнего фрактала вручную в цикле), `AO` (Awesome Oscillator); вход по пробою фрактала + Alligator; **докупка (пирамидинг)**: `_maximumPositions` позиций, `LogicOpenPositionSecondary` — добавление по дивергенции AO (`_secondAo < _lastAo && _secondAo < _thirdAo`); лимитные заявки со сдвигом (`_slippage * PriceStep`); ограничение времени торговли (11:00–18:00) |
| `TwoTimeFramesBot` | Два таймфрейма | **Две вкладки**: `_tabToTrade` (младший ТФ) + `_tabBigTf` (старший ТФ); `TabCreate(BotTabType.Simple)` ×2; вход только при совпадении сигналов: пробой PriceChannel на младшем + цена выше SMA на старшем (`_tabBigTf.CandlesAll`); индикаторы привязаны к разным табам (`_pc` на `_tabToTrade`, `_sma` на `_tabBigTf`) |

**Ключевые паттерны трендовых роботов:**

```csharp
// Отложенное открытие позиции через Стоп-ордер на пробой с активацией HigherOrEqual
_tab.BuyAtStop(volume, price, price + slippage, StopActivateType.HigherOrEqual, 1);

// Трейлинг-стоп сразу после открытия позиции
_tab.PositionOpeningSuccesEvent += (pos) => {
    _tab.CloseAtTrailingStop(pos, activationPrice, orderPrice);
};

// Реверс позиции: закрыть Long и сразу открыть Short
if (position.Direction == Side.Buy && exitSignal) {
    _tab.CloseAtLimit(position, price, volume);
    _tab.SellAtLimit(GetVolume(_tab), price);  // реверс
}

// Две вкладки (два ТФ)
TabCreate(BotTabType.Simple);  // [0] — торговая
TabCreate(BotTabType.Simple);  // [1] — фильтр на старшем ТФ
_tabBigTf = TabsSimple[1];
```

**Изучать:** `EnvelopTrend.cs` — базовый пробой канала с трейлинг-стопом. `BreakLinearRegressionChannel.cs` — пробой с SMA-фильтрами и стоп-ордерами. `StrategyBillWilliams.cs` — мульти-индикаторная система с пирамидингом. `TwoTimeFramesBot.cs` — работа с двумя таймфреймами. `ParabolicSarTrade.cs` — внешнее управление через Telegram/VK.

---

### 5.2 Контртрендовые роботы (`Robots/CounterTrend/`)

Стратегии на отскок от границ канала или экстремальных уровней осцилляторов. Вход против тренда — в зоны перекупленности/перепроданности.

#### 5.2.1 Отскок от полос Боллинджера

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `StrategyBollinger` | Отскок от Bollinger + SMA | **Классика контртренда**: покупка при `Close < BollingerDown`, продажа при `Close > BollingerUp`; выход при пересечении ценой SMA (возврат к среднему); `BotTradeRegime` как публичное поле (не `StrategyParameter`); ручное Save/Load через `StreamWriter`/`StreamReader`; WPF-диалог настроек (`StrategyBollingerUi`); `DeleteEvent` для удаления файла настроек; ограничение времени закрытия (до 18:00) |

#### 5.2.2 Осцилляторные стратегии

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `WilliamsRangeTrade` | Williams %R | **Горизонтальные линии на графике индикатора**: `LineHorisontal` в области `WilliamsArea` (`_tab.SetChartElement`); `_upline.Value = -20`, `_downline.Value = -80` — уровни перекупленности/перепроданности; вход при `_lastWr < _downline` (Buy) / `_lastWr > _upline` (Sell); выход на возврат к противоположному уровню; реверс позиции при выходе; `_upline.Refresh()` / `_downline.Refresh()` для обновления линий на графике |
| `RsiContrtrend` | RSI + SMA (тренд-фильтр) | **Контртренд с фильтром по тренду**: вход только если цена выше SMA (падение в зону перепроданности RSI при восходящем тренде) или ниже SMA (рост в перекупленность при нисходящем); два индикатора в разных областях (`Prime` + `RsiArea`); `LineHorisontal` на RSI для уровней 65/35; выход по возвратному сигналу RSI **или** пересечению SMA — что наступит раньше (`\|\|` в условии); настройка цвета серии индикатора (`_sma.DataSeries[0].Color = Color.CornflowerBlue`) |

#### 5.2.3 Объёмный анализ (кластеры)

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `ClusterCountertrend` | Кластерный контртренд | **Две вкладки разных типов**: `BotTabSimple` (торговля) + `BotTabCluster` (анализ объёмов); `TabCreate(BotTabType.Cluster)`; поиск максимального кластера покупок/продаж за N свечей (`FindMaxVolumeCluster` с `ClusterType.BuyVolume`/`SellVolume`); вход при пробое ценой уровня максимального объёма; автоматический выход противоположной позиции при сигнале; работа с `HorizontalVolumeCluster` и `MaxBuyVolumeLine.Price` |


**Изучать:** `StrategyBollinger.cs` — базовый отскок с ручным Save/Load. `RsiContrtrend.cs` — осциллятор + тренд-фильтр + линии на графике. `ClusterCountertrend.cs` — работа с кластерным табом и объёмным анализом.


### 5.3 Паттерновые стратегии (`Robots/Patterns/`)

Торговля по свечным паттернам, паттернам и уровням.

#### 5.3.1 Свечные паттерны

| Робот | Паттерн | Что почерпнуть |
|-------|---------|----------------|
| `PinBarTrade` | Пин-бар | **Классический пин-бар**: проверка `candle.ShadowUp` / `candle.ShadowDown` относительно `candle.Body`; фильтр по SMA (`_tab.PriceBestBid > smaValue` для Buy); выход через `CloseAtTrailingStopMarket`; настройка минимальной длины тени через `StrategyParameterDecimal` |
| `ThreeSoldier` | Три солдата | **Три свечи подряд в одном направлении**: проверка `candles[i].IsUp` / `IsDown` в цикле; вход на 4-й свече после подтверждения паттерна; простой стоп-лосс в процентах от входа; `BotTradeRegime` для управления режимами торговли |
| `ThreeSoldierVolatilityAdaptive` | Адаптивные три солдата | **Волатильностная адаптация**: расчёт волатильности за N дней (`High - Low` в процентах); автоматическая подстройка параметров `_heightSignalCandle` и `_trailingStopPercent` от волатильности; `AdaptSignalCandleHeight()` вызывается при смене даты; группировка свечей по дате через `candle.TimeStart.Date` |
| `CandlePatternBoost` | Импульс + Van Gerchik | **Паттерн "Буст" за N свечей**: расчёт процентного движения за `_candleForBoost` свечей относительно канала Ван-Герчика; два фильтра SMA — позиционный (`_lastPrice < lastSma`) и по наклону (`lastSma < previousSma`); два режима выхода — трейлинг-стоп или по количеству свечей; `ParametrsChangeByUser` для динамического обновления индикаторов; `Reload()` индикаторов при изменении параметров |

#### 5.3.2 Уровни и паттерны

| Робот | Паттерн | Что почерпнуть |
|-------|---------|----------------|
| `PivotPointsRobot` | Pivot Points (R1/S1, R3/S3) | **Торговля на пробой уровней**: индикатор `PivotFloor` с сериями данных (R1=`DataSeries[1]`, S1=`DataSeries[4]`, R3=`DataSeries[3]`, S3=`DataSeries[6]`); вход при пересечении цены уровня (`Close > R1 && Open < R1`); выход по R3/S3 или стопу в процентах; WPF-диалог настроек (`PivotPointsRobotUi`) |
| `VolatilityAdaptiveCandlesTrader` | Адаптивные свечи | **Адаптация под волатильность**: расчёт средней волатильности за N дней в процентах; автоматическая подстройка `_heightSignalCandle` и `_trailingStopPercent`; вход только если размер свечи > порога в % от цены; проверка бычьей/медвежьей свечи через `Open < Close` / `Open > Close`; выход через `CloseAtTrailingStop` |
| `CustomCandlesImpulseTrader` | Импульс N свечей | **Серия свечей в одном направлении за заданное время**: проверка `IsUp`/`IsDown` в цикле; ограничение по времени между первой и последней свечой паттерна (`TimeSpan.TotalSeconds`); запись времени входа в `SignalTypeOpen` для последующего выхода; выход по количеству свечей после входа (`endCandlesFromOpenPosition`); `DateTime.ParseExact` для парсинга времени из `SignalTypeOpen` |

**Изучать:** `PinBarTrade.cs` — классический пин-бар с фильтром по SMA. `ThreeSoldierVolatilityAdaptive.cs` — адаптация параметров по волатильности. `CandlePatternBoost.cs` — сложный паттерн с фильтрами SMA и двумя режимами выхода. `CustomCandlesImpulseTrader.cs` — импульс с ограничением по времени.

### 5.4 Скринеры (`Robots/Screeners/`)

Торговля по множеству инструментов одновременно. Используется `BotTabScreener` — специальный тип таба, который автоматически создаёт отдельные вкладки для каждого инструмента из списка.

#### 5.4.1 Паттерновые скринеры

| Робот | Паттерн | Что почерпнуть |
|-------|---------|----------------|
| `PinBarScreener` | Пин-бар + SMA | **Базовый скринер пин-баров**: проверка входа в верхнюю/нижнюю треть диапазона (`lastClose >= lastHigh - ((lastHigh - lastLow) / 3)`); фильтр по высоте свечи в % (`lenCandlePercent`); фильтр по SMA (ручной расчёт `Sma()`); ограничение `_maxPositions` на количество одновременных позиций; выход через `CloseAtTrailingStop` |
| `PinBarVolatilityScreener` | Адаптивный пин-бар | **Волатильностная адаптация на инструмент**: класс `SecuritiesVolatilitySettings` для хранения настроек на каждый инструмент (`SecName`, `SecClass`, `HeightPinBar`); сохранение/загрузка в файл через `GetSaveString()`/`LoadFromString()`; `AdaptPinBarHeight()` вызывается раз в день; SMA-фильтр по наклону (`smaValue < smaPrev`); стоп в % от высоты паттерна; `CloseAtTrailingStopMarket` |
| `ThreeSoldierAdaptiveScreener` | Три солдата (адаптивный) | **Контртренд на три солдата**: проверка трёх свечей подряд в одном направлении; расчёт общего движения за 3 свечи в %; адаптация `_heightSoldiers` и `_minHeightOneSoldier` по волатильности; класс `SecuritiesTradeSettings` с сохранением на диск; фильтр SMA по наклону; выход через `CloseAtStop` + `CloseAtProfit` с расчётом от высоты паттерна |

#### 5.4.2 Индикаторные скринеры

| Робот | Индикаторы | Что почерпнуть |
|-------|------------|----------------|
| `SmaScreener` | SMA | **Простейший скринер**: вход если N свечей подряд выше SMA; ограничение `_maxPositions`; выход по трейлинг-стопу; базовая структура скринера для новичков |
| `BollingerMomentumScreener` | Bollinger + Momentum | **Пробой Bollinger с моментумом**: вход при `lastCandleClose > lastUpBollingerLine && lastMomentum > _minMomentumValue`; динамическое обновление параметров индикатора (`bollinger.ParametersDigit[0].Value = _bollingerLen.ValueInt`); `Reload()` индикатора; выход через `CloseAtTrailingStop` |
| `LinearRegressionFastScreener` | LinearRegression + ADX + SMA | **Три индикатора одновременно**: создание через `_screenerTab.CreateCandleIndicator(id, name, parameters, area)`; ADX-фильтр (`adxLast < _minAdxValue || adxLast > _maxAdxValue`); SMA-фильтр положения; вход по пробою верхней линии канала (`candleClose > lrUp`); выход по пробою нижней (`lastCandleClose < lrDown`); `BuyAtIcebergMarket` / `CloseAtIcebergMarket`; ограничение по времени торговли (`_timeStart`/`_timeEnd`) |
| `PriceChannelAdaptiveRsiScreener` | PriceChannelAdaptive + RSI + SMA | **RSI-фильтр + PriceChannel**: вход только если `rsi.Last > _minRsiValueToEntry` (высокий RSI); пробой `pcUp`; SMA-фильтр по наклону; выход через `CloseAtTrailingStopMarket(pos, pcDown)`; обновление параметров индикаторов через `ParametrsChangeByUser` |

#### 5.4.3 Специализированные скринеры

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `PlateDetectorScreener` | Анализ стакана | **Событие MarketDepthUpdateEvent**: подписка `_tabScreener.MarketDepthUpdateEvent += ...`; анализ объёмов в стакане (`md.Bids[0].Bid`); соотношение объёмов (`bestBidVolume / curVolume`); вход лимитным ордером над лучшим бидом; отмена ордера по времени (`_orderLifeTime`); стоп/профит в %; `StopOrderRedLine` для уровня стопа; `CloseAllOrderToPosition` для отмены; `ManualPositionSupport.DisableManualSupport()` |
| `PumpDetectorScreener` | Обнаружение пампа | **Резкий рост объёма/цены**: детектирование аномального движения; быстрый вход и выход; работа с высоковолатильными инструментами |

**Ключевые паттерны скринеров:**

```csharp
// Базовая структура скринера
TabCreate(BotTabType.Screener);
BotTabScreener _tabScreener = TabsScreener[0];
_tabScreener.CandleFinishedEvent += _tabScreener_CandleFinishedEvent;

// Обработчик получает tab конкретного инструмента
private void _tabScreener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
{
    List<Position> positions = tab.PositionsOpenAll;
    
    if (positions.Count == 0) {
        LogicOpenPosition(candles, tab);
    } else {
        LogicClosePosition(candles, tab, positions[0]);
    }
}

// Ограничение на количество позиций across all tabs
if (_tabScreener.PositionsOpenAll.Count >= _maxPositions.ValueInt) {
    return;
}

// Создание индикаторов на скринере
_screenerTab.CreateCandleIndicator(1, "ADX", new List<string>() { "30" }, "Second");
_screenerTab.CreateCandleIndicator(2, "LinearRegressionChannelFast_Indicator", 
    new List<string>() { "50", "Close", "2", "2" }, "Prime");
_screenerTab.CreateCandleIndicator(3, "Sma", new List<string>() { "100", "Close" }, "Prime");

// Динамическое обновление параметров индикатора
Aindicator bollinger = (Aindicator)tab.Indicators[0];
if (bollinger.ParametersDigit[0].Value != _bollingerLen.ValueInt) {
    bollinger.ParametersDigit[0].Value = _bollingerLen.ValueInt;
    bollinger.Save();
    bollinger.Reload();
}

// MarketDepthUpdateEvent для анализа стакана
_tabScreener.MarketDepthUpdateEvent += _tabScreener_MarketDepthUpdateEvent;
private void _tabScreener_MarketDepthUpdateEvent(MarketDepth marketDepth, BotTabSimple tab)
{
    MarketDepth md = marketDepth.GetCopy();
    decimal bestBidVolume = md.Bids[0].Bid.ToDecimal();
    
    // Проверка соотношения объёмов для обнаружения "плиты"
    for (int i = 1; i < md.Bids.Count; i++) {
        decimal curVolume = md.Bids[i].Bid.ToDecimal();
        decimal ratio = bestBidVolume / curVolume;
        if (ratio < _bestBidMinRatioToAll.ValueDecimal) {
            return;  // не плита
        }
    }
    
    tab.BuyAtLimit(volume, md.Bids[0].Price + tab.Security.PriceStep);
}

// Выход по стопу и профиту от высоты паттерна
decimal heightPattern = Math.Abs(tab.CandlesAll[tab.CandlesAll.Count - 4].Open - 
                                  tab.CandlesAll[tab.CandlesAll.Count - 2].Close);
decimal priceStop = _lastPrice - (heightPattern * _procHeightStop.ValueDecimal) / 100;
decimal priceTake = _lastPrice + (heightPattern * _procHeightTake.ValueDecimal) / 100;
tab.CloseAtStop(position, priceStop, slippage);
tab.CloseAtProfit(position, priceTake, slippage);

// Айсберг-ордера на скринере
tab.BuyAtIcebergMarket(GetVolume(tab), _icebergOrdersCount.ValueInt, 2000);
tab.CloseAtIcebergMarket(pos, pos.OpenVolume, _icebergOrdersCount.ValueInt, 2000);
```

**Изучать:** `SmaScreener.cs` — простейший скринер для понимания базовой структуры. `PinBarScreener.cs` — паттерн пин-бар с ограничением позиций. `PinBarVolatilityScreener.cs` — адаптация на инструмент с сохранением настроек. `LinearRegressionFastScreener.cs` — работа с тремя индикаторами и айсбергами. `PlateDetectorScreener.cs` — анализ стакана через `MarketDepthUpdateEvent`.

---

### 5.5 Продвинутое управление позициями (`Robots/PositionsMicromanagement/`)

Роботы, демонстрирующие сложные техники управления позицией: усреднение, пирамидинг, частичное закрытие, кастомные айсберги, работу с несколькими позициями.

#### 5.5.1 Частичное закрытие и многоуровневые тейки

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `UnsafeLimitsClosingSample` | Контртренд на Envelops | `CloseAtLimitUnsafe` — выставление двух лимитных ордеров на закрытие частями; пересчёт остатка по `executeCloseOrdersCount`; стоп и профит в процентах от входа |
| `CandlesTurnaroundPattern` | Разворот по свечам + ATR | Трёхэтапный выход (`CloseAtLimit` 1/3 → 1/3 → остаток); отслеживание `executeCloseOrdersCount` через `CloseOrders`; вход по `Body` свечи и ATR |
| `PriceChannelCounterTrend` | Контртренд на PriceChannel | **Несколько позиций одновременно**: открытие двух позиций с разными `SignalTypeOpen` ("First"/"Second"); разные уровни тейка для каждой позиции; проход по `PositionsOpenAll` в цикле |

#### 5.5.2 Усреднение (усреднение убытка)

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `UnsafeAveragePosition` | Контртренд на Envelops | `BuyAtLimitToPositionUnsafe` / `SellAtLimitToPositionUnsafe` — добавление к позиции на двух уровнях; отслеживание `executeOpenOrdersCount`; стоп и профит в процентах |
| `EnvelopsCountertrend` | Контртренд на Envelops | **Усреднение через StopMarket**: `BuyAtStopMarket` / `SellAtStopMarket` с `StopActivateType.LowerOrEqual`; усреднение на 2 уровнях; пересчёт средней цены входа `middleEntryPrice` по всем позициям; стоп и профит от средней цены |
| `AlligatorTrendAverage` | Тренд на Alligator | **Усреднение + пирамидинг + стандартный выход**: `BuyAtMarketToPosition` при откате (усреднение); `BuyAtMarketToPosition` при продолжении тренда (пирамидинг по `_pyramidPercent`); закрытие по противоположному сигналу Alligator; использование `Position.Comment` для маркировки операций |

#### 5.5.3 Мульти-позиционные стратегии

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `TwoEntrySample` | Тренд: PriceChannel + Envelops | **Две независимые позиции**: проверка `SignalTypeOpen` ("PriceChannel"/"Envelops"); открытие второй позиции только если первая от другого индикатора; каждая позиция закрывается по своему трейлинг-стопу |

#### 5.5.4 Кастомные айсберг-ордера

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `CustomIcebergSample` | Контртренд на Bollinger | **Собственный класс `IcebergMaker`** с `Thread`: разбиение объёма на N частей, округление через `Security.DecimalsVolume`, корректировка остатка на первый ордер; айсберг и на вход, и на выход; отличие поведения в тестере (`BuyAtMarket`) от реального торгов (`IcebergMaker.Start()`) |

**Ключевые паттерны управления позицией:**

```csharp
// Частичное закрытие: сколько ордеров уже исполнилось
int executed = 0;
for (int i = 0; position.CloseOrders != null && i < position.CloseOrders.Count; i++)
{
    if (position.CloseOrders[i].State == OrderStateType.Done)
        executed++;
}

// Пересчёт средней цены входа по нескольким позициям
decimal middlePrice = 0, allVolume = 0;
for (int i = 0; i < positions.Count; i++)
{
    middlePrice += positions[i].EntryPrice * positions[i].OpenVolume;
    allVolume += positions[i].OpenVolume;
}
middlePrice = middlePrice / allVolume;

// Усреднение: добавление к позиции
_tab.BuyAtLimitToPositionUnsafe(position, price, volume);

// Пирамидинг: докупка по тренду
_tab.BuyAtMarketToPosition(position, GetVolume(_tab));

// Маркировка операций через Comment
if (position.Comment.Contains("Average") == false)
{
    position.Comment += "Average";
    _tab.BuyAtMarketToPosition(position, volume);
}
```

**Изучать:** `UnsafeLimitsClosingSample.cs` — базовое частичное закрытие. `EnvelopsCountertrend.cs` — усреднение через StopMarket. `CustomIcebergSample.cs` — кастомный айсберг. `AlligatorTrendAverage.cs` — комбинация усреднения, пирамидинга и стандартного выхода.

---

### 5.6 Технические примеры (`Robots/TechSamples/`)

Примеры работы с API OsEngine, демонстрация технических возможностей платформы.

#### 5.6.1 Работа с индикаторами

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `CustomDataInIndicatorSample` | Запись собственных данных в индикатор | `EmptyIndicator`, `DataSeries[0].Values[index] = value`, `RePaint()` |
| `BlockIndicatorsSample` | Блокировка/разблокировка индикаторов | `Indicator.IsOn = bool`, `Reload()` через параметры `StrategyParameterBool` |
| `BlockIndicatorsOnScreenerSample` | Блокировка индикаторов на скринере | Доступ к индикаторам через `tab.Indicators[n]` в `CandleFinishedEvent`, `IsOn`, `Reload()` |

**Паттерн: Запись данных в EmptyIndicator**
```csharp
_indicatorEmpty = IndicatorsFactory.CreateIndicatorByName("EmptyIndicator", name, false);
_indicatorEmpty = (Aindicator)_tab.CreateCandleIndicator(_indicatorEmpty, "SecondArea");

private void _tab_CandleFinishedEvent(List<Candle> candles)
{
    decimal dataPoint = candles[candles.Count - 1].Close / 2;
    _indicatorEmpty.DataSeries[0].Values[_indicatorEmpty.DataSeries[0].Values.Count-1] = dataPoint;
    _indicatorEmpty.RePaint();
}
```

**Паттерн: Блокировка индикаторов**
```csharp
// В простом табе
Aindicator bollinger = (Aindicator)_tab.Indicators[0];
if (_bollingerIsOn.ValueBool != bollinger.IsOn) {
    bollinger.IsOn = _bollingerIsOn.ValueBool;
    bollinger.Reload();
}

// На скринере (в CandleFinishedEvent)
private void _screenerSource_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab) {
    Aindicator bollinger = (Aindicator)tab.Indicators[0];
    if (_bollingerIsOn.ValueBool != bollinger.IsOn) {
        bollinger.IsOn = _bollingerIsOn.ValueBool;
        bollinger.Reload();
    }
}
```

---

#### 5.6.2 Визуальные элементы на графике

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `ElementsOnChartSampleBot` | Рисование трёх типов элементов на чарте | `PointElement`, `LineHorisontal`, `Line` (наклонная), `SetChartElement()`, `DeleteChartElement()`, `DeleteAllChartElement()` |
| `TradeLineExample` | Торговля по наклонным уровням ZigZag | Построение наклонной линии через `Line`, расчёт шага `stepCorner = (high3 - high2) / (index3 - index2 + 1)`, `MarkerStyle.Star4` для точек |
| `FakeOutExample` | Ложный пробой + уровни | `LineHorisontal` для уровней, `PointElement` для экстремумов, локальные High/Low через цикл по свечам |

**Паттерн: Точка на графике**
```csharp
PointElement point = new PointElement("Some label", "Prime");
point.Y = candles[candles.Count - 2].Close;
point.TimePoint = candles[candles.Count - 2].TimeStart;
point.Label = "Some label";
point.Font = new Font("Arial", 10);
point.LabelTextColor = Color.White;
point.LabelBackColor = Color.Blue;
point.Color = Color.Red;
point.Style = MarkerStyle.Star4;
point.Size = 12;
_tab.SetChartElement(point);
```

**Паттерн: Горизонтальная линия**
```csharp
LineHorisontal line = new LineHorisontal("Some line", "Prime", false);
line.Value = candles[candles.Count - 1].Close;
line.TimeStart = candles[0].TimeStart;
line.TimeEnd = candles[candles.Count-1].TimeStart;
line.CanResize = true;
line.Color = Color.White;
line.LineWidth = 3;
line.Label = "Some label on Line";
line.Font = new Font("Arial", 10);
line.LabelTextColor = Color.White;
line.LabelBackColor = Color.Green;
_tab.SetChartElement(line);
```

**Паттерн: Наклонная линия**
```csharp
Line line = new Line("Inclined line", "Prime");
line.ValueYStart = candles[candles.Count - 11].Close;
line.TimeStart = candles[candles.Count - 11].TimeStart;
line.ValueYEnd = candles[candles.Count - 1].Close;
line.TimeEnd = candles[candles.Count - 1].TimeStart;
line.Color = Color.Bisque;
line.LineWidth = 3;
_tab.SetChartElement(line);
```

**Паттерн: Обновление линии в конце свечи**
```csharp
private void _tab_CandleFinishedEvent(List<Candle> candles)
{
    if (_lineOnPrimeChart != null) {
        _lineOnPrimeChart.TimeEnd = candles[candles.Count - 1].TimeStart;
        _lineOnPrimeChart.Refresh();
    }
}
```

---

#### 5.6.3 Кастомизация окна параметров

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `VisualSettingsParametersExample` | Цветовое оформление параметров | `ParamGuiSettings.SetForeColorParameter()`, `SetSelectionColorParameter()`, `SetBorderUnderParameter()`, `RePaintParameterTables()` |
| `CustomParamsUseBotSample` | Кастомная вкладка с таблицей | `ParamGuiSettings.CreateCustomTab()`, `WindowsFormsHost`, `DataGridView`, `DataGridFactory.GetDataGridView()` |
| `CustomTableInTheParamWindowSample` | Динамическая таблица на скринере | `DataGridView.CellValueChanged`, сохранение/загрузка строк таблицы в файл, `ComboBoxCell` для выбора Side |
| `CustomChartInParamWindowSample` | График в окне параметров | `Chart` (System.Windows.Forms.DataVisualization), `Series`, `ChartArea`, `TextAnnotation`, отдельный `Thread` для обновления |

**Паттерн: Цветовое оформление параметров**
```csharp
// Цвет текста параметра
this.ParamGuiSettings.SetForeColorParameter("VolumeLong", Color.Green);
this.ParamGuiSettings.SetForeColorParameter("VolumeShort", Color.DarkRed);

// Цвет выделения
this.ParamGuiSettings.SetSelectionColorParameter("VolumeLong", Color.LightGreen);

// Разделительная линия
this.ParamGuiSettings.SetBorderUnderParameter("Regime", Color.LightGray, 1);

// Перерисовка
this.ParamGuiSettings.RePaintParameterTables();
```

**Паттерн: Кастомная вкладка с таблицей**
```csharp
// Настройка окна параметров
this.ParamGuiSettings.Title = "Custom param gui sample";
this.ParamGuiSettings.Height = 800;
this.ParamGuiSettings.Width = 600;

// Создание вкладки
CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab("Indicators values");

// Создание таблицы
WindowsFormsHost _host = new WindowsFormsHost();
DataGridView grid = DataGridFactory.GetDataGridView(
    DataGridViewSelectionMode.FullRowSelect, 
    DataGridViewAutoSizeRowsMode.AllCells);

// Добавление колонок
DataGridViewColumn col = new DataGridViewColumn();
col.CellTemplate = new DataGridViewTextBoxCell();
col.HeaderText = "Time";
col.ReadOnly = true;
col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
grid.Columns.Add(col);

// Добавление на вкладку
customTab.AddChildren(_host);
```

**Паттерн: График в параметрах (отдельный поток)**
```csharp
// Создание графика
Chart _chart = new Chart();
ChartArea area = new ChartArea("ChartAreaSpread");
area.CursorX.IsUserSelectionEnabled = true;
area.CursorX.IsUserEnabled = true;
_chart.ChartAreas.Add(area);

// События зума и курсора
_chart.AxisScrollBarClicked += chart_AxisScrollBarClicked;
_chart.AxisViewChanged += chart_AxisViewChanged;
_chart.CursorPositionChanged += chart_CursorPositionChanged;
_chart.MouseClick += Chart_MouseClick;

// Отдельный поток для обновления
Thread worker = new Thread(StartPaintChart);
worker.Start();

private void StartPaintChart() {
    while (true) {
        Thread.Sleep(1000);
        LoadValueOnChart();  // добавление точек в Series
        SetSeries(lineSeries);  // отрисовка
    }
}
```

**Паттерн: Annotation на графике**
```csharp
TextAnnotation _annotation = new TextAnnotation {
    Text = $"{xValue}: {yValue}",
    X = 0, Y = -1,
    AnchorX = xValue,
    AnchorY = yValue,
    Font = new Font("Arial", 12, FontStyle.Bold),
    ForeColor = Color.Gray,
    BackColor = Color.Gray,
    LineColor = Color.Gray,
    AnchorAlignment = ContentAlignment.MiddleCenter
};
_chart.Annotations.Add(_annotation);
```

---

#### 5.6.4 Работа с ордерами и позициями

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `ChangePriceBotExtStopMarket` | Изменение цены лимитного ордера | `ChangeOrderPrice(order, newPrice)`, `ManualPositionSupport.DisableManualSupport()`, `MarketDepthUpdateEvent`, пересчёт цены по `%` от BestBid/BestAsk |

**Паттерн: Изменение цены ордера**
```csharp
_tab.MarketDepthUpdateEvent += _tab_MarketDepthUpdateEvent;
_tab.ManualPositionSupport.DisableManualSupport();

private void _tab_MarketDepthUpdateEvent(MarketDepth md)
{
    Position pos = _tab.PositionsOpenAll[0];
    
    if (pos.State == PositionStateType.Opening) {
        // Изменение цены ордера каждые N секунд
        if (_lastChangeOrderTime.AddSeconds(_seconds) > DateTime.Now) return;
        
        decimal newPrice = _tab.PriceBestBid - _tab.PriceBestBid * (_slippagePercent / 100);
        newPrice = Math.Round(newPrice, _tab.Security.Decimals);
        
        _tab.ChangeOrderPrice(pos.OpenOrders[0], newPrice);
        _lastChangeOrderTime = DateTime.Now;
    }
}
```

---

#### 5.6.5 События и логирование

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `CandlesLoggingSample` | Базовое логирование | `SendNewLogMessage(message, LogMessageType.User/Error)` |
| `StopByTradeFeedSample` | Стоп по ленте сделок | `NewTickEvent(Trade trade)`, трейлинг-стоп по цене сделки, `CloseAtTrailingStop(pos, stopPrice, orderPrice)` |

**Паттерн: Логирование**
```csharp
// Только в окно логов
SendNewLogMessage(message, Logging.LogMessageType.User);

// Как ошибка
SendNewLogMessage(message, Logging.LogMessageType.Error);
```

**Паттерн: Стоп по ленте (NewTickEvent)**
```csharp
_tab.NewTickEvent += _tab_NewTickEvent;

private void _tab_NewTickEvent(Trade trade)
{
    Position myPos = _tab.PositionsOpenAll[0];
    
    if (myPos.Direction == Side.Buy) {
        stopPrice = trade.Price - (trade.Price * (_trailStopPercent / 100));
        orderPrice = stopPrice - _slippage * _tab.Security.PriceStep;
    } else {
        stopPrice = trade.Price + (trade.Price * (_trailStopPercent / 100));
        orderPrice = stopPrice + _slippage * _tab.Security.PriceStep;
    }
    
    _tab.CloseAtTrailingStop(myPos, stopPrice, orderPrice);
}
```

---

#### 5.6.6 Работа с открытым интересом

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `OpenInterestBotSample` | Торговля по изменению Open Interest | `Candle.OpenInterest`, сравнение текущего и предыдущего OI, вход при падении OI на заданную величину |

**Паттерн: Анализ Open Interest**
```csharp
private void LogicEntry(List<Candle> candles)
{
    Candle currentCandle = candles[^1];
    Candle prevCandle = candles[^2];
    
    if (currentCandle.OpenInterest == 0 || prevCandle.OpenInterest == 0) return;
    
    decimal currentOi = currentCandle.OpenInterest;
    decimal prevOi = prevCandle.OpenInterest;
    
    // Вход если OI упал
    if (currentOi < prevOi) {
        decimal oiDownSize = prevOi - currentOi;
        if (oiDownSize > _oiDownsizeToEntry.ValueDecimal) {
            _tab.BuyAtMarket(GetVolume(_tab));
        }
    }
}
```

---

#### 5.6.7 Инициализация всех типов табов

| Робот | Что демонстрирует | Ключевые API |
|-------|-------------------|--------------|
| `AllSourcesInOneSample` | Создание всех 7 типов табов | `TabCreate(BotTabType.Simple/Index/Pair/Screener/Polygon/Cluster/News)` |

**Паттерн: Создание всех типов источников**
```csharp
TabCreate(BotTabType.Simple);
TabCreate(BotTabType.Index);
TabCreate(BotTabType.Pair);
TabCreate(BotTabType.Screener);
TabCreate(BotTabType.Polygon);
TabCreate(BotTabType.Cluster);
TabCreate(BotTabType.News);

// Доступ через массивы:
// TabsSimple[0], TabsIndex[0], TabsPair[0], TabsScreener[0],
// TabsPolygon[0], TabsCluster[0], TabsNews[0]
```

---

**Изучать:** `ElementsOnChartSampleBot.cs` — все типы элементов на графике. `CustomParamsUseBotSample.cs` — кастомная таблица в параметрах. `CustomChartInParamWindowSample.cs` — график в реальном времени. `StopByTradeFeedSample.cs` — работа с NewTickEvent. `ChangePriceBotExtStopMarket.cs` — изменение цены ордера.

---

### 5.7 Парный арбитраж (`Robots/PairArbitrage/`)

Торговля расхождением двух коррелирующих инструментов. Используется `BotTabPair` — специальный тип таба для парного трейдинга, который автоматически рассчитывает корреляцию и отклонения между инструментами.

#### 5.7.1 Стратегии парного арбитража

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `PairCorrelationTrader` | Конвергенция при высокой корреляции | **Базовый парный арбитраж**: вход при корреляции > 0.9; использование события `CorrelationChangeEvent`; проверка `pair.SideCointegrationValue` (Up/Down); методы `SellSec1BuySec2()` / `BuySec1SellSec2()`; выход при инверсии стороны коинтеграции (`pair.LastEntryCointegrationSide`); ограничение `_pairTrader.PairsWithPositionsCount`; `pair.ClosePositions()` для закрытия всех позиций пары |
| `PairCorrelationNegative` | Торговля в тренд при отрицательной корреляции | **Отрицательная корреляция**: вход при корреляции < -0.8 (параметр `_maxCorrelationToEntry`); выход при корреляции > 0.8 (`_minCorrelationToExit`); логика для антикоррелирующих инструментов; проверка `pair.CorrelationLast`; использование тех же методов входа/выхода что и `PairCorrelationTrader` |
| `PairCointegrationSideTrader` | Пробой каналов коинтеграции | **Событие CointegrationPositionSideChangeEvent**: реакция на пробой верхней/нижней границы канала; вход при смене стороны (`pair.SideCointegrationValue != pair.LastEntryCointegrationSide`); закрытие предыдущей позиции перед открытием новой; торговля на возврат к среднему (mean reversion); методы `SellSec1BuySec2()` при пробое вверх, `BuySec1SellSec2()` при пробое вниз |

**Ключевые понятия парного арбитража:**

```csharp
// Создание Pair-таба
TabCreate(BotTabType.Pair);
BotTabPair _pairTrader = TabsPair[0];

// Подписка на событие изменения корреляции
_pairTrader.CorrelationChangeEvent += _pairTrader_CorrelationChangeEvent;

// Подписка на событие изменения стороны коинтеграции
_pairTrader.CointegrationPositionSideChangeEvent += _pairTrader_CointegrationPositionSideChangeEvent;

// Обработчик события корреляции
private void _pairTrader_CorrelationChangeEvent(
    List<PairIndicatorValue> correlation, 
    PairToTrade pair)
{
    if (_regime.ValueString == "Off") return;
    
    if (pair.HavePositions) {
        ClosePositionLogic(pair);
    } else {
        OpenPositionLogic(pair);
    }
}

// Проверка корреляции перед входом
if (pair.CorrelationLast < 0.9m) {
    return;  // корреляция недостаточно высокая
}

// Ограничение на количество пар с позициями
if (_pairTrader.PairsWithPositionsCount >= _maxPositionsCount.ValueInt) {
    return;
}

// Вход: продажа первого инструмента, покупка второго
if (pair.SideCointegrationValue == CointegrationLineSide.Up) {
    pair.SellSec1BuySec2();
}
// Вход: покупка первого инструмента, продажа второго
else if (pair.SideCointegrationValue == CointegrationLineSide.Down) {
    pair.BuySec1SellSec2();
}

// Выход: закрытие всех позиций пары
pair.ClosePositions();

// Проверка стороны коинтеграции для выхода
if (pair.SideCointegrationValue == CointegrationLineSide.Up 
    && pair.LastEntryCointegrationSide == CointegrationLineSide.Down) {
    pair.ClosePositions();  // инверсия стороны - выход
}

// Для отрицательной корреляции: вход при корреляции < -0.8
if (pair.CorrelationLast > _maxCorrelationToEntry.ValueDecimal) {
    return;  // корреляция слишком высокая (близка к положительной)
}

// Выход при росте корреляции выше 0.8
if (pair.CorrelationLast > _minCorrelationToExit.ValueDecimal) {
    pair.ClosePositions();
}

// Проверка: есть ли уже позиции по этой паре
if (pair.HavePositions) {
    // позиции есть
}

// Проверка: сменилась ли сторона коинтеграции
if (pair.SideCointegrationValue != pair.LastEntryCointegrationSide) {
    // сторона сменилась - можно открывать новую позицию
}
```

**Типы событий в PairTab:**

| Событие | Когда срабатывает | Что передаёт |
|---------|-------------------|--------------|
| `CorrelationChangeEvent` | При изменении корреляции между инструментами | `List<PairIndicatorValue> correlation`, `PairToTrade pair` |
| `CointegrationPositionSideChangeEvent` | При пробое верхней/нижней границы канала коинтеграции | `CointegrationLineSide side`, `PairToTrade pair` |

**Свойства PairToTrade:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| `CorrelationLast` | `decimal` | Последнее значение корреляции (-1 до 1) |
| `SideCointegrationValue` | `CointegrationLineSide` | Текущая сторона коинтеграции (Up/Down/None) |
| `LastEntryCointegrationSide` | `CointegrationLineSide` | Сторона коинтеграции на момент последнего входа |
| `HavePositions` | `bool` | Есть ли открытые позиции по этой паре |
| `SecurityName1`, `SecurityName2` | `string` | Имена инструментов в паре |

**Методы для торговли парой:**

| Метод | Описание |
|-------|----------|
| `pair.BuySec1SellSec2()` | Купить первый инструмент, продать второй (ставка на сближение если первый дешевле) |
| `pair.SellSec1BuySec2()` | Продать первый инструмент, купить второй (ставка на сближение если первый дороже) |
| `pair.ClosePositions()` | Закрыть все позиции по паре |

**Стратегии по типу корреляции:**

| Тип корреляции | Значение | Стратегия |
|----------------|----------|-----------|
| Положительная | > 0.7 | Инструменты движутся в одном направлении. Вход при расхождении, выход при сближении |
| Отрицательная | < -0.7 | Инструменты движутся в противоположных направлениях. Вход при сближении, выход при расхождении |
| Околонулевая | -0.3 до 0.3 | Нет статистической связи. Парный арбитраж не рекомендуется |

**Изучать:** `PairCorrelationTrader.cs` — базовая стратегия с положительной корреляцией. `PairCorrelationNegative.cs` — торговля антикоррелирующих инструментов. `PairCointegrationSideTrader.cs` — торговля изменения графика минимальных остатков от разницы, между двумя ценовыми рядами с оптимальным мультипликатором.

---

### 5.8 Сеточные стратегии (`Robots/Grids/`)

Роботы, выставляющие сетку лимитных ордеров. Управление через `_tab.GridsMaster`.

| Робот | Сигнал на вход | Тип сетки | Особенности |
|-------|---------------|-----------|-------------|
| `GridBollinger` | Пробой Bollinger | Открытие позиции | Односторонняя, трейлинг-стоп на всю сетку |
| `GridBollingerScreener` | Пробой Bollinger | Открытие позиции | Скринер-версия GridBollinger |
| `GridLinearRegression` | Пробой канала линейной регрессии | Открытие позиции | Трейлинг-стоп в процентах |
| `GridTwoSides` | Падение ATR | Маркет-мейкинг | Две сетки одновременно: BUY + SELL |
| `GridTwoSignals` | Пробой PriceChannel + возврат | Маркет-мейкинг | Две последовательные сетки, TrailingUp/Down |
| `GridPair` | Коинтеграция (2 инструмента) | Маркет-мейкинг | Парный арбитраж через скринер |
| `GridScreenerAdaptiveSoldiers` | Свечные паттерны | Маркет-мейкинг | Адаптивные параметры по волатильности |
| `GridVolumeBollingerRankingScreener` | Объём + Bollinger | Маркет-мейкинг | Ранжирование инструментов по объёму |

**Ключевые понятия сеток:**

```csharp
// Создание сетки
TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();

// Тип сетки
grid.GridType = TradeGridPrimeType.OpenPosition;   // Накопление позиции
grid.GridType = TradeGridPrimeType.MarketMaking;   // Маркет-мейкинг

// Параметры линий
grid.GridCreator.FirstPrice = lastPrice;           // Цена первой линии
grid.GridCreator.LineCountStart = 10;              // Количество линий
grid.GridCreator.LineStep = 0.05m;                 // Шаг между линиями
grid.GridCreator.TypeStep = TradeGridValueType.Percent; // Шаг в %
grid.GridCreator.ProfitStep = 0.05m;               // Тейк-профит каждой линии
grid.GridCreator.GridSide = Side.Buy;              // Направление

// Стоп-лосс на всю сетку
grid.StopAndProfit.TrailStopRegime = OnOffRegime.On;
grid.StopAndProfit.TrailStopValue = 1.5m;

// Перемещение сетки (Trailing)
grid.TrailingUp.TrailingUpIsOn = true;
grid.TrailingUp.TrailingUpStep = _tab.Security.PriceStep * 20;

// Ограничения на сетку
grid.StopBy.StopGridByLifeTimeIsOn = true;         // По времени
grid.StopBy.StopGridByPositionsCountIsOn = true;   // По количеству сделок

// Активация
grid.Regime = TradeGridRegime.On;
```

**Изучать:** `GridBollinger.cs` — базовая сетка с трейлинг-стопом. `GridTwoSides.cs` — работа с двумя сетками.

---

### 5.9 Учебные роботы (`Robots/BotsFromStartLessons/`)

Пошаговый курс «C# для алготрейдера». Каждый робот демонстрирует одну конкретную тему. Рекомендуется изучать по порядку.

#### 5.9.1 Базовые концепции

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson1HelloWorld` | Кнопка в параметрах | `CreateParameterButton`, обработчик `UserClickOnButtonEvent`, `SendNewLogMessage` |
| `Lesson2Bot1` | Типы данных C# | Работа со строками, int, decimal, bool, DateTime через кнопки и лог |
| `Lesson2Bot2` | Параметры робота | `StrategyParameterString`, `Int`, `Bool`, `Decimal`, `TimeOfDay` — как создавать |

#### 5.9.2 Первая торговля

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson3Bot1` | Событие свечи | `CandleFinishedEvent`, `IsUp`, `BuyAtMarket`, `CloseAtTrailingStopMarket` |
| `Lesson3Bot2` | Индикатор + лимит | Создание SMA, `BuyAtLimit`, `CloseAtMarket`, проверка `Position.State` |
| `Lesson3Bot3` | Два индикатора | Две SMA (fast/slow), `ParametrsChangeByUser`, `Reload()`/`Save()` индикаторов |

#### 5.9.3 Продвинутая архитектура

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson4Bot1` | Все события таба | `CandleFinished`, `CandleUpdate`, `OrderUpdate`, `MarketDepthUpdate`, `PositionOpeningSucces`, `NewTick` + `GetVolume` |
| `Lesson5Bot1` | Время в позиции | `Position.TimeOpen`, `AddMinutes()`, двунаправленная торговля (Long/Short) |
| `Lesson5Bot2` | Три индикатора | Alligator + PriceChannel + AO, докупка `BuyAtMarketToPosition` |
| `Lesson6Bot1` | Пирамидинг | 3 последовательных входа `BuyAtStop`, расчёт цены через ATR, `CloseAtTrailingStop` на все позиции |
| `Lesson7Bot1` | Адаптивность | Ручной расчёт SMA, волатильность за N дней, адаптивные параметры, время торговли `TimeOfDay` |

#### 5.9.4 Стакан и потоки

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson8Bot1` | Стакан + поток | `Thread` в конструкторе, `MarketDepth`, `Bids[0].Bid`, `CloseAtStopMarket` + `CloseAtProfitMarket` |
| `Lesson8Bot2` | Ручные экстремумы | Самостоятельный расчёт High/Low за период, `BuyAtStop`, `CloseAtTrailingStopMarket` |

#### 5.9.5 Справочник ордеров (Lesson 9)

Эти роботы не торгуют сами — они демонстрируют **все методы API** через кнопки.

| Робот | Тема | Что почерпнуть |
|-------|------|----------------|
| `Lesson9Bot1` | Входы в позицию | `BuyAtMarket`, `BuyAtLimit`, `BuyAtIceberg`, `BuyAtFake`, `BuyAtStop` + Sell-аналоги |
| `Lesson9Bot2` | Добавление к позиции | `BuyAtLimitToPosition`, `BuyAtLimitToPositionUnsafe`, `BuyAtMarketToPosition`, `BuyAtIcebergToPosition` |
| `Lesson9Bot3` | Закрытие позиции | `CloseAllAtMarket`, `CloseAtMarket`, `CloseAtLimit`, `CloseAtLimitUnsafe`, `CloseAtIceberg`, `CloseAtFake` |
| `Lesson9Bot4` | Стопы и профиты | `CloseAtStop`, `CloseAtTrailingStop`, `CloseAtProfit` (Limit и Market) + отмена |
| `Lesson9Bot5` | Управление ордерами | `CloseAllOrderToPosition`, `CloseAllOrderInSystem`, `CloseOrder`, `ChangeOrderPrice` |

**Изучать:** По порядку от Lesson1 к Lesson9. Lesson9Bot1-5 — как интерактивный справочник API.

---

### 5.10 Перекладывание экспирируемых фьючерсов (`Robots/FuturesTrend/`)

Торговля фьючерсами с автоматическим перекладыванием из одной серии в другую при приближении экспирации. Используется `BotTabScreener` для работы с несколькими сериями фьючерса одновременно.

#### 5.10.1 Пробой адаптивного канала

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `FuturesTrendPriceChannel` | Пробой PriceChannelAdaptive | **Автоматический выбор серии фьючерса**: метод `GetFuturesToTrade()` с приоритетом позиции; проверка `Security.Expiration`; правило 3 дней до экспирации для выхода; правило 3-100 дней для входа; `BuyAtIcebergMarket()` / `CloseAtIcebergMarket()`; индикатор `PriceChannelAdaptive` с параметрами `_pcAdxLength` и `_pcRatio`; обновление индикатора через `_futuresSource.UpdateIndicatorsParameters()`; `NonTradePeriods` для ограничения торгового времени; кнопка `_tradePeriodsShowDialogButton` для настройки периодов |

#### 5.10.2 Пробой Bollinger Bands

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `FuturesTrendBollinger` | Пробой Bollinger | **Двунаправленная торговля**: вход в лонг при `price > BollingerUpper`, вход в шорт при `price < BollingerLower`; режимы `_regime` ("Off"/"On"/"OnlyLong"/"OnlyShort"); выход при пробое противоположной границы; проверка `daysByExpiration < 3` для принудительного выхода; параметры `_bollingerLength` и `_bollingerDeviation`; айсберг-ордера на вход и выход; та же логика выбора фьючерса что и в `FuturesTrendPriceChannel` |

**Ключевые понятия перекладывания фьючерсов:**

```csharp
// Создание скринера для фьючерсов
BotTabScreener _futuresSource = TabCreate<BotTabScreener>();
_futuresSource.CandleFinishedEvent += _futs1_CandleFinishedEvent;

// Создание индикатора на скринере
_futuresSource.CreateCandleIndicator(2, "PriceChannelAdaptive", 
    new List<string>() { _pcAdxLength.ValueInt.ToString(), _pcRatio.ValueInt.ToString() }, 
    "Prime");

// Обновление параметров индикатора
ParametrsChangeByUser += FuturesStartContangoScreener_ParametrsChangeByUser;
private void FuturesStartContangoScreener_ParametrsChangeByUser()
{
    _futuresSource._indicators[0].Parameters = 
        new List<string>() { _pcAdxLength.ValueInt.ToString(), _pcRatio.ValueInt.ToString() };
    _futuresSource.UpdateIndicatorsParameters();
}

// Выбор фьючерса для торговли
private BotTabSimple GetFuturesToTrade(BotTabScreener futures, DateTime currentTime)
{
    // 1. Приоритет: фьючерс с открытой позицией
    for (int i = 0; i < futures.Tabs.Count; i++) {
        BotTabSimple currentFutures = futures.Tabs[i];
        if (currentFutures.PositionsOpenAll.Count != 0) {
            return currentFutures;  // торгуем по позиции
        }
    }

    // 2. Выбор ближайшей серии с учётом экспирации
    BotTabSimple selectedFutures = null;
    
    for (int i = 0; i < futures.Tabs.Count; i++) {
        Security sec = futures.Tabs[i].Security;
        
        if (sec == null || sec.Expiration == DateTime.MinValue) continue;
        
        double daysByExpiration = (sec.Expiration - currentTime).TotalDays;
        
        // Пропускаем если меньше 3 дней или больше 100 дней до экспирации
        if (daysByExpiration < 3 || daysByExpiration > 100) continue;
        
        // Выбираем ближайшую экспирацию
        if (selectedFutures != null && selectedFutures.Security.Expiration < sec.Expiration) {
            continue;
        }
        
        selectedFutures = futures.Tabs[i];
    }
    
    return selectedFutures;
}

**Алгоритм выбора фьючерса:**

```
1. Есть ли открытая позиция?
   └─ ДА → Торгуем по этой серии (независимо от экспирации)
   └─ НЕТ → Переходим к шагу 2

2. Поиск ближайшей серии:
   └─ Expiration < 3 дней → Пропускаем (слишком близко к экспирации)
   └─ Expiration > 100 дней → Пропускаем (слишком далеко)
   └─ Иначе → Выбираем серию с ближайшей экспирацией

3. Возвращаем выбранный фьючерс или null
```

**Правила экспирации:**

| Ситуация | Дней до экспирации | Действие |
|----------|-------------------|----------|
| Вход в позицию | 3-100 дней | Разрешён |
| Вход в позицию | < 3 дней | Запрещён |
| Вход в позицию | > 100 дней | Запрещён |
| Выход из позиции | < 3 дней | Принудительный выход |
| Выход из позиции | По сигналу индикатора | Выход по стратегии |
| Позиция открыта | Любое | Держим до сигнала или экспирации |

**Преимущества такого подхода:**

| Преимущество | Реализация |
|--------------|------------|
| Непрерывность торговли | Автоматический переход на следующую серию |
| Избегание экспирации | Выход за 3 дня до экспирации |
| Приоритет позиции | Если позиция есть — не переключаемся |
| Работа в тестере | Защита от пропуска серий (лимит 100 дней) |

```

**Изучать:** `FuturesTrendPriceChannel.cs` — базовая логика перекладывания с PriceChannel. `FuturesTrendBollinger.cs` — двунаправленная торговля с Bollinger. Оба робота демонстрируют ключевой паттерн `GetFuturesToTrade()` для автоматического выбора серии фьючерса.

---

### 5.11 Ротация фьючерсов по раздвижке к базе (`Robots/FuturesStart/`)

Торговля фьючерсами с ротацией по размеру премии/дисконта к базовому активу (споту). Используется архитектура "пара спот/фьючерс" с фильтром по стадии контанго/бэквордации. Скринер фьючерсов ранжирует инструменты по проценту отклонения и позволяет торговать только определённые стадии.

#### 5.11.1 Пробой Bollinger с фильтром контанго

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `FuturesStart1Bollinger` | Пробой Bollinger Bands + ранкинг раздвижек | **Архитектура 10 пар спот/фьючерс**: 10 табов `BotTabSimple` для спота + 10 табов `BotTabScreener` для фьючерсов; расчёт контанго в %: `contangoPercent = (futuresPrice - spotPrice) / spotPrice * 100`; ранжирование `_contangoValues.OrderBy(x => x.ContangoPercent)`; стадии 1 (мин. контанго — лонг) и 2 (макс. контанго — шорт); фильтр `_contangoStageToTradeLong/Short`; авто-режим для MOEX (`On_MOEXStocksAuto`) с коэффициентами для разных бумаг; ручной режим (`On_Manual`) с индивидуальными коэффициентами на инструмент; кнопка `Show contango` для отладки; `SetTSecurities()` для авто-настройки инструментов через T-Invest API; проверка `SecurityType.Futures` и `SecurityType.Stock`; фильтрация по префиксам серий (SRH/SRM/SRZ/SRU для SBER и т.д.); `CanTradeThisSecurity()` для включения/выключения инструментов; `_entryLogicByBaseSecurityInReal` для защиты от повторного входа в течение минуты |

#### 5.11.2 Пробой Keltner Channel с фильтром контанго

| Робот | Стратегия | Что почерпнуть |
|-------|-----------|----------------|
| `FuturesStart2Keltner` | Пробой Keltner Channel + ранкинг раздвижек | **Индикатор KeltnerChannel**: параметры `_keltnerEmaLength`, `_keltnerAtrLength`, `_keltnerDeviation`; вход по пробою верхней линии (`DataSeries[1]`) для лонга, нижней (`DataSeries[2]`) для шорта; та же архитектура контанго-фильтра что и `FuturesStart1Bollinger`; те же 10 пар спот/фьючерс; те же стадии торговли; отличие только в индикаторе (Keltner вместо Bollinger); `CreateIndicators()` с передачей 5 параметров в индикатор |

**Ключевые понятия ротации фьючерсов:**

```csharp
// Архитектура: 10 пар спот/фьючерс
BotTabSimple _base1, _base2, ..., _base10;          // спот (базовый актив)
BotTabScreener _futs1, _futs2, ..., _futs10;        // фьючерсы (серии)

// Создание пар
_base1 = TabCreate<BotTabSimple>();
_base1.CandleFinishedEvent += _base1_CandleFinishedEvent;
_futs1 = TabCreate<BotTabScreener>();
_futs1.CandleFinishedEvent += _futs1_CandleFinishedEvent;
CreateIndicators(_base1, _futs1);

// Расчёт контанго в процентах
private void SetContangoValues(BotTabSimple baseSource, BotTabSimple futuresSource)
{
    decimal coeff = 1;  // коэффициент для нормализации цен (зависит от количества знаков)
    
    // Авто-режим для MOEX
    if (_contangoFilterRegime.ValueString == "On_MOEXStocksAuto") {
        if (baseSource.Security.Name.Contains("VTB")) {
            coeff = 20;  // VTB имеет специфичный масштаб цен
        } else if (baseSource.Security.Name.Contains("GMKN")) {
            coeff = 100; // GMKN тоже специфичный
        } else {
            // Для остальных бумаг: 10^decimals
            for (int i = 0; i < baseSource.Security.Decimals; i++) {
                coeff = coeff * 10;
            }
        }
    }
    // Ручной режим: индивидуальные коэффициенты
    else if (_contangoFilterRegime.ValueString == "On_Manual") {
        coeff = _contangoCoefficient1.ValueDecimal;  // для первого инструмента
        // ... для остальных
    }
    
    // Расчёт абсолютной разницы
    decimal contangoAbs = (futuresSource.PriceBestBid / coeff) - baseSource.PriceBestAsk;
    
    // Расчёт в процентах от спота
    decimal contangoPercent = contangoAbs / (baseSource.PriceBestAsk / 100);
    
    // Сохранение значения
    value.ContangoPercent = contangoPercent;
    
    // Сортировка по возрастанию контанго
    _contangoValues = _contangoValues.OrderBy(x => x.ContangoPercent).ToList();
}

// Определение стадии контанго
private int GetContangoStage(string secName)
{
    for (int i = 0; i < _contangoValues.Count; i++) {
        if (_contangoValues[i].SecurityName == secName) {
            // Стадия 1: самые низкие значения контанго (дисконт) — для лонга
            if (i <= _contangoFilterCountSecurities.ValueInt) {
                return 1;
            }
            // Стадия 2: самые высокие значения контанго (премия) — для шорта
            else if (i >= _contangoValues.Count - _contangoFilterCountSecurities.ValueInt) {
                return 2;
            }
            // Стадия 0: средние значения — не торгуем
            else {
                return 0;
            }
        }
    }
    return 0;
}

// Проверка стадии перед входом
if (_contangoFilterRegime.ValueString != "Off") {
    int stageContango = GetContangoStage(futuresSource.Security.Name);
    
    // Для лонга: только стадия 1 (дисконт)
    if (_regime.ValueString != "OnlyShort" && stageContango != _contangoStageToTradeLong.ValueInt) {
        return;
    }
    
    // Для шорта: только стадия 2 (премия)
    if (_regime.ValueString != "OnlyLong" && stageContango != _contangoStageToTradeShort.ValueInt) {
        return;
    }
}

// Кнопка для отображения текущих значений контанго
StrategyParameterButton buttonShowContango = CreateParameterButton("Show contango", "Contango");
buttonShowContango.UserClickOnButtonEvent += ButtonShowContango_UserClickOnButtonEvent;

private void ButtonShowContango_UserClickOnButtonEvent()
{
    string message = "";
    for (int i = 0; i < _contangoValues.Count; i++) {
        message += _contangoValues[i].SecurityName
                 + " Value%: " + Math.Round(_contangoValues[i].ContangoPercent, 3)
                 + "\n";
    }
    SendNewLogMessage(message, Logging.LogMessageType.Error);
}

// Выбор фьючерса для торговли (с учётом экспирации)
private BotTabSimple GetFuturesToTrade(BotTabSimple baseSource, BotTabScreener futures, DateTime currentTime)
{
    // 1. Приоритет: фьючерс с открытой позицией
    for (int i = 0; i < futures.Tabs.Count; i++) {
        if (futures.Tabs[i].PositionsOpenAll.Count != 0) {
            return futures.Tabs[i];
        }
    }

    // 2. Выбор ближайшей серии (3-100 дней до экспирации)
    BotTabSimple selectedFutures = null;
    for (int i = 0; i < futures.Tabs.Count; i++) {
        Security sec = futures.Tabs[i].Security;
        double daysByExpiration = (sec.Expiration - currentTime).TotalDays;
        
        if (daysByExpiration < 3 || daysByExpiration > 100) continue;
        
        if (selectedFutures == null || sec.Expiration < selectedFutures.Security.Expiration) {
            selectedFutures = futures.Tabs[i];
        }
    }
    return selectedFutures;
}

// Защита от повторного входа в течение минуты
private List<LastTradeTimeValue> _entryLogicByBaseSecurityInReal = new List<LastTradeTimeValue>();

private void SetLastLogicEntryTime(string securityBase, DateTime time)
{
    LastTradeTimeValue existing = _entryLogicByBaseSecurityInReal
        .Find(x => x.SecurityName == securityBase);
    
    if (existing != null) {
        existing.Time = time;
    } else {
        _entryLogicByBaseSecurityInReal.Add(new LastTradeTimeValue {
            SecurityName = securityBase,
            Time = time
        });
    }
}

// Проверка в логике
if (this.StartProgram == StartProgram.IsOsTrader) {
    DateTime lastPairTradeTime = GetLastEntryLogicTime(baseSource.Security.Name);
    if (lastPairTradeTime.AddMinutes(1) > DateTime.Now) {
        return;  // уже входили в логику за последнюю минуту
    }
    SetLastLogicEntryTime(baseSource.Security.Name, DateTime.Now);
}

// Включение/выключение отдельных инструментов
_tradeRegimeSecurity1 = CreateParameter("Trade security 1", true, "Trade securities");
// ... для 10 инструментов

private bool CanTradeThisSecurity(string securityName)
{
    if (this.TabsSimple[0].Security.Name == securityName) {
        return _tradeRegimeSecurity1.ValueBool;
    }
    // ... для остальных
    return false;
}
```

**Классы данных:**

```csharp
// Значение контанго для инструмента
public class ContangoValue
{
    public string SecurityName;      // имя фьючерса
    public decimal ContangoPercent;  // процент отклонения от спота
    public DateTime LastTimeUpdate;  // время последнего обновления
}

// Время последнего входа в логику
public class LastTradeTimeValue
{
    public string SecurityName;  // имя базового актива
    public DateTime Time;        // время последнего входа
}
```

**Стадии контанго:**

| Стадия | Описание | Когда торговать | Пример |
|--------|-----------|-----------------|--------|
| 1 | Минимальное контанго (дисконт) | Лонг | Фьючерс торгуется с дисконтом к споту |
| 2 | Максимальное контанго (премия) | Шорт | Фьючерс торгуется с премией к споту |
| 0 | Средние значения | Не торговать | Нейтральная зона |

**Режимы фильтра контанго:**

| Режим | Описание | Когда использовать |
|-------|----------|-------------------|
| `Off` | Фильтр отключён | Тестирование без фильтра |
| `On_MOEXStocksAuto` | Авто-коэффициенты для MOEX | Торговля фьючерсами на акции РФ |
| `On_Manual` | Ручные коэффициенты | Кастомные инструменты |

**Коэффициенты для MOEX (авто-режим):**

| Инструмент | Коэффициент | Примечание |
|------------|-------------|------------|
| SBER, GAZP, ROSN, LKOH | 10^decimals | Стандартные бумаги |
| VTB | 20 (до 2024.07), 100 (после) | Специфичный масштаб |
| GMKN | 100 (до 2024.04), 10 (после) | Сплит акций |
| MGNT | 10^decimals | Стандарт |

**Изучать:** `FuturesStart1Bollinger.cs` — базовая логика с Bollinger и полной реализацией контанго-фильтра. `FuturesStart2Keltner.cs` — альтернатива с Keltner Channel. Оба робота демонстрируют ключевые паттерны: расчёт контанго, ранжирование, стадии торговли, авто-настройку инструментов.

---

## 6. Индикаторы

### 6.1 `Aindicator` и его окружение

Все индикаторы OsEngine наследуются от абстрактного класса `Aindicator` (`OsEngine/Indicators/Aindicator.cs`). Скриптовые индикаторы (в `Custom/Indicators/Scripts/`) компилируются через Roslyn и автоматически подхватываются фабрикой `IndicatorsFactory`.

**Минимальная структура индикатора:**

```csharp
using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Indicators.Samples
{
    [Indicator("MyIndicatorName")]  // имя, по которому индикатор вызывается из робота
    public class MyIndicator : Aindicator
    {
        // 1. Объявление параметров и серий
        public IndicatorParameterInt _length;
        public IndicatorDataSeries _series;

        // 2. Конфигурация (вместо конструктора)
        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);
                _series = CreateSeries("MyLine", System.Drawing.Color.Red,
                                       IndicatorChartPaintType.Line, true);
            }
        }

        // 3. Логика расчёта
        public override void OnProcess(List<Candle> source, int index)
        {
            // source — все свечи, index — текущий индекс
            if (index < _length.ValueInt) {
                _series.Values[index] = 0;
                return;
            }

            decimal sum = 0;
            for (int i = index; i > index - _length.ValueInt; i--)
            {
                sum += source[i].Close;
            }
            _series.Values[index] = sum / _length.ValueInt;
        }
    }
}
```

**Ключевые компоненты `Aindicator`:**

| Компонент | Метод / Свойство | Описание |
|-----------|------------------|----------|
| **Параметры** | `CreateParameterInt()`, `CreateParameterDecimal()`, `CreateParameterBool()`, `CreateParameterString()`, `CreateParameterStringCollection()` | Пользовательские настройки индикатора, доступные в UI |
| **Серии данных** | `CreateSeries(name, color, paintType, isPaint)` | Выходные данные индикатора (линии, гистограммы, точки) |
| **Жизненный цикл** | `OnStateChange(IndicatorState state)` | `Configure` — инициализация параметров и серий |
| **Расчёт** | `OnProcess(List<Candle> source, int index)` | Вызывается для каждой свечи; `source` — все свечи, `index` — текущий индекс |
| **Встроенные индикаторы** | `CreateIndicator(typeName, name, parameters)` | Создание дочерних индикаторов внутри текущего |
| **Перерисовка** | `RePaint()` | Принудительное обновление графика |
| **Перезагрузка** | `Reload()` | Пересчёт всех значений с начала |

**Типы отрисовки серий (`IndicatorChartPaintType`):**

| Тип | Описание |
|-----|----------|
| `Line` | Линия |
| `Column` | Столбцы (гистограмма) |
| `Point` | Точки |
| `LinePoint` | Линия с точками |

---

### 6.2 Полный пример индикатора SMA

Ниже — полный скрипт простой скользящей средней, который можно положить в `Custom/Indicators/Scripts/SmaSimple.cs`:

```csharp
using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Indicators.Samples
{
    [Indicator("SmaSimple")]
    public class SmaSimple : Aindicator
    {
        public IndicatorParameterInt _length;
        public IndicatorParameterString _priceType;
        public IndicatorDataSeries _smaSeries;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 20);
                _priceType = CreateParameterStringCollection("Price type", "Close",
                    new List<string>() { "Open", "High", "Low", "Close" });
                _smaSeries = CreateSeries("SMA", System.Drawing.Color.DodgerBlue,
                                          IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> source, int index)
        {
            if (index < _length.ValueInt - 1)
            {
                _smaSeries.Values[index] = 0;
                return;
            }

            decimal sum = 0;
            for (int i = index; i > index - _length.ValueInt; i--)
            {
                sum += GetPrice(source[i]);
            }
            _smaSeries.Values[index] = sum / _length.ValueInt;
        }

        private decimal GetPrice(Candle candle)
        {
            switch (_priceType.ValueString)
            {
                case "Open":  return candle.Open;
                case "High":  return candle.High;
                case "Low":   return candle.Low;
                default:      return candle.Close;
            }
        }
    }
}
```

**Как использовать из робота на BotTabSimple:**

```csharp
// Создание индикатора на табе
Aindicator sma = IndicatorsFactory.CreateIndicatorByName("SmaSimple", "MySma", false);
sma = (Aindicator)_tab.CreateCandleIndicator(sma, "Prime");

// Доступ к значению
if (_sma.DataSeries[0].Values.Count > 0) {
    decimal lastSma = _sma.DataSeries[0].Values[_sma.DataSeries[0].Values.Count - 1];
}

// Изменение параметра и перезагрузка
_sma.ParametersDigit[0].Value = 50;  // меняем Length
_sma.Save();
_sma.Reload();
```

---

### 6.3 Примеры скриптов индикаторов из папки `Custom`

Папка `Custom/Indicators/Scripts/` создаётся рядом с `OsEngine.exe` (вне исходного кода). В неё кладутся файлы `.cs` или `.txt` — OsEngine компилирует их при первом обращении через `IndicatorsFactory`.

**Шаблоны для создания скриптов (внутри репозитория):**

| Название | Описание | Путь в репозитории |
|----------|----------|-------------------|
| `Sample1Blank` | Минимальный пустой индикатор — скелет | `OsEngine/Indicators/Samples/Sample1Blank.cs` |
| `Sample2IndicatorParameters` | Все типы параметров: `Int`, `Decimal`, `Bool`, `String`, `StringCollection` | `OsEngine/Indicators/Samples/Sample2IndicatorParameters.cs` |
| `Sample3IndicatorDataSeries` | Создание серии данных и запись значений | `OsEngine/Indicators/Samples/Sample3IndicatorDataSeries.cs` |

**Где размещать готовые скрипты:**

```
OsEngine.exe
Custom/
└── Indicators/
    └── Scripts/
        ├── MySma.cs          ← ваш индикатор
        ├── MyRsi.txt         ← тоже поддерживается
        └── Dlls/
            └── MyMathLib.dll ← внешние зависимости
```

**Правила для скриптов в `Custom/Indicators/Scripts/`:**

1. **Пространство имён:** Любое, но класс должен быть `public` и наследовать `Aindicator`
2. **Атрибут:** `[Indicator("ИмяДляВызова")]` — по этому имени индикатор ищется в `CreateIndicatorByName`
3. **Расширение файла:** `.cs`
4. **Перекомпиляция:** Не требуется — скрипт компилируется один раз и кэшируется в `_compiledIndicatorTypesCache`

**Порядок поиска индикатора по имени:**

1. Встроенные индикаторы (`OsEngine/Charts/CandleChart/Indicators/` — устаревшие)
2. Скрипты в `Custom/Indicators/Scripts/` (предпочтительный способ)
3. Если не найден — ошибка `MessageBox`

**Изучать:** `Sample1Blank.cs` — минимальный скелет. `Sample2IndicatorParameters.cs` — работа с параметрами. `Sample3IndicatorDataSeries.cs` — запись данных в серию.
