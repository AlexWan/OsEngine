# OsEngine — Сценарии работы через MCP API V2 (рекомендуемая)

> **MCP API V2 — текущая рекомендуемая версия.** Транспорт Streamable HTTP, camelCase-ответы, сессии.
> Сценарии V1 (легаси): [`CONTEXT_MCP_SCENARIO_V1.md`](CONTEXT_MCP_SCENARIO_V1.md). Полная справка по инструментам: [`CONTEXT_MCP_V2.md`](CONTEXT_MCP_V2.md).

Пошаговые сценарии. Каждая глава — отдельная задача. Только действия.

## Правила для ИИ-агентов

1. **Хост — строго `localhost`** (не `127.0.0.1`): HTTP Listener зарегистрирован на `http://localhost:6500/`. `127.0.0.1` вернёт `400 Invalid Hostname`.
2. **Все инструменты — только через `tools/call`** (`initialize` / `tools/list` / `tools/call`). Прямой вызов `ping` и т.п. вернёт `-32601 Method not found`.
3. **Сессия обязательна.** Сначала `initialize` → из заголовка ответа взять `Mcp-Session-Id` → слать его на каждый следующий запрос вместе с `MCP-Protocol-Version: 2024-11-05`. Без сессии — `400`; чужой/протухший id — `404`.
4. **Полезная нагрузка** — в `result.content[0].text` (вложенная JSON-строка, ключи camelCase). Разбирать отдельно, не `grep`/`sed`/`awk` по сырому ответу.
5. **В OpenCode работать инструментами `osengine_*`** — они уже делают initialize/сессию/разбор под капотом. Это основной «живой» способ работы из чата. `curl` — только для отладки транспорта (Сценарий 1).
6. **Перед работой проверять режим терминала** (`osengine_terminal_get_status` → `mode`). Инструменты одного режима в другом вернут ошибку «master is not available».
7. **После задачи закрывать терминал** (`osengine_terminal_stop`), если пользователь не просил оставить открытым.

---

## Сценарий 1. Золотой путь: инициализация и вызов (curl, эталон транспорта)

```bash
# 1. initialize — получить сессию (Mcp-Session-Id в заголовке ответа)
curl -si -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Accept: application/json, text/event-stream" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{"logging":{}},"clientInfo":{"name":"cli","version":"1.0"}},"id":1}' \
  http://localhost:6500/api/v2/mcp | grep -i "mcp-session-id"

# 2. notifications/initialized → 202
curl -s -o /dev/null -w "%{http_code}\n" -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Accept: application/json, text/event-stream" -H "Mcp-Session-Id: <SESSION>" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"notifications/initialized"}' \
  http://localhost:6500/api/v2/mcp

# 3. tools/call — универсальный шаблон (name/arguments любые)
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Accept: application/json, text/event-stream" \
  -H "Mcp-Session-Id: <SESSION>" -H "MCP-Protocol-Version: 2024-11-05" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_get_status","arguments":{}},"id":3}' \
  http://localhost:6500/api/v2/mcp | powershell -Command '$r = $input | ConvertFrom-Json; $r.result.content[0].text | ConvertFrom-Json | ConvertTo-Json -Depth 10'
```

Запрос **должен** содержать `id`. Без `id` (notification) сервер вернёт `202` с пустым телом. Ответ — `200` + JSON-RPC `result` (без `"error": null`).

## Сценарий 2. Получение серверных событий (SSE V2)

Клиент открывает SSE-стрим **на том же endpoint** `GET /api/v2/mcp` (не `/api/v1/events`), передав `Mcp-Session-Id` и `Accept: text/event-stream`:

```bash
curl -N -H "X-Api-Key: osengine-mcp-default-key" -H "Accept: text/event-stream" \
  -H "Mcp-Session-Id: <SESSION>" \
  http://localhost:6500/api/v2/mcp
```

Каждое событие — кадр `event: message` + `data:` с JSON-RPC-уведомлением (`notifications/message` или `notifications/progress`). Имя события OsEngine и полезная нагрузка — внутри `params.data`:

```
event: message
data: {"jsonrpc":"2.0","method":"notifications/message","params":{"level":"notice","logger":"osengine","data":{"event":"terminal.mode_changed","payload":{...}}}}
```

События по модулям (18 шт.):
- `tester.test.started` / `.progress` / `.finished` / `.paused` / `.resumed` — прогон тестера;
- `optimizer.test.progress` / `optimizer.test.finished` (`is_partial`) — прогон оптимизатора;
- `data_set_load_completed_event` / `data_set_security_load_completed_event` — загрузка сетов OsData;
- `server_instance.status_changed` / `.security.updated` / `.portfolio.updated` / `.log` — сервера брокеров;
- `terminal.launched` / `.mode_changed` / `.stopped`, `prime_settings.changed`, `heartbeat` (debug).

