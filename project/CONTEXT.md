# OsEngine — Контекст разработки

> Карта проекта. Агентам — сначала [`AGENTS.md`](AGENTS.md).

## Метаданные

| Параметр | Значение |
|----------|----------|
| **Платформа** | .NET 10 (`net10.0-windows`), WPF Desktop |
| **Язык** | C# |
| **Репозиторий** | https://github.com/AlexWan/OsEngine |
| **Сборка** | `dotnet build OsEngine.sln` |
| **Исполняемый файл** | `OsEngine/bin/Debug/OsEngine.exe` |
| **Стартер** | `OsEngine/bin/Debug/osEngineStarter.exe` |
| **MCP** | `http://localhost:6500/api/v1/mcp` (по умолчанию выключено) |
| **SSE** | `http://localhost:6500/api/v1/events` |
| **Тестовый стенд** | `Tests/McpTestStand/OsEngine.McpApi.TestStand/` |

## Что читать под задачу

| Задача | Файлы |
|--------|-------|
| Архитектура роботов | `CONTEXT_ROBOTS_ARCHITECTURE.md` |
| Робот / скринер | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_ROBOTS.md` |
| Индикатор | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_INDICATORS.md` |
| Индекс / спред | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_INDEX_AND_SPREAD.md` |
| Пары / фьючерсы | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_PAIRS_AND_FUTURES.md` |
| Сетки / MM | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_GRIDS.md` |
| HFT / стакан | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_HIGH_FREQUENCY.md` |
| Стопы / риск | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_POSITIONS_AND_RISK.md` |
| Мониторы | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_MONITORS.md` |
| Code style | `CONTEXT_CODING_GUIDELINES.md` |
| MCP API | `CONTEXT_MCP.md` |
| Сценарии MCP | `CONTEXT_MCP_SCENARIO.md` |
| Дорожная карта MCP | `CONTEXT_MCP_API_DEVELOPMENT.md` |
| Дивиденды в роботах | `CONTEXT_DIVIDENDS.md` |
| Промпты | `CONTEXT_PROMPTS_ROBOTS.md`, `CONTEXT_PROMPTS_INDICATORS.md` |

## Ключевые файлы

```
OsEngine/
  MainWindow.xaml.cs                    # Главное окно и MCP-хост
  MCP/McpMaster.cs                      # Маршрутизатор MCP
  MCP/Modules/OsDataApi.cs              # data_*
  OsData/OsDataMaster.cs                # Сеты данных
  OsData/OsDataSet.cs                   # Один сет
  OsTrader/Panels/Tab/BotPanel.cs       # Базовый робот
  OsTrader/Panels/Tab/BotTabSimple.cs   # Простой таб
  Market/                               # Коннекторы
  Entity/                               # Сущности
  Logging/                              # Логирование
```

## Сборка и запуск

```bash
# Сборка
dotnet build OsEngine.sln

# Корректный запуск (из папки exe)
cd OsEngine/bin/Debug
./osEngineStarter.exe -data

# Тестовый стенд
cd Tests/McpTestStand/OsEngine.McpApi.TestStand/bin/Debug/net10.0
./OsEngine.McpApi.TestStand.exe
```

**Важно:** перед `dotnet build` завершить `OsEngine.exe`, иначе файл заблокирован.

## Ссылки

[CONTEXT_ROBOTS_ARCHITECTURE.md](CONTEXT_ROBOTS_ARCHITECTURE.md) · [CONTEXT_ROBOTS.md](CONTEXT_ROBOTS.md) · [CONTEXT_INDICATORS.md](CONTEXT_INDICATORS.md) · [CONTEXT_DIVIDENDS.md](CONTEXT_DIVIDENDS.md) · [CONTEXT_CODING_GUIDELINES.md](CONTEXT_CODING_GUIDELINES.md) · [CONTEXT_MCP.md](CONTEXT_MCP.md) · [CONTEXT_MCP_SCENARIO.md](CONTEXT_MCP_SCENARIO.md) · [CONTEXT_MCP_API_DEVELOPMENT.md](CONTEXT_MCP_API_DEVELOPMENT.md)
