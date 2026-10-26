# CONTEXT_INDICATORS_MARKET_DEPTH_DESCRIPTIONS — Описания индикаторов стакана и ленты сделок

Каталог всех индикаторов слоя `AMarketDepth`. По каждому — что считает, точная формула, параметры, серии, как читать значения, ограничения и источники.

Как создавать такие индикаторы — в `CONTEXT_INDICATORS_MARKET_DEPTH.md`.

## Оглавление

| № | Индикатор | Вход |
|---|---|---|
| 1 | OrderBookImbalance (Дисбаланс стакана) | стакан |
| 2 | DepthVolume (Суммарная глубина) | стакан |
| 3 | Spread (Спред) | стакан |
| 4 | MicroPrice (Микро-цена) | стакан |
| 5 | LiquidityWalls (Стены ликвидности) | стакан |
| 6 | Ofi (Поток заявок) | стакан |
| 7 | ExpectedSlippage (Ожидаемое проскальзывание) | стакан |
| 8 | Spoofing (Спуфинг) | стакан |
| 9 | Delta (Дельта / CVD) | лента |
| 10 | SpeedOfTape (Скорость ленты) | лента |
| 11 | Rvol (Относительный объём) | лента |
| 12 | TradeSizeAnalytics (Аналитика размера сделок) | лента |

Структура карточки у всех одинаковая:

- Суть — что показывает индикатор и зачем.
- Формула — точный расчёт, как в коде.
- Параметры — таблица имён, типов, значений по умолчанию.
- Серии — выходные ряды данных.
- Как читать — интерпретация значений.
- Источники — ссылки на первоисточники и документацию платформ.

---

## 1. OrderBookImbalance — Дисбаланс стакана

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/OrderBookImbalance.cs`
**Вход:** стакан (`MarketDepth`)

### Суть

Дисбаланс стакана (Order Book Imbalance, OBI) показывает перевес заявок на покупку или на продажу на ближайших уровнях книги. Отвечает на вопрос: «на чьей стороне сейчас больше пассивной ликвидности».

Это один из базовых показателей микроструктуры рынка. Его используют, чтобы оценить краткосрочное давление: если на бидах объёма заметно больше, чем на асках, ожидают давление вверх, и наоборот.

### Формула

Простой режим (`Mode = Simple`):

```
OBI = (ΣBid − ΣAsk) / (ΣBid + ΣAsk)
```

где `ΣBid` и `ΣAsk` — суммарные объёмы первых `Levels` уровней бидов и асков соответственно.

Взвешенный режим (`Mode = Weighted`) затухает по экспоненте — вес уровня `i` равен `1 / 2^i` (1, 1/2, 1/4, ...):

```
ΣBid = Bid[0] + Bid[1]/2 + Bid[2]/4 + ...
ΣAsk = Ask[0] + Ask[1]/2 + Ask[2]/4 + ...
OBI = (ΣBid − ΣAsk) / (ΣBid + ΣAsk)
```

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Mode` | строка | `Simple` | `Simple` — простой, `Weighted` — с весом по близости уровня |
| `Levels` | int | `5` | Сколько ближайших уровней суммировать |

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `Imbalance` | Line | Значение дисбаланса от −1 до +1 |

### Как читать

- `+1` — все заявки на покупку (сильный перевес покупателей).
- `−1` — все заявки на продажу (сильный перевес продавцов).
- `0` — равенство сторон.
- Положительное значение — давление вверх, отрицательное — вниз.
- Чем дальше от нуля, тем выраженнее перекос. Взвешенный режим сильнее реагирует на изменения именно у лучших цен.

### Источники

- ATAS — Imbalance Ratio: https://help.atas.net/en/support/solutions/articles/72000602404-imbalance-ratio
- Bookmap — Orderbook Imbalance: https://bookmap.com/knowledgebase/docs/Addons-Orderbook-Imbalance
- Bookmap — Imbalance Indicators: https://bookmap.com/knowledgebase/docs/KB-Indicators-Imbalance
- thinkorswim (Schwab) — Imbalance Indicators: https://toslc.thinkorswim.com/center/howToTos/thinkManual/Bookmap/Imbalance-Indicators

---

