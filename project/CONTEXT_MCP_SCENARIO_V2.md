# OsEngine — Сценарии работы через MCP API V2 (рекомендуемая)

> **MCP API V2 — текущая рекомендуемая версия.** Транспорт Streamable HTTP, camelCase-ответы, сессии.
> Сценарии V1 (легаси): [`CONTEXT_MCP_SCENARIO_V1.md`](CONTEXT_MCP_SCENARIO_V1.md). Полная справка по инструментам: [`CONTEXT_MCP_V2.md`](CONTEXT_MCP_V2.md).

Пошаговые сценарии. Каждая глава — отдельная пользовательская задача. Только действия.

## Как устроены сценарии

Агент работает с OsEngine через **инструменты `osengine_*`** (так их отдаёт OpenCode; в Python SDK это `session.call_tool("<имя>", {...})`, в curl — `tools/call` из Сценария 1). Имя инструмента — это имя MCP-тулзы с префиксом `osengine_`: `osengine_terminal_get_status` = тулза `terminal_get_status` и т.д. Аргументы передаются объектом JSON.

Внутри каждого сценария инструменты вызываются по имени, а под ними — что смотреть в ответе и что может пойти не так.

## Правила для ИИ-агентов

1. **Всегда `localhost`, не `127.0.0.1`.** HTTP Listener зарегистрирован на `http://localhost:6500/`; `127.0.0.1` вернёт `400 Invalid Hostname`.
2. **Работайте инструментами `osengine_*`** — они сами делают initialize/сессию/разбор под капотом. `curl` — только для отладки транспорта (Сценарий 1).
3. **Результат инструмента — уже готовые данные** (не конверт JSON-RPC). В сыром `curl` полезная нагрузка лежит в `result.content[0].text` (вложенная JSON-строка, camelCase).
4. **Не создавайте временные `.py` / `.sh` / `.ps1` файлы.** Если нужно ждать долгую операцию — делайте серией вызовов из чата или слушайте SSE (Сценарий 4).
5. **Перед работой проверяйте, что терминал запущен и в каком он режиме.** `osengine_terminal_get_status` → `mode` (`IsOsData`, `IsTester`, `IsOsOptimizer`, `IsOsTrader`, `IsMainWindow`). Режимы несовместимы: инструменты одного режима в другом вернут «master is not available». Не зовите инструменты «вслепую», если терминал мог быть закрыт.
6. **Кириллица не проблема** — через `osengine_*` кодировка не ломается (в отличие от `curl` в git bash).
7. **После задачи закрывайте терминал**, если пользователь не просил оставить его открытым: `osengine_terminal_stop` (Сценарий 6).

---

## Сценарий 1. Золотой путь: инициализация и вызов (curl, эталон транспорта)

Для отладки транспорта или интеграций не через MCP-клиент. В обычной работе из чата это не нужно — инструменты `osengine_*` делают всё сами.

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
  http://localhost:6500/api/v2/mcp
```

- Запрос **должен** содержать `id`. Без `id` (notification) сервер вернёт `202` с пустым телом.
- Ответ — `200` + JSON-RPC `result` (без `"error": null`), конверт camelCase: `result.content[0].text` — вложенная JSON-строка с данными.
- Запрос без `Mcp-Session-Id` (кроме `initialize`) → `400`; неизвестная сессия → `404`; неверный `MCP-Protocol-Version` → `400`.

## Сценарий 2. Подключение OpenCode и работа из чата

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
3. Работать ими напрямую из чата: `osengine_terminal_get_status`, `osengine_bot_create`, `osengine_data_create_set`, `osengine_optimizer_start` и т.д.

Так же подключаются Claude Code и MCP Inspector (URL `/api/v2/mcp`). Все сценарии ниже дают вызовы в форме `osengine_*`.

## Сценарий 3. Python SDK (официальный клиент)

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

## Сценарий 4. Получение серверных событий (SSE V2)

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

События по модулям:
- `tester.test.started` / `.progress` / `.finished` / `.paused` / `.resumed` — прогон тестера;
- `optimizer.test.progress` / `optimizer.test.finished` (`is_partial` при досрочной остановке) — прогон оптимизатора;
- `data_set_load_completed_event` / `data_set_security_load_completed_event` — загрузка сетов OsData;
- `server_instance.status_changed` / `.security.updated` / `.portfolio.updated` / `.log` — сервера брокеров;
- `terminal.launched` / `.mode_changed` / `.stopped`, `prime_settings.changed`, `heartbeat` (debug).

Типовой приём — читать поток фоном во время длительной операции (прогон тестера/оптимизатора, подключение брокера, скачивание данных) вместо частого опроса `*_get_status`. Если соединение оборвалось — переподключиться; пропущенные события не буферизуются, состояние добирается через `*_get_status`. Частота прогресса — не чаще раза в секунду.

---

## Сценарий 5. Запуск терминала

1. Найти папку с `OsEngine.exe`. Обычно это `<репо>/project/OsEngine/bin/Debug`, но путь зависит от структуры репозитория — если папки нет, найди `osEngineStarter.exe` поиском.
2. Запустить стартер из этой папки (в git bash `./osEngineStarter.exe`, в PowerShell `.\osEngineStarter.exe`):
   ```bash
   cd <путь>/OsEngine/bin/Debug
   ./osEngineStarter.exe             # главное окно
   ./osEngineStarter.exe -robots     # BotStation (роботы)
   ./osEngineStarter.exe -tester     # тестер
   ./osEngineStarter.exe -testerlight # облегчённый тестер
   ./osEngineStarter.exe -optimizer  # оптимизатор
   ./osEngineStarter.exe -data       # OsData
   ```
3. Дождаться сообщения `OsEngine started from ...` или `OsEngine is already running from ...` (стартер не блокирует — печатает строку и выходит).
4. Проверить процесс (в git bash `//FI`, в PowerShell `/FI`):
   ```bash
   tasklist //FI "IMAGENAME eq OsEngine.exe"
   ```
