# CONTEXT_MCP_V2 — MCP API V2 в OsEngine (рекомендуемая)

> **MCP API V2 — текущая рекомендуемая версия.** Полное соответствие спецификации Anthropic MCP (транспорт **Streamable HTTP**). Все новые интеграции делаются только сюда.
> V1 (легаси) описана отдельно: [`CONTEXT_MCP_V1.md`](CONTEXT_MCP_V1.md). Она будет удалена весной 2027 года.

---

## 1. Endpoint и авторизация

Один endpoint — `http://localhost:<port>/api/v2/mcp` (поддерживает `POST`, `GET`, `DELETE`).

- Порт по умолчанию `6500` (`Engine\McpSettings.txt`);
- авторизация — заголовок `X-Api-Key` (по умолчанию `osengine-mcp-default-key`);
- IP-фильтр (`AllowedIps`) и `Origin` — как у V1;
- хост строго `localhost` (не `127.0.0.1`): HTTP Listener зарегистрирован на `http://localhost:6500/`.

---

## 2. Жизненный цикл

1. **`initialize`** — согласование версии протокола. Клиент шлёт свою версию (можно любую), сервер отвечает своей (`2024-11-05`). В заголовке ответа — **`Mcp-Session-Id`** (UUID сессии).
2. **`notifications/initialized`** — уведомление о готовности → `202 Accepted` без тела. До него сервер не шлёт серверных событий.
3. Дальше — обычные запросы с заголовками `Mcp-Session-Id` и `MCP-Protocol-Version: 2024-11-05`.

```json
// initialize (запрос)
{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{"logging":{}},"clientInfo":{"name":"cli","version":"1.0"}},"id":1}
```

---

## 3. Правила транспорта (Streamable HTTP)

| Запрос | Поведение сервера |
|---|---|
| `POST` — JSON-RPC **request** (есть `id`) | `200` + `application/json` (ответ) |
| `POST` — JSON-RPC **notification/response** (нет `id`) | `202 Accepted`, пустое тело |
| `GET` (открыть SSE-стрим) | `200` + `text/event-stream` (серверные события) |
| `DELETE` (с `Mcp-Session-Id`) | завершение сессии (`200`) |

- Клиент обязан слать `Accept: application/json, text/event-stream`.
- На последующих запросах — заголовок `MCP-Protocol-Version: 2024-11-05` (неверный → `400`).
- Запрос без `Mcp-Session-Id` (кроме `initialize`) → `400`; неизвестная сессия → `404`.
- Поддерживается capability `logging` (сервер шлёт `notifications/message` только тем клиентам, кто заявил её в `initialize`).

---

## 4. Формат ответов

