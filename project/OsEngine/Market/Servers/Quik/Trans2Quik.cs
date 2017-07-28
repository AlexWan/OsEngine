/*
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
            UNKNOWN                     = -1,   // Результат выполнения неизвестен или функция не выполнялась
            SUCCESS                     = 0,    // Успешное выолнение функции
            FAILED                      = 1,    // Выполнении функции закончилось неудачей
            QUIK_TERMINAL_NOT_FOUND     = 2,    // В указанном каталоге либо отсутствует INFO.EXE, либо у него не запущен сервис обработки внешних подключений
            DLL_VERSION_NOT_SUPPORTED   = 3,    // Используемая версия библиотеки TRANS2QUIK.DLL указанным INFO.EXE не поддерживается
            ALREADY_CONNECTED_TO_QUIK   = 4,    // Cоединение уже установлено
            WRONG_SYNTAX                = 5,    // Строка транзакции заполнена неверно
            QUIK_NOT_CONNECTED          = 6,    // Не установлена связь терминала QUIK с сервером 
            DLL_NOT_CONNECTED           = 7,    // Не установлена связь библиотеки TRANS2QUIK.DLL с терминалом QUIK
            QUIK_CONNECTED              = 8,    // Соединение терминала QUIK с сервером установлено
            QUIK_DISCONNECTED           = 9,    // Соединение терминала QUIK с сервером разорвано
            DLL_CONNECTED               = 10,   // Соединение библиотеки TRANS2QUIK.DLL с терминалом QUIK установлено
            DLL_DISCONNECTED            = 11,   // Соединение библиотеки TRANS2QUIK.DLL с терминалом QUIK разорвано
            MEMORY_ALLOCATION_ERROR     = 12,   // Ошибка распределения памяти
            WRONG_CONNECTION_HANDLE     = 13,   // Ошибка при обработке соединения
            WRONG_INPUT_PARAMS          = 14    // Ошибочные входные параметры функции
        }

        // RESULT TRANS2QUIK_SUCCESS, TRANS2QUIK_QUIK_TERMINAL_NOT_FOUND, TRANS2QUIK_DLL_VERSION_NOT_SUPPORTED,
        // TRANS2QUIK_ALREADY_CONNECTED_TO_QUIK, TRANS2QUIK_FAILED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_CONNECT@16", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult CONNECT(
            [MarshalAs(UnmanagedType.LPStr)] string lpstConnectionParamsString,
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        // RESULT TRANS2QUIK_SUCCESS, TRANS2QUIK_FAILED, TRANS2QUIK_DLL_NOT_CONNECTED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_DISCONNECT@12", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult DISCONNECT(
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        // RESULT TRANS2QUIK_DLL_CONNECTED, TRANS2QUIK_DLL_NOT_CONNECTED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_IS_DLL_CONNECTED@12", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult IS_DLL_CONNECTED(
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        // RESULT TRANS2QUIK_QUIK_CONNECTED, TRANS2QUIK_QUIK_NOT_CONNECTED, TRANS2QUIK_DLL_NOT_CONNECTED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_IS_QUIK_CONNECTED@12", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult IS_QUIK_CONNECTED(
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        // RESULT TRANS2QUIK_SUCCESS, TRANS2QUIK_WRONG_SYNTAX, TRANS2QUIK_DLL_NOT_CONNECTED, TRANS2QUIK_QUIK_NOT_CONNECTED, TRANS2QUIK_FAILED
        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_SEND_ASYNC_TRANSACTION@16", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult SEND_ASYNC_TRANSACTION(
            [MarshalAs(UnmanagedType.LPStr)] string lpstTransactionString,
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstErrorMessage,
            int dwErrorMessageSize);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_SUBSCRIBE_ORDERS@8", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult SUBSCRIBE_ORDERS(
            String classCode,
            String secCodes);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_UNSUBSCRIBE_ORDERS@0", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult UNSUBSCRIBE_ORDERS();

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_SUBSCRIBE_TRADES@8", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult SUBSCRIBE_TRADES(
            String classCode,
            String secCodes);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_UNSUBSCRIBE_TRADES@0", CallingConvention = CallingConvention.StdCall)]
        public static extern QuikResult UNSUBSCRIBE_TRADES();

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_TRADE_ACCOUNT@4", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr TRADE_ACCOUNT(int nTradeDescriptor);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_TRADE_DATE@4", CallingConvention = CallingConvention.StdCall)]
        public static extern int TRADE_DATE(int nTradeDescriptor);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_TRADE_TIME@4", CallingConvention = CallingConvention.StdCall)]
        public static extern int TRADE_TIME(int nTradeDescriptor);

        public static string GetTradeAccount(Int32 descriptor)
        {
            return Marshal.PtrToStringAnsi(TRADE_ACCOUNT(descriptor));
        }

        private delegate void CONNECTION_STATUS_CALLBACK_UNMGR(
            QuikResult nConnectionEvent,
            int nExtendedErrorCode,
            IntPtr lpcstrInfoMessage);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_SET_CONNECTION_STATUS_CALLBACK@16", CallingConvention = CallingConvention.StdCall)]
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
            QuikResult nTransactionResult,
            int nTransactionExtendedErrorCode,
            int nTransactionReplyCode,
            int dwTransId,
            double dOrderNum,
            IntPtr lpcstrTransactionReplyMessage);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_SET_TRANSACTIONS_REPLY_CALLBACK@16", CallingConvention = CallingConvention.StdCall)]
        private static extern QuikResult SET_TRANSACTIONS_REPLY_CALLBACK_UNMGR(
            TRANSACTION_REPLY_CALLBACK_UNMGR pfTransactionReplyCallback,
            out int pnExtendedErrorCode,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lpstrErrorMessage,
            int dwErrorMessageSize);

        public delegate void TRANSACTION_REPLY_CALLBACK(
            QuikResult nTransactionResult,
            int nTransactionExtendedErrorCode,
            int nTransactionReplyCode,
            int dwTransId,
            double dOrderNum,
            string lpcstrTransactionReplyMessage);

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
            double dNumber,
            IntPtr ClassCode,
            IntPtr SecCode,
            double dPrice,
            int nBalance,
            double dValue,
            int nIsSell,
            int nStatus,
            int nOrderDescriptor);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_START_ORDERS@4", CallingConvention = CallingConvention.StdCall)]
        private static extern QuikResult START_ORDERS_UNMGR(
            ORDER_STATUS_CALLBACK_UNMGR pfnOrderStatusCallback);

        public delegate void ORDER_STATUS_CALLBACK(
            int nMode,
            int dwTransID,
            double dNumber,
            string ClassCode,
            string SecCode,
            double dPrice,
            int nBalance,
            double dValue,
            int nIsSell,
            int nStatus,
            int nOrderDescriptor);

        public static QuikResult START_ORDERS(
            ORDER_STATUS_CALLBACK pfnOrderStatusCallback)
        {
            order_status_callback = pfnOrderStatusCallback;
            return START_ORDERS_UNMGR(order_status_callback_unmgr);
        }

        private delegate void TRADE_STATUS_CALLBACK_UNMGR(
            int nMode,
            double dNumber,
            double dOrderNumber,
            IntPtr ClassCode,
            IntPtr SecCode,
            double dPrice,
            int nQty,
            double dValue,
            int nIsSell,
            int nTradeDescriptor);

        [DllImport("TRANS2QUIK.DLL", EntryPoint = "_TRANS2QUIK_START_TRADES@4", CallingConvention = CallingConvention.StdCall)]
        private static extern QuikResult START_TRADES_UNMGR(
            TRADE_STATUS_CALLBACK_UNMGR pfnTradeStatusCallback);

        public delegate void TRADE_STATUS_CALLBACK(
            int nMode,
            double dNumber,
            double dOrderNumber,
            string ClassCode,
            string SecCode,
            double dPrice,
            int nQty,
            double dValue,
            int nIsSell,
            int nTradeDescriptor);

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
            QuikResult nTransactionResult,
            int nTransactionExtendedErrorCode,
            int nTransactionReplyCode,
            int dwTransId,
            double dOrderNum,
            IntPtr lpcstrTransactionReplyMessage)
        {
            if (transaction_reply_callback != null)
            {
                transaction_reply_callback(
                    nTransactionResult,
                    nTransactionExtendedErrorCode,
                    nTransactionReplyCode,
                    dwTransId,
                    dOrderNum,
                    Marshal.PtrToStringAnsi(lpcstrTransactionReplyMessage));
            }
        }

        private static void order_status_callback_impl(
            int nMode,
            int dwTransID,
            double dNumber,
            IntPtr ClassCode,
            IntPtr SecCode,
            double dPrice,
            int nBalance,
            double dValue,
            int nIsSell,
            int nStatus,
            int nOrderDescriptor)
        {
            if (order_status_callback != null)
            {
                order_status_callback(
                    nMode,
                    dwTransID,
                    dNumber,
                    Marshal.PtrToStringAnsi(ClassCode),
                    Marshal.PtrToStringAnsi(SecCode),
                    dPrice,
                    nBalance,
                    dValue,
                    nIsSell,
                    nStatus,
                    nOrderDescriptor);
            }
        }

        private static void trade_status_callback_impl(
            int nMode,
            double dNumber,
            double dOrderNumber,
            IntPtr ClassCode,
            IntPtr SecCode,
            double dPrice,
            int nQty,
            double dValue,
            int nIsSell,
            int nTradeDescriptor)
        {
            if (trade_status_callback != null)
            {
                trade_status_callback(
                    nMode,
                    dNumber,
                    dOrderNumber,
                    Marshal.PtrToStringAnsi(ClassCode),
                    Marshal.PtrToStringAnsi(SecCode),
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
