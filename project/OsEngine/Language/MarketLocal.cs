/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Language
{
    public class MarketLocal
    {
        public string Message1 => OsLocalization.ConvertToLocString(
            "Eng:No connection to the exchange was found!_" +
            "Ru:Ни одного соединения с биржей не найдено!_");

        public string Message2 => OsLocalization.ConvertToLocString(
            "Eng:Intercepted attempt to place an order when the connection is off!_" +
            "Ru:Перехвачена попытка выставить ордер при выключенном соединении!_");

        public string Message3 => OsLocalization.ConvertToLocString(
            "Eng:Create server _" +
            "Ru:Создан сервер _");

        public string Message4 => OsLocalization.ConvertToLocString(
            "Eng:Revoke all active orders_" +
            "Ru:Отозвать все активные заявки_");

        public string Message5 => OsLocalization.ConvertToLocString(
            "Eng:Revoke current order_" +
            "Ru:Отозвать текущую_");

        public string TitleConnectorCandle => OsLocalization.ConvertToLocString(
            "Eng:Data flow connection_" +
            "Ru:Подключение потока данных_");

        public string ButtonAccept => OsLocalization.ConvertToLocString(
            "Eng:Accept_" +
            "Ru:Принять_");

        public string Label1 => OsLocalization.ConvertToLocString(
            "Eng:Server_" +
            "Ru:Торговый сервер_");

        public string Label2 => OsLocalization.ConvertToLocString(
            "Eng:Order execution_" +
            "Ru:Исполнение ордеров_");

        public string Label3 => OsLocalization.ConvertToLocString(
            "Eng:Portfolio for operations_" +
            "Ru:Портфель для операций_");

        public string Label4 => OsLocalization.ConvertToLocString(
            "Eng:Execute trades in the emulator_" +
            "Ru:Исполнять сделки в эмуляторе_");

        public string Label5 => OsLocalization.ConvertToLocString(
            "Eng:Candles settings_" +
            "Ru:Настройки свечей_");

        public string Label6 => OsLocalization.ConvertToLocString(
            "Eng:Classes displayed_" +
            "Ru:Отображаемые классы_");

        public string Label7 => OsLocalization.ConvertToLocString(
            "Eng:Security_" +
            "Ru:Инструмент_"); 

        public string Label8 => OsLocalization.ConvertToLocString(
            "Eng:From what we collect candles_" +
            "Ru:Из чего собираем свечи_");

        public string Label9 => OsLocalization.ConvertToLocString(
            "Eng:Candles type_" +
            "Ru:Тип свечей_");

        public string Label10 => OsLocalization.ConvertToLocString(
            "Eng:TimeFrame_" +
            "Ru:ТаймФрейм_");

        public string Label11 => OsLocalization.ConvertToLocString(
            "Eng:Trades count in candle_" +
            "Ru:Трейдов в свече_");

        public string Label12 => OsLocalization.ConvertToLocString(
            "Eng:Build non-trading candles_" +
            "Ru:Строить неторговые свечи_");

        public string Label13 => OsLocalization.ConvertToLocString(
            "Eng:Change delta to close_" +
            "Ru:Изменение дельты для закрытия_");

        public string Label14 => OsLocalization.ConvertToLocString(
            "Eng:Volume to close_" +
            "Ru:Объём для закрытия_");

        public string Label15 => OsLocalization.ConvertToLocString(
            "Eng:Move to close_" +
            "Ru:Движение для закрытия_");

        public string Label16 => OsLocalization.ConvertToLocString(
            "Eng:Build shadows_" +
            "Ru:Строить тени_");

        public string Label17 => OsLocalization.ConvertToLocString(
            "Eng:Size_" +
            "Ru:Размер_");

        public string Label18 => OsLocalization.ConvertToLocString(
            "Eng:Min movement_" +
            "Ru:Минимальное движение_");

        public string Label19 => OsLocalization.ConvertToLocString(
            "Eng:Rollback_" +
            "Ru:Откат_");

        public string Label20 => OsLocalization.ConvertToLocString(
            "Eng:Deploy servers automatically_" +
            "Ru:Разворачивать сервера автоматически_");

        public string LabelComissionType => OsLocalization.ConvertToLocString(
            "Eng:Comission type_" +
            "Ru:Тип комиссии_");

        public string LabelComissionValue => OsLocalization.ConvertToLocString(
            "Eng:Comission value_" +
            "Ru:Значение комиссии_");

        public string TitleServerMasterUi => OsLocalization.ConvertToLocString(
            "Eng:Router_" +
            "Ru:Роутер_");

        public string TabItem1 => OsLocalization.ConvertToLocString(
            "Eng: Servers _" +
            "Ru: Подключения _");

        public string TabItem2 => OsLocalization.ConvertToLocString(
            "Eng: Logging_" +
            "Ru: Логирование _");


        // Servers

        public string Message6 => OsLocalization.ConvertToLocString(
            "Eng:Connection established_" +
            "Ru:Соединение установлено_");

        public string Message7 => OsLocalization.ConvertToLocString(
            "Eng:Connection status changed_" +
            "Ru:Изменилось состояние соединения_");

        public string Message8 => OsLocalization.ConvertToLocString(
            "Eng:Connection activation procedure started_" +
            "Ru:Запущена процедура активации подключения_");

        public string Message9 => OsLocalization.ConvertToLocString(
            "Eng:Disconnection procedure started_" +
            "Ru:Запущена процедура отключения подключения_");

        public string Message10 => OsLocalization.ConvertToLocString(
            "Eng:Create a candle manager_" +
            "Ru:Создаём менеджер свечей_");

        public string Message11 => OsLocalization.ConvertToLocString(
            "Eng:CRITICAL ERROR. Reconnect_" +
            "Ru:КРИТИЧЕСКАЯ ОШИБКА. Реконнект_");

        public string Message12 => OsLocalization.ConvertToLocString(
            "Eng:Connection lost_" +
            "Ru:Соединение разорвано_");

        public string Message13 => OsLocalization.ConvertToLocString(
            "Eng:In incoming papers, missing NameId_" +
            "Ru:Во входящих бумагах, отсутствуют NameId_");

        public string Message14 => OsLocalization.ConvertToLocString(
            "Eng:Security _" +
            "Ru:Инструмент _");

        public string Message15 => OsLocalization.ConvertToLocString(
            "Eng:TimeFrame _" +
            "Ru:Таймфрейм _");

        public string Message16 => OsLocalization.ConvertToLocString(
            "Eng: successfully connected to receive data and listen to candles_" +
            "Ru: успешно подключен на получение данных и прослушивание свечек_");

        public string Message17 => OsLocalization.ConvertToLocString(
            "Eng:Order _" +
            "Ru:Ордер № _");

        public string Message18 => OsLocalization.ConvertToLocString(
            "Eng: can not be set because less than one minute has passed since the server was turned on_" +
            "Ru: не может быть выставлен, т.к. с времени предыдущего включения сервера прошло менее одной минуты_");

        public string Message19 => OsLocalization.ConvertToLocString(
            "Eng:Send Order. Price - _" +
            "Ru:Выставлен ордер, цена - _");

        public string Message20 => OsLocalization.ConvertToLocString(
            "Eng: Side - _" +
            "Ru: Сторона - _");

        public string Message21 => OsLocalization.ConvertToLocString(
            "Eng: Volume - _" +
            "Ru: Объём - _");

        public string Message22 => OsLocalization.ConvertToLocString(
            "Eng: Security - _" +
            "Ru: Инструмент - _");

        public string Message23 => OsLocalization.ConvertToLocString(
            "Eng: Number - _" +
            "Ru: Номер - _");

        public string Message24 => OsLocalization.ConvertToLocString(
            "Eng: Cancel order - _" +
            "Ru: Отзываем ордер - _");

        public string TitleAServerParametrUi => OsLocalization.ConvertToLocString(
            "Eng:Connection settings _" +
            "Ru:Настройка подключения _");

        public string GridColumn1 => OsLocalization.ConvertToLocString(
            "Eng:Parameter name_" +
            "Ru:Название параметра_");

        public string GridColumn2 => OsLocalization.ConvertToLocString(
            "Eng:Value_" +
            "Ru:Значение_");

        public string TabItem3 => OsLocalization.ConvertToLocString(
            "Eng: Settings _" +
            "Ru: Настройки _");

        public string TabItem4 => OsLocalization.ConvertToLocString(
            "Eng: Logging _" +
            "Ru: Логирование _");

        public string Label21 => OsLocalization.ConvertToLocString(
            "Eng: Connection state _" +
            "Ru: Статус сервера _");

        public string ButtonConnect => OsLocalization.ConvertToLocString(
            "Eng:Connect_" +
            "Ru:Подключить_");

        public string ButtonDisconnect => OsLocalization.ConvertToLocString(
            "Eng:Disconnect_" +
            "Ru:Отключить_");

        // оптимизатор / тестер

        public string Message25 => OsLocalization.ConvertToLocString(
            "Eng:Not a single set was found in the Data folder. Test server will not work_" +
            "Ru:В папке Data не обнаружено ни одного сета. Тестовый сервер не будет работать _");

        public string Message26 => OsLocalization.ConvertToLocString(
            "Eng:Set was found - _" +
            "Ru:Найден сет- _");

        public string Message27 => OsLocalization.ConvertToLocString(
            "Eng:Load new Data Set - _" +
            "Ru:Подключаем новый сет данных- _");

        public string Message28 => OsLocalization.ConvertToLocString(
            "Eng:Loading of security is interrupted. There are no loaded data in the set._" +
            "Ru:Загрузка бумаг прервана. В указанном сете нет загруженных инструментов._");

        public string Message29 => OsLocalization.ConvertToLocString(
            "Eng:No relevant data was found in the repository. Security-_" +
            "Ru:В хранилище не найдены соответствующие данные. Бумага- _");

        public string Message30 => OsLocalization.ConvertToLocString(
            "Eng:Type -_" +
            "Ru:Тип данных - _");

        public string Message31 => OsLocalization.ConvertToLocString(
            "Eng:Error in data format inside data file_" +
            "Ru:Ошибка в формате данных внутри файла с данными _");

        public string Message32 => OsLocalization.ConvertToLocString(
            "Eng:For the period not a single candle was found. Beginning of period-_" +
            "Ru:За период не найдено ни одной свечи. Начало периода- _");

        public string Message33 => OsLocalization.ConvertToLocString(
            "Eng:End of period-_" +
            "Ru:Конец периода- _");

        public string Message34 => OsLocalization.ConvertToLocString(
            "Eng:For the period not a single trade was found. Beginning of period-_" +
            "Ru:За период не найдено ни однго трейда. Начало периода- _");

        public string Message35 => OsLocalization.ConvertToLocString(
            "Eng:The testing process is start_" +
            "Ru:Включен процесс тестирования с самого начала _");

        public string Message36 => OsLocalization.ConvertToLocString(
            "Eng:No data storage was found._" +
            "Ru:Хранилище с такими данными не обнаружено _");

        public string Message37 => OsLocalization.ConvertToLocString(
            "Eng:Testing completed because the timer has come to an end_" +
            "Ru:Тестирование завершилось т.к. время таймера подошло к концу _");

        public string Message38 => OsLocalization.ConvertToLocString(
            "Eng:Testing completed because no robot is subscribed to server data_" +
            "Ru:Тестирование завершилось т.к. на инструменты сервера не подписан ни один бот_");

        public string Message39 => OsLocalization.ConvertToLocString(
            "Eng:Error in placing orders. An order with this number is already in the system._" +
            "Ru:Ошибка в выставлении ордера. Ордер с таким номером уже есть в системе_");

        public string Message40 => OsLocalization.ConvertToLocString(
            "Eng:Error in placing orders. Server is not active_" +
            "Ru:Ошибка в выставлении ордера. Сервер не активен_");

        public string Message41 => OsLocalization.ConvertToLocString(
            "Eng:Error in placing orders. Bid price is out of range. order.Price -_" +
            "Ru:Ошибка в выставлении ордера. Цена заявки находиться за пределами диапазона. order.Price -_");

        public string Message42 => OsLocalization.ConvertToLocString(
            "Eng:Error in placing orders. Wrong volume. order.Volume -_" +
            "Ru:Ошибка в выставлении ордера. Неправильный объём. order.Volume - _");

        public string Message43 => OsLocalization.ConvertToLocString(
            "Eng:Error in placing orders. Portfolio number not specified_" +
            "Ru:Ошибка в выставлении ордера. Не указан номер портфеля_");

        public string Message44 => OsLocalization.ConvertToLocString(
            "Eng:Error in placing orders. Security name not specified_" +
            "Ru:Ошибка в выставлении ордера. Не указан инструмент_");

        public string Message45 => OsLocalization.ConvertToLocString(
            "Eng:Error in order removal. Server is not active_" +
            "Ru:Ошибка в снятии ордера. Сервер не активен_");

        public string Message46 => OsLocalization.ConvertToLocString(
            "Eng:Error in order removal. There is no such order in the system._" +
            "Ru:Ошибка в снятии ордера. Такого ордера нет в системе_");

        public string Message47 => OsLocalization.ConvertToLocString(
            "Eng:The testing process is interrupted, because no initial test date has been determined_" +
            "Ru:Процесс тестирования прерван, т.к. не определена начальная дата тестирования_");

        public string Message48 => OsLocalization.ConvertToLocString(
            "Eng:Testing stopped. No data is connected_" +
            "Ru:Тестирование остановлено. Не подключены данные_");

        public string Message49 => OsLocalization.ConvertToLocString(
            "Eng:Loading of securities is interrupted. The specified folder does not contain any files._" +
            "Ru:Загрузка бумаг прервана. В указанной папке не содержиться ни одного файла_");

        public string Message50 => OsLocalization.ConvertToLocString(
            "Eng:Load securities_" +
            "Ru:Скачиваем бумаги_");

        public string Message51 => OsLocalization.ConvertToLocString(
            "Eng:Error accessing server. Invalid address or no internet connection._" +
            "Ru:Ошибка доступа к серверу. Не верный адрес или отсутствует интернет соединение_");

        public string Message52 => OsLocalization.ConvertToLocString(
            "Eng:Available securities_" +
            "Ru:Доступно бумаг - _");

        public string Message53 => OsLocalization.ConvertToLocString(
            "Eng:Created portfolio for trading in the emulator _" +
            "Ru:Создан портфель для торговли в эмуляторе  _");

        public string Message54 => OsLocalization.ConvertToLocString(
            "Eng:Start downloading data. Tf _" +
            "Ru:Старт скачивания данных. ТФ  _");

        public string Message55 => OsLocalization.ConvertToLocString(
            "Eng:Finished downloading data. Tf _" +
            "Ru:Закончили скачивание данных. ТФ  _");

        public string Message56 => OsLocalization.ConvertToLocString(
            "Eng:Update trades data for security _" +
            "Ru:Обновляем данные по трейдам для бумаги  _");

        public string Message57 => OsLocalization.ConvertToLocString(
            "Eng: for _" +
            "Ru: за _");

        public string Message58 => OsLocalization.ConvertToLocString(
            "Eng:Update data on candles for security _" +
            "Ru:Обновляем данные по свечам для бумаги _");

        public string Message59 => OsLocalization.ConvertToLocString(
            "Eng:Adress_" +
            "Ru:Адрес_");

        public string Message60 => OsLocalization.ConvertToLocString(
            "Eng:ServerName_" +
            "Ru:Имя сервера_");

        public string Message61 => OsLocalization.ConvertToLocString(
            "Eng:Service name_" +
            "Ru:Имя сервиса_");

        public string Message62 => OsLocalization.ConvertToLocString(
            "Eng:Place_" +
            "Ru:Расположение_");

        public string Message63 => OsLocalization.ConvertToLocString(
            "Eng:User name_" +
            "Ru:Имя пользователя_");

        public string Message64 => OsLocalization.ConvertToLocString(
            "Eng:Password_" +
            "Ru:Пароль_");

        public string Message65 => OsLocalization.ConvertToLocString(
            "Eng:No key specified. Connection terminated_" +
            "Ru:Не указан ключ.  Подключение прервано_");

        public string Message66 => OsLocalization.ConvertToLocString(
            "Eng:Error subscribing to account info_" +
            "Ru:Ошибка подписки на аккаунт инфо_");

        public string Message67 => OsLocalization.ConvertToLocString(
            "Eng:Signed up to receive candles_" +
            "Ru:Подписались на получение свечей_");

        public string Message68 => OsLocalization.ConvertToLocString(
            "Eng:Error subscription candles_" +
            "Ru:Ошибка подписки на получение свечей_");

        public string Message69 => OsLocalization.ConvertToLocString(
            "Eng:Successful portfolio request_" +
            "Ru:Успешный запрос портфелей_");

        public string Message70 => OsLocalization.ConvertToLocString(
            "Eng:Portfolios request error_" +
            "Ru:Ошибка запроса портфелей_");

        public string Message71 => OsLocalization.ConvertToLocString(
            "Eng:Securities request error_" +
            "Ru:Ошибка запроса инструментов_");

        public string Message72 => OsLocalization.ConvertToLocString(
            "Eng:The tool request was successful_" +
            "Ru:Запрос инструментов прошел успешно_");

        public string Message73 => OsLocalization.ConvertToLocString(
            "Eng:Subscribe to the marketdepth_" +
            "Ru:Подписались на стакан_");

        public string Message74 => OsLocalization.ConvertToLocString(
            "Eng:MarketDepth subscription error_" +
            "Ru:Ошибка подписки на стакан_");

        public string Message75 => OsLocalization.ConvertToLocString(
            "Eng:Error canceling order. Reason_" +
            "Ru:Ошибка отмены ордера. Причина _");

        public string Message76 => OsLocalization.ConvertToLocString(
            "Eng:Ninja not responding. Probably you have not active script OsEngineConnect in Ninja_" +
            "Ru:Ninja не отвечает. Вероятно у Вас не активен скрипт OsEngineConnect в Ninja _");

        public string Message77 => OsLocalization.ConvertToLocString(
            "Eng:The token cannot be empty! Get a unique access key at_" +
            "Ru:Токен не может быть пустым! Получите уникальный ключ доступа по адресу _");

        public string Message78 => OsLocalization.ConvertToLocString(
            "Eng:Attention! The main stream does not respond for more than a minute!_" +
            "Ru:ВНИМАНИЕ! Основной поток не отвечает больше минуты!_");

        public string Message79 => OsLocalization.ConvertToLocString(
            "Eng:Trades loading started. If this happens for the first time after downloading the program, it may take a few minutes._" +
            "Ru:Включена загрузка тиков. Если это произошло впервые после загрузки программы, это может занять несколько минут._");

        public string Message80 => OsLocalization.ConvertToLocString(
            "Eng:Trades is fully loaded. The server is Activated for further work_" +
            "Ru:Тики полностью загрузились. Сервер Активирован для дальнейшей работы_");

        public string Message81 => OsLocalization.ConvertToLocString(
            "Eng:The excess of the FLOOD control! PENALTIES! Too many requests!_" +
            "Ru:Превышение ФЛУД контроля! ШТРАФЫ! Слишком много заявок!_");

        public string Message82 => OsLocalization.ConvertToLocString(
            "Eng:Path to Quik_" +
            "Ru:Путь к Квик_");

        public string Message83 => OsLocalization.ConvertToLocString(
            "Eng:Error. You must specify the location of the Quik_" +
            "Ru:Ошибка. Необходимо указать местоположение Quik_");

        public string Message84 => OsLocalization.ConvertToLocString(
            "Eng:Error. Trans2Quik does not want to connect_" +
            "Ru:Ошибка. Trans2Quik не хочет подключаться _");

        public string Message85 => OsLocalization.ConvertToLocString(
            "Eng:Transe2Quik status change_" +
            "Ru:Transe2Quik изменение статуса _");

        public string Message86 => OsLocalization.ConvertToLocString(
            "Eng:Broker connection status change_" +
            "Ru:Сервер Брокера изменил статус _");

        public string Message87 => OsLocalization.ConvertToLocString(
            "Eng:DDE Server status change_" +
            "Ru:DDE Server изменение статуса_");

        public string Message88 => OsLocalization.ConvertToLocString(
            "Eng:Recorded crossing of trades in the online broadcast._" +
            "Ru:Зафиксирован переход тиков в онЛайн трансляцию._");

        public string Message89 => OsLocalization.ConvertToLocString(
            "Eng: Order execute error _" +
            "Ru: Ошибка выставления заявки_");

        public string Message90 => OsLocalization.ConvertToLocString(
            "Eng:Port_" +
            "Ru:Порт_");

        public string Message91 => OsLocalization.ConvertToLocString(
            "Eng: Order reject successfully_" +
            "Ru: Ордер отозван успешно_");

        public string Message92 => OsLocalization.ConvertToLocString(
            "Eng: Order reject error_" +
            "Ru: Ошибка при отзыве ордера_");

        public string Message93 => OsLocalization.ConvertToLocString(
            "Eng: The loss of order is find. Re_" +
            "Ru: Зафиксирована пропажа ордера. Переподключаемся_");

        public string Message94 => OsLocalization.ConvertToLocString(
            "Eng: Your password has expired. You must change your password_" +
            "Ru: Время действия Вашего пароля истекло. Необходимо изменить пароль_");

        public string Message95 => OsLocalization.ConvertToLocString(
            "Eng: Failed to get the candles. Security _" +
            "Ru: Не удалось получить свечи по инструменту_");

        public string Message96 => OsLocalization.ConvertToLocString(
            "Eng: Incorrect data entered _" +
            "Ru: Введены не верные данные_");

        public string TitleTester => OsLocalization.ConvertToLocString(
            "Eng:Exchange emulator_" +
            "Ru:Эмулятор биржи_");

        public string Label22 => OsLocalization.ConvertToLocString(
            "Eng:Broadcast data_" +
            "Ru:Транслируемые данные_");

        public string Label23 => OsLocalization.ConvertToLocString(
            "Eng:Logging_" +
            "Ru:Лог_");

        public string Label24 => OsLocalization.ConvertToLocString(
            "Eng:Source_" +
            "Ru:Источник_");

        public string Label25 => OsLocalization.ConvertToLocString(
            "Eng:Translation type_" +
            "Ru:Транслируем_");

        public string Label26 => OsLocalization.ConvertToLocString(
            "Eng:From_" +
            "Ru:От_");

        public string Label27 => OsLocalization.ConvertToLocString(
            "Eng:To_" +
            "Ru:До_");

        public string Label28 => OsLocalization.ConvertToLocString(
            "Eng:Sets_" +
            "Ru:Сеты_");

        public string Label29 => OsLocalization.ConvertToLocString(
            "Eng: Broadcast data _" +
            "Ru: Транслируемые данные _");

        public string Label30 => OsLocalization.ConvertToLocString(
            "Eng: Order settings _" +
            "Ru: Настройки исполнения _");

        public string Label31 => OsLocalization.ConvertToLocString(
            "Eng: Portfolio _" +
            "Ru: Общий портфель _");

        public string ButtonSetFolder => OsLocalization.ConvertToLocString(
            "Eng:Find in folder_" +
            "Ru:Указать в папке_");

        public string Button1 => OsLocalization.ConvertToLocString(
            "Eng:More settings_" +
            "Ru:Дополнительно_");

        public string Button2 => OsLocalization.ConvertToLocString(
            "Eng:Start test_" +
            "Ru:Начать тест_");

        public string Label32 => OsLocalization.ConvertToLocString(
            "Eng:Limit slippage_" +
            "Ru:Проскальзывание для лимитов_");

        public string Label33 => OsLocalization.ConvertToLocString(
            "Eng:Stop slippage_" +
            "Ru:Проскальзывание для стопов_");

        public string Label34 => OsLocalization.ConvertToLocString(
            "Eng:Order execution_" +
            "Ru:Исполнение ордеров_");

        public string Label35 => OsLocalization.ConvertToLocString(
            "Eng:Disabled_" +
            "Ru:Отключено_");

        public string Label36 => OsLocalization.ConvertToLocString(
            "Eng:In steps_" +
            "Ru:В шагах_");

        public string Label37 => OsLocalization.ConvertToLocString(
            "Eng:Price touch_" +
            "Ru:Прикосновение цены_");

        public string Label38 => OsLocalization.ConvertToLocString(
            "Eng:Price crossing_" +
            "Ru:Пересечение цены_");

        public string Label39 => OsLocalization.ConvertToLocString(
            "Eng:Enable portfolio calculation_" +
            "Ru:Включить расчёт портфеля_");

        public string Label40 => OsLocalization.ConvertToLocString(
            "Eng:Initial deposit_" +
            "Ru:Начальный депозит_");

        public string Label41 => OsLocalization.ConvertToLocString(
            "Eng:Server address_" +
            "Ru:Адрес сервера_");

        public string Label42 => OsLocalization.ConvertToLocString(
            "Eng:Base active_" +
            "Ru:Базовый актив_");

        public string Label43 => OsLocalization.ConvertToLocString(
            "Eng:Market_" +
            "Ru:Биржа_");

        public string Label44 => OsLocalization.ConvertToLocString(
            "Eng:Security Type_" +
            "Ru:Тип инструмента_");

        public string Label45 => OsLocalization.ConvertToLocString(
            "Eng:Symbol_" +
            "Ru:Символ_");

        public string Label46 => OsLocalization.ConvertToLocString(
            "Eng:Prime market_" +
            "Ru:Биржа основная_");

        public string Label47 => OsLocalization.ConvertToLocString(
            "Eng:Delete_" +
            "Ru:Удалить_");

        public string Label48 => OsLocalization.ConvertToLocString(
            "Eng:Add_" +
            "Ru:Добавить_");

        public string Label49 => OsLocalization.ConvertToLocString(
            "Eng:The host cannot be empty. Connection terminated_" +
            "Ru:Хост не может быть пустым. Подключение прервано_");

        public string Label50 => OsLocalization.ConvertToLocString(
            "Eng:The port value is not a valid value. Connection terminated_" +
            "Ru:В значении порт не верное значение. Подключение прервано_");

        public string Label51 => OsLocalization.ConvertToLocString(
            "Eng:The value Id number is not a valid value. Connection terminated_" +
            "Ru:В значении номер Id не верное значение. Подключение прервано_");

        public string Label52 => OsLocalization.ConvertToLocString(
            "Eng:There are no securities to download. The connection was stopped. Specify the securities in the appropriate window_" +
            "Ru:Не указаны инструменты которые надо скачать. Подключение остановлено. Укажите инструменты в соответствующем окне_");

        public string Label53 => OsLocalization.ConvertToLocString(
            "Eng:Portfolio created _" +
            "Ru:Создан портфель _");


        public string Label55 => OsLocalization.ConvertToLocString(
            "Eng:Not enough data to start the server_" +
            "Ru:Не хватает данных чтобы запустить сервер_");

        public string Label56 => OsLocalization.ConvertToLocString(
            "Eng:Connection failed. The server is not responding_" +
            "Ru:Подключение не удалось. Сервер не отвечает_");

        public string Label57 => OsLocalization.ConvertToLocString(
            "Eng:Failed to start connection, missing one or more required parameters_" +
            "Ru:Не удалось начать подключение, отсутствует один или несколько обязательных параметров_");

        public string Label58 => OsLocalization.ConvertToLocString(
            "Eng:Login failed_" +
            "Ru:Ошибка входа в систему_");

        public string Label59 => OsLocalization.ConvertToLocString(
            "Eng:Save trades array in Candle_" +
            "Ru:Сохранять трейды в свече_");

        public string Label60 => OsLocalization.ConvertToLocString(
            "Eng:MD is internal?_" +
            "Ru:Стаканы эмулируются?_");

        public string Label61 => OsLocalization.ConvertToLocString(
            "Eng:Currency_" +
            "Ru:Валюта_");

        public string ServerParam1 => OsLocalization.ConvertToLocString(
            "Eng:Keep trade history_" +
            "Ru:Сохранять историю трейдов_");

        public string ServerParam2 => OsLocalization.ConvertToLocString(
            "Eng:Days to load trades_" +
            "Ru:Трейдов подгружать, дней_");

        public string ServerParamPublicKey => OsLocalization.ConvertToLocString(
            "Eng:Public key_" +
            "Ru:Публичный ключ_");

        public string ServerParamSecretKey => OsLocalization.ConvertToLocString(
            "Eng:Secret key_" +
            "Ru:Секретный ключ_");


        public string ServerParamToken => OsLocalization.ConvertToLocString(
            "Eng:Token_" +
            "Ru:Token_");

        public string ServerParamId => OsLocalization.ConvertToLocString(
            "Eng:Id_" +
            "Ru:Идентификатор_");

        public string ServerParamProxy => OsLocalization.ConvertToLocString(
            "Eng:Proxy_" +
            "Ru:Прокси_");

        public string ServerParamLeverage => OsLocalization.ConvertToLocString(
            "Eng:Leverage_" +
            "Ru:Плечо_");

        public string ServerParam3 => OsLocalization.ConvertToLocString(
            "Eng:Load data type_" +
            "Ru:Скачиваем_");

        public string ServerParam4 => OsLocalization.ConvertToLocString(
            "Eng:Is margin trading_" +
            "Ru:Маржинальная торговля_");

        public string ServerParam5 => OsLocalization.ConvertToLocString(
            "Eng:Keep candle history_" +
            "Ru:Сохранять историю свечек_");

        public string ServerParam6 => OsLocalization.ConvertToLocString(
            "Eng:Candles to load_" +
            "Ru:Свечей подгружать_");

        public string ServerParam7 => OsLocalization.ConvertToLocString(
            "Eng:Bid Ask in trades_" +
            "Ru:Грузим данные bid/ask в трейды_");

        public string ServerParam8 => OsLocalization.ConvertToLocString(
            "Eng:Remove Trades From Memory_" +
            "Ru:Удалять трейды из памяти_");

        public string ServerParam9 => OsLocalization.ConvertToLocString(
            "Eng:Remove Candles From Memory_" +
            "Ru:Удалять свечи из памяти_");

        public string UseStock => OsLocalization.ConvertToLocString(
            "Eng:Stock_" +
            "Ru:Акции_");

        public string UseFutures => OsLocalization.ConvertToLocString(
            "Eng:Futures_" +
            "Ru:Фьючерсы_");

        public string UseOptions => OsLocalization.ConvertToLocString(
            "Eng:Options_" +
            "Ru:Опционы_");

        public string UseCurrency => OsLocalization.ConvertToLocString(
            "Eng:Currency_" +
            "Ru:Валюты_");

        public string UseOther => OsLocalization.ConvertToLocString(
            "Eng:Other_" +
            "Ru:Другое_");

        public string UseSecInfoUpdates => OsLocalization.ConvertToLocString(
            "Eng:Use sec info updates_" +
            "Ru:Включить обновления инструментов_");
    }
}