Ключи в **camelCase**, конверт строго по JSON-RPC 2.0 (только `result` XOR `error`, никакого `"error": null`).

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [ { "type": "text", "text": "{\"mode\":\"IsMainWindow\",...}" } ],
    "isError": false
  }
}
```

- `tools/list` → ключ `tools`; `tools/call` → ключи `content` / `type` / `text` / `isError`.
- Полезная нагрузка инструмента — в `result.content[0].text` (вложенная JSON-строка).
- Ошибка инструмента: `isError: true`, текст ошибки в `content[0].text`.

---

## 5. Доступные инструменты

Полный список возвращает `tools/list`. Каждый инструмент вызывается через `tools/call` с параметрами `name` и `arguments`.

| Инструмент | Описание |
|------------|----------|
| `ping` | Проверка доступности API |
| `terminal_get_status` | Текущий статус терминала |
| `terminal_launch` | Перезапуск терминала в указанном режиме (`-tester`, `-testerlight`, `-robots`, `-robotslight`, `-data`, `-optimizer`, `-converter`). Режимы `robots` и `robotslight` эквивалентны `trader` / `trader_light` |
| `terminal_stop` | Корректная остановка терминала |
| `terminal_kill` | Принудительное завершение процесса OsEngine |
| `terminal_open_mode` | Открыть режим из запущенного `MainWindow` без перезапуска процесса |
| `log_get_emergency_log` | Последние записи emergency-лога |
| `log_get_mcp_log` | Последние записи лога MCP |
| `prime_settings_get` | Общие настройки терминала |
| `prime_settings_set` | Изменить общие настройки терминала |
| `mcp_settings_get` | Настройки хоста MCP |
| `mcp_settings_set` | Изменить настройки хоста MCP |
| `server_management_get_list` | Список развёрнутых биржевых коннекторов (имя, тип, статус) |
| `server_management_activate` | Активация сервера указанного типа: загрузка сохранённых экземпляров и создание основного |
| `server_management_get_trade_connectors` | Список типов коннекторов, доступных для торговли |
| `server_management_get_data_connectors` | Полный список типов коннекторов для загрузки рыночных данных |
| `server_management_get_connector_permissions` | Разрешения коннектора (`IServerPermission`): таймфреймы, торговые права, плечо, время жизни ордеров |
| `server_management_get_data_timeframes` | Список таймфреймов, доступных для скачивания с коннектора (`type`). `"MarketDepthHistory"` — история стакана |
| `server_instance_get_params` | Параметры экземпляра сервера (пароли маскируются) |
| `server_instance_set_params` | Установить параметры экземпляра сервера |
| `server_instance_create` | Создать новый экземпляр коннектора указанного типа |
| `server_instance_delete` | Удалить экземпляр коннектора (номер 0 защищён) |
| `server_instance_connect` | Подключить экземпляр сервера |
| `server_instance_disconnect` | Отключить экземпляр сервера |
| `server_instance_get_status` | Статус подключения экземпляра сервера |
| `server_instance_get_securities` | Список бумаг экземпляра сервера (фильтры по классу/коду) |
| `server_instance_get_portfolios` | Список портфелей и позиций экземпляра сервера |
| `server_instance_get_log` | Журнал экземпляра сервера |
| `wiki_robots_list` | Список доступных роботов: имя класса, описание, источники, индикаторы |
| `wiki_robot_info` | Подробная информация по одному роботу (live): описание, источники, индикаторы, параметры |
| `wiki_indicators_list` | Список доступных индикаторов: имя класса, отображаемое имя, описание, параметры, серии |
| `wiki_indicator_info` | Подробная информация по одному индикатору (live): описание, параметры, серии |
| `wiki_securities_moex_iss` | Справочник бумаг MOEX ISS. Фильтр по подстроке |
| `wiki_securities_tinvest` | Справочник бумаг TInvest. Фильтр по подстроке |
| `wiki_securities_alor` | Справочник бумаг Alor. Фильтр по подстроке |
| `wiki_securities_qscalp` | Справочник бумаг QScalp. Фильтр по подстроке |
| `wiki_securities_mapping_info` | Универсальный поиск бумаги по всем справочникам (`query`, `connector`, `limit`, `exact`) |
| `wiki_dividends_get_history` | Исторические дивиденды акции (`ticker`, `date`, `refresh`) |
| `wiki_dividends_get_future` | Ближайшая будущая запись дивидендов (`ticker`, `date`, `refresh`) |
| `wiki_dividends_get_past` | Ближайшая прошлая запись дивидендов (`ticker`, `date`, `refresh`) |
| `wiki_dividends_search_by_date` | Поиск дивидендов по дате закрытия реестра (`ticker`, `date`, `refresh`) |
| `data_get_sets` | Список существующих сетов данных OsData |
| `data_create_set` | Создать сет данных (`name`, `source`, `source_name`, `timeframes`, `date_from`, `date_to`). В `timeframes` можно `"MarketDepthHistory"` — история стакана (только `QscalpMarketDepth`) |
| `data_delete_set` | Удалить сет данных по имени |
| `data_set_settings_get` | Настройки сета данных |
| `data_set_settings_set` | Частично обновить настройки сета (`regime`, `timeframes`, `date_from`, `date_to`, `market_depth_depth`). `timeframes` принимает `"MarketDepthHistory"` |
| `data_set_securities_get` | Список бумаг в сете данных |
| `data_set_securities_add` | Добавить бумаги в сет данных |
| `data_set_securities_remove` | Удалить бумаги из сета данных |
| `data_set_on` | Включить сет данных (запустить загрузку) |
| `data_set_off` | Выключить сет данных |
| `data_get_set_status` | Агрегированный статус загрузки сета (`regime`, `status`, `percent_load`) |
| `data_get_security_status` | Статус загрузки бумаги/таймфрейма (`time_start`, `time_end`, `objects_count`, `percent_load`, `status`). `timeframe` принимает `"MarketDepthHistory"` |
| `bot_get_list` | Список загруженных роботов |
| `bot_create` | Создать нового робота |
| `bot_delete` | Удалить робота |
| `bot_get_params` | Параметры созданного робота |
| `bot_set_params` | Установить параметры робота |
| `bot_click_param_button` | Нажать на Button-параметр робота (`bot_id`, `param_name`) |
| `bot_get_sources` | Список источников (вкладок) робота |
| `bot_get_config_tab_simple` | Конфигурация вкладки `BotTabSimple` |
| `bot_set_config_tab_simple` | Настроить вкладку `BotTabSimple` (сервер, портфель, эмулятор, комиссия, инструмент, свечи) |
| `bot_get_config_tab_screener` | Конфигурация вкладки `BotTabScreener` |
| `bot_set_config_tab_screener` | Настроить вкладку `BotTabScreener` |
| `bot_get_config_tab_index` | Конфигурация вкладки `BotTabIndex` (формула, глубина расчёта, авто-формула) |
| `bot_set_config_tab_index` | Настроить вкладку `BotTabIndex` |
| `bot_get_position_support` | Настройки сопровождения позиции (`BotManualControl`) для вкладки |
| `bot_set_position_support` | Установить сопровождение позиции |
| `bot_grid_get` | Сетки вкладки `Simple` |
| `bot_grid_create` | Создать сетку на вкладке `Simple` (`grid_type`, `first_price`, `line_count_start`, `line_step`, `start_volume`) |
| `bot_grid_set_settings` | Частичное изменение настроек сетки |
| `bot_grid_set_regime` | Режим сетки: `On`, `Off`, `OffAndCancelOrders`, `CloseOnly`, `CloseForced` |
| `bot_grid_delete` | Удалить сетку |
| `bot_position_get_open` | Открытые позиции источника |
| `bot_position_open_at_market` | Открыть позицию по маркету |
| `bot_position_close_at_market` | Закрыть позицию по маркету |
| `bot_journal_get_settings` | Настройки журнала роботов |
| `bot_journal_set_settings` | Установить настройки журнала |
| `bot_journal_get_summary` | Сводка журнала (прибыль, диапазон дат, сделки) |
| `bot_journal_get_equity` | Кривая эквити |
| `bot_journal_get_statistics` | Статистика журнала |
| `bot_journal_get_drawdown` | Кривая просадки |
| `bot_journal_get_volume` | Объёмы торговли по бумагам/плечу |
| `bot_journal_get_open_positions` | Открытые позиции |
| `bot_journal_get_closed_positions` | Закрытые позиции |
| `system_load_get_current` | Последние точки загруженности системы (RAM, CPU, очереди) |
| `system_load_get_history` | История точек загруженности по типу (`Ram`, `Cpu`, `Ecq`, `Moq`) |
| `system_load_get_settings` | Настройки сбора загруженности |
| `system_load_set_settings` | Частичное изменение настроек сбора загруженности |
| `compare_positions_get` | Сверка позиций роботов с биржей по портфелям сервера |
| `compare_positions_get_settings` | Настройки модуля сверки |
| `compare_positions_set_settings` | Частичное изменение настроек сверки |
| `compare_positions_set_ignored` | Установить список игнорируемых бумаг сверки |
| `compare_positions_sync_all` | Синхронизировать весь портфель под учёт роботов |
| `compare_positions_sync_this` | Синхронизировать одну бумагу в портфеле |
| `proxy_get_list` | Список всех прокси прокси-роутера (пароли маскированы) |
| `proxy_create` | Создать прокси (`ip`, `port`; опц. `is_on`, `login`, `password`, `ping_web_address`) |
| `proxy_delete` | Удалить прокси по `number` |
| `proxy_get_settings` | Настройки прокси по `number` |
| `proxy_set_settings` | Частичное изменение настроек прокси |
| `proxy_get_status` | Статус прокси (`auto_ping_last_status`, `location`, `use_connection_count`) |
| `proxy_ping` | Пропинговать прокси и вернуть статус |
| `optimizer_data_get_config` / `optimizer_data_set_config` | Источник данных оптимизатора (сет/папка, тип данных, диапазон) |
| `optimizer_data_get_status` | Статус хранилища данных оптимизатора |
| `optimizer_dividends_get_config` / `optimizer_dividends_set_config` | Дивиденды/маржа/налоги оптимизатора |
| `optimizer_bot_get` / `optimizer_bot_set` | Текущий робот оптимизации / выбор робота |
| `optimizer_trade_settings_get` / `optimizer_trade_settings_set` | Комиссия, тип исполнения, проскальзывания, депозит |
| `optimizer_position_support_get` / `optimizer_position_support_set` | Сопровождение позиций для прогонов |
| `optimizer_phases_get` / `optimizer_phases_set` | Фазы walk-forward |
| `optimizer_filters_get` / `optimizer_filters_set` | Фильтры отсева между фазами |
| `optimizer_params_get` / `optimizer_params_set` / `optimizer_params_reset` | Пространство параметров. Для числовых параметров `value` эквивалентно колонке «По умолчанию» грида оптимизатора |
| `optimizer_get_pass_count` | Предполагаемое число прогонов |
| `optimizer_get_threads` / `optimizer_set_threads` | Число потоков оптимизации (1..50) |
| `optimizer_bot_tab_get_config` / `optimizer_bot_tab_set_config` | Вкладки робота оптимизации (Simple: бумага+ТФ; Screener: `securities`+ТФ) |
| `optimizer_start` / `optimizer_stop` | Запуск (ошибки готовности списком) / остановка |
| `optimizer_get_status` | `is_running`, прогресс, оценка времени |
| `optimizer_get_report` | Результаты по фазам: параметры + метрики |
| `optimizer_save_report` / `optimizer_load_report` | Сохранение/загрузка результатов |
| `encryption_get_status` | Статус шифрователя (`Plain`/`Encrypted`/`Declined`), `unlocked` |
| `encryption_unlock` | Разблокировать шифрователь мастер-паролем |
| `encryption_enable` | Включить шифрование с новым паролем (мин. 8 символов) |
| `encryption_disable` | Выключить шифрование и расшифровать ключи (деструктивно) |

**Важно про имена бумаг в тестере и оптимизаторе.** Хранилище хранит бумаги как имена файлов **с расширением**: `SBER.txt`, а не `SBER`. Во вкладки робота через `optimizer_bot_tab_set_config` передавать имя с `.txt`.

**Важно про историю стакана (`MarketDepthHistory`).** В движке «история стакана» — это не отдельный timeframe, а режим `MarketDepth`. Через MCP он задаётся значением `"MarketDepthHistory"` в списке `timeframes` (в `data_create_set` / `data_set_settings_set`, и принимается в `data_get_security_status`). Поддерживает только коннектор `QscalpMarketDepth` (у него `DataFeedTfMarketDepthHistoryCanLoad = true`, а живой `MarketDepth` — `false`). Значения `"MarketDepth"` (живой) и `"MarketDepthHistory"` взаимоисключающие: на сервере без поддержки нужного режима MCP вернёт ошибку.

---

## 6. Примеры запросов

**Универсальный шаблон вызова** (после `initialize` — подставлять `name`/`arguments` любого инструмента):

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Accept: application/json, text/event-stream" \
  -H "Mcp-Session-Id: <SESSION>" -H "MCP-Protocol-Version: 2024-11-05" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"<tool>","arguments":{}},"id":3}' \
  http://localhost:6500/api/v2/mcp
```

