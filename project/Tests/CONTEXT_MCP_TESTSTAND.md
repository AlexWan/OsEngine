# CONTEXT_MCP_TESTSTAND — тестовый стенд MCP API (консольная программа)

> Описание тестового стенда `OsEngine.McpApi.TestStand` — детерминированная проверка
> MCP API OsEngine (всех групп инструментов) через HTTP/JSON-RPC.
> Это **эталонный стенд**, по образцу которого построены `OsDataTestStand` и `OsTesterTestStand`
> (см. [`CONTEXT_OSDATA_TESTSTAND.md`](CONTEXT_OSDATA_TESTSTAND.md) и [`CONTEXT_OSTESTER_TESTSTAND.md`](CONTEXT_OSTESTER_TESTSTAND.md)).

## 1. Цель

Дать готовый инструмент для штатной проверки MCP API OsEngine: прогнать все группы
инструментов (`server_*`, `data_*`, `wiki_*`, `tester_*`, `terminal_*`, `system_load_*`,
`proxy_*`, `optimizer_*` и т.д.) и получить структурированный отчёт pass/fail по проверкам.
Стенд не проверяет бизнес-логику OsEngine — он проверяет **контракт MCP**: что инструмент
отвечает, в правильном формате, с корректными ошибками и протоколом.

## 2. Расположение и имя

