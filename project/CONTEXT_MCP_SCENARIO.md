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

6. **Перед работой проверяйте, что терминал вообще запущен и в каком он режиме.**  
   Вызовите `terminal_get_status`: поле `mode` покажет текущий режим (`IsOsData`, `IsTester`, `IsOsOptimizer`, `IsOsTrader`, главное окно). Режимы несовместимы: инструменты одного режима в другом вернут ошибку вида «master is not available». Не пытайтесь вызывать инструменты «вслепую», если терминал мог быть закрыт.

7. **После завершения задачи закрывайте терминал**, если пользователь не просил оставить его открытым.  
   Корректное закрытие — `terminal_stop` (см. Сценарий 2). Не оставляйте за собой запущенный OsEngine: он держит порт MCP, коннекторы и данные в памяти.

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

4. Получить ближайшую будущую отсечку:
   ```bash
   ./mcp_call.sh wiki_dividends_get_future '{"ticker":"SBER"}'
   ```

   В ответе `future` — одна запись с ближайшей `registry_close_date >= сегодня` или `null`.

5. Получить ближайшую прошлую отсечку:
   ```bash
   ./mcp_call.sh wiki_dividends_get_past '{"ticker":"SBER"}'
   ```

   Чтобы искать отсечку от конкретной даты, используйте `date`:
   ```bash
   ./mcp_call.sh wiki_dividends_get_past '{"ticker":"SBER","date":"01.01.2025"}'
   ```

   В ответе `past` — одна запись с ближайшей `registry_close_date <= date` или `null`.

6. Найти дивиденд по точной дате закрытия реестра:
   ```bash
   ./mcp_call.sh wiki_dividends_search_by_date '{"ticker":"SBER","date":"18.07.2025"}'
   ```

   В ответе `matches` содержит все записи с указанной датой (обычно 0 или 1).

7. Если нужно обновить кэш после ручного редактирования файлов, передайте `refresh=true`:
   ```bash
   ./mcp_call.sh wiki_dividends_get_history '{"ticker":"SBER","refresh":true}'
   ```

## Сценарий 11. Пользователь просит оптимизировать робота

> Полный цикл оптимизации: данные → робот → вкладки → параметры → фазы → запуск → отчёт → перенос параметров в бой.
>
> **Критические правила (их нарушение — 90% ошибок при оптимизации через API):**
> 1. **Имена бумаг в тестере и оптимизаторе — имена файлов данных С РАСШИРЕНИЕМ**: `SBER.txt`, а не `SBER`. Посмотреть точные имена можно в `optimizer_data_get_status` (через диалог хранилища в UI — колонка «Бумага»). Если передать имя, которого нет в хранилище, `optimizer_bot_tab_set_config` вернёт ошибку со списком доступных имён.
> 2. **Настроить нужно ВСЕ вкладки робота** — иначе `optimizer_start` откажет с `No securities configured in robot tabs`.
> 3. **У скринера бумаги задаются массивом одним вызовом** (`securities`), внутренние вкладки пересоздаются сразу. Портфель скринера по умолчанию `GodMode` (можно переопределить `portfolio_name`).
> 4. **Фильтры влияют на отчёт.** Если `optimizer_get_report` вернул `reports_count: 0` при завершённой оптимизации — первым делом проверяйте `optimizer_filters_get`.
> 5. **Даты мастера перезаписываются хранилищем, пока идёт загрузка данных.** После `optimizer_data_set_config` перечитайте `optimizer_data_get_config`; если даты откатились — примените ещё раз.
> 6. **Прогоны на больших диапазонах идут долго** (минуты — десятки минут). Не прерывайте оптимизацию, пока `optimizer_get_status.is_running == true`.

1. Запустить терминал в режиме оптимизатора (см. Сценарий 1):
   ```bash
   cd OsEngine/bin/Debug
   ./osEngineStarter.exe -optimizer
   ```

   Если терминал уже запущен без режима (главное окно), можно открыть режим через `./mcp_call.sh terminal_open_mode '{"mode":"optimizer"}'`.

2. Проверить состояние данных оптимизатора и список доступных сетов:
   ```bash
   ./mcp_call.sh optimizer_data_get_status
   ```

   В ответе смотреть `available_sets`. Если нужного сета нет — скачать его по Сценарию 7 (скачивание данных), затем вернуться сюда.

