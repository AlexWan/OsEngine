# OsEngine — Контекст разработки

> Карта проекта. Агентам — сначала [`AGENTS.md`](AGENTS.md).

## Метаданные

| Параметр | Значение |
|----------|----------|
| **Платформа** | .NET 10 (`net10.0-windows`), WPF Desktop |
| **Язык** | C# |
| **Репозиторий** | https://github.com/AlexWan/OsEngine |
| **Сборка** | `dotnet build OsEngine/OsEngine.csproj` (всё решение — `dotnet build OsEngine.sln`) |
| **Исполняемый файл** | `OsEngine/bin/Debug/OsEngine.exe` |
| **Стартер** | `OsEngine/bin/Debug/osEngineStarter.exe` |
| **MCP (V2, рекомендуемый)** | `http://localhost:6500/api/v2/mcp` (Streamable HTTP) |
| **MCP (V1, легаси)** | `http://localhost:6500/api/v1/mcp` |
| **SSE (V1, легаси)** | `http://localhost:6500/api/v1/events` (в V2 события — через `GET /api/v2/mcp`) |
| **Тестовый стенд** | `Tests/McpTestStand/OsEngine.McpApi.TestStand/` |

## Что читать под задачу

| Задача | Файлы |
|--------|-------|
| Архитектура роботов | `CONTEXT_ROBOTS_ARCHITECTURE.md` |
| Робот / скринер | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_ROBOTS.md` |
| Секторальные роботы (SectorsSet) | `CONTEXT_SECTORS_SET.md` |
| Индикатор | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_INDICATORS.md` |
| Индекс / спред | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_INDEX_AND_SPREAD.md` |
| Пары / фьючерсы | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_PAIRS_AND_FUTURES.md` |
| Сетки / MM | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_GRIDS.md` |
| HFT / стакан | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_HIGH_FREQUENCY.md` |
| Стопы / риск | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_POSITIONS_AND_RISK.md` |
| Мониторы | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_MONITORS.md` |
| Code style | `CONTEXT_CODING_GUIDELINES.md` |
| Коннекторы | `CONTEXT_CONNECTORS.md` |
| MCP API (V2, рекомендуемая) | `CONTEXT_MCP_V2.md` |
| MCP API (V1, легаси) | `CONTEXT_MCP_V1.md` |
| Сценарии MCP (V2) | `CONTEXT_MCP_SCENARIO_V2.md` |
| Сценарии MCP (V1, легаси) | `CONTEXT_MCP_SCENARIO_V1.md` |
| Защита / пароли / блокировка | `CONTEXT_SECURITY.md` |
| Дорожная карта MCP | `TempContext/CONTEXT_MCP_API_DEVELOPMENT.md` |
| Дивиденды в роботах | `CONTEXT_DIVIDENDS.md` |
| Синтетические облигации | `CONTEXT_SYNTHETIC_BOND.md` |
| Ребалансировщик | `CONTEXT_REBALANCER.md` |
| Темы / цвета / оформление | `CONTEXT_THEMES.md` |
| Удалённые серверы (VPS/VDS) | `CONTEXT_VPS_VDS.md` |
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
# Сборка основного проекта (обычный случай)
dotnet build OsEngine/OsEngine.csproj

# Полная сборка решения — только если тронуты Tests/* или перед релизом
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

[CONTEXT_ROBOTS_ARCHITECTURE.md](CONTEXT_ROBOTS_ARCHITECTURE.md) · [CONTEXT_ROBOTS.md](CONTEXT_ROBOTS.md) · [CONTEXT_SECTORS_SET.md](CONTEXT_SECTORS_SET.md) · [CONTEXT_INDICATORS.md](CONTEXT_INDICATORS.md) · [CONTEXT_DIVIDENDS.md](CONTEXT_DIVIDENDS.md) · [CONTEXT_SYNTHETIC_BOND.md](CONTEXT_SYNTHETIC_BOND.md) · [CONTEXT_REBALANCER.md](CONTEXT_REBALANCER.md) · [CONTEXT_THEMES.md](CONTEXT_THEMES.md) · [CONTEXT_VPS_VDS.md](CONTEXT_VPS_VDS.md) · [CONTEXT_CODING_GUIDELINES.md](CONTEXT_CODING_GUIDELINES.md) · [CONTEXT_CONNECTORS.md](CONTEXT_CONNECTORS.md) · [CONTEXT_MCP_V2.md](CONTEXT_MCP_V2.md) · [CONTEXT_MCP_V1.md](CONTEXT_MCP_V1.md) · [CONTEXT_MCP_SCENARIO_V2.md](CONTEXT_MCP_SCENARIO_V2.md) · [CONTEXT_MCP_SCENARIO_V1.md](CONTEXT_MCP_SCENARIO_V1.md) · [TempContext/CONTEXT_MCP_API_DEVELOPMENT.md](TempContext/CONTEXT_MCP_API_DEVELOPMENT.md)
