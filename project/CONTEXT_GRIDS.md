# CONTEXT_GRIDS — Сеточные стратегии OsEngine

Сеточная торговля (grid trading) в OsEngine реализована через подсистему `GridsMaster` / `TradeGrid`. Этот файл содержит всё необходимое для создания сеточных роботов: от минимального шаблона до разбора реальных примеров из `OsEngine/Robots/Grids/`.

Базовая архитектура — в `CONTEXT.md`, каталог остальных роботов — в `CONTEXT_ROBOTS.md`.

---

## 1. Общие принципы сеточной торговли

### 1.1 Что такое `TradeGrid`

`TradeGrid` — это объект, управляющий сеткой лимитных ордеров на одном табе (`BotTabSimple`). Сетка создаётся через `_tab.GridsMaster.CreateNewTradeGrid()` и живёт до явного удаления или остановки.

### 1.2 Два типа сеток

| Тип | `TradeGridPrimeType` | Логика | Когда использовать |
|-----|----------------------|--------|-------------------|
| **Накопление позиции** | `OpenPosition` | Сетка набирает одну большую позицию частями. Общий стоп/профит/трейлинг на всю позицию. | Трендовый вход: усреднение по мере движения цены |
| **Маркет-мейкинг** | `MarketMaking` | Каждая линия сетки — отдельная позиция. Каждая закрывается своим профитом. | Контртренд: ловим откаты, зарабатываем на колебаниях |

**Важно:** тип сетки определяет всю внутреннюю логику. Нельзя менять `GridType` после создания — нужно удалить сетку и создать новую.

### 1.3 Жизненный цикл сетки

```
Создание (GridsMaster.CreateNewTradeGrid)
        ↓
Настройка (GridCreator, StopAndProfit, TrailingUp, StopBy)
        ↓
Активация (Regime = TradeGridRegime.On)
        ↓
Торговля (выставление/перестановка/исполнение ордеров)
        ↓
Остановка (Regime = CloseOnly / CloseForced / Off)
        ↓
Удаление (DeleteGrid / Delete)
```

### 1.4 Режимы работы (`TradeGridRegime`)

| Режим | Значение | Описание |
|-------|----------|----------|
| `On` | Торговля | Сетка активна, выставляет и закрывает позиции |
| `Off` | Выключено | Сетка простаивает, ордера НЕ отменяются |
| `OffAndCancelOrders` | Выключено + отмена | Сетка выключена, все ордера отменены |
| `CloseOnly` | Только закрытие | Новые позиции не открываются, работают только выходы |
| `CloseForced` | Форсированное закрытие | Немедленное закрытие всех позиций по Market или Limit |

---

## 2. Бойлерплейты

### 2.1 Минимальный сеточный робот

```csharp
using OsEngine.Entity;
using OsEngine.Market.Servers;
using OsEngine.Market;
using OsEngine.Logging;
using System.Collections.Generic;

namespace OsEngine.Robots.Grids
{
    [Bot("MyGridBot")]
    public class MyGridBot : BotPanel
    {
        private BotTabSimple _tab;
        private StrategyParameterString _regime;
        private StrategyParameterInt _lineCount;
        private StrategyParameterDecimal _lineStep;

        public MyGridBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            _regime = CreateParameter("Regime", "On", new[] { "On", "Off" });
            _lineCount = CreateParameter("Line count", 5, 2, 50, 1);
            _lineStep = CreateParameter("Line step %", 1.0m, 0.1m, 10.0m, 0.1m);

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            try
            {
                if (_regime.ValueString == "Off") return;
                if (candles.Count < 5) return;

                // Проверяем, есть ли уже сетка
                if (_tab.GridsMaster.Grid != null)
                {
                    return; // сетка уже работает
                }

                // Создание сетки
                TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();

                // Тип сетки
                grid.GridType = TradeGridPrimeType.MarketMaking;

                // Параметры линий
                decimal lastPrice = candles[candles.Count - 1].Close;
                grid.GridCreator.FirstPrice = lastPrice;
                grid.GridCreator.LineCountStart = _lineCount.ValueInt;
                grid.GridCreator.LineStep = _lineStep.ValueDecimal;
                grid.GridCreator.TypeStep = TradeGridValueType.Percent;
                grid.GridCreator.ProfitStep = _lineStep.ValueDecimal;
                grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
                grid.GridCreator.GridSide = Side.Buy;

                // Объём
                grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
                grid.GridCreator.StartVolume = 1;

                // Трейлинг-стоп на всю сетку
                grid.StopAndProfit.TrailStopRegime = OnOffRegime.On;
                grid.StopAndProfit.TrailStopValueType = TradeGridValueType.Percent;
                grid.StopAndProfit.TrailStopValue = 2.0m;

                // Ограничения
                grid.StopBy.StopGridByPositionsCountIsOn = true;
                grid.StopBy.StopGridByPositionsCountValue = 10;

                // Активация
                grid.Regime = TradeGridRegime.On;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}
```

### 2.2 Расширенный бойлерплейт с TrailingUp, TrailingDown и StopBy

Используйте этот шаблон, если сетка должна двигаться вслед за ценой (например, в тренде) и иметь защиту от размазывания.