Если соединение оборвалось — переподключиться; пропущенные события не буферизуются, состояние добирается через `*_get_status`.

## Сценарий 3. Подключение OpenCode и работа из чата

1. В `opencode.json` (глобально или в проекте):

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

2. Перезапустить OpenCode — появятся 148 инструментов `osengine_*`.
3. Работать ими напрямую из чата: `osengine_terminal_get_status`, `osengine_bot_create`, `osengine_data_create_set`, `osengine_optimizer_start` и т.д. Сессия/разбор под капотом.

Все сценарии ниже приводят вызовы в форме `osengine_*` (как из чата). curl-эквивалент — Сценарий 1.

## Сценарий 4. Python SDK (официальный клиент)

```python
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client
import httpx

async with httpx.AsyncClient(headers={"X-Api-Key": "osengine-mcp-default-key"}) as http:
    async with streamable_http_client("http://localhost:6500/api/v2/mcp", http_client=http) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools = await session.list_tools()        # 148 инструментов
            res = await session.call_tool("ping", {}) # {"result":{"content":[{"type":"text","text":"\"pong\""}],"isError":false}}
```

---

## Сценарий 5. Запуск терминала

1. Перейти в папку с `OsEngine.exe` и запустить стартер:
   ```bash
   cd OsEngine/bin/Debug
   ./osEngineStarter.exe        # главное окно
   ./osEngineStarter.exe -robots   # BotStation (роботы)
   ./osEngineStarter.exe -tester   # тестер
   ./osEngineStarter.exe -optimizer # оптимизатор
   ./osEngineStarter.exe -data     # OsData
   ```
2. Проверить процесс:
   ```bash
   tasklist //FI "IMAGENAME eq OsEngine.exe"
   ```
3. Убедиться, что MCP жив: `osengine_ping`.

## Сценарий 6. Закрытие терминала

1. `osengine_terminal_stop` (корректное закрытие активного окна + MainWindow + процесс).
2. Подождать ~10 c, проверить `tasklist //FI "IMAGENAME eq OsEngine.exe"`.
3. Если процесс жив — `osengine_terminal_kill`.

## Сценарий 7. Поиск информации по бумаге

1. `osengine_wiki_securities_mapping_info` с `query` (тикер или подстрока имени), опц. `connector`, `limit`.
2. В ответе: `connector`, `is_trading_supported`, `is_data_feed_supported`, `security.name`, `security.nameClass`.
3. Точечно по коннектору: `osengine_wiki_securities_moex_iss` / `_tinvest` / `_alor` / `_qscalp`.

## Сценарий 8. Состояние сетов данных (OsData)

1. `osengine_terminal_get_status` → если `mode` ≠ `IsOsData`, `osengine_terminal_open_mode` `{"mode":"data"}` (работает только из `MainWindow`; иначе закрыть и перезапустить через `-data`).
2. Подождать 3–5 c.
3. `osengine_data_get_sets` → смотреть `name`, `regime`, `source`, `source_name`, `percent_load`, `securities`.
4. Статус сета: `osengine_data_get_set_status`; конкретной бумаги: `osengine_data_get_security_status`.

## Сценарий 9. Скачивание данных (создание сета)

> **Обязательные правила:** коннектор, имя сета, бумаги/таймфреймы/период — только от пользователя. Не выбирать молча.

1. Режим OsData (см. Сценарий 8, шаг 1).
2. `osengine_server_management_get_data_connectors` → показать варианты, спросить коннектор.
3. `osengine_server_management_activate` `{"type":"<коннектор>"}` → `name` = `source_name`.
4. (Для `MoexDataServer`) `osengine_server_instance_connect` `{"type":"MoexDataServer"}`, дождаться `osengine_server_instance_get_securities` с `count > 0`.
5. `osengine_data_create_set` `{"name":"MySet","source":"<коннектор>","source_name":"<из шага 3>","timeframes":["Min30"],"date_from":"2024-01-01T00:00:00","date_to":"2024-06-30T00:00:00"}`.
6. Найти бумаги: `osengine_server_instance_get_securities` `{"type":"...","filter":"SBER"}` или `osengine_wiki_securities_mapping_info`. Показать варианты пользователю.
7. `osengine_data_set_securities_add` `{"name":"MySet","securities":[{"name":"SBER","class":"...","exchange":""}]}`.
8. `osengine_data_set_on` `{"name":"MySet"}`.
9. Мониторить: `osengine_data_get_set_status` (раз в 5–10 c) → `status: Load`, `percent_load: 100` (или реальное < 100 при частичной загрузке). SSE: `data_set_load_completed_event`.
10. `osengine_data_set_off` при необходимости.

