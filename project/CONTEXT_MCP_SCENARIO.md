# OsEngine — Сценарии работы через MCP API

Пошаговые сценарии. Каждая глава — отдельная пользовательская задача. Только действия.

## Способ передать Кириллицу через git bash в MCP API OsEngine

`git bash` передаёт не-ASCII символы из командной строки в `curl` некорректно: вместо кириллицы сервер получает последовательность `��������`. Чтобы отправить запрос с кириллицей, JSON должен формироваться не в аргументах `curl`, а внутри shell-скрипта и передаваться `curl` через stdin (`-d @-`).

Для удобства в `OsEngine/bin/Debug/` есть скрипт `mcp_call.sh`:

```bash
cd OsEngine/bin/Debug
./mcp_call.sh wiki_securities_mapping_info '{"query":"Сбербанк","limit":10}'
```

Скрипт:
- формирует JSON внутри себя и передаёт его `curl` через stdin;
- отправляет запрос на `http://localhost:6500/api/v1/mcp`;
- если доступен PowerShell, извлекает `result.Content[0].Text` и выводит его как отформатированный JSON;
- если PowerShell недоступен, выводит сырой ответ `curl`.

Если в запросе нет кириллицы, скрипт не нужен — используйте обычный `curl -d '...'`.

### Как читать ответ MCP API

Ответ приходит в формате JSON-RPC:

```json
{
  "jsonrpc": "2.0",
  "result": {
    "Content": [
      {
        "Type": "text",
        "Text": "{...}"
      }
    ],
    "IsError": false
  },
  "error": null,
  "id": 1
}
```

Полезная нагрузка находится в `result.Content[0].Text`. Это строка, содержащая вложенный JSON. Чтобы отформатировать её через PowerShell:

```powershell
powershell -Command '$r = $input | ConvertFrom-Json; $r.result.Content[0].Text | ConvertFrom-Json | ConvertTo-Json -Depth 10' < response.json
```

Если ответ не влезает в консоль, сохраните его в файл в текущей папке:

```bash
curl -s -H "X-Api-Key: osengine-mcp-default-key" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_securities_mapping_info","arguments":{"query":"SBER","limit":10}},"id":1}' \
  http://localhost:6500/api/v1/mcp > response.json
```

## Сценарий 1. Пользователь просит запустить терминал

1. Перейти в папку с `OsEngine.exe`:
   ```bash
   cd OsEngine/bin/Debug
   ```

2. Запустить стартер:
   ```bash
   ./osEngineStarter.exe
   ```

3. Дождаться одного из сообщений:
   - `OsEngine started from ...`
   - `OsEngine is already running from ...`

С аргументами:
```bash
./osEngineStarter.exe -robots
```

Проверить, что процесс запущен:
```bash
tasklist //FI "IMAGENAME eq OsEngine.exe"
```

## Сценарий 2. Пользователь просит закрыть терминал OsEngine

1. Попробовать корректное завершение через MCP API:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_stop","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

2. Подождать 10 секунд:
   ```bash
   sleep 10
   ```

3. Проверить, что процесс завершился:
   ```bash
   tasklist //FI "IMAGENAME eq OsEngine.exe"
   ```

4. Если процесс всё ещё есть или MCP API не отвечал — принудительно завершить:
   ```bash
   taskkill //F //IM OsEngine.exe
   ```

5. Проверить снова:
   ```bash
   tasklist //FI "IMAGENAME eq OsEngine.exe"
   ```

## Сценарий 3. Пользователь просит найти информацию по бумаге

1. Убедиться, что терминал запущен (см. Сценарий 1).

2. Вызвать поиск по тикеру:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_securities_mapping_info","arguments":{"query":"SBER","limit":10}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

3. В ответе смотреть поля:
   - `connector` — название коннектора;
   - `is_trading_supported` — можно ли торговать;
   - `is_data_feed_supported` — можно ли получать данные;
   - `security.name` — тикер;
   - `security.nameClass` — класс бумаги.