3. **Спросить у пользователя:** какого робота оптимизируем, какой сет и диапазон дат, какие параметры перебираем. Список роботов — `./mcp_call.sh wiki_robots_list`.

4. Установить источник данных и диапазон:
   ```bash
   ./mcp_call.sh optimizer_data_set_config '{"source_type":"Set","set_name":"OptimizerToTestStend","date_from":"2024-01-01T00:00:00","date_to":"2024-03-31T00:00:00"}'
   ```

   Перечитать конфиг и убедиться, что значения закрепились (см. правило 5):
   ```bash
   ./mcp_call.sh optimizer_data_get_config
   ```

   Дождаться загрузки хранилища: `optimizer_data_get_status` → `is_loaded: true` и `securities_count` равен числу бумаг сета.

5. Выбрать робота:
   ```bash
   ./mcp_call.sh optimizer_bot_set '{"strategy_name":"TwoTimeFramesBot"}'
   ```

   В ответе `is_loaded` должен быть `true`. Список вкладок робота:
   ```bash
   ./mcp_call.sh optimizer_bot_tab_get_config
   ```

6. Настроить **каждую** вкладку. Для `Simple`:
   ```bash
   ./mcp_call.sh optimizer_bot_tab_set_config '{"tab_name":"<tab_name из шага 5>","security_name":"SBER.txt","time_frame":"Min30"}'
   ```

   Для `Screener` — все бумаги одним вызовом:
   ```bash
   ./mcp_call.sh optimizer_bot_tab_set_config '{"tab_name":"<tab_name>","time_frame":"Min30","securities":[{"name":"SBER.txt"},{"name":"VTBR.txt"},{"name":"GAZP.txt"}]}'
   ```

   В ответе для скринера проверить: `securities_count` равен числу бумаг, `tabs_count` равен числу бумаг (внутренние вкладки созданы).

7. Настроить параметры перебора. Сначала посмотреть текущие:
   ```bash
   ./mcp_call.sh optimizer_params_get
   ```

   Включить перебор нужного параметра и задать диапазон (хотя бы один параметр с `on: true`, иначе `optimizer_start` откажет). Заодно включить робота (`Regime: On`):
   ```bash
   ./mcp_call.sh optimizer_params_set '{"parameters":[{"name":"PC length","value":20,"start":20,"stop":22,"step":1,"on":true},{"name":"Regime","value":"On"}]}'
   ```

   Остальные параметры лучше выключить (`"on":false`) — иначе число проходов перемножится.

8. Настроить фазы walk-forward:
   ```bash
   ./mcp_call.sh optimizer_phases_set '{"time_start":"2024-01-01T00:00:00","time_end":"2024-03-31T00:00:00","iteration_count":1,"percent_on_filtration":25,"last_in_sample":false}'
   ```

   `last_in_sample: false` — после каждой InSample-фазы идёт форвард (OutOfSample); `true` — только одна InSample-фаза на весь диапазон.

9. Проверить/настроить фильтры и число проходов:
   ```bash
   ./mcp_call.sh optimizer_filters_get
   ./mcp_call.sh optimizer_get_pass_count
   ```

   Если фильтры не нужны — выключить: `optimizer_filters_set` со всеми `*_is_on: false`. `pass_count` — сколько ботов будет прогнано; при сотнях проходов оцените время (правило 6).

10. Запустить оптимизацию:
    ```bash
    ./mcp_call.sh optimizer_start
    ```

    В ответе `started: true` — работа пошла. Если `started: false` — в `errors` список проблем готовности (не настроены вкладки, Regime выключен, нет параметров в переборе, пустое хранилище, нет фаз).

11. Ждать завершения. Периодически (раз в 30–60 секунд):
    ```bash
    ./mcp_call.sh optimizer_get_status
    ```

    Пока `is_running: true` — идёт работа, смотреть `prime_progress`. Также можно слушать SSE: `optimizer.test.progress`, `optimizer.test.finished`:
    ```bash
    curl -s -H "X-Api-Key: osengine-mcp-default-key" http://localhost:6500/api/v1/events
    ```

    Остановка досрочно (отчёт будет частичным, `is_partial: true`):
    ```bash
    ./mcp_call.sh optimizer_stop
    ```

