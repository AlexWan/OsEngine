# CONTEXT — Monitor-роботы (`BotTabScreener` + таблица + алерты)

> **Область**: Роботы-наблюдатели за скринером, которые отображают данные в виде таблицы, выбрасывают алерты и могут автоматически торговать.
> **Ключевое отличие**: Помимо торговой логики, Monitor создаёт кастомную `DataGridView` прямо в панели параметров робота. Пользователь видит таблицу движений по всем инструментам скринера и может вручную открыть график или позицию одним кликом.

---

## 1.1 Что такое Monitor-робот

Monitor-робот — это надстройка над `BotTabScreener`, которая:

1. **Собирает метрики** по каждому инструменту скринера (движение к High/Low, импульс, RSI, объёмный рейтинг).
2. **Отображает таблицу** с этими метриками в UI параметров робота.
3. **Выбрасывает алерты** (звук + текстовое сообщение в лог) при достижении порога.
4. **Может торговать** — открывать позиции автоматически или вручную через кнопки в таблице.

**Типичная архитектура:**

| Компонент | Назначение |
|-----------|------------|
| `BotTabScreener` | Источник данных (свечи, позиции) по портфелю инструментов |
| `DataGridView` | Таблица с метриками, кнопками Chart/Open/Close |
| `SignalData` + `Dictionary` | Дедупликация алертов (один сигнал на свечу) |
| `SoundPlayer` | Звуковое оповещение |
| `NonTradePeriods` | Запрет торговли в заданные часы/дни |

---

## 1.2 Архитектура Monitor

### События скринера

| Событие | Сигнатура | Режим | Когда использовать |
|---------|-----------|-------|-------------------|
| `CandleFinishedEvent` | `Action<List<Candle>, BotTabSimple>` | `OnCandleFinish` | Торговля по закрытым свечам |
| `CandleUpdateEvent` | `Action<List<Candle>, BotTabSimple>` | `OnCandleUpdate` | Мониторинг на каждом тике |
| `CandlesSyncFinishedEvent` | `Action` | `On` | Обработка всех табов разом (MonitorVolume) |
| `PositionOpeningSuccesEvent` | `Action<Position, BotTabSimple>` | — | Установка стопа/тейка при открытии |
| `PositionClosingSuccesEvent` | `Action<Position, BotTabSimple>` | — | Обновление таблицы после закрытия |

> **Режим работы** задаётся параметром `_regime`: `"Off"` / `"On"` / `"OnCandleUpdate"` / `"OnCandleFinish"`.

### Структура UI-таблицы

Типичные колонки:

| № | Колонка | Содержимое |
|---|---------|------------|
| 0 | `Security` | Название инструмента |
| 1 | `Metric 1` | Вычисленное значение (движение, RSI, индекс объёма) |
| 2 | `Metric 2` | Дополнительное значение (объём, рейтинг) |
| 3 | `Move` | Значение движения/отклонения |
| 4 | `Chart` | Кнопка «График» |
| 5 | `Open` | Кнопка «Открыть позицию» |
| 6 | `Pos` | Статус позиции (`0`, `Long`, `Short`) |
| 7 | `Close` | Кнопка «Закрыть позицию» |

---

## 1.3 Бойлерплейт Monitor-робота