**initialize:**

```bash
curl -si -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Accept: application/json, text/event-stream" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{"logging":{}},"clientInfo":{"name":"cli","version":"1.0"}},"id":1}' \
  http://localhost:6500/api/v2/mcp | grep -i "mcp-session-id"
```

**notifications/initialized** → `202`:

```bash
curl -s -o /dev/null -w "%{http_code}" -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Accept: application/json, text/event-stream" -H "Mcp-Session-Id: <SESSION>" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"notifications/initialized"}' \
  http://localhost:6500/api/v2/mcp
```

**tools/call ping** (ответ camelCase):

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" -H "Accept: application/json, text/event-stream" \
  -H "Mcp-Session-Id: <SESSION>" -H "MCP-Protocol-Version: 2024-11-05" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"ping","arguments":{}},"id":3}' \
  http://localhost:6500/api/v2/mcp
```

Пример ответа `tools/call`:

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "content": [ { "type": "text", "text": "\"pong\"" } ],
    "isError": false
  }
}
```

**Разбор полезной нагрузки** (PowerShell, camelCase):

```bash
curl -s ... http://localhost:6500/api/v2/mcp | powershell -Command '$r = $input | ConvertFrom-Json; $r.result.content[0].text | ConvertFrom-Json | ConvertTo-Json -Depth 10'
```