12. Получить отчёт:
    ```bash
    ./mcp_call.sh optimizer_get_report
    ```

    Структура: `fazes[]` (по каждой фазе `reports[]`): `bot_name`, полные значения `parameters`, метрики (`total_profit`, `total_profit_percent`, `positions_count`, `max_draw_down`, `profit_factor`, `sharp_ratio`, `average_time_in_position`). Если `reports_count: 0` — см. правило 4 (фильтры). Отчёт можно сохранить в файл:
    ```bash
    ./mcp_call.sh optimizer_save_report '{"path":"F:\\OsEngine\\project\\OsEngine\\bin\\Debug\\my-optimizer-report.txt"}'
    ```

13. Сообщить пользователю итоги: лучший бот, его параметры и метрики на InSample и OutOfSample.

14. Вернуть настройки оптимизатора в исходное состояние (сет, даты, робот, фильтры — что меняли) и закрыть терминал через `terminal_stop` (см. Сценарий 2), если пользователь не просил оставить его открытым.

## Сценарий 12. Пользователь просит запустить робота в торговлю

> Создание и запуск робота в BotStation: вкладки, параметры, сопровождение, включение.
>
> **Критические правила:**
> 1. **`Regime: On` — только после явного подтверждения пользователя.** Это начало реальной торговли (выставление заявок). Сначала робот настраивается и проверяется с `Regime: Off`.
> 2. **Реальная торговля — только на реальном брокере.** Эмулятор `Tester` в BotStation не заменяет брокера: без настроенного реального коннектора (шаг 3) сценарий не выполняется. Учётные данные — только от пользователя.
> 3. **Имена бумаг здесь БЕЗ `.txt`** — это биржевые тикеры из справочника сервера (`SBER`), а не файлы хранилища. Правило `.txt` действует только в тестере и оптимизаторе.
> 4. **Настроить нужно ВСЕ вкладки робота** — без сервера, портфеля и бумаги во вкладке робот не будет торговать.
> 5. **Позиции могут появиться через часы** — зависит от таймфрейма и логики робота. Отсутствие позиций первые минуты — не признак неисправности; проверяйте лог робота и статус подключения.
> 6. После работы вернуть `Regime: Off` и закрыть терминал (правило 7), если пользователь не просил иное.

1. Проверить режим терминала (правило 6):
   ```bash
   ./mcp_call.sh terminal_get_status
   ```

   Для роботов нужен режим BotStation. Запуск (см. Сценарий 1):
   ```bash
   ./osEngineStarter.exe -robotslight
   ```

   `-robotslight` — облегчённый BotStation, `-robots` — полный.

2. **Спросить у пользователя:** какого робота запускаем, на каком брокере, какие бумаги и таймфрейм. Список стратегий:
   ```bash
   ./mcp_call.sh wiki_robots_list
   ```

3. **Настроить реальный коннектор брокера (обязательно).** Без этого шага дальше не идём:
   ```bash
   ./mcp_call.sh server_management_activate '{"type":"TInvest"}'
   ./mcp_call.sh server_instance_set_params '{"type":"TInvest","parameters":{"token":"<токен пользователя>"}}'
   ./mcp_call.sh server_instance_connect '{"type":"TInvest"}'
   ```

   Проверить подключение и получить справочники:
   ```bash
   ./mcp_call.sh server_instance_get_status '{"type":"TInvest"}'
   ./mcp_call.sh server_instance_get_securities '{"type":"TInvest","filter":"SBER"}'
   ./mcp_call.sh server_instance_get_portfolios '{"type":"TInvest"}'
   ```

   Токен/ключи — только от пользователя, в чат не выводить. Подробности — Сценарий 16 (подключение брокера).

4. Создать робота:
   ```bash
   ./mcp_call.sh bot_create '{"strategy_name":"TwoTimeFramesBot"}'
   ```

   В ответе — `bot_id` (например, `TwoTimeFramesBot_1`).

5. Посмотреть вкладки робота:
   ```bash
   ./mcp_call.sh bot_get_sources '{"bot_id":"TwoTimeFramesBot_1"}'
   ```