```csharp
using System;
using System.Collections.Generic;
using System.Media;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Properties;

namespace OsEngine.Robots.Monitors
{
    [Bot("MyMonitor")]
    public class MyMonitor : BotPanel
    {
        private BotTabScreener _tabScreener;
        private StrategyParameterString _regime;

        // Table UI
        private WindowsFormsHost _hostTable;
        private DataGridView _tableDataGrid;
        private DateTime _lastTimeUpdateTable = DateTime.MinValue;

        // Signal deduplication
        private Dictionary<string, SignalData> _upSignals = new Dictionary<string, SignalData>();
        private Dictionary<string, SignalData> _downSignals = new Dictionary<string, SignalData>();

        public MyMonitor(string name, StartProgram startProgram) : base(name, startProgram)
        {
            TabCreate(BotTabType.Screener);
            _tabScreener = TabsScreener[0];

            _regime = CreateParameter("Regime", "Off",
                new[] { "Off", "OnCandleUpdate", "OnCandleFinish" });

            // Events
            _tabScreener.CandleFinishedEvent += _tabScreener_CandleFinishedEvent;
            _tabScreener.CandleUpdateEvent += _tabScreener_CandleUpdateEvent;
            _tabScreener.PositionOpeningSuccesEvent += _tabScreener_PositionOpeningSuccesEvent;
            _tabScreener.PositionClosingSuccesEvent += _tabScreener_PositionClosingSuccesEvent;

            // UI table setup
            this.ParamGuiSettings.Height = 800;
            this.ParamGuiSettings.Width = 780;
            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab(" Monitor ");
            CreateColumnsTable();
            customTab.AddChildren(_hostTable);
        }

        private void _tabScreener_CandleFinishedEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString == "Off") return;
            MainLogic(candles, tab);
        }

        private void _tabScreener_CandleUpdateEvent(List<Candle> candles, BotTabSimple tab)
        {
            if (_regime.ValueString != "OnCandleUpdate") return;
            MainLogic(candles, tab);
        }

        private void MainLogic(List<Candle> candles, BotTabSimple tab)
        {
            if (tab.IsConnected == false || tab.IsReadyToTrade == false) return;
            if (candles == null || candles.Count < 5) return;

            UpdateMoveData(candles, tab);
            TryUpdateTable();
            TrySendSignal(tab, candles);

            // ... торговая логика ...
        }

        private void _tabScreener_PositionOpeningSuccesEvent(Position position, BotTabSimple tab)
        {
            TryUpdateTable();
        }

        private void _tabScreener_PositionClosingSuccesEvent(Position position, BotTabSimple tab)
        {
            TryUpdateTable();
        }
    }
}
```

---

## 1.4 Создание таблицы (`DataGridView`)

### Создание колонок

```csharp
private void CreateColumnsTable()
{
    if (MainWindow.GetDispatcher.CheckAccess() == false)
    {
        MainWindow.GetDispatcher.Invoke(new Action(CreateColumnsTable));
        return;
    }

    _hostTable = new WindowsFormsHost();

    _tableDataGrid = DataGridFactory.GetDataGridView(
        DataGridViewSelectionMode.FullRowSelect,
        DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);

    _tableDataGrid.ScrollBars = ScrollBars.Vertical;
    _tableDataGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
    _tableDataGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
    _tableDataGrid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

    DataGridViewTextBoxCell cellTemplate = new DataGridViewTextBoxCell();
    cellTemplate.Style.WrapMode = DataGridViewTriState.True;

    // Колонка Security
    DataGridViewColumn col0 = new DataGridViewColumn();
    col0.CellTemplate = cellTemplate;
    col0.HeaderText = "Security";
    col0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    _tableDataGrid.Columns.Add(col0);

    // ... остальные колонки ...

    _tableDataGrid.DataError += _tableDataGrid_DataError;
    _tableDataGrid.CellClick += _tableDataGrid_CellClick;
    _hostTable.Child = _tableDataGrid;
}
```

### Обновление таблицы (rate limiting 1 раз в секунду)