5. Убедиться, что MCP жив: `osengine_ping`. Хост поднимается не мгновенно — подожди 5–10 секунд после старта; если `ping` не ответил, повтори через пару секунд.

## Сценарий 6. Закрытие терминала

1. `osengine_terminal_stop` — корректное завершение (закрывает активное окно режима + MainWindow + процесс). Ответ `"Stopping terminal"` — это «останавливаюсь», а не «уже готово»: процесс умирает через несколько секунд.
2. Подождать ~10 c, проверить `tasklist //FI "IMAGENAME eq OsEngine.exe"` (в PowerShell — `/FI`).
3. Если процесс жив или MCP не отвечал — `osengine_terminal_kill` (принудительное завершение, аналог `taskkill /F`, последний рубеж).
4. После stop гаснет и MCP-хост: дальнейшие `osengine_*` вернут `fetch failed` — это ожидаемо, не поломка.

## Сценарий 7. Поиск информации по бумаге

1. Убедиться, что терминал запущен (Сценарий 5). Режим любой.
2. Вызвать поиск по тикеру:
   `osengine_wiki_securities_mapping_info` с `{"query":"SBER","limit":10}` (по русскому названию — `{"query":"Сбербанк","limit":10}`). Для точного совпадения тикера добавь `"exact":true` — иначе `SBER` тянет ещё `SBERP`, `SBERF` и опционы.
3. В ответе смотреть поля (внутри `results[]`):
   - `connector` / `connector_short` — название коннектора;
   - `is_trading_supported` — можно ли торговать;
   - `is_data_feed_supported` — можно ли получать данные;
   - `security.name` — тикер;
   - `security.nameClass` — класс бумаги;
   - `security.securityType` — тип (`Stock`/`Futures`/`Option`/`Bond`) — важно, чтобы не спутать акцию с фьючерсом/опционом/облигацией;
   - `security.nameFull` — полное название.
4. Точечно по коннектору: `osengine_wiki_securities_moex_iss` / `_tinvest` / `_alor` / `_qscalp`. Справочники — это кэш-снимок (в ответе `collected_at`); если данные устарели — `"refresh":true` перечитает с диска.

## Сценарий 8. Состояние сетов данных (OsData)

1. `osengine_terminal_get_status` → если `mode` ≠ `IsOsData`, открыть режим: `osengine_terminal_open_mode` `{"mode":"data"}`.
   > `terminal_open_mode` работает только из `MainWindow`. Если уже открыт другой режим (`IsTester`, `IsOsTrader` и т.д.), сначала корректно закрыть процесс и запустить заново без режима (Сценарий 6, затем Сценарий 5 с `./osEngineStarter.exe -data`).
2. Подождать 3–5 c, пока загрузится окно OsData.
3. `osengine_data_get_sets`. В ответе смотреть поля:
   - `name` — имя сета;
   - `regime` — `On` или `Off`;
   - `source` — тип коннектора (`MoexDataServer`, `Finam` и т.д.);
   - `source_name` — имя экземпляра коннектора;
   - `percent_load` — процент загрузки (может быть < 100, если часть данных не скачалась);
   - `securities_count` — число бумаг;
   - `securities` — массив имён бумаг.

   > **Внимание:** ответ может быть огромным — `securities` отдаётся **целиком** для каждого сета (у фьючерсных сетов по 300+ бумаг). Не вываливай его пользователю целиком — для сводки достаточно `name`/`regime`/`source`/`percent_load`/`securities_count`.
4. Если сет включён (`On`) и загрузка идёт — подписка на SSE: `data_set_load_completed_event`, `data_set_security_load_completed_event` (Сценарий 4).

## Сценарий 9. Создание сета данных

> **Обязательное правило:** коннектор и имя сета — только от пользователя. Нельзя выбирать коннектор молча.

1. Режим OsData (Сценарий 8, шаг 1–2).
2. `osengine_server_management_get_data_connectors` → показать варианты, спросить коннектор (допустимые значения из `ServerType`: `Finam`, `MoexDataServer`, `Binance`, `TInvest` и т.д.).
3. Спросить имя нового сета (OsEngine добавит префикс `Set_` сам).
4. Активировать коннектор, если ещё не активирован: `osengine_server_management_activate` `{"type":"Finam"}`. Ответ — **массив экземпляров**; `source_name` = `name` нужного экземпляра (обычно `number: 0`, для одноконнекторных совпадает с типом).
5. Создать сет:
   `osengine_data_create_set` `{"name":"MyNewSet","source":"Finam","source_name":"Finam","timeframes":["Min30"],"date_from":"2024-01-01T00:00:00","date_to":"2024-06-30T00:00:00"}`.
   Сет создаётся **выключенным** (`regime: Off`) — скачивание само не стартует, только после `data_set_on` (Сценарий 11).
