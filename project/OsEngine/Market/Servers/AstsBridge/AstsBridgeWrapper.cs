/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using OsEngine.Entity;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.AstsBridge
{

    /// <summary>
    /// library wrapper mtesrl.dll.
    /// обёртка библиотеки mtesrl.dll.
    /// </summary>
    public class AstsBridgeWrapper
    {

// PART ONE. Managment version mtesrl.dll
// ЧАСТЬ ПЕРВАЯ. Managment версия mtesrl.dll

        /// <summary>
        /// connecto to AstsBrige
        /// Подключиться к AstsBrige
        /// </summary>
        /// <param name="paramsString"> string with parameters / строка с параметрами </param>
        /// <param name="errorString"> the variable in which the error code will be written if something goes wrong / переменная в которую будет записан код ошибки если что-то пойдёт не так</param>
        /// <returns>Connection descriptor or error code (negative value) if something went wrong / Дескриптор соединения или код ошибки(отрицательное значение) если что-то пошло не так</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTEConnect", 
            CallingConvention = CallingConvention.StdCall)]
        private static extern int MteConnect(
             StringBuilder paramsString,
             StringBuilder errorString);

        /// <summary>
        /// request the AstsBrige status
        /// запросить статус AstsBrige
        /// </summary>
        /// <param name="procNum">number of connect / номер коннекта</param>
        /// <returns>Status: 0 - enabled and running, else error code / Статус: 0 - включен и работает. Остальное код ошибки</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTEConnectionStatus", 
            CallingConvention = CallingConvention.StdCall)]
        private static extern int MTEConnectionStatus(
            [param: MarshalAs(UnmanagedType.I4)] Int32 procNum);

        /// <summary>
        /// disconnect from AstsBrige
        /// Отключиться от AstsBrige
        /// </summary>
        /// <returns>result of execution. 0 - OK, else error code / результат выполнения. 0 - ОК. Остальное код ошибки</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTEDisconnect", 
            CallingConvention = CallingConvention.StdCall)]
        private static extern int MteDisconnect(
            [param: MarshalAs(UnmanagedType.I4)] Int32 procNum);

        /// <summary>
        /// request an error specification
        /// Запросить спецификацию ошибки
        /// </summary>
        /// <returns>result of execution. 0 - OK, else error code / результат выполнения. 0 - ОК. Остальное код ошибки</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTEErrorMsg", 
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr MteErrorMsg(
            [param: MarshalAs(UnmanagedType.I4)] Int32 procNum);

        /// <summary>
        /// Request table opening
        /// Запросить открытие таблицы
        /// </summary>
        /// <param name="procNum">connection handle issued MteConnect / дескриптор соединения выданный MteConnect</param>
        /// <param name="tableName">name of the table that we request / название таблицы которую мы запрашиваем</param>
        /// <param name="tableParams">parameters for opening the table / параметры для открытия таблицы</param>
        /// <param name="loadAllTable">need to upload full table / нужно ли подгружать полную таблицу</param>
        /// <param name="msg"> variable for request result / переменная в которую будет записан результат запроса</param>
        /// <returns>if >=0 then this's the table descriptor needed to request its update, else returns error code / если >= 0, то это дескриптор таблицы, нужный для запроса её обновления. Если меньше 0, то код ошибки</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTEOpenTable",
            CallingConvention = CallingConvention.StdCall)]
        private static unsafe extern int MteOpenTableFirstTime(
            [param: MarshalAs(UnmanagedType.I4)] Int32 procNum,
            [param: MarshalAs(UnmanagedType.LPStr)] string tableName,
            [param: MarshalAs(UnmanagedType.LPStr)] string tableParams,
            bool loadAllTable,
            out int* msg);

        /// <summary>
        /// Request data structures
        /// Запросить структуры данных
        /// </summary>
        /// <param name="procNum">connection handle issued MteConnect / дескриптор соединения выданный MteConnect</param>
        /// <param name="version">number of requested structures / номер запрашиваемых структур</param>
        /// <param name="msg"> variable for request result / переменная в которую будет записан результат запроса</param>
        /// <returns> if less 0, then returns error code / если меньше 0, то код ошибки</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTEStructureEx", 
            CallingConvention = CallingConvention.StdCall)]
        private static unsafe extern int MteStructureEx(
            [param: MarshalAs(UnmanagedType.I4)] Int32 procNum,
            [param: MarshalAs(UnmanagedType.I4)] Int32 version,
            out int* msg);

        /// <summary>
        /// Queue table for update
        /// Поставить таблицу в очередь на обновление
        /// </summary>
        /// <param name="procNum">connection handle issued MteConnect / дескриптор соединения выданный MteConnect</param>
        /// <param name="tableNum">descriptor of the table to be queued for update/дескриптор таблицы которую нужно ставить в очередь на обновление</param>
        /// <param name="userId">field for entering the table identifier(created by the user)/поле для ввода идентификатра таблицы(придумывается пользователем) 
        /// which is then passed to memory at the beginning of this table / которая потом передатся в память в начале этой таблицы</param>
        /// <returns>0 - everything is OK. Else returns error code/0 - всё ок. Всё остальное код ошибки</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTEAddTable", 
            CallingConvention = CallingConvention.StdCall)]
        private static extern int MteAddTableInQueueOnRefresh(
            [param: MarshalAs(UnmanagedType.I4)] Int32 procNum,
            [param: MarshalAs(UnmanagedType.I4)] Int32 tableNum,
            [param: MarshalAs(UnmanagedType.I4)] Int32 userId);

        /// <summary>
        /// update the tables placed in the queue to update
        /// Обновить таблицы поставленные в очередь на обновление
        /// </summary>
        /// <param name="procNum">connection descriptor is issued by MteConnect/дескриптор соединения выданный MteConnect</param>
        /// <param name="msg">request result / результат запросов</param>
        /// <returns>0 - everything is OK. Else returns error code / 0 - всё ок. Всё остальное код ошибки. если пришла ошибка, то msg содержит её спецификацию</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTERefresh", 
            CallingConvention = CallingConvention.StdCall)]
        private static unsafe extern int MteRefresh(
            [param: MarshalAs(UnmanagedType.I4)] Int32 procNum,
            out int* msg);

        /// <summary>
        /// cloae the table
        /// Закрыть таблицу
        /// </summary>
        /// <param name="procNum">connection descriptor is issued by MteConnect/дескриптор соединения выданный MteConnect</param>
        /// <param name="tabNum">table descriptor / дескриптор таблицы</param>
        /// <returns>0 - everything is OK. Else returns error code / 0 - всё ок. Всё остальное код ошибки</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTECloseTable", 
            CallingConvention = CallingConvention.StdCall)]
        private static extern int MteCloseTable(
            [param: MarshalAs(UnmanagedType.I4)] Int32 procNum,
            [param: MarshalAs(UnmanagedType.I4)] Int32 tabNum);

        /// <summary>
        /// execute the transaction
        /// Исполнить транзакцию
        /// </summary>
        /// <param name="procNum">connection descriptor is issued by MteConnect/дескриптор соединения выданный MteConnect</param>
        /// <param name="transName">transaction name / название транзакции</param>
        /// <param name="transParams">transaction parameters/параметры транзакции</param>
        /// <param name="errorString">result/результат</param>
        /// <returns>0 - everything is OK. Else returns error code / 0 - всё ок. Всё остальное код ошибки</returns>
        [DllImport("mtesrl64.dll", EntryPoint = "MTEExecTrans",
            CallingConvention = CallingConvention.StdCall)]
        private static extern int MteExecTrans(
            [param: MarshalAs(UnmanagedType.I4)] Int32 procNum,
            StringBuilder transName,
            StringBuilder transParams,
            StringBuilder errorString);

        // PART TWO. Implementation Of API
        // ЧАСТЬ ВТОРАЯ. Реализация Апи 

        /// <summary>
        /// deploy ASTS Bridge Api
        /// развернуть ASTS Bridge Api. 
        /// </summary>
        public AstsBridgeWrapper()
        {
            _ordersToExecute = new ConcurrentQueue<Order>();
            _ordersToCansel = new ConcurrentQueue<Order>();
        }

        /// <summary>
        /// client code
        /// код клиента
        /// </summary>
        public string ClientCode;

        /// <summary>
        /// data processing class from my trades table
        /// класс обработки данных из таблицы моих сделок
        /// </summary>
        private MyTradeTableConverter _tableMyTrade;

        /// <summary>
        /// data processing class from my orders table
        /// класс обработки данных из таблицы моих ордеров
        /// </summary>
        private OrderTableConverter _tableOrder;

        /// <summary>
        /// data processing class from security table
        /// класс обработки данных из таблицы бумаг
        /// </summary>
        private SecurityTableConverter _tableSecurity;

        /// <summary>
        /// data processing class from portfolio table
        /// класс обработки данных из таблицы портфелей
        /// </summary>
        private PortfoliosTableConverter _tablePortfolios;

        /// <summary>
        /// data processing class from trades table
        /// класс обработки данных из таблицы трейдов
        /// </summary>
        private TradesTableConverter _tableTrade;

        /// <summary>
        /// data processing class from place table
        /// класс обработки данных из таблицы площадок
        /// </summary>
        private SecurityBoardsConverter _tableBoards;

        /// <summary>
        /// класс обработки данных из таблицы площадок
        /// класс обработки данных из таблицы стаканов
        /// </summary>
        private MarketDepthTableConverter _marketDepthTable;

        /// <summary>
        /// создать соединение с Asts
        /// </summary>
        /// <param name="startParam">параметры доступа к Asts серверу</param>
        public void Connect(StringBuilder startParam)
        {
            if (IsConnected == false)
            {
                StringBuilder stringBuilder = new StringBuilder(256);

              int result = MteConnect(startParam, stringBuilder);

              if (result >= 0)
                {
                    _isConnected = true;

                    if (ConnectedEvent != null)
                    {
                        ConnectedEvent();
                    }
                    _procNum = result;
                }
                else
                {
                    _isConnected = false;
                    if (DisconnectedEvent != null)
                    {
                        DisconnectedEvent(stringBuilder.ToString());
                    }
                    SendErrorFromAsts(result);
                }
            }
        }

        /// <summary>
        /// connection descriptor. Unique number issued by the server when connecting
        /// дескриптор соединения. Уникальный номер выданный сервером при подключении
        /// </summary>
        private int _procNum;

        /// <summary>
        /// disconnect with Asts
        /// разорвать соединение с Asts
        /// </summary>
        public void Disconnect()
        {
            if (IsConnected)
            {
              int result =  MteDisconnect(_procNum);

                _isConnected = false;
                _procNum = -1;

                if (DisconnectedEvent != null)
                {
                    DisconnectedEvent("Пользователь запросил отключение от Asts");
                }

                if (result < 0)
                {
                    SendErrorFromAsts(result);
                }
            }
        }

        /// <summary>
        /// whether the wrapper for the Asts Bridge is running. True - if everything is OK.
        /// запущена ли обёртка для AstsBridge. True - если всё ОК.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
        }
        private bool _isConnected;

        /// <summary>
        /// array of data type ASTS
        /// массив типов данных ASTS
        /// </summary>
        private List<AstsEnumType> _typesStruct;

        /// <summary>
        /// array of data table ASTS
        /// массив таблиц данных ASTS
        /// </summary>
        private List<AstsTable> _tablesStruct;

        /// <summary>
        /// array of data table ASTS
        /// массив таблиц данных ASTS
        /// </summary>
        private List<AstsTransaction> _transactionStruct;