**События (SSE):**

```bash
curl -N -H "X-Api-Key: osengine-mcp-default-key" -H "Accept: text/event-stream" \
  -H "Mcp-Session-Id: <SESSION>" \
  http://localhost:6500/api/v2/mcp
```

---

## 7. События сервер → клиент

Клиент открывает `GET /api/v2/mcp` (`Accept: text/event-stream`, `Mcp-Session-Id`) и держит SSE-стрим. Каждое событие — кадр **`event: message`** + `data:` = JSON-RPC-уведомление.

Все 18 событий OsEngine идут на V2:

| Событие | Тип уведомления |
|---|---|
| `terminal.launched` | `notifications/message` (при подключении к стриму) |
| `terminal.mode_changed`, `terminal.stopped` | `notifications/message` |
| `prime_settings.changed` | `notifications/message` |
| `server_instance.status_changed` / `.security.updated` / `.portfolio.updated` / `.log` | `notifications/message` |
| `data_set_load_completed_event` / `data_set_security_load_completed_event` | `notifications/message` |
| `optimizer.test.finished` | `notifications/message` |
| `optimizer.test.progress` | `notifications/progress` (при `progressToken`) + `notifications/message` |
| `tester.test.started` / `.finished` / `.paused` / `.resumed` / `.progress` | `notifications/message` |
| `heartbeat` | `notifications/message` (уровень `debug`) |