6. В ответе проверить:
   - `name` — `Set_<имя>`;
   - `regime` — `Off`;
   - `source`/`source_name` — совпадают с коннектором;
   - `timeframes`, `date_from`, `date_to` — совпадают с запрошенными (отображаются фактически активные таймфреймы).
7. Сообщить, что сет создан, и уточнить, добавлять ли бумаги (Сценарий 11).

## Сценарий 10. Удаление сета данных

> **Обязательное правило:** какой сет удалять — только от пользователя, с явным подтверждением.

1. `osengine_data_get_sets` → показать список, спросить имя (с префиксом `Set_` или без). Для списка брать только сводные поля (см. Сценарий 8 — `securities` огромный).
2. Явно сообщить, какой сет будет удалён, получить подтверждение.
3. `osengine_data_delete_set` `{"name":"MySet"}`.
4. В ответе проверить: `name` совпадает, `deleted: true`.
5. Убедиться, что сет исчез — точечно, без повторного `data_get_sets`: `osengine_data_set_settings_get` `{"name":"MySet"}` вернёт `Set 'MySet' not found`.

## Сценарий 11. Скачивание данных

> **Обязательное правило:** коннектор, бумаги, таймфреймы и период — только от пользователя. Не выбирать молча.

1. Режим OsData (Сценарий 8, шаг 1–2).
2. `osengine_server_management_get_data_connectors` → спросить коннектор.
3. Спросить параметры: имя сета, список бумаг (тикеры), таймфреймы (`Min1`, `Min30`, `Hour1`, `Day`), период (`date_from`/`date_to` в ISO 8601).
4. `osengine_server_management_activate` `{"type":"MoexDataServer"}` → `name` = `source_name`.
5. Подключить сервер, чтобы появились бумаги (актуально для `MoexDataServer`): `osengine_server_instance_connect` `{"type":"MoexDataServer"}`. Дождаться, пока `osengine_server_instance_get_securities` `{"type":"MoexDataServer"}` вернёт `count > 0` — справочник грузится с задержкой, опрашивай повторно 10–30 c.
6. Создать сет: `osengine_data_create_set` `{"name":"MyDownloadSet","source":"MoexDataServer","source_name":"MoexDataServer","timeframes":["Min30"],"date_from":"2024-01-01T00:00:00","date_to":"2024-06-30T00:00:00"}`.
7. **Перед добавлением бумаг запросить у коннектора доступные инструменты**, чтобы пользователь выбрал существующий тикер, а не придумал его:
   - вариант А — точный справочник активного сервера: `osengine_server_instance_get_securities` `{"type":"MoexDataServer","filter":"SBER"}`;
   - вариант Б — поиск по всем коннекторам: `osengine_wiki_securities_mapping_info` `{"query":"SBER","connector":"MoexDataServer","limit":10}`.
   Показать варианты, дождаться выбора.
8. Добавить бумаги: `osengine_data_set_securities_add` `{"name":"MyDownloadSet","securities":[{"name":"SBER","class":"Акции#TQBR","exchange":""}]}`.
9. Включить сет: `osengine_data_set_on` `{"name":"MyDownloadSet"}`.
10. Мониторить загрузку раз в 5–10 c: `osengine_data_get_set_status` `{"name":"MyDownloadSet"}`. В ответе:
    - `status` — `Loading` (идёт) или `Load` (завершена);
    - `percent_load` — процент.
    Конкретная бумага: `osengine_data_get_security_status` `{"name":"MyDownloadSet","security":"SBER","timeframe":"Min1"}` → `objects_count` — реальное число скачанных свечей (надёжнее `percent_load`: подтверждает, что данные пришли, а не «100% от пустого»).
11. Когда `status == Load` и `percent_load` = 100 (или реальное значение < 100 при частичной загрузке) — сообщить, что скачивание завершено.
12. По желанию выключить сет: `osengine_data_set_off` `{"name":"MyDownloadSet"}`.

## Сценарий 11б. Скачать исторические стаканы (MarketDepthHistory)

> История стакана (`MarketDepthHistory`) — это режим `MarketDepth`, поддерживаемый **только** коннектором `QscalpMarketDepth` (у него `DataFeedTfMarketDepthHistoryCanLoad = true`, а живой `MarketDepth` — `false`). Качаются `.qsh`-файлы истории стакана.

1. Режим OsData (Сценарий 8, шаг 1–2).
2. Активировать Qscalp: `osengine_server_management_activate` `{"type":"QscalpMarketDepth"}` → `name` = `source_name`.
3. Создать сет с таймфреймом `"MarketDepthHistory"`:
   `osengine_data_create_set` `{"name":"MyMdSet","source":"QscalpMarketDepth","source_name":"QscalpMarketDepth","timeframes":["MarketDepthHistory"],"date_from":"2026-09-01T00:00:00","date_to":"2026-09-02T00:00:00"}`.
4. Добавить бумагу (тикер из справочника Qscalp): `osengine_data_set_securities_add` `{"name":"MyMdSet","securities":[{"name":"SBER","class":"...","exchange":""}]}`.
5. `osengine_data_set_on` `{"name":"MyMdSet"}` → загрузка `.qsh`.
6. Мониторить `osengine_data_get_set_status` / `osengine_data_get_security_status` `{"name":"MyMdSet","security":"SBER","timeframe":"MarketDepthHistory"}`.

> Не путай: `"MarketDepth"` (живой стакан) на Qscalp **не поддерживается** — MCP вернёт ошибку. Используй именно `"MarketDepthHistory"`.

## Сценарий 12. Журнал робота после теста

> Работает в режиме тестера (`IsTester`); предполагается, что тест завершён (`regime: Pause`, `progress_percent: 100`). Если журнал пуст (всё по нулям) — тест ещё не гоняли, это не ошибка; сначала прогони тест (Сценарий 14).

1. `osengine_bot_get_list` → взять реальное имя бота (в примерах ниже `ParabolicBollinger` — условный, подставляй своё).
2. `osengine_bot_journal_get_summary` `{"bot_name":"..."}` — сводка (прибыль абс/%, диапазон дат, число сделок). Пустая строка `{"bot_name":""}` — по всем роботам.
3. `osengine_bot_journal_get_equity` `{"bot_name":"...","chart_type":"DepositPercent"}`. `chart_type`: `Absolute` (абс. прибыль), `Percent1Contract` (% на сделку), `DepositPercent` (% на депозит).
4. `osengine_bot_journal_get_statistics` `{"bot_name":"...","side":"All"}`. `side`: `All`, `Long`, `Short`. Ключевые поля: `net_profit`, `deals_count`, `profit_factor`, `max_drawdown_percent`, `sharpe`, `recovery`, `profitable_deals`/`losing_deals`.
5. `osengine_bot_journal_get_drawdown` `{"bot_name":"..."}` — просадка.
6. `osengine_bot_journal_get_closed_positions` `{"bot_name":"...","include_failed":false,"limit":100,"offset":0}`.
7. `osengine_bot_journal_get_open_positions` `{"bot_name":"...","limit":100,"offset":0}`.
8. Если нужно изменить группировку/мультипликатор: `osengine_bot_journal_set_settings` `{"bot_name":"...","group":"NewGroup","mult":1.0,"is_on":true}`. После этого повторить нужные `bot_journal_get_*`.

## Сценарий 13. Настройка скринера в тестере

> Работает в `IsTester`. Настраивает робота-скринер, подключает бумаги из сета, включает робота и запускает тест.

1. `osengine_terminal_open_mode` `{"mode":"tester"}`.
2. Дождаться готовности: `osengine_tester_data_get_config`.
3. Загрузить сет: `osengine_tester_data_set_config` `{"source_type":"Set","set_name":"<из tester_data_get_available_sets>","type_tester_data":"Candle","date_from":"...","date_to":"...","delete_trades_from_memory":true}`. Имя сета брать из `osengine_tester_data_get_available_sets` — пример `McpReleaseSet` условный.
4. `osengine_tester_get_securities` — если бумаг нет, дальнейшая настройка скринера невозможна.
5. `osengine_bot_create` `{"strategy_name":"AlgoStart1LinearRegression"}`.
6. `osengine_bot_get_sources` `{"bot_id":"..."}` → имя вкладки-скринера.
7. `osengine_bot_set_config_tab_screener` `{"bot_id":"...","tab_name":"...","server_type":"Tester","server_name":"Tester","portfolio_name":"GodMode","emulator_is_on":true,"time_frame":"Min30","securities":[{"name":"SBER","class_name":"","is_on":true},{"name":"GAZP","class_name":"","is_on":true}]}`.
8. `osengine_bot_get_config_tab_screener` `{"bot_id":"...","tab_name":"..."}` → проверить `tabs_count` = число бумаг, `securities`, `time_frame`. Сразу после настройки `tabs_count` может быть `0` — внутренние вкладки создаются лениво, перепроверь через 3–5 c.
9. Включить робота. Для `AlgoStart1LinearRegression` с малым числом бумаг отключить волатильностный кластер, иначе фильтр не допустит ни одной сделки:
   `osengine_bot_set_params` `{"bot_id":"...","parameters":{"Regime":"On","Volatility cluster to trade":0}}`.
10. `osengine_tester_start` `{"fast_forward":true}`.
11. Ждать окончания: `osengine_tester_get_status` до `regime: Pause` и `time_now == time_end`.
12. `osengine_bot_journal_get_statistics` `{"bot_name":"...","side":"All"}` → `deals_count > 0` = скринер торговал. На коротком периоде `deals_count: 0` — это норма (стратегии нужен прогрев + длинный диапазон), а не ошибка.
13. `osengine_tester_stop`, `osengine_bot_delete` `{"bot_id":"..."}`.

## Сценарий 14. Прогон робота в тестере