## Сценарий 10. Удаление сета данных

> Спросить, какой сет; показать список; получить явное подтверждение.

1. `osengine_data_get_sets` → показать, спросить имя (с префиксом `Set_` или без).
2. Подтверждение → `osengine_data_delete_set` `{"name":"MySet"}` → в ответе `deleted: true`.
3. Повторить `osengine_data_get_sets` для проверки.

## Сценарий 11. Прогон робота в тестере

> **Критические правила:** имена бумаг в тестере — **с расширением** `SBER.txt` (см. `osengine_tester_get_securities`); настроить **все** вкладки (`server_type: Tester`, `portfolio: GodMode`); тест идёт на скачанном сете.

1. Режим тестера: `osengine_terminal_open_mode` `{"mode":"testerlight"}` (или `./osEngineStarter.exe -testerlight`).
2. `osengine_tester_data_set_config` `{"source_type":"Set","set_name":"<сет>","type_tester_data":"Candle","date_from":"...","date_to":"..."}`; проверить `osengine_tester_get_securities` (имена с `.txt`).
3. `osengine_bot_create` `{"strategy_name":"TwoTimeFramesBot"}` → `bot_id`.
4. `osengine_bot_get_sources` `{"bot_id":"..."}` → имя вкладки.
5. `osengine_bot_set_config_tab_simple` `{"bot_id":"...","tab_name":"...","server_type":"Tester","server_name":"Tester","portfolio_name":"GodMode","emulator_is_on":true,"security_name":"SBER.txt","time_frame":"Min30"}`.
6. `osengine_bot_set_params` `{"bot_id":"...","parameters":{"Regime":"On"}}`.
7. Подождать 5–10 c (подключение бумаг), затем `osengine_tester_start` `{"fast_forward":true}`.
8. Ждать `osengine_tester_get_status` → `regime: Pause`, `progress_percent: 100`.
9. `osengine_bot_journal_get_statistics` `{"bot_name":"...","side":"All"}` → `deals_count > 0`.
10. `osengine_bot_delete` `{"bot_id":"..."}`, закрыть терминал.

## Сценарий 12. Журнал робота после теста

1. `osengine_bot_get_list`.
2. `osengine_bot_journal_get_summary` `{"bot_name":"..."}` (пустая строка — все роботы).
3. `osengine_bot_journal_get_equity` `{"bot_name":"...","chart_type":"DepositPercent"}`.
4. `osengine_bot_journal_get_statistics` `{"bot_name":"...","side":"All"}`.
5. `osengine_bot_journal_get_drawdown`, `osengine_bot_journal_get_open_positions`, `osengine_bot_journal_get_closed_positions`.

## Сценарий 13. Настройка скринера в тестере

1. `osengine_terminal_open_mode` `{"mode":"tester"}`.
2. `osengine_tester_data_set_config` (см. Сценарий 11).
3. `osengine_bot_create` `{"strategy_name":"AlgoStart1LinearRegression"}`.
4. `osengine_bot_set_config_tab_screener` `{"bot_id":"...","tab_name":"...","server_type":"Tester","server_name":"Tester","portfolio_name":"GodMode","emulator_is_on":true,"time_frame":"Min30","securities":[{"name":"SBER","class_name":"","is_on":true},{"name":"GAZP","class_name":"","is_on":true}]}`.
5. `osengine_bot_get_config_tab_screener` → `tabs_count` = число бумаг.
6. `osengine_bot_set_params` `{"bot_id":"...","parameters":{"Regime":"On","Volatility cluster to trade":0}}` (иначе кластер отсечёт все сделки).
7. `osengine_tester_start` `{"fast_forward":true}` → ждать `Pause`/100.
8. `osengine_bot_journal_get_statistics` → `deals_count > 0`.
9. `osengine_tester_stop`, `osengine_bot_delete`.

## Сценарий 14. Дивиденды по акции

1. `osengine_wiki_dividends_get_history` `{"ticker":"SBER"}` → `historical[]`, `count`, `source`.
2. `osengine_wiki_dividends_get_future` / `_get_past` `{"ticker":"SBER"}` → одна запись или `null`.
3. `osengine_wiki_dividends_search_by_date` `{"ticker":"SBER","date":"18.07.2025"}`.
4. `refresh: true` — принудительная перечитка файлов.

## Сценарий 15. Оптимизация робота

