# CONTEXT_MCP — MCP API в OsEngine

> Этот файл — справочник по MCP API для AI-агентов. Здесь описано, как вызывать существующие инструменты, как обрабатывать ответы и события, и какие правила действуют при работе с MCP.

---

## 1. Введение

MCP API — это HTTP + JSON-RPC 2.0 + Server-Sent Events (SSE) интерфейс, встроенный в главное окно OsEngine (`MainWindow`). Он реализован в формате **Anthropic Model Context Protocol (MCP)**: все операции выполняются через инструменты (`tools/call`), а список инструментов возвращает `tools/list`.

Код модуля находится в `OsEngine/MCP/`:

```
OsEngine/MCP/
  McpMaster.cs              // HTTP-сервер, маршрутизация JSON-RPC, SSE
  McpSettings.cs            // Загрузка/сохранение настроек хоста
  McpPrimeSettings.cs       // Модель настроек терминала для MCP
  McpLogEntry.cs            // Запись MCP-лога
  McpTerminalStatus.cs      // Модель статуса терминала
  McpApiUi.xaml/.xaml.cs    // Окно управления API
  Json/                     // DTO JSON-RPC и SSE
    McpJsonRpcRequest.cs
    McpJsonRpcResponse.cs
    McpJsonRpcError.cs
    McpEvent.cs
    McpTool.cs              // Описание инструмента MCP
  Modules/                  // Обработчики инструментов
    IMcpToolProvider.cs     // Интерфейс провайдера инструментов
    TerminalApi.cs          // terminal_*, terminal.* events
    LogsApi.cs              // log_get_*
    SettingsApi.cs          // prime_settings_*, prime_settings.changed
    McpConfigApi.cs         // mcp_settings_*
    ServerManagementApi.cs  // server_management_*
    ServerInstanceApi.cs    // server_instance_*
    WikiRobotsApi.cs        // wiki_robots_list, wiki_robot_info
    WikiIndicatorsApi.cs    // wiki_indicators_list, wiki_indicator_info
    WikiSecuritiesApi.cs    // wiki_securities_*, wiki_securities_mapping_info
    WikiDividendsApi.cs     // wiki_dividends_get_history, wiki_dividends_get_future, wiki_dividends_get_past, wiki_dividends_search_by_date
    OsDataApi.cs            // data_*
    RobotsApi.cs            // bot_* (robot management in any mode with robots)
    TesterApi.cs            // tester_* configuration
    McpProtocolApi.cs       // initialize, tools/list, tools/call, notifications/initialized
```

Тестовый стенд находится в `Tests/McpTestStand/OsEngine.McpApi.TestStand/`:

```
Tests/McpTestStand/OsEngine.McpApi.TestStand/
  Program.cs                // Точка входа, запуск OsEngine, оркестрация
  McpApiClient.cs           // Синхронный HTTP-клиент
  TestContext.cs            // Контекст прогона и печать отчёта
  TestRunner.cs             // Запуск модульных тестов
  TestResult.cs             // Результат одного теста
  Tests/                    // Модульные тесты
    ProtocolTests.cs
    TerminalTests.cs
    LogsTests.cs
    SettingsTests.cs
    ConfigTests.cs
    ServerManagementTests.cs
    ServerInstanceTests.cs
    WikiRobotsTests.cs
    WikiIndicatorsTests.cs
    WikiSecuritiesTests.cs
    WikiDividendsTests.cs
    SseTests.cs
    ErrorTests.cs
    DataTests.cs
    TesterTests.cs          // tester_* and bot_* via tester mode
```

---

## 2. Как пользоваться MCP API

### 2.1. Endpoint'ы

- **JSON-RPC:** `POST http://localhost:<port>/api/v1/mcp`
- **SSE:** `GET http://localhost:<port>/api/v1/events`

Порт по умолчанию: `6500`. Хранится в `Engine\McpSettings.txt`.

### 2.2. Авторизация

Все запросы должны содержать заголовок:

```http
X-Api-Key: <ключ>
```

По умолчанию ключ: `osengine-mcp-default-key`.

### 2.2.1. Золотой путь для ИИ-агентов

> Этот раздел написан, чтобы ИИ-агенты не путались при вызове API прямо из чата.

1. **Хост обязательно `localhost`, а не `127.0.0.1`.**  
   HTTP Listener OsEngine регистрирует префикс `http://localhost:6500/`. Запрос на `http://127.0.0.1:6500/...` вернёт `400 Bad Request - Invalid Hostname`.

2. **Все инструменты вызываются только через `tools/call`.**  
   Прямой вызов `terminal_get_status`, `ping` и т.п. вернёт ошибку `-32601`. В запросе обязательно должно быть поле `id`, иначе сервер посчитает запрос notification и вернёт `202 Accepted` с пустым телом.

3. **Полезная нагрузка лежит в `result.Content[0].Text`.**  
   Это вложенная JSON-строка. Её нужно распарсить отдельно. Внутри строки кавычки могут быть экранированы как `\u0022`, поэтому `grep`/`sed`/`awk` не подходят для извлечения полей.

4. **Из Git Bash используйте готовый скрипт `OsEngine/bin/Debug/mcp_call.sh`.**  
   Он формирует корректный JSON-RPC запрос (в том числе с кириллицей) и распаковывает `Content[0].Text` через PowerShell:

   ```bash
   cd OsEngine/bin/Debug
   ./mcp_call.sh tester_get_status
   ```

5. **Если вызываете `curl` вручную, разбирайте ответ через PowerShell одной командой:**

   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"tester_get_status","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp | powershell -Command '$r = $input | ConvertFrom-Json; $r.result.Content[0].Text | ConvertFrom-Json | ConvertTo-Json -Depth 10'
   ```

6. **Не создавайте временные `.py` / `.sh` / `.ps1` файлы.**  
   MCP API существует для того, чтобы ИИ делал запросы к OsEngine напрямую из чата. Для повторяющихся проверок используйте `mcp_call.sh` или `curl | powershell`, а не самописные скрипты.

### 2.3. JSON-RPC endpoint: только протокольные методы

JSON-RPC endpoint принимает только методы MCP-протокола:

- `initialize` — handshake.
- `tools/list` — список инструментов.
- `tools/call` — вызов инструмента.
- `notifications/initialized` — notification без ответа.

Прямой вызов `terminal_get_status` или `ping` вернёт ошибку `-32601`.

**Пример запроса:**

```json
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "terminal_get_status",
    "arguments": {}
  },
  "id": 1
}
```

**Успешный ответ:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      { "Type": "text", "Text": "{\"mode\":\"IsMainWindow\",...}" }
    ],
    "IsError": false
  },
  "error": null,
  "id": 1
}
```

**Ответ с ошибкой инструмента:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      { "Type": "text", "Text": "Tool 'unknown_tool' not found" }
    ],
    "IsError": true
  },
  "error": null,
  "id": 1
}
```

### 2.4. Доступные инструменты

Полный список возвращает `tools/list`. Каждый инструмент вызывается через `tools/call` с параметрами `name` и `arguments`.

| Инструмент | Описание |
|------------|----------|
| `ping` | Проверка доступности API |
| `terminal_get_status` | Текущий статус терминала |
| `terminal_launch` | Перезапуск терминала в указанном режиме. Текущий процесс OsEngine завершается, запускается новый с нужным аргументом (`-tester`, `-testerlight`, `-robots`, `-robotslight`, `-data`, `-optimizer`, `-converter`). Режимы `robots` и `robotslight` эквивалентны `trader` / `trader_light` |
| `terminal_stop` | Корректная остановка терминала: закрывает активное окно режима, затем `MainWindow`, и завершает процесс |
| `terminal_kill` | Принудительное завершение процесса OsEngine |
| `terminal_open_mode` | Открыть режим (`tester`, `testerlight`, `robots`, `robotslight`, `data`, `optimizer`, `converter`) из запущенного `MainWindow` без перезапуска процесса |
| `log_get_emergency_log` | Последние записи emergency-лога |
| `log_get_mcp_log` | Последние записи лога MCP |
| `prime_settings_get` | Общие настройки терминала |
| `prime_settings_set` | Изменить общие настройки терминала |
| `mcp_settings_get` | Настройки хоста MCP |
| `mcp_settings_set` | Изменить настройки хоста MCP |
| `server_management_get_list` | Список развёрнутых биржевых коннекторов (имя, тип, статус) |
| `server_management_activate` | Активация сервера указанного типа: загрузка сохранённых экземпляров и создание основного |
| `server_management_get_trade_connectors` | Список типов коннекторов, доступных для торговли |
| `server_management_get_data_connectors` | Полный список типов коннекторов, доступных для загрузки рыночных данных |
| `server_management_get_connector_permissions` | Полные разрешения коннектора (`IServerPermission`): таймфреймы данных, торговые права, плечо, время жизни ордеров и пр. |
| `server_instance_get_params` | Получить параметры конкретного экземпляра сервера. Пароли маскируются |
| `server_instance_set_params` | Установить параметры конкретного экземпляра сервера |
| `server_instance_create` | Создать новый экземпляр коннектора указанного типа (если поддерживаются множественные экземпляры) |
| `server_instance_delete` | Удалить экземпляр коннектора по типу и номеру. Номер 0 защищён от удаления |
| `server_instance_connect` | Подключить конкретный экземпляр сервера |
| `server_instance_disconnect` | Отключить конкретный экземпляр сервера |
| `server_instance_get_status` | Текущий статус подключения экземпляра сервера |
| `server_instance_get_securities` | Список бумаг экземпляра сервера (с фильтрами по классу/коду) |
| `server_instance_get_portfolios` | Список портфелей и позиций экземпляра сервера |
| `server_instance_get_log` | Последние записи журнала экземпляра сервера |
| `wiki_robots_list` | Список доступных роботов: имя класса, описание, источники, индикаторы. Использует кэш `BotsDescription.txt`, недостающих роботов догружает live |
| `wiki_robot_info` | Подробная информация по одному роботу (live-экземпляр): описание, источники, индикаторы и полный набор параметров |
| `wiki_indicators_list` | Список доступных индикаторов: имя класса, отображаемое имя, расположение, описание, количество параметров и серий. Использует кэш `IndicatorsDescription.json`, недостающих догружает live |
| `wiki_indicator_info` | Подробная информация по одному индикатору (live-экземпляр): описание, параметры и выходные серии |
| `wiki_securities_moex_iss` | Справочник бумаг MOEX ISS из `Wiki/moex_iss_securities.md`. Поддерживает фильтр по подстроке |
| `wiki_securities_tinvest` | Справочник бумаг TInvest из `Wiki/tinvest_securities.md`. Поддерживает фильтр по подстроке |
| `wiki_securities_alor` | Справочник бумаг Alor из `Wiki/alor_securities.md`. Поддерживает фильтр по подстроке |
| `wiki_securities_qscalp` | Справочник бумаг QScalp из `Wiki/qscalp_securities.md`. Поддерживает фильтр по подстроке |
| `wiki_securities_mapping_info` | Универсальный поиск бумаги по всем справочникам. Принимает `query` (имя/тикер/часть названия), опциональный `connector`, `limit` (default 50) и `exact`. Возвращает массив вариантов с metadata каждого коннектора |
| `wiki_dividends_get_history` | Исторические дивиденды российской акции из `Wiki/Dividends/{ticker}.md`. Параметры: `ticker` (обязательный), `date` (dd.MM.yyyy, по умолчанию сегодня), `refresh`. Возвращает записи с `registry_close_date <= date` |
| `wiki_dividends_get_future` | Ближайшая будущая запись дивидендов российской акции из `Wiki/Dividends/{ticker}.md`. Параметры: `ticker` (обязательный), `date` (dd.MM.yyyy, по умолчанию сегодня), `refresh`. Возвращает одну запись с `registry_close_date >= date` или `null` |
| `wiki_dividends_get_past` | Ближайшая прошлая запись дивидендов российской акции из `Wiki/Dividends/{ticker}.md`. Параметры: `ticker` (обязательный), `date` (dd.MM.yyyy, по умолчанию сегодня), `refresh`. Возвращает одну запись с `registry_close_date <= date` или `null` |
| `wiki_dividends_search_by_date` | Поиск дивидендов по конкретной дате закрытия реестра. Параметры: `ticker`, `date` (dd.MM.yyyy, обязательный), `refresh` |
| `data_get_sets` | Список существующих сетов данных OsData |
| `data_create_set` | Создать новый сет данных. Параметры: `name`, `source` (тип сервера), `source_name` (имя активного экземпляра), `timeframes` (массив строк), `date_from`, `date_to`. В ответе возвращает фактически активные таймфреймы сета |
| `data_delete_set` | Удалить сет данных по имени |
| `data_set_settings_get` | Получить настройки сета данных |
| `data_set_settings_set` | Частично обновить настройки сета. Доступны поля: `regime`, `timeframes`, `date_from`, `date_to`, `market_depth_depth` |
| `data_set_securities_get` | Получить список бумаг в сете данных |
| `data_set_securities_add` | Добавить бумаги в сет данных. Бумаги валидируются по справочнику активного сервера |
| `data_set_securities_remove` | Удалить бумаги из сета данных по именам |
| `data_set_on` | Включить сет данных (запустить загрузку) |
| `data_set_off` | Выключить сет данных (остановить загрузку) |
| `data_get_set_status` | Агрегированный статус загрузки сета: `regime`, `status`, `percent_load` |
| `data_get_security_status` | Статус загрузки конкретной бумаги и таймфрейма: `time_start`, `time_end`, `objects_count`, `percent_load`, `status` |
| `bot_get_list` | Список загруженных роботов |
| `bot_create` | Создать нового робота |
| `bot_delete` | Удалить робота |
| `bot_get_params` | Получить параметры созданного робота |
| `bot_set_params` | Установить параметры робота |
| `bot_get_sources` | Получить список источников (вкладок) робота |
| `bot_get_config_tab_simple` | Получить конфигурацию вкладки `BotTabSimple` |
| `bot_set_config_tab_simple` | Настроить вкладку `BotTabSimple` (сервер, портфель, эмулятор, комиссия, инструмент, свечи) |
| `bot_get_config_tab_screener` | Получить конфигурацию вкладки `BotTabScreener` (сервер, портфель, таймфрейм, список бумаг, созданные вкладки) |
| `bot_set_config_tab_screener` | Настроить вкладку `BotTabScreener` (сервер, портфель, эмулятор, таймфрейм, список бумаг). После изменения автоматически пересоздаются дочерние вкладки |
| `bot_get_config_tab_index` | Получить конфигурацию вкладки `BotTabIndex` (сервер, портфель, таймфрейм, список бумаг, формула, глубина расчёта, авто-формула) |
| `bot_set_config_tab_index` | Настроить вкладку `BotTabIndex` (сервер, портфель, таймфрейм, список бумаг, формула `A0+A1`, глубина расчёта, авто-формула). После изменения списка бумаг или общих настроек пересоздаются коннекторы |
| `bot_journal_get_settings` | Получить настройки журнала (группа, мультипликатор, включён) для одного или всех роботов |
| `bot_journal_set_settings` | Установить настройки журнала для роботов |
| `bot_journal_get_summary` | Сводка журнала: прибыль/убыток абс/%, диапазон дат, количество сделок. `bot_name` опционален (`null`/`""` — все роботы) |
| `bot_journal_get_equity` | Кривая эквити. Параметры: `bot_name`, `chart_type` (`Absolute`, `Percent1Contract`, `DepositPercent`) |
| `bot_journal_get_statistics` | Статистика журнала. Параметры: `bot_name`, `side` (`All`, `Long`, `Short`) |
| `bot_journal_get_drawdown` | Кривая просадки. Параметр `bot_name` опционален |
| `bot_journal_get_volume` | Объёмы торговли по бумагам/плечу. Параметр `bot_name` опционален |
| `bot_journal_get_open_positions` | Открытые позиции. Параметры: `bot_name`, `limit`, `offset` |
| `bot_journal_get_closed_positions` | Закрытые позиции. Параметры: `bot_name`, `include_failed`, `limit`, `offset` |

### 2.5. Примеры запросов

**initialize (Anthropic MCP handshake):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}},"id":1}' \
  http://localhost:6500/api/v1/mcp
```

**tools/list:**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/list","params":{},"id":2}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (ping):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"ping","arguments":{}},"id":3}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (terminal_get_status):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_get_status","arguments":{}},"id":4}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (terminal_launch):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_launch","arguments":{"mode":"data"}},"id":5}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (terminal_stop):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_stop","arguments":{}},"id":6}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (terminal_kill):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_kill","arguments":{}},"id":7}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (terminal_open_mode):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_open_mode","arguments":{"mode":"data"}},"id":8}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (log_get_mcp_log):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"log_get_mcp_log","arguments":{"count":5}},"id":8}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (prime_settings_set):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"prime_settings_set","arguments":{"reportCriticalErrors":false}},"id":9}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (mcp_settings_set):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"mcp_settings_set","arguments":{"isFullLogEnabled":true}},"id":10}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (server_management_get_list):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_management_get_list","arguments":{}},"id":11}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `server_management_get_list`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "[{\"name\":\"TInvest\",\"type\":\"TInvest\",\"status\":\"Disconnect\",\"number\":0}]"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 11
}
```

**tools/call (server_management_activate):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_management_activate","arguments":{"type":"TInvest"}},"id":12}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `server_management_activate`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "[{\"name\":\"TInvest\",\"type\":\"TInvest\",\"status\":\"Disconnect\",\"number\":0}]"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 12
}
```

**tools/call (server_instance_get_params):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_get_params","arguments":{"type":"TInvest","number":0}},"id":13}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `server_instance_get_params`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "[{\"name\":\"Token\",\"type\":\"Password\",\"value\":\"t.********Yw\",\"comment\":null},{\"name\":\"Акции\",\"type\":\"Bool\",\"value\":true,\"comment\":null}]"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 13
}
```

**tools/call (server_instance_create):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_create","arguments":{"type":"Binance"}},"id":14}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `server_instance_create`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{\"name\":\"Binance server 1\",\"type\":\"Binance\",\"status\":\"Disconnect\",\"number\":1}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 14
}
```

**tools/call (server_instance_delete):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_delete","arguments":{"type":"Binance","number":1}},"id":15}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `server_instance_delete`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{\"type\":\"Binance\",\"number\":1,\"deleted\":true}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 15
}
```

**tools/call (server_instance_connect):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_connect","arguments":{"type":"TInvest","number":7}},"id":16}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `server_instance_connect`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{\"type\":\"TInvest\",\"number\":7,\"command\":\"connect\",\"status\":\"Disconnect\"}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 16
}
```

**tools/call (server_instance_get_status):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_get_status","arguments":{"type":"TInvest","number":7}},"id":17}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `server_instance_get_status`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{\"type\":\"TInvest\",\"number\":7,\"status\":\"Connect\"}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 17
}
```

**tools/call (server_instance_get_securities):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_get_securities","arguments":{"type":"TInvest","number":7,"filter":"SBER"}},"id":18}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (server_instance_get_portfolios):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_get_portfolios","arguments":{"type":"TInvest","number":7}},"id":19}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (server_instance_get_log):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_get_log","arguments":{"type":"TInvest","number":7,"count":10}},"id":20}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (wiki_robots_list):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_robots_list","arguments":{"location":"All","include_engines":true}},"id":21}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (wiki_robot_info):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_robot_info","arguments":{"class_name":"Engine"}},"id":22}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (wiki_indicators_list):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_indicators_list","arguments":{"location":"All"}},"id":23}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (wiki_indicator_info):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_indicator_info","arguments":{"class_name":"Sma"}},"id":24}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (wiki_securities_tinvest):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_securities_tinvest","arguments":{"filter":"SBER"}},"id":25}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `wiki_securities_tinvest`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{\"securities\":[{\"schema\":\"tradeSecurity\",\"name\":\"SBER\",\"nameClass\":\"Stock rub\",\"nameFull\":\"Сбер Банк\",\"nameId\":\"e6123145-9665-43e0-8413-cd61b8aa9b13\",\"exchange\":\"moex_mrng_evng_e_wknd_dlr\",\"state\":\"Activ\",\"securityType\":\"Stock\"}],\"count\":1,\"connector\":\"TInvest\",\"collected_at\":\"2026-06-24T20:56:09.5640833+01:00\"}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 25
}
```

**tools/call (wiki_securities_mapping_info):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_securities_mapping_info","arguments":{"query":"Сбербанк"}},"id":26}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `wiki_securities_mapping_info`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{\"query\":\"\\u0421\\u0431\\u0435\\u0440\\u0431\\u0430\\u043d\\u043a\",\"total\":4,\"results\":[{\"connector\":\"TInvest\",\"connector_short\":\"tinvest\",\"is_trading_supported\":true,\"is_data_feed_supported\":true,\"security\":{\"schema\":\"tradeSecurity\",\"name\":\"SBER\",\"nameClass\":\"Stock rub\",\"nameFull\":\"\\u0421\\u0431\\u0435\\u0440 \\u0411\\u0430\\u043d\\u043a\",\"nameId\":\"e6123145-9665-43e0-8413-cd61b8aa9b13\",\"exchange\":\"moex_mrng_evng_e_wknd_dlr\",\"state\":\"Activ\",\"securityType\":\"Stock\"}},...]}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 26
}
```

**tools/call (wiki_dividends_get_history):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_dividends_get_history","arguments":{"ticker":"SBER"}},"id":27}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `wiki_dividends_get_history`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{\"ticker\":\"SBER\",\"date\":\"06.07.2026\",\"source\":\"https://smart-lab.ru/q/SBER/dividend/\",\"last_updated\":\"06.07.2026\",\"historical\":[{\"year\":2016,\"registry_close_date\":\"14.06.2017\",\"dividend_amount\":6,\"dividend_yield\":4.0},...],\"count\":8}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 27
}
```

**tools/call (wiki_dividends_get_history с date):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_dividends_get_history","arguments":{"ticker":"SBER","date":"01.01.2020"}},"id":28}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (wiki_dividends_get_future):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_dividends_get_future","arguments":{"ticker":"SBER"}},"id":29}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `wiki_dividends_get_future`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{\"ticker\":\"SBER\",\"date\":\"06.07.2026\",\"source\":\"https://smart-lab.ru/q/SBER/dividend/\",\"last_updated\":\"06.07.2026\",\"future\":{\"year\":2025,\"registry_close_date\":\"20.07.2026\",\"dividend_amount\":34.84,\"dividend_yield\":10.7}}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 29
}
```

**tools/call (wiki_dividends_get_past):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_dividends_get_past","arguments":{"ticker":"SBER","date":"01.01.2025"}},"id":30}' \
  http://localhost:6500/api/v1/mcp
```

**Пример ответа `wiki_dividends_get_past`:**

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{\"ticker\":\"SBER\",\"date\":\"01.01.2025\",\"source\":\"https://smart-lab.ru/q/SBER/dividend/\",\"last_updated\":\"06.07.2026\",\"past\":{\"year\":2024,\"registry_close_date\":\"18.07.2024\",\"dividend_amount\":35.17,\"dividend_yield\":10.8}}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 30
}
```

**tools/call (wiki_dividends_search_by_date):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_dividends_search_by_date","arguments":{"ticker":"SBER","date":"18.07.2025"}},"id":31}' \
  http://localhost:6500/api/v1/mcp
```

**notifications/initialized (notification, без `id`)**:

```bash
curl -s -o /dev/null -w "HTTP %{http_code}\n" -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (data_create_set):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_create_set","arguments":{"name":"TestSet","source":"MoexDataServer","source_name":"MoexDataServer","timeframes":["Min30"],"date_from":"2024-01-01T00:00:00","date_to":"2024-06-30T00:00:00"}},"id":27}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (data_set_securities_add):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_set_securities_add","arguments":{"name":"TestSet","securities":[{"name":"SBER"}]}},"id":28}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (data_set_on):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_set_on","arguments":{"name":"TestSet"}},"id":29}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (data_get_set_status):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_get_set_status","arguments":{"name":"TestSet"}},"id":30}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (tester_data_get_config):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"tester_data_get_config","arguments":{}},"id":31}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (tester_data_get_available_sets):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"tester_data_get_available_sets","arguments":{}},"id":32}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (tester_data_set_config):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"tester_data_set_config","arguments":{"source_type":"Set","set_name":"McpReleaseSet","type_tester_data":"Candle","date_from":"2024-01-01T00:00:00","date_to":"2024-06-30T00:00:00","delete_trades_from_memory":true}},"id":32}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (tester_execution_get_config):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"tester_execution_get_config","arguments":{}},"id":33}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (tester_execution_set_config):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"tester_execution_set_config","arguments":{"slippage_to_simple_order":0,"slippage_to_stop_order":2,"order_execution_type":"Intersection","non_trade_periods":[]}},"id":34}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (tester_portfolio_get_config):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"tester_portfolio_get_config","arguments":{}},"id":35}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (tester_portfolio_set_config):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"tester_portfolio_set_config","arguments":{"start_portfolio":1000000,"portfolio_calculation_enabled":true}},"id":36}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_get_list):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_get_list","arguments":{}},"id":37}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_create):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_create","arguments":{"strategy_name":"TwoTimeFramesBot"}},"id":38}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_delete):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_delete","arguments":{"bot_id":"TwoTimeFramesBot_1"}},"id":39}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_get_params):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_get_params","arguments":{"bot_id":"TwoTimeFramesBot_1"}},"id":40}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_set_params):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_set_params","arguments":{"bot_id":"TwoTimeFramesBot_1","parameters":{"PC length":25}}},"id":41}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_get_sources):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_get_sources","arguments":{"bot_id":"TwoTimeFramesBot_1"}},"id":42}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_get_config_tab_simple):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_get_config_tab_simple","arguments":{"bot_id":"TwoTimeFramesBot_1","tab_name":"TwoTimeFramesBot_1tab0"}},"id":43}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_set_config_tab_simple):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_set_config_tab_simple","arguments":{"bot_id":"TwoTimeFramesBot_1","tab_name":"TwoTimeFramesBot_1tab0","commission_type":"Percent","commission_value":0.01,"time_frame":"Min30","save_trades_in_candles":true,"build_non_trading_candles":true}},"id":44}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_get_config_tab_screener):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_get_config_tab_screener","arguments":{"bot_id":"AlgoStart1LinearRegression_1","tab_name":"AlgoStart1LinearRegression_1tab0"}},"id":46}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_set_config_tab_screener):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_set_config_tab_screener","arguments":{"bot_id":"AlgoStart1LinearRegression_1","tab_name":"AlgoStart1LinearRegression_1tab0","server_type":"Tester","server_name":"Tester","portfolio_name":"GodMode","emulator_is_on":true,"time_frame":"Min30","securities":[{"name":"SBER","class_name":"","is_on":true},{"name":"GAZP","class_name":"","is_on":true}]}},"id":47}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_get_config_tab_index):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_get_config_tab_index","arguments":{"bot_id":"IndexArbitrageClassic_1","tab_name":"IndexArbitrageClassic_1tab0"}},"id":48}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_set_config_tab_index):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_set_config_tab_index","arguments":{"bot_id":"IndexArbitrageClassic_1","tab_name":"IndexArbitrageClassic_1tab0","server_type":"Tester","server_name":"Tester","portfolio_name":"GodMode","emulator_is_on":true,"time_frame":"Min30","user_formula":"A0+A1","calculation_depth":1000,"auto_formula":{"regime":"Off","day_of_week":"Monday","hour":10,"sec_count":2,"days_look_back":20,"sort_type":"FirstInArray","mult_type":"PriceWeighted","write_log_on_rebuild":true},"securities":[{"name":"SBER","class_name":"","is_on":true},{"name":"VTBR","class_name":"","is_on":true}]}},"id":49}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_journal_get_summary):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_journal_get_summary","arguments":{"bot_name":"ParabolicBollinger"}},"id":45}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_journal_get_equity):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_journal_get_equity","arguments":{"bot_name":"ParabolicBollinger","chart_type":"DepositPercent"}},"id":46}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_journal_get_statistics):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_journal_get_statistics","arguments":{"bot_name":"ParabolicBollinger","side":"All"}},"id":47}' \
  http://localhost:6500/api/v1/mcp
```

**tools/call (bot_journal_get_closed_positions):**

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"bot_journal_get_closed_positions","arguments":{"bot_name":"ParabolicBollinger","include_failed":false,"limit":100,"offset":0}},"id":48}' \
  http://localhost:6500/api/v1/mcp
```

### 2.5.1. Авто-формула индекса

В инструментах `bot_get_config_tab_index` / `bot_set_config_tab_index` используется поле `auto_formula` — настройки автоматического перестроения формулы индекса. Все поля объекта опциональны; при `bot_set_config_tab_index` отсутствующие поля не меняют текущее значение.

| Поле | Тип | Допустимые значения | Описание |
|------|-----|---------------------|----------|
| `regime` | string | `Off`, `OncePerHour`, `OncePerDay`, `OncePerWeek` | Режим автоперестроения формулы |
| `day_of_week` | string | `Monday`..`Sunday` | День недели для перестроения (актуален для `OncePerWeek`) |
| `hour` | integer | 0..23 | Час дня для перестроения |
| `sec_count` | integer | ≥1 | Количество бумаг, отбираемых в индекс |
| `days_look_back` | integer | ≥1 | Глубина истории в днях для отбора бумаг |
| `sort_type` | string | `FirstInArray`, `VolumeWeighted`, `MaxVolatilityWeighted`, `MinVolatilityWeighted` | Критерий сортировки бумаг |
| `mult_type` | string | `PriceWeighted`, `VolumeWeighted`, `EqualWeighted`, `Cointegration` | Тип взвешивания бумаг в индексе |
| `write_log_on_rebuild` | boolean | true / false | Писать ли сообщение в лог при перестроении |

> `bot_get_config_tab_index` всегда возвращает полный объект `auto_formula` с актуальными значениями, даже если авто-формула отключена (`regime: Off`).

### 2.6. SSE-события

Подключение к потоку событий:

```bash
curl -N -H "X-Api-Key: osengine-mcp-default-key" \
  http://localhost:6500/api/v1/events
```

Формат одного события:

`terminal.launched` — payload содержит текущий статус терминала (`mode`, `version`, `processStarted`, `isMainWindowVisible`):

```
event: terminal.launched
data: {"event":"terminal.launched","timestamp":"2026-06-21T08:15:33","payload":{"mode":"IsMainWindow","version":"2.0.1.5","processStarted":"2026-06-21T08:15:30","isMainWindowVisible":true}}

```

`terminal.mode_changed` — payload содержит открытый режим (`Mode`), статус (`Status`) и время (`Time`):

```
event: terminal.mode_changed
data: {"event":"terminal.mode_changed","timestamp":"2026-06-21T08:15:33","payload":{"Mode":"IsTester","Status":{...},"Time":"2026-06-21T08:15:33"}}

```

**Реализованные события:**

| Событие | Когда публикуется |
|---------|-------------------|
| `terminal.launched` | При старте хоста и при подключении нового SSE-клиента |
| `terminal.stopped` | При корректном/аварийном завершении процесса |
| `terminal.mode_changed` | При открытии режима (Tester, Optimizer, OsData, OsTrader, Converter) |
| `prime_settings.changed` | При изменении настроек `PrimeSettingsMaster` |
| `server_instance.status_changed` | При изменении статуса подключения экземпляра сервера (Connect / Disconnect) |
| `server_instance.security.updated` | При обновлении списка бумаг экземпляра сервера |
| `server_instance.portfolio.updated` | При обновлении списка портфелей экземпляра сервера |
| `server_instance.log` | При новой записи в журнале экземпляра сервера |
| `data_set_load_completed_event` | Когда загрузка всего сета OsData завершена |
| `data_set_security_load_completed_event` | Когда завершена загрузка конкретной бумаги/таймфрейма в сете OsData |
| `heartbeat` | Каждые 5 секунд |

Примеры событий экземпляра сервера:

```
event: server_instance.status_changed
data: {"event":"server_instance.status_changed","timestamp":"2026-06-22T18:10:00","payload":{"type":"TInvest","number":7,"status":"Connect"}}

event: server_instance.security.updated
data: {"event":"server_instance.security.updated","timestamp":"2026-06-22T18:10:05","payload":{"type":"TInvest","number":7,"count":2464}}

event: server_instance.portfolio.updated
data: {"event":"server_instance.portfolio.updated","timestamp":"2026-06-22T18:10:06","payload":{"type":"TInvest","number":7,"count":1}}

event: server_instance.log
data: {"event":"server_instance.log","timestamp":"2026-06-22T18:10:07","payload":{"type":"TInvest","number":7,"message":"TInvest_7 Connect","messageType":"Connect"}}

```

### 2.7. Коды ошибок JSON-RPC

| Код | Назначение | Когда возникает |
|-----|------------|---------------|
| `-32700` | Parse error | Тело запроса не является валидным JSON |
| `-32600` | Invalid Request | Невалидный JSON-RPC запрос |
| `-32601` | Method not found | Метод не из набора `initialize`/`tools/list`/`tools/call` |
| `-32602` | Invalid params | Некорректные параметры |
| `-32603` | Internal error | Исключение внутри обработчика |

### 2.8. Настройки хоста

Настройки хранятся в `Engine\McpSettings.txt`:

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Port` | `int` | Порт HTTP-сервера |
| `ApiKey` | `string` | Ключ для заголовка `X-Api-Key` |
| `IsEnabled` | `bool` | Автоматически запускать хост при старте `MainWindow`. **По умолчанию `false`** — API выключено до явного включения пользователем |
| `IsFullLogEnabled` | `bool` | Логировать каждый запрос, тело, ответ и SSE-подключения |
| `AllowedIps` | `List<{Ip,Port}>` | Белый список IP-адресов, с которых разрешён доступ. По умолчанию `127.0.0.1` и `::1`. Поле `Port` — удалённый порт клиента; значение `any` означает любой порт |

Способы изменить:
- Через окно `MCP API` (`MainWindow` → кнопка **API**).
- Вручную в файле `Engine\McpSettings.txt`.

При смене `Port`, `ApiKey` или `IsEnabled` в UI инструмент `mcp_settings_set` возвращает `RestartRequired: true` — для применения нужно перезапустить хост (кнопка **Restart** в UI).

**Первое включение MCP API возможно только через UI или вручную в файле `Engine\McpSettings.txt`.** Пока API выключено, инструмент `mcp_settings_set` недоступен.

**IP-фильтрация.** Перед проверкой API-ключа сервер проверяет удалённый IP-адрес клиента по списку `AllowedIps`. Если IP не найден в белом списке, запрос возвращает `403 Forbidden`. Редактирование списка доступно на вкладке **Supports ip`s** окна `MCP API` и через `mcp_settings_set` (изменение не требует перезапуска хоста).

---

## 2.9. Тестовый стенд

Репозиторий содержит консольный тестовый стенд:

```
Tests/McpTestStand/OsEngine.McpApi.TestStand/
```

Стенд запускает `OsEngine.exe`, дожидается готовности MCP API и последовательно проверяет каждый модуль:

| Модуль | Что проверяется |
|--------|-----------------|
| Protocol | `initialize`, `notifications/initialized`, `tools/list` |
| Logs | `log_get_emergency_log`, `log_get_mcp_log` |
| Settings | `prime_settings_get`, `prime_settings_set` |
| Config | `mcp_settings_get`, `mcp_settings_set` |
| Server Management | `server_management_get_list`, `server_management_activate`, `server_management_get_trade_connectors`, `server_management_get_data_connectors`, `server_management_get_connector_permissions` |
| Server Instance | `server_instance_get_params`, `server_instance_set_params`, `server_instance_create`, `server_instance_delete`, `server_instance_connect`, `server_instance_disconnect`, `server_instance_get_status`, `server_instance_get_securities`, `server_instance_get_portfolios`, `server_instance_get_log` |
| Wiki Robots | `wiki_robots_list`, `wiki_robot_info` |
| Wiki Indicators | `wiki_indicators_list`, `wiki_indicator_info` |
| Wiki Securities | `wiki_securities_moex_iss`, `wiki_securities_tinvest`, `wiki_securities_alor`, `wiki_securities_qscalp`, `wiki_securities_mapping_info` |
| SSE | подключение к `/api/v1/events`, события `terminal.launched` и `heartbeat` |
| Errors | HTTP 401, `-32601`, неизвестный инструмент, невалидные параметры |
| Terminal | `ping`, `terminal_get_status`, `terminal_launch`, `terminal_stop`, `terminal_kill`, `terminal_open_mode` |
| Data | `data_get_sets`, `data_create_set`, `data_delete_set`, `data_set_settings_get`, `data_set_settings_set`, `data_set_securities_get`, `data_set_securities_add`, `data_set_securities_remove`, `data_set_on`, `data_set_off`, `data_get_set_status`, `data_get_security_status` |
| Robot | `bot_get_list`, `bot_create`, `bot_delete`, `bot_get_params`, `bot_set_params`, `bot_get_sources`, `bot_get_config_tab_simple`, `bot_set_config_tab_simple`, `bot_get_config_tab_screener`, `bot_set_config_tab_screener`, `bot_get_config_tab_index`, `bot_set_config_tab_index` |
| Journal | `bot_journal_get_settings`, `bot_journal_set_settings`, `bot_journal_get_summary`, `bot_journal_get_equity`, `bot_journal_get_statistics`, `bot_journal_get_drawdown`, `bot_journal_get_volume`, `bot_journal_get_open_positions`, `bot_journal_get_closed_positions` |
| Tester | `tester_data_get_config`, `tester_data_get_available_sets`, `tester_data_set_config`, `tester_execution_get_config`, `tester_execution_set_config`, `tester_portfolio_get_config`, `tester_portfolio_set_config`, `tester_start`, `tester_pause`, `tester_stop`, `tester_fast_forward`, `tester_step_forward`, `tester_get_status` |

### Запуск

```bash
cd Tests/McpTestStand/OsEngine.McpApi.TestStand/bin/Debug/net10.0
./OsEngine.McpApi.TestStand.exe
```

### Аргументы командной строки

| Аргумент | Описание |
|----------|----------|
| `path/to/OsEngine.exe` | Путь к OsEngine (по умолчанию `../../../../../../OsEngine/bin/Debug/OsEngine.exe`) |
| `--port <port>` | Порт MCP (по умолчанию `6500`) |
| `--api-key <key>` | Ключ (по умолчанию `osengine-mcp-default-key`) |
| `--timeout <seconds>` | Таймаут ожидания готовности (по умолчанию `60`) |
| `--no-wait` | Не ждать нажатия клавиши в конце |

### Настройка секретов коннектора

Некоторые тесты (например, подключение к бирже) требуют реальных учётных данных. Тестовый стенд **не хранит секреты в исходном коде** и **не коммитит их**. Секреты загружаются по следующему приоритету:

1. **Переменные окружения** (удобно для CI/CD):
   ```bash
   set OSENGINE_TEST_CONNECTOR_TYPE=TInvest
   set OSENGINE_TEST_CONNECTOR_PARAMETERS={"Token":"..."}
   ```

2. **Локальный файл `test-secrets.json`** рядом с `.exe` (удобно для ручного запуска):
   ```json
   {
     "connector": {
       "type": "TInvest",
       "parameters": {
         "Token": "..."
       }
     }
   }
   ```

3. **Интерактивный консольный prompt** — если и env vars, и файл отсутствуют, стенд спросит тип коннектора и параметры, а затем сохранит их в `test-secrets.json`.

Файл `test-secrets.json` добавлен в `.gitignore` и не должен попадать в репозиторий. При выводе запросов значения параметров, чьи имена содержат `token`, `key`, `secret` или `password`, маскируются.

### Пример отчёта

```
--- Module Summary ---
PROTOCOL:          3/3 passed
LOGS:              3/3 passed
SETTINGS:          2/2 passed
CONFIG:            2/2 passed
SERVER_MANAGEMENT: 5/5 passed
SERVER_INSTANCE:   5/5 passed
SSE:               1/1 passed
ERRORS:            4/4 passed
WIKI_ROBOTS:       6/6 passed
WIKI_INDICATORS:   7/7 passed
WIKI_SECURITIES:  12/12 passed
DATA:             15/15 passed
TESTER:           17/17 passed
TERMINAL:         13/13 passed

Total: 95/95 passed in 190.1s
```

Если стенд запущен двойным кликом из проводника, окно консоли остаётся открытым до нажатия клавиши.

---

## 3. Запуск OsEngine.exe: рабочий каталог и одиночный экземпляр

`OsEngine.exe` — WPF-приложение, которое при старте выполняет ряд проверок, зависящих от **текущего рабочего каталога** (`Directory.GetCurrentDirectory()`). Если каталог неправильный, программа закрывается ещё до инициализации MCP API.

### 3.1. Проверки при старте (`MainWindow.xaml.cs`)

| Проверка | Что делает | Причина ошибки | Сообщение пользователю |
|----------|-----------|----------------|------------------------|
| `CheckWorkWithDirectory()` | Создаёт/проверяет папку `Engine` и файл `Engine\checkFile.txt` в текущем каталоге | Нет прав на запись в текущий каталог | "Ваша оперативная система не даёт программе сохранять данные. Перезапустите её из под администратора." |
| `CheckOutSomeLibrariesNearby()` | Проверяет наличие `QuikSharp.dll` в текущем каталоге | Запуск не из папки с `OsEngine.exe` | "Похоже Вы запустили программу не из папки с ней. Не создав ярлык, а обычным перемещением. Так нельзя!" |
| `CheckAlreadyWorkEngine()` | Проверяет, не запущен ли уже `OsEngine.exe` из текущего каталога | Попытка запустить второй экземпляр | "Os Engine уже запущен из данной директории. Второй запускать нельзя!" |

### 3.2. Почему иногда "не запускается", а иногда запускается

Ошибка "запуск из этой директории не возможен" — это, скорее всего, одно из двух сообщений выше. Разница зависит от того, **из какого каталога** и **при каких условиях** запускается процесс:

1. **Неправильный рабочий каталог.**  
   Если запускать `OsEngine.exe` по полному пути из другой папки (например, из корня репозитория), текущий каталог остаётся той папкой, откуда был запуск. В ней нет `QuikSharp.dll` → Message6.

2. **Предыдущий процесс ещё жив.**  
   Если `OsEngine.exe` уже запущен из той же папки, второй запуск блокируется → Message7. Процесс может оставаться висеть после сбоев, после `terminal_stop` (закрытие не мгновенное) или если тестовый стенд/оркестратор не дождались завершения.

3. **Нет прав на запись.**  
   Если текущий каталог защищён (например, Program Files), `CheckWorkWithDirectory` падает → Message2.

### 3.3. Правильный способ запуска

**Для запуска из командной строки, скриптов или shell используйте `osEngineStarter.exe`.**

`osEngineStarter.exe` находится в той же папке, что и `OsEngine.exe` (например, `OsEngine/bin/Debug/`). Он делает запуск надёжным:

- автоматически использует папку со своим расположением как рабочий каталог (`WorkingDirectory`);
- проверяет, не запущен ли уже `OsEngine.exe` из этой же папки;
- передаёт все свои аргументы командной строки в `OsEngine.exe`;
- запускает `OsEngine.exe` отдельно от текущей консоли.

```bash
cd OsEngine/bin/Debug
./osEngineStarter.exe
```

С аргументами:

```bash
./osEngineStarter.exe -robots
```

При повторном запуске, когда `OsEngine.exe` уже работает из той же директории:

```
OsEngine is already running from <путь к папке>\OsEngine\bin\Debug
```

#### Почему не `./OsEngine.exe`

`OsEngine.exe` — WPF-приложение, которое при старте проверяет `Directory.GetCurrentDirectory()`. Если запускать его напрямую из другой папки или из некоторых shell-окружений (например, Git Bash), рабочий каталог может оказаться неправильным, и процесс закроется ещё до инициализации MCP API. `osEngineStarter.exe` гарантирует правильный рабочий каталог и корректное поведение в shell.

#### Программный запуск из кода

Если запуск нужен из собственного C#-кода, используйте `ProcessStartInfo` с явным `WorkingDirectory`:

```csharp
string exePath = @"<путь к OsEngine>\OsEngine\bin\Debug\OsEngine.exe";

Process.Start(new ProcessStartInfo(exePath)
{
    WorkingDirectory = Path.GetDirectoryName(exePath),
    UseShellExecute = false,
    CreateNoWindow = false
});
```

#### Файлы стартера

Рядом с `OsEngine.exe` должны лежать:

- `osEngineStarter.exe` — apphost;
- `OsEngineStarter.dll` — сборка логики;
- `OsEngineStarter.runtimeconfig.json` — настройки runtime.

Исходники находятся в `Tests/OsEngineStarter/`. Стартер собран под `net10.0-windows` и требует установленного .NET 10 runtime (так же, как и сам `OsEngine.exe`).

### 3.4. Если предыдущий процесс ещё жив

**Не всегда нужно убивать процесс.** Зависит от цели:

- **Если нужно просто подключиться к MCP API** — не запускайте новый `OsEngine.exe`, а обращайтесь к уже запущенному экземпляру по его адресу и ключу:
  ```bash
  curl -H "X-Api-Key: osengine-mcp-default-key" \
       -X POST http://localhost:6500/api/v1/mcp \
       -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_get_status","arguments":{}},"id":1}'
  ```
  Или используйте инструмент `terminal_get_status` из клиента.

- **Если нужно перезапустить OsEngine в другом режиме** (`-tester`, `-testerlight`, `-robots`, `-robotslight`, `-data`, `-optimizer`, `-converter`) или запустить свежую сборку после `dotnet build` — тогда предыдущий процесс нужно корректно завершить:
  1. Сначала попробовать `terminal_stop` через MCP API.
  2. Дождаться полного завершения (в диспетчере задач не должно остаться `OsEngine.exe`).
  3. Если процесс завис и не реагирует — принудительно завершить: `taskkill /F /IM OsEngine.exe`.
  4. После `taskkill` тоже дождаться, пока процесс исчезнет из списка, иначе `dotnet build` может не получить доступ к `.exe`.

**Перед `dotnet build` обязательно завершить процесс**, потому что запущенный `OsEngine.exe` блокирует свой файл.

### 3.5. Не запускать два экземпляра из одной папки

OsEngine не поддерживает несколько процессов, работающих с одной директорией данных. `CheckAlreadyWorkEngine()` заблокирует второй запуск. Для параллельных тестов нужны отдельные копии папки.

### 3.6. Что делает тестовый стенд

Тестовый стенд уже устанавливает правильный `WorkingDirectory`:

```csharp
string workingDirectory = Path.GetDirectoryName(osEnginePath) ?? string.Empty;

osEngineProcess = Process.Start(new ProcessStartInfo(osEnginePath)
{
    WorkingDirectory = workingDirectory,
    UseShellExecute = false,
    CreateNoWindow = false
});
```

Поэтому при запуске через стенд проблема Message6 не возникает. Ошибки обычно связаны либо с правами, либо с тем, что предыдущий процесс `OsEngine.exe` ещё не завершился (Message7).
