# CONTEXT_CODING_GUIDELINES — Правила написания кода в OsEngine

> Назначение: собрать соглашения, паттерны и правила разработки **кодовой базы самого движка OsEngine** (не торговых роботов на нём), чтобы новый код был консистентен с существующей кодовой базой и не ломал архитектуру движка.
>
> Правила для написания торговых роботов описаны отдельно в `CONTEXT_ROBOTS_ARCHITECTURE.md` и `CONTEXT_ROBOTS.md`.

---

## 1. Общие принципы

### 1.1. Преамбула у классов (кроме UI)

Каждый файл `.cs`, содержащий класс логики движка, должен начинаться с преамбулы, содержащей ссылку на лицензию:

```csharp
/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
```

**Исключения:** файлы `.xaml` и `.xaml.cs` преамбулу не требуют.

### 1.2. Разделение по регионам

Если класс содержит больше пяти методов, группируйте их по смыслу с помощью `#region` / `#endregion`. Главный архитектор предпочитает 2–3 крупных класса с чёткими смысловыми регионами десяткам мелких вспомогательных классов.

Пример:

```csharp
#region Constructors

public MyClass() { }

#endregion

#region Public methods

public void DoWork() { }

#endregion

#region Event handlers

private void OnTick() { }

#endregion
```

### 1.3. Минимальные изменения существующего кода

Не переписывай ядро движка без крайней необходимости. Если нужно добавить новое поведение, создавай адаптер или расширение, а не меняй существующие классы. Сохраняй обратную совместимость форматов файлов настроек и публичных API.

### 1.4. Использование существующих абстракций движка

Используй готовые абстракции: `BotPanel`, `BotTabSimple`, `IServer`, `AServer`, `ServerMaster`, `Aindicator`, `Journal`, `RiskManager`. Не создавай параллельную логику для управления позициями, ордерами, серверами или данными, если эта функциональность уже реализована в движке.

### 1.5. Потокобезопасность как обязательное требование

События от биржевых серверов приходят из фоновых потоков. Любое общее изменяемое состояние должно быть защищено `lock` или потокобезопасной коллекцией (`ConcurrentQueue`, `ConcurrentDictionary`). Обработчики событий должны быть короткими и реентерабельными.

```csharp
private readonly object _locker = new object();

public void AddItem(string item)
{
    lock (_locker)
    {
        _items.Add(item);
    }
}
```

### 1.6. Логирование всех исключений

Все исключения должны логироваться через `SendNewLogMessage(message, LogMessageType.Error)`. Пустые блоки `catch` запрещены. В обработчиках событий от табов и серверов оборачивай логику в `try-catch`.

```csharp
try
{
    // логика
}
catch (Exception error)
{
    SendNewLogMessage(error.ToString(), LogMessageType.Error);
}
```

---

## 2. Структура проекта и namespaces

### 2.1. Текущая структура проекта

Корень `OsEngine/` разбит на функциональные папки верхнего уровня. Каждая папка отвечает за законченную область: рыночные данные, торговлю, оптимизацию, логирование, графики и т.д. Не смешивай код разных модулей в одной папке без явной необходимости.

### 2.2. Текущие крупные модули

| Папка | Модуль | Назначение |
|-------|--------|-----------|
| `OsData/` | OsData | Загрузка и хранение исторических данных |
| `OsOptimizer/` | Optimizer | Оптимизация параметров стратегий |
| `OsConverter/` | Converter | Конвертация тиков в свечи |
| `OsTrader/` | Trader | Торговая станция, роботы, табы, риск-менеджмент |
| `Journal/` | Journal | Журнал сделок и статистика |
| `Market/` | Market | Серверы бирж, коннекторы, `ServerMaster` |
| `Logging/` | Logging | Логирование и оповещения |
| `Charts/` | Charts | Отрисовка графиков и индикаторов |
| `Entity/` | Entity | Базовые сущности: свечи, ордера, позиции |

### 2.3. При создании нового функционала выбираем папку модуля

Новый код должен попадать в папку того модуля, к которому он логически относится. Не создавай отдельные общие папки в корне проекта, если функциональность можно разместить внутри существующего модуля.

### 2.4. Соответствие namespace и пути к файлу

`namespace` должен совпадать с путём к файлу относительно папки `OsEngine/`.

```csharp
// Файл: OsEngine/Market/Servers/Binance/BinanceServerSpot.cs
namespace OsEngine.Market.Servers.Binance

// Файл: OsEngine/OsTrader/Panels/Tab/BotTabSimple.cs
namespace OsEngine.OsTrader.Panels.Tab
```