> **Критические правила (90% ошибок):**
> 1. Имена бумаг в оптимизаторе — **с расширением** `SBER.txt` (из `osengine_optimizer_data_get_status`).
> 2. Настроить **все** вкладки — иначе `optimizer_start` откажет с `No securities configured in robot tabs`.
> 3. У скринера бумаги — массивом одним вызовом (`securities`), внутренние вкладки пересоздаются сразу.
> 4. `optimizer_get_report` вернул `reports_count: 0` → проверить `osengine_optimizer_filters_get`.
> 5. Даты мастера перезаписываются хранилищем при загрузке — после `optimizer_data_set_config` перечитать `optimizer_data_get_config`, при откате применить ещё раз.
> 6. Большие диапазоны — минуты/десятки минут; не прерывать, пока `optimizer_get_status.is_running == true`.

1. `osengine_terminal_open_mode` `{"mode":"optimizer"}` (или `./osEngineStarter.exe -optimizer`).
2. `osengine_optimizer_data_get_status` → `available_sets`. Если нет — Сценарий 9.
3. Спросить робота/сет/диапазон/параметры (`osengine_wiki_robots_list`).
4. `osengine_optimizer_data_set_config` `{"source_type":"Set","set_name":"<сет>","date_from":"...","date_to":"..."}`; перечитать `osengine_optimizer_data_get_config`; дождаться `osengine_optimizer_data_get_status` → `is_loaded: true`.
5. `osengine_optimizer_bot_set` `{"strategy_name":"TwoTimeFramesBot"}` → `is_loaded: true`.
6. `osengine_optimizer_bot_tab_get_config`; настроить **каждую** вкладку:
   - Simple: `osengine_optimizer_bot_tab_set_config` `{"tab_name":"...","security_name":"SBER.txt","time_frame":"Min30"}`;
   - Screener: `{"tab_name":"...","time_frame":"Min30","securities":[{"name":"SBER.txt"},{"name":"GAZP.txt"}]}`.
7. `osengine_optimizer_params_get` → включить перебор хотя бы одного параметра + `Regime: On`:
   `osengine_optimizer_params_set` `{"parameters":[{"name":"PC length","value":20,"start":20,"stop":22,"step":1,"on":true},{"name":"Regime","value":"On"}]}`.
8. `osengine_optimizer_phases_set` `{"time_start":"...","time_end":"...","iteration_count":1,"percent_on_filtration":25,"last_in_sample":false}`.
9. `osengine_optimizer_filters_get` (выключить лишнее), `osengine_optimizer_get_pass_count`.
10. `osengine_optimizer_start` → `started: true` (или `errors` — список проблем готовности).
11. Ждать `osengine_optimizer_get_status` (раз в 30–60 c) / SSE `optimizer.test.progress`, `.finished`. Досрочно — `osengine_optimizer_stop` (`is_partial: true`).
12. `osengine_optimizer_get_report` → `fazes[].reports[]`: `parameters`, `total_profit`, `total_profit_percent`, `max_draw_down`, `profit_factor`, `sharp_ratio`, `positions_count`.
13. При необходимости `osengine_optimizer_save_report`.
14. Вернуть настройки, закрыть терминал.

## Сценарий 16. Запуск робота в торговлю (BotStation)

> **Критические правила:**
> 1. `Regime: On` — **только после явного подтверждения пользователя** (начало реальной торговли).
> 2. Реальная торговля — только на реальном брокере; `emulator_is_on` `true` → виртуальные заявки, `false` → реальные.
> 3. Имена бумаг — тикеры **без** `.txt` (из справочника сервера).
> 4. Настроить **все** вкладки (сервер, портфель, бумага, ТФ).
> 5. Позиции могут появиться через часы — это норма.

1. `osengine_terminal_get_status`; запуск `./osEngineStarter.exe -robotslight`.
2. Спросить робота/брокера/бумаги/ТФ (`osengine_wiki_robots_list`).
3. Настроить брокера (см. Сценарий 17): `osengine_server_management_activate` → `osengine_server_instance_set_params` (токен) → `osengine_server_instance_connect` → проверить `osengine_server_instance_get_status`/`_get_securities`/`_get_portfolios`.
4. `osengine_bot_create` `{"strategy_name":"TwoTimeFramesBot"}` → `bot_id`.
5. `osengine_bot_get_sources` → имя вкладки.
6. `osengine_bot_set_config_tab_simple` `{"bot_id":"...","tab_name":"...","server_type":"TInvest","server_name":"TInvest","portfolio_name":"<из шага 3>","emulator_is_on":true,"security_name":"SBER","time_frame":"Min30"}`.
7. (Опц.) `osengine_bot_set_position_support` `{"bot_id":"...","tab_name":"...","stop_is_on":true,"stop_distance":30,"profit_is_on":true,"profit_distance":15}`.
8. `osengine_bot_set_params` `{"bot_id":"...","parameters":{"PC length":21,"Regime":"Off"}}` → проверить `osengine_bot_get_params`.
9. **Показать конфигурацию, получить подтверждение** → `osengine_bot_set_params` `{"bot_id":"...","parameters":{"Regime":"On"}}`.
10. Контроль: `osengine_bot_position_get_open`, `osengine_bot_journal_get_summary`, SSE.
11. По завершении: `Regime: Off`, при необходимости `osengine_bot_delete`, закрыть терминал.