## 2. DepthVolume — Суммарная глубина

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/DepthVolume.cs`
**Вход:** стакан (`MarketDepth`)

### Суть

Суммарный объём заявок на покупку и на продажу на ближайших уровнях книги. Показывает, сколько пассивной ликвидности стоит с каждой стороны — «толщину» бидов и асков.

### Формула

```
BidDepth = Σ Bid[i]   по i от 0 до Levels−1
AskDepth = Σ Ask[i]   по i от 0 до Levels−1
```

где `Bid[i]` — объём i-го уровня бидов, `Ask[i]` — объём i-го уровня асков.

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Levels` | int | `10` | Сколько ближайших уровней суммировать |

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `BidDepth` | Line | Суммарный объём бидов на `Levels` уровнях |
| `AskDepth` | Line | Суммарный объём асков на `Levels` уровнях |

### Как читать

- Большие значения — плотная ликвидность (стена заявок), цену сложнее сдвинуть.
- Маленькие значения — тонкий рынок, цену легко сдвинуть небольшим объёмом.
- Сравнение `BidDepth` и `AskDepth` показывает, с какой стороны больше ликвидности: перекос в сторону бидов — поддержка снизу, в сторону асков — сопротивление сверху.

### Источники

- Quantower — Depth of Bid/Ask: https://help.quantower.com/quantower/analytics-panels/chart/technical-indicators/volume/depth-of-bid-ask
- Quantower Knowledge Base (GitHub) — depth-of-bid-ask.md: https://github.com/Quantower/QuantowerKB/blob/master/analytics-panels/chart/technical-indicators/volume/depth-of-bid-ask.md

---

## 3. Spread — Спред

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/Spread.cs`
**Вход:** стакан (`MarketDepth`)

### Суть

Спред — расстояние между лучшей ценой покупки (Bid) и лучшей ценой продажи (Ask). Это базовая мера издержек входа/выхода и ликвидности инструмента.

### Формула

Абсолютный режим (`Mode = Absolute`):

```
Spread = bestAsk − bestBid
```

Процентный режим (`Mode = Percent`), относительный спред к лучшему биду:

```
Spread = (bestAsk − bestBid) / bestBid × 100
```

Дополнительно считаются:

```
SpreadMa  = среднее значение Spread за последние Period секунд
SpreadVol = стандартное отклонение Spread за последние Period секунд
```

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Mode` | строка | `Absolute` | `Absolute` — в цене, `Percent` — в процентах от Bid |
| `Period` | int | `10` | Окно для среднего и волатильности (секунд) |

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `Spread` | Line | Текущий спред |
| `SpreadMa` | Line | Средний спред за `Period` секунд |
| `SpreadVol` | Line | Волатильность спреда за `Period` секунд |

### Как читать

- Узкий спред — дешёвый вход и ликвидный рынок.
- Широкий спред — дорогая торговля, часто низкая ликвидность или высокая волатильность.
- Рост `SpreadMa` говорит об ухудшении условий исполнения, рост `SpreadVol` — о нестабильности ликвидности.

### Источники

- ATAS — Bid, ask and spread in trading: https://atas.net/blog/bid-ask-and-spread-in-trading/
- Investopedia — Spread Indicator: https://www.investopedia.com/terms/s/spread-indicator.asp
- WallStreetMojo — Bid-Ask Spread (формула): https://www.wallstreetmojo.com/bid-ask-spread/

---

## 4. MicroPrice — Микро-цена

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/MicroPrice.cs`
**Вход:** стакан (`MarketDepth`)

### Суть

Микро-цена — взвешенная по объёму оценка справедливой цены на лучших уровнях книги. Это более точная альтернатива середине спреда (мид-прайсу): она смещается в сторону той стороны книги, где больше объёма.

### Формула

Взвешенная мид-цена на лучших уровнях (weighted mid-price):

```
MicroPrice = (P_bid × V_ask + P_ask × V_bid) / (V_bid + V_ask)
```

где:

- `P_bid` — цена лучшего бида, `P_ask` — цена лучшего аска;
- `V_bid` — объём на лучшем биде, `V_ask` — объём на лучшем аске.

### Параметры

Нет параметров — считается по лучшему уровню стакана.

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `MicroPrice` | Line | Взвешенная оценка справедливой цены |

### Как читать

- Цена выше микро-цены — перевес продавцов, ниже — перевес покупателей.
- Микро-цена почти всегда лежит между лучшим бидом и лучшим аском.
- Используется как «справедливая цена» для маркет-мейкинга и оценки направления краткосрочного движения.

### Источники

- Stoikov — The Micro-Price: A High-Frequency Estimator of Future Prices (SSRN): https://papers.ssrn.com/sol3/papers.cfm?abstract_id=2970694
- Sasha Stoikov — The Microprice (проект): http://www.sashastoikov.com/the-microprice/
- Quant Stack Exchange — How to understand micro-price (weighted mid-price): https://quant.stackexchange.com/questions/50651/how-to-understand-micro-price-aka-weighted-mid-price

---

## 5. LiquidityWalls — Стены ликвидности

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/LiquidityWalls.cs`
**Вход:** стакан (`MarketDepth`)

