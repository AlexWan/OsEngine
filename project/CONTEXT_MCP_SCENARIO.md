# OsEngine — Сценарии работы через MCP API

Пошаговые сценарии. Каждая глава — отдельная пользовательская задача. Только действия.

## Способ передать Кириллицу через git bash в MCP API OsEngine

`git bash` передаёт не-ASCII символы из командной строки в `curl` некорректно: вместо кириллицы сервер получает последовательность `��������`. Чтобы отправить запрос с кириллицей, JSON должен формироваться не в аргументах `curl`, а внутри shell-скрипта и передаваться `curl` через stdin (`-d @-`).

Для удобства в `OsEngine/bin/Debug/` есть скрипт `mcp_call.sh`:

```bash
cd /f/OsEngine/project/OsEngine/bin/Debug
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
   cd /f/OsEngine/project/OsEngine/bin/Debug
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