```csharp
namespace OsEngine.Robots.Grids
{
    [Bot("MyGridBotTrailing")]
    public class MyGridBotTrailing : BotPanel
    {
        private BotTabSimple _tab;
        private StrategyParameterString _regime;
        private StrategyParameterInt _lineCount;
        private StrategyParameterDecimal _lineStep;
        private StrategyParameterDecimal _trailingStep;
        private StrategyParameterDecimal _trailingLimit;
        private StrategyParameterInt _lifeTime;
        private StrategyParameterInt _maxPositions;

        public MyGridBotTrailing(string name, StartProgram startProgram) : base(name, startProgram)
        {
            _regime = CreateParameter("Regime", "On", new[] { "On", "Off" });
            _lineCount = CreateParameter("Line count", 5, 2, 50, 1);
            _lineStep = CreateParameter("Line step %", 0.5m, 0.1m, 5.0m, 0.1m);
            _trailingStep = CreateParameter("Trailing step %", 0.5m, 0.1m, 5.0m, 0.1m);
            _trailingLimit = CreateParameter("Trailing limit %", 10.0m, 1.0m, 50.0m, 1.0m);
            _lifeTime = CreateParameter("Life time sec", 600, 60, 3600, 60);
            _maxPositions = CreateParameter("Max positions", 50, 10, 200, 10);

            TabCreate(BotTabType.Simple);
            _tab = TabsSimple[0];
            _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
            _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccesEvent;
        }

        private void _tab_CandleFinishedEvent(List<Candle> candles)
        {
            try
            {
                if (_regime.ValueString == "Off") return;
                if (candles.Count < 5) return;

                if (_tab.GridsMaster.Grid != null)
                    return;

                decimal lastPrice = candles[candles.Count - 1].Close;
                TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();

                grid.GridType = TradeGridPrimeType.MarketMaking;
                grid.GridCreator.FirstPrice = lastPrice;
                grid.GridCreator.LineCountStart = _lineCount.ValueInt;
                grid.GridCreator.LineStep = _lineStep.ValueDecimal;
                grid.GridCreator.TypeStep = TradeGridValueType.Percent;
                grid.GridCreator.ProfitStep = _lineStep.ValueDecimal;
                grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
                grid.GridCreator.GridSide = Side.Buy;

                grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
                grid.GridCreator.StartVolume = 1;

                // TrailingUp — сдвиг сетки вверх при росте цены
                grid.TrailingUp.TrailingUpIsOn = true;
                grid.TrailingUp.TrailingUpStep = _tab.RoundPrice(
                    lastPrice * _trailingStep.ValueDecimal / 100, _tab.Security, Side.Buy);
                grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * _trailingLimit.ValueDecimal / 100;
                grid.TrailingUp.TrailingUpCanMoveExitOrder = false; // тейки остаются на месте

                // TrailingDown — сдвиг сетки вниз при падении цены
                grid.TrailingDown.TrailingDownIsOn = true;
                grid.TrailingDown.TrailingDownStep = _tab.RoundPrice(
                    lastPrice * _trailingStep.ValueDecimal / 100, _tab.Security, Side.Sell);
                grid.TrailingDown.TrailingDownLimit = lastPrice - lastPrice * _trailingLimit.ValueDecimal / 100;
                grid.TrailingDown.TrailingDownCanMoveExitOrder = false;

                // StopBy — авто-остановка сетки
                grid.StopBy.StopGridByLifeTimeIsOn = true;
                grid.StopBy.StopGridByLifeTimeSecondsToLife = _lifeTime.ValueInt;
                grid.StopBy.StopGridByPositionsCountIsOn = true;
                grid.StopBy.StopGridByPositionsCountValue = _maxPositions.ValueInt;

                grid.Regime = TradeGridRegime.On;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _tab_PositionClosingSuccesEvent(Position pos)
        {
            // Удаляем сетку после закрытия всех позиций
            if (_tab.GridsMaster.Grid == null) return;
            if (!_tab.GridsMaster.Grid.HaveOpenPositionsByGrid)
            {
                _tab.GridsMaster.Grid.DeleteGrid();
            }
        }
    }
}
```

---

## 3. Справочник API TradeGrid

### 3.1 Создание и удаление

```csharp
// Создание сетки
TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();

// Проверка существования сетки
if (_tab.GridsMaster.Grid != null) { /* сетка уже есть */ }

// Форсированное закрытие (закрыть все позиции, потом удалить сетку)
if (_tab.GridsMaster.Grid != null)
{
    _tab.GridsMaster.Grid.Regime = TradeGridRegime.CloseForced;
}

// Удаление сетки после закрытия всех позиций (в обработчике события)
private void _tab_PositionClosingSuccesEvent(Position pos)
{
    if (_tab.GridsMaster.Grid == null) return;
    if (!_tab.GridsMaster.Grid.HaveOpenPositionsByGrid)
    {
        _tab.GridsMaster.Grid.DeleteGrid();
    }
}

// Немедленное удаление (ордера отменятся, но открытые позиции останутся без ордеров на выход!)
_tab.GridsMaster.Grid.DeleteGrid(); // удалить сетку
// или
_tab.GridsMaster.DeleteGrid();       // через мастер
```

### 3.2 GridCreator — параметры линий