### Суть

Стены ликвидности — самые крупные пассивные заявки в книге. Это уровни, где стоит заметно больше объёма, чем на соседних. Крупная заявка на биде работает как поддержка, на аске — как сопротивление, пока её не исполнят или не снимут.

### Формула

На первых `Levels` уровнях каждой стороны ищется уровень с максимальным объёмом:

```
BidWall      = max(Bid[i])        по i от 0 до Levels−1
BidWallPrice = цена уровня, где достигнут BidWall
AskWall      = max(Ask[i])        по i от 0 до Levels−1
AskWallPrice = цена уровня, где достигнут AskWall
```

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Levels` | int | `20` | Сколько ближайших уровней сканировать |

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `BidWall` | Column | Объём крупнейшего бида |
| `BidWallPrice` | Line | Цена крупнейшего бида |
| `AskWall` | Column | Объём крупнейшего аска |
| `AskWallPrice` | Line | Цена крупнейшего аска |

### Как читать

- Рост `BidWall` — крупная покупка под ценой, потенциальная поддержка.
- Рост `AskWall` — крупная продажа над ценой, потенциальное сопротивление.
- Сдвиг `BidWallPrice`/`AskWallPrice` вверх или вниз показывает, куда переставляют крупные заявки.

### Источники

- Finder-Arbitrage — Order-book walls: how big limit orders move price: https://finder-arbitrage.com/blog/order-book-walls
- Bookmap — Liquidity Marker (крупная ликвидность и её изменения): https://bookmap.com/knowledgebase/docs/Addons-Liquidity-Marker

---

## 6. Ofi — Поток заявок (Order Flow Imbalance)

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/Ofi.cs`
**Вход:** стакан (`MarketDepth`)

### Суть

Дисбаланс потока заявок (Order Flow Imbalance, OFI) — изменение давления покупателей и продавцов на лучших уровнях между двумя обновлениями книги. Положительный OFI означает, что ликвидность на бидах растёт быстрее, чем на асках (давление вверх), отрицательный — наоборот.

### Формула

Канонический OFI (Cont, Kukanov, Stoikov, 2014) по лучшим биду и аску между двумя снимками:

```
e_bid = I(P_bid_now ≥ P_bid_prev) · Q_bid_now − I(P_bid_now ≤ P_bid_prev) · Q_bid_prev
e_ask = −I(P_ask_now ≤ P_ask_prev) · Q_ask_now + I(P_ask_now ≥ P_ask_prev) · Q_ask_prev
OFI   = e_bid + e_ask
```

где `P_bid`/`Q_bid` — цена и объём лучшего бида, `P_ask`/`Q_ask` — цена и объём лучшего аска, `I(условие)` = 1 при истине и 0 иначе.

Смысл: если лучший бид поднялся — в поток входит новый размер бида; если опустился — из потока уходит старый размер; при неизменной цене учитывается изменение размера. Для аска — зеркально.

### Параметры

Нет параметров — считается по лучшему уровню стакана.

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `Ofi` | Line | Изменение давления покупки/продажи |

### Как читать

- Положительное значение — приток ликвидности на биды / отток с асков, давление вверх.
- Отрицательное — давление вниз.
- Ноль — баланс между изменениями сторон.
- Устойчиво положительный/отрицательный OFI на горизонте обычно предшествует движению цены.

### Источники

- Cont, Kukanov, Stoikov — The Price Impact of Order Book Events (arXiv): https://arxiv.org/abs/1011.6402
- Cont, Kukanov, Stoikov — The Price Impact of Order Book Events (Journal of Financial Econometrics): https://academic.oup.com/jfec/article-abstract/12/1/47/816163

---