// get data structure
// принимаем структуры данных

        /// <summary>
        /// request data structure for tables
        /// запросить структуру данных для таблиц
        /// </summary>
        public unsafe void GetStructureData()
        {
            /*  The structure of TMTEMsg is defined as: / Структура TMTEMsg определена так:
            С++ typedef 
            struct TMTEMSG_TAG
            {
                long DataLen; // Length of the next data / Длина следующих далее данных // 
                char Data [
                DataLen]
                ; // commented
            }*/

            /* 
            4 bytes containing the length of this string are passed before each String field / перед каждым полем типа String передаётся 4 байта, содержащие длину этой строки)
            interface name String / ИмяИнтерфейса String
            header of interface / ЗаголовокИнтерфейса String
            description of interface / ОписаниеИнтерфейса String // only / только MTEStructureEx c Version>=2
            enumerated type / ПеречислимыеТипы TEnumTypes
            tables / Таблицы TTables
            transaction / Транзакции TTransactions
           */

            int* ptrOnTable;

            int result = MteStructureEx(_procNum, 2, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig = 0;

                int[] lenghtFull = new int[1];
                Marshal.Copy(IntPtr.Add(new IntPtr(ptrOnTable),  sdvig), lenghtFull, 0, 1);

                ptrOnTable += 1; // shift to the length to int / сдвигаем на длинну длинны инта

                string intrFase = GetString(ptrOnTable,out ptrOnTable, sdvig, out sdvig);


                string intrFaseHader = GetString(ptrOnTable, out ptrOnTable, sdvig, out sdvig);

                _typesStruct = GetAllTypes(ptrOnTable, out ptrOnTable, sdvig, out  sdvig);

                _tablesStruct = GetAllTables(ptrOnTable, out ptrOnTable, sdvig, out  sdvig);

                List<AstsTable> table = _tablesStruct.FindAll(t => t.Header == "Обязательства и требования по активам");

                _transactionStruct = GetAllTrans(ptrOnTable, out ptrOnTable, sdvig, out  sdvig);

                // string res = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptrOnTable), sdvig), 400);

                SendLogMessage("Примитивы подргужены. Типов: " + _typesStruct.Count + ". Таблиц: " + _tablesStruct.Count + "Транзакций: " + _transactionStruct.Count, LogMessageType.System);

                return;

            }
            else
            {
                SendErrorFromAsts(result);
                return;
            }

        }

        /// <summary>
        /// take data types from the memory in the message MTEStructureEx
        /// взять типы данных из памяти в сообщении MTEStructureEx
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес сдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift / побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <returns>types/типы</returns>
        private unsafe List<AstsEnumType> GetAllTypes(int* ptr, out int* newPtr, int sdvig, out int newSdvig)
        {
            GetInt(ptr, out ptr, sdvig);
            int typesCount = GetInt(ptr, out ptr, sdvig);

            List<AstsEnumType> types = new List<AstsEnumType>();

            for (int i = 0; i < typesCount; i++)
            {
                types.Add(GetOneType(ptr, out ptr, sdvig, out  sdvig));
            }

            newPtr = ptr;
            newSdvig = sdvig;
            return types;
        }

        /// <summary>
        /// take one type of data from the memory in the message MTEStructureEx
        /// взять один тип данных из памяти в сообщении MTEStructureEx
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес вдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <returns>type/тип</returns>
        private unsafe AstsEnumType GetOneType(int* ptr, out int* newPtr, int sdvig, out int newSdvig)
        {
            AstsEnumType type = new AstsEnumType();

            type.Name = GetString(ptr, out ptr, sdvig, out sdvig);
            type.Header = GetString(ptr, out ptr, sdvig, out sdvig);
            type.Description = GetString(ptr, out ptr, sdvig, out sdvig);
            type.Lenght = GetInt(ptr, out ptr, sdvig);

            int numType = GetInt(ptr, out ptr, sdvig);
            if (numType == 0)
            {
                type.Type  = AstsEnumKind.EkCheck;
            }
            else if (numType == 1)
            {
                type.Type = AstsEnumKind.EkGroup;
            }
            else if (numType == 2)
            {
                type.Type = AstsEnumKind.EkCombo;
            }

            int constCount = GetInt(ptr, out ptr, sdvig);

            type.Consts = new List<AstsEnumConst>();
            
            for (int i = 0; i < constCount; i++)
            {
                AstsEnumConst newConst = new AstsEnumConst();
                newConst.Value = GetString(ptr, out ptr, sdvig, out sdvig);
                newConst.LongDescription = GetString(ptr, out ptr, sdvig, out sdvig);
                newConst.ShorDescription = GetString(ptr, out ptr, sdvig, out sdvig);

                type.Consts.Add(newConst);
            }

            newPtr = ptr;
            newSdvig = sdvig;
            return type;
        }

        /// <summary>
        /// take tables from the memory in the message MTEStructureEx
        /// взять таблицы из памяти в сообщении MTEStructureEx
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес сдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <returns>types/типы</returns>
        private unsafe List<AstsTable> GetAllTables(int* ptr, out int* newPtr, int sdvig, out int newSdvig)
        {
            int tableCount = GetInt(ptr, out ptr, sdvig);

            List<AstsTable> tables = new List<AstsTable>();
           
            for (int i = 0; i < tableCount; i++)
            {
                tables.Add(GetOneTable(ptr, out ptr, sdvig, out  sdvig));
            }

            newPtr = ptr;
            newSdvig = sdvig;
            return tables;
        }

        /// <summary>
        /// take one table from the memory in the message MTEStructureEx
        /// взять одну таблицу данных из памяти в сообщении MTEStructureEx
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес сдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <returns>tables/таблица</returns>
        private unsafe AstsTable GetOneTable(int* ptr, out int* newPtr, int sdvig, out int newSdvig)
        {
            AstsTable table = new AstsTable();

            table.Name = GetString(ptr, out ptr, sdvig, out sdvig);
            table.Header = GetString(ptr, out ptr, sdvig, out sdvig);
            table.Description = GetString(ptr, out ptr, sdvig, out sdvig);
            table.IndexInSystem = GetInt(ptr, out ptr, sdvig);

            int numType = GetInt(ptr, out ptr, sdvig);
            if (numType ==1)
            {
                table.Flag = AstsTableFlags.TfUpdateable;
            }
            else if (numType == 2)
            {
                table.Flag = AstsTableFlags.TfClearOnUpdate;
            }
            else if (numType == 4)
            {
                table.Flag = AstsTableFlags.TfOrderbook;
            }

            table.FieldsIn = GetTableFields(ptr, out ptr, sdvig, out sdvig,true);
            table.FieldsOut = GetTableFields(ptr, out ptr, sdvig, out sdvig,false);

            newPtr = ptr;
            newSdvig = sdvig;
            return table;
        }

        /// <summary>
        /// take table fields
        /// взять поля таблицы
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес сдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <param name="neadDefoltValue">wether need to read the default value/нужно ли считывать дефолтное значение</param>
        /// <returns>table fields/поля таблицы</returns>
        private unsafe List<AstsTableField> GetTableFields(int* ptr, out int* newPtr, int sdvig, out int newSdvig, bool neadDefoltValue)
        {
            
            int count = GetInt(ptr, out ptr, sdvig);

            List<AstsTableField> fields = new List<AstsTableField>();
            
            for (int i = 0; i < count; i++)
            {
                AstsTableField field = new AstsTableField();
                field.Name = GetString(ptr, out ptr, sdvig, out sdvig);
                
                field.Header = GetString(ptr, out ptr, sdvig, out sdvig);
                field.Description = GetString(ptr, out ptr, sdvig, out sdvig);
                
                field.Lenght = GetInt(ptr, out ptr, sdvig);

                int type = GetInt(ptr, out ptr, sdvig);

                if (type == 0)
                {
                     field.FieldType = AstsTableFieldType.FtChar;
                }
                else if (type == 1)
                {
                    field.FieldType = AstsTableFieldType.FtInteger;
                }
                else if (type == 2)
                {
                    field.FieldType = AstsTableFieldType.FtFixed;
                }
                else if (type == 3)
                {
                    field.FieldType = AstsTableFieldType.FtFloat;
                }
                else if (type == 4)
                {
                    field.FieldType = AstsTableFieldType.FtDate;
                }
                else if (type == 5)
                {
                    field.FieldType = AstsTableFieldType.FtTime;
                }
                else if (type == 6)
                {
                    field.FieldType = AstsTableFieldType.FtFloatPoint;
                }
                field.CountDecimal = GetInt(ptr, out ptr, sdvig);

                int atribute = GetInt(ptr, out ptr, sdvig);

                if (atribute == 1)
                {
                    field.FieldFlag = AstsTableFieldFlags.FfKey;
                }
                if (atribute == 2)
                {
                    field.FieldFlag = AstsTableFieldFlags.FfSecCode;
                }
                if (atribute == 4)
                {
                    field.FieldFlag = AstsTableFieldFlags.FfNotNull;
                }
                if (atribute == 8)
                {
                    field.FieldFlag = AstsTableFieldFlags.FfVarBlock;
                }
                field.TypeEnums = GetString(ptr, out ptr, sdvig, out sdvig);

                if (neadDefoltValue)
                {
                    field.DefoltValue = GetString(ptr, out ptr, sdvig, out sdvig);
                }
                fields.Add(field);
            }


            newPtr = ptr;
            newSdvig = sdvig;
            return fields;
        }

        /// <summary>
        /// take transactions from the memory in the message MTEStructureEx
        /// взять транзакции из памяти в сообщении MTEStructureEx
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес сдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <returns>types/типы</returns>
        private unsafe List<AstsTransaction> GetAllTrans(int* ptr, out int* newPtr, int sdvig, out int newSdvig)
        {
            int count = GetInt(ptr, out ptr, sdvig);

            List<AstsTransaction> transactions= new List<AstsTransaction>();

            for (int i = 0; i < count; i++)
            {
                transactions.Add(GetOneTrans(ptr, out ptr, sdvig, out  sdvig));
            }

            newPtr = ptr;
            newSdvig = sdvig;
            return transactions;
        }

        /// <summary>
        /// take one transaction from the memory in the message MTEStructureEx
        /// взять одну транзакцию из памяти в сообщении MTEStructureEx
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес сдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <returns>type/тип</returns>
        private unsafe AstsTransaction GetOneTrans(int* ptr, out int* newPtr, int sdvig, out int newSdvig)
        {
            AstsTransaction type = new AstsTransaction();

            type.Name = GetString(ptr, out ptr, sdvig, out sdvig);
            type.Header = GetString(ptr, out ptr, sdvig, out sdvig);
            type.Description = GetString(ptr, out ptr, sdvig, out sdvig);
            type.IndexInSystem = GetInt(ptr, out ptr, sdvig);

            type.Fields = GetTableFields(ptr, out ptr, sdvig, out sdvig,true);

            newPtr = ptr;
            newSdvig = sdvig;
            return type;
        }

        /// <summary>
        /// read 4 bytes int from memory
        /// считать из памяти целое 4 байт
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес сдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <returns>string from the memory/строка скачанная из памяти</returns>
        private unsafe int GetInt(int* ptr, out int* newPtr, int sdvig)
        {
            int[] stringLength = new int[1];
            Marshal.Copy(IntPtr.Add(new IntPtr(ptr), sdvig), stringLength, 0, 1);
            int length = stringLength[0];
            ptr += 1; // shift to the int length / сдвигаем на длинну длинны инта
            newPtr = ptr;

            return length;
        }

        /// <summary>
        /// read from memory an integer reading it from a byte field
        /// считать из памяти целое считав его из байтового поля
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес сдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <returns>string from the memory/строка скачанная из памяти</returns>
        private unsafe int GetIntFromByte(int* ptr, out int* newPtr, int sdvig, out int newSdvig)
        {
            byte[] stringLength = new byte[1];
            Marshal.Copy(IntPtr.Add(new IntPtr(ptr), sdvig), stringLength, 0, 1);
            int length = stringLength[0];
            sdvig += 1; // shift/ сдвигаем 

            while (sdvig >= 4)
            {
                sdvig -= 4;
                ptr += 1;
            }

            newSdvig = sdvig;
            newPtr = ptr;

            return length;
        }

        /// <summary>
        /// read string from memory
        /// считать из памяти строку
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес вдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <returns>string from memory/строка скачанная из памяти</returns>
        private unsafe string GetString(int* ptr, out int* newPtr, int sdvig, out int newSdvig)
        {
            int[] stringLength = new int[1];
            Marshal.Copy(IntPtr.Add(new IntPtr(ptr), sdvig), stringLength, 0, 1);

            int length = stringLength[0]; 

            ptr += 1; // shift to the int length / сдвигаем на длинну длинны инта

            string result = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptr), sdvig), stringLength[0]);

            while (length % 4 != 0)
            {
                length--;
                sdvig++;
            }

            while (sdvig >= 4)
            {
                sdvig -=4;
                ptr += 1;
            }

            ptr += length/4;

            newSdvig = sdvig;

            newPtr = ptr;

            return result;
        }

        /// <summary>
        /// read string from memory when we know int length
        /// считать из памяти строку когда мы знаем длинну интов
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес вдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <param name="length">length of string/длинна строки</param>
        /// <returns>string from memory/строка скачанная из памяти</returns>
        private unsafe string GetStringWhenWeKnownLength(int* ptr, out int* newPtr, int sdvig, out int newSdvig, int length)
        {
            string result;

            if (sdvig == 0)
            {
                result = Marshal.PtrToStringAnsi(new IntPtr(ptr), length);
            }
            else
            {
                result = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptr), sdvig), length);
            }

            while (length % 4 != 0)
            {
                length--;
                sdvig++;
            }

            while (sdvig >= 4)
            {
                sdvig -= 4;
                ptr += 1;
            }

            ptr += length / 4;

            newSdvig = sdvig;

            newPtr = ptr;

            return result;
        }

        // working with a table query for the first time/работа с запросом таблиц в первый раз

        // tables used by us, specification at link / используемые нами таблицы, спецификация по адресу: ftp://ftp.moex.com/pub/ClientsAPI/ASTS/Bridge_Interfaces/Currency/Currency26_Broker_Russian.htm#t0_36
        // ALL_TRADES - table with all trade. Without parameters / таблица всех сделок. Без параметров
        // EXT_ORDERBOOK - depth / стакан. parameters / Параметры: string secBoard, string secCode, int depth
        // TRDACC - number of client account / номер счёта клиента. Without parameters/ Без параметров
        // POSITIONS - client positions / позиции клиента. Without parameters/ Без параметров
        // BOARDS - available boards / доступные площадки. Without parameters /Без параметоров. Provides the data needed for the call GetSecurities/Предоставляет данные нужные для вызова GetSecurities
        // SECURITIES - Request securities by market/ Запросить бумаги по рынку. parameters/Параметры:  string marketId. string boardId
        // ORDERS - Request orders / Запросить ордера. Without parameters/Без параметров
        // TRADES - My trades / Мои трейды. Without parameters/ Без параметров

        /// <summary>
        /// request tables for the first time
        /// запросить таблицы в первый раз
        /// </summary>
        public unsafe void OpenTablesInFirstTime()
        {
            _tableMyTrade = new MyTradeTableConverter();
            _tableOrder = new OrderTableConverter();
            _tableSecurity = new SecurityTableConverter();
            _tablePortfolios = new PortfoliosTableConverter();
            _tableTrade = new TradesTableConverter();
            _tableBoards = new SecurityBoardsConverter();
            _marketDepthTable = new MarketDepthTableConverter();

            _marketDepthTable.MarketDepthEvent += _marketDepthTable_MarketDepthEvent;
            _tableMyTrade.MyTradeUpdateEvent += _tableMyTrade_MyTradeUpdateEvent;
            _tableOrder.OrderUpdateEvent += _tableOrder_OrderUpdateEvent;
            _tableSecurity.SecurityUpdateEvent += _tableSecurity_SecurityUpdateEvent;
            _tableSecurity.SecurityMoexUpdateEvent += _tableSecurity_SecurityMoexUpdateEvent;
            _tablePortfolios.PortfolioUpdateEvent += _tablePortfolios_PortfolioUpdateEvent;
            _tableTrade.TradeUpdateEvent += _tableTrade_TradeUpdateEvent;

            int* ptrOnTable;

// portfolios / портфели

            int result = MteOpenTableFirstTime(_procNum, "TRDACC", null, true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                _tablePortfolios.Descriptor= result;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "TRDACC");
            }
            else
            {
                SendErrorFromAsts(result);
            }

            result = MteOpenTableFirstTime(_procNum, "CLIENTCODES", null, true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                _tablePortfolios.Descriptor = result;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "CLIENTCODES");
            }
            else
            {
                SendErrorFromAsts(result);
            }