> **Критические правила:**
> 1. **Имена бумаг в тестере — имена файлов С РАСШИРЕНИЕМ**: `SBER.txt`, а не `SBER`. Точные имена — в `osengine_tester_get_securities`.
> 2. **Настроить ВСЕ вкладки робота** — сервер `Tester`, портфель `GodMode`, бумага, таймфрейм. Без этого робот не получит данные и не будет торговать.
> 3. Тест идёт на уже скачанном сете. Если сета нет — сначала Сценарий 11.
> 4. После работы удалить временного робота (с подтверждения) и закрыть терминал (правило 7).

1. Режим тестера light: `osengine_terminal_open_mode` `{"mode":"testerlight"}` (или `./osEngineStarter.exe -testerlight`).
2. Спросить: робот, сет, диапазон, бумага, таймфрейм.
3. `osengine_tester_data_set_config` `{"source_type":"Set","set_name":"OptimizerToTestStend","type_tester_data":"Candle","date_from":"2024-01-01T00:00:00","date_to":"2024-03-31T00:00:00","delete_trades_from_memory":true}`.
4. Проверить, что бумаги загрузились: `osengine_tester_get_securities` (имена с `.txt`).
5. `osengine_bot_create` `{"strategy_name":"TwoTimeFramesBot"}` → вернёт `{"name":"TwoTimeFramesBot","number":N}`. Дальше используй **это точное имя** (или `number`) как `bot_id` — без суффикса `_1`.
6. `osengine_bot_get_sources` `{"bot_id":"TwoTimeFramesBot"}` → имя вкладки (вкладок может быть несколько — настраивать все).
7. Настроить **каждую** вкладку: `osengine_bot_set_config_tab_simple` `{"bot_id":"...","tab_name":"...","server_type":"Tester","server_name":"Tester","portfolio_name":"GodMode","emulator_is_on":true,"security_name":"SBER.txt","time_frame":"Min30"}`.
8. Включить робота (в тестере безопасно — заявки виртуальные): `osengine_bot_set_params` `{"bot_id":"...","parameters":{"Regime":"On"}}`.
9. **Перед стартом подождать 5–10 c** (подключение бумаг; в логе тестера «Инструмент … успешно подключен»). Ранний старт отклоняется ошибкой «идёт процедура подключения бумаг в торги» — просто повторить старт.
10. `osengine_tester_start` `{"fast_forward":true}`.
11. Ждать: `osengine_tester_get_status` до `regime: Pause` и `progress_percent: 100` (или `time_now == time_end`).
12. `osengine_bot_journal_get_statistics` `{"bot_name":"...","side":"All"}` → `deals_count > 0` = робот торговал. Дополнительно: `bot_journal_get_summary`, `_equity`, `_drawdown` (Сценарий 12).
13. `osengine_bot_delete` `{"bot_id":"..."}`, закрыть терминал.

## Сценарий 15. Дивиденды по акции

> Читается из готовых `Wiki/Dividends/{ticker}.md`. Коннектор и режим не нужны — только запущенный OsEngine. Параметр `date` — в формате `dd.MM.yyyy` (не ISO).

1. `osengine_wiki_dividends_get_history` `{"ticker":"SBER"}` → `historical[]`, `count`, `source` (Smart-Lab), `last_updated`.
2. На конкретную прошлую дату: `{"ticker":"SBER","date":"01.01.2020"}` → только `registry_close_date <= date`.
3. `osengine_wiki_dividends_get_future` `{"ticker":"SBER"}` → `future` = ближайшая `registry_close_date >= сегодня` или `null`.
4. `osengine_wiki_dividends_get_past` `{"ticker":"SBER"}` → `past` = ближайшая `registry_close_date <= date` или `null`.
5. По точной дате отсечки: `osengine_wiki_dividends_search_by_date` `{"ticker":"SBER","date":"18.07.2025"}` → `matches[]`.
6. После ручного редактирования файлов — `{"ticker":"SBER","refresh":true}`.

## Сценарий 16. Оптимизация робота

> **Критические правила (их нарушение — 90% ошибок при оптимизации):**
> 1. **Имена бумаг — файлы С РАСШИРЕНИЕМ**: `SBER.txt`, а не `SBER`. Точные имена — в `osengine_optimizer_data_get_status`. Если передать имя, которого нет в хранилище, `optimizer_bot_tab_set_config` вернёт ошибку со списком доступных имён.
> 2. **Настроить ВСЕ вкладки робота** — иначе `optimizer_start` откажет с `No securities configured in robot tabs`.
> 3. **У скринера бумаги задаются массивом одним вызовом** (`securities`), внутренние вкладки пересоздаются сразу. Портфель по умолчанию `GodMode` (переопределяется `portfolio_name`).
> 4. **Фильтры влияют на отчёт.** Если `optimizer_get_report` вернул `reports_count: 0` при завершённой оптимизации — проверить `osengine_optimizer_filters_get`.
> 5. **Даты мастера перезаписываются хранилищем, пока идёт загрузка.** После `optimizer_data_set_config` перечитать `optimizer_data_get_config`. Даты подстраиваются под реальный диапазон данных (например, `00:00:00` → `06:30:00` на МосБирже) — это норма, не откат; «применить ещё раз» нужно только если даты уехали не в ту сторону.
> 6. **Прогоны на больших диапазонах идут долго** (минуты–десятки минут). Не прерывать, пока `optimizer_get_status.is_running == true`.