Формат `notifications/message`: `params.level` (из `LogMessageType`), `params.logger = "osengine"`, `params.data = { event, payload }`.

Пример кадра:

```
event: message
data: {"jsonrpc":"2.0","method":"notifications/message","params":{"level":"notice","logger":"osengine","data":{"event":"terminal.mode_changed","payload":{...}}}}
```

---

## 8. locked-режим и шифрование

Когда шифрование включено (ключ API зашифрован), хост работает в locked-режиме:
- `initialize` **создаёт сессию** (как обычно);
- внутри сессии разрешён `encryption_unlock` (с паролем);
- после разблокировки ключ подхватывается автоматически — дальше обычная работа;
- прочие инструменты до разблокировки → `401 Encryptor is locked...`.

---

## 9. Коды ошибок

**JSON-RPC:**

| Код | Назначение |
|-----|-----------|
| `-32700` | Parse error |
| `-32600` | Invalid Request |
| `-32601` | Method not found |
| `-32602` | Invalid params |
| `-32603` | Internal error |

**HTTP:**

| Код | Когда |
|-----|-------|
| `200` | успешный `tools/call` / `initialize` / `tools/list` |
| `202` | принятое уведомление/ответ (нет тела) |
| `400` | нет `Mcp-Session-Id`, неверный `MCP-Protocol-Version`, невалидный JSON |
| `401` | нет/неверный `X-Api-Key`, locked-режим |
| `404` | неизвестная сессия |
| `405` | метод не POST/GET/DELETE на `/api/v2/mcp` |

---

## 10. Настройки хоста

`Engine\McpSettings.txt`:

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Port` | `int` | Порт HTTP-сервера |
| `ApiKey` | `string` | Ключ для заголовка `X-Api-Key` |
| `IsEnabled` | `bool` | Автозапуск хоста при старте `MainWindow` |
| `IsFullLogEnabled` | `bool` | Логировать каждый запрос/ответ/SSE |
| `AllowedIps` | `List<{Ip,Port}>` | Белый список IP |

---

## 11. Тестовый стенд

`Tests/McpTestStand/OsEngine.McpApi.TestStand/`. Флаг транспорта: `--transport v1|v2` (по умолчанию `v1`).

```bash
./OsEngine.McpApi.TestStand.exe --transport v2              # все модули по V2
./OsEngine.McpApi.TestStand.exe --transport v2 --module StreamableHttp
```

- V2: **200/200**; V1: **191/191**. Модуль `StreamableHttp` (10 проверок) — транспорт/сессии/события V2.

---

## 12. Подключение стандартных клиентов

**OpenCode** (`opencode.json`):

```json
{
  "mcp": {
    "osengine": {
      "type": "remote",
      "url": "http://localhost:6500/api/v2/mcp",
      "enabled": true,
      "headers": { "X-Api-Key": "osengine-mcp-default-key" }
    }
  }
}
```

После перезапуска OpenCode инструменты появятся как `osengine_*`. Так же подключаются Claude Code и MCP Inspector (URL `/api/v2/mcp`).

**Python SDK** (официальный клиент):

```python
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client
import httpx

async with httpx.AsyncClient(headers={"X-Api-Key": "osengine-mcp-default-key"}) as http:
    async with streamable_http_client("http://localhost:6500/api/v2/mcp", http_client=http) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools = await session.list_tools()
            res = await session.call_tool("ping", {})
```

---

## 13. Запуск OsEngine.exe

Рабочий каталог и одиночный экземпляр — как в V1 (`CONTEXT_MCP_V1.md`, раздел 3). Кратко:
- запускать из папки с `OsEngine.exe` (или через `osEngineStarter.exe`);
- нельзя запускать два экземпляра из одной папки;
- MCP-хост поднимается в `MainWindow` и живёт во всех режимах.