| Свойство | Тип | Описание | Пример |
|----------|-----|----------|--------|
| `GridSide` | `Side` | Направление сетки (`Buy` / `Sell`) | `Side.Buy` |
| `FirstPrice` | `decimal` | Цена первой линии | `lastPrice` |
| `LineCountStart` | `int` | Количество линий | `10` |
| `LineStep` | `decimal` | Шаг между линиями | `0.5m` |
| `TypeStep` | `TradeGridValueType` | Тип шага (`Absolute` / `Percent`) | `Percent` |
| `StepMultiplicator` | `decimal` | Мультипликатор шага (1 = линейно) | `1.0m` |
| `ProfitStep` | `decimal` | Тейк-профит каждой линии | `0.5m` |
| `TypeProfit` | `TradeGridValueType` | Тип профита | `Percent` |
| `ProfitMultiplicator` | `decimal` | Мультипликатор профита | `1.0m` |
| `TypeVolume` | `TradeGridVolumeType` | Тип объёма (`Contracts` / `ContractCurrency` / `DepositPercent`) | `Contracts` |
| `StartVolume` | `decimal` | Стартовый объём | `1` |
| `MartingaleMultiplicator` | `decimal` | Мартингейл: объём × на каждой следующей линии | `1.0m` |
| `TradeAssetInPortfolio` | `string` | Актив для расчёта объёма в валюте | `"USDT"` |

**Объём через `GetVolume(tab)`:**

Если робот использует стандартные параметры `VolumeType` + `Volume` + `TradeAssetInPortfolio`, рассчитайте объём через `GetVolume(tab)` и передайте в сетку:

```csharp
// Расчёт объёма через универсальный метод
decimal volume = GetVolume(_tab);

// Передача в сетку
grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
grid.GridCreator.StartVolume = volume;
```

**Важно:** `TradeGridVolumeType` имеет три режима:
- `Contracts` — фиксированное количество контрактов (`StartVolume = 1`)
- `ContractCurrency` — сумма в валюте контракта (`StartVolume = 1000`, `TradeAssetInPortfolio = "USDT"`)
- `DepositPercent` — процент от депозита (`StartVolume = 10`, `TradeAssetInPortfolio = "Prime"`)

Если робот стандартизирован под `GetVolume(tab)`, используйте `Contracts` и передавайте рассчитанное значение.

### 3.3 StopAndProfit — стопы и профиты

```csharp
// Общий профит по средней цене (OpenPosition)
grid.StopAndProfit.ProfitRegime = OnOffRegime.On;
grid.StopAndProfit.ProfitValueType = TradeGridValueType.Percent;
grid.StopAndProfit.ProfitValue = 5.0m;
grid.StopAndProfit.StopTradingAfterProfit = true; // остановить сетку после профита

// Общий стоп по средней цене (OpenPosition)
grid.StopAndProfit.StopRegime = OnOffRegime.On;
grid.StopAndProfit.StopValueType = TradeGridValueType.Percent;
grid.StopAndProfit.StopValue = 2.0m;

// Трейлинг-стоп по средней цене (OpenPosition)
grid.StopAndProfit.TrailStopRegime = OnOffRegime.On;
grid.StopAndProfit.TrailStopValueType = TradeGridValueType.Percent;
grid.StopAndProfit.TrailStopValue = 1.5m;
```

### 3.4 TrailingUp / TrailingDown — движение сетки вслед за ценой

Механизм автоматически сдвигает сетку лимитных ордеров, когда цена выходит за её границы. Не требует ручного пересоздания сетки.

**Как это работает:**
- `TrailingUpStep` — **порог срабатывания**. На сколько цена должна выйти за верхнюю границу сетки, чтобы сетка сдвинулась вверх. Это не шаг сдвига! Сетка сдвигается на величину превышения.
- `TrailingUpLimit` — **абсолютная цена**, выше которой сетка не сдвинется. Не процент! Считается как `lastPrice + lastPrice * limitPercent / 100`.
- `CanMoveExitOrder` — двигать ли вместе с ордерами входа и ордера на выход (тейк-профит). `false` — тейки остаются на старых уровнях (рекомендуется для MarketMaking).

```csharp
decimal lastPrice = candles[candles.Count - 1].Close;

// Сдвиг сетки вверх (при росте цены)
grid.TrailingUp.TrailingUpIsOn = true;
grid.TrailingUp.TrailingUpStep = _tab.RoundPrice(
    lastPrice * 0.005m, _tab.Security, Side.Buy); // 0.5% от цены, округлено до PriceStep
grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * 0.1m; // абсолютный предел: цена + 10%
grid.TrailingUp.TrailingUpCanMoveExitOrder = false; // тейки не двигаем

// Сдвиг сетки вниз (при падении цены)
grid.TrailingDown.TrailingDownIsOn = true;
grid.TrailingDown.TrailingDownStep = _tab.RoundPrice(
    lastPrice * 0.005m, _tab.Security, Side.Sell);
grid.TrailingDown.TrailingDownLimit = lastPrice - lastPrice * 0.1m; // абсолютный предел: цена - 10%
grid.TrailingDown.TrailingDownCanMoveExitOrder = false;
```

**Обязательно округляйте `TrailingUpStep` и `TrailingDownStep` через `_tab.RoundPrice(...)`**. Иначе сетка будет работать с дробными шагами цены и некорректно рассчитает уровни.

### 3.5 StopBy — авто-остановка сетки