6. Настроить **каждую** вкладку. Simple (сервер и портфель брокера из шага 3):
   ```bash
   ./mcp_call.sh bot_set_config_tab_simple '{"bot_id":"TwoTimeFramesBot_1","tab_name":"TwoTimeFramesBot_1tab0","server_type":"TInvest","server_name":"TInvest","portfolio_name":"<портфель из шага 3>","emulator_is_on":true,"security_name":"SBER","time_frame":"Min30"}'
   ```

   Screener — бумаги массивом:
   ```bash
   ./mcp_call.sh bot_set_config_tab_screener '{"bot_id":"AlgoStart1LinearRegression_1","tab_name":"AlgoStart1LinearRegression_1tab0","server_type":"TInvest","server_name":"TInvest","portfolio_name":"<портфель из шага 3>","emulator_is_on":true,"time_frame":"Min30","securities":[{"name":"SBER"},{"name":"GAZP"}]}'
   ```

   Проверить конфигурацию: `bot_get_config_tab_simple` / `bot_get_config_tab_screener`. У скринера `tabs_count` должен стать равен числу бумаг.

   **`emulator_is_on`:** `true` — заявки виртуальные (данные реальные, исполнение эмулируется вкладкой); `false` — реальные заявки на счёт. Для первой проверки оставляйте `true`, переход на `false` — только по явному решению пользователя вместе с `Regime: On` (шаг 9).

7. При необходимости настроить сопровождение позиции:
   ```bash
   ./mcp_call.sh bot_set_position_support '{"bot_id":"TwoTimeFramesBot_1","tab_name":"TwoTimeFramesBot_1tab0","stop_is_on":true,"stop_distance":30,"profit_is_on":true,"profit_distance":15}'
   ```

8. Установить параметры стратегии и проверить их. **`Regime` пока `Off`:**
   ```bash
   ./mcp_call.sh bot_set_params '{"bot_id":"TwoTimeFramesBot_1","parameters":{"PC length":21,"Regime":"Off"}}'
   ./mcp_call.sh bot_get_params '{"bot_id":"TwoTimeFramesBot_1"}'
   ```

9. **Показать пользователю итоговую конфигурацию и спросить подтверждение включения.** Только после явного «да»:
   ```bash
   ./mcp_call.sh bot_set_params '{"bot_id":"TwoTimeFramesBot_1","parameters":{"Regime":"On"}}'
   ```

10. Контроль работы: позиции, журнал, события:
    ```bash
    ./mcp_call.sh bot_position_get_open '{"bot_id":"TwoTimeFramesBot_1"}'
    ./mcp_call.sh bot_journal_get_summary '{"bot_name":"TwoTimeFramesBot_1"}'
    ```

    Также доступны SSE-события (`curl -s -H "X-Api-Key: osengine-mcp-default-key" http://localhost:6500/api/v1/events`).

11. Выключить робота по просьбе пользователя или по завершении проверки:
    ```bash
    ./mcp_call.sh bot_set_params '{"bot_id":"TwoTimeFramesBot_1","parameters":{"Regime":"Off"}}'
    ```

    Если робот был временным (для проверки) — удалить с подтверждения пользователя:
    ```bash
    ./mcp_call.sh bot_delete '{"bot_id":"TwoTimeFramesBot_1"}'
    ```

12. Закрыть терминал (см. Сценарий 2), если пользователь не просил оставить его открытым.

## Сценарий 13. Пользователь просит сверить позиции роботов с биржей

> Сверка учёта позиций в роботах с фактическим состоянием счёта у брокера, и синхронизация расхождений.
>
> **Критические правила:**
> 1. **`compare_positions_sync_all` и `compare_positions_sync_this` выставляют РЫНОЧНЫЕ ОРДЕРА** на реальном счёте. Выполнять их можно только после явного подтверждения пользователя и показа ему полного списка расхождений. Без подтверждения — только чтение.
> 2. Сверка работает только на подключённом реальном коннекторе (см. Сценарий 12, шаг 3 — настройка коннектора).
> 3. Роботы, не попавшие в сверку, и бумаги из `ignored_securities` в расхождения не входят — сначала проверьте настройки модуля.

1. Проверить режим терминала (правило 6): сверка доступна в BotStation (`-robotslight` / `-robots`).