Если нужен поиск по русскому названию, используйте способ из главы «Способ передать Кириллицу через git bash в MCP API OsEngine».

## Сценарий 4. Пользователь просит узнать состояние сетов данных

1. Убедиться, что терминал запущен (см. Сценарий 1).

2. Проверить текущий режим терминала:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_get_status","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

3. Если в ответе `mode` не равен `IsOsData`, открыть режим OsData:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_open_mode","arguments":{"mode":"data"}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   > `terminal_open_mode` работает только из `MainWindow`. Если уже открыт другой режим (`IsTester`, `IsOsTrader` и т.д.), сначала нужно корректно закрыть текущий процесс и запустить OsEngine заново без режима (см. Сценарий 2, затем Сценарий 1).

4. Подождать 3–5 секунд, пока загрузится окно OsData.

5. Запросить список сетов:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_get_sets","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

6. В ответе смотреть поля:
   - `name` — имя сета;
   - `regime` — `On` или `Off`;
   - `source` — тип коннектора (`MoexDataServer`, `Finam`, `BinanceSpot` и т.д.);
   - `source_name` — имя экземпляра коннектора;
   - `percent_load` — процент загрузки (может быть меньше 100, если часть данных не удалось загрузить);
   - `securities_count` — количество бумаг в сете;
   - `securities` — массив имён бумаг в сете.

