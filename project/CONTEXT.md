# OsEngine — Контекст разработки

Это оглавление проекта. Выбирай файл по задаче.

## Метаданные проекта

| Параметр | Значение |
|----------|----------|
| **Название** | OsEngine |
| **Платформа** | .NET 10 (`net10.0-windows`) |
| **Тип приложения** | WPF Desktop |
| **Назначение** | Платформа для алгоритмической торговли на бирже |
| **Язык** | C# |
| **Репозиторий** | https://github.com/AlexWan/OsEngine |
| **Сборка** | `dotnet build OsEngine.sln` (Visual Studio или Rider) |

## Карта контекста

| Задача пользователя | Какой файл подключить |
|---------------------|----------------------|
| «Быстрый старт / базовая архитектура / выбор таба» | `CONTEXT_ROBOTS_ARCHITECTURE.md` |
| «Напиши робота / стратегию / скринер» | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_ROBOTS.md` |
| «Напиши индикатор / переделай индикатор» | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_INDICATORS.md` |
| «Индекс / спред / арбитраж против индекса» | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_INDEX_AND_SPREAD.md` |
| «Парный арбитраж / фьючерсы / контанго» | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_PAIRS_AND_FUTURES.md` |
| «Сеточная стратегия / маркет-мейкинг» | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_GRIDS.md` |
| «HFT / торговля по стакану / лента сделок» | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_HIGH_FREQUENCY.md` |
| «Стопы / тейки / трейлинг / риск-менеджмент» | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_POSITIONS_AND_RISK.md` |
| «Монитор / таблица / алерты по скринеру» | `CONTEXT_ROBOTS_ARCHITECTURE.md` + `CONTEXT_MONITORS.md` |
| «Правила написания кода / code style / работа с движком» | `CONTEXT_CODING_GUIDELINES.md` |
| «Как составить промпт на робота» | `CONTEXT_PROMPTS_ROBOTS.md` |
| «Как составить промпт на индикатор» | `CONTEXT_PROMPTS_INDICATORS.md` |

## Быстрые ссылки

- **[CONTEXT_ROBOTS_ARCHITECTURE.md](CONTEXT_ROBOTS_ARCHITECTURE.md)** — архитектура торговых роботов: BotPanel, табы, параметры, события, шаблоны и краткая сводка по индикаторам
- **[CONTEXT_ROBOTS.md](CONTEXT_ROBOTS.md)** — каталог готовых роботов (~100 примеров)
- **[CONTEXT_INDICATORS.md](CONTEXT_INDICATORS.md)** — написание и использование индикаторов
- **[CONTEXT_PAIRS_AND_FUTURES.md](CONTEXT_PAIRS_AND_FUTURES.md)** — парный арбитраж, фьючерсы, контанго
- **[CONTEXT_INDEX_AND_SPREAD.md](CONTEXT_INDEX_AND_SPREAD.md)** — индексы, спреды, коинтеграция
- **[CONTEXT_GRIDS.md](CONTEXT_GRIDS.md)** — сеточные стратегии
- **[CONTEXT_HIGH_FREQUENCY.md](CONTEXT_HIGH_FREQUENCY.md)** — HFT, стакан, тики
- **[CONTEXT_POSITIONS_AND_RISK.md](CONTEXT_POSITIONS_AND_RISK.md)** — позиции, стопы, тейки, трейлинг, риск
- **[CONTEXT_MONITORS.md](CONTEXT_MONITORS.md)** — монитор-роботы, таблицы, алерты
- **[CONTEXT_CODING_GUIDELINES.md](CONTEXT_CODING_GUIDELINES.md)** — правила написания кода в OsEngine: структура, naming, WPF, потокобезопасность, логирование, ошибки, комментарии
- **[CONTEXT_PROMPTS_ROBOTS.md](CONTEXT_PROMPTS_ROBOTS.md)** — шаблоны промптов на роботов
- **[CONTEXT_PROMPTS_INDICATORS.md](CONTEXT_PROMPTS_INDICATORS.md)** — шаблоны промптов на индикаторы