### 2.5. Где размещать новый код движка

| Что добавляем | Где размещать |
|---------------|---------------|
| MCP API | `OsEngine/MCP/` или рядом с `MainWindow` |
| Новый биржевой сервер | `OsEngine/Market/Servers/<ИмяБиржи>/` |
| UI-окно общего назначения | `OsEngine/OsTrader/Gui/`, `OsEngine/Entity/` или папка модуля |
| Новая сущность | `OsEngine/Entity/` |
| Новый индикатор | `OsEngine/Indicators/Scripts/` |

### 2.6. Исключения из правил namespace

В кодовой базе есть исторические исключения, которые не нужно копировать:

- `Candles/*.cs` → namespace `OsEngine.Entity`
- `App.xaml.cs`, `MainWindow.xaml.cs` → namespace `OsEngine`

Для новых файлов всегда предпочитай соответствие пути и namespace.

---

## 3. Naming conventions

### 3.1. Регионы

Название региона должно начинаться с заглавной буквы и быть на английском языке.

```csharp
#region Public methods

public void DoWork() { }

#endregion
```

### 3.2. Общие правила именования

| Элемент | Стиль | Пример |
|---------|-------|--------|
| Классы | `PascalCase` | `BotPanel`, `ServerMaster` |
| Методы | `PascalCase` | `CreateParameter`, `StartServer` |
| Свойства | `PascalCase` | `ServerStatus`, `NameStrategyUniq` |
| Публичные поля | `PascalCase` | `CandlesAll`, `PositionsOpenAll` |
| Приватные поля | `_camelCase` | `_tab`, `_riskManager` |
| Перечисления | `PascalCase` | `ServerType`, `OrderStateType` |

### 3.3. Интерфейсы

Интерфейсы именуются с префиксом `I`.

```csharp
public interface IServer { }
public interface IIndicator { }
```

### 3.4. События и обработчики

События имеют суффикс `Event`. Обработчики именуются по шаблону `источник_ИмяСобытия`.

```csharp
public event Action<List<Candle>> CandleFinishedEvent;

private void _tab_CandleFinishedEvent(List<Candle> candles) { }
```

### 3.5. Локеры

Локеры именуются в `_camelCase` или как именованная строка.

```csharp
private object _lockerManualReload = new object();
private string _serversArrayLocker = "_serversArrayLocker";
```

### 3.6. Параметры и локальные переменные

Параметры методов и локальные переменные используют `camelCase`.

```csharp
public void CreateOrder(string symbol, decimal price, decimal volume)
{
    int userNumber = GetNextUserNumber();
}
```

### 3.7. Именование контролов в WPF

Имя любого элемента управления в XAML должно начинаться с типа контрола, за которым следует описание.

```xml
<ComboBox x:Name="ComboBoxServerType" />
<TextBox x:Name="TextBoxApiKey" />
<Button x:Name="ButtonStart" />
<DataGrid x:Name="DataGridPositions" />
<CheckBox x:Name="CheckBoxAutoStart" />
```

Это правило упрощает поиск контролов в коде и делает их назначение очевидным.

---

## 4. Работа с WPF и UI-потоком

### 4.1. Подписка на события контролов — внутри кода на C#

Подписка на события элементов управления выполняется в коде C#, а не в XAML. Это упрощает поиск обработчиков и гарантирует контроль над отпиской.

```csharp
public MyWindow()
{
    InitializeComponent();
    ButtonStart.Click += ButtonStart_Click;
    Closing += MyWindow_Closing;
}
```

### 4.2. Обработка события закрытия окна

При закрытии окна необходимо:

- отписаться от всех событий;
- вызвать `Dispose()` для ресурсов;
- обнулить ссылки на все объекты.

```csharp
private void MyWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
{
    try
    {
        ButtonStart.Click -= ButtonStart_Click;
        _controller.NewDataEvent -= Controller_NewDataEvent;

        _controller?.Dispose();
        _controller = null;
    }
    catch (Exception error)
    {
        ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
    }
}
```

### 4.3. Все методы и события окна обёрнуты в try-catch

Любое событие окна и публичный метод должны содержать `try-catch`. Ошибки направляются в `ServerMaster.SendNewLogMessage`.

```csharp
private void ButtonStart_Click(object sender, RoutedEventArgs e)
{
    try
    {
        StartProcess();
    }
    catch (Exception error)
    {
        ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
    }
}
```

### 4.4. Очистка таблиц DataGrid

Для `DataGridView` используется следующая последовательность:

```csharp
if (_grid != null)
{
    _grid.Click -= _grid_Click;
    _grid.DoubleClick -= _grid_DoubleClick;
    _grid.DataError -= _grid_DataError;

    DataGridFactory.ClearLinks(_grid);

    _grid.Rows.Clear();
    _grid.Columns.Clear();
    _grid.DataSource = null;
    _grid.Dispose();
    _grid = null;
}
```

Если таблица использует `DataGridFactory.GetDataGridView`, обязательно вызывай `DataGridFactory.ClearLinks` перед освобождением ресурсов.

### 4.5. UI только из UI-потока

Любое обращение к WPF-элементам из фонового потока должно выполняться через `Dispatcher.Invoke`. Перед вызовом проверяй `Dispatcher.CheckAccess()`.

```csharp
if (!myControl.Dispatcher.CheckAccess())
{
    myControl.Dispatcher.Invoke(new Action(UpdateUI));
    return;
}
UpdateUI();
```

### 4.6. InitializeComponent в конструкторе

Первым вызовом в конструкторе `.xaml.cs` должен быть `InitializeComponent()`.

```csharp
public MyWindow()
{
    InitializeComponent();
    // дальнейшая инициализация
}
```

### 4.7. Использование WindowsFormsHost

Для встраивания WinForms-контролов в WPF используется `WindowsFormsHost`. При очистке сначала отписывайся от событий, затем обнуляй `Child`, затем освобождай сам `WindowsFormsHost`.

```csharp
if (_host != null)
{
    _host.Child = null;
    _host.Dispose();
    _host = null;
}
```

---

## 5. Потокобезопасность

### 5.1. Асинхронные методы — подозрительны

Если метод выполняется асинхронно (`async`/`await`), скорее всего это ошибка архитектуры. OsEngine построен на синхронной обработке событий и фоновых потоках. `async void` особенно опасен, так как скрывает исключения и усложняет отладку.

### 5.2. Асинхронные методы только с явного разрешения

Создавать асинхронные методы можно только после согласования с ведущим разработчиком. В подавляющем большинстве случаев синхронный код с фоновыми потоками предпочтительнее.

### 5.3. Потоки и задачи направляются только на методы с try-catch

Любой метод, запускаемый в новом потоке или `Task`, должен содержать `try-catch` на верхнем уровне. Необработанное исключение в фоновом потоке падает молча и может нарушить работу движка.

```csharp
Task.Run(() =>
{
    try
    {
        DoBackgroundWork();
    }
    catch (Exception error)
    {
        ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
    }
});
```

### 5.4. Запрещены анонимные делегаты для событий

Анонимные делегаты (`+= (sender, e) => { }`) нельзя отписать, что приводит к утечкам памяти. Всегда используй именованные методы.

```csharp
// Плохо
_server.NewTickEvent += (tick) => { Process(tick); };

// Хорошо
_server.NewTickEvent += Server_NewTickEvent;
```

### 5.5. Общее изменяемое состояние защищать lock

Любое изменяемое состояние, к которому обращаются несколько потоков, должно быть защищено `lock` или потокобезопасной коллекцией.

```csharp
private readonly object _locker = new object();
private List<Order> _orders = new List<Order>();

public void AddOrder(Order order)
{
    lock (_locker)
    {
        _orders.Add(order);
    }
}
```

### 5.6. Обработчики событий должны быть короткими

Обработчики событий от серверов и табов должны выполняться быстро. Тяжёлые вычисления выноси в отдельный фоновый поток или очередь.

### 5.7. Статические коллекции — источник утечек и гонок

Статические коллекции доступны из всех потоков и живут до завершения процесса. Их модификация без синхронизации опасна. Предпочитай `ConcurrentBag`, `ConcurrentDictionary` или защищай доступ `lock`.

### 5.8. Проверка CheckAccess перед обращением к UI

Перед обновлением UI из фонового потока проверяй `Dispatcher.CheckAccess()`. Маршалинг через `Dispatcher.Invoke` нужен только если поток не является UI-потоком.

```csharp
if (!myControl.Dispatcher.CheckAccess())
{
    myControl.Dispatcher.Invoke(new Action(UpdateUI));
    return;
}
UpdateUI();
```

---

## 6. Логирование

### 6.1. Новые модули используют встроенный лог

При создании нового модуля используй класс `Log` из `OsEngine.Logging`. Это обеспечивает единообразие хранения, ротацию и отображение логов.

```csharp
private Log _log;

public MyModule(string name)
{
    _log = new Log(name);
    _log.StartPaint();
}
```