7. Если сет включён (`On`) и загрузка не завершена, можно подписаться на SSE-события:
   - `data_set_load_completed_event` — сет завершил загрузку;
   - `data_set_security_load_completed_event` — конкретная бумага в сете завершила загрузку.

   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     http://localhost:6500/api/v1/events
   ```

## Сценарий 5. Пользователь просит создать новый сет данных

> **Обязательное правило:** перед созданием сета нужно спросить пользователя, под какой коннектор создавать сет. Нельзя выбирать коннектор самостоятельно и молча использовать произвольный источник. Это правило должны соблюдать все ИИ-агенты. Коннектор определяет, с какого сервера будут качаться данные, какие таймфреймы доступны и какие бумаги можно будет добавить.

1. Убедиться, что терминал запущен (см. Сценарий 1).

2. Проверить текущий режим терминала:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_get_status","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

3. Если в ответе `mode` не равен `IsOsData`, открыть режим OsData:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_open_mode","arguments":{"mode":"data"}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   > `terminal_open_mode` работает только из `MainWindow`. Если уже открыт другой режим (`IsTester`, `IsOsTrader` и т.д.), сначала нужно корректно закрыть текущий процесс и запустить OsEngine заново без режима (см. Сценарий 2, затем Сценарий 1).

4. Подождать 3–5 секунд, пока загрузится окно OsData.

5. **Спросить у пользователя коннектор.** Показать доступные варианты из `server_management_get_data_connectors`:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_management_get_data_connectors","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   Дождаться ответа пользователя. Примеры допустимых значений: `Finam`, `MoexDataServer`, `Binance`, `TInvest` и т.д. Точное значение берётся из перечисления `ServerType`.

6. **Спросить у пользователя имя нового сета.** Если пользователь не указал имя в запросе, запросить его. OsEngine добавит префикс `Set_` автоматически.

7. Активировать выбранный коннектор, если он ещё не активирован:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_management_activate","arguments":{"type":"Finam"}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   В ответе смотреть поле `name` — это `source_name` для создаваемого сета (обычно совпадает с типом коннектора для нулевого инстанса, например `Finam`).

8. Создать сет:
   ```bash
   cd OsEngine/bin/Debug
   ./mcp_call.sh data_create_set '{"name":"MyNewSet","source":"Finam","source_name":"Finam","timeframes":["Min5","Hour1","Day"],"date_from":"2024-01-01T00:00:00","date_to":"2024-12-31T00:00:00"}'
   ```

   > Для имён на кириллице или других не-ASCII символах обязательно использовать `mcp_call.sh`, чтобы избежать искажения символов в `git bash`.

9. В ответе проверить:
   - `name` — должно быть `Set_<имя>`;
   - `regime` — должно быть `Off`;
   - `source` и `source_name` — должны совпадать с выбранным коннектором;
   - `timeframes`, `date_from`, `date_to` — должны совпадать с запрошенными.

10. Сообщить пользователю, что сет создан, и уточнить, нужно ли добавить в него бумаги (см. Сценарий 6 — добавление бумаг в сет).

## Сценарий 6. Пользователь просит удалить сет данных

> **Обязательное правило:** перед удалением нужно спросить пользователя, какой именно сет удалять. Если пользователь не назвал сет, вывести список сетов через `data_get_sets` и дождаться ответа. Нельзя удалять сеты самостоятельно без явного подтверждения пользователя.

1. Убедиться, что терминал запущен (см. Сценарий 1).

2. Проверить текущий режим терминала:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_get_status","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

3. Если в ответе `mode` не равен `IsOsData`, открыть режим OsData:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_open_mode","arguments":{"mode":"data"}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   > `terminal_open_mode` работает только из `MainWindow`. Если уже открыт другой режим (`IsTester`, `IsOsTrader` и т.д.), сначала нужно корректно закрыть текущий процесс и запустить OsEngine заново без режима (см. Сценарий 2, затем Сценарий 1).

4. Подождать 3–5 секунд, пока загрузится окно OsData.

5. **Спросить у пользователя, какой сет удалить.** Если имя не указано, показать список сетов:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_get_sets","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   Дождаться ответа пользователя. Имя можно передавать с префиксом `Set_` или без него.

6. **Попросить подтверждение.** Перед удалением явно сообщить пользователю, какой сет будет удалён, и получить подтверждение.

7. Удалить сет:
   ```bash
   cd OsEngine/bin/Debug
   ./mcp_call.sh data_delete_set '{"name":"MySet"}'
   ```

   > Для имён на кириллице или других не-ASCII символах обязательно использовать `mcp_call.sh`, чтобы избежать искажения символов в `git bash`.

8. В ответе проверить:
   - `name` — должно совпадать с удаляемым сетом;
   - `deleted` — должно быть `true`.

9. Дополнительно можно запросить список сетов ещё раз, чтобы убедиться, что сет исчез:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_get_sets","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

10. Сообщить пользователю, что сет удалён.

## Сценарий 7. Пользователь просит скачать данные

> **Обязательное правило:** перед созданием сета нужно спросить пользователя, под какой коннектор создавать сет, и какие бумаги/таймфреймы/период нужны. Нельзя выбирать коннектор, бумаги или параметры самостоятельно.

1. Убедиться, что терминал запущен (см. Сценарий 1).

2. Проверить текущий режим терминала:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_get_status","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

3. Если в ответе `mode` не равен `IsOsData`, открыть режим OsData:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"terminal_open_mode","arguments":{"mode":"data"}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   > `terminal_open_mode` работает только из `MainWindow`. Если уже открыт другой режим (`IsTester`, `IsOsTrader` и т.д.), сначала нужно корректно закрыть текущий процесс и запустить OsEngine заново без режима (см. Сценарий 2, затем Сценарий 1).

4. Подождать 3–5 секунд, пока загрузится окно OsData.

5. **Спросить у пользователя коннектор.** Показать доступные варианты из `server_management_get_data_connectors`:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_management_get_data_connectors","arguments":{}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   Дождаться ответа пользователя. Примеры допустимых значений: `Finam`, `MoexDataServer`, `Binance`, `TInvest` и т.д.

6. **Спросить у пользователя параметры скачивания:**
   - имя нового сета;
   - список бумаг (тикеры);
   - таймфреймы (например, `Min1`, `Min5`, `Hour1`, `Day`);
   - период (`date_from`, `date_to`) в формате ISO 8601.

7. Активировать выбранный коннектор, если он ещё не активирован:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_management_activate","arguments":{"type":"MoexDataServer"}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   В ответе смотреть поле `name` — это `source_name` для создаваемого сета.

8. Подключить сервер, чтобы в его справочнике появились бумаги (особенно актуально для `MoexDataServer`):
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_connect","arguments":{"type":"MoexDataServer"}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

   Дождаться, пока `server_instance_get_securities` вернёт `count > 0`:
   ```bash
   curl -s -H "X-Api-Key: osengine-mcp-default-key" \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_get_securities","arguments":{"type":"MoexDataServer"}},"id":1}' \
     http://localhost:6500/api/v1/mcp
   ```

9. Создать сет:
   ```bash
   cd OsEngine/bin/Debug
   ./mcp_call.sh data_create_set '{"name":"MyDownloadSet","source":"MoexDataServer","source_name":"MoexDataServer","timeframes":["Min1","Min5"],"date_from":"2026-06-20T00:00:00","date_to":"2026-06-25T00:00:00"}'
   ```

10. **Перед добавлением бумаг запросить у коннектора доступные инструменты.** Это нужно, чтобы пользователь выбрал существующий тикер, а не придумал его.

    Вариант А — через `server_instance_get_securities` (точный справочник активного сервера):
    ```bash
    curl -s -H "X-Api-Key: osengine-mcp-default-key" \
      -H "Content-Type: application/json" \
      -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"server_instance_get_securities","arguments":{"type":"MoexDataServer","filter":"SBER"}},"id":1}' \
      http://localhost:6500/api/v1/mcp
    ```

    Вариант Б — через `wiki_securities_mapping_info` (поиск по всем коннекторам из Wiki):
    ```bash
    curl -s -H "X-Api-Key: osengine-mcp-default-key" \
      -H "Content-Type: application/json" \
      -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"wiki_securities_mapping_info","arguments":{"query":"SBER","connector":"MoexDataServer","limit":10}},"id":1}' \
      http://localhost:6500/api/v1/mcp
    ```

    Показать пользователю найденные варианты и дождаться, пока он выберет конкретную бумагу (или несколько).

11. Добавить выбранные бумаги в сет:
    ```bash
    ./mcp_call.sh data_set_securities_add '{"name":"MyDownloadSet","securities":[{"name":"SBER","class":"Акции#TQBR","exchange":""}]}'
    ```

    > Для имён на кириллице или других не-ASCII символах обязательно использовать `mcp_call.sh`, чтобы избежать искажения символов в `git bash`.

12. Включить сет (начать скачивание):
    ```bash
    curl -s -H "X-Api-Key: osengine-mcp-default-key" \
      -H "Content-Type: application/json" \
      -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_set_on","arguments":{"name":"MyDownloadSet"}},"id":1}' \
      http://localhost:6500/api/v1/mcp
    ```

13. **Мониторить загрузку.** Запрашивать статус сета каждые 5–10 секунд:
    ```bash
    curl -s -H "X-Api-Key: osengine-mcp-default-key" \
      -H "Content-Type: application/json" \
      -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_get_set_status","arguments":{"name":"MyDownloadSet"}},"id":1}' \
      http://localhost:6500/api/v1/mcp
    ```

    В ответе смотреть:
    - `status` — `Loading` (идёт загрузка) или `Load` (завершена);
    - `percent_load` — процент выполнения.

    Можно также смотреть статус конкретной бумаги:
    ```bash
    curl -s -H "X-Api-Key: osengine-mcp-default-key" \
      -H "Content-Type: application/json" \
      -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_get_security_status","arguments":{"name":"MyDownloadSet","security":"SBER","timeframe":"Min1"}},"id":1}' \
      http://localhost:6500/api/v1/mcp
    ```

14. Когда `status` стал `Load` и `percent_load` достиг 100 (или реального значения < 100, если часть данных не удалось загрузить), сообщить пользователю, что скачивание завершено.

15. По желанию пользователя выключить сет:
    ```bash
    curl -s -H "X-Api-Key: osengine-mcp-default-key" \
      -H "Content-Type: application/json" \
      -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"data_set_off","arguments":{"name":"MyDownloadSet"}},"id":1}' \
      http://localhost:6500/api/v1/mcp
    ```
