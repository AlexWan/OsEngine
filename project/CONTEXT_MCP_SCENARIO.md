# OsEngine — Сценарии работы через MCP API

Пошаговые сценарии. Каждая глава — отдельная пользовательская задача. Только действия.

## Правила для ИИ-агентов

> Эти правила помогут ИИ-агентам делать запросы к OsEngine напрямую из чата, не создавая временные скрипты.

1. **Всегда используйте `localhost`, а не `127.0.0.1`.**  
   OsEngine слушает `http://localhost:6500/`. `127.0.0.1` вернёт `400 Bad Request - Invalid Hostname`.

2. **Все инструменты вызываются через `tools/call`.**  
   Запрос должен содержать поле `id`. Без `id` сервер вернёт `202 Accepted` с пустым телом.

3. **Результат инструмента находится в `result.Content[0].Text`.**  
   Это JSON-строка. Её нужно распарсить отдельно. Не пытайтесь вытаскивать поля из ответа `grep`/`sed`/`awk` — кавычки могут быть экранированы как `\u0022`.

4. **Из Git Bash используйте `mcp_call.sh`.**  
   Он формирует JSON-RPC запрос и автоматически распаковывает `Content[0].Text`. Это работает даже без кириллицы и защищает от ошибок парсинга:

   ```bash
   cd OsEngine/bin/Debug
   ./mcp_call.sh tester_get_status
   ```

5. **Не создавайте временные `.py` / `.sh` / `.ps1` файлы.**  
   Если нужно подождать завершение длительной операции, делайте это серией прямых вызовов из чата или используйте SSE `/api/v1/events`.

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

Для ИИ-агентов рекомендуется использовать `mcp_call.sh` даже без кириллицы, потому что он сразу возвращает готовый JSON из `Content[0].Text` и исключает ошибки ручного парсинга. Если `mcp_call.sh` недоступен, используйте `curl` с разбором через PowerShell.

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
   ./mcp_call.sh data_create_set '{"name":"MyNewSet","source":"Finam","source_name":"Finam","timeframes":["Min30"],"date_from":"2024-01-01T00:00:00","date_to":"2024-06-30T00:00:00"}'
   ```

   > Для имён на кириллице или других не-ASCII символах обязательно использовать `mcp_call.sh`, чтобы избежать искажения символов в `git bash`.

9. В ответе проверить:
   - `name` — должно быть `Set_<имя>`;
   - `regime` — должно быть `Off`;
   - `source` и `source_name` — должны совпадать с выбранным коннектором;
   - `timeframes`, `date_from`, `date_to` — должны совпадать с запрошенными (отображаются фактически активные таймфреймы).

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
   - таймфреймы (например, `Min1`, `Min30`, `Hour1`, `Day`);
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
   ./mcp_call.sh data_create_set '{"name":"MyDownloadSet","source":"MoexDataServer","source_name":"MoexDataServer","timeframes":["Min30"],"date_from":"2024-01-01T00:00:00","date_to":"2024-06-30T00:00:00"}'
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

## Сценарий 8. Пользователь просит посмотреть журнал робота после теста

> Этот сценарий работает в режиме тестера (`IsTester`). Предполагается, что тест уже завершён (`tester_get_status` возвращает `regime: Pause` и `progress_percent: 100`).

1. Получить список роботов:
   ```bash
   ./mcp_call.sh bot_get_list
   ```

2. Получить сводку по журналу (для одного робота или для всех):
   ```bash
   ./mcp_call.sh bot_journal_get_summary '{"bot_name":"ParabolicBollinger"}'
   ```

   Чтобы посмотреть сводку по всем роботам, передайте пустую строку или `null`:
   ```bash
   ./mcp_call.sh bot_journal_get_summary '{"bot_name":""}'
   ```

3. Получить кривую эквити:
   ```bash
   ./mcp_call.sh bot_journal_get_equity '{"bot_name":"ParabolicBollinger","chart_type":"DepositPercent"}'
   ```

   Доступные `chart_type`:
   - `Absolute` — абсолютная прибыль;
   - `Percent1Contract` — процент на одну сделку;
   - `DepositPercent` — процент на депозит.

4. Получить статистику:
   ```bash
   ./mcp_call.sh bot_journal_get_statistics '{"bot_name":"ParabolicBollinger","side":"All"}'
   ```

   `side` может быть `All`, `Long`, `Short`.

5. Получить кривую просадки:
   ```bash
   ./mcp_call.sh bot_journal_get_drawdown '{"bot_name":"ParabolicBollinger"}'
   ```

6. Получить закрытые позиции:
   ```bash
   ./mcp_call.sh bot_journal_get_closed_positions '{"bot_name":"ParabolicBollinger","include_failed":false,"limit":100,"offset":0}'
   ```

7. Получить открытые позиции:
   ```bash
   ./mcp_call.sh bot_journal_get_open_positions '{"bot_name":"ParabolicBollinger","limit":100,"offset":0}'
   ```

8. Если нужно изменить настройки журнала (группировка, мультипликатор, вкл/выкл):
   ```bash
   ./mcp_call.sh bot_journal_set_settings '{"bot_name":"ParabolicBollinger","group":"NewGroup","mult":1.0,"is_on":true}'
   ```

   После изменения настроек журнала повторите нужные `bot_journal_get_*` запросы, чтобы увидеть пересчитанные значения.

## Сценарий 9. Пользователь просит настроить скринер в тестере

> Этот сценарий работает в режиме тестера (`IsTester`). Он настраивает робота-скринер, подключает к нему все бумаги из загруженного сета данных, включает робота и запускает тест.

1. Открыть режим тестера, если он ещё не открыт:
   ```bash
   ./mcp_call.sh terminal_open_mode '{"mode":"tester"}'
   ```

2. Дождаться готовности тестера:
   ```bash
   ./mcp_call.sh tester_data_get_config
   ```

3. Загрузить сет данных (если он ещё не загружен):
   ```bash
   ./mcp_call.sh tester_data_set_config '{"source_type":"Set","set_name":"McpReleaseSet","type_tester_data":"Candle","date_from":"2024-01-01T00:00:00","date_to":"2024-06-30T00:00:00","delete_trades_from_memory":true}'
   ```

4. Получить список бумаг, доступных в тестере:
   ```bash
   ./mcp_call.sh tester_get_securities
   ```

   Если бумаг нет — дальнейшая настройка скринера невозможна.

5. Создать робота-скринер:
   ```bash
   ./mcp_call.sh bot_create '{"strategy_name":"AlgoStart1LinearRegression"}'
   ```

6. Найти имя вкладки-скринера через `bot_get_sources`:
   ```bash
   ./mcp_call.sh bot_get_sources '{"bot_id":"AlgoStart1LinearRegression_1"}'
   ```

7. Настроить скринер: подключить все бумаги из сета, задать портфель и таймфрейм:
   ```bash
   ./mcp_call.sh bot_set_config_tab_screener '{"bot_id":"AlgoStart1LinearRegression_1","tab_name":"AlgoStart1LinearRegression_1tab0","server_type":"Tester","server_name":"Tester","portfolio_name":"GodMode","emulator_is_on":true,"time_frame":"Min30","securities":[{"name":"SBER","class_name":"","is_on":true},{"name":"GAZP","class_name":"","is_on":true}]}'
   ```

8. Проверить конфигурацию скринера и дождаться, пока `tabs_count` станет равен количеству бумаг:
   ```bash
   ./mcp_call.sh bot_get_config_tab_screener '{"bot_id":"AlgoStart1LinearRegression_1","tab_name":"AlgoStart1LinearRegression_1tab0"}'
   ```

   В ответе должны быть:
   - `tabs_count` — количество созданных дочерних вкладок (равно количеству бумаг);
   - `securities` — список подключённых бумаг;
   - `time_frame` — выбранный таймфрейм.

9. Включить робота. При использовании `AlgoStart1LinearRegression` с небольшим числом бумаг отключите волатильностный кластер, иначе фильтр не допустит ни одной сделки:
   ```bash
   ./mcp_call.sh bot_set_params '{"bot_id":"AlgoStart1LinearRegression_1","parameters":{"Regime":"On","Volatility cluster to trade":0}}'
   ```

10. Запустить тест:
    ```bash
    ./mcp_call.sh tester_start '{"fast_forward":true}'
    ```

11. Дождаться окончания теста, периодически запрашивая `tester_get_status`, пока `regime` не станет `Pause` и `time_now` не дойдёт до `time_end`.

12. Получить статистику по журналу:
    ```bash
    ./mcp_call.sh bot_journal_get_statistics '{"bot_name":"AlgoStart1LinearRegression_1","side":"All"}'
    ```

    Если `deals_count` больше 0 — скринер торговал, тест прошёл успешно.

13. Остановить тестер и удалить робота:
    ```bash
    ./mcp_call.sh tester_stop
    ./mcp_call.sh bot_delete '{"bot_id":"AlgoStart1LinearRegression_1"}'
    ```

## Сценарий 10. Пользователь просит посмотреть дивиденды по акции

> Дивиденды читаются из готовых markdown-файлов `Wiki/Dividends/{ticker}.md`. Для этого не нужен ни коннектор, ни режим терминала — достаточно, чтобы OsEngine был запущен и MCP API включён.

1. Убедиться, что терминал запущен (см. Сценарий 1).

2. Получить историю дивидендов по тикеру:
   ```bash
   ./mcp_call.sh wiki_dividends_get_history '{"ticker":"SBER"}'
   ```

   В ответе смотреть:
   - `historical` — массив выплаченных дивидендов;
   - `count` — количество записей;
   - `source` — ссылка на источник (Smart-Lab);
   - `last_updated` — дата последнего обновления файла.

3. Чтобы посмотреть дивиденды на конкретную дату в прошлом, передайте параметр `date`:
   ```bash
   ./mcp_call.sh wiki_dividends_get_history '{"ticker":"SBER","date":"01.01.2020"}'
   ```

   В ответе вернутся только записи с `registry_close_date <= 01.01.2020`.

4. Получить будущие отсечки:
   ```bash
   ./mcp_call.sh wiki_dividends_get_future '{"ticker":"SBER"}'
   ```

5. Получить ближайшую предстоящую отсечку:
   ```bash
   ./mcp_call.sh wiki_dividends_get_nearest '{"ticker":"SBER"}'
   ```

   Чтобы искать отсечку от конкретной даты, используйте `from_date`:
   ```bash
   ./mcp_call.sh wiki_dividends_get_nearest '{"ticker":"SBER","from_date":"01.01.2025"}'
   ```

6. Найти дивиденд по точной дате закрытия реестра:
   ```bash
   ./mcp_call.sh wiki_dividends_search_by_date '{"ticker":"SBER","date":"18.07.2025"}'
   ```

   В ответе `matches` содержит все записи с указанной датой (обычно 0 или 1).

7. Если нужно обновить кэш после ручного редактирования файлов, передайте `refresh=true`:
   ```bash
   ./mcp_call.sh wiki_dividends_get_history '{"ticker":"SBER","refresh":true}'
   ```