### 6.2. Проброс события при встраивании в существующий модуль

Если класс встраивается в существующую иерархию, пробрасывай событие `LogMessageEvent`. Если обработчик не подключён, а тип сообщения — ошибка — направляй её в `ServerMaster`.

```csharp
public event Action<string, LogMessageType> LogMessageEvent;

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
```

### 6.3. Отсутствие штатного лога — запись в ServerMaster

Если в объекте нет собственного лога и нет события для проброса сообщения выше, ошибки направляй в общий лог через `ServerMaster.SendNewLogMessage`.

```csharp
catch (Exception error)
{
    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
}
```

### 6.4. Уровни логов

| Уровень | Когда использовать |
|---------|-------------------|
| `Error` | Исключения и критические ошибки |
| `System` | Системные сообщения о работе движка |
| `Connect` | Подключение/отключение серверов |
| `Trade` | Исполнение сделок |
| `Signal` | Сигналы торговых стратегий |
| `User` | Действия пользователя |

### 6.5. Не логировать в оптимизаторе

В режиме `StartProgram.IsOsOptimizer` логирование отключено или должно быть минимальным. Не пиши каждое событие в лог — это сильно замедляет оптимизацию.

### 6.6. Формат сообщений об ошибках

Логируй полную информацию об ошибке: `error.ToString()`. `error.Message` недостаточно, так как теряется stack trace и внутренние исключения.

```csharp
// Плохо
SendNewLogMessage(error.Message, LogMessageType.Error);

// Хорошо
SendNewLogMessage(error.ToString(), LogMessageType.Error);
```

---

## 7. Обработка ошибок

### 7.1. Подписка на ошибки DataGridView

Для каждого `DataGridView` обязательно подписывайся на `DataError` и записывай ошибку в лог.

```csharp
_grid.DataError += _grid_DataError;

private void _grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
{
    ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
}
```

### 7.2. Каждое событие окна WPF обёрнуто в try-catch

В проекте принято строгое правило: любой обработчик событий в WPF-окне должен быть обёрнут в `try-catch`. Исключений нет.

```csharp
private void ButtonStart_Click(object sender, RoutedEventArgs e)
{
    try
    {
        StartProcess();
    }
    catch (Exception error)
    {
        ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
    }
}
```

### 7.3. Не проглатывать исключения

Пустые блоки `catch` запрещены. Если исключение перехвачено, оно должно быть обработано или, как минимум, залогировано.

```csharp
// Плохо
try
{
    DoWork();
}
catch (Exception)
{
    // ошибка потеряна
}

// Хорошо
try
{
    DoWork();
}
catch (Exception error)
{
    ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
}
```

### 7.4. Все события от Chart обёрнуты в try-catch

События от графических контролов (`Chart`, `ChartArea`, `Series` и др.) так же, как и события WPF-окон, должны быть обёрнуты в `try-catch`.

```csharp
private void Chart_Click(object sender, EventArgs e)
{
    try
    {
        ProcessChartClick();
    }
    catch (Exception error)
    {
        ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
    }
}
```

---

## 8. Правила комментирования в коде

### 8.1. WPF класс с кодом окна не должен содержать комментариев в теле класса

В файлах `.xaml.cs` не должно быть комментариев вне методов — в теле класса. Логика окна должна быть понятна из имён методов, полей и контролов.

### 8.2. В теле WPF-класса не должно быть русских комментариев

Если комментарий в теле WPF-класса всё же необходим (например, XML-документация), он не должен быть на русском языке. Русскоязычные комментарии допустимы только внутри методов.

### 8.3. Комментарии на русском допустимы внутри реализации методов

Внутри методов комментарии на русском языке допустимы и считаются хорошим стилем, если они поясняют нетривиальную логику.

```csharp
private void ProcessTick(Trade trade)
{
    // если торговля отключена, игнорируем тик
    if (_regime.ValueString == "Off") return;

    // обновляем лучшие цены для расчёта проскальзывания
    UpdateBestPrices(trade);
}
```

### 8.4. Не комментировать очевидное

Не пиши комментарии к очевидному коду. Комментарий должен объяснять «почему», а не «что».

```csharp
// Плохо
int i = 0; // создаём счётчик

// Хорошо
int i = 0; // индекс начинается с нуля, т.к. первая строка — заголовок
```

---

*Файл `CONTEXT_CODING_GUIDELINES.md` содержит базовые правила разработки кодовой базы OsEngine. При изменении архитектуры, появлении новых паттернов или обнаружении устоявшихся практик в коде документ следует обновлять.*