```csharp
private void TryUpdateTable()
{
    if (_tableDataGrid.InvokeRequired)
    {
        _tableDataGrid.Invoke(new Action(TryUpdateTable));
        return;
    }

    // Не чаще 1 раза в секунду
    if (_lastTimeUpdateTable != DateTime.MinValue
        && _lastTimeUpdateTable.AddSeconds(1) > DateTime.Now)
    {
        return;
    }
    _lastTimeUpdateTable = DateTime.Now;

    if (_tableDataGrid.Rows.Count != _dataCollection.Count)
    {
        // Полное перестроение
        _tableDataGrid.Rows.Clear();
        foreach (var data in _dataCollection.Values)
        {
            _tableDataGrid.Rows.Add(GetRow(data));
        }
    }
    else
    {
        // Обновление только изменившихся ячеек
        for (int i = 0; i < _tableDataGrid.Rows.Count; i++)
        {
            var row = _tableDataGrid.Rows[i];
            var data = GetRow(_dataCollection[i]);
            if (row.Cells[1].Value?.ToString() != data.Cells[1].Value?.ToString())
                row.Cells[1].Value = data.Cells[1].Value;
            // ... аналогично для других колонок ...
        }
    }
}
```

### Обработка кликов по кнопкам

```csharp
private void _tableDataGrid_CellClick(object sender, DataGridViewCellEventArgs e)
{
    int row = e.RowIndex;
    int column = e.ColumnIndex;
    if (row < 0 || row >= _tableDataGrid.Rows.Count) return;

    string secName = _tableDataGrid.Rows[row].Cells[0].Value.ToString();
    BotTabSimple tab = _tabScreener.Tabs.Find(t => t.Connector.SecurityName == secName);
    if (tab == null) return;

    if (column == 4)
    {   // Chart
        int tabNumber = _tabScreener.Tabs.IndexOf(tab);
        _tabScreener.ShowChart(tabNumber);
    }
    else if (column == 5)
    {   // Open position
        tab.ShowOpenPositionDialog();
    }
    else if (column == 7)
    {   // Close position
        List<Position> openPoses = tab.PositionsOpenAll;
        if (openPoses.Count > 0)
            tab.ShowClosePositionDialog(openPoses[0]);
    }
}

private void _tableDataGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
{
    SendNewLogMessage(sender.ToString(), LogMessageType.Error);
}
```

---

## 1.5 Алерты и сигналы

### Дедупликация сигналов

```csharp
private Dictionary<string, SignalData> _upSignals = new Dictionary<string, SignalData>();
private Dictionary<string, SignalData> _downSignals = new Dictionary<string, SignalData>();

public class SignalData
{
    public string SecurityName;
    public DateTime Time;
}
```

### Отправка сигнала (проверка + дедупликация)

```csharp
private void TrySendSignal(BotTabSimple tab, List<Candle> candles)
{
    if (_upSignalsIsOn.ValueBool == false && _downSignalsIsOn.ValueBool == false)
        return;

    // Up signal
    if (_upSignalsIsOn.ValueBool)
    {
        MoveData myData = GetMoveData(tab);
        if (myData.MoveUp > _upSignalsPercentMove.ValueDecimal)
        {
            SignalData signal;
            if (_upSignals.TryGetValue(tab.Connector.SecurityName, out signal) == false)
            {
                signal = new SignalData { SecurityName = tab.Connector.SecurityName };
                _upSignals.Add(tab.Connector.SecurityName, signal);
            }

            if (signal.Time == candles[^1].TimeStart)
                return; // Уже отправляли на этой свече

            signal.Time = candles[^1].TimeStart;
            DropSignal(myData, "Up signal", _upSignalsMusic.ValueString, _upSignalsErrorLogIsOn.ValueBool);
        }
    }
}
```

### Выброс сигнала (звук + лог)

```csharp
private void DropSignal(MoveData myData, string signalName, string soundType, bool errorLogTo)
{
    PlaySound(soundType);

    string message = signalName + " " + myData.Tab.Connector.SecurityName + "\n";
    message += "Time: " + myData.Time + "\n";
    message += "Move percent now: " + myData.MoveUp + "\n";

    LogMessageType msgType = errorLogTo ? LogMessageType.Error : LogMessageType.User;
    myData.Tab.SetNewLogMessage(message, msgType);
}

private void PlaySound(string soundName)
{
    try
    {
        UnmanagedMemoryStream stream = Resources.Bird;
        if (soundName == "Duck") stream = Resources.Duck;
        if (soundName == "Wolf") stream = Resources.wolf01;

        if (stream != null)
        {
            SoundPlayer player = new SoundPlayer(stream);
            player.Play();
        }
    }
    catch { /* ignore */ }
}
```