2. Проверить настройки модуля сверки:
   ```bash
   ./mcp_call.sh compare_positions_get_settings '{"server_type":"TInvest","number":0}'
   ```

   В ответе: `verification_period` (как часто сверяется модуль), `time_delay_seconds`, `portfolios_to_watch` (за какими портфелями следит), `ignored_securities` (бумаги-исключения).

3. При необходимости изменить настройки (записать исходные — вернуть после работы):
   ```bash
   ./mcp_call.sh compare_positions_set_settings '{"server_type":"TInvest","number":0,"verification_period":"Min10","time_delay_seconds":10,"portfolios_to_watch":["<портфель>"]}'
   ```

   `verification_period` принимает только `Min1`, `Min5`, `Min10`, `Min30` — другое значение молча игнорируется.

   Список исключений заменяется целиком (параметр называется `securities`):
   ```bash
   ./mcp_call.sh compare_positions_set_ignored '{"server_type":"TInvest","number":0,"securities":["LKOH","ROSN"]}'
   ```

4. Получить свежую сверку:
   ```bash
   ./mcp_call.sh compare_positions_get '{"server_type":"TInvest","number":0}'
   ```

   В ответе `portfolios[]` — по каждому портфелю позиции с разбивкой: сколько у роботов в учёте, сколько фактически на бирже, расхождение и по каким бумагам.

5. Показать пользователю список расхождений и **спросить, что синхронизировать**. Без расхождений — сообщить, что учёт сходится, и закончить.

6. Только после подтверждения пользователя — синхронизация. Точечно по одной бумаге:
   ```bash
   ./mcp_call.sh compare_positions_sync_this '{"server_type":"TInvest","number":0,"portfolio_name":"<портфель>","security_name":"SBER"}'
   ```

   Или по всему портфелю:
   ```bash
   ./mcp_call.sh compare_positions_sync_all '{"server_type":"TInvest","number":0,"portfolio_name":"<портфель>"}'
   ```

   Синхронизация закрывает лишнее и дооткрывает недостающее рыночными ордерами до состояния «как в учёте роботов».

7. Повторить сверку и убедиться, что расхождения устранены:
   ```bash
   ./mcp_call.sh compare_positions_get '{"server_type":"TInvest","number":0}'
   ```

8. Вернуть изменённые настройки модуля (шаг 3) и закрыть терминал (см. Сценарий 2), если пользователь не просил оставить его открытым.

## Сценарий 14. Пользователь просит прогнать робота в тестере

> Простой прогон одного робота на исторических данных в режиме тестера (`IsTester`).
>
> **Критические правила:**
> 1. **Имена бумаг в тестере — имена файлов данных С РАСШИРЕНИЕМ**: `SBER.txt`, а не `SBER`. Точные имена — в `tester_get_securities`.
> 2. **Настроить нужно ВСЕ вкладки робота** — сервер `Tester`, портфель `GodMode`, бумага, таймфрейм. Без этого робот не получит данные и не будет торговать.
> 3. Тест идёт на уже скачанном сете данных. Если сета нет — сначала Сценарий 7 (скачивание).
> 4. После работы удалить временного робота (с подтверждения) и закрыть терминал (правило 7).

1. Проверить режим терминала (правило 6). Нужен **тестер версии Light** (`testerlight`) — не полный тестер. Если терминал в главном окне, открыть:
   ```bash
   ./mcp_call.sh terminal_open_mode '{"mode":"testerlight"}'
   ```

   Запуск из командной строки: `./osEngineStarter.exe -testerlight`.

2. **Спросить у пользователя:** какого робота, какой сет, диапазон, бумага и таймфрейм.

3. Установить источник данных:
   ```bash
   ./mcp_call.sh tester_data_set_config '{"source_type":"Set","set_name":"OptimizerToTestStend","type_tester_data":"Candle","date_from":"2024-01-01T00:00:00","date_to":"2024-03-31T00:00:00","delete_trades_from_memory":true}'
   ```

   Проверить, что бумаги загрузились (имена с `.txt`):
   ```bash
   ./mcp_call.sh tester_get_securities
   ```

4. Создать робота:
   ```bash
   ./mcp_call.sh bot_create '{"strategy_name":"TwoTimeFramesBot"}'
   ```

5. Посмотреть вкладки:
   ```bash
   ./mcp_call.sh bot_get_sources '{"bot_id":"TwoTimeFramesBot_1"}'
   ```