## 7. ExpectedSlippage — Ожидаемое проскальзывание

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/ExpectedSlippage.cs`
**Вход:** стакан (`MarketDepth`)

### Суть

Ожидаемое проскальзывание — оценка влияния на рынок для заданного размера заявки. Показывает, насколько средняя цена исполнения уйдёт от лучшей цены, если исполнить `Volume` из текущего стакана («пройти по книге»).

### Формула

Покупка: идём по аскам сверху вниз, пока не наберём `Volume`:

```
averagePrice = Σ(askPrice_i × fill_i) / filled,   fill_i = min(remaining, askVolume_i)
SlippageBuy  = (averagePrice − bestAsk) / bestAsk × 100
```

Продажа: идём по бидам, зеркально:

```
SlippageSell = (bestBid − averagePrice) / bestBid × 100
```

Если ликвидности меньше `Volume`, расчёт идёт по фактически исполненной части.

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Volume` | decimal | `100` | Целевой объём заявки |

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `SlippageBuy` | Line | Проскальзывание покупки, % |
| `SlippageSell` | Line | Проскальзывание продажи, % |

### Как читать

- Большое значение — тонкий стакан, крупную заявку дорого исполнить.
- Маленькое — плотная ликвидность, заявка исполнится почти по лучшей цене.
- Сравнение `SlippageBuy` и `SlippageSell` показывает, с какой стороны исполнение дороже.

### Источники

- Aligrithm — Market Impact and the Square-Root Law (walking the book): https://aligrithm.com/market-impact-and-the-square-root-law-walking-the-book-to-price-your-slippage/
- TensorParadigm — Crypto Slippage & Order-Book Impact Calculator: https://tensorparadigm.com/tools/slippage-calculator

---

## 8. Spoofing — Спуфинг

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/Spoofing.cs`
**Вход:** стакан (`MarketDepth`)

### Суть

Спуфинг — размещение крупных ненастоящих заявок, которые создают ложное впечатление спроса/предложения и снимаются до исполнения. Индикатор ловит ключевой признак: крупный уровень появляется и быстро исчезает.

### Формула

Для каждого из первых `Levels` уровней сравнивается объём с предыдущим снимком:

```
SpoofScore = 1, если объём уровня был ≥ Threshold на прошлом снимке и стал < Threshold на текущем;
              иначе 0
```

Проверяются обе стороны — биды и аски.

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Threshold` | decimal | `10000` | Минимальный объём, который считаем «крупным» |
| `Levels` | int | `1` | Сколько ближайших уровней отслеживать |

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `SpoofScore` | Line | 1 — крупный уровень исчез, 0 — нет |

### Как читать

- `1` — на ближайших уровнях быстро сняли крупную заявку, возможный спуфинг или отмена крупного игрока.
- `0` — крупные уровни ведут себя обычно.
- Частые всплески `SpoofScore` на одной стороне — сигнал, что книгу «рисуют».

### Источники

- SpoofScan — Spoofing (large orders appear and vanish quickly): https://spoofscan.com/en/resources/spoofing-en.html
- Trader Profesional — Spoofing and Market Manipulation: https://traderprofesional.com/en/spoofing-market-manipulation/
- QuantStrategy — Detecting Spoofing and Iceberg Orders: https://quantstrategy.io/blog/detecting-spoofing-and-iceberg-orders-advanced-techniques/

---

## 9. Delta — Дельта / CVD

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/Delta.cs`
**Вход:** лента сделок (`Trade`)

### Суть

Дельта — разница между агрессивными покупками и агрессивными продажами по сделкам. CVD (Cumulative Volume Delta) — накопленная дельта с начала торговой сессии. Показывает, кто «толкает» рынок — покупатели или продавцы.

### Формула

За текущую секунду:

```
Delta = Σ(объём покупок) − Σ(объём продаж)    при Aggregation = Volume
Delta = Σ(1 покупка) − Σ(1 продажа)           при Aggregation = Trades
```

Накопление (сбрасывается при смене торгового дня):

```
Cvd = Σ Delta с начала дня
```

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Aggregation` | строка | `Volume` | `Volume` — по объёму сделок, `Trades` — по числу сделок |

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `Delta` | Line | Покупки минус продажи за текущую секунду |
| `Cvd` | Line | Накопленная дельта с начала дня |

### Как читать

- `Delta > 0` — покупатели агрессивнее, `Delta < 0` — продавцы.
- `Cvd` растёт — доминируют покупки, падает — продажи.
- Дивергенция цены и `Cvd` (цена растёт, а CVD падает) — возможная слабость движения.

