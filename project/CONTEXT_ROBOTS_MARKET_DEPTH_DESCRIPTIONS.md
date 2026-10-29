# CONTEXT_ROBOTS_MARKET_DEPTH_DESCRIPTIONS — Описания HFT-роботов на стаканных индикаторах

Каталог HFT-роботов на стаканных/лентовых индикаторах слоя `AMarketDepth`. По каждому роботу — суть, логика входа/выхода, параметры, риски.

Как создавать стаканные индикаторы — `CONTEXT_INDICATORS_MARKET_DEPTH.md`. Описания индикаторов — `CONTEXT_INDICATORS_MARKET_DEPTH_DESCRIPTIONS.md`.

## Оглавление

| № | Робот | Индикатор | Источник |
|---|---|---|---|
| 1 | HhfOrderBookImbalanceScreener | OrderBookImbalance | Screener |
| 2 | HhfDepthVolumeBot | DepthVolume | Simple |
| 3 | HhfSpreadScreener | Spread | Screener |
| 4 | HhfMicroPriceBot | MicroPrice | Simple |
| 5 | HhfLiquidityWallsBot | LiquidityWalls | Simple |
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