6. Настроить **каждую** вкладку (сервер `Tester`, портфель `GodMode`):
   ```bash
   ./mcp_call.sh bot_set_config_tab_simple '{"bot_id":"TwoTimeFramesBot_1","tab_name":"TwoTimeFramesBot_1tab0","server_type":"Tester","server_name":"Tester","portfolio_name":"GodMode","emulator_is_on":true,"security_name":"SBER.txt","time_frame":"Min30"}'
   ```

7. Включить робота (в тестере это безопасно — заявки виртуальные):
   ```bash
   ./mcp_call.sh bot_set_params '{"bot_id":"TwoTimeFramesBot_1","parameters":{"Regime":"On"}}'
   ```

8. Запустить тест. **Перед стартом убедиться, что бумаги вкладок подключены** — после настройки вкладок подождите 5–10 секунд (в логе тестера появляются строки «Инструмент … успешно подключен»). Старт раньше времени отклоняется ошибкой «идёт процедура подключения бумаг в торги» — просто повторите старт:
   ```bash
   ./mcp_call.sh tester_start '{"fast_forward":true}'
   ```

9. Ждать окончания, периодически проверяя:
   ```bash
   ./mcp_call.sh tester_get_status
   ```

   Тест завершён, когда `regime` стал `Pause` и `progress_percent` дошёл до 100 (или `time_now` дошёл до `time_end`).

10. Получить статистику:
    ```bash
    ./mcp_call.sh bot_journal_get_statistics '{"bot_name":"TwoTimeFramesBot_1","side":"All"}'
    ```

    `deals_count > 0` — робот торговал. Дополнительно: `bot_journal_get_summary`, `bot_journal_get_equity`, `bot_journal_get_drawdown` (см. Сценарий 8).

11. Удалить временного робота (с подтверждения пользователя) и закрыть терминал (см. Сценарий 2):
    ```bash
    ./mcp_call.sh bot_delete '{"bot_id":"TwoTimeFramesBot_1"}'
    ```

## Сценарий 15. Пользователь просит рассказать о роботах и индикаторах

> Справка по встроенным стратегиям и индикаторам из Wiki терминала. Только чтение — режим терминала не важен, коннекторы не нужны.
>
> Сценарий нужен перед настройкой робота (Сценарии 11, 12, 14): какие параметры у стратегии есть, их типы, значения по умолчанию и диапазоны; какие вкладки робот создаёт; какой индикатор что считает.

1. Убедиться, что терминал запущен (см. Сценарий 1). Режим любой.

2. Получить список всех стратегий:
   ```bash
   ./mcp_call.sh wiki_robots_list
   ```

   В ответе — имена классов роботов, пригодные для `bot_create` / `optimizer_bot_set`.

3. Получить подробную карточку робота (параметр называется `class_name`):
   ```bash
   ./mcp_call.sh wiki_robot_info '{"class_name":"TwoTimeFramesBot"}'
   ```

   В ответе смотреть:
   - описание стратегии и логику работы;
   - `parameters` — все параметры: имя, тип, значение по умолчанию, диапазон для оптимизации;
   - какие вкладки (источники) создаёт робот и какие бумаги/таймфреймы им нужны.

4. Получить список индикаторов:
   ```bash
   ./mcp_call.sh wiki_indicators_list
   ```

5. Получить карточку индикатора (параметр называется `class_name`):
   ```bash
   ./mcp_call.sh wiki_indicator_info '{"class_name":"Bollinger"}'
   ```

   В ответе — описание расчёта, параметры индикатора и серии данных, которые он строит.

6. Если пользователь спрашивает «что умеет терминал» — дать сводку из `wiki_robots_list` по группам (трендовые, скринеры, арбитраж, сеточные и т.д.) и предложить сценарии запуска (11 — оптимизация, 12 — реал, 14 — тестер).

## Сценарий 16. Пользователь просит подключить брокера

> Активация и подключение коннектора биржи, проверка статуса, справочников и лога.
>
> **Критические правила:**
> 1. **Токены и ключи — только от пользователя.** Спросить их явно, не выводить в чат, не логировать. В ответах API секреты маскируются.
> 2. Подключение и чтение статусов безопасны; любые действия, ведущие к заявкам (роботы, синхронизация позиций), — только по сценариям 12/13 с подтверждением пользователя.
> 3. Если коннектор уже настроен (есть сохранённые параметры), шаг с `server_instance_set_params` пропускается.