### Источники

- Bookmap — Cumulative Volume Delta: https://bookmap.com/knowledgebase/docs/KB-Indicators-CVD
- ATAS — CVD (Cumulative Volume Delta): https://help.atas.net/en/support/solutions/articles/72000602360-cvd-cumulative-volume-delta
- ATAS — CVD Pro (Cumulative Volume Delta): https://atas.net/blog/cvd-pro/

---

## 10. SpeedOfTape — Скорость ленты

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/SpeedOfTape.cs`
**Вход:** лента сделок (`Trade`)

### Суть

Скорость ленты — сколько сделок проходит в секунду. Показывает интенсивность рыночной активности: всплески скорости говорят о вспышке интереса, спад — о затишье.

### Формула

```
Speed = число сделок за текущую секунду
```

### Параметры

Нет параметров.

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `Speed` | Column | Число сделок в секунду |

### Как читать

- Всплеск `Speed` — вспышка активности (новость, крупный игрок, пробой уровня).
- Спад — затишье.
- Высокая скорость вместе с движением цены подтверждает движение; высокая скорость без движения — возможное поглощение объёма.

### Источники

- ATAS — Speed Of Tape: https://help.atas.net/en/support/solutions/articles/72000602472-speed-of-tape
- ATAS — Alternative analysis of the Smart Tape with Speed of Tape: https://atas.net/blog/alternative-analysis-of-the-smart-tape-with-the-speed-of-tape-indicator-in-atas/
- DeepCharts — Speed of Tape: https://www.deepcharts.com/helpcenter/article/speed-of-tape

---

## 11. Rvol — Относительный объём

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/Rvol.cs`
**Вход:** лента сделок (`Trade`)

### Суть

Относительный объём (RVOL) — насколько текущий объём сделок выше или ниже обычного. Это вариант классического RVOL, применённый к ленте: сравниваем объём текущей секунды со средним за предыдущие секунды.

### Формула

```
Rvol = объём текущей секунды / средний объём предыдущих Period секунд
```

Если среднего ещё нет (мало истории), значение 0.

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Period` | int | `10` | Сколько предыдущих секунд брать за базу |

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `Rvol` | Line | Относительный объём (отношение) |

### Как читать

- `Rvol = 1` — обычная активность.
- `Rvol > 1` — активность выше нормы (всплеск).
- `Rvol < 1` — затишье.
- Резкий рост `Rvol` часто сопровождает пробои и важные события.

### Источники

- TradingSim — Relative Volume (RVOL): https://www.tradingsim.com/blog/relative-volume-rvol
- StockCharts — Relative Volume (RVOL): https://chartschool.stockcharts.com/table-of-contents/technical-indicators-and-overlays/technical-indicators/relative-volume-rvol

---

## 12. TradeSizeAnalytics — Аналитика размера сделок

**Файл:** `OsEngine/IndicatorMarketDepth/Scripts/TradeSizeAnalytics.cs`
**Вход:** лента сделок (`Trade`)

### Суть

Аналитика размера сделок: средний размер сделки и доля крупных сделок. Помогает увидеть активность крупных игроков в ленте.

### Формула

За окно из последних `Period` секунд:

```
AvgTradeSize    = Σ(объём сделок) / Σ(число сделок)
LargeTradeShare = Σ(объём крупных сделок) / Σ(объём всех сделок)
```

Крупная сделка — с объёмом не меньше `LargeThreshold`.

### Параметры

| Имя | Тип | По умолчанию | Смысл |
|---|---|---|---|
| `Period` | int | `10` | Окно в секундах |
| `LargeThreshold` | decimal | `1000` | Порог «крупной» сделки по объёму |

### Серии

| Имя | Тип отрисовки | Смысл |
|---|---|---|
| `AvgTradeSize` | Line | Средний размер сделки |
| `LargeTradeShare` | Line | Доля объёма от крупных сделок (0..1) |

### Как читать

- Рост `AvgTradeSize` — в ленту выходят крупные игроки.
- Высокий `LargeTradeShare` — активность крупных участников.
- Рост обеих метрик вместе с ценой подтверждает движение.

### Источники

- ATAS — Big Trades: https://help.atas.net/en/support/solutions/articles/72000602332-big-trades
- ATAS — 4 useful tricks of the Big Trades Indicator: https://atas.net/blog/4-useful-tricks-of-the-big-trades-indicator/

---