- Путь: `project\Tests\McpTestStand\OsEngine.McpApi.TestStand\`.
- Имя проекта: `OsEngine.McpApi.TestStand` (`.csproj`, `net10.0`, консоль).
- Работает только через MCP — **без прямой ссылки на OsEngine** (всё по HTTP).

## 3. Как устроен

- `Program.cs` — вход: лог-файл, отдельное консольное окно, парсинг аргументов,
  перебор модулей через `RunModule` (каждый модуль **перезапускает OsEngine перед собой**
  и гасит после).
- `OsEngineProcessController.cs` — запуск/остановка OsEngine, ожидание готовности MCP-порта.
- `McpApiClient.cs` — MCP-клиент (`ToolsCall`, транспорт V1/V2).
- `ProtocolValidator.cs` — проверка протокола (заголовки, JSON-RPC, статус-коды).
- `TestContext.cs` — отчётность: `PrintHeader/PrintRequest/PrintResponse/RecordPass/RecordFail/PrintSummary`, маскирование секретов.
- `TestResult.cs` — pass/fail.
- `TestSecrets.cs` — файл с ключами/токенами (не в git).
- `Tests/*.cs` — 21 модуль тестов (см. п. 4).

## 4. Модули (что проверяет)

Режим `—` = дефолтный `MainWindow` (без спец. аргумента запуска).

| № | Модуль | Режим OsEngine | Что проверяет |
|---|---|---|---|
| 1 | Protocol | — | транспорт MCP (v1/v2), рукопожатие, JSON-RPC |
| 2 | Logs | — | получение логов сервера |
| 3 | Settings | — | инструменты настроек терминала |
| 4 | Config | — | конфигурацию MCP-хоста |
| 5 | ServerManagement | — | активация/список коннекторов, permissions, таймфреймы |
| 6 | ServerInstance | `-robotslight` | инстансы серверов (создание/параметры/подключение/бумаги/портфели/статус) |
| 7 | SSE | — | Server-Sent Events (транспорт v1) |
| 8 | Errors | — | обработку ошибок MCP |
| 9 | WikiRobots | — | вики роботов |
| 10 | WikiIndicators | — | вики индикаторов |
| 11 | WikiSecurities | — | вики бумаг |
| 12 | WikiDividends | — | вики дивидендов |
| 13 | Data | — | OsData: сеты, свечи/тики/стаканы, загрузка |
| 14 | Tester | — | тестер (прогон бэктестов) |
| 15 | Terminal | — | режимы терминала (открыть/закрыть) |
| 16 | SystemLoad | `-robotslight` | нагрузку системы (CPU/RAM) |
| 17 | ComparePositions | `-robotslight` | сравнение позиций (только ошибочные пути — реальные ордера не шлёт) |
| 18 | Proxy | `-robotslight` | роутер прокси (создаёт свой «мёртвый» прокси и удаляет его) |
| 19 | Optimizer | `-optimizer` | оптимизатор |
| 20 | Encryption | `-robotslight` | шифрование паролей серверов (в зашифрованной ветке — только unlock, файлы не мутирует) |
| 21 | StreamableHttp | — | стандартный Streamable HTTP `/api/v2/mcp` (только при `--transport v2`) |

## 5. Как запускать

```bash
cd Tests/McpTestStand/OsEngine.McpApi.TestStand
dotnet run -- [--port 6500] [--api-key ...] [--timeout 60] [--no-wait] [--transport v2] [--mode data] [--module/-m NAME]
```

- `--transport v2` — Streamable HTTP (**стандарт**; V1 устарел). Без флага по умолчанию `v1`.
- `--module/-m` — фильтр по номеру (1–21) или подстроке имени (без регистра), несколько — через запятую.
  Нумерация = порядок полного прогона. Пропущенные модули не перезапускают OsEngine (не тратят время).
  Если фильтр ни с чем не совпал — печатает нумерованный список модулей и завершается с ошибкой.
- `--mode` — режим запуска OsEngine (переопределяет дефолтный режим модуля из таблицы в п. 4):
  - `robots` / `trader` / `botstation` → `-robots` (BotStation);
  - `robotslight` / `botstationlight` → `-robotslight` (BotStation light);
  - `tester` → `-tester`, `data` → `-data`, `optimizer` → `-optimizer`, `converter` → `-converter`;
  - без `--mode` модуль запускает OsEngine в своём режиме.
- `--no-wait` — не ждать нажатия клавиши при запуске из проводника.

## 6. Длительность

- **Полный прогон всех 21 модулей — около 5–6 минут** (пример: `Total: 196/196 passed in 338.6s`).
  Основное время — перезапуск OsEngine перед каждым модулем, а не сами проверки.
- Отдельный модуль (`--module Name` или номер) — обычно **10–60 секунд** (перезапуск + вызовы).
- «Тяжёлые» по времени модули: `Data` (скачивание свечей), `Tester` / `Optimizer` (прогон), `SSE` (ожидание стрима).

## 7. Что на выходе

Полный набор проверок: **V2 — 200** (из них 10 — модуль `StreamableHttp`), **V1 — 191**.

Во время прогона — построчно:

```
[Module 5] ServerManagement
  Status:   PASS - ...
  Status:   FAIL - ...
```

В конце — сводка (пример успешного прогона):

```
--- Module Summary ---
PROTOCOL:           3/3 passed
SERVER_MANAGEMENT:  5/5 passed
DATA:              16/16 passed
TESTER:            43/43 passed
TERMINAL:          13/13 passed
OPTIMIZER:         20/20 passed
STREAMABLE_HTTP:   10/10 passed
...

Total: 196/196 passed in 338.6s

--- Protocol Violations ---
none
```

- **`Total: X/Y passed`** — главный итог. При `> 0` failed стенд завершается с ненулевым кодом выхода.
- **`--- Protocol Violations ---`** — отдельно от падений: отклонения от протокола
  (заголовки/JSON-RPC/статус-коды), не считаются за фейл, но их видно отдельно.
- Модуль, упавший исключением, не останавливает прогон — пишет `[Name] Module failed` и идёт дальше.
- Вывод дублируется в консольное окно + stdout + лог-файл `mcp-test-stand-yyyyMMdd-HHmmss.log` рядом с `.exe`.
- Стенд запускается только по **явному разрешению пользователя**; работает в foreground
  (не использовать `run_in_background`), ждать завершения через блокирующий вывод/уведомление.

## 8. Примечания

- **Секреты коннекторов** (для модулей, требующих токен: `ServerInstance`, `Data`, `Tester`, …) грузятся по приоритету:
  1. env-переменные `OSENGINE_TEST_CONNECTOR_TYPE` + `OSENGINE_TEST_CONNECTOR_PARAMETERS` (JSON);
  2. файл `test-secrets.json` рядом с `.exe` — `{"connector":{"type":"TInvest","parameters":{"Token":"..."}}}`;
  3. интерактивный prompt (сохранит в `test-secrets.json`).
  Файл в `.gitignore`; в выводах значения, чьи имена содержат `token`/`key`/`secret`/`password`, маскируются.
- Транспорт V2 — текущий стандарт: [`CONTEXT_MCP_V2.md`](../CONTEXT_MCP_V2.md),
  сценарий работы: [`CONTEXT_MCP_SCENARIO_V2.md`](../CONTEXT_MCP_SCENARIO_V2.md).
- V1 устарел (будет удалён в 2027): [`CONTEXT_MCP_V1.md`](../CONTEXT_MCP_V1.md).
- Каждый модуль перезапускает OsEngine перед собой.