1. Проверить режим терминала (правило 6). Управление серверами доступно из любого режима.

2. **Спросить у пользователя:** какой коннектор (`TInvest`, `Binance`, `Alor` и т.д.) и есть ли уже сохранённые настройки. Список доступных типов:
   ```bash
   ./mcp_call.sh server_management_get_trade_connectors
   ```

3. Активировать коннектор (создаёт экземпляр, если его нет):
   ```bash
   ./mcp_call.sh server_management_activate '{"type":"TInvest"}'
   ```

   В ответе — массив экземпляров: `name`, `type`, `number`, `status`.

4. Если коннектор настраивается впервые — записать параметры от пользователя. Сначала посмотреть текущие (секреты маскированы):
   ```bash
   ./mcp_call.sh server_instance_get_params '{"type":"TInvest"}'
   ```

   Записать токен:
   ```bash
   ./mcp_call.sh server_instance_set_params '{"type":"TInvest","parameters":{"token":"<токен пользователя>"}}'
   ```

5. Подключиться:
   ```bash
   ./mcp_call.sh server_instance_connect '{"type":"TInvest"}'
   ```

6. Проверить статус подключения:
   ```bash
   ./mcp_call.sh server_instance_get_status '{"type":"TInvest"}'
   ```

   Ждать `Connect` (первое подключение может занять до 30–60 секунд — опрашивать повторно). События смены статуса также приходят по SSE (`server_instance.status_changed`).

7. Получить справочники:
   ```bash
   ./mcp_call.sh server_instance_get_securities '{"type":"TInvest","filter":"SBER"}'
   ./mcp_call.sh server_instance_get_portfolios '{"type":"TInvest"}'
   ```

   Бумаги и портфели появляются после успешного `Connect`; если массивы пустые — подождать и повторить.

8. Если что-то не подключается — читать лог сервера:
   ```bash
   ./mcp_call.sh server_instance_get_log '{"type":"TInvest","count":50}'
   ```

   Типовые причины: неверный токен, нет доступа к счёту, сеть. Сообщить пользователю текст ошибки из лога.

9. Отключение (если попросил пользователь):
   ```bash
   ./mcp_call.sh server_instance_disconnect '{"type":"TInvest"}'
   ```

## Сценарий 17. Пользователь спрашивает «что сломалось» / «почему тупит»

> Диагностика: экстренный лог терминала, лог MCP-запросов, загруженность системы. Только чтение (настройки сбора — по запросу пользователя).

1. Убедиться, что терминал запущен (см. Сценарий 1). Режим любой.

2. Прочитать экстренный лог (последние ошибки всех модулей терминала):
   ```bash
   ./mcp_call.sh log_get_emergency_log '{"count":50}'
   ```

   В ответе `messages[]`: `time`, `type`, `message`. Это первое место, куда смотреть при любой ошибке — туда пишутся исключения движка.

3. Прочитать лог MCP API (какие запросы приходили и что отвечал сервер):
   ```bash
   ./mcp_call.sh log_get_mcp_log '{"count":50}'
   ```

4. Проверить текущую загруженность системы:
   ```bash
   ./mcp_call.sh system_load_get_current
   ```

   В ответе — точки по типам: `Ram` (память), `Cpu` (процессор), `Ecq`/`Moq` (очереди событий движка). Растущие очереди — признак того, что терминал «тонет».

5. Посмотреть историю загруженности:
   ```bash
   ./mcp_call.sh system_load_get_history '{"type":"Ram","limit":50}'
   ```

   `type`: `Ram`, `Cpu`, `Ecq`, `Moq`. `limit` — последние N точек (по умолчанию 100).

6. Если пользователь просит изменить сбор метрик — настройки (записать исходные, вернуть после):
   ```bash
   ./mcp_call.sh system_load_get_settings
   ./mcp_call.sh system_load_set_settings '{"ram_collect_data_is_on":true,"ram_period":"OneSecond","ram_points_max":1000}'
   ```

   Периоды: `OneSecond`, `TenSeconds`, `Minute`. Поля по каждому типу: `<тип>_collect_data_is_on`, `<тип>_period`, `<тип>_points_max`.

