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
    public class DataLocal
    {
        public string TitleNewSecurity => OsLocalization.ConvertToLocString(
            "Eng:Add security_" +
            "Ru:Новый инструмент_");

        public string TitleDataSet => OsLocalization.ConvertToLocString(
            "Eng:Data set_" +
            "Ru:Сет данных_");

        public string ButtonAccept => OsLocalization.ConvertToLocString(
            "Eng:Accept_" +
            "Ru:Принять_");

        public string Label1 => OsLocalization.ConvertToLocString(
            "Eng:Class_" +
            "Ru:Класс_");

        public string Label2 => OsLocalization.ConvertToLocString(
            "Eng:Id_" +
            "Ru:Код бумаги_");

        public string Label3 => OsLocalization.ConvertToLocString(
            "Eng:Name_" +
            "Ru:Название_");

        public string Label4 => OsLocalization.ConvertToLocString(
            "Eng:Source_" +
            "Ru:Источник_");

        public string Label5 => OsLocalization.ConvertToLocString(
            "Eng:State_" +
            "Ru:Статус_");

        public string Label6 => OsLocalization.ConvertToLocString(
            "Eng:Add_" +
            "Ru:Добавить_");

        public string Label7 => OsLocalization.ConvertToLocString(
            "Eng:Redact_" +
            "Ru:Редактировать_");

        public string Label8 => OsLocalization.ConvertToLocString(
            "Eng:Delete_" +
            "Ru:Удалить_");

        public string Label9 => OsLocalization.ConvertToLocString(
            "Eng:You want to delete the set. Are you sure?_" +
            "Ru:Вы собираетесь удалить сет. Вы уверены?_");

        public string Label10 => OsLocalization.ConvertToLocString(
            "Eng:Creation of set is interrupted. You must give the name of the Set!_" +
            "Ru:Создание сета прервано. Необходимо дать сету имя!_");

        public string Label11 => OsLocalization.ConvertToLocString(
            "Eng:Creation of set is interrupted. Set with that name already exists!_" +
            "Ru:Создание сета прервано. Сет с таким именем уже существует!_");

        public string Label12 => OsLocalization.ConvertToLocString(
            "Eng:Source not configured_" +
            "Ru:Источник не настроен_");

        public string Label13 => OsLocalization.ConvertToLocString(
            "Eng:There are no securities in the source._" +
            "Ru:В источнике нет доступных бумаг_");

        public string Label14 => OsLocalization.ConvertToLocString(
            "Eng:Name_" +
            "Ru:Название_");

        public string Label15 => OsLocalization.ConvertToLocString(
            "Eng:Candle points_" +
            "Ru:Сборка свечей_");

        public string Label16 => OsLocalization.ConvertToLocString(
            "Eng:Securities_" +
            "Ru:Инструменты_");

        public string Label17 => OsLocalization.ConvertToLocString(
            "Eng:Depth_" +
            "Ru:Глубина_");

        public string Label18 => OsLocalization.ConvertToLocString(
            "Eng:Start_" +
            "Ru:Начало_");

        public string Label19 => OsLocalization.ConvertToLocString(
            "Eng:End_" +
            "Ru:Конец_");

        public string Label20 => OsLocalization.ConvertToLocString(
            "Eng:Regime_" +
            "Ru:Режим_");

        public string Label21 => OsLocalization.ConvertToLocString(
            "Eng:Add data to trade servers_" +
            "Ru:Добавить данные к торговым серверам_");

        public string Label22 => OsLocalization.ConvertToLocString(
            "Eng:Auto update_" +
            "Ru:Авто обновление_");

        public string Label23 => OsLocalization.ConvertToLocString(
            "Eng:Saving aborted. You need to give a name to Set_" +
            "Ru:Сохранение прервано. Сету необходимо задать имя_");

        public string Label24 => OsLocalization.ConvertToLocString(
            "Eng:Sets_" +
            "Ru:Сеты_");

        public string Label25 => OsLocalization.ConvertToLocString(
            "Eng:Chart is paining_" +
            "Ru:Прорисовка графика_");

        public string Label26 => OsLocalization.ConvertToLocString(
            "Eng:Log_" +
            "Ru:Лог_");

        public string Label27 => OsLocalization.ConvertToLocString(
            "Eng:You want to close the program. Are you sure?_" +
            "Ru:Вы собираетесь закрыть программу. Вы уверены?_");

        public string Label28 => OsLocalization.ConvertToLocString(
            "Eng:We request trades on security _" +
            "Ru:Запрашиваем трейды по бумаге _");

        public string Label29 => OsLocalization.ConvertToLocString(
            "Eng:Trades successfully loaded. Security _" +
            "Ru:Трейды успешно загружены. Бумага _");
    }
}