```csharp
// По движению цены от первой цены сетки
grid.StopBy.StopGridByMoveUpIsOn = true;
grid.StopBy.StopGridByMoveUpValuePercent = 10.0m;   // цена ушла вверх на 10%
grid.StopBy.StopGridByMoveDownIsOn = true;
grid.StopBy.StopGridByMoveDownValuePercent = 10.0m; // цена ушла вниз на 10%

// По количеству позиций
grid.StopBy.StopGridByPositionsCountIsOn = true;
grid.StopBy.StopGridByPositionsCountValue = 20;

// По времени жизни сетки (секунды)
grid.StopBy.StopGridByLifeTimeIsOn = true;
grid.StopBy.StopGridByLifeTimeSecondsToLife = 3600; // 1 час

// По времени дня
grid.StopBy.StopGridByTimeOfDayIsOn = true;
grid.StopBy.StopGridByTimeOfDayHour = 18;
grid.StopBy.StopGridByTimeOfDayMinute = 0;
grid.StopBy.StopGridByTimeOfDaySecond = 0;
```

### 3.6 NonTradePeriods — не торговые периоды

```csharp
// Создание периода
grid.NonTradePeriods.CreateNewPeriod();
grid.NonTradePeriods.Periods[0].HourStart = 23;
grid.NonTradePeriods.Periods[0].MinuteStart = 50;
grid.NonTradePeriods.Periods[0].HourEnd = 0;
grid.NonTradePeriods.Periods[0].MinuteEnd = 10;
grid.NonTradePeriods.RegimeInNonTradePeriod = TradeGridRegime.CloseForced;
```

### 3.7 Дополнительные настройки

```csharp
// Задержка между операциями (мс) — защита от флуда биржи
grid.DelayInReal = 500;

// Макс. ордеров в рынке одновременно
grid.MaxOpenOrdersInMarket = 5;
grid.MaxCloseOrdersInMarket = 5;

// Проверка микро-объёмов
grid.CheckMicroVolumes = true;

// Макс. дистанция от текущей цены до ордеров (%)
grid.MaxDistanceToOrdersPercent = 50.0m;

// Только мейкер-ордера (без проскальзывания)
grid.OpenOrdersMakerOnly = true;

// Тип ордера при форсированном закрытии
grid.CloseForcedRegimeOrderType = OrderPriceType.Market;
```

### 3.8 Свойства состояния (read-only)

```csharp
decimal firstPrice = grid.FirstPriceReal;          // цена первой сделки
int openCount = grid.OpenPositionsCount;           // кол-во открытых позиций
decimal middlePrice = grid.MiddleEntryPrice;       // средняя цена входа
bool havePositions = grid.HaveOpenPositionsByGrid; // есть открытые позиции
bool haveOrders = grid.HaveOrdersInMarketInGrid;   // есть ордера в рынке
decimal maxPrice = grid.MaxGridPrice;              // макс. цена линии
decimal minPrice = grid.MinGridPrice;              // мин. цена линии
```

---

## 4. Каталог сеточных роботов из `Robots/Grids/`

### 4.1 `GridBollinger` — пробой Bollinger, односторонняя сетка

**Тип сетки:** `MarketMaking`  
**Сигнал на вход:** цена закрылась выше верхней линии Боллинджера → `Sell`-сетка (шорт); ниже нижней → `Buy`-сетка (лонг).  
**Сигнал на выход:** цена ушла на противоположную сторону канала → `CloseForced`, после закрытия сетка удаляется.

**Что почерпнуть:**
- Создание/удаление сетки по сигналу индикатора
- Настройка `MarketMaking` с `TrailStop` на всю сетку
- Ограничение на количество сеток (`if (_tab.GridsMaster.Grid != null) return`)
- Удаление сетки после форсированного закрытия через `PositionClosingSuccesEvent` + проверка `!grid.HaveOpenPositionsByGrid`

### 4.2 `GridTwoSides` — двусторонний маркет-мейкинг по ATR

**Тип сетки:** `MarketMaking` (две сетки одновременно)  
**Сигнал на вход:** ATR упал на заданный `%` относительно значения `N` свечей назад → рынок спокоен, выставляем обе сетки.  
**Сигнал на выход:** ATR вырос выше предыдущего значения → `CloseForced` для обеих сеток.

**Что почерпнуть:**
- Одновременное управление двумя сетками (Buy + Sell) на одном табе
- Проверка наличия сеток перед созданием новых
- `CloseForced` по внешнему сигналу (не по цене сетки)
- Использование `ParametrsChangeByUser` для пересоздания сеток при смене настроек

### 4.3 `GridTwoSignals` — две последовательные сетки по PriceChannel

**Тип сетки:** `MarketMaking` (две сетки последовательно)  
**Сигнал 1:** пробой `PriceChannel` вниз → первая `Buy`-сетка.  
**Сигнал 2:** есть первая сетка + цена вернулась к центру канала → вторая `Buy`-сетка.

**Что почерпнуть:**
- Каскадное усиление позиции по двум независимым сигналам
- `TrailingUp` и `TrailingDown` с лимитом (`TrailingUpLimit = 10%`)
- `StopBy.LifeTime` — авто-закрытие по времени жизни
- `StopBy.PositionsCount` — авто-закрытие по количеству сделок
- Переключение между сетками через `if (_tab.GridsMaster.GridArray.Count == 0)` и `Count == 1`

### 4.4 `GridLinearRegression` — трендовое накопление позиции

**Тип сетки:** `OpenPosition` (единственный робот с этим типом!)  
**Сигнал:** цена выше верхней линии `LinearRegressionChannel` → создаёт сетку на покупку.  
**Выход:** общий трейлинг-стоп на всю позицию. После закрытия сетка удаляется.