---

## 1.6 Торговая логика

### Лимит позиций

```csharp
private void TryOpenLongPosition(List<Candle> candles, BotTabSimple tab)
{
    List<Position> positions = _tabScreener.PositionsOpenAll.FindAll(p => p.Direction == Side.Buy);
    if (positions.Count >= _longMaxPositions.ValueInt)
        return;

    // ... логика входа ...
    tab.BuyAtMarket(GetVolume(tab));
}
```

### Стоп и тейк при открытии (скринер)

```csharp
private void _tabScreener_PositionOpeningSuccesEvent(Position position, BotTabSimple tab)
{
    if (position.Direction == Side.Buy)
    {
        if (_longStopPercent.ValueDecimal != 0)
        {
            decimal stopPrice = position.EntryPrice
                - position.EntryPrice * (_longStopPercent.ValueDecimal / 100);
            tab.CloseAtStopMarket(position, stopPrice);
        }
        if (_longProfitPercent.ValueDecimal != 0)
        {
            decimal profitPrice = position.EntryPrice
                + position.EntryPrice * (_longProfitPercent.ValueDecimal / 100);
            tab.CloseAtProfitMarket(position, profitPrice);
        }
    }
    else if (position.Direction == Side.Sell)
    {
        // ... зеркально для шорта ...
    }
}
```

### Закрытие по времени — свечи

```csharp
// MonitorHighLow / MonitorVolume
int candlesInPosition = Convert.ToInt32(
    (tab.TimeServerCurrent - position.TimeOpen).TotalMinutes
    / tab.Connector.TimeFrameTimeSpan.TotalMinutes);

if (candlesInPosition > _longMaxCandlesInPositions.ValueInt)
{
    tab.CloseAtMarket(position, position.OpenVolume);
}
```

### Закрытие по времени — секунды

```csharp
// MonitorImpulse / MonitorRsi
int secondsInPosition = Convert.ToInt32(
    (tab.TimeServerCurrent - position.TimeOpen).TotalSeconds);

if (secondsInPosition > _longMaxSecondsInPositions.ValueInt)
{
    tab.CloseAtMarket(position, position.OpenVolume);
}
```

---

## 1.7 Неторговые периоды (`NonTradePeriods`)

```csharp
private NonTradePeriods _tradePeriodsSettings;
private StrategyParameterButton _tradePeriodsShowDialogButton;

public MyMonitor(string name, StartProgram startProgram) : base(name, startProgram)
{
    _tradePeriodsSettings = new NonTradePeriods(name);

    // Задать периоды по умолчанию
    _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
    _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 10, Minute = 5 };
    _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

    _tradePeriodsSettings.TradeInSunday = false;
    _tradePeriodsSettings.TradeInSaturday = false;
    _tradePeriodsSettings.Load();

    // Кнопка для открытия диалога настроек
    _tradePeriodsShowDialogButton = CreateParameterButton("Non trade periods");
    _tradePeriodsShowDialogButton.UserClickOnButtonEvent += (sender, e) =>
    {
        _tradePeriodsSettings.ShowDialog();
    };
}

private void MainLogic(List<Candle> candles, BotTabSimple tab)
{
    if (_tradePeriodsSettings.CanTradeThisTime(tab.TimeServerCurrent) == false)
        return;

    // ... торговая логика ...
}
```

| Свойство | Описание |
|----------|----------|
| `NonTradePeriod1Start` / `End` / `OnOff` | Первый период без торговли |
| `NonTradePeriod2...` / `NonTradePeriod3...` | Второй и третий периоды |
| `TradeInSunday` / `TradeInSaturday` | Торговать ли в выходные |
| `CanTradeThisTime(DateTime)` | Проверка: можно ли сейчас торговать |

