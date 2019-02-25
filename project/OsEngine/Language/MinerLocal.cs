/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Language
{
    public class MinerLocal
    {
        public string Title1 => OsLocalization.ConvertToLocString(
            "Eng:Give the new pattern set a name_" +
            "Ru:Задайте новому сету паттернов имя_");

        public string Title2 => OsLocalization.ConvertToLocString(
            "Eng:Patterns found in the last mining session_" +
            "Ru:Паттерны найденные за последнюю сессию майнинга_");

        public string Button1 => OsLocalization.ConvertToLocString(
            "Eng:Accept_" +
            "Ru:Применить_");

        public string Message1 => OsLocalization.ConvertToLocString(
            "Eng:Set with that name has already been created_" +
            "Ru:Сет с таким именем уже создан_");

        public string Message2 => OsLocalization.ConvertToLocString(
            "Eng:Characters # *? % ^; banned in titles_" +
            "Ru:Символы # * ? % ^ ; запрещены в названиях_");


        public string Message3 => OsLocalization.ConvertToLocString(
            "Eng:Do you want to delete the set of patterns?_" +
            "Ru:Вы собираетесь удалить сет паттернов?_");

        public string Message4 => OsLocalization.ConvertToLocString(
            "Eng:Name_" +
            "Ru:Название_");

        public string Message5 => OsLocalization.ConvertToLocString(
            "Eng:Patterns count_" +
            "Ru:Паттернов_");

        public string Message6 => OsLocalization.ConvertToLocString(
            "Eng:Add_" +
            "Ru:Добавить_");

        public string Message7 => OsLocalization.ConvertToLocString(
            "Eng:Delete_" +
            "Ru:Удалить_");

        public string Message8 => OsLocalization.ConvertToLocString(
            "Eng:You want to delete the pattern. Are you sure?_" +
            "Ru:Вы собираетесь удалить паттерн. Вы уверены?_");

        public string Message9 => OsLocalization.ConvertToLocString(
            "Eng:Redact_" +
            "Ru:Редактировать_");

        public string Label1 => OsLocalization.ConvertToLocString(
            "Eng:Set of patterns #_" +
            "Ru:Набор паттернов #_");

        public string Label2 => OsLocalization.ConvertToLocString(
            "Eng:Set name cannot be empty_" +
            "Ru:Имя сета не может быть пустым_");

        public string Label3 => OsLocalization.ConvertToLocString(
            "Eng:Pattern Set Name_" +
            "Ru:Имя сета паттернов_");

        public string Label5 => OsLocalization.ConvertToLocString(
            "Eng:Sets_" +
            "Ru:Сеты_");

        public string Label6 => OsLocalization.ConvertToLocString(
            "Eng:Log_" +
            "Ru:Лог_");

        public string Label7 => OsLocalization.ConvertToLocString(
            "Eng:Groups of patterns in the set_" +
            "Ru:Группы паттернов в сете_");

        public string Label8 => OsLocalization.ConvertToLocString(
            "Eng:Number of inputs_" +
            "Ru:Кол-во входов_");

        public string Label9 => OsLocalization.ConvertToLocString(
            "Eng:Profitability_" +
            "Ru:Прибыльность_");

        public string Label10 => OsLocalization.ConvertToLocString(
            "Eng:Уxpected value_" +
            "Ru:МО_");

        public string Label11 => OsLocalization.ConvertToLocString(
            "Eng:The data set is not accepted because contains data with different timeframes_" +
            "Ru:Сет данных не принят, т.к. содержит данные с разным таймфреймом_");

        public string Label12 => OsLocalization.ConvertToLocString(
            "Eng:Selected series for trade is absent_" +
            "Ru:Выбранная серия для торговли - отсутствует_");

        public string Label13 => OsLocalization.ConvertToLocString(
            "Eng:Not one method of closing a position has been chosen. Testing is not possible_" +
            "Ru:Не выбран не один способ закрытия позиции. Тестирование не возможно_");

        public string Label14 => OsLocalization.ConvertToLocString(
            "Eng:It was not possible to find all the necessary data series for the patterns at OPENING. Go to the appropriate tab and assign a series of data to all the patterns._" +
            "Ru:К паттернам на ОТКРЫТИЕ не удалось найти всех нужных серий данных. Перейдите в соответствующую вкладку и назначте всем паттернам серию данных_");

        public string Label15 => OsLocalization.ConvertToLocString(
            "Eng:The patterns for CLOSURE could not find all the necessary data series. Go to the appropriate tab and assign a series of data to all the patterns._" +
            "Ru:К паттернам на ЗАКРЫТИЕ не удалось найти всех нужных серий данных. Перейдите в соответствующую вкладку и назначте всем паттернам серию данных_");

        public string Label16 => OsLocalization.ConvertToLocString(
            "Eng:There were no deals on the pattern_" +
            "Ru:По паттерну небыло сделок_");

        public string Label17 => OsLocalization.ConvertToLocString(
            "Eng:Total profit _" +
            "Ru:Общий профит _");

        public string Label18 => OsLocalization.ConvertToLocString(
            "Eng:Number of deals _" +
            "Ru:Количество входов _");

        public string Label19 => OsLocalization.ConvertToLocString(
            "Eng:Profit from deal _" +
            "Ru:Прибыль со сделки _");

        public string Label20 => OsLocalization.ConvertToLocString(
            "Eng:Pattern search completed. We checked all the data_" +
            "Ru:Поиск паттернов завершён. Мы проверили все данные_");

        public string Label21 => OsLocalization.ConvertToLocString(
            "Eng:Set security for trade_" +
            "Ru:Установите бумагу для торговли_");

        public string Label22 => OsLocalization.ConvertToLocString(
            "Eng:Weight can not be zero_" +
            "Ru:Вес не может быть нулевым_");

        public string Label23 => OsLocalization.ConvertToLocString(
            "Eng:It is impossible to set a pattern for closing positions at that moment when there is not a single pattern for opening!_" +
            "Ru:Нельзя устанавливать паттерн на закрытие позиций в тот момент когда нет ни одного паттерна на открытие!_");

        public string Label24 => OsLocalization.ConvertToLocString(
            "Eng:No historical data has been found for trading this security!_" +
            "Ru:Не найдены исторические данные, установленные для торговли по этому инструменту!_");

        public string Label25 => OsLocalization.ConvertToLocString(
            "Eng:Pattern _" +
            "Ru:Паттерн _");

        public string Label26 => OsLocalization.ConvertToLocString(
            "Eng:Creating a pattern group_" +
            "Ru:Создание группы паттернов_");

        public string Label27 => OsLocalization.ConvertToLocString(
            "Eng:Setting and searching patterns_" +
            "Ru:Настройка и поиск паттернов_");

        public string Label28 => OsLocalization.ConvertToLocString(
            "Eng:Test_" +
            "Ru:Пересчитать_");

        public string Label29 => OsLocalization.ConvertToLocString(
            "Eng:Tests the current configuration of patterns without a pattern in the search tab_" +
            "Ru:Тестирует текущую конфигурацию паттернов без учёта паттерна во вкладке поиска_");

        public string Label30 => OsLocalization.ConvertToLocString(
            "Eng:Security_" +
            "Ru:Инструмент_");

        public string Label31 => OsLocalization.ConvertToLocString(
            "Eng:Positions_" +
            "Ru:Позиции_");

        public string Label32 => OsLocalization.ConvertToLocString(
            "Eng:Journal_" +
            "Ru:Журнал_");

        public string Label33 => OsLocalization.ConvertToLocString(
            "Eng:Last trades transaction log journal_" +
            "Ru:Журнал сделок по последнему тесту_");

        public string Label34 => OsLocalization.ConvertToLocString(
            "Eng: Data _" +
            "Ru: Данные _");

        public string Label35 => OsLocalization.ConvertToLocString(
            "Eng:Source_" +
            "Ru:Источник_");

        public string Label36 => OsLocalization.ConvertToLocString(
            "Eng:Folder - data folder. Set - data set downloaded with OsData_" +
            "Ru:Folder - папка с данными.Set - сет данных скаченный при помощи OsData_");

        public string Label37 => OsLocalization.ConvertToLocString(
            "Eng:Sets_" +
            "Ru:Сеты_");

        public string Label38 => OsLocalization.ConvertToLocString(
            "Eng:Indicate in the folder_" +
            "Ru:Указать в папке_");

        public string Label39 => OsLocalization.ConvertToLocString(
            "Eng: Opening deals _" +
            "Ru: Открытие позиции _");

        public string Label40 => OsLocalization.ConvertToLocString(
            "Eng:Patterns_" +
            "Ru:Паттерны_");

        public string Label41 => OsLocalization.ConvertToLocString(
            "Eng:Side_" +
            "Ru:Сторона_");

        public string Label42 => OsLocalization.ConvertToLocString(
            "Eng:What we will do when we find the necessary patterns for entry. Buy or sell_" +
            "Ru:Что будем делать когда наёдём нужные паттерны для входа. Покупать или продавать_");

        public string Label43 => OsLocalization.ConvertToLocString(
            "Eng:Entry Weight_" +
            "Ru:Вес для входа_");

        public string Label44 => OsLocalization.ConvertToLocString(
            "Eng:The total weight of the patterns found on the current candle that are required to enter a position_" +
            "Ru:Общий вес найденных на текущей свече паттернов, необходимых для входа в позицию_");

        public string Label45 => OsLocalization.ConvertToLocString(
            "Eng:Closing deals_" +
            "Ru:Закрытие позиции_");

        public string Label46 => OsLocalization.ConvertToLocString(
            "Eng:Stop order %_" +
            "Ru:Стоп-ордер %_");

        public string Label47 => OsLocalization.ConvertToLocString(
            "Eng:Profit order %_" +
            "Ru:Профит-ордер %_");

        public string Label48 => OsLocalization.ConvertToLocString(
            "Eng:Through N candles_" +
            "Ru:Через N свечей_");

        public string Label49 => OsLocalization.ConvertToLocString(
            "Eng:Trailing Stop %_" +
            "Ru:Трейлинг-Стоп %_");

        public string Label50 => OsLocalization.ConvertToLocString(
            "Eng:Weight for exit_" +
            "Ru:Вес для выхода_");

        public string Label51 => OsLocalization.ConvertToLocString(
            "Eng:Weight for exit_" +
            "Ru:* все настройки в минимальных движениях цены инструмента_");

        public string Label52 => OsLocalization.ConvertToLocString(
            "Eng:Pattern Search_" +
            "Ru:Поиск паттерна_");

        public string Label53 => OsLocalization.ConvertToLocString(
            "Eng:Use_" +
            "Ru:Использование_");

        public string Label54 => OsLocalization.ConvertToLocString(
            "Eng:Weight_" +
            "Ru:Вес_");

        public string Label55 => OsLocalization.ConvertToLocString(
            "Eng:Recognizability_" +
            "Ru:Узнаваемость_");

        public string Label56 => OsLocalization.ConvertToLocString(
            "Eng:Save_" +
            "Ru:Сохранить_");

        public string Label57 => OsLocalization.ConvertToLocString(
            "Eng:Short report_" +
            "Ru:Короткий отчёт_");

        public string Label58 => OsLocalization.ConvertToLocString(
            "Eng:Automatic search_" +
            "Ru:Автоматический поиск_");

        public string Label59 => OsLocalization.ConvertToLocString(
            "Eng:E(X) >_" +
            "Ru:МО >_");

        public string Label60 => OsLocalization.ConvertToLocString(
            "Eng:Deals >_" +
            "Ru:Сделок >_");

        public string Label61 => OsLocalization.ConvertToLocString(
            "Eng:Profit >_" +
            "Ru:Профит >_");

        public string Label62 => OsLocalization.ConvertToLocString(
            "Eng:Report_" +
            "Ru:Отчёт_");

        public string Label63 => OsLocalization.ConvertToLocString(
            "Eng:Candles_" +
            "Ru:Свечи_");

        public string Label64 => OsLocalization.ConvertToLocString(
            "Eng:Volume_" +
            "Ru:Объём_");

        public string Label65 => OsLocalization.ConvertToLocString(
            "Eng:Time_" +
            "Ru:Время_");

        public string Label66 => OsLocalization.ConvertToLocString(
            "Eng:Indicators_" +
            "Ru:Индикаторы_");

        public string Label67 => OsLocalization.ConvertToLocString(
            "Eng:Length_" +
            "Ru:Длина_");

        public string Label68 => OsLocalization.ConvertToLocString(
            "Eng:Search method_" +
            "Ru:Способ поиска_");

        public string Label69 => OsLocalization.ConvertToLocString(
            "Eng:Trading period_" +
            "Ru:Торговый период_");

        public string Label70 => OsLocalization.ConvertToLocString(
            "Eng:Start trading_" +
            "Ru:Начало торговли_");

        public string Label71 => OsLocalization.ConvertToLocString(
            "Eng:End trading_" +
            "Ru:Конец торговли_");

        public string Label72 => OsLocalization.ConvertToLocString(
            "Eng:Analysis of indicator lines_" +
            "Ru:Анализ линий индикаторов_");

        public string Label73 => OsLocalization.ConvertToLocString(
            "Eng:Type_" +
            "Ru:Тип_");

        public string Label74 => OsLocalization.ConvertToLocString(
            "Eng:Type_" +
            "Ru:Тип_");
    }
}