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
  McpApiUi.xaml/.xaml.cs    // Окно управления API
  Json/                     // DTO JSON-RPC и SSE
    McpJsonRpcRequest.cs
    McpJsonRpcResponse.cs
    McpJsonRpcError.cs
    McpEvent.cs
    McpTool.cs              // Описание инструмента MCP
  Modules/                  // Обработчики инструментов
    TerminalApi.cs          // terminal_*, terminal.* events
    LogsApi.cs              // log_get_*
    SettingsApi.cs          // prime_settings_*, prime_settings.changed
    McpConfigApi.cs         // mcp_settings_*
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
    SseTests.cs
    ErrorTests.cs
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
| `terminal_launch` | Перезапуск терминала в указанном режиме. Текущий процесс OsEngine завершается, запускается новый с нужным аргументом (`-tester`, `-robots` и т.д.) |
| `terminal_stop` | Корректная остановка терминала (закрытие `MainWindow`) |
| `terminal_kill` | Принудительное завершение процесса OsEngine |
| `log_get_emergency_log` | Последние записи emergency-лога |
| `log_get_mcp_log` | Последние записи лога MCP |
| `prime_settings_get` | Общие настройки терминала |
| `prime_settings_set` | Изменить общие настройки терминала |
| `mcp_settings_get` | Настройки хоста MCP |
| `mcp_settings_set` | Изменить настройки хоста MCP |

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
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_launch","arguments":{"mode":"tester"}},"id":5}' \
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

**notifications/initialized (notification, без `id`):**

```bash
curl -s -o /dev/null -w "HTTP %{http_code}\n" -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}' \
  http://localhost:6500/api/v1/mcp
```

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
| `heartbeat` | Каждые 5 секунд |

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
| `IsEnabled` | `bool` | Автоматически запускать хост при старте `MainWindow` |
| `IsFullLogEnabled` | `bool` | Логировать каждый запрос, тело, ответ и SSE-подключения |

Способы изменить:
- Через окно `MCP API` (`MainWindow` → кнопка **API**).
- Через инструмент `mcp_settings_set` (внутри `tools/call`).
- Через CLI-аргументы при запуске `OsEngine.exe`:
  - `-mcpPort <port>`
  - `-mcpApiKey <key>`

При смене `Port`, `ApiKey` или `IsEnabled` инструмент `mcp_settings_set` возвращает `RestartRequired: true` — для применения нужно перезапустить хост (кнопка **Restart** в UI или повторный вызов настройки с последующим перезапуском).

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
| SSE | подключение к `/api/v1/events`, события `terminal.launched` и `heartbeat` |
| Errors | HTTP 401, `-32601`, неизвестный инструмент, невалидные параметры |
| Terminal | `ping`, `terminal_get_status`, `terminal_launch`, `terminal_stop`, `terminal_kill` |

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

### Пример отчёта

```
--- Module Summary ---
PROTOCOL: 3/3 passed
LOGS:     3/3 passed
SETTINGS: 2/2 passed
CONFIG:   2/2 passed
SSE:      1/1 passed
ERRORS:   4/4 passed
TERMINAL: 5/5 passed

Total: 20/20 passed
```

Если стенд запущен двойным кликом из проводника, окно консоли остаётся открытым до нажатия клавиши.
