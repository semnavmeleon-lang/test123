# Satoshi Coins

Отдельная, самостоятельная валюта сервера. Баланс — целое число "сатоши" (неделимая единица, по аналогии с настоящим биткоином). Курс BTC подтягивается с публичного API CoinGecko и используется не для изменения самого баланса, а для расчёта его реальной (долларовой/рублёвой) стоимости и для масштабирования наград/цен в других плагинах семейства (SisyphusBoulder, SatoshiShop, DiogenesBarrel).

## Команды

| Команда | Права | Действие |
|---|---|---|
| `/coins` | — | Показать свой баланс (+ примерная стоимость в $/₽, если курс уже получен) |
| `/coins balance <игрок>` | `satoshicoins.balance` | Показать баланс другого игрока |
| `/coins pay <игрок\|*> <сумма>` | `satoshicoins.pay` (или `payall` для `*`) | Перевести монеты игроку или всем онлайн |
| `/coins give <игрок\|*> <сумма>` | `satoshicoins.give` / `giveall` | Начислить монеты (админ) |
| `/coins take <игрок\|*> <сумма>` | `satoshicoins.take` / `takeall` | Списать монеты (админ) |
| `/coins set <игрок\|*> <сумма>` | `satoshicoins.set` / `setall` | Установить точный баланс (админ) |
| `/coins wipe confirm` | `satoshicoins.wipe` | Обнулить все балансы (нужно подтверждение вторым словом) |
| `/coins rate` | — | Показать текущий курс BTC (USD/RUB, изменение за 24ч, время обновления) |

`*` вместо имени игрока — применить действие ко всем игрокам, которые сейчас онлайн.

## Права

`satoshicoins.balance`, `satoshicoins.give`, `satoshicoins.giveall`, `satoshicoins.take`, `satoshicoins.takeall`, `satoshicoins.set`, `satoshicoins.setall`, `satoshicoins.pay`, `satoshicoins.payall`, `satoshicoins.wipe`.

## Конфигурация

| Параметр | По умолчанию | Описание |
|---|---|---|
| Price API URL | CoinGecko `simple/price?ids=bitcoin&vs_currencies=usd,rub&include_24hr_change=true` | Откуда брать курс BTC |
| Rate refresh interval (seconds) | 300 | Как часто обновлять курс |

## Публичный API для других плагинов

Через `[PluginReference] Plugin SatoshiCoins;` и `SatoshiCoins.Call(...)`:

| Метод | Возвращает | Описание |
|---|---|---|
| `Balance(ulong playerId)` | `long` | Текущий баланс |
| `Deposit(ulong playerId, long amount)` | `bool` | Начислить фиксированную сумму |
| `Withdraw(ulong playerId, long amount)` | `bool` | Списать (false, если недостаточно средств) |
| `SetBalance(ulong playerId, long amount)` | `bool` | Установить точный баланс |
| `Transfer(ulong fromId, ulong toId, long amount)` | `bool` | Перевод между игроками |
| `WipeAll()` | — | Обнулить все балансы |
| `DepositScaled(ulong playerId, long baseAmount)` | `long` | Начислить `baseAmount`, умноженный на текущее 24ч-изменение курса BTC; возвращает фактически начисленное |
| `PriceScaled(long baseAmount)` | `long` | Тот же расчёт, но без начисления — просто цена/сумма на сейчас |
| `BtcPriceUsd()` / `BtcPriceRub()` | `double` | Текущий курс |
| `ValueUsd(long satoshis)` / `ValueRub(long satoshis)` | `double` | Стоимость N сатоши в валюте |
| `BtcChange24h()` | `double` | Изменение курса за 24ч в % (например, -5.2) |

## Хуки для подписчиков

При изменениях вызываются (через `Interface.Oxide.CallHook`): `OnSatoshiCoinsDeposit`, `OnSatoshiCoinsWithdraw`, `OnSatoshiCoinsSet`, `OnSatoshiCoinsTransfer`, `OnSatoshiCoinsWipe`.

## Данные

Персистентный баланс на игрока (data-файл). Курс BTC в памяти не персистится — при рестарте подтягивается заново при первом запросе.

## Известные риски (не проверено на реальном сервере)

- Сигнатура `webrequest.Enqueue(url, body, callback, this, RequestMethod.GET, headers, timeout)` и пространство имён `RequestMethod` (`Oxide.Core.Libraries`) — самый рискованный момент, менялся между версиями Oxide/Carbon.
- CoinGecko — публичный, без ключа, но с лимитом запросов; при частых рестартах/перезагрузках плагина возможны временные отказы (обрабатываются мягко — используется последний известный курс).