---

## 1.8 Паттерны мониторинга

### 1.8.1 High/Low (`MonitorHighLow`)

Мониторит расстояние от текущей цены до High/Low за N свечей через индикатор `PriceChannel`.

```csharp
// Инициализация
_pc = IndicatorsFactory.CreateIndicatorByName("PriceChannel", name + "PC", false);
_pc = (Aindicator)tab.CreateCandleIndicator(_pc, "Prime");

// Расчёт
decimal maxPrice = _pc.DataSeries[0].Last;   // верхний канал
decimal minPrice = _pc.DataSeries[1].Last;   // нижний канал
decimal lastPrice = candles[^1].Close;

decimal moveUp = (maxPrice - lastPrice) / (maxPrice / 100);     // расстояние до High
decimal moveDown = (lastPrice - minPrice) / (minPrice / 100);   // расстояние до Low
```

### 1.8.2 Impulse (`MonitorImpulse`)

Мониторит ценовой импульс — максимальное движение за N свечей.

```csharp
decimal maxPrice = decimal.MinValue;
decimal minPrice = decimal.MaxValue;

for (int i = candles.Count - 1; i >= 0 && i > candles.Count - 1 - _candlesToAnalyze.ValueInt; i--)
{
    if (candles[i].High > maxPrice) maxPrice = candles[i].High;
    if (candles[i].Low < minPrice) minPrice = candles[i].Low;
}

decimal currentPrice = candles[^1].Close;
decimal movePercentUp = (currentPrice - minPrice) / (minPrice / 100);
decimal movePercentDown = (maxPrice - currentPrice) / (maxPrice / 100);
```

### 1.8.3 RSI (`MonitorRsi`)

Мониторит отклонение RSI от минимума/максимума за N свечей.

```csharp
_rsi = IndicatorsFactory.CreateIndicatorByName("RSI", name + "RSI", false);
_rsi = (Aindicator)tab.CreateCandleIndicator(_rsi, "Second");

// Находим min/max RSI за период
decimal maxRsi = decimal.MinValue;
decimal minRsi = decimal.MaxValue;
var rsiValues = _rsi.DataSeries[0].Values;

for (int i = rsiValues.Count - 1; i >= 0 && i > rsiValues.Count - 1 - _candlesToAnalyze.ValueInt; i--)
{
    if (rsiValues[i] > maxRsi) maxRsi = rsiValues[i];
    if (rsiValues[i] < minRsi) minRsi = rsiValues[i];
}

decimal currentRsi = rsiValues[^1];
decimal moveUp = currentRsi - minRsi;     // отклонение от минимума
decimal moveDown = maxRsi - currentRsi;   // отклонение от максимума
```

### 1.8.4 Volume Ranking (`MonitorVolume`)

Ранжирует бумаги по объёму и отслеживает сдвиг позиции в рейтинге.

```csharp
// Суммарный объём за N часов
decimal summVolume = 0;
foreach (var candle in candlesWindow)
{
    summVolume += candle.Center * candle.Volume;
}
if (security.Lot > 1) summVolume *= security.Lot;

// Ранжирование всех бумаг скринера по summVolume
var ranked = allVolumes.OrderByDescending(v => v.SummVolume).ToList();
for (int i = 0; i < ranked.Count; i++)
    ranked[i].Rank = i + 1;

// Сравнение с историческим рейтингом
int rankMove = historicalRank - currentRank;  // положительный = поднялась в рейтинге
```

> **Особенность**: `MonitorVolume` использует `CandlesSyncFinishedEvent` вместо `CandleFinishedEvent`, так как обрабатывает все табы скринера разом для корректного ранжирования.

---

## 1.9 Каталог Monitor-роботов

| Робот | Путь | Что мониторит | Закрытие по |
|-------|------|---------------|-------------|
| `MonitorHighLow` | `Monitors/MonitorHighLow.cs` | Расстояние до High/Low (PriceChannel) | Стоп/тейк % + свечи |
| `MonitorImpulse` | `Monitors/MonitorImpulse.cs` | Ценовой импульс (High/Low свечей) | Стоп/тейк % + секунды |
| `MonitorRsi` | `Monitors/MonitorRsi.cs` | Отклонение RSI от min/max | Стоп/тейк % + секунды |
| `MonitorVolume` | `Monitors/MonitorVolume.cs` | Объёмный рейтинг + Keltner Channel | Стоп/тейк % + свечи |

---

## 1.10 Типичные ошибки при создании Monitor

| № | Ошибка | Причина | Решение |
|---|--------|---------|---------|
| 1 | `InvalidOperationException` в `TryUpdateTable` | Обращение к `DataGridView` не из UI-потока | Использовать `InvokeRequired` + `Invoke` |
| 2 | Таблица не обновляется | Забыли вызвать `TryUpdateTable` после изменения данных | Вызывать в конце `MainLogic` и в событиях позиций |
| 3 | Дублирование алертов | Нет проверки `signal.Time == candles[^1].TimeStart` | Использовать `Dictionary<string, SignalData>` |
| 4 | Робот торгует, когда не должен | Нет проверки `_tradePeriodsSettings.CanTradeThisTime()` | Проверять перед входом |
| 5 | `NullReferenceException` в `MainLogic` | `tab.CandlesAll == null` или `tab.IsConnected == false` | Проверять `IsConnected`, `IsReadyToTrade`, `Count >= N` |
| 6 | Таблица мерцает/тормозит | Обновление на каждом тике без rate limiting | `AddSeconds(1)` guard |
| 7 | Неправильное закрытие по времени | Путаница `TotalMinutes` vs `TotalSeconds` | `MonitorHighLow` → `TotalMinutes / TimeFrame`; `MonitorImpulse` → `TotalSeconds` |
| 8 | Нулевой объём | `GetVolume` не учитывает `Lot` или `PriceStepCost` | Копировать стандартный `GetVolume` из `CONTEXT_POSITIONS_AND_RISK.md` |
| 9 | Сигнал не приходит | `_upSignalsIsOn.ValueBool == false` по умолчанию | Проверять включение сигналов в параметрах |
| 10 | Позиция открывается повторно | Нет проверки `tab.PositionsOpenAll.Count > 0` | Проверять перед входом |

---

## 1.11 Шпаргалка

**Создать таблицу:**
```csharp
CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab(" Monitor ");
CreateColumnsTable();
customTab.AddChildren(_hostTable);
```

**Rate-limited обновление:**
```csharp
if (_lastTimeUpdateTable.AddSeconds(1) > DateTime.Now) return;
```

**Дедупликация сигнала:**
```csharp
if (signal.Time == candles[^1].TimeStart) return;
signal.Time = candles[^1].TimeStart;
```

**Проверка неторгового периода:**
```csharp
if (_tradePeriodsSettings.CanTradeThisTime(tab.TimeServerCurrent) == false) return;
```

**Закрытие по свечам:**
```csharp
int candlesInPos = (int)((tab.TimeServerCurrent - pos.TimeOpen).TotalMinutes
    / tab.Connector.TimeFrameTimeSpan.TotalMinutes);
```

**Закрытие по секундам:**
```csharp
int secondsInPos = (int)(tab.TimeServerCurrent - pos.TimeOpen).TotalSeconds;
```

**Показать график из таблицы:**
```csharp
int tabNumber = _tabScreener.Tabs.IndexOf(tab);
_tabScreener.ShowChart(tabNumber);
```

---

> **Связанные файлы контекста:**
> - `CONTEXT.md` — базовая архитектура, выбор типа таба
> - `CONTEXT_POSITIONS_AND_RISK.md` — стопы, тейки, объём, закрытие позиций
> - `CONTEXT_INDICATORS.md` — создание и использование индикаторов
