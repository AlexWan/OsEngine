namespace AdminPanel.Entity
{
    public enum LogMessageType
    {
        /// <summary>
        /// systemic message
        /// Системное сообщение
        /// </summary>
        System,

        /// <summary>
        /// Bot got a signal from one of strategies 
        /// Робот получил сигнал из одной из стратегий
        /// </summary>
        Signal,

        /// <summary>
        /// error happened
        /// Случилась ошибка
        /// </summary>
        Error,

        /// <summary>
        /// connect or disconnect message
        /// Сообщение о установке или обрыве соединения
        /// </summary>
        Connect,

        /// <summary>
        /// transaction message
        /// Сообщение об исполнении транзакции
        /// </summary>
        Trade,

        /// <summary>
        /// message without specification
        /// Сообщение без спецификации
        /// </summary>
        NoName,

        /// <summary>
        /// user action recorded
        /// Зафиксировано действие пользователя
        /// </summary>
        User

    }
}