## Сценарий 17. Подключение брокера

> Токены/ключи — только от пользователя, в чат не выводить. Секреты в ответах маскируются.

1. `osengine_server_management_get_trade_connectors` → спросить коннектор.
2. `osengine_server_management_activate` `{"type":"TInvest"}`.
3. Если впервые: `osengine_server_instance_get_params` `{"type":"TInvest"}` → `osengine_server_instance_set_params` `{"type":"TInvest","parameters":{"token":"..."}}`.
4. `osengine_server_instance_connect` `{"type":"TInvest"}`.
5. `osengine_server_instance_get_status` → ждать `Connect` (до 30–60 c; SSE `server_instance.status_changed`).
6. `osengine_server_instance_get_securities` `{"type":"TInvest","filter":"SBER"}`, `osengine_server_instance_get_portfolios`.
7. При ошибке — `osengine_server_instance_get_log` `{"type":"TInvest","count":50}`.
8. Отключение: `osengine_server_instance_disconnect`.

## Сценарий 18. Сверка позиций роботов с биржей

> `compare_positions_sync_all`/`_sync_this` выставляют **рыночные ордера** — только после явного подтверждения и показа расхождений.

1. Режим BotStation (см. Сценарий 16).
2. `osengine_compare_positions_get_settings` `{"server_type":"TInvest","number":0}`.
3. (Опц.) `osengine_compare_positions_set_settings` / `osengine_compare_positions_set_ignored` (записать исходные, вернуть после).
4. `osengine_compare_positions_get` `{"server_type":"TInvest","number":0}` → расхождения по портфелям/бумагам.
5. Показать расхождения, спросить что синхронизировать.
6. После подтверждения: `osengine_compare_positions_sync_this` (одна бумага) или `osengine_compare_positions_sync_all` (портфель).
7. Повторить `osengine_compare_positions_get`, убедиться в сходимости.

## Сценарий 19. Справка по роботам и индикаторам

1. `osengine_wiki_robots_list` — список стратегий.
2. `osengine_wiki_robot_info` `{"class_name":"TwoTimeFramesBot"}` — параметры (имя/тип/значение/диапазон), вкладки.
3. `osengine_wiki_indicators_list`; `osengine_wiki_indicator_info` `{"class_name":"Bollinger"}`.

## Сценарий 20. Диагностика («что сломалось»)

1. `osengine_log_get_emergency_log` `{"count":50}` — исключения движка.
2. `osengine_log_get_mcp_log` `{"count":50}` — какие запросы приходили.
3. `osengine_system_load_get_current` — RAM/CPU/очереди.
4. `osengine_system_load_get_history` `{"type":"Ram","limit":50}`.
5. (Опц.) `osengine_system_load_set_settings` для включения сбора метрик.

## Сценарий 21. Настройка прокси

1. `osengine_proxy_get_list`.
2. `osengine_proxy_create` `{"ip":"203.0.113.10","port":1080,"is_on":true,"login":"user","password":"***"}`.
3. `osengine_proxy_ping` `{"number":3}` (до 10 c на мёртвом адресе).
4. `osengine_proxy_get_settings` / `osengine_proxy_set_settings`; `osengine_proxy_get_status`.
5. `osengine_proxy_delete` `{"number":3}` (только свой, с подтверждения).

## Сценарий 22. Шифрование и locked-режим

1. `osengine_encryption_get_status` → `status` (`Plain`/`Encrypted`/`Declined`), `unlocked`.
2. `osengine_encryption_enable` `{"password":"..."}` (мин. 8 символов) — включить.
3. После включения хост в locked-режиме: сессия создаётся `initialize`, внутри неё — `osengine_encryption_unlock` `{"password":"..."}`; прочие инструменты до разблокировки → `401`.
4. `osengine_encryption_disable` `{"password":"..."}` — выключить (деструктивно).