**Что почерпнуть:**
- `TradeGridPrimeType.OpenPosition` — накопление одной позиции
- Общий `StopAndProfit.TrailStopRegime` на всю сетку
- `GridCreator.GridSide = Side.Buy` — однонаправленная сетка
- Логика удаления сетки после закрытия всех позиций

### 4.5 `GridBollingerScreener` — скринер по Bollinger + ADX

**Тип сетки:** `MarketMaking` на `BotTabScreener`  
**Фильтры:** ADX в диапазоне `[min, max]` (сниженная волатильность) + пробой Боллинджера.  
**Ограничения:** `_maxGridsCount` — максимум активных сеток одновременно.

**Что почерпнуть:**
- Работа с `tab.GridsMaster` внутри обработчика `CandleFinishedEvent(List<Candle>, BotTabSimple tab)`
- Подсчёт общего количества сеток через перебор всех табов скринера
- `NonTradePeriods` — запрет торговли в заданные часы
- `TrailingUp/Down` на скринере
- Выход по пробою противоположной стороны канала → `CloseForced`

### 4.6 `GridPair` — парный арбитраж через сетки

**Тип сетки:** `MarketMaking` на `BotTabScreener` с двумя инструментами  
**Сигнал:** отклонение коинтеграции на `N` стандартных отклонений.  
**Логика:** `CointegrationLineSide.Down` — первая бумага лонг, вторая шорт; `Up` — наоборот.

**Что почерпнуть:**
- Две сетки на разных инструментах (лонг на одном, шорт на другом)
- `TrailingUp/Down` с малым шагом (`0.25%`)
- Выход при возврате коинтеграции → `CloseForced` обеим сеткам
- Управление сетками через `tab.GridsMaster.Grid.Regime`

### 4.7 `GridScreenerAdaptiveSoldiers` — адаптивный скринер

**Тип сетки:** `MarketMaking` на скринере  
**Сигнал:** паттерн «три растущих/падающих свечи» (Three Soldiers), адаптивный по волатильности.  
**Фильтр:** опциональный SMA-фильтр по наклону.

**Что почерпнуть:**
- Адаптация параметров сетки (`LineStep`, `ProfitStep`) под волатильность инструмента
- Класс `SecuritiesGridSettings` для хранения настроек на каждый инструмент
- Сохранение/загрузка настроек через `GetSaveString()` / `LoadFromString()`

### 4.8 `GridVolumeBollingerRankingScreener` — ранжирование по объёму

**Тип сетки:** `MarketMaking` на скринере  
**Фильтры:** `BollingerRankingFilter` (положение цены внутри канала в %) + `VolumeRanking` (топ-N по объёму).

**Что почерпнуть:**
- Ранжирование инструментов по совокупности фильтров
- Класс `SecurityRankingValue` для хранения ранга
- Создание сетки только на топ-N инструментов
- Управление лимитом активных сеток через счётчик

---

## 5. Примеры кода из реальных роботов

### 5.1 Создание сетки из `GridBollinger`

```csharp
TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();
grid.GridType = TradeGridPrimeType.MarketMaking;

grid.GridCreator.FirstPrice = candles[candles.Count - 1].Close;
grid.GridCreator.LineCountStart = _lineCount.ValueInt;
grid.GridCreator.LineStep = _lineStep.ValueDecimal;
grid.GridCreator.TypeStep = TradeGridValueType.Percent;
grid.GridCreator.ProfitStep = _profitStep.ValueDecimal;
grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
grid.GridCreator.GridSide = side; // Side.Buy или Side.Sell

grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
grid.GridCreator.StartVolume = _volume.ValueDecimal;

grid.StopAndProfit.TrailStopRegime = OnOffRegime.On;
grid.StopAndProfit.TrailStopValueType = TradeGridValueType.Percent;
grid.StopAndProfit.TrailStopValue = _trailStopPercent.ValueDecimal;

grid.Regime = TradeGridRegime.On;
```

### 5.2 Две сетки из `GridTwoSides`

```csharp
// Проверяем, что сеток нет
if (_tab.GridsMaster.GridArray.Count != 0) return;

// Первая сетка — Buy
TradeGrid gridBuy = _tab.GridsMaster.CreateNewTradeGrid();
gridBuy.GridType = TradeGridPrimeType.MarketMaking;
gridBuy.GridCreator.GridSide = Side.Buy;
gridBuy.GridCreator.FirstPrice = _tab.PriceBestBid - _tab.Security.PriceStep * 5;
// ... настройка шага, объёма, профита ...
gridBuy.Regime = TradeGridRegime.On;

// Вторая сетка — Sell
TradeGrid gridSell = _tab.GridsMaster.CreateNewTradeGrid();
gridSell.GridType = TradeGridPrimeType.MarketMaking;
gridSell.GridCreator.GridSide = Side.Sell;
gridSell.GridCreator.FirstPrice = _tab.PriceBestAsk + _tab.Security.PriceStep * 5;
// ... настройка ...
gridSell.Regime = TradeGridRegime.On;
```

### 5.3 Остановка и удаление сетки

```csharp
// Форсированное закрытие
if (_tab.GridsMaster.Grid != null)
{
    _tab.GridsMaster.Grid.Regime = TradeGridRegime.CloseForced;
}

// Удаление после закрытия всех позиций (в обработчике события)
private void _tab_PositionClosingSuccesEvent(Position pos)
{
    if (_tab.GridsMaster.Grid == null) return;
    if (!_tab.GridsMaster.Grid.HaveOpenPositionsByGrid)
    {
        _tab.GridsMaster.Grid.DeleteGrid();
    }
}
```

