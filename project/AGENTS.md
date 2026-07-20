# AGENTS.md — Правила для ИИ-агентов

> Действует на корень проекта и подкаталоги. Собственный `AGENTS.md` в подкаталоге имеет приоритет.

## Перед работой

1. [`CONTEXT.md`](CONTEXT.md) — карта проекта.
2. [`CONTEXT_CODING_GUIDELINES.md`](CONTEXT_CODING_GUIDELINES.md) — стиль кода.
3. Доменный `CONTEXT_*.md` по задаче.

## Принципы

- Код изменяется только через инструменты (`WriteFile`, `StrReplaceFile`, `Shell`). Показать код в чате — не замена.
- Минимальные изменения. Сохраняй стиль и сигнатуры.
- Не ломай обратную совместимость без необходимости.
- Собирай и тестируй после правок.

## Сборка и тесты

```bash
# Завершить процесс, если запущен
taskkill /F /IM OsEngine.exe

# Сборка основного проекта (обычный случай)
dotnet build OsEngine/OsEngine.csproj

# Полная сборка решения — только если тронуты Tests/*
# (DividendsUpdater, McpTestStand и т.п.) или перед релизом
dotnet build OsEngine.sln

# Тестовый стенд MCP
cd Tests/McpTestStand/OsEngine.McpApi.TestStand/bin/Debug/net10.0
./OsEngine.McpApi.TestStand.exe
```

Цель стенда: **114/114 passed**.

**Важно:** тестовый стенд MCP API (`OsEngine.McpApi.TestStand.exe`) запускать только с **явного разрешения пользователя**.

Стенд работает в foreground. При запуске из Kimi Shell он создаёт собственное видимое консольное окно; вывод дублируется в это окно, в исходный stdout и в лог-файл `mcp-test-stand-yyyyMMdd-HHmmss.log` рядом с `.exe`. Запрещено использовать `run_in_background=true`. Длительность прогона — около 4 минут; дожидаться завершения через `TaskOutput(block=true)` или автоматическое уведомление.

## Исследование кода

- Известный путь / 1–2 запроса: `ReadFile`, `Grep`.
- Больше 3 запросов или незнакомый модуль: `Agent(subagent_type="explore")`.
- Планирование: `Agent(subagent_type="plan")`.
- Сложная задача: `Agent(subagent_type="coder")`.

## Запрещено без разрешения пользователя

- `git commit`, `git push`, `git reset`, `git rebase`.
- Изменения файлов за пределами рабочей директории.
- Установка ПО за пределами рабочей директории.
- Операции с правами администратора.

## Обновляй документацию

Если меняешь:

- MCP API → `CONTEXT_MCP.md`, `CONTEXT_MCP_API_DEVELOPMENT.md`.
- Сценарии MCP → `CONTEXT_MCP_SCENARIO.md`.
- Соглашения → `CONTEXT_CODING_GUIDELINES.md`.
- Карту проекта → `CONTEXT.md`.
- Правила агентов → этот файл.

## Среда

- Windows, Git Bash.
- Пути в Shell: используй относительные пути от рабочей директории проекта (`./OsEngine/...`, `./Tests/...`).
- Долгие операции — с `run_in_background=true`.

## Спрашивай пользователя

- Несколько валидных подходов.
- Неясный масштаб или требования.
- Нужны реальные учётные данные для тестов.

## Чек-лист перед ответом

- [ ] Код записан в файловую систему.
- [ ] Сборка успешна (`dotnet build OsEngine/OsEngine.csproj`; для `Tests/*` — `dotnet build OsEngine.sln`).
- [ ] Релевантные тесты пройдены.
- [ ] Документация обновлена при необходимости.
- [ ] Git не мутировал без разрешения.