1. `osengine_terminal_open_mode` `{"mode":"optimizer"}` (или `./osEngineStarter.exe -optimizer`).
2. `osengine_optimizer_data_get_status` → `available_sets`. Если нужного нет — Сценарий 11.
3. Спросить: робот, сет, диапазон, какие параметры перебираем (`osengine_wiki_robots_list`).
4. `osengine_optimizer_data_set_config` `{"source_type":"Set","set_name":"OptimizerToTestStend","date_from":"2024-01-01T00:00:00","date_to":"2024-03-31T00:00:00"}`. Перечитать `osengine_optimizer_data_get_config` (правило 5). Дождаться `osengine_optimizer_data_get_status` → `is_loaded: true`, `securities_count` = числу бумаг.
5. `osengine_optimizer_bot_set` `{"strategy_name":"TwoTimeFramesBot"}` → `is_loaded: true`.
6. `osengine_optimizer_bot_tab_get_config` → настроить **каждую** вкладку:
   - Simple: `osengine_optimizer_bot_tab_set_config` `{"tab_name":"<tab>","security_name":"SBER.txt","time_frame":"Min30"}`;
   - Screener: `{"tab_name":"<tab>","time_frame":"Min30","securities":[{"name":"SBER.txt"},{"name":"VTBR.txt"},{"name":"GAZP.txt"}]}` → проверить `securities_count` и `tabs_count` = числу бумаг.
7. `osengine_optimizer_params_get`. Включить перебор хотя бы одного параметра (`on: true`), иначе `optimizer_start` откажет. Заодно включить робота:
   `osengine_optimizer_params_set` `{"parameters":[{"name":"PC length","value":20,"start":20,"stop":22,"step":1,"on":true},{"name":"Regime","value":"On"}]}`. Остальные параметры лучше `"on":false` — иначе число проходов перемножится.
   `value` принимает число (`PC length`), строку (`Regime`, `Volume type`, `Asset in portfolio`) и булево (Bool-параметры) — по типу параметра.
8. `osengine_optimizer_phases_set` `{"time_start":"2024-01-01T00:00:00","time_end":"2024-03-31T00:00:00","iteration_count":1,"percent_on_filtration":25,"last_in_sample":false}` (`last_in_sample: false` — после каждой InSample идёт OutOfSample).
9. `osengine_optimizer_filters_get` (лишнее выключить), `osengine_optimizer_get_pass_count` (оценить время).
10. `osengine_optimizer_start` → `started: true`; если `started: false` — в `errors` список проблем готовности.
11. Ждать `osengine_optimizer_get_status` (раз в 30–60 c) / SSE `optimizer.test.progress`, `.finished`. Досрочно — `osengine_optimizer_stop` (`is_partial: true`).
12. `osengine_optimizer_get_report` → `fazes[].reports[]`: `parameters`, `total_profit`, `total_profit_percent`, `max_draw_down`, `profit_factor`, `sharp_ratio`, `positions_count`. Сохранить: `osengine_optimizer_save_report` `{"path":"..."}`.
13. Сообщить итоги: лучший бот, параметры и метрики на InSample/OutOfSample.
14. Вернуть изменённые настройки (сет, даты, робот, фильтры), закрыть терминал.

## Сценарий 17. Запуск робота в торговлю (BotStation)

> **Критические правила:**
> 1. **`Regime: On` — только после явного подтверждения пользователя** (начало реальной торговли). Сначала настраиваем и проверяем с `Regime: Off`.
> 2. **Реальная торговля — только на реальном брокере.** Эмулятор `Tester` в BotStation не заменяет брокера. Учётные данные — только от пользователя.
> 3. **Имена бумаг — биржевые тикеры БЕЗ `.txt`** (`SBER`), из справочника сервера. Правило `.txt` действует только в тестере и оптимизаторе.
> 4. **Настроить ВСЕ вкладки** — без сервера, портфеля и бумаги робот не будет торговать.
> 5. **Позиции могут появиться через часы** — зависит от таймфрейма и логики. Отсутствие позиций первые минуты — не неисправность.
> 6. После работы вернуть `Regime: Off` и закрыть терминал (правило 7).

1. `osengine_terminal_get_status`; для роботов нужен BotStation: `./osEngineStarter.exe -robotslight` (облегчённый) или `-robots` (полный).
2. Спросить: робот, брокер, бумаги, таймфрейм (`osengine_wiki_robots_list`).
3. Настроить реальный коннектор (см. Сценарий 18): `osengine_server_management_activate` → `osengine_server_instance_set_params` (токен) → `osengine_server_instance_connect` → проверить `osengine_server_instance_get_status`/`_get_securities`/`_get_portfolios`. Без этого шага дальше не идём.
4. `osengine_bot_create` `{"strategy_name":"TwoTimeFramesBot"}` → `bot_id`.
5. `osengine_bot_get_sources` `{"bot_id":"..."}` → имя вкладки.
6. Настроить **каждую** вкладку. Simple: `osengine_bot_set_config_tab_simple` `{"bot_id":"...","tab_name":"...","server_type":"TInvest","server_name":"TInvest","portfolio_name":"<портфель из шага 3>","emulator_is_on":true,"security_name":"SBER","time_frame":"Min30"}`. Screener — бумаги массивом `securities`.
   **`emulator_is_on`:** `true` — заявки виртуальные (данные реальные, исполнение эмулируется вкладкой); `false` — реальные заявки на счёт. Для первой проверки — `true`; переход на `false` — только по явному решению пользователя вместе с `Regime: On`.