### 5.4 Проверка сеток на скринере

```csharp
private void _screener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
{
    // Подсчёт общего количества активных сеток
    int activeGrids = 0;
    for (int i = 0; i < _screener.Tabs.Count; i++)
    {
        if (_screener.Tabs[i].GridsMaster.Grid != null)
            activeGrids++;
    }
    if (activeGrids >= _maxGridsCount.ValueInt) return;

    // Проверка сетки на конкретном табе
    if (tab.GridsMaster.Grid != null) return;

    // Создание сетки на этом табе
    TradeGrid grid = tab.GridsMaster.CreateNewTradeGrid();
    grid.GridType = TradeGridPrimeType.MarketMaking;
    // ... настройка ...
    grid.Regime = TradeGridRegime.On;
}
```

### 5.5 TrailingUp и StopBy из `GridTwoSignals`

```csharp
decimal lastPrice = candles[candles.Count - 1].Close;

TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();
grid.GridType = TradeGridPrimeType.MarketMaking;
grid.GridCreator.FirstPrice = lastPrice;
grid.GridCreator.LineCountStart = _lineCount.ValueInt;
grid.GridCreator.LineStep = _lineStep.ValueDecimal;
grid.GridCreator.TypeStep = TradeGridValueType.Percent;
grid.GridCreator.ProfitStep = _profitStep.ValueDecimal;
grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
grid.GridCreator.GridSide = Side.Buy;

grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
grid.GridCreator.StartVolume = _volume.ValueDecimal;

// TrailingUp — сдвиг вверх при росте цены
grid.TrailingUp.TrailingUpStep = _tab.RoundPrice(lastPrice * 0.005m, _tab.Security, Side.Buy);
grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * 0.1m;
grid.TrailingUp.TrailingUpIsOn = true;
grid.TrailingUp.TrailingUpCanMoveExitOrder = false;

// TrailingDown — сдвиг вниз при падении цены
grid.TrailingDown.TrailingDownStep = _tab.RoundPrice(lastPrice * 0.005m, _tab.Security, Side.Sell);
grid.TrailingDown.TrailingDownLimit = lastPrice - lastPrice * 0.1m;
grid.TrailingDown.TrailingDownIsOn = true;
grid.TrailingDown.TrailingDownCanMoveExitOrder = false;

// Защита от размазывания
grid.StopBy.StopGridByLifeTimeIsOn = true;
grid.StopBy.StopGridByLifeTimeSecondsToLife = 3600;
grid.StopBy.StopGridByPositionsCountIsOn = true;
grid.StopBy.StopGridByPositionsCountValue = 20;

grid.Regime = TradeGridRegime.On;
```

### 5.6 TrailingUp на двух инструментах из `GridPair`

```csharp
// Первая сетка — на первом инструменте (лонг)
decimal lastPrice1 = tab1.PriceCenterMarketDepth;
TradeGrid grid1 = tab1.GridsMaster.CreateNewTradeGrid();
grid1.GridType = TradeGridPrimeType.MarketMaking;
grid1.GridCreator.GridSide = Side.Buy;
grid1.GridCreator.FirstPrice = lastPrice1;
grid1.GridCreator.LineCountStart = _lineCount.ValueInt;
grid1.GridCreator.LineStep = _lineStep.ValueDecimal;
grid1.GridCreator.TypeStep = TradeGridValueType.Percent;
grid1.GridCreator.ProfitStep = _profitStep.ValueDecimal;
grid1.GridCreator.TypeProfit = TradeGridValueType.Percent;

// TrailingUp/Down с малым шагом (0.25%)
grid1.TrailingUp.TrailingUpStep = tab1.RoundPrice(lastPrice1 * 0.002m, tab1.Security, Side.Buy);
grid1.TrailingUp.TrailingUpLimit = lastPrice1 + lastPrice1 * 0.25m;
grid1.TrailingUp.TrailingUpIsOn = true;
grid1.TrailingUp.TrailingUpCanMoveExitOrder = false;

grid1.TrailingDown.TrailingDownStep = tab1.RoundPrice(lastPrice1 * 0.002m, tab1.Security, Side.Sell);
grid1.TrailingDown.TrailingDownLimit = lastPrice1 - lastPrice1 * 0.25m;
grid1.TrailingDown.TrailingDownIsOn = true;

grid1.Regime = TradeGridRegime.On;

// Вторая сетка — на втором инструменте (шорт)
decimal lastPrice2 = tab2.PriceCenterMarketDepth;
TradeGrid grid2 = tab2.GridsMaster.CreateNewTradeGrid();
grid2.GridType = TradeGridPrimeType.MarketMaking;
grid2.GridCreator.GridSide = Side.Sell;
grid2.GridCreator.FirstPrice = lastPrice2;
// ... настройка аналогично grid1 ...
grid2.Regime = TradeGridRegime.On;
```

---

## 6. Частые ошибки при работе с сетками