// money limits on portfolio / денежные лимиты по портфелю

            result = MteOpenTableFirstTime(_procNum, "POSITIONS", null, true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                _tablePortfolios.DescriptorPosition = result;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "POSITIONS");
            }
            else
            {
                SendErrorFromAsts(result);
            }

// securities / бумаги

            result = MteOpenTableFirstTime(_procNum, "BOARDS", null, true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                _tableBoards.Descriptor = result;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "BOARDS");
            }
            else
            {
                SendErrorFromAsts(result);
            }

            string messag = "FNDT" + "    ";//"TQBR";

            result = MteOpenTableFirstTime(_procNum, "SECURITIES", messag, true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                _tableSecurity.Descriptor = result;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "SECURITIES");

                _tablePortfolios.Securities = _tableSecurity.MySecurities;
            }
            else
            {
               // string res = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptrOnTable), 0), 400);
                SendErrorFromAsts(result);
                //break;
            }

            _tableOrder.Securities = _tableSecurity.MySecurities;
            _tableMyTrade.Securities = _tableSecurity.MySecurities;
            _tableTrade.Securities = _tableSecurity.MySecurities;

// securities limits / денежные лимиты по бумагам

            result = MteOpenTableFirstTime(_procNum, "RM_HOLD", null, true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                _tablePortfolios.DescriptorLimits = result;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "RM_HOLD");
            }
            else
            {
                SendErrorFromAsts(result);
            }

// table of all trades / таблица всех сделок

            result = MteOpenTableFirstTime(_procNum, "ALL_TRADES", null, true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                _tableTrade.Descriptor = result;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "ALL_TRADES");
            }
            else
            {
                SendErrorFromAsts(result);
            }

            result = MteOpenTableFirstTime(_procNum, "ORDERS", null, true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                _tableOrder.Descriptor = result;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "ORDERS");
            }
            else
            {
                SendErrorFromAsts(result);
            }

            result = MteOpenTableFirstTime(_procNum, "TRADES", null, true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                _tableMyTrade.Descriptor = result;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "TRADES");
            }
            else
            {
                SendErrorFromAsts(result);
            }

// from this table get the maximum and minimum value for securities today
// из этой таблицы качаем максимальное и минимально знаечение для бумаг на сегодня

            result = MteOpenTableFirstTime(_procNum, "ASSETS", "", true, out ptrOnTable);

            if (result >= 0)
            {
                int sdvig;
                ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvig, "ASSETS");
            }
            else
            {
                string res = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptrOnTable), 0), 400);
                SendErrorFromAsts(result);
                //break;
            }
        }

        /// <summary>
        /// download table from memory
        /// загрузить таблицу из памяти
        /// </summary>
        /// <param name="ptr">address of string begin/адрес начала строки</param>
        /// <param name="newPtr">OUT address is shifted to the length of the string/OUT адрес вдвинутый на длинну строки</param>
        /// <param name="sdvig">bit shift/побайтовый сдвиг</param>
        /// <param name="newSdvig">new bit shift/новый побайтовый сдвиг</param>
        /// <param name="nameTable">table name, if we know it/название таблицы, если мы её сразу знаем</param>
        /// <returns>did you get something to download/получилось ли что-то скачать то, в конце концов!</returns>
        private unsafe bool ReadTable(int* ptr, out int* newPtr, int sdvig, out int newSdvig, string nameTable)
        {
            AstsTable tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == nameTable);

           // string res = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptr), sdvig), 400);

            int refOnTable = GetInt(ptr, out ptr, sdvig);

            if (refOnTable > 15 && tableStruct == null)
            {
                refOnTable = _tablePortfolios.TablePortfolioPositionOnBoardUniq;
            }

            if(refOnTable == 0)
            {
                newSdvig = sdvig;
                newPtr = ptr;
                return false;
            }

            // ALL_TRADES - table with all trades/таблица всех сделок. Without parameters / Без параметров
            // EXT_ORDERBOOK - depth/стакан. parameters / Параметры: string secBoard, string secCode, int depth
            // TRDACC - number of client account / номер счёта клиента. Without parameters / Без параметров
            // POSITIONS - client positions / позиции клиента. Without parameters / Без параметров
            // BOARDS - available boards / доступные площадки. Without parameters / Без параметоров. Provides the data needed for the call GetSecurities/Предоставляет данные нужные для вызова GetSecurities
            // SECURITIES - Request securities by market/Запросить бумаги по рынку. parameters/Параметры:  string marketId. string boardId
            // ORDERS - Request orders /Запросить ордера. Without parameters/Без параметров
            // TRADES - My trades / Мои трейды. Without parameters/Без параметров

            if (tableStruct == null)
            {
                if (_tableTrade.TableUniqNum == refOnTable)
                {
                    tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == "ALL_TRADES");
                }
                else if (_tableMyTrade.TableUniqNum == refOnTable)
                {
                    tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == "TRADES");
                }
                else if (_tableOrder.TableUniqNum == refOnTable)
                {
                    tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == "ORDERS");
                }
                else if (_tableSecurity.TableUniqNum == refOnTable)
                {
                    tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == "SECURITIES");
                }
                else if (_tablePortfolios.TablePortfolioUniq == refOnTable)
                {
                    tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == "TRDACC");
                }
                else if (_tablePortfolios.TablePortfolioPositionOnBoardUniq == refOnTable)
                {
                    tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == "RM_HOLD");
                }
                else if (_tablePortfolios.TablePortfolioLimitsUniq == refOnTable)
                {
                    tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == "POSITIONS");
                }
                else if (_tableBoards.TableUniqNum == refOnTable)
                {
                    tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == "BOARDS");
                }
                else if (_marketDepthTable.TableUniqNum == refOnTable)
                {
                    tableStruct = _tablesStruct.Find(astsTable => astsTable.Name == "EXT_ORDERBOOK");
                }


            }

            if (tableStruct == null)
            {
                //string res = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptr), sdvig), 400);
                newSdvig = sdvig;
                newPtr = ptr;
                throw new Exception("не опознана таблица входящая из ASTS");
            }


            UniversalTable table = new UniversalTable(tableStruct.Name);

            int integ = GetInt(ptr, out ptr, sdvig);
            int countRow = 0;

            if (nameTable != "Unknown")
            {
                countRow = GetInt(ptr, out ptr, sdvig);
            }
            else
            {
                countRow = integ;
            }
            
            for (int i = 0; i < countRow; i++)
            {
                //string res = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptr), sdvig), 400);

                int fieldCount = GetIntFromByte(ptr, out ptr, sdvig,out sdvig);
                //string res2 = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptr), sdvig), 400);

                int dataLength = GetInt(ptr, out ptr, sdvig);

                //string res3 = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptr), sdvig), 400);

                List<AstsTableField> fields = new List<AstsTableField>();

                if (fieldCount == 0)
                {
                    fields = tableStruct.FieldsOut;
                }
                else
                {
                    for (int i2 = 0; i2 < fieldCount; i2++)
                    {
                        fields.Add(tableStruct.FieldsOut[GetIntFromByte(ptr,out ptr,sdvig,out sdvig)]);
                    }
                }

                UniversalRow row = new UniversalRow();

                for (int i2 = 0; i2 < fields.Count; i2++)
                {
                    AstsTableField field = fields[i2];

                    UniversalField fieldNew = new UniversalField();

                    fieldNew.Name = field.Name;
                    fieldNew.Value = GetStringWhenWeKnownLength(ptr, out ptr, sdvig, out sdvig, field.Lenght);
                    fieldNew.Decimals = field.CountDecimal;

                    row.Fields.Add(fieldNew);

                }
                if (nameTable == "Unknown")
                {
                
                }
                table.Rows.Add(row);
            }

            newSdvig = sdvig;
            newPtr = ptr;


            if (table.Name == "ALL_TRADES")
            {
               _tableTrade.LoadTables(table);
            }
            else if (table.Name == "TRADES")
            {
               _tableMyTrade.LoadTable(table);
            }
            else if (table.Name == "ORDERS")
            {
               _tableOrder.LoadTable(table);
            }
            else if (table.Name == "BOARDS")
            {
                _tableBoards.LoadTable(table);
            }
            else if (table.Name == "SECURITIES")
            {
              _tableSecurity.LoadTable(table);
            }
            else if (table.Name == "ASSETS")
            {
                _tableSecurity.LoadLimits(table);
            }
            else if (table.Name == "TRDACC")
            {
                _tablePortfolios.LoadPortfoliosAccount(table);
            }
            else if (table.Name == "CLIENTCODES")
            {
                _tablePortfolios.LoadPortfolioCodeClient(table);
            }
            else if (table.Name == "POSITIONS")
            {
                _tablePortfolios.LoadLimits(table);
            }
            else if (table.Name == "RM_HOLD")
            {
                _tablePortfolios.LoadPositionOnBoard(table);
            }
            else if (table.Name =="EXT_ORDERBOOK")
            {
                _marketDepthTable.LoadTable(table);
            }
            return true;
        }