7. (Опц.) Сопровождение позиции: `osengine_bot_set_position_support` `{"bot_id":"...","tab_name":"...","stop_is_on":true,"stop_distance":30,"profit_is_on":true,"profit_distance":15}`.
8. `osengine_bot_set_params` `{"bot_id":"...","parameters":{"PC length":21,"Regime":"Off"}}` → проверить `osengine_bot_get_params`.
9. **Показать итоговую конфигурацию, спросить подтверждение включения.** Только после явного «да»: `osengine_bot_set_params` `{"bot_id":"...","parameters":{"Regime":"On"}}`.
10. Контроль: `osengine_bot_position_get_open` `{"bot_id":"..."}`, `osengine_bot_journal_get_summary` `{"bot_name":"..."}`, SSE.
11. По завершении: `osengine_bot_set_params` `{"bot_id":"...","parameters":{"Regime":"Off"}}`; временного робота удалить с подтверждения (`osengine_bot_delete`); закрыть терминал.

## Сценарий 18. Подключение брокера

> **Критические правила:**
> 1. **Токены и ключи — только от пользователя.** Спросить явно, в чат не выводить, не логировать. В ответах API секреты маскируются.
> 2. Подключение и чтение статусов безопасны; действия, ведущие к заявкам, — только по Сценариям 17/19 с подтверждением.
> 3. Если коннектор уже настроен (есть сохранённые параметры) — шаг с `server_instance_set_params` пропустить.

1. `osengine_terminal_get_status` (управление серверами доступно из любого режима).
2. Спросить коннектор (`TInvest`, `Binance`, `Alor` и т.д.) и есть ли сохранённые настройки. Список: `osengine_server_management_get_trade_connectors`.
3. Активировать: `osengine_server_management_activate` `{"type":"TInvest"}` → массив экземпляров (`name`, `type`, `number`, `status`).
4. Если впервые: посмотреть текущие (секреты маскированы) `osengine_server_instance_get_params` `{"type":"TInvest"}` → записать токен `osengine_server_instance_set_params` `{"type":"TInvest","parameters":{"token":"<токен>"}}`.
5. Подключиться: `osengine_server_instance_connect` `{"type":"TInvest"}`.
6. `osengine_server_instance_get_status` `{"type":"TInvest"}` → ждать `Connect` (первое подключение до 30–60 c, опрашивать повторно; SSE `server_instance.status_changed`).
7. Справочники: `osengine_server_instance_get_securities` `{"type":"TInvest","filter":"SBER"}`, `osengine_server_instance_get_portfolios` `{"type":"TInvest"}`. Пусто — подождать и повторить.
8. Не подключается — `osengine_server_instance_get_log` `{"type":"TInvest","count":50}` (неверный токен, нет доступа к счёту, сеть).
9. Отключение (по просьбе): `osengine_server_instance_disconnect` `{"type":"TInvest"}`. Асинхронно — сразу возвращает `status: Connect`, реально отключается через несколько секунд (проверь `get_status` повторно).

## Сценарий 19. Сверка позиций роботов с биржей

> **Критические правила:**
> 1. **`compare_positions_sync_all` и `_sync_this` выставляют РЫНОЧНЫЕ ОРДЕРА** на реальном счёте. Только после явного подтверждения и показа полного списка расхождений. Без подтверждения — только чтение.
> 2. Сверка работает только на подключённом реальном коннекторе (Сценарий 18).
> 3. Роботы, не попавшие в сверку, и бумаги из `ignored_securities` в расхождения не входят — сначала проверьте настройки модуля.

1. Режим BotStation (Сценарий 17).
2. `osengine_compare_positions_get_settings` `{"server_type":"TInvest","number":0}` → `verification_period`, `time_delay_seconds`, `portfolios_to_watch`, `ignored_securities`.
3. (Опц., записать исходные) `osengine_compare_positions_set_settings` `{"server_type":"TInvest","number":0,"verification_period":"Min10","time_delay_seconds":10,"portfolios_to_watch":["<портфель>"]}`. `verification_period` только `Min1`/`Min5`/`Min10`/`Min30`. Исключения — `osengine_compare_positions_set_ignored` `{"server_type":"TInvest","number":0,"securities":["LKOH","ROSN"]}` (заменяет список целиком).
4. `osengine_compare_positions_get` `{"server_type":"TInvest","number":0}` → `portfolios[]` с разбивкой: сколько в учёте роботов, сколько фактически на бирже, расхождение по бумагам. Возвращает **все** портфели, даже если `portfolios_to_watch` пуст (пустой = модуль ни за кем не следит; у каждого портфеля есть флаг `is_watched`). Поле `status: "Error"` у бумаги — это **расхождение** (учёт не сходится с биржей), а не техническая ошибка.
5. Показать расхождения, спросить что синхронизировать. Без расхождений — сообщить, что учёт сходится.
6. После подтверждения: точечно `osengine_compare_positions_sync_this` `{"server_type":"TInvest","number":0,"portfolio_name":"<портфель>","security_name":"SBER"}` или весь портфель `osengine_compare_positions_sync_all` `{"server_type":"TInvest","number":0,"portfolio_name":"<портфель>"}`. Синхронизация закрывает лишнее и дооткрывает недостающее до состояния «как в учёте роботов».
7. Повторить `osengine_compare_positions_get`, убедиться в сходимости.
8. Вернуть изменённые настройки, закрыть терминал.

