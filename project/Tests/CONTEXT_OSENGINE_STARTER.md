# CONTEXT_OSENGINE_STARTER — надёжный запуск OsEngine.exe (консольная программа)

> Описание утилиты `OsEngineStarter` — тонкая обёртка над `OsEngine.exe` для запуска из
> командной строки/скриптов с правильным рабочим каталогом и защитой от двойного запуска.

## 1. Цель

Запускать `OsEngine.exe` надёжно: `OsEngine.exe` — WPF-приложение, которое при старте
проверяет `Directory.GetCurrentDirectory()` (папку `Engine`, `QuikSharp.dll` рядом, одиночный
экземпляр). При запуске напрямую из чужой папки или из некоторых shell (Git Bash) рабочий
каталог оказывается неверным, и процесс закрывается ещё до инициализации MCP.
Стартер сам выставляет правильный `WorkingDirectory`, проверяет, не запущен ли уже
`OsEngine.exe` из этой же папки, и пробрасывает аргументы режима.

## 2. Расположение и имя

- Исходники: `project\Tests\OsEngineStarter\` (проект `OsEngineStarter`, `net10.0-windows`, консоль).
- Собранные файлы лежат **рядом с `OsEngine.exe`** (`OsEngine/bin/Debug/`):
  `osEngineStarter.exe` (apphost), `OsEngineStarter.dll`, `OsEngineStarter.runtimeconfig.json`.

## 3. Как работает

1. `baseDirectory` = папка, где лежит сам стартер.
2. Ищет `OsEngine.exe` в этой папке; нет — `Error: OsEngine.exe not found in …`, код `1`.
3. Проверяет `Process.GetProcessesByName("OsEngine")` с совпадением по каталогу процесса:
   если уже запущен из той же папки — печатает `OsEngine is already running from …`, код `0`
   (второй экземпляр не запускает).
4. Иначе запускает `OsEngine.exe` с `WorkingDirectory = baseDirectory`,
   `Arguments = string.Join(" ", args)` (проброс всех аргументов), `UseShellExecute = true`.
5. Печатает `OsEngine started from …`, код `0`.

## 4. Как запускать

```bash
cd OsEngine/bin/Debug
./osEngineStarter.exe              # главное окно (без аргументов)
./osEngineStarter.exe -robots      # BotStation (роботы)
./osEngineStarter.exe -robotslight # BotStation light
./osEngineStarter.exe -tester      # тестер
./osEngineStarter.exe -testerlight # облегчённый тестер
./osEngineStarter.exe -optimizer   # оптимизатор
./osEngineStarter.exe -data        # OsData
./osEngineStarter.exe -converter   # конвертер
```

Аргументы стартер **не валидирует** — просто пробрасывает их в `OsEngine.exe`.

## 5. Примечания

- **Почему не `./OsEngine.exe`:** напрямую из чужой папки/shell рабочий каталог может быть
  неверным → `OsEngine.exe` закроется до старта MCP. Стартер это снимает.
- **Перед `dotnet build` завершить запущенный `OsEngine.exe`** — процесс блокирует свой `.exe`.
- OsEngine не поддерживает несколько процессов из одной папки данных; для параллельных
  запусков нужны отдельные копии папки.
- Стартер не блокирует: печатает строку и выходит (процесс `OsEngine.exe` остаётся в фоне).
- Подробности проверок старта `OsEngine.exe` — в [`CONTEXT_MCP_V1.md`](../CONTEXT_MCP_V1.md), раздел 3.
