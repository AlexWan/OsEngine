# CONTEXT_ROBOTS_MARKET_DEPTH_DESCRIPTIONS — Описания HFT-роботов на стаканных индикаторах

Каталог HFT-роботов на стаканных/лентовых индикаторах слоя `AMarketDepth`. По каждому роботу — суть, логика входа/выхода, параметры, риски.

Как создавать стаканные индикаторы — `CONTEXT_INDICATORS_MARKET_DEPTH.md`. Описания индикаторов — `CONTEXT_INDICATORS_MARKET_DEPTH_DESCRIPTIONS.md`.

## Оглавление

| № | Робот | Индикатор | Источник |
|---|---|---|---|
| 1 | HhfOrderBookImbalanceScreener | OrderBookImbalance | Screener |
| 2 | HhfDepthVolumeBot | DepthVolume | Simple |
| 3 | HhfSpreadScreener | Spread | Screener (пропущен) |
| 4 | HhfMicroPriceBot | MicroPrice | Simple (пропущен) |
| 5 | HhfLiquidityWallsBot | LiquidityWalls + Keltner | Screener |
| 6 | HhfOfiBot | Ofi | Simple |
| 7 | HhfExpectedSlippageBot | ExpectedSlippage | Simple |
| 8 | HhfSpoofingBot | Spoofing | Simple |
| 9 | HhfDeltaBot | Delta | Simple |
| 10 | HhfSpeedOfTapeBot | SpeedOfTape | Simple |
| 11 | HhfRvolScreener | Rvol | Screener |
| 12 | HhfTradeSizeAnalyticsBot | TradeSizeAnalytics | Simple |
| 13 | HhfIcebergBot | Iceberg | Simple |
| 14 | HhfResilienceBot | Resilience | Simple |

---

## 1. HhfOrderBookImbalanceScreener — OrderBookImbalance + Боллинджер на скринере

**Файл:** `OsEngine/Robots/HhfIndicators/HhfOrderBookImbalanceScreener.cs`
**Источник:** Screener, TF минута (1 мин)

### Индикаторы

**OrderBookImbalance** (стакан) — дисбаланс стакана: перевес пассивной ликвидности между бидами и асками на ближайших уровнях книги. Значение `Imbalance` лежит в диапазоне от −1 до +1: положительное — давление вверх, отрицательное — вниз.

**Bollinger Bands** (свечной, на минутках) — полоса вокруг скользящей средней на расстоянии нескольких стандартных отклонений. Серии: `Up line` (верхняя), `Down line` (нижняя). Используется как фильтр входа.

### Суть

Робот сканирует несколько бумаг на скринере. Входит в лонг, когда стакан показывает сильный перевес покупателей (дисбаланс выше `LongThreshold`) и цена опустилась ниже нижней линии Боллинджера. Входит в шорт, когда перевес продавцов (дисбаланс ниже `ShortThreshold`) и цена поднялась выше верхней линии Боллинджера. Выходит по возврату цены к центральной линии Боллинджера.

### Логика (на каждый таб скринера)

**Подписка на события источника:** `tab.MarketDepthUpdateEvent` (обновление стакана) на каждом табе скринера.

1. В обработчике `MarketDepthUpdateEvent` читаем текущее `Imbalance`, цену закрытия последней свечи и линии Боллинджера.
2. **Вход (по маркету):** только если общее число открытых позиций на всех бумагах < `MaxPositions`. Сигнал входа должен удерживаться `ConfirmationTime` секунд подряд.
   - `Imbalance ≥ LongThreshold` **и** цена < нижней линии Боллинджера **≥ ConfirmationTime с**, нет позиции → лонг: `tab.BuyAtMarket(GetVolume(tab))`.
   - `Imbalance ≤ ShortThreshold` **и** цена > верхней линии Боллинджера **≥ ConfirmationTime с**, нет позиции → шорт: `tab.SellAtMarket(GetVolume(tab))`.
