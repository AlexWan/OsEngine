/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Language
{
    public class TraderLocal
    {
        public string Label1 => OsLocalization.ConvertToLocString(
            "Eng:Launch OsTraderMaster. Inclusion of the program_" +
            "Ru:Запуск OsTraderMaster. Включение программы_");

        public string Label2 => OsLocalization.ConvertToLocString(
            "Eng:Created a new bot _" +
            "Ru:Создан новый бот _");

        public string Label3 => OsLocalization.ConvertToLocString(
            "Eng:Risk manager warns of exceeding the daily loss limit!_" +
            "Ru:Риск менеджер предупреждает о превышении дневного лимита убытков!_");

        public string Label4 => OsLocalization.ConvertToLocString(
            "Eng:You want to remove the robot. Are you sure?_" +
            "Ru:Вы собираетесь удалить робота. Вы уверены?_");

        public string Label5 => OsLocalization.ConvertToLocString(
            "Eng:Robot deleted _" +
            "Ru:Бот удалён _");

        public string Label6 => OsLocalization.ConvertToLocString(
            "Eng:Unable to complete creation of the robot. The h character is reserved for the system._" +
            "Ru:Невозможно завершить создание робота. Символ h зарезервирован для ситсемы_");

        public string Label7 => OsLocalization.ConvertToLocString(
            "Eng:Unable to complete creation of the robot. The symbol l is reserved for the system._" +
            "Ru:Невозможно завершить создание робота. Символ h зарезервирован для ситсемы_");

        public string Label8 => OsLocalization.ConvertToLocString(
            "Eng:Unable to complete creation of the robot. A robot with the same name already exists._" +
            "Ru:Невозможно завершить создание робота. Робот с таким именем уже существует._");

        public string Label9 => OsLocalization.ConvertToLocString(
            "Eng:Created a new bot_" +
            "Ru:Создан новый бот _");

        public string Label10 => OsLocalization.ConvertToLocString(
            "Eng:The operation cannot be completed, because bot is not active_" +
            "Ru:Операция не может быть завершена, т.к. бот не активен_");

        public string Label11 => OsLocalization.ConvertToLocString(
            "Eng:This feature is only available from the trading tool tab._" +
            "Ru:Данная функция доступна только у вкладки с инструментом для торговли_");

        public string Label12 => OsLocalization.ConvertToLocString(
            "Eng:Risk Manager_" +
            "Ru:Риск Менеджер_");

        public string Label13 => OsLocalization.ConvertToLocString(
            "Eng:In one of the fields is invalid values. Save process aborted_" +
            "Ru:В одном из полей недопустимые значения. Процесс сохранения прерван_");

        public string Label14 => OsLocalization.ConvertToLocString(
            "Eng:Maximum loss per day in %_" +
            "Ru:Максимальный убыток за день в %_");

        public string Label15 => OsLocalization.ConvertToLocString(
            "Eng:Max Loss Reaction_" +
            "Ru:Реакция на максимальный убыток_");

        public string Label16 => OsLocalization.ConvertToLocString(
            "Eng:Is On_" +
            "Ru:Включить_");

        public string Label17 => OsLocalization.ConvertToLocString(
            "Eng:Accept_" +
            "Ru:Принять_");

        public string Label18 => OsLocalization.ConvertToLocString(
            "Eng:Open positions_" +
            "Ru:Позиции открытые_");

        public string Label19 => OsLocalization.ConvertToLocString(
            "Eng:Closed positions_" +
            "Ru:Позиции закрытые_");

        public string Label20 => OsLocalization.ConvertToLocString(
            "Eng:All positions_" +
            "Ru:Все позиции_");

        public string Label21 => OsLocalization.ConvertToLocString(
            "Eng:Portfolio_" +
            "Ru:Портфель_");

        public string Label22 => OsLocalization.ConvertToLocString(
            "Eng:Orders_" +
            "Ru:Ордера_");

        public string Label23 => OsLocalization.ConvertToLocString(
            "Eng:Bot log_" +
            "Ru:Бот лог_");

        public string Label24 => OsLocalization.ConvertToLocString(
            "Eng:Prime Log_" +
            "Ru:Прайм лог_");

        public string Label25 => OsLocalization.ConvertToLocString(
            "Eng:Market depth_" +
            "Ru:Стакан_");

        public string Label26 => OsLocalization.ConvertToLocString(
            "Eng:Alerts_" +
            "Ru:Алерты_");

        public string Label27 => OsLocalization.ConvertToLocString(
            "Eng:Control_" +
            "Ru:Управление_");

        public string Label28 => OsLocalization.ConvertToLocString(
            "Eng:   Buy \nat market_" +
            "Ru:  Купить \nпо рынку_");

        public string Label29 => OsLocalization.ConvertToLocString(
            "Eng:           Sell \n       at market_" +
            "Ru:           Продать\n         по рынку_");

        public string Label30 => OsLocalization.ConvertToLocString(
            "Eng:Volume_" +
            "Ru:Объём_");

        public string Label31 => OsLocalization.ConvertToLocString(
            "Eng:Price_" +
            "Ru:Цена_");

        public string Label32 => OsLocalization.ConvertToLocString(
            "Eng:  Buy \nat limit_" +
            "Ru:  Купить \n лимит_");

        public string Label33 => OsLocalization.ConvertToLocString(
            "Eng:           Sell \n       at limit_" +
            "Ru:           Продать\n          лимит_");

        public string Label34 => OsLocalization.ConvertToLocString(
            "Eng:Revoke limits_" +
            "Ru:Отозвать лимиты_");

        public string Label35 => OsLocalization.ConvertToLocString(
            "Eng:General settings_" +
            "Ru:Общие настройки_");

        public string Label36 => OsLocalization.ConvertToLocString(
            "Eng:Bot control_" +
            "Ru:Управление ботом_");

        public string Label37 => OsLocalization.ConvertToLocString(
            "Eng:Connection Servers_" +
            "Ru:Сервера подключения_");

        public string Label38 => OsLocalization.ConvertToLocString(
            "Eng:Add bot_" +
            "Ru:Добавить бота_");

        public string Label39 => OsLocalization.ConvertToLocString(
            "Eng:Delete_" +
            "Ru:Удалить_");

        public string Label40 => OsLocalization.ConvertToLocString(
            "Eng:Journal_" +
            "Ru:Журнал_");

        public string Label41 => OsLocalization.ConvertToLocString(
            "Eng:General risk manager_" +
            "Ru:Общий риск-менеджер_");

        public string Label42 => OsLocalization.ConvertToLocString(
            "Eng:Interface drawing_" +
            "Ru:Прорисовка интерфейса_");

        public string Label43 => OsLocalization.ConvertToLocString(
            "Eng:Bot trade settings_" +
            "Ru:Настройки торговли бота_");

        public string Label44 => OsLocalization.ConvertToLocString(
            "Eng:Data settings_" +
            "Ru:Настройки данных_");

        public string Label45 => OsLocalization.ConvertToLocString(
            "Eng:Parameters_" +
            "Ru:Параметры_");

        public string Label46 => OsLocalization.ConvertToLocString(
            "Eng:Risk manager_" +
            "Ru:Риск-менеджер_");

        public string Label47 => OsLocalization.ConvertToLocString(
            "Eng:Position support_" +
            "Ru:Сопровождение позиции_");

        public string Label48 => OsLocalization.ConvertToLocString(
            "Eng:You want to close the program. Are you sure?_" +
            "Ru:Вы собираетесь закрыть программу. Вы уверены?_");

        public string Label49 => OsLocalization.ConvertToLocString(
            "Eng: Volume wrong value_" +
            "Ru:В графе объём неправильное значение_");

        public string Label50 => OsLocalization.ConvertToLocString(
            "Eng:Price wrong value_" +
            "Ru:В графе цена неправильное значение_");

        public string Label51 => OsLocalization.ConvertToLocString(
            "Eng:This strategy has no parameters to configure._" +
            "Ru:У данной стратегии нет встроенных параметров для настройки_");

        public string Label52 => OsLocalization.ConvertToLocString(
            "Eng:A parameter with the same name already exists!_" +
            "Ru:Ахтунг! Параметр с таким именем уже существует!_");

        public string Label53 => OsLocalization.ConvertToLocString(
            "Eng:Risk manager warns of exceeding the daily loss limit!_" +
            "Ru:Риск менеджер предупреждает о превышении дневного лимита убытков!_");

        public string Label54 => OsLocalization.ConvertToLocString(
            "Eng:Risk manager warns of exceeding the daily loss limit! Trading stopped! Robot-_" +
            "Ru:Риск менеджер предупреждает о превышении дневного лимита убытков! Дальнейшие торги остановлены! Робот- _");

        public string Label55 => OsLocalization.ConvertToLocString(
            "Eng:This is a trend robot based on the strategy of Bill Williams._" +
            "Ru:Это трендовый робот оснванный на стратегии Билла Вильямса_");

        public string Label56 => OsLocalization.ConvertToLocString(
            "Eng:The trend strategy described in the book of Edwin Lafevre: Reminiscences of a Stock Operator Quotes_" +
            "Ru:Трендовая стратегия описанная в книге Эдвина Лафевра: Воспоминания биржевого спекулянта_");

        public string Label57 => OsLocalization.ConvertToLocString(
            "Eng:This strategy has no settings_" +
            "Ru:У данной стратегии нет настроек. Это ж привод и сам он ничего не делает_");

        public string Label58 => OsLocalization.ConvertToLocString(
            "Eng:Wrong name. It is not possible to continue the process of creating a bot._" +
            "Ru:Не верное имя. Не возможно продолжить процесс создания бота._");

        public string Label59 => OsLocalization.ConvertToLocString(
            "Eng:Robot create_" +
            "Ru:Создание робота_");

        public string Label60 => OsLocalization.ConvertToLocString(
            "Eng:Strategy type_" +
            "Ru:Тип стратегии_");

        public string Label61 => OsLocalization.ConvertToLocString(
            "Eng:Name_" +
            "Ru:Имя_");

        public string Label62 => OsLocalization.ConvertToLocString(
            "Eng:It is not possible to open a deal! Connector is not active!_" +
            "Ru:Не возможно открыть сделку! Коннектор не активен!_");

        public string Label63 => OsLocalization.ConvertToLocString(
            "Eng:It is not possible to open a deal! The volume can not be equal to zero!_" +
            "Ru:Не возможно открыть сделку! Объём не может быть равен нулю!_");

        public string Label64 => OsLocalization.ConvertToLocString(
            "Eng:It is not possible to open a deal! No portfolio or security!_" +
            "Ru:Не возможно открыть сделку! Нет портфеля или бумаги!_");

        public string Label65 => OsLocalization.ConvertToLocString(
            "Eng: attempt to add a short order to the long. Blocked_" +
            "Ru: попытка добавить в шорт ордер лонг. Блокировано_");

        public string Label66 => OsLocalization.ConvertToLocString(
            "Eng: attempt to add a long order to the short. Blocked_" +
            "Ru: попытка добавить в лонг ордер шорт. Блокировано_");

        public string Label67 => OsLocalization.ConvertToLocString(
            "Eng: Alert cover all positions. Price _" +
            "Ru: Алерт кроем все позиции. Цена _");

        public string Label68 => OsLocalization.ConvertToLocString(
            "Eng: Alert Signal Short. Price _" +
            "Ru: Алерт Сигнал Шорт. Цена _");

        public string Label69 => OsLocalization.ConvertToLocString(
            "Eng: Alert Signal Long. Price _" +
            "Ru: Алерт Сигнал Лонг. Цена _");

        public string Label70 => OsLocalization.ConvertToLocString(
            "Eng: Removed the order by time, number_" +
            "Ru: Отозвали ордер по времени, номер _");

        public string Label71 => OsLocalization.ConvertToLocString(
            "Eng: Closing Position. Number _" +
            "Ru: Закрытие сделки номер _");

        public string Label72 => OsLocalization.ConvertToLocString(
            "Eng: Position did not open. Number _" +
            "Ru: Сделка не открылась номер _");

        public string Label73 => OsLocalization.ConvertToLocString(
            "Eng: Opening transaction number _" +
            "Ru: Открытие сделки номер _");

        public string Label74 => OsLocalization.ConvertToLocString(
            "Eng: Position did not close. Number_" +
            "Ru: Сделка не закрылась номер _");

        public string Label75 => OsLocalization.ConvertToLocString(
            "Eng: The user ordered the closure of all positions_" +
            "Ru: Пользователь заказал закрытие всех сделок _");

        public string Label76 => OsLocalization.ConvertToLocString(
            "Eng: Could not format string because it contains prohibited characters_" +
            "Ru:Не удалось форматировать строку, т.к. в ней запрещённые символы_");

        public string Label77 => OsLocalization.ConvertToLocString(
            "Eng:Cluster Graphics Setup_" +
            "Ru:Настройка кластерного графика_");

        public string Label78 => OsLocalization.ConvertToLocString(
            "Eng:Setting the source candles_" +
            "Ru:Настройка исходных свечей_");

        public string Label79 => OsLocalization.ConvertToLocString(
            "Eng:Show_" +
            "Ru:Отображаем_");

        public string Label80 => OsLocalization.ConvertToLocString(
            "Eng:Lines step_" +
            "Ru:Шаг линии_");

        public string Label81 => OsLocalization.ConvertToLocString(
            "Eng:Connecting data flow to calculate the index_" +
            "Ru:Подключение потока данных для расчета индекса_");

        public string Label82 => OsLocalization.ConvertToLocString(
            "Eng:Index number_" +
            "Ru:Номер индекса_");

        public string Label83 => OsLocalization.ConvertToLocString(
            "Eng:Security code_" +
            "Ru:Код бумаги_");

        public string Label84 => OsLocalization.ConvertToLocString(
            "Eng:Empty_" +
            "Ru:Пусто_");

        public string Label85 => OsLocalization.ConvertToLocString(
            "Eng:Position Tracking Settings_" +
            "Ru:Настройки сопровождения позиций_");

        public string Label86 => OsLocalization.ConvertToLocString(
            "Eng:Stop_" +
            "Ru:Стоп_");

        public string Label87 => OsLocalization.ConvertToLocString(
            "Eng:Profit_" +
            "Ru:Профит_");

        public string Label88 => OsLocalization.ConvertToLocString(
            "Eng:Position closing_" +
            "Ru:Закрытие позиции_");

        public string Label89 => OsLocalization.ConvertToLocString(
            "Eng:Position opening_" +
            "Ru:Открытие позиции_");

        public string Label90 => OsLocalization.ConvertToLocString(
            "Eng:Close order reject_" +
            "Ru:Ордер на закрытие отозван_");

        public string Label91 => OsLocalization.ConvertToLocString(
            "Eng:On/Off_" +
            "Ru:Включить_");

        public string Label92 => OsLocalization.ConvertToLocString(
            "Eng:Slippage_" +
            "Ru:Проскальзывание_");

        public string Label93 => OsLocalization.ConvertToLocString(
            "Eng:From entry to stop_" +
            "Ru:От входа до Стопа_");

        public string Label94 => OsLocalization.ConvertToLocString(
            "Eng:From entry to profit_" +
            "Ru:От входа до Профита_");

        public string Label95 => OsLocalization.ConvertToLocString(
            "Eng:Seconds to close_" +
            "Ru:Секунд на закрытие_");

        public string Label96 => OsLocalization.ConvertToLocString(
            "Eng:Max price rollback_" +
            "Ru:Макс откат цены_");

        public string Label97 => OsLocalization.ConvertToLocString(
            "Eng:Seconds to open_" +
            "Ru:Секунд на открытие_");

        public string Label98 => OsLocalization.ConvertToLocString(
            "Eng:Seconds to open_" +
            "Ru:Секунд на открытие_");

        public string Label99 => OsLocalization.ConvertToLocString(
            "Eng:Reaction_" +
            "Ru:Реакция_");

        public string Label100 => OsLocalization.ConvertToLocString(
            "Eng:Close position_" +
            "Ru:Закрытие позиции_");

        public string Label101 => OsLocalization.ConvertToLocString(
            "Eng:Position number_" +
            "Ru:Номер сделки_");

        public string Label102 => OsLocalization.ConvertToLocString(
            "Eng:Security_" +
            "Ru:Бумага_");

        public string Label103 => OsLocalization.ConvertToLocString(
            "Eng:Order type_" +
            "Ru:Тип ордера_");

        public string Label104 => OsLocalization.ConvertToLocString(
            "Eng:Orders to iceberg_" +
            "Ru:Ордеров в айсберг_");

        public string Label105 => OsLocalization.ConvertToLocString(
            "Eng:Position modification_" +
            "Ru:Модификация позиции_");

        public string Label106 => OsLocalization.ConvertToLocString(
            "Eng:Side_" +
            "Ru:Направление_");

        public string Label107 => OsLocalization.ConvertToLocString(
            "Eng:Stop for position_" +
            "Ru:Стоп для позиции_");

        public string Label108 => OsLocalization.ConvertToLocString(
            "Eng:Activation price_" +
            "Ru:Цена активации_");

        public string Label109 => OsLocalization.ConvertToLocString(
            "Eng:Order price_" +
            "Ru:Цена ордера_");

        public string Label110 => OsLocalization.ConvertToLocString(
            "Eng:Order price_" +
            "Ru:Профит для позиции_");

        public string Label111 => OsLocalization.ConvertToLocString(
            "Eng:This strategy has settings in the form of parameters. We buy if we are under the largest volume for sale in the last N clusters. We sell if we are above the largest volume to buy in the last N clusters_" +
            "Ru:У данной стратегии настройки в виде параметров. Покупаем если находимся под самым большим объёмом на продажу за последние N кластеров. Продаём если находимся над самым большим объёмом на покупку за последние N кластеров_");

        public string Label112 => OsLocalization.ConvertToLocString(
            "Eng:This strategy has no settings. This is a chart where you can watch horizontal volumes._" +
            "Ru:У данной стратегии нет настроек. Это чарт на котором можно смотреть горизонтальные объёмы._");

        public string Label113 => OsLocalization.ConvertToLocString(
            "Eng:On Friday, all positions are closed. Every Monday, 20 levels are built up and 20 levels down. Through the distance specified in the settings. At the intersection of these levels in the opposite direction position is opened_" +
            "Ru:В пятницу все позиции закрываются. Каждый понедельник строются 20 уровней вверх и 20 уровней вниз. Через расстояние указываемое в настройках. При пересечении этих уровней в противоположную сторону открывается позиция_");

        public string Label114 => OsLocalization.ConvertToLocString(
            "Eng:Pattern trading setting_" +
            "Ru:Настройка торговли паттернами_");

        public string Label115 => OsLocalization.ConvertToLocString(
            "Eng:Regime_" +
            "Ru:Режим_");

        public string Label116 => OsLocalization.ConvertToLocString(
            "Eng:Set_" +
            "Ru:Сет_");

        public string Label117 => OsLocalization.ConvertToLocString(
            "Eng:Pattern group_" +
            "Ru:Группа паттернов_");

        public string Label118 => OsLocalization.ConvertToLocString(
            "Eng:Maximum positions_" +
            "Ru:Максимум позиций_");

        public string Label119 => OsLocalization.ConvertToLocString(
            "Eng:Opening position_" +
            "Ru:Открытие позиции_");

        public string Label120 => OsLocalization.ConvertToLocString(
            "Eng:Patterns_" +
            "Ru:Паттерны_");

        public string Label121 => OsLocalization.ConvertToLocString(
            "Eng:Side_" +
            "Ru:Сторона_");

        public string Label122 => OsLocalization.ConvertToLocString(
            "Eng:Entry Weight_" +
            "Ru:Вес для входа_");

        public string Label123 => OsLocalization.ConvertToLocString(
            "Eng:Stop order %_" +
            "Ru:Стоп-ордер %_");

        public string Label124 => OsLocalization.ConvertToLocString(
            "Eng:Profit order %_" +
            "Ru:Профит-ордер %_");

        public string Label125 => OsLocalization.ConvertToLocString(
            "Eng:Through N candles_" +
            "Ru:Через N свечей_");

        public string Label126 => OsLocalization.ConvertToLocString(
            "Eng:Trailing Stop %_" +
            "Ru:Трейлинг-Стоп %_");

        public string Label127 => OsLocalization.ConvertToLocString(
            "Eng:Slippage for exit by patterns_" +
            "Ru:Проскальзывание для выхода по  паттернам_");

        public string Label128 => OsLocalization.ConvertToLocString(
            "Eng:Weight for exit_" +
            "Ru:Вес для выхода_");

        public string Label129 => OsLocalization.ConvertToLocString(
            "Eng:Strategy on the Bollinger indicator. Sell when the line closure occurs above the line of the Bollinger and buy when the closure is below the line of the Bollinger. Closing occurs at the intersection of the moving average.._" +
            "Ru:Стратегия на индикаторе Боллинджер. Продаём когда линия закрытие происходит выше линии боллинджера и покупаем когда закрытие ниже линии боллинжера. Закрытие происходит по пересечению скользящей средней_");

        public string Label130 => OsLocalization.ConvertToLocString(
            "Eng:% between the lines_" +
            "Ru:% между линиями_");

        public string Label131 => OsLocalization.ConvertToLocString(
            "Eng:Is painting_" +
            "Ru:Прорисовывать_");

        public string Label132 => OsLocalization.ConvertToLocString(
            "Eng:Indent from level 0_" +
            "Ru:Отступ от 0-го уровня_");

        public string Label133 => OsLocalization.ConvertToLocString(
            "Eng:Redraw_" +
            "Ru:Перерисовать_");

        public string Label134 => OsLocalization.ConvertToLocString(
            "Eng:CCI upper limit_" +
            "Ru:CCI верхний предел_");

        public string Label135 => OsLocalization.ConvertToLocString(
            "Eng:CCI lower limit_" +
            "Ru:CCI нижний предел_");

        public string Label136 => OsLocalization.ConvertToLocString(
            "Eng:Step_" +
            "Ru:Шаг_");

        public string Label137 => OsLocalization.ConvertToLocString(
            "Eng:Trailing Stop_" +
            "Ru:Трейлинг стоп_");
          
        public string Label138 => OsLocalization.ConvertToLocString(
            "Eng:RSI difference_" + 
            "Ru:Разница RSI_");

        public string Label139 => OsLocalization.ConvertToLocString(
            "Eng:ATR period_" +
            "Ru:Период ATR_");

        public string Label140 => OsLocalization.ConvertToLocString(
            "Eng:ATR coefficient_" +
            "Ru:Коэффициент для ATR_");

        public string Label141 => OsLocalization.ConvertToLocString(
            "Eng:RSI overbought_" +
            "Ru:Перекупленность по RSI_");

        public string Label142 => OsLocalization.ConvertToLocString(
            "Eng:RSI Oversold_" +
            "Ru:Перепроданность по RSI_");

        public string Label143 => OsLocalization.ConvertToLocString(
            "Eng:Candles count_" +
            "Ru:Количество свечей_");

        public string Label144 => OsLocalization.ConvertToLocString(
            "Eng:Spread Expansion_" +
            "Ru:Расширение спреда_");

        public string Label145 => OsLocalization.ConvertToLocString(
            "Eng:Profit in % of spread_" +
            "Ru:Профит в % от спреда_");

        public string Label146 => OsLocalization.ConvertToLocString(
            "Eng:Loss % Spread_" +
            "Ru:Лосс в % от спреда_");

        public string Label147 => OsLocalization.ConvertToLocString(
            "Eng:When the spread widens by a certain amount for a certain number of candles, we buy the spread. We exit the position when the spread narrows or increases by a certain percentage. We exit the position when the spread narrows or increases by a certain percentage._" +
            "Ru:Когда за определённое кол-во свечек спред расширяется на определённую величину, то мы покупаем спред. Из позиции выходим когда спред сужается или увеличивается на определённый процент. Из позиции выходим когда спред сужается или увеличивается на определённый процент_");

        public string Label148 => OsLocalization.ConvertToLocString(
            "Eng:The robot looks at the spread chart between the instruments. It has a short and long mash. When the short crosses the long, it serves as a signal to enter the position. Closing, too, by breakdown_" +
            "Ru:Робот смотрит на график спреда между инструментами. На нём есть короткая и длинная машка. Когда короткая пересекает длинную это служит сигналом для входа в позицию. Закрытие тоже по пробою_");

        public string Label149 => OsLocalization.ConvertToLocString(
            "Eng:Stochastic upper limit_" +
            "Ru:Stochastic верхний предел_");

        public string Label150 => OsLocalization.ConvertToLocString(
            "Eng:Stochastic lower limit_" +
            "Ru:Stochastic нижний предел_");

        public string Label151 => OsLocalization.ConvertToLocString(
            "Eng:Step value_" +
            "Ru:Значение шага_");

        public string Label152 => OsLocalization.ConvertToLocString(
            "Eng:Pattern height_" +
            "Ru:Высота паттерна_");

        public string Label153 => OsLocalization.ConvertToLocString(
            "Eng:Min candle body height_" +
            "Ru:Мин. высота тела свечи_");

        public string Label154 => OsLocalization.ConvertToLocString(
            "Eng:Step from level 0_" +
            "Ru:Шаг от 0-го уровня_");

        public string Label155 => OsLocalization.ConvertToLocString(
            "Eng:WilliamsR upper limit_" +
            "Ru:WilliamsR верхний предел_");

        public string Label156 => OsLocalization.ConvertToLocString(
            "Eng:WilliamsR lower limit_" +
            "Ru:WilliamsR нижний предел_");

        public string Label157 => OsLocalization.ConvertToLocString(
            "Eng: Withdrew the order on the rollback of the price in the marketDepth from the order price_" +
            "Ru: Отозвали ордер по откату цены в стакане от цены ордера, номер _");

        public string Label158 => OsLocalization.ConvertToLocString(
            "Eng: Values type_" +
            "Ru: Тип переменных _"); 
        
        public string Label159 => OsLocalization.ConvertToLocString(
            "Eng: Update Bot_" +
            "Ru: Обновить Бота _");
        
        public string Label160 => OsLocalization.ConvertToLocString(
            "Eng: Hot update changes from source code_" +
            "Ru: Загрзука изменений исходного кода_");
        
        public string Label161 => OsLocalization.ConvertToLocString(
            "Eng: Start updating changes from the source code_" +
            "Ru: Начинается загрузка изменений исходного кода_");
        
        public string Label162 => OsLocalization.ConvertToLocString(
            "Eng: Bot successfully updated_" +
            "Ru: Робот удачно обновлен_");
        
        public string Label163 => OsLocalization.ConvertToLocString(
            "Eng: Failed to update current bot_" +
            "Ru: Не удалось обновить робота_");
    }
}