| Ошибка | Почему плохо | Как правильно |
|--------|-------------|---------------|
| **Забыть `grid.Regime = TradeGridRegime.On`** | Сетка создана, но не торгует | Всегда явно активировать после настройки |
| **Создать вторую сетку, не проверив `GridsMaster.Grid`** | Две сетки конфликтуют, ордера перемешиваются | `if (_tab.GridsMaster.Grid != null) return;` |
| **Перепутать `OpenPosition` и `MarketMaking`** | `OpenPosition` не закрывает линии по отдельности; `MarketMaking` не имеет общего стопа | `OpenPosition` — для тренда/усреднения; `MarketMaking` — для откатов |
| **Не задать `ProfitStep`** | `MarketMaking` не знает, где закрывать прибыль | Всегда указывать `ProfitStep` и `TypeProfit` |
| **Не удалять сетку после `CloseForced`** | Сетка остаётся в памяти, мешает созданию новой | В `PositionClosingSuccesEvent` проверять `HaveOpenPositionsByGrid` и вызывать `DeleteGrid()` |
| **Забыть про `PriceStep` при `TrailingUpStep`** | Сетка двигается слишком часто или слишком редко | `TrailingUpStep = _tab.Security.PriceStep * N` |
| **Не использовать `StopBy`** | Сетка может «размазаться» при сильном движении | Всегда ограничивать по времени, количеству позиций или движению цены |
| **Забыть `try-catch` вокруг логики сетки** | Ошибка в `GridsMaster` остановит робота | Оборачивать всю логику создания/управления сеткой |
| **Хардкод цены в `FirstPrice`** | При перезапуске робота цена устарела | Использовать `candles.Last.Close` или `_tab.PriceBestBid/Ask` |
| **Не проверять `candles.Count`** | Обращение к `candles[candles.Count - 1]` при пустом списке | `if (candles.Count < N) return;` |
| **Думать, что `TrailingUpLimit` — процент** | Сетка уедет за пределы лимита или не сдвинется вообще | `TrailingUpLimit` — абсолютная цена: `lastPrice + lastPrice * 0.1m` |
| **Думать, что `TrailingUpStep` — шаг сдвига** | ИИ напишет фиксированный шаг, а должен быть порог срабатывания | `TrailingUpStep` — порог: на сколько цена выйдет за границу, чтобы сетка сдвинулась |
| **Забыть `RoundPrice` для `TrailingUpStep`** | Сетка получит дробный шаг цены и сломается | `_tab.RoundPrice(lastPrice * 0.005m, _tab.Security, Side.Buy)` |
| **Поставить `CanMoveExitOrder = true` бездумно** | Тейки уедут вместе со входами, сетка никогда не закроет прибыль | Для `MarketMaking` всегда `false` — тейки стоят на месте |
| **Удалить сетку сразу после `CloseForced`** | Открытые позиции останутся без ордеров на выход | `CloseForced` → ждать `PositionClosingSuccesEvent` → `DeleteGrid()` |
| **Забыть `TrailingDown` при настройке `TrailingUp`** | Сетка сдвигается вверх, но при падении цены застревает | Всегда настраивать оба направления, если сетка работает в обе стороны |
| **Перепутать `StopGridByMoveUp` с `TrailingUpLimit`** | `MoveUp` — стоп-лосс для всей сетки; `TrailingUpLimit` — предел сдвига | `MoveUp` закрывает сетку; `TrailingUpLimit` только ограничивает сдвиг |

---

## 7. Быстрый справочник: от задачи к сетке

| Запрос пользователя | Тип сетки | Робот-образец | Ключевой паттерн |
|---------------------|-----------|---------------|------------------|
| «Сетка на покупку по индикатору» | `MarketMaking` | `GridBollinger` | Создание/удаление по сигналу, `TrailStop` |
| «Маркет-мейкинг с двух сторон» | `MarketMaking` | `GridTwoSides` | Две сетки Buy+Sell, выход по ATR |
| «Усреднение в лонг по тренду» | `OpenPosition` | `GridLinearRegression` | Общий `TrailStop`, накопление позиции |
| «Сетка на скринере» | `MarketMaking` | `GridBollingerScreener` | `tab.GridsMaster`, лимит сеток |
| «Парный арбитраж с сетками» | `MarketMaking` | `GridPair` | Две сетки на разных инструментах |
| «Адаптивная сетка под волатильность» | `MarketMaking` | `GridScreenerAdaptiveSoldiers` | Адаптация `LineStep` под ATR |
| «Сетка с ранжированием по объёму» | `MarketMaking` | `GridVolumeBollingerRankingScreener` | `VolumeRanking`, топ-N инструментов |
| «Каскад из двух сеток» | `MarketMaking` | `GridTwoSignals` | `TrailingUp/Down` с лимитом, `StopBy.LifeTime` |

---

---

## 8. Шаблон: сетка по индикатору с TrailingUp и StopBy

Полный паттерн для робота, который создаёт сетку по сигналу индикатора, сдвигает её за ценой и закрывает по противоположному сигналу.

```csharp
[Bot("GridOnIndicator")]
public class GridOnIndicator : BotPanel
{
    private BotTabSimple _tab;
    private StrategyParameterString _regime;
    private StrategyParameterInt _emaPeriod;
    private StrategyParameterInt _lineCount;
    private StrategyParameterDecimal _lineStep;
    private StrategyParameterDecimal _trailingStep;
    private StrategyParameterDecimal _trailingLimit;
    private StrategyParameterInt _lifeTime;
    private StrategyParameterInt _maxPositions;

    private Aindicator _ema;

    public GridOnIndicator(string name, StartProgram startProgram) : base(name, startProgram)
    {
        _regime = CreateParameter("Regime", "On", new[] { "On", "Off" });
        _emaPeriod = CreateParameter("EMA period", 50, 10, 200, 10);
        _lineCount = CreateParameter("Line count", 5, 2, 50, 1);
        _lineStep = CreateParameter("Line step %", 0.5m, 0.1m, 5.0m, 0.1m);
        _trailingStep = CreateParameter("Trailing step %", 0.5m, 0.1m, 5.0m, 0.1m);
        _trailingLimit = CreateParameter("Trailing limit %", 10.0m, 1.0m, 50.0m, 1.0m);
        _lifeTime = CreateParameter("Life time sec", 600, 60, 3600, 60);
        _maxPositions = CreateParameter("Max positions", 50, 10, 200, 10);

        TabCreate(BotTabType.Simple);
        _tab = TabsSimple[0];

        _ema = new Aindicator("Ema");
        _ema = _tab.CreateCandleIndicator(_ema, "Prime");
        _ema.ParametersDigit[0].Value = _emaPeriod.ValueInt;
        _ema.ParametersDigit[1].Value = 1; // свеча закрытия
        _ema.Save();

        _tab.CandleFinishedEvent += _tab_CandleFinishedEvent;
        _tab.PositionClosingSuccesEvent += _tab_PositionClosingSuccesEvent;
    }

    private void _tab_CandleFinishedEvent(List<Candle> candles)
    {
        try
        {
            if (_regime.ValueString == "Off") return;
            if (candles.Count < _emaPeriod.ValueInt + 5) return;
            if (_ema.DataSeries.Count == 0 || _ema.DataSeries[0].Values == null) return;

            decimal lastPrice = candles[candles.Count - 1].Close;
            decimal emaValue = _ema.DataSeries[0].Values[_ema.DataSeries[0].Values.Count - 1];

            // Есть активная сетка — проверяем условие закрытия (смена тренда)
            if (_tab.GridsMaster.Grid != null)
            {
                bool trendChanged = (lastPrice < emaValue && _tab.GridsMaster.Grid.GridCreator.GridSide == Side.Buy)
                                 || (lastPrice > emaValue && _tab.GridsMaster.Grid.GridCreator.GridSide == Side.Sell);

                if (trendChanged)
                {
                    _tab.GridsMaster.Grid.Regime = TradeGridRegime.CloseForced;
                }
                return;
            }

            // Создание сетки по сигналу тренда
            Side side = lastPrice > emaValue ? Side.Buy : Side.Sell;
            if (side == Side.Sell && _regime.ValueString == "OnlyLong") return;
            if (side == Side.Buy && _regime.ValueString == "OnlyShort") return;

            TradeGrid grid = _tab.GridsMaster.CreateNewTradeGrid();
            grid.GridType = TradeGridPrimeType.MarketMaking;
            grid.GridCreator.FirstPrice = lastPrice;
            grid.GridCreator.LineCountStart = _lineCount.ValueInt;
            grid.GridCreator.LineStep = _lineStep.ValueDecimal;
            grid.GridCreator.TypeStep = TradeGridValueType.Percent;
            grid.GridCreator.ProfitStep = _lineStep.ValueDecimal;
            grid.GridCreator.TypeProfit = TradeGridValueType.Percent;
            grid.GridCreator.GridSide = side;

            grid.GridCreator.TypeVolume = TradeGridVolumeType.Contracts;
            grid.GridCreator.StartVolume = 1;

            // TrailingUp
            grid.TrailingUp.TrailingUpIsOn = true;
            grid.TrailingUp.TrailingUpStep = _tab.RoundPrice(
                lastPrice * _trailingStep.ValueDecimal / 100, _tab.Security, Side.Buy);
            grid.TrailingUp.TrailingUpLimit = lastPrice + lastPrice * _trailingLimit.ValueDecimal / 100;
            grid.TrailingUp.TrailingUpCanMoveExitOrder = false;

            // TrailingDown
            grid.TrailingDown.TrailingDownIsOn = true;
            grid.TrailingDown.TrailingDownStep = _tab.RoundPrice(
                lastPrice * _trailingStep.ValueDecimal / 100, _tab.Security, Side.Sell);
            grid.TrailingDown.TrailingDownLimit = lastPrice - lastPrice * _trailingLimit.ValueDecimal / 100;
            grid.TrailingDown.TrailingDownCanMoveExitOrder = false;

            // StopBy
            grid.StopBy.StopGridByLifeTimeIsOn = true;
            grid.StopBy.StopGridByLifeTimeSecondsToLife = _lifeTime.ValueInt;
            grid.StopBy.StopGridByPositionsCountIsOn = true;
            grid.StopBy.StopGridByPositionsCountValue = _maxPositions.ValueInt;

            grid.Regime = TradeGridRegime.On;
        }
        catch (Exception error)
        {
            SendNewLogMessage(error.ToString(), LogMessageType.Error);
        }
    }

    private void _tab_PositionClosingSuccesEvent(Position pos)
    {
        if (_tab.GridsMaster.Grid == null) return;
        if (!_tab.GridsMaster.Grid.HaveOpenPositionsByGrid)
        {
            _tab.GridsMaster.Grid.DeleteGrid();
        }
    }
}
```

**Изучать:**
- `GridBollinger.cs` — базовая `MarketMaking` сетка с трейлинг-стопом
- `GridLinearRegression.cs` — единственный пример `OpenPosition`
- `GridTwoSides.cs` — две сетки одновременно
- `GridBollingerScreener.cs` — скринер с сетками
- `GridTwoSignals.cs` — каскадные сетки с TrailingUp/Down
