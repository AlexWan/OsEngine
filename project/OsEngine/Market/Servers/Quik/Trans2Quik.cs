/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OsEngine.Market.Servers.Quik
{
    internal static class Trans2Quik
    {
        internal enum QuikResult
        {
            UNKNOWN                     = -1,   // The result of execution is unknown or function has not been performed/Результат выполнения неизвестен или функция не выполнялась
            SUCCESS                     = 0,    // Successful execution of the function/Успешное выолнение функции
            FAILED                      = 1,    // The execution of the function failed/Выполнении функции закончилось неудачей
            QUIK_TERMINAL_NOT_FOUND     = 2,    // Either there is no INFO in the specified directory.EXE, or it does not run the service for processing external connections/В указанном каталоге либо отсутствует INFO.EXE, либо у него не запущен сервис обработки внешних подключений
            DLL_VERSION_NOT_SUPPORTED   = 3,    // The version of the TRANS2QUIK library used.DLL specified INFO.EXE is not supported/Используемая версия библиотеки TRANS2QUIK.DLL указанным INFO.EXE не поддерживается
            ALREADY_CONNECTED_TO_QUIK   = 4,    // The connection has been established yet/Cоединение уже установлено
            WRONG_SYNTAX                = 5,    // Transaction line is filled incorrectly/Строка транзакции заполнена неверно
            QUIK_NOT_CONNECTED          = 6,    // No connection terminal QUIK server/Не установлена связь терминала QUIK с сервером 
            DLL_NOT_CONNECTED           = 7,    // No connection the library TRANS2QUIK.DLL with the QUIK terminal/Не установлена связь библиотеки TRANS2QUIK.DLL с терминалом QUIK
            QUIK_CONNECTED              = 8,    // QUIK terminal connection to the server is established/Соединение терминала QUIK с сервером установлено
            QUIK_DISCONNECTED           = 9,    // QUIK terminal connection to the server is lost/Соединение терминала QUIK с сервером разорвано
            DLL_CONNECTED               = 10,   // Trans2quik library connection.DLL with QUIK terminal established/Соединение библиотеки TRANS2QUIK.DLL с терминалом QUIK установлено
            DLL_DISCONNECTED            = 11,   // Trans2quik library connection.DLL with QUIK terminal lost/Соединение библиотеки TRANS2QUIK.DLL с терминалом QUIK разорвано
            MEMORY_ALLOCATION_ERROR     = 12,   // Memory allocation error/Ошибка распределения памяти
            WRONG_CONNECTION_HANDLE     = 13,   // Processing connection error/Ошибка при обработке соединения
            WRONG_INPUT_PARAMS          = 14    // Error incoming function parameters/Ошибочные входные параметры функции
        }

        // RESULT TRANS2QUIK_SUCCESS, TRANS2QUIK_QUIK_TERMINAL_NOT_FOUND, TRANS2QUIK_DLL_VERSION_NOT_SUPPORTED,
        // TRANS2QUIK_ALREADY_CONNECTED_TO_QUIK, TRANS2QUIK_FAILED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_CONNECT", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult CONNECT(
            [MarshalAs(UnmanagedType.LPStr)] string lpstConnectionParamsString,
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        // RESULT TRANS2QUIK_SUCCESS, TRANS2QUIK_FAILED, TRANS2QUIK_DLL_NOT_CONNECTED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_DISCONNECT", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult DISCONNECT(
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        // RESULT TRANS2QUIK_DLL_CONNECTED, TRANS2QUIK_DLL_NOT_CONNECTED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_IS_DLL_CONNECTED", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult IS_DLL_CONNECTED(
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        // RESULT TRANS2QUIK_QUIK_CONNECTED, TRANS2QUIK_QUIK_NOT_CONNECTED, TRANS2QUIK_DLL_NOT_CONNECTED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_IS_QUIK_CONNECTED", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult IS_QUIK_CONNECTED(
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        // RESULT TRANS2QUIK_SUCCESS, TRANS2QUIK_WRONG_SYNTAX, TRANS2QUIK_DLL_NOT_CONNECTED, TRANS2QUIK_QUIK_NOT_CONNECTED, TRANS2QUIK_FAILED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_SEND_ASYNC_TRANSACTION", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult SEND_ASYNC_TRANSACTION(
            [MarshalAs(UnmanagedType.LPStr)] string lpstTransactionString,
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstErrorMessage,
            int dwErrorMessageSize);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_SUBSCRIBE_ORDERS", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult SUBSCRIBE_ORDERS(
            String classCode,
            String secCodes);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_UNSUBSCRIBE_ORDERS", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult UNSUBSCRIBE_ORDERS();

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_SUBSCRIBE_TRADES", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult SUBSCRIBE_TRADES(
            String classCode,
            String secCodes);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_UNSUBSCRIBE_TRADES", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult UNSUBSCRIBE_TRADES();

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_TRADE_ACCOUNT", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TRADE_ACCOUNT(int nTradeDescriptor);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_TRADE_DATE", CallingConvention = CallingConvention.StdCall)]
        public static extern int TRADE_DATE(int nTradeDescriptor);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_TRADE_TIME", CallingConvention = CallingConvention.StdCall)]
        public static extern int TRADE_TIME(int nTradeDescriptor);

        public static string GetTradeAccount(Int32 descriptor)
        {
            return Marshal.PtrToStringAnsi(TRADE_ACCOUNT(descriptor));
        }

        private delegate void CONNECTION_STATUS_CALLBACK_UNMGR(
            QuikResult nConnectionEvent,
            int nExtendedErrorCode,
            IntPtr lpcstrInfoMessage);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_SET_CONNECTION_STATUS_CALLBACK", CallingConvention = CallingConvention.StdCall)]
        private static extern QuikResult SET_CONNECTION_STATUS_CALLBACK_UNMGR(
            CONNECTION_STATUS_CALLBACK_UNMGR pfConnectionStatusCallback,
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        public delegate void CONNECTION_STATUS_CALLBACK(
            QuikResult nConnectionEvent,
            int nExtendedErrorCode,
            string lpcstrInfoMessage);

        public static QuikResult SET_CONNECTION_STATUS_CALLBACK(
            CONNECTION_STATUS_CALLBACK pfConnectionStatusCallback,
            out int pnExtendedErrorCode,
            StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize)
        {
            connection_status_callback = pfConnectionStatusCallback;
            return SET_CONNECTION_STATUS_CALLBACK_UNMGR(
               connection_status_callback_unmgr,
               out pnExtendedErrorCode,
               lpstrErrorMessage,
               lpstrErrorMessage.Capacity);
        }

        private delegate void TRANSACTION_REPLY_CALLBACK_UNMGR(
            Int32 nTransactionResult,
            Int32 nTransactionExtendedErrorCode,
            Int32 nTransactionReplyCode,
            UInt32 dwTransId,
            UInt64 dOrderNum,
            [MarshalAs(UnmanagedType.LPStr)] string TransactionReplyMessage,
            IntPtr pTransReplyDescriptor);


        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_SET_TRANSACTIONS_REPLY_CALLBACK", CallingConvention = CallingConvention.StdCall)]
        private static extern QuikResult SET_TRANSACTIONS_REPLY_CALLBACK_UNMGR(
            TRANSACTION_REPLY_CALLBACK_UNMGR pfTransactionReplyCallback,
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        public delegate void TRANSACTION_REPLY_CALLBACK(
            Int32 nTransactionResult,
            Int32 nTransactionExtendedErrorCode,
            Int32 nTransactionReplyCode,
            UInt32 dwTransId,
            UInt64 dOrderNum,
            [MarshalAs(UnmanagedType.LPStr)] string TransactionReplyMessage,
            IntPtr pTransReplyDescriptor);

        public static QuikResult SET_TRANSACTIONS_REPLY_CALLBACK(
            TRANSACTION_REPLY_CALLBACK pfTransactionReplyCallback,
            out int pnExtendedErrorCode,
            StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize)
        {
            transaction_reply_callback = pfTransactionReplyCallback;
            return SET_TRANSACTIONS_REPLY_CALLBACK_UNMGR(
                transaction_reply_callback_unmgr,
                out pnExtendedErrorCode,
                lpstrErrorMessage,
                lpstrErrorMessage.Capacity);
        }

        private delegate void ORDER_STATUS_CALLBACK_UNMGR(
            int nMode,
            int dwTransID,
            ulong @ulong,
            IntPtr ClassCode,
            IntPtr SecCode,
            double dPrice,
            int nBalance,
            double dValue,
            int nIsSell,
            int nStatus,
            IntPtr intPtr);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_START_ORDERS", CallingConvention = CallingConvention.StdCall)]
        private static extern QuikResult START_ORDERS_UNMGR(
            ORDER_STATUS_CALLBACK_UNMGR pfnOrderStatusCallback);

        public delegate void ORDER_STATUS_CALLBACK(
                Int32 nMode,
                Int32 dwTransID,
                UInt64 nOrderNum,
                [MarshalAs(UnmanagedType.LPStr)]string ClassCode,
                [MarshalAs(UnmanagedType.LPStr)]string SecCode,
                double dPrice,
                Int64 nBalance,
                Double dValue,
                Int32 nIsSell,
                Int32 nStatus,
                IntPtr pOrderDescriptor);

        public static QuikResult START_ORDERS(
            ORDER_STATUS_CALLBACK pfnOrderStatusCallback)
        {
            order_status_callback = pfnOrderStatusCallback;
            return START_ORDERS_UNMGR(order_status_callback_unmgr);
        }

        private delegate void TRADE_STATUS_CALLBACK_UNMGR(
                Int32 nMode,
                ulong dNumber,
                UInt64 nOrderNum,
                [MarshalAs(UnmanagedType.LPStr)]string ClassCode,
                [MarshalAs(UnmanagedType.LPStr)]string SecCode,
                double dPrice,
                Int64 nBalance,
                Double dValue,
                Int32 nIsSell,
                IntPtr nTradeDescriptor);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "TRANS2QUIK_START_TRADES", CallingConvention = CallingConvention.StdCall)]
        private static extern QuikResult START_TRADES_UNMGR(
            TRADE_STATUS_CALLBACK_UNMGR pfnTradeStatusCallback);

        public delegate void TRADE_STATUS_CALLBACK(
                Int32 nMode,
                UInt64 nNumber,
                UInt64 nOrderNumber,
                [MarshalAs(UnmanagedType.LPStr)]string ClassCode,
                [MarshalAs(UnmanagedType.LPStr)]string SecCode,
                Double dPrice,
                Int64 nQty,
                Double dValue,
                Int32 nIsSell,
                IntPtr pTradeDescriptor);


        public static QuikResult START_TRADES(
            TRADE_STATUS_CALLBACK pfnTradeStatusCallback)
        {
            trade_status_callback = pfnTradeStatusCallback;
            return START_TRADES_UNMGR(trade_status_callback_unmgr);
        }

        private static void connection_status_callback_impl(
            QuikResult nConnectionEvent,
            int nExtendedErrorCode,
            IntPtr lpcstrInfoMessage)
        {
            if (connection_status_callback != null)
            {
                connection_status_callback(
                    nConnectionEvent,
                    nExtendedErrorCode,
                    Marshal.PtrToStringAnsi(lpcstrInfoMessage));
            }
        }

        private static void transaction_reply_callback_impl(
            int nTransactionResult,
            int nTransactionExtendedErrorCode,
            int nTransactionReplyCode,
            uint dwTransId,
            ulong dOrderNum,
            string transactionReplyMessage,
            IntPtr pTransReplyDescriptor)
        {
            if (transaction_reply_callback != null)
            {
                transaction_reply_callback(
                    nTransactionResult,
                    nTransactionExtendedErrorCode,
                    nTransactionReplyCode,
                    dwTransId,
                    dOrderNum,
                    transactionReplyMessage,
                    pTransReplyDescriptor);
            }
        }

        private static void order_status_callback_impl(
            int nMode,
            int dwTransID,
            ulong d,
            IntPtr ClassCode,
            IntPtr SecCode,
            double dPrice,
            int nBalance,
            double dValue,
            int nIsSell,
            int nStatus,
            IntPtr i)
        {
            if (order_status_callback != null)
            {
                order_status_callback(
                    nMode,
                    dwTransID,
                    d,
                    Marshal.PtrToStringAnsi(ClassCode),
                    Marshal.PtrToStringAnsi(SecCode),
                    dPrice,
                    nBalance,
                    dValue,
                    nIsSell,
                    nStatus,
                    i);
            }
        }

        private static void trade_status_callback_impl(
            int nMode,
            ulong dNumber,
            ulong dOrderNumber,
            string classCode,
            string secCode,
            double dPrice,
            long nQty,
            double dValue,
            int nIsSell,
            IntPtr nTradeDescriptor)
        {
            if (trade_status_callback != null)
            {
                trade_status_callback(
                    nMode,
                    dNumber,
                    dOrderNumber,
                    classCode,
                    secCode,
                    dPrice,
                    nQty,
                    dValue,
                    nIsSell,
                    nTradeDescriptor);
            }
        }

        private static CONNECTION_STATUS_CALLBACK connection_status_callback;
        private static TRANSACTION_REPLY_CALLBACK transaction_reply_callback;
        private static ORDER_STATUS_CALLBACK order_status_callback;
        private static TRADE_STATUS_CALLBACK trade_status_callback;

        private static CONNECTION_STATUS_CALLBACK_UNMGR connection_status_callback_unmgr;
        private static TRANSACTION_REPLY_CALLBACK_UNMGR transaction_reply_callback_unmgr;
        private static ORDER_STATUS_CALLBACK_UNMGR order_status_callback_unmgr;
        private static TRADE_STATUS_CALLBACK_UNMGR trade_status_callback_unmgr;

        private static GCHandle gc_connection_status;
        private static GCHandle gc_transaction_reply;
        private static GCHandle gc_order_status;
        private static GCHandle gc_trade_status;

        static Trans2Quik()
        {
            connection_status_callback_unmgr = new CONNECTION_STATUS_CALLBACK_UNMGR(connection_status_callback_impl);
            transaction_reply_callback_unmgr = new TRANSACTION_REPLY_CALLBACK_UNMGR(transaction_reply_callback_impl);
            order_status_callback_unmgr = new ORDER_STATUS_CALLBACK_UNMGR(order_status_callback_impl);
            trade_status_callback_unmgr = new TRADE_STATUS_CALLBACK_UNMGR(trade_status_callback_impl);

            gc_connection_status = GCHandle.Alloc(connection_status_callback_unmgr);
            gc_transaction_reply = GCHandle.Alloc(transaction_reply_callback_unmgr);
            gc_order_status = GCHandle.Alloc(order_status_callback_unmgr);
            gc_trade_status = GCHandle.Alloc(trade_status_callback_unmgr);
        }
    }
}