// work with table updating       
// работа с обновлением таблиц

        /// <summary>
        /// contact server to send transactions and update table data
        /// обратиться к серверу для отправки транзакций и обновления данных в таблицах
        /// </summary>
        public unsafe void Process()
        {
// sending transaction block / блок отправки транзакций

            if (!_ordersToExecute.IsEmpty)
            {
                for (int i = 0; !_ordersToExecute.IsEmpty; i++)
                {
                    Order myOrder;
                    _ordersToExecute.TryDequeue(out myOrder);

                    if (myOrder == null)
                    {
                        continue;
                    }

                    StringBuilder str = GetStringToExecuteOrder(myOrder);
                    StringBuilder builder = new StringBuilder(256);

                    StringBuilder trans = new StringBuilder();
                    trans.Append("ORDER");

                    int result = MteExecTrans(_procNum, trans, str, builder);

                    if (result != 0)
                    {

                       // int integer=0;
                       // string res44 = Marshal.PtrToStringAnsi(IntPtr.Add(new IntPtr(ptr), integer), 400);
                       // string resEx = GetString(ptr, out ptr, 0, out integer);
                        SendLogMessage(builder.ToString(),LogMessageType.Error);
                        SendErrorFromAsts(result);
                        if (OrderFailedEvent != null)
                        {
                            OrderFailedEvent("ошибка на выставлении заявки", myOrder.NumberUser);
                        }
                    }
                }
            }

            if (!_ordersToCansel.IsEmpty)
            {
                for (int i = 0; !_ordersToCansel.IsEmpty; i++)
                {
                    Order myOrder;
                    _ordersToCansel.TryDequeue(out myOrder);

                    if (myOrder == null)
                    {
                        continue;
                    }

                    //AstsTransaction transactioin = _transactionStruct.Find(t => t.Name == "WD_ORDER_BY_NUMBER");

                    StringBuilder str = new StringBuilder();
                    str.Append(myOrder.NumberMarket);

                    for (int i2 = 0; str.Length < 12; i2++)
                    {
                        str = str.Insert(0, "0");
                    }

                    StringBuilder builder = new StringBuilder();

                    StringBuilder trans = new StringBuilder();
                    trans.Append("WD_ORDER_BY_NUMBER");

                    int result = MteExecTrans(_procNum, trans, str, builder);

                    if (result != 0)
                    {
                        if (OrderFailedEvent != null)
                        {
                            OrderFailedEvent("ошибка на отзыве заявки", myOrder.NumberUser);
                        }

                        SendErrorFromAsts(result);
                    }
                }
            }

// get depths for the first time
// подгружаем стаканы в первый раз

            for (int i = 0; _securitiesToMarketDepth != null && i < _securitiesToMarketDepth.Count; i++)
            {
                if (_marketDepthTable.Securities.Find(s => s.Name == _securitiesToMarketDepth[i].Name) == null)
                {
                    _marketDepthTable.Securities.Add(_securitiesToMarketDepth[i]);
                    int* ptrOnTable;

                    string par = "TQBR"; // _securitiesToMarketDepth[i].NameClass; // 

                    string name = _securitiesToMarketDepth[i].Name;

                    while (name.Length < 12)
                    {
                        name += " ";
                    }
                    par += name + "10";

                    int result = MteOpenTableFirstTime(_procNum, "EXT_ORDERBOOK", par, true, out ptrOnTable);

                    if (result >= 0)
                    {
                        _marketDepthTable.Descriptors.Add(result);
                        var sdvigToDepth = 0;
                        ReadTable(ptrOnTable, out ptrOnTable, 0, out sdvigToDepth, "EXT_ORDERBOOK");
                    }
                    else
                    {
                        SendErrorFromAsts(result);
                    }
                }
            }

// block of updating table
// блок обновления таблиц

            if (_marketDepthTable.Descriptors.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _marketDepthTable.Descriptors.Count; i++)
            {
                MteAddTableInQueueOnRefresh(_procNum, _marketDepthTable.Descriptors[i], _marketDepthTable.TableUniqNum);
            }

            MteAddTableInQueueOnRefresh(_procNum, _tableMyTrade.Descriptor, _tableMyTrade.TableUniqNum);

            MteAddTableInQueueOnRefresh(_procNum, _tableOrder.Descriptor, _tableOrder.TableUniqNum);

            MteAddTableInQueueOnRefresh(_procNum, _tableTrade.Descriptor, _tableTrade.TableUniqNum);

            //MteAddTableInQueueOnRefresh(_procNum, _tableSecurity.Descriptor, _tableSecurity.TableUniqNum);

            //MteAddTableInQueueOnRefresh(_procNum, _tablePortfolios.DescriptorPosition, _tablePortfolios.TablePortfolioLimitsUniq);

            //MteAddTableInQueueOnRefresh(_procNum, _tablePortfolios.DescriptorLimits, _tablePortfolios.TablePortfolioPositionOnBoardUniq);

            int* ptrRef;
            int sdvig = 0;

            int res = MteRefresh(_procNum, out ptrRef);

            if (res >= 0)
            {
                int count = GetInt(ptrRef, out ptrRef, sdvig);

                if (count > 5)
                {
                    count = GetInt(ptrRef, out ptrRef, sdvig);
                }

                for (int i = 0; i < count; i++)
                {
                    if (ReadTable(ptrRef, out ptrRef, sdvig, out sdvig, "Unknown") == false)
                    {
                        return;
                    }
                }
            }
            else if (res == -12)
            {
                return;
            }
            else
            {
                SendErrorFromAsts(res);
            }
        }

        /// <summary>
        /// take line from order to send the transaction
        /// взять из ордера строку для отправки транзакции
        /// </summary>
        /// <param name="order">входящий ордер</param>
        /// <returns>строка для исполнения </returns>
        private StringBuilder GetStringToExecuteOrder(Order order)
        {
            /*   Char
             * added on the right with spaces to the length specified in the field description. For example,/Дополняется справа пробелами до длины, указанной в описании поля. Например, 
             * for a Char(12) field, the string "ROOT" must be represented as " ROOT "/для поля типа Char(12) строка "ROOT" должна быть представлена как "ROOT "
             Integer
             On the left is added with zeros to the needed length. For example, a value of 127 of type Integer(10) is converted to the string "0000000127"/Дополняется слева нулями до нужной длины. Например, значение 127 с типом Integer(10) преобразуется в строку "0000000127"
             Fixed
             Left two characters after the decimal point, removed the decimal point/Оставляется два знака после десятичной точки, убирается десятичная точка, 
             * on the left is added with zeros to needed length. For example, a value of 927.4 with type Fixed(8) is converted to the string "00092740"/дополняется слева нулями до нужной длины. Например, значение 927,4 с типом Fixed(8) преобразуется в строку "00092740"
             Float
             Leaves N digits after the decimal point, removes the decimal point/Оставляется N знаков после десятичной точки, убирается десятичная точка, 
             * on the left is added with zeros to needed length. The value of N depends on the presentation format of prices for financial instrument,/дополняется слева нулями до нужной длины. Значение N зависит от формата представления цен для финансового инструмента,
             * to which this field belongs./к которому относится данное поле. 
             * For example, a value of 26.75 with type Float (9) for a tool with N = 4 is converted to the string "000267500"/Например, значение 26,75 с типом Float(9) для инструмента с N = 4 преобразуется в строку "000267500"
             Date
             Submitted in the YYYYMMDD format. For example, the value is August 24, 1999. converted to " 19990824"/Представляется в формате YYYYMMDD. Например значение 24 августа 1999г. преобразуется к "19990824"
             Time
             Submitted in the format/Представляется в формате*/

            Security mySecurity = _tableSecurity.MySecurities.Find(s => s.Name == order.SecurityNameCode);

            if (mySecurity == null)
            {
                SendLogMessage("Не найдена бумага для ордера. Ордер не выставлен.", LogMessageType.Error);
                return null;
            }

            AstsTransaction transactioin = _transactionStruct.Find(t => t.Name == "ORDER");

            StringBuilder result = new StringBuilder();

            for (int indexField = 0; indexField < transactioin.Fields.Count; indexField++)
            {
                AstsTableField field = transactioin.Fields[indexField];

                if (field.Name == "ACCOUNT")
                {
                    string acc = order.PortfolioNumber.Split('@')[0];

                    for (int i = 0; field.Lenght > acc.Length; i++)
                    {
                        acc += " ";
                    }
                    result.Append(acc);
                }
                else if (field.Name == "BUYSELL")
                { // 12th cell / 12 ячейка

                    if (order.Side == Side.Buy)
                    {
                        result.Append("B");
                    }
                    else if (order.Side == Side.Sell)
                    {
                        result.Append("S");
                    }
                }
                else if (field.Name == "MKTLIMIT")
                {
                    result.Append("L");
                }
                else if (field.Name == "SPLITFLAG")
                {
                    result.Append("S");
                }
                else if (field.Name == "IMMCANCEL")
                {
                    // AstsEnumType type = _typesStruct.Find(t => t.Name == "TImmCancel");
                    result.Append(" ");
                }
                else if (field.Name == "SECBOARD")
                {
                    string name = "TQBR"; //mySecurity.NameClass;

                    for (int i = 0; field.Lenght > name.Length; i++)
                    {
                        name += " ";
                    }
                    result.Append(name);
                }
                else if (field.Name == "SECCODE")
                {
                    string name = mySecurity.Name;

                    if (field.Lenght > name.Length)
                    {
                        name += new string(' ', field.Lenght - name.Length);
                    }
                    result.Append(name);
                }
                else if (field.Name == "PRICE")
                { // the first cell 35/первая ячейка 35

                    string price;
                    int countDecimals = 0;

                    price = order.Price.ToString().Replace(",", ".");

                    if (price.Split('.').Length > 1)
                    {
                        countDecimals = price.Split('.')[1].Length;
                        price = price.Replace(".", "");
                    }
                    else
                    {
                        price = price.ToString();
                    }

                    for (int i = 0; i < mySecurity.Decimals - countDecimals; i++)
                    {
                        price += "0";
                    }

                    if (9 > price.Length)
                    {
                        price = price.Insert(0, new string('0', 9 - price.Length));
                    }

                    result.Append(price);
                    // the last cell 43/последня ячейка 43
                }
                else if (field.Name == "QUANTITY")
                {
                    // the first cell 44/первая ячейка 44
                    string volume = order.Volume.ToString();

                    if (field.Lenght > volume.Length)
                    {
                        volume = volume.Insert(0, new string('0', field.Lenght - volume.Length));
                    }

                    result.Append(volume);
                    // the first cell 53/первая ячейка 53
                }
                else if (field.Name == "HIDDEN")
                {
                    string hide = "0";

                    if (field.Lenght > hide.Length)
                    {
                        hide = hide.Insert(0, new string('0', field.Lenght - hide.Length));
                    }

                    result.Append(hide);
                }
                else if (field.Name == "BROKERREF")
                {// the first cell 64/первая ячейка 64
                    string refB = "@" + order.NumberUser.ToString();

                    if (field.Lenght > refB.Length)
                    {
                        refB = refB.Insert(0, new string(' ', field.Lenght - refB.Length));
                    }

                    result.Append(refB);
                    // the last cell 83/последняя 83
                }
                else if (field.Name == "EXTREF")
                {
                    string code = "";

                    if (field.Lenght > code.Length)
                    {
                        code += new string(' ', field.Lenght - code.Length);
                    }

                    result.Append(code);
                }
                else if (field.Name == "CLIENTCODE")
                {
                    string code = ClientCode;

                    if (field.Lenght > code.Length)
                    {
                        code += new string(' ', field.Lenght - code.Length);
                    }

                    result.Append(code);
                }
                else if (field.Name == "PRICEYIELDENTERTYPE")
                {
                    //AstsEnumType type = _typesStruct.Find(t => t.Name == "TPriceEntryType");

                    string code = "P";

                    result.Append(code);
                }
                else if (field.Name == "MMORDER")
                {
                    //AstsEnumType type = _typesStruct.Find(t => t.Name == "TMMOrder");

                    string code = " ";

                    result.Append(code);
                }
                else if (field.Name == "ACTIVATIONTYPE")
                {
                    // AstsEnumType type = _typesStruct.Find(t => t.Name == "TOrderActivationType");

                    string code = " ";

                    result.Append(code);
                }
                else
                {
                    string code = "";

                    if (field.Lenght > code.Length)
                    {
                        code += new string(' ', field.Lenght - code.Length);
                    }

                    result.Append(code);
                }
            }

            return result;
        }

        /// <summary>
        /// new incoming trade came
        /// новый входящий трейд пришёл
        /// </summary>
        void _tableTrade_TradeUpdateEvent(List<Trade> trades)
        {
            if (NewTradesEvent != null)
            {
                NewTradesEvent(trades);
            }
        }

        /// <summary>
        /// updating portfolio
        /// обновление портфеля
        /// </summary>
        void _tablePortfolios_PortfolioUpdateEvent(Portfolio portfolio)
        {
            if (PortfolioUpdateEvent != null)
            {
                PortfolioUpdateEvent(portfolio);
            }
        }

        /// <summary>
        /// Level1 was changed in the security
        /// Level один изменился у бумаги
        /// </summary>
        void _tableSecurity_SecurityMoexUpdateEvent(SecurityLevelOne securityMoex)
        {
            if (SecurityMoexUpdateEvent != null)
            {
                SecurityMoexUpdateEvent(securityMoex);
            }
        }

        /// <summary>
        /// new security in the system
        /// новая бумага в системе
        /// </summary>
        void _tableSecurity_SecurityUpdateEvent(Security security)
        {
            if (NewSecurityEvent != null)
            {
                NewSecurityEvent(security);
            }
        }

        /// <summary>
        /// updated order
        /// обновился ордер
        /// </summary>
        void _tableOrder_OrderUpdateEvent(Order order)
        {
            if (OrderUpdateEvent != null)
            {
                OrderUpdateEvent(order);
            }
        }

        /// <summary>
        /// my new trade
        /// новая моя сделка
        /// </summary>
        void _tableMyTrade_MyTradeUpdateEvent(MyTrade myTrade)
        {
            if (NewMyTradeEvent != null)
            {
                NewMyTradeEvent(myTrade);
            }
        }

        /// <summary>
        /// new depth
        /// новый стакан
        /// </summary>
        void _marketDepthTable_MarketDepthEvent(MarketDepth marketDepth)
        {
            if (MarketDepthUpdateEvent != null)
            {
                MarketDepthUpdateEvent(marketDepth);
            }
        }

        /// <summary>
        /// securities subscribed to get the depth
        /// бумаги подписанные на получение стакана
        /// </summary>
        private List<Security> _securitiesToMarketDepth; 

        /// <summary>
        /// start listening depth on the instrument
        /// начать прослушивание стакана по инструменту
        /// </summary>
        public void ListenBidAsks(Security newSecurity)
        {
            if (_securitiesToMarketDepth == null)
            {
                _securitiesToMarketDepth = new List<Security>();
            }

            if (_securitiesToMarketDepth.Find(s => s.Name == newSecurity.Name) == null)
            {
                _securitiesToMarketDepth.Add(newSecurity);
            }
        }

        /// <summary>
        /// queue of orders to be placed in the system
        /// очередь ордеров для выставления в систему
        /// </summary>
        private ConcurrentQueue<Order> _ordersToExecute;

        /// <summary>
        /// queue of orders to be canceled in the system
        /// очередь ордеров для отмены в системе
        /// </summary>
        private ConcurrentQueue<Order> _ordersToCansel;

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            _ordersToExecute.Enqueue(order);
        }


        private List<Order> _canselledOrders; 
        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            if (_canselledOrders == null)
            {
                _canselledOrders = new List<Order>();
            }

            if (_canselledOrders.Find(o => o.NumberUser == order.NumberUser) != null)
            {
                return;
            }

            _canselledOrders.Add(order);
            _ordersToCansel.Enqueue(order);
        }

        /// <summary>
        /// connection is established
        /// соединение установлено
        /// </summary>
        public event Action ConnectedEvent;

        /// <summary>
        /// connection is lost
        /// соединение разорвано
        /// </summary>
        public event Action<string> DisconnectedEvent;

        /// <summary>
        /// downloaded new instrument
        /// подгружен новый инструмент
        /// </summary>
        public event Action<Security> NewSecurityEvent;

        /// <summary>
        /// updated portfolio
        /// обновился портфель
        /// </summary>
        public event Action<Portfolio> PortfolioUpdateEvent;

        /// <summary>
        /// new trades in the system
        /// новые трейды в системе
        /// </summary>
        public event Action<List<Trade>> NewTradesEvent;

        /// <summary>
        /// updated depth
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthUpdateEvent;

        /// <summary>
        /// my new trade
        /// новая Моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        /// <summary>
        /// updated order state in the system
        /// обновилось состояние ордера в системе
        /// </summary>
        public event Action<Order> OrderUpdateEvent;

        /// <summary>
        /// error when placing order
        /// ошибка при выставлении ордера
        /// </summary>
        public event Action<string, int> OrderFailedEvent;

        /// <summary>
        /// updated security in exchange format
        /// обновились бумаги в формате биржи
        /// </summary>
        public event Action<SecurityLevelOne> SecurityMoexUpdateEvent;

        // logging / логирование

        /// <summary>
        /// send a specification error to log
        /// выслать спецификацию ошибки в лог
        /// </summary>
        /// <param name="numberError">номер ошибки которую вернул Asts</param>
        public void SendErrorFromAsts(int numberError)
        {
            IntPtr ptr = MteErrorMsg(numberError);

            string fullError = Marshal.PtrToStringAnsi(ptr);

            SendLogMessage("Ошибка в неуправляемом коде. Спецификация ошибки за номером " + numberError + ": " + fullError, LogMessageType.System);
        }

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }

    // universal table with returning data
    // универсальная таблица с возвращающимися данными

    /// <summary>
    /// part of the data structure: table
    /// часть структуры данных: таблица
    /// </summary>
    public class UniversalTable
    {
        /// <summary>
        /// private constructor
        /// закрытый конструктор
        /// </summary>
        private UniversalTable()
        {
            
        }

        public UniversalTable(string tableName)
        {
            Name = tableName;

            Rows = new List<UniversalRow>();
        }

        /// <summary>
        /// table name
        /// название таблицы
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// rows
        /// строки
        /// </summary>
        public List<UniversalRow> Rows;
    }

    /// <summary>
    /// part of the data structure: row in table
    /// часть структуры данных: строка в таблице
    /// </summary>
    public class UniversalRow
    {
        public UniversalRow()
        {
            Fields = new List<UniversalField>();
        }

        /// <summary>
        /// fields
        /// поля строки
        /// </summary>
        public List<UniversalField> Fields;

        /// <summary>
        /// take the field by name and convert it to int
        /// взять поле по название и конвертировать его в int
        /// </summary>
        /// <param name="fieldName">field name/название поля которое нам нужно</param>
        /// <returns>returned value. If 0, then we may have an error/возвращаемое значение. Если 0, то вероятна ошибка</returns>
        public int GetAsInt32(string fieldName)
        {
            if (Fields == null)
            {
                return 0;
            }

            UniversalField field = Fields.Find(universalField => universalField.Name == fieldName);

            if (field == null)
            {
                return 0;
            }

            int result;

            try
            {
               result  = Convert.ToInt32(field.Value);
            }
            catch (Exception)
            {
                return 0;
            }

            return result;
        }

        /// <summary>
        /// take the field by name and convert it to int
        /// взять поле по название и конвертировать его в int
        /// </summary>
        /// <param name="fieldName">field name/название поля которое нам нужно</param>
        /// <returns>returned value. If 0, then we may have an error/возвращаемое значение. Если 0, то вероятна ошибка</returns>
        public long GetAsInt64(string fieldName)
        {
            if (Fields == null)
            {
                return 0;
            }

            UniversalField field = Fields.Find(universalField => universalField.Name == fieldName);

            if (field == null)
            {
                return 0;
            }

            long result;

            try
            {
                result = Convert.ToInt64(field.Value);
            }
            catch (Exception)
            {
                return 0;
            }

            return result;
        }

        /// <summary>
        /// take the field by name and convert it to string
        /// взять поле по название и конвертировать его в строку
        /// </summary>
        /// <param name="fieldName">field name/название поля которое нам нужно</param>
        /// <returns>returned value. If 0, then we may have an error/возвращаемое значение. Если null, то у нас ошибка</returns>
        public string GetAsString(string fieldName)
        {
            if (Fields == null)
            {
                return null;
            }

            UniversalField field = Fields.Find(universalField => universalField.Name == fieldName);

            if (field == null)
            {
                return null;
            }

           return field.Value;
        }

        /// <summary>
        /// take the field by name and convert it to a string
        /// взять поле по название и конвертировать его в строку
        /// </summary>
        /// <param name="fieldName">field name / название поля которое нам нужно</param>
        /// <param name="decimals">number of decimal places/количество знаков после зяпятой</param>
        /// <returns>returned value. If 0, then we may have an error/возвращаемое значение. Если 0, то у нас может быть ошибка</returns>
        public decimal GetAsDecimal(string fieldName, int decimals)
        {
            if (Fields == null)
            {
                return 0;
            }

            UniversalField field = Fields.Find(universalField => universalField.Name == fieldName);

            if (field == null ||
                string.IsNullOrWhiteSpace(field.Value))
            {
                return 0;
            }

            decimal result;

            try
            {
                if (decimals > 0)
                {
                    StringBuilder newValue = new StringBuilder();

                    for (int i = 0; i < field.Value.Length - decimals; i++)
                    {
                        newValue.Append(field.Value[i]);
                    }
                    newValue.Append(",");

                    for (int i = field.Value.Length - decimals; i < field.Value.Length; i++)
                    {
                        newValue.Append(field.Value[i]);
                    }
                    result = Convert.ToDecimal(newValue.ToString());
                }
                else
                {
                    result = Convert.ToDecimal(field.Value);
                }


            }
            catch (Exception)
            {
                return 0;
            }

            return result;
        }

        /// <summary>
        /// take the field by name and convert it to a string
        /// взять поле по название и конвертировать его в строку
        /// </summary>
        /// <param name="fieldName">field name / название поля которое нам нужно</param>
        /// <returns>returned value. If 0, then we may have an error/возвращаемое значение. Если 0, то у нас может быть ошибка</returns>
        public decimal GetAsDecimal(string fieldName)
        {
            if (Fields == null)
            {
                return 0;
            }

            UniversalField field = Fields.Find(universalField => universalField.Name == fieldName);

            if (field == null ||
                string.IsNullOrWhiteSpace(field.Value))
            {
                return 0;
            }

            decimal result;

            try
            {
                field.Value = field.Value.Replace(".", ",");
                result = Convert.ToDecimal(field.Value);
            }
            catch (Exception)
            {
                return 0;
            }

            return result;
        }

        /// <summary>
        /// take the field by name and convert it to DateTime
        /// взять поле по название и конвертировать его в DateTime
        /// </summary>
        /// <param name="fieldDate">name of field in which the date is stored/название поля в котором храниться дата</param>
        /// <param name="fieldTime">name of field in which the time is stored/название поля в котором храниться время</param>
        /// <returns>returned value. If 0, then we may have an error/возвращаемое значение. Если 0, то у нас может быть ошибка</returns>
        public DateTime GetAsDateTime(string fieldDate, string fieldTime)
        {
            if (Fields == null)
            {
                return DateTime.MinValue;
            }

            UniversalField fieldD = Fields.Find(universalField => universalField.Name == fieldDate);
            UniversalField fieldT = Fields.Find(universalField => universalField.Name == fieldTime);

            if (fieldD == null &&
                fieldT == null)
            {
                return DateTime.MinValue;
            }
            int hour = 0;
            int min = 0;
            int sec = 0;

            if (fieldT != null)
            {
                hour = Convert.ToInt32(fieldT.Value[0].ToString() + fieldT.Value[1].ToString());
                min = Convert.ToInt32(fieldT.Value[2].ToString() + fieldT.Value[3].ToString());
                sec = Convert.ToInt32(fieldT.Value[4].ToString() + fieldT.Value[5].ToString());
            }

            int day = 0;
            int month = 0;
            int year = 0;

            if (fieldD != null)
            {
                day = Convert.ToInt32(fieldD.Value[6].ToString() + fieldD.Value[7].ToString());
                month = Convert.ToInt32(fieldD.Value[4].ToString() + fieldD.Value[5].ToString());
                year = Convert.ToInt32(fieldD.Value[0].ToString() + fieldD.Value[1].ToString() + fieldD.Value[2].ToString() + fieldD.Value[3].ToString());
            }

            DateTime result = new DateTime(year, month, day, hour, min, sec);

            return result;
        }

    }

    /// <summary>
    /// part of the data structure: field for a row in the data table
    /// часть структуры данных: поле для строки в таблице данных
    /// </summary>
    public class UniversalField
    {
        /// <summary>
        /// field name
        /// имя поля
        /// </summary>
        public string Name;

        /// <summary>
        /// value
        /// значение
        /// </summary>
        public string Value;

        /// <summary>
        /// Unused field
        /// Не используемое поле
        /// </summary>
        public int Decimals;
    }

    // description of data structures: enumerations Asts Bridge
    // описание структур данных: Перечисления Asts Bridge

    /// <summary>
    /// enumeration Asts Bridge
    /// перечисление Asts Bridge
    /// </summary>
    public class AstsEnumType
    {
        /// <summary>
        /// name
        /// имя String
        /// </summary>
        public string Name;

        /// <summary>
        /// header
        /// заголовок String
        /// </summary>
        public string Header;

        /// <summary>
        /// description
        /// описание String
        /// </summary>
        public string Description;

        /// <summary>
        /// size
        /// размер int
        /// </summary>
        public int Lenght;

        /// <summary>
        /// type of type
        /// тип Типа. int
        /// </summary>
        public AstsEnumKind Type;

        /// <summary>
        /// variable constants
        /// константы переменной
        /// </summary>
        public List<AstsEnumConst> Consts;

    }

    /// <summary>
    /// constant for an enumeration AstsBridge
    /// константа для перечисления AstsBridge
    /// </summary>
    public class AstsEnumConst
    {
        /// <summary>
        /// value
        /// значение
        /// </summary>
        public string Value;

        /// <summary>
        /// long description
        /// длинная запись
        /// </summary>
        public string LongDescription;

        /// <summary>
        /// short description
        /// короткая запись
        /// </summary>
        public string ShorDescription;
    }

    /// <summary>
    /// preferred view
    /// предпочтительный вид представления
    /// </summary>
    public enum AstsEnumKind
    {
        /// <summary>
        /// НЁХ 0
        /// </summary>
        EkCheck = 0,

        /// <summary>
        /// group 1
        /// группа 1
        /// </summary>
        EkGroup = 1,

        /// <summary>
        /// type 2
        /// тип 2
        /// </summary>
        EkCombo = 2
    }

    // description of data structures: table Asts Bridge
    // описание структур данных: Таблицы Asts Bridge

    public class AstsTable
    {
        /// <summary>
        /// name
        /// имя
        /// </summary>
        public string Name;

        /// <summary>
        /// header
        /// заголовок
        /// </summary>
        public string Header;

        /// <summary>
        /// description
        /// описание String
        /// </summary>
        public string Description;

        /// <summary>
        /// index
        /// индекс
        /// </summary>
        public int IndexInSystem;

        /// <summary>
        /// way of updating table
        /// способ обновления таблицы
        /// </summary>
        public AstsTableFlags Flag;

        /// <summary>
        /// input fields
        /// поля для ввода
        /// </summary>
        public List<AstsTableField> FieldsIn;

        /// <summary>
        /// output fields
        /// поля для вывода
        /// </summary>
        public List<AstsTableField> FieldsOut;

    }

    /// <summary>
    /// sign of updatability table
    /// признак обновляемости таблицы
    /// </summary>
    public enum AstsTableFlags
    {
        /// <summary>
        /// 1 the table is updatable. It is possible to call functions MTEAddTable/MTERefresh;
        /// 1 таблица является обновляемой. Для нее можно вызывать функции MTEAddTable/MTERefresh;
        /// </summary>
        TfUpdateable = 1,

        /// <summary>
        /// 2 old contents of the table must be deleted each time an update is received
        /// 2 старое содержимое таблицы должно удаляться при получении каждого обновления 
        /// с помощью функций MTEAddTable/MTERefresh.
        /// </summary>
        TfClearOnUpdate = 2,

        /// <summary>
        /// 3 table has a quotation format and should be processed accordingly
        /// 3 таблица имеет формат котировок и должна обрабатываться соответсвующим образом
        /// </summary>
        TfOrderbook = 4,
    }

    /// <summary>
    /// table field
    /// поле таблицы
    /// </summary>
    public class AstsTableField
    {
        /// <summary>
        /// name
        /// имя
        /// </summary>
        public string Name;

        /// <summary>
        /// заголовок
        /// </summary>
        public string Header;

        /// <summary>
        /// description
        /// описание String
        /// </summary>
        public string Description;

        /// <summary>
        /// size
        /// размер
        /// </summary>
        public int Lenght;

        /// <summary>
        /// field type
        /// тип поля
        /// </summary>
        public AstsTableFieldType FieldType;

        /// <summary>
        /// number of decimal places
        /// кол-во знаков после запятой
        /// </summary>
        public int CountDecimal;

        /// <summary>
        /// flag
        /// флаг
        /// </summary>
        public AstsTableFieldFlags FieldFlag;

        /// <summary>
        /// enumerated type
        /// перечисляемый тип
        /// </summary>
        public string TypeEnums;

        /// <summary>
        /// default value
        /// значение по умолчанию
        /// </summary>
        public string DefoltValue;

    }

    /// <summary>
    /// field type
    /// тип поля
    /// </summary>
    public enum AstsTableFieldType
    {
        /// <summary>
        /// 0 Char
        /// </summary>
        FtChar = 0,

        /// <summary>
        /// 1 Int
        /// </summary>
        FtInteger = 1,

        /// <summary>
        /// 2 НЁХ
        /// </summary>
        FtFixed = 2,

        /// <summary>
        /// 3 Float
        /// </summary>
        FtFloat = 3,

        /// <summary>
        /// 4 - Date
        /// </summary>
        FtDate = 4,

        /// <summary>
        /// 5 Time
        /// </summary>
        FtTime = 5,

        /// <summary>
        /// 6 FloatPoint?? Double
        /// </summary>
        FtFloatPoint = 6,
    }

    /// <summary>
    /// flag of table fields
    /// флаг поля таблицы
    /// </summary>
    public enum AstsTableFieldFlags
    {
        /// <summary>
        /// 1
        /// </summary>
        FfKey = 1,

        /// <summary>
        /// 2
        /// </summary>
        FfSecCode = 2,

        /// <summary>
        /// 4
        /// </summary>
        FfNotNull = 4,

        /// <summary>
        /// 8
        /// </summary>
        FfVarBlock = 8
    }

    /// <summary>
    /// description of transaction
    /// описание транзакции
    /// </summary>
    public class AstsTransaction
    {
        /// <summary>
        /// name
        /// имя
        /// </summary>
        public string Name;

        /// <summary>
        /// header
        /// заголовок
        /// </summary>
        public string Header;

        /// <summary>
        /// description
        /// описание String
        /// </summary>
        public string Description;

        /// <summary>
        /// index
        /// индекс
        /// </summary>
        public int IndexInSystem;

        /// <summary>
        /// fields
        /// поля
        /// </summary>
        public List<AstsTableField> Fields;
    }

    // tables in which are formed familiar to the Os.Engine data
    // таблицы в которых формируются привычные для Os.Engine данные

    /// <summary>
    /// class-converter of order table
    /// класс конвертер таблицы ордеров 
    /// </summary>
    public class OrderTableConverter
    {
        /// <summary>
        /// descriptor of table assigned to a gateway
        /// дескриптор таблицы назначенный шлюзом
        /// </summary>
        public int Descriptor;

        /// <summary>
        /// unique table number assigned by us
        /// уникальный номер таблицы назначенный нами
        /// </summary>
        public int TableUniqNum = 1;

        /// <summary>
        /// orders
        /// ордера
        /// </summary>
        public List<Order> Orders;

        /// <summary>
        /// securities
        /// бумаги
        /// </summary>
        public List<Security> Securities; 

        /// <summary>
        /// parse table
        /// разобрать таблицу
        /// </summary>
        /// <param name="table"></param>
        public void LoadTable(UniversalTable table)
        {
            for (int i = 0; i < table.Rows.Count; i++)
            {
                Order order = new Order();

                order.NumberMarket = table.Rows[i].GetAsInt64("ORDERNO").ToString();

                DateTime time = table.Rows[i].GetAsDateTime("SETTLEDATE", "ORDERTIME");

                if (time == DateTime.MinValue)
                {
                    continue;
                }
                order.TimeCallBack = time;

                order.SecurityNameCode = table.Rows[i].GetAsString("SECCODE").Replace(" ", "");

                Security mySecurity = Securities.Find(s => s.Name == order.SecurityNameCode);

                if (mySecurity == null)
                {
                    continue;
                }

                order.Volume = table.Rows[i].GetAsInt32("QUANTITY");
                order.Price = table.Rows[i].GetAsDecimal("PRICE", mySecurity.Decimals);
                order.ServerType = ServerType.AstsBridge;


                string[] refB = table.Rows[i].GetAsString("BROKERREF").Split('@');

                if (refB.Length == 1)
                {
                    return;
                }
                try
                {
                    order.NumberUser = Convert.ToInt32(refB[refB.Length - 1]);
                }
                catch (Exception)
                {
                    throw new Exception(refB[refB.Length - 1]);
                }

                string buyS = table.Rows[i].GetAsString("BUYSELL");

                if (buyS == "S")
                {
                    order.Side = Side.Sell;
                }
                else
                {
                    order.Side = Side.Buy;
                }

                string state = table.Rows[i].GetAsString("STATUS");

                if (state == "O")
                {
                     order.State = OrderStateType.Activ;
                }
                else if (state == "M")
                {
                    order.State = OrderStateType.Done;
                }
                else if (state == "W")
                {
                    order.State = OrderStateType.Cancel;
                    order.TimeCancel = time;
                }
                else
                {
                    order.State = OrderStateType.Fail;
                }

                if (OrderUpdateEvent != null)
                {
                    OrderUpdateEvent(order);
                }

            }
        }

        /// <summary>
        /// outgoing event of updating order
        /// исходящее событие обновление ордера
        /// </summary>
        public event Action<Order> OrderUpdateEvent;
    }

    /// <summary>
    /// class-converter of my trades table
    /// класс конвертер таблицы моих сделок
    /// </summary>
    public class MyTradeTableConverter
    {
        /// <summary>
        /// descriptor tables assigned to a gateway
        /// дескриптор таблицы назначенный шлюзом
        /// </summary>
        public int Descriptor;

        /// <summary>
        /// unique table number assigned by us
        /// уникальный номер таблицы назначенный нами
        /// </summary>
        public int TableUniqNum = 2;

        /// <summary>
        /// loaded my trades
        /// загруженные мои сделки
        /// </summary>
        public List<MyTrade> MyTrades;

        /// <summary>
        /// securities
        /// бумаги
        /// </summary>
        public List<Security> Securities;

        /// <summary>
        /// parse table
        /// разобрать таблицу
        /// </summary>
        public void LoadTable(UniversalTable table)
        {
            for (int i = 0; i < table.Rows.Count; i++)
            {
                MyTrade trade = new MyTrade();

                trade.NumberTrade = table.Rows[i].GetAsInt64("TRADENO").ToString();

                DateTime time = table.Rows[i].GetAsDateTime("SETTLEDATE", "TRADETIME");

                if (time == DateTime.MinValue)
                {
                    continue;
                }
                trade.Time = time;

                trade.SecurityNameCode = table.Rows[i].GetAsString("SECCODE").Replace(" ", "");

                Security mySecurity = Securities.Find(s => s.Name == trade.SecurityNameCode);

                if (mySecurity == null)
                {
                    continue;
                }

                trade.Volume = table.Rows[i].GetAsInt32("QUANTITY");


                trade.Price = table.Rows[i].GetAsDecimal("PRICE",mySecurity.Decimals);
                trade.NumberOrderParent = table.Rows[i].GetAsInt64("ORDERNO").ToString();

                string buyS = table.Rows[i].GetAsString("BUYSELL");

                if (buyS == "S")
                {
                    trade.Side = Side.Sell;
                }
                else
                {
                    trade.Side = Side.Buy;
                }

                if (MyTradeUpdateEvent != null)
                {
                    MyTradeUpdateEvent(trade);
                }

            }
        }

        /// <summary>
        /// outgoing event of create my trade
        /// исходящее событие создания моей сделки
        /// </summary>
        public event Action<MyTrade> MyTradeUpdateEvent;
    }

    /// <summary>
    /// class-converter of all trades table
    /// класс конвертер таблицы всех сделок
    /// </summary>
    public class TradesTableConverter
    {
        /// <summary>
        /// descriptor of table assigned to a gateway
        /// дескриптор таблицы назначенный шлюзом
        /// </summary>
        public int Descriptor;

        /// <summary>
        /// unique table number assigned by us
        /// уникальный номер таблицы назначенный нами
        /// </summary>
        public int TableUniqNum = 3;

        /// <summary>
        /// securities
        /// бумаги
        /// </summary>
        public List<Security> Securities;

        /// <summary>
        /// parse incoming data
        /// разобрать входящие данные
        /// </summary>
        public void LoadTables(UniversalTable table)
        {
            List<Trade> newTrades = new List<Trade>();

            for (int i = 0; i < table.Rows.Count; i++)
            {
                Trade trade = new Trade();

                trade.Id = table.Rows[i].GetAsInt32("TRADENO").ToString();

                DateTime time = table.Rows[i].GetAsDateTime("SETTLEDATE", "TRADETIME");

                if (time == DateTime.MinValue)
                {
                    continue;
                }
                trade.Time = time;

                trade.SecurityNameCode = table.Rows[i].GetAsString("SECCODE").Replace(" ", "");

                Security mySecurity = Securities.Find(s => s.Name == trade.SecurityNameCode);

                if (mySecurity == null)
                {
                    continue;
                }
                trade.MicroSeconds = table.Rows[i].GetAsInt32("MICROSECONDS");
                trade.Volume = table.Rows[i].GetAsInt32("QUANTITY");
                trade.Price = table.Rows[i].GetAsDecimal("PRICE", mySecurity.Decimals);

                string buyS = table.Rows[i].GetAsString("BUYSELL");

                if (buyS == "S")
                {
                    trade.Side = Side.Sell;
                }
                else
                {
                    trade.Side = Side.Buy;
                }

                newTrades.Add(trade);
            }

            if (TradeUpdateEvent != null)
            {
                TradeUpdateEvent(newTrades);
            }
        }

        /// <summary>
        /// outgoing events of updating trades
        /// исходящее событие обновления сделок
        /// </summary>
        public event Action<List<Trade>> TradeUpdateEvent;
    }

    /// <summary>
    /// class-converter of table associated with updating the portfolio
    /// класс конвертер таблиц связанных с обновлением портфеля
    /// </summary>
    public class PortfoliosTableConverter
    {
        /// <summary>
        /// descriptor of table is assigned to gateway
        /// дескриптор таблицы назначенный шлюзом
        /// </summary>
        public int Descriptor;

        /// <summary>
        /// descriptor of positions table is assigned to gateway
        /// дескриптор таблицы позиций назначенный шлюзом
        /// </summary>
        public int DescriptorPosition;

        /// <summary>
        /// descriptor of positions table is assigned to gateway of table with limits
        /// дескриптор таблицы позиций назначенный шлюзом таблицы с лимитами
        /// </summary>
        public int DescriptorLimits;

        /// <summary>
        /// unique number of table with portfolios assigned to us
        /// уникальный номер таблицы с портфелями назначенный нами
        /// </summary>
        public int TablePortfolioUniq = 4;

        /// <summary>
        /// unique number of table with portfolio limits assigned to us POSITIONS
        /// уникальный номер таблицы с лимитами портфелей назначенный нами POSITIONS
        /// </summary>
        public int TablePortfolioLimitsUniq = 5;

        /// <summary>
        /// unique number of table with positions assigned to us RM_HOLDING
        /// уникальный номер таблицы с позициями по бумагам назначенный нами RM_HOLD
        /// </summary>
        public int TablePortfolioPositionOnBoardUniq = 6;

        /// <summary>
        /// portfolios
        /// портфели
        /// </summary>
        public List<Portfolio> Portfolios;

        /// <summary>
        /// securities 
        /// бумаги
        /// </summary>
        public List<Security> Securities;

        /// <summary>
        /// upload portfolios. table BANKUSE
        /// загрузить портфели. таблица BANKUSE
        /// </summary>
        public void LoadPortfoliosAccount(UniversalTable table)
        {
            if (table.Rows == null ||
                table.Rows.Count == 0)
            {
                return;
            }

            if (Portfolios == null)
            {
                Portfolios = new List<Portfolio>();
            }

            for (int i = 0; i < table.Rows.Count; i++)
            {

                string type = table.Rows[i].GetAsString("DESCRIPTION");

                if (type == null) //|| (type.Replace(" ", "") != "Собственныйсчет(обеспечение)"))
                {
                    continue;
                }

                string accauntInStr = table.Rows[i].GetAsString("TRDACCID").Replace(" ", "");

                if (string.IsNullOrWhiteSpace(accauntInStr))
                {
                    continue;
                }


                string firm = table.Rows[i].GetAsString("FIRMID").Replace(" ", "");

                if (string.IsNullOrWhiteSpace(accauntInStr) ||
                    string.IsNullOrWhiteSpace(firm))
                {
                    continue;
                }

                Portfolio myPortfolio = Portfolios.Find(portfolio => portfolio.Number == accauntInStr
                    || portfolio.Number.Split('@')[0] == accauntInStr);

                if (myPortfolio == null)
                {
                    myPortfolio = new Portfolio();
                    myPortfolio.Number = accauntInStr;
                    Portfolios.Add(myPortfolio);
                }
                else
                {
                    continue;
                }
                if (PortfolioUpdateEvent != null)
                {
                    PortfolioUpdateEvent(myPortfolio);
                }

                if (_codePortfolio == null)
                {
                    _codePortfolio = new List<string>();
                }


                _codePortfolio.Add(accauntInStr + "@" + firm);
            }
        }

        private List<string> _codePortfolio;

        /// <summary>
        /// portfolio to update the client code. table BANKACC
        /// обновить портфель кодом клиента. таблица BANKACC
        /// </summary>
        public void LoadPortfolioCodeClient(UniversalTable table)
        {
            for (int i = 0; i < table.Rows.Count; i++)
            {

                List<Portfolio> myPortfolio = Portfolios;

                if (myPortfolio.Count == 0)
                {
                    continue;
                }

                for (int i2 = 0; i2 < myPortfolio.Count; i2++)
                {
                    if (myPortfolio[i2].Number.Split('@').Length == 2)
                    {
                        continue;
                    }

                    string client = table.Rows[i].GetAsString("CLIENTCODE").Replace(" ", "");

                    myPortfolio[i2].Number = myPortfolio[i2].Number + "@" + client;
                }
            }
           
        }

        /// <summary>
        /// download the position for the money portfolio. table POSITIONS
        /// загрузить позиции по деньгам по портфелю. таблица POSITIONS
        /// </summary>
        public void LoadLimits(UniversalTable table)
        {
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string firm = table.Rows[i].GetAsString("FIRMID");

                if (string.IsNullOrWhiteSpace(firm))
                {
                    continue;
                }

                List<string> myC = _codePortfolio.FindAll(code => code.Split('@').Length == 2 && code.Split('@')[1] == firm);

                if(myC.Count == 0)
                {
                    continue;
                }

                for (int i2 = 0; i2 < myC.Count; i2++)
                {
                     myC[i2] = myC[i2].Split('@')[0];
                }

                List<Portfolio> myPortfolio = new List<Portfolio>();

                for (int i2 = 0; i2 < myC.Count; i2++)
                {
                    myPortfolio.AddRange(Portfolios.FindAll(p => p.Number.Split('@')[0] == myC[i2]));
                }


                if (myPortfolio.Count == 0)
                {
                    continue;
                }

                for (int i2 = 0; i2 < myPortfolio.Count; i2++)
                {
                    decimal openValue = table.Rows[i].GetAsDecimal("OPENBAL",0);
                    if (openValue != 0)
                    {
                        myPortfolio[i2].ValueBegin = openValue;
                    }

                    decimal currentValue = table.Rows[i].GetAsDecimal("CURRENTPOS",0);
                    if (currentValue != 0)
                    {
                        myPortfolio[i2].ValueCurrent = currentValue;
                    }
                    if (PortfolioUpdateEvent != null)
                    {
                        PortfolioUpdateEvent(myPortfolio[i2]);
                    }
                }


            }      
        }

        /// <summary>
        /// updated securities positions on the exchange/ table RM_HOLD
        /// обновились позиции по бумагам на бирже/ таблица RM_HOLD
        /// </summary>
        public void LoadPositionOnBoard(UniversalTable table)
        {
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string asset = table.Rows[i].GetAsString("ASSET");
                if (string.IsNullOrWhiteSpace(asset))
                {
                    continue;
                }

                asset = asset.Replace(" ","");

                Security mySecurity = Securities.Find(s => s.Name == asset);

                if (mySecurity == null)
                {
                    continue;
                }

                string firm = table.Rows[i].GetAsString("FIRMID");
                if (string.IsNullOrWhiteSpace(firm))
                {
                    continue;
                }

                string acc = table.Rows[i].GetAsString("ACCOUNT");
                if (string.IsNullOrWhiteSpace(acc))
                {
                    continue;
                }

                Portfolio portfolio = Portfolios.Find(p => p.Number.Split('@')[0].Split('-')[0] == acc.Split('+')[0]);

                if (portfolio == null)
                {
                    continue;
                }

                PositionOnBoard position = new PositionOnBoard();
                position.PortfolioName = portfolio.Number;
                position.SecurityNameCode = mySecurity.Name;

                DateTime time = table.Rows[i].GetAsDateTime("DATE", "");

               /* if (time.Date != DateTime.Now.Date)
                {
                    return;
                }*/

                decimal valCred = table.Rows[i].GetAsDecimal("CREDIT",2); //DEBITBALANCE // CREDITBALANCE //DATE

                decimal valDebt = table.Rows[i].GetAsDecimal("DEBIT", 2);

                if(valCred == 0 &&
                    valDebt ==0 )
                {
                    continue;
                }

                position.ValueCurrent = (valCred + valDebt)/mySecurity.Lot;
                portfolio.SetNewPosition(position);
                if (PortfolioUpdateEvent != null)
                {
                    PortfolioUpdateEvent(portfolio);
                }
            }
        }

        /// <summary>
        /// update portfolio message
        /// сообщение обновления портфеля
        /// </summary>
        public event Action<Portfolio> PortfolioUpdateEvent;

    }

    /// <summary>
    /// class-converter of tables assinged with update portfolio
    /// класс конвертер таблиц связанных с обновлением портфеля
    /// </summary>
    public class SecurityTableConverter
    {
        /// <summary>
        /// descriptor tables assigned to a gateway
        /// дескриптор таблицы назначенный шлюзом
        /// </summary>
        public int Descriptor;

        /// <summary>
        /// unique table number
        /// уникальный номер таблицы назначенный нами
        /// </summary>
        public int TableUniqNum = 7;

        /// <summary>
        /// securities as Level 1
        /// бумаги в виде Level один
        /// </summary>
        public List<SecurityLevelOne> MySecurityMoex;

        /// <summary>
        /// securities in Os.Engine format
        /// бумаги формата Os.Engine
        /// </summary>
        public List<Security> MySecurities;

        /// <summary>
        /// securities codes
        /// коды бумаг
        /// </summary>
        public List<string> MySecuritiesCodeToGetLimits;

        /// <summary>
        /// process incoming table of securities
        /// обработать входящую таблицу бумаг
        /// </summary>
        public void LoadTable(UniversalTable table)
        {
            if (table.Rows == null ||
                      table.Rows.Count == 0)
            {
                return;
            }

            if (MySecurities == null)
            {
                MySecurities = new List<Security>();
            }

            if (MySecuritiesCodeToGetLimits == null)
            {
                MySecuritiesCodeToGetLimits = new List<string>();
            }

            for (int i = 0; i < table.Rows.Count; i++)
            {
                string name = table.Rows[i].GetAsString("SECCODE");
                if (name == null)
                {
                    return;
                }
                name = name.Replace(" ", "");

                string marketCode = table.Rows[i].GetAsString("INSTRID");
                if (marketCode == null)
                {
                    continue;
                }
                marketCode = marketCode.Replace(" ", "");

                Security mySecurity = MySecurities.Find(s => s.Name == name && s.NameClass == marketCode);

                if (mySecurity == null)
                {
                    mySecurity = new Security();
                    mySecurity.Name = name;
                    mySecurity.NameClass = marketCode;
                    MySecurities.Add(mySecurity);

                    if (name == "AFLT")
                    {
                        
                    }

                    mySecurity.Decimals = table.Rows[i].GetAsInt32("DECIMALS");

                    string secName = table.Rows[i].GetAsString("ASSET");
                    if (!string.IsNullOrWhiteSpace(secName))
                        mySecurity.NameFull = secName.Replace(" ", "");

                    string codeName = table.Rows[i].GetAsString("SHORTNAME");
                    if (!string.IsNullOrWhiteSpace(codeName))
                        mySecurity.NameId = codeName.Replace(" ", "");

                    int lots = table.Rows[i].GetAsInt32("LOTSIZE");
                    if (lots != 0)
                        mySecurity.Lot = lots;

                    decimal step = table.Rows[i].GetAsDecimal("MINSTEP", mySecurity.Decimals);
                    if (step != 0)
                    {
                        mySecurity.PriceStep = step;
                        mySecurity.PriceStepCost = step;
                    }

                    if (SecurityUpdateEvent != null)
                    {
                        SecurityUpdateEvent(mySecurity);
                    }

                    string board = table.Rows[i].GetAsString("SECBOARD");
                    string code = table.Rows[i].GetAsString("SECCODE");
                    string scode = table.Rows[i].GetAsString("SETTLECODE");

                    if (!string.IsNullOrWhiteSpace(board) &&
                        !string.IsNullOrWhiteSpace(code) &&
                        !string.IsNullOrWhiteSpace(scode))
                    {
                        string newCode = board + code + scode;

                        if (MySecuritiesCodeToGetLimits.Find(s => s == newCode) == null)
                        {
                            MySecuritiesCodeToGetLimits.Add(newCode);
                        }
                    }
                }

// part two level 1 update
// часть два обновление Level 1

                if (MySecurityMoex == null)
                {
                    MySecurityMoex = new List<SecurityLevelOne>();
                }

                SecurityLevelOne mySecurityMoex = MySecurityMoex.Find(m => m.Security.Name == mySecurity.Name && m.Security.NameClass == mySecurity.NameClass);

                if (mySecurityMoex == null)
                {
                    mySecurityMoex = new SecurityLevelOne();
                    mySecurityMoex.Security = mySecurity;
                    MySecurityMoex.Add(mySecurityMoex);
                }

                string status = table.Rows[i].GetAsString("STATUS"); // STATUS Status A-operations allowed S-operations prohibited/Статус A - операции разрешены S - операции запрещены

                if (status== "S")
                {
                    continue;
                }

                int biddepth = table.Rows[i].GetAsInt32("BIDDEPTH"); //  BIDDEPTH Lots to buy at the best / Лотов на покупку по лучшей
                if (biddepth != 0)
                {
                    mySecurityMoex.Biddepth = biddepth;
                }

                int biddeptht = table.Rows[i].GetAsInt32("BIDDEPTHT"); // BIDDEPTHT	Aggregate demand / Совокупный спрос
                if (biddeptht != 0)
                {
                    mySecurityMoex.Biddeptht = biddeptht;
                }

               /* int numbids = table.Rows[i].GetAsInt("NUMBIDS"); //	NUMBIDS Заявок на покупку
                if (numbids != 0)
                {
                    mySecurityMoex.Numbids = numbids;
                }*/

                int offerdepth = table.Rows[i].GetAsInt32("OFFERDEPTH"); //	OFFERDEPTH Lots for sale at the best / Лотов на продажу по лучшей
                if (offerdepth != 0)
                {
                    mySecurityMoex.Offerdepth = offerdepth;
                }
                int offerdeptht = table.Rows[i].GetAsInt32("OFFERDEPTHT"); // OFFERDEPTHT	Aggregate supply / Совокупное предложение
                if (offerdeptht != 0)
                {
                    mySecurityMoex.Offerdeptht = offerdeptht;
                }

              /*  int numoffers = table.Rows[i].GetAsInt("NUMOFFERS"); // NUMOFFERS Заявок на продажу
                if (numoffers != 0)
                {
                    mySecurityMoex.Numoffers = numoffers;
                }*/

                decimal change = table.Rows[i].GetAsDecimal("CHANGE", mySecurity.Decimals);  //	CHANGE Change to close the previous day / Изменение к закрытию предыдущего дня
                if (change != 0)
                {
                    mySecurityMoex.Change = change;
                }
                int qty = table.Rows[i].GetAsInt32("QTY"); // QTY	volume in the last trade/Лотов в последней
                if (qty != 0)
                {
                    mySecurityMoex.Qty = qty;
                }

                decimal closeprice = table.Rows[i].GetAsDecimal("CLOSEPRICE", mySecurity.Decimals);  // CLOSEPRICE	Закрытие
                if (closeprice != 0)
                {
                    mySecurityMoex.Closeprice = closeprice;
                }

                DateTime dateTime = table.Rows[i].GetAsDateTime("SETTLEDATE", "TIME"); // 	last trade time / Время последней Поля: TIME SETTLEDATE
                if (dateTime != DateTime.MinValue)
                {
                    mySecurityMoex.DateTime = dateTime;
                }

                decimal highbid = table.Rows[i].GetAsDecimal("HIGHBID", mySecurity.Decimals);  // HIGHBID	best bid / Лучший спрос
                if (highbid != 0)
                {
                    mySecurityMoex.Highbid = highbid;
                }

                decimal lowoffer = table.Rows[i].GetAsDecimal("LOWOFFER", mySecurity.Decimals);  //	LOWOFFER best offer / Лучшее предложение
                if (lowoffer != 0)
                {
                    mySecurityMoex.Lowoffer = lowoffer;
                }

                int numtrades = table.Rows[i].GetAsInt32("NUMTRADES");// NUMTRADES	Trades for today / Сделок за сегодня
                if (numtrades != 0)
                {
                    mySecurityMoex.Numtrades = numtrades;
                }

                if (SecurityMoexUpdateEvent != null)
                {
                    SecurityMoexUpdateEvent(mySecurityMoex);
                }
            }
        }

        /// <summary>
        /// upload limits to instruments
        /// подгрузить к инструментам лимиты
        /// </summary>
        /// <param name="table"></param>
        public void LoadLimits(UniversalTable table)
        {
            if (MySecurities == null ||
                           MySecurities.Count == 0)
            {
                return;
            }
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string secName = table.Rows[i].GetAsString("ASSET");
                if (string.IsNullOrWhiteSpace(secName))
                {
                    continue;
                }

                Security mySecurity = MySecurities.Find(s => s.Name == secName.Replace(" ", ""));

                if (mySecurity == null)
                {
                    continue;
                }

                decimal upLimit = table.Rows[i].GetAsDecimal("RTH_RUB");

                if (upLimit != 0)
                {
                    mySecurity.PriceLimitHigh = upLimit;
                }
                decimal downLimit = table.Rows[i].GetAsDecimal("RTL_RUB");

                if (upLimit != 0)
                {
                    mySecurity.PriceLimitLow = downLimit;
                }
            }
        }

        /// <summary>
        /// updated instruments
        /// обновились инструменты
        /// </summary>
        public event Action<Security> SecurityUpdateEvent;

        /// <summary>
        /// updated instruments in format Level 1
        /// обновились инструменты в формате Level 1
        /// </summary>
        public event Action<SecurityLevelOne> SecurityMoexUpdateEvent;
    }

    /// <summary>
    /// class-converter of tables associated with sites for sale
    /// класс конвертер таблиц связанных с площадками для торговли
    /// </summary>
    public class SecurityBoardsConverter
    {
        /// <summary>
        /// descriptor tables assigned to a gateway
        /// дескриптор таблицы назначенный шлюзом
        /// </summary>
        public int Descriptor;

        /// <summary>
        /// unique table number
        /// уникальный номер таблицы назначенный нами
        /// </summary>
        public int TableUniqNum = 6;

        /// <summary>
        /// places
        /// площадки
        /// </summary>
        public List<AstsBoards> Bords;

        /// <summary>
        /// process table with places
        /// обработать таблицу с площадками
        /// </summary>
        public void LoadTable(UniversalTable  table)
        {
            if (Bords == null)
            {
                 Bords = new List<AstsBoards>();
            }
           
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string boardId = table.Rows[i].GetAsString("BOARDID");

                AstsBoards myBoard = Bords.Find(b => b.BoardId == boardId);

                if (myBoard == null)
                {
                    myBoard = new AstsBoards();
                    myBoard.BoardId = boardId;
                }

                string boardName = table.Rows[i].GetAsString("BOARDNAME");

                if (!string.IsNullOrWhiteSpace(boardName))
                {
                    myBoard.BoardName = boardName;
                }

                string marketId = table.Rows[i].GetAsString("MARKETID");

                if (!string.IsNullOrWhiteSpace(marketId))
                {
                    myBoard.MarketId = marketId;
                }
                Bords.Add(myBoard);
            }
        }
    }

    /// <summary>
    /// places of ASTS
    /// площадки ASTS
    /// </summary>
    public class AstsBoards
    {
        /// <summary>
        /// unique place number
        /// уникальный номер площадки
        /// </summary>
        public string BoardId;

        /// <summary>
        /// name
        /// имя
        /// </summary>
        public string BoardName;

        /// <summary>
        /// unique market number
        /// уникальный номер рынка
        /// </summary>
        public string MarketId;
    }

    /// <summary>
    /// class-converter of depth table
    /// класс конвертер таблиц стаканов
    /// </summary>
    public class MarketDepthTableConverter
    {
        public MarketDepthTableConverter()
        {
            Descriptors = new List<int>();
            Securities = new List<Security>();
        }

        /// <summary>
        /// descriptor of tables assigned to a gateway 
        /// дескриптор таблицы назначенный шлюзом
        /// </summary>
        public List<int> Descriptors;

        /// <summary>
        /// subscribed securities to update depth
        /// бумаги подписанные на обновление стакана
        /// </summary>
        public List<Security> Securities;

        /// <summary>
        /// unique table number
        /// уникальный номер таблицы назначенный нами
        /// </summary>
        public int TableUniqNum = 8;

        /// <summary>
        /// depths
        /// стаканы
        /// </summary>
        public List<MarketDepth> MarketDepths;

        /// <summary>
        /// load a row from the instrument into the table
        /// загрузить строку с инструментов в таблицу
        /// </summary>
        public void LoadTable(UniversalTable table)
        {
            MarketDepths = new List<MarketDepth>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string securityName = table.Rows[i].GetAsString("SECCODE");

                if (securityName == null)
                {
                    continue;
                }
                securityName = securityName.Replace(" ", "");

                Security mySecurity = Securities.Find(s => s.Name == securityName);

                if (mySecurity == null)
                {
                    continue;
                }

                MarketDepth myDepth = MarketDepths.Find(m => m.SecurityNameCode == securityName);

                if (myDepth == null)
                {
                    myDepth = new MarketDepth();
                    myDepth.SecurityNameCode = securityName;
                    MarketDepths.Add(myDepth);
                }

                string buySell = table.Rows[i].GetAsString("BUYSELL");

                MarketDepthLevel level = new MarketDepthLevel();
                level.Price = table.Rows[i].GetAsDecimal("PRICE", mySecurity.Decimals);
                
                if(buySell == "B")
                {
                    level.Bid = table.Rows[i].GetAsInt32("QUANTITY");
                    bool insert = false;
                    for (int i2 = 0; i2 < myDepth.Bids.Count; i2++)
                    {
                        if (myDepth.Bids[i2].Price < level.Price)
                        {
                            myDepth.Bids.Insert(i2, level);
                            insert = true;
                            break;
                        }
                    }
                    if (insert == false)
                    {
                        myDepth.Bids.Add(level);
                    }
                }
                else if(buySell == "S")
                {
                    level.Ask = table.Rows[i].GetAsInt32("QUANTITY");
                    bool insert = false;
                    for (int i2 = 0; i2 < myDepth.Asks.Count; i2++)
                    {
                        if (myDepth.Asks[i2].Price > level.Price)
                        {
                            myDepth.Asks.Insert(i2, level);
                            insert = true;
                            break;
                        }
                    }
                    if (insert == false)
                    {
                        myDepth.Asks.Add(level);
                    }
                }
            }

            for (int i = 0; i < MarketDepths.Count; i++)
            {
                if (MarketDepthEvent != null)
                {
                    MarketDepthEvent(MarketDepths[i]);
                }
            }
        }

        /// <summary>
        /// depth update event
        /// событие обновления стаканов
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;
    }
}