7. Сообщить пользователю вывод: что за ошибка в логе или что именно перегружено. При ошибке в чужом сценарии — сослаться на номер сценария и шаг, где она возникла.

## Сценарий 18. Работа с SSE-событиями терминала

> Потоковые уведомления `/api/v1/events`: как подключиться, что приходит, как не потерять события.

1. Подключение — обычный GET на порт MCP с ключом:
   ```bash
   curl -s -N -H "X-Api-Key: osengine-mcp-default-key" http://localhost:6500/api/v1/events
   ```

   Флаг `-N` обязателен: отключает буферизацию, события видны сразу. Соединение держится открытым, события идут текстовым потоком.

2. Формат каждого события — два поля, пустая строка-разделитель:
   ```
   event: tester.test.progress
   data: {"progress_percent":42.5,"regime":"Play"}

   ```

   `event` — имя (иерархия `модуль.тип.подтип`), `data` — JSON с полезной нагрузкой.

3. События по модулям:
   - `tester.test.progress`, `tester.test.finished` — прогон тестера;
   - `optimizer.test.progress`, `optimizer.test.finished` — прогон оптимизатора (`is_partial` при досрочной остановке);
   - `data_set_load_completed_event`, `data_set_security_load_completed_event` — загрузка сетов OsData;
   - `server_instance.status_changed`, `server_instance.security.updated`, `server_instance.portfolio.updated`, `server_instance.log` — сервера брокеров;
   - `terminal.mode_changed` — смена режима терминала.

4. Типовой приём — читать поток фоном во время длительной операции (прогон тестера/оптимизатора, подключение брокера, скачивание данных) вместо частого опроса `get_status`. Из Git Bash читать N секунд и выйти:
   ```bash
   timeout 30 curl -s -N -H "X-Api-Key: osengine-mcp-default-key" http://localhost:6500/api/v1/events
   ```

5. Если соединение оборвалось — подключиться заново (сервер не буферизует пропущенные события: всё, что произошло офлайн, потеряно; состояние всегда добирается через `*_get_status`).

6. Частота: события прогресса тестера/оптимизатора идут не чаще раза в секунду, остальные — по факту изменения.

## Сценарий 19. Пользователь просит настроить прокси

> Прокси-роутер терминала: список, создание, настройки, проверка связи, удаление.
>
> **Критические правила:**
> 1. **`proxy_ping` на мёртвом адресе блокирует до 10 секунд** — это не зависание API, это таймаут проверки.
> 2. Номер прокси назначается автоматически; дубликат (тот же `ip:port`) отклоняется ошибкой.
> 3. Пароли в ответах всегда маскируются. Удаление — только с подтверждения пользователя и только свой прокси.

1. Убедиться, что терминал запущен (см. Сценарий 1). Режим любой.

2. Посмотреть текущие прокси:
   ```bash
   ./mcp_call.sh proxy_get_list
   ```

   В ответе `proxies[]`: `number`, `ip`, `port`, `is_on`, `location`, `auto_ping_last_status`. Паролей там нет — они маскируются.

3. **Спросить у пользователя** адрес, порт, логин и пароль нового прокси. Создать:
   ```bash
   ./mcp_call.sh proxy_create '{"ip":"203.0.113.10","port":1080,"is_on":true,"login":"user","password":"***"}'
   ```

   В ответе — созданный `number`. Дубликат вернёт ошибку.

4. Проверить связь (может занять до 10 секунд):
   ```bash
   ./mcp_call.sh proxy_ping '{"number":3}'
   ```

   В ответе `auto_ping_last_status` — `Success` или текст ошибки, плюс `location`, если пинг удался.

5. Посмотреть/изменить настройки:
   ```bash
   ./mcp_call.sh proxy_get_settings '{"number":3}'
   ./mcp_call.sh proxy_set_settings '{"number":3,"is_on":false,"ping_web_address":"https://api.ipify.org"}'
   ```

6. Проверить статус использования:
   ```bash
   ./mcp_call.sh proxy_get_status '{"number":3}'
   ```

   `use_connection_count` — сколько коннекторов сейчас работают через этот прокси.

7. Удалить временный прокси (с подтверждения пользователя, только созданный на этом шаге):
   ```bash
   ./mcp_call.sh proxy_delete '{"number":3}'
   ```
