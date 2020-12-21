/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace AdminPanel.Language
{
    public class MainWindowLocal
    {
        public string Title => OsLocalization.ConvertToLocString(
            "Eng:Admin panel_" +
            "Ru:Панель администратора_");

        public string TabOverview => OsLocalization.ConvertToLocString(
            "Eng:Overview_" +
            "Ru:Обзор_");

        public string TabClients => OsLocalization.ConvertToLocString(
            "Eng:Clients_" +
            "Ru:Клиенты_");

        public string TabSettings => OsLocalization.ConvertToLocString(
            "Eng:Settings_" +
            "Ru:Настройки");

        public string TabMain => OsLocalization.ConvertToLocString(
            "Eng:Main_" +
            "Ru:Общее_");

        public string TabServers => OsLocalization.ConvertToLocString(
            "Eng:Servers_" +
            "Ru:Сервера_");

        public string TabRobots => OsLocalization.ConvertToLocString(
            "Eng:Robots_" +
            "Ru:Роботы_");

        public string TabAllPositions => OsLocalization.ConvertToLocString(
            "Eng:All positions_" +
            "Ru:Все позиции_");

        public string TabPortfolio => OsLocalization.ConvertToLocString(
            "Eng:Portfolio_" +
            "Ru:Портфель_");

        public string TabOrders => OsLocalization.ConvertToLocString(
            "Eng:Orders_" +
            "Ru:Ордера_");

        public string CloseLabel => OsLocalization.ConvertToLocString(
            "Eng:You want to close the program. Are you sure?_" +
            "Ru:Вы собираетесь закрыть программу. Вы уверены?_");

        public string DeleteLabel => OsLocalization.ConvertToLocString(
            "Eng:You are about to remove the client. Are you sure?_" +
            "Ru:Вы собираетесь удалить клиента. Вы уверены?_");

        public string NewClientErrorName => OsLocalization.ConvertToLocString(
            "Eng:A customer with the same name already exists, please enter another._" +
            "Ru:Клиент с таким именем уже существует, введите другое._");

        public string AddClientTitle => OsLocalization.ConvertToLocString(
            "Eng:New client_" +
            "Ru:Новый клиент_");

        public string Label13 => OsLocalization.ConvertToLocString(
            "Eng:In one of the fields is invalid values. Save process aborted_" +
            "Ru:В одном из полей недопустимые значения. Процесс сохранения прерван_");

        public string Label14 => OsLocalization.ConvertToLocString(
            "Eng:The client already has an os engine with the same name_" +
            "Ru:Os engine с таким именем уже есть у клиента_");

        public string DeleteLabel2 => OsLocalization.ConvertToLocString(
            "Eng:You are about to remove os engine. Are you sure?_" +
            "Ru:Вы собираетесь удалить os engine. Вы уверены?_");

        public string TlConnectError => OsLocalization.ConvertToLocString(
            "Eng:Failed to connect to telegrams, please try again._" +
            "Ru:Не удалось подключиться к телеграмм, повторите попытке._");
    }
}