3. **Выход (по центральной линии Боллинджера):**
   - Лонг: цена ≥ центральной линии → `tab.CloseAllAtMarket()`.
   - Шорт: цена ≤ центральной линии → `tab.CloseAllAtMarket()`.

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Regime` | строка | Off | Режим: `Off` / `On` / `OnlyLong` / `OnlyShort` |
| `LongThreshold` | decimal | 0.6 | Порог входа в лонг (дисбаланс) |
| `ShortThreshold` | decimal | −0.6 | Порог входа в шорт (дисбаланс) |
| `ConfirmationTime` | int | 2 | Секунд подряд, которые сигнал должен удерживаться |
| `VolumeType` | строка | Deposit percent | Тип объёма: `Contracts` / `Contract currency` / `Deposit percent` |
| `Volume` | decimal | 10 | Объём заявки (для `Deposit percent` — % от депозита) |
| `MaxPositions` | int | 2 | Максимум одновременно открытых позиций (на все бумаги скринера) |
| `Levels` | int | 5 | Глубина индикатора OrderBookImbalance |
| `Mode` | строка | Simple | Режим OrderBookImbalance: `Simple` / `Weighted` |
| `BollingerLength` | int | 21 | Период Боллинджера |
| `BollingerDeviation` | decimal | 2 | Отклонение Боллинджера |

### Риски и ограничения

- Без стоп-лосса: позиция закрывается только по возврату цены к центральной линии Боллинджера. В MVP стопа нет.
- На скринере торгует каждую бумагу независимо; общий лимит позиций не задан.
- Фильтр Боллинджера считается по закрытию минутных свечей; внутри минуты значения линии «замирают» до закрытия следующей свечи.

---

## 2. HhfDepthVolumeBot — Стена ликвидности по DepthVolume

**Файл:** `OsEngine/Robots/HhfIndicators/HhfDepthVolumeBot.cs`
**Источник:** Simple (одна бумага)

### Индикаторы

**DepthVolume** (стакан) — суммарный объём заявок на покупку и на продажу на ближайших уровнях книги. Серии `BidDepth` и `AskDepth` — «толщина» бидов и асков на `Levels` уровнях.

**SMA** (свечной, на минутках, длина `SmaLength`) — скользящая средняя, задаёт направление тренда. Выше SMA разрешены только лонги, ниже — только шорты.

### Суть

Робот ловит аномально плотную стену ликвидности и торгует против неё: если аски резко выросли (стена продавцов сверху) — цена упрётся и отскочит вниз, входим в шорт; если биды выросли (стена покупателей снизу) — входим в лонг. Стена считается в % от недавней базы (EMA глубины), поэтому портабельна по бумагам и периодам.

### Логика (на таб Simple)

**Подписка на события источника:** `tab.MarketDepthUpdateEvent` (обновление стакана).

1. В обработчике `MarketDepthUpdateEvent` читаем `BidDepth`/`AskDepth` индикатора и обновляем EMA базы (`avgAsk`, `avgBid`).
2. **Вход (по маркету, с фильтром тренда):**
   - `AskDepth ≥ avgAsk × (1 + WallPercent/100)` **и** цена < SMA **удерживается ≥ ConfirmationTime с**, нет позиции → шорт: `tab.SellAtMarket(GetVolume(tab))`.
   - `BidDepth ≥ avgBid × (1 + WallPercent/100)` **и** цена > SMA **удерживается ≥ ConfirmationTime с**, нет позиции → лонг: `tab.BuyAtMarket(GetVolume(tab))`.
3. **Выход (стоп/профит, по цене):**
   - Лонг: `tab.CloseAllAtMarket()` при цене ≥ `EntryPrice × (1 + TakeProfit/100)` или ≤ `EntryPrice × (1 − StopLoss/100)`.
   - Шорт: `tab.CloseAllAtMarket()` при цене ≤ `EntryPrice × (1 − TakeProfit/100)` или ≥ `EntryPrice × (1 + StopLoss/100)`.

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Regime` | строка | Off | Режим: `Off` / `On` / `OnlyLong` / `OnlyShort` |
| `WallPercent` | decimal | 40 | На сколько % глубина выше базы — стена |
| `AveragingPeriod` | int | 90 | Период EMA базы (сек) |
| `ConfirmationTime` | int | 3 | Секунд подтверждения сигнала |
| `TakeProfit` | decimal | 0.5 | Тейк-профит, % от цены входа |
| `StopLoss` | decimal | 0.15 | Стоп-лосс, % от цены входа |
| `Levels` | int | 10 | Глубина индикатора DepthVolume |
| `SmaLength` | int | 20 | Длина скользящей средней (тренд-фильтр) |
| `VolumeType` | строка | Deposit percent | Тип объёма |
| `Volume` | decimal | 10 | Объём (% от депозита) |

### Риски и ограничения

- EMA базы считается по стаканным обновлениям (~1/сек); период `AveragingPeriod` приблизителен.
- Выход только по стопу/профиту — без возврата по стене.

---

## 5. HhfLiquidityWallsBot — Пробой плиты по LiquidityWalls + Келтнер

**Файл:** `OsEngine/Robots/HhfIndicators/HhfLiquidityWallsBot.cs`
**Источник:** Screener (несколько бумаг)

### Индикаторы

**LiquidityWalls** (стакан) — крупнейший уровень и его цена на каждой стороне книги: `BidWall`/`BidWallPrice` (крупнейший бид и его цена), `AskWall`/`AskWallPrice` (крупнейший аск и его цена).

**KeltnerChannel** (свечной, на минутках) — канал вокруг EMA цены на расстоянии, кратном ATR. Серии `Up Channel` (верхняя) и `Down Channel` (нижняя) задают направление тренда.

### Суть

Робот ловит пробой «плиты» — крупной стены на **краю** стакана, которая держалась определённое время и вдруг исчезла. Исчезла аск-плита (сопротивление убрали) и цена выше верхней линии Келтнера — лонг; исчезла бид-плита (поддержку убрали) и цена ниже нижней линии Келтнера — шорт.

### Логика (на каждый таб скринера)

**Подписка на события источника:** `tab.MarketDepthUpdateEvent` (обновление стакана) на каждом табе скринера.

1. Ведём EMA-базу по `AskWall` и `BidWall`. «Плита» = объём ≥ базы × (1 + WallPercent/100) **и** плита на краю:
   - аск-плита: `AskWallPrice == md.Asks[0].Price` (крупнейший аск — на лучшем аске);
   - бид-плита: `BidWallPrice == md.Bids[0].Price` (крупнейший бид — на лучшем биде).
2. Плита должна удержаться ≥ `WallPersistenceTime` секунд.
3. Когда плита исчезает (объём упал к базе):
   - аск-плита исчезла **и** цена > верхней линии Келтнера → лонг: `tab.BuyAtMarket(GetVolume(tab))`.
   - бид-плита исчезла **и** цена < нижней линии Келтнера → шорт: `tab.SellAtMarket(GetVolume(tab))`.
4. Выход (стоп/профит): реальные ордера `CloseAtStop`/`CloseAtProfit`.
5. Общее число открытых позиций на всех бумагах < `MaxPositions`.

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Regime` | строка | Off | Режим: `Off` / `On` / `OnlyLong` / `OnlyShort` |
| `Levels` | int | 20 | Глубина индикатора LiquidityWalls |
| `WallPercent` | decimal | 50 | На сколько % объём выше EMA базы — плита |
| `AveragingPeriod` | int | 60 | Период EMA базы (сек) |
| `WallPersistenceTime` | int | 10 | Секунд удержания плиты |
| `KeltnerEmaPeriod` | int | 20 | Период EMA канала Келтнера |
| `KeltnerMultiplier` | decimal | 2 | Множитель ATR канала Келтнера |
| `TakeProfit` | decimal | 0.2 | Тейк-профит, % от цены входа |
| `StopLoss` | decimal | 0.2 | Стоп-лосс, % от цены входа |
| `VolumeType` | строка | Deposit percent | Тип объёма |
| `Volume` | decimal | 10 | Объём (% от депозита) |
| `MaxPositions` | int | 3 | Максимум одновременно открытых позиций (на все бумаги скринера) |

### Риски и ограничения

- Плита определяется относительно EMA-базы по стаканным обновлениям (~1/сек); период `WallPersistenceTime` приблизителен.
- Выход только по стопу/профиту.
- Канал Келтнера считается по минутным свечам; внутри минуты значения линии «замирают» до закрытия свечи.