## Сценарий 20. Справка по роботам и индикаторам

> Только чтение. Режим не важен, коннекторы не нужны. Полезно перед Сценариями 16/17/14 — узнать параметры стратегии, типы, дефолты и какие вкладки она создаёт. Списки (`wiki_robots_list`/`wiki_indicators_list`) огромные — не вываливать пользователю целиком, суммировать; для конкретного робота/индикатора использовать точечные `wiki_robot_info`/`wiki_indicator_info` по `class_name`.

1. `osengine_wiki_robots_list` — список стратегий (имена классов для `bot_create`/`optimizer_bot_set`).
2. `osengine_wiki_robot_info` `{"class_name":"TwoTimeFramesBot"}` → описание, `parameters` (имя/тип/значение/диапазон), какие вкладки создаёт.
3. `osengine_wiki_indicators_list` — список индикаторов.
4. `osengine_wiki_indicator_info` `{"class_name":"Bollinger"}` → описание, параметры, серии.
5. Если спрашивают «что умеет терминал» — сводка из `wiki_robots_list` по группам + предложить сценарии (16 оптимизация, 17 реал, 14 тестер).

## Сценарий 21. Диагностика («что сломалось» / «почему тупит»)

> Только чтение (настройки сбора — по запросу). Режим любой.

1. `osengine_log_get_emergency_log` `{"count":50}` → плоский массив `[{"time","type","message"}, ...]`. Первое место при любой ошибке — туда пишутся исключения движка. Пустой `[]` — это «ошибок нет», а не сбой.
2. `osengine_log_get_mcp_log` `{"count":50}` — какие запросы приходили и что отвечал сервер.
3. `osengine_system_load_get_current` — точки по типам: `Ram` (память), `Cpu`, `Ecq`/`Moq` (очереди событий). Растущие очереди — терминал «тонет».
4. `osengine_system_load_get_history` `{"type":"Ram","limit":50}` (`type`: `Ram`, `Cpu`, `Ecq`, `Moq`).
5. Изменить сбор метрик (записать исходные): `osengine_system_load_get_settings`, затем `osengine_system_load_set_settings` `{"ram_collect_data_is_on":true,"ram_period":"OneSecond","ram_points_max":1000}`. Периоды: `OneSecond`, `TenSeconds`, `Minute`.
6. Сообщить вывод: какая ошибка в логе или что перегружено.

## Сценарий 22. Настройка прокси

> **Критические правила:**
> 1. **`proxy_ping` на мёртвом адресе блокирует до 10 секунд** — это таймаут проверки, не зависание API.
> 2. Номер назначается автоматически; дубликат (тот же `ip:port`) отклоняется ошибкой.
> 3. Пароли в ответах маскируются. Удаление — только с подтверждения и только свой прокси.

1. `osengine_proxy_get_list` → `proxies[]`: `number`, `ip`, `port`, `is_on`, `location`, `auto_ping_last_status`.
2. Спросить адрес/порт/логин/пароль. Создать: `osengine_proxy_create` `{"ip":"203.0.113.10","port":1080,"is_on":true,"login":"user","password":"***"}` → `number`.
3. Проверить связь: `osengine_proxy_ping` `{"number":3}` → `auto_ping_last_status` (`Success` или текст ошибки), `location`.
4. `osengine_proxy_get_settings` `{"number":3}`; изменить `osengine_proxy_set_settings` `{"number":3,"is_on":false,"ping_web_address":"https://api.ipify.org"}`.
5. `osengine_proxy_get_status` `{"number":3}` → `use_connection_count` — сколько коннекторов сейчас работают через прокси.
6. Удалить (только созданный на этом шаге, с подтверждения): `osengine_proxy_delete` `{"number":3}`.

## Сценарий 23. Шифрование и locked-режим

1. `osengine_encryption_get_status` → `status` (`Plain`/`Encrypted`/`Declined`), `unlocked`.
2. Включить: `osengine_encryption_enable` `{"password":"..."}` (мин. 8 символов). Работает только когда выключено; смена пароля через API недоступна.
3. После включения хост в locked-режиме: `initialize` создаёт сессию, внутри неё — `osengine_encryption_unlock` `{"password":"..."}`; прочие инструменты до разблокировки → `401`.
4. Выключить (деструктивно): `osengine_encryption_disable` `{"password":"..."}` — расшифрует все пароли серверов в открытый вид.
