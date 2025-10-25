#property copyright "Vyacheslav Demidyuk"
#property link      ""

#property version   "2.1"
#property description "MtApi (MT5) connection expert"

#include <json.mqh>
#include <Trade\SymbolInfo.mqh>
#include <trade/trade.mqh>
#include <generic/hashmap.mqh>

#import "MT5Connector.dll"
   bool initExpert(int expertHandle, int port, string& err);
   bool deinitExpert(int expertHandle, string& err);
 
   bool sendEvent(int expertHandle, int eventType, string payload, string& err);
   bool sendResponse(int expertHandle, string response, string& err);

   bool getCommandType(int expertHandle, int& res, string& err);
   bool getPayload(int expertHandle, string& res, string& err);
#import

///--------------------------------------------------------------------------------------

//#define __DEBUG_LOG__

enum LockTickType
{
   NO_LOCK,
   LOCK_EVERY_TICK,
   LOCK_EVERY_CANDLE
};

input int Port = 8228;
input LockTickType BacktestingLockTicks = NO_LOCK;
input group           "Disable Events "
input bool Enable_OnBookEvent = true;                 
input bool Enable_OnTickEvent = false;                 
input bool Enable_OnTradeTransactionEvent = true;     
input bool Enable_OnLastBarEvent = true;    


int ExpertHandle;

string _error;
bool isCrashed = false;

bool IsRemoteReadyForTesting = false;

long _last_bar_open_time = 0;
bool _is_ticks_locked = false;

int OnInit()
{
   int result = init();  
   return (result);
}

double OnTester()
{
    Print("OnTester");
    return 0;
}

void OnDeinit(const int reason)
{
   deinit();
}

void OnTick()
{
   string symbol = Symbol();
   
   bool lastbar_time_changed = false;
   long lastbar_time = SeriesInfoInteger(symbol, Period(), SERIES_LASTBAR_DATE); 
   if (_last_bar_open_time != lastbar_time)
   {
      if (_last_bar_open_time != 0 )
      {
         if(Enable_OnLastBarEvent)
         {
           MqlRates rates_array[];
           CopyRates(symbol, Period(), 1, 1, rates_array);
      
           MtTimeBarEvent time_bar(symbol, rates_array[0]);
           SendMtEvent(ON_LAST_TIME_BAR_EVENT, time_bar);
         }
        lastbar_time_changed = true;
      }
      
      _last_bar_open_time = lastbar_time;
   }
   
   if (Enable_OnTickEvent)
   {
       MqlTick last_tick;
       SymbolInfoTick(Symbol(),last_tick);
   
       MtQuote quote(symbol, last_tick);
       SendMtEvent(ON_TICK_EVENT, quote);
   }
   
   if (IsTesting())
   {
      if (BacktestingLockTicks == LOCK_EVERY_TICK ||
         (BacktestingLockTicks == LOCK_EVERY_CANDLE && lastbar_time_changed))
      {
         _is_ticks_locked = true;
         
         MtLockTickEvent lock_tick_event(symbol);
         SendMtEvent(ON_LOCK_TICKS_EVENT, lock_tick_event);
      }
      
      OnTimer();
   }
}

void  OnTradeTransaction( 
   const MqlTradeTransaction&    trans,        // trade transaction structure 
   const MqlTradeRequest&        request,      // request structure 
   const MqlTradeResult&         result        // result structure 
   )
{
      if (!Enable_OnTradeTransactionEvent) return;
      
      #ifdef __DEBUG_LOG__
       PrintFormat("%s:", __FUNCTION__);
      #endif 
      
       
      MtOnTradeTransactionEvent trans_event(trans, request, result);
      SendMtEvent(ON_TRADE_TRANSACTION_EVENT, trans_event);
}

void OnBookEvent(const string& symbol)
{
    if(!Enable_OnBookEvent) return;
    
    #ifdef __DEBUG_LOG__
      PrintFormat("%s: %s", __FUNCTION__, symbol);
    #endif 

    MtOnBookEvent book_event(symbol);
    SendMtEvent(ON_BOOK_EVENT, book_event);
}

typedef string (*TExecutor)();

class CExecutorWrapper
{
private:
   TExecutor _executor;

public:
   CExecutorWrapper(TExecutor executor)
      :_executor(executor)
   {
   }
   
   string Execute() { return  _executor(); }
};

CHashMap<int, CExecutorWrapper*> _executors;

#define ADD_EXECUTOR(cmd_type, exec_name) _executors.Add(cmd_type, new CExecutorWrapper(Execute_##exec_name))

int preinit()
{
   StringInit(_error,1000,0);

   ADD_EXECUTOR(1, GetQuote);
   ADD_EXECUTOR(2, OrderCalcMargin);
   ADD_EXECUTOR(3, OrderCalcProfit);
   ADD_EXECUTOR(4, PositionGetTicket);
   ADD_EXECUTOR(6, PositionsTotal);
   ADD_EXECUTOR(7, PositionGetSymbol);
   ADD_EXECUTOR(8, PositionSelect);
   ADD_EXECUTOR(9, PositionGetDouble);
   ADD_EXECUTOR(10, PositionGetInteger);
   ADD_EXECUTOR(11, PositionGetString);
   ADD_EXECUTOR(12, OrdersTotal);
   ADD_EXECUTOR(13, OrderGetTicket);
   ADD_EXECUTOR(14, OrderSelect);
   ADD_EXECUTOR(15, OrderGetDouble);
   ADD_EXECUTOR(16, OrderGetInteger);
   ADD_EXECUTOR(17, OrderGetString);
   ADD_EXECUTOR(18, HistorySelect);
   ADD_EXECUTOR(19, HistorySelectByPosition);
   ADD_EXECUTOR(20, HistoryOrderSelect);
   ADD_EXECUTOR(21, HistoryOrdersTotal);
   ADD_EXECUTOR(22, HistoryOrderGetTicket);
   ADD_EXECUTOR(23, HistoryOrderGetDouble);
   ADD_EXECUTOR(24, HistoryOrderGetInteger);
   ADD_EXECUTOR(25, HistoryOrderGetString);
   ADD_EXECUTOR(26, HistoryDealSelect);
   ADD_EXECUTOR(27, HistoryDealsTotal);
   ADD_EXECUTOR(28, HistoryDealGetTicket);
   ADD_EXECUTOR(29, HistoryDealGetDouble);
   ADD_EXECUTOR(30, HistoryDealGetInteger);
   ADD_EXECUTOR(31, HistoryDealGetString);
   ADD_EXECUTOR(32, AccountInfoDouble);
   ADD_EXECUTOR(33, AccountInfoInteger);
   ADD_EXECUTOR(34, AccountInfoString);
   ADD_EXECUTOR(35, SeriesInfoInteger);
   ADD_EXECUTOR(36, Bars);
   ADD_EXECUTOR(1036, Bars2);
   ADD_EXECUTOR(37, BarsCalculated);
   ADD_EXECUTOR(38, IndicatorCreate);
   ADD_EXECUTOR(39, IndicatorRelease);
   ADD_EXECUTOR(40, CopyBuffer);
   ADD_EXECUTOR(1040, CopyBuffer1);
   ADD_EXECUTOR(1140, CopyBuffer2);
   ADD_EXECUTOR(41, CopyRates);
   ADD_EXECUTOR(1041, CopyRates1);
   ADD_EXECUTOR(1141, CopyRates2);
   ADD_EXECUTOR(42, CopyTime);
   ADD_EXECUTOR(1042, CopyTime1);
   ADD_EXECUTOR(1142, CopyTime2);
   ADD_EXECUTOR(43, CopyOpen);
   ADD_EXECUTOR(1043, CopyOpen1);
   ADD_EXECUTOR(1143, CopyOpen2);
   ADD_EXECUTOR(44, CopyHigh);
   ADD_EXECUTOR(1044, CopyHigh1);
   ADD_EXECUTOR(1144, CopyHigh2);
   ADD_EXECUTOR(45, CopyLow);
   ADD_EXECUTOR(1045, CopyLow1);
   ADD_EXECUTOR(1145, CopyLow2);
   ADD_EXECUTOR(46, CopyClose);
   ADD_EXECUTOR(1046, CopyClose1);
   ADD_EXECUTOR(1146, CopyClose2);
   ADD_EXECUTOR(47, CopyTickVolume);
   ADD_EXECUTOR(1047, CopyTickVolume1);
   ADD_EXECUTOR(1147, CopyTickVolume2);
   ADD_EXECUTOR(48, CopyRealVolume);
   ADD_EXECUTOR(1048, CopyRealVolume1);
   ADD_EXECUTOR(1148, CopyRealVolume2);
   ADD_EXECUTOR(49, CopySpread);
   ADD_EXECUTOR(1049, CopySpread1);
   ADD_EXECUTOR(1149, CopySpread2);
   ADD_EXECUTOR(50, SymbolsTotal);
   ADD_EXECUTOR(51, SymbolName);
   ADD_EXECUTOR(52, SymbolSelect);
   ADD_EXECUTOR(53, SymbolIsSynchronized);
   ADD_EXECUTOR(54, SymbolInfoDouble);
   ADD_EXECUTOR(55, SymbolInfoInteger);
   ADD_EXECUTOR(56, SymbolInfoString);
   ADD_EXECUTOR(1056, SymbolInfoString2);
   ADD_EXECUTOR(57, SymbolInfoTick);
   ADD_EXECUTOR(58, SymbolInfoSessionQuote); 
   ADD_EXECUTOR(59, SymbolInfoSessionTrade);
   ADD_EXECUTOR(60, MarketBookAdd);
   ADD_EXECUTOR(61, MarketBookRelease);
   ADD_EXECUTOR(62, MarketBookGet);
   ADD_EXECUTOR(63, OrderCloseAll);
   ADD_EXECUTOR(64, PositionClose);
   ADD_EXECUTOR(65, PositionOpen);
   ADD_EXECUTOR(1065, PositionOpen2);
   ADD_EXECUTOR(6066, PositionModify);
   ADD_EXECUTOR(6067, PositionClosePartialBySymbol);
   ADD_EXECUTOR(6068, PositionClosePartialByTicket);
   ADD_EXECUTOR(66, BacktestingReady);
   ADD_EXECUTOR(67, IsTesting);
   ADD_EXECUTOR(68, Print);
   ADD_EXECUTOR(69, PositionSelectByTicket);
   ADD_EXECUTOR(70, ObjectCreate);
   ADD_EXECUTOR(71, ObjectName);
   ADD_EXECUTOR(72, ObjectDelete);
   ADD_EXECUTOR(73, ObjectsDeleteAll);
   ADD_EXECUTOR(74, ObjectFind);
   ADD_EXECUTOR(75, ObjectGetTimeByValue);
   ADD_EXECUTOR(76, ObjectGetValueByTime);
   ADD_EXECUTOR(77, ObjectMove);
   ADD_EXECUTOR(78, ObjectsTotal);
   ADD_EXECUTOR(79, ObjectGetDouble);
   ADD_EXECUTOR(80, ObjectGetInteger);
   ADD_EXECUTOR(81, ObjectGetString);
   ADD_EXECUTOR(82, ObjectSetDouble);
   ADD_EXECUTOR(83, ObjectSetInteger);
   ADD_EXECUTOR(84, ObjectSetString);
   ADD_EXECUTOR(88, iAC);
   ADD_EXECUTOR(89, iAD);
   ADD_EXECUTOR(90, iADX);
   ADD_EXECUTOR(91, iADXWilder);
   ADD_EXECUTOR(92, iAlligator);
   ADD_EXECUTOR(93, iAMA);
   ADD_EXECUTOR(94, iAO);
   ADD_EXECUTOR(95, iATR);
   ADD_EXECUTOR(96, iBearsPower);
   ADD_EXECUTOR(97, iBands);
   ADD_EXECUTOR(98, iBullsPower);
   ADD_EXECUTOR(99, iCCI);
   ADD_EXECUTOR(100, iChaikin);
   ADD_EXECUTOR(101, iCustom);
   ADD_EXECUTOR(102, iDEMA);
   ADD_EXECUTOR(103, iDeMarker);
   ADD_EXECUTOR(104, iEnvelopes);
   ADD_EXECUTOR(105, iForce);
   ADD_EXECUTOR(106, iFractals);
   ADD_EXECUTOR(107, iFrAMA);
   ADD_EXECUTOR(108, iGator);
   ADD_EXECUTOR(109, iIchimoku);
   ADD_EXECUTOR(110, iBWMFI);
   ADD_EXECUTOR(111, iMomentum);
   ADD_EXECUTOR(112, iMFI);
   ADD_EXECUTOR(113, iMA);
   ADD_EXECUTOR(114, iOsMA);
   ADD_EXECUTOR(115, iMACD);
   ADD_EXECUTOR(116, iOBV);
   ADD_EXECUTOR(117, iSAR);
   ADD_EXECUTOR(118, iRSI);
   ADD_EXECUTOR(119, iRVI);
   ADD_EXECUTOR(120, iStdDev);
   ADD_EXECUTOR(121, iStochastic);
   ADD_EXECUTOR(122, iTEMA);
   ADD_EXECUTOR(123, iTriX);
   ADD_EXECUTOR(124, iWPR);
   ADD_EXECUTOR(125, iVIDyA);
   ADD_EXECUTOR(126, iVolumes);
   ADD_EXECUTOR(127, TimeCurrent);
   ADD_EXECUTOR(128, TimeTradeServer);
   ADD_EXECUTOR(129, TimeLocal);
   ADD_EXECUTOR(130, TimeGMT);
   ADD_EXECUTOR(132, GetLastError);
   ADD_EXECUTOR(136, Alert);
   ADD_EXECUTOR(143, ResetLastError);
   ADD_EXECUTOR(146, GlobalVariableCheck);
   ADD_EXECUTOR(147, GlobalVariableTime);
   ADD_EXECUTOR(148, GlobalVariableDel);
   ADD_EXECUTOR(149, GlobalVariableGet);
   ADD_EXECUTOR(150, GlobalVariableName);
   ADD_EXECUTOR(151, GlobalVariableSet);
   ADD_EXECUTOR(152, GlobalVariablesFlush);
   ADD_EXECUTOR(153, TerminalInfoString);
   ADD_EXECUTOR(154, GlobalVariableTemp);
   ADD_EXECUTOR(156, GlobalVariableSetOnCondition);
   ADD_EXECUTOR(157, GlobalVariablesDeleteAll);
   ADD_EXECUTOR(158, GlobalVariablesTotal);
   ADD_EXECUTOR(159, UnlockTicks);
   ADD_EXECUTOR(160, PositionCloseAll);
   ADD_EXECUTOR(161, TesterStop);
   ADD_EXECUTOR(204, TerminalInfoInteger);
   ADD_EXECUTOR(205, TerminalInfoDouble);
   ADD_EXECUTOR(206, ChartId);
   ADD_EXECUTOR(207, ChartRedraw);
   ADD_EXECUTOR(236, ChartApplyTemplate);
   ADD_EXECUTOR(237, ChartSaveTemplate);
   ADD_EXECUTOR(238, ChartWindowFind);
   ADD_EXECUTOR(239, ChartTimePriceToXY);
   ADD_EXECUTOR(240, ChartXYToTimePrice);
   ADD_EXECUTOR(241, ChartOpen);
   ADD_EXECUTOR(242, ChartFirst);
   ADD_EXECUTOR(243, ChartNext);
   ADD_EXECUTOR(244, ChartClose);
   ADD_EXECUTOR(245, ChartSymbol);
   ADD_EXECUTOR(246, ChartPeriod);
   ADD_EXECUTOR(247, ChartSetDouble);
   ADD_EXECUTOR(248, ChartSetInteger);
   ADD_EXECUTOR(249, ChartSetString);
   ADD_EXECUTOR(250, ChartGetDouble);
   ADD_EXECUTOR(251, ChartGetInteger);
   ADD_EXECUTOR(252, ChartGetString);
   ADD_EXECUTOR(253, ChartNavigate);
   ADD_EXECUTOR(254, ChartIndicatorDelete);
   ADD_EXECUTOR(255, ChartIndicatorName);
   ADD_EXECUTOR(256, ChartIndicatorsTotal);
   ADD_EXECUTOR(257, ChartWindowOnDropped);
   ADD_EXECUTOR(258, ChartPriceOnDropped);
   ADD_EXECUTOR(259, ChartTimeOnDropped);
   ADD_EXECUTOR(260, ChartXOnDropped);
   ADD_EXECUTOR(261, ChartYOnDropped);
   ADD_EXECUTOR(262, ChartSetSymbolPeriod);
   ADD_EXECUTOR(263, ChartScreenShot);
   ADD_EXECUTOR(264, WindowBarsPerChart);
   ADD_EXECUTOR(280, ChartIndicatorAdd);
   ADD_EXECUTOR(281, ChartIndicatorGet);

   ADD_EXECUTOR(300, CopyTicks);
   ADD_EXECUTOR(301, OrderSend);
   ADD_EXECUTOR(302, OrderSendAsync);
   ADD_EXECUTOR(303, OrderCheck);
   ADD_EXECUTOR(304, Buy);
   ADD_EXECUTOR(305, Sell);
   ADD_EXECUTOR(306, GetSymbols);
   
   return (0);
}

bool IsDemo()
{
   if(AccountInfoInteger(ACCOUNT_TRADE_MODE)==ACCOUNT_TRADE_MODE_DEMO)
      return(true);
   else
      return(false);
}

bool IsTesting()
{  
   bool isTesting = MQLInfoInteger(MQL_TESTER);
   return isTesting;
}

int init() 
{
   preinit();  

   if (TerminalInfoInteger(TERMINAL_DLLS_ALLOWED) == false) 
   {
      MessageBox("Dlls not allowed.", "MtApi", 0);
      isCrashed = true;
      return (1);
   }
   if (MQLInfoInteger(MQL_DLLS_ALLOWED) == false) 
   {
      MessageBox("Libraries not allowed.", "MtApi", 0);
      isCrashed = true;
      return (1);
   }

   if (MQLInfoInteger(MQL_TRADE_ALLOWED) == false) 
   {
      MessageBox("Trade not allowed.", "MtApi", 0);
      isCrashed = true;
      return (1);
   }

   long chartID = ChartID();
   ExpertHandle = (int) ChartGetInteger(chartID, CHART_WINDOW_HANDLE);
   
   if (!initExpert(ExpertHandle, Port, _error))
   {
       MessageBox(_error, "MtApi", 0);
       isCrashed = true;
       return(1);
   }
   
   if (executeCommand() == 1)
   {   
      isCrashed = true;
      return (1);
   }
   
#ifdef __DEBUG_LOG__
   PrintFormat("Expert Handle = %d", ExpertHandle);
   PrintFormat("IsTesting: %s", IsTesting() ? "true" : "false");
#endif
   
   //--- Backtesting mode
    if (IsTesting())
    {      
       Print("Waiting on remote client...");
       //wait for command (BacktestingReady) from remote side to be ready for work
       while(!IsRemoteReadyForTesting)
       {
          executeCommand();
          
          //This section uses a while loop to simulate Sleep() during Backtest.
          unsigned int viSleepUntilTick = GetTickCount() + 100; //100 milliseconds
          while(GetTickCount() < viSleepUntilTick) 
          {
             //Do absolutely nothing. Just loop until the desired tick is reached.
          }
       }
    }
   //--- 

   return (0);
}

int deinit() 
{
   if (isCrashed == 0) 
   {
      if (!deinitExpert(ExpertHandle, _error)) 
      {
         MessageBox(_error, "MtApi", 0);
         isCrashed = true;
         return (1);
      }
      Print("Expert was deinitialized.");
   }
   
   //--- clear and delete all values from map
   int keys[];
   CExecutorWrapper *values[];
   int count = _executors.CopyTo(keys, values);
   for(int i = 0; i < count; i++)
   {
      //--- release object pointers to avoid memory leaks
      if(CheckPointer(values[i]) == POINTER_DYNAMIC)
         delete values[i];
   }
   _executors.Clear();
   
   return (0);
}

void OnTimer()
{
   while(true)
   {
      int executedCommand = executeCommand();
      
      if (_is_ticks_locked)
         continue;
      
      if (executedCommand == 0)
         break;
   }
}

int executeCommand()
{
   int commandType = 0;

   if (!getCommandType(ExpertHandle, commandType, _error))
   {
      Print("[ERROR] ExecuteCommand: Failed to get command type! ", _error);
      return (0);
   }

   if (commandType == 0)
      return 0;
   
#ifdef __DEBUG_LOG__
   Print("executeCommand: commnad type = ", commandType);
#endif 

   string response;
   CExecutorWrapper *wrapper;
   if (_executors.TryGetValue(commandType, wrapper)) 
   {
      response = wrapper.Execute();
   }
   else
   {
      Print("WARNING: Unknown command type = ", commandType);
      response = CreateErrorResponse(-1, "Unknown command type");
   }
   
   if (!sendResponse(ExpertHandle, response, _error))
      PrintFormat("[ERROR] response: %s", _error);
   
   return (commandType);
}

//------ helper macros to get and send values ------------------
template <typename T_>
class auto_ptr
{
public:
   T_ *p;
   void reset() { if (this.p) delete this.p; this.p = NULL;}
   
   auto_ptr(void *ptr = NULL): p(ptr) {}
   ~auto_ptr()  { this.reset(); }
   
   void swap(auto_ptr<T_> &other)
   {
      T_ *buf = this.p;
      this.p = other.p;
      other.p = buf;
   }
};

JSONObject* GetJsonPayload()
{
   string payload;
   StringInit(payload, 5000, 0);
   
   if (!getPayload(ExpertHandle, payload, _error))
   {
      PrintFormat("%s [ERROR]: %s", __FUNCTION__, _error);
      return NULL;
   }

   JSONParser payload_parser;
   JSONValue *payload_json = payload_parser.parse(payload);
   
   if (payload_json == NULL) 
   {   
      PrintFormat("%s [ERROR]: %d - %s", __FUNCTION__, (string)payload_parser.getErrorCode(), payload_parser.getErrorMessage());
      return NULL;
   }
   
   return payload_json.isObject() ? payload_json : NULL;
}

#define PRINT_MSG_AND_RETURN_VALUE(msg,value) PrintFormat("%s: %s",__FUNCTION__,msg);return value
#define GET_JSON_PAYLOAD(json) auto_ptr<JSONObject> json(GetJsonPayload()); if (json.p == NULL) { return CreateErrorResponse(-1, "Failed to get payload"); }
#define CHECK_JSON_VALUE(json, name_value) if (json.p.getValue(name_value) == NULL) { PRINT_MSG_AND_RETURN_VALUE(StringFormat("failed to get %s from JSON!", name_value), CreateErrorResponse(-1, (StringFormat("Undefinded mandatory parameter %s", name_value)))); }
#define GET_INT_JSON_VALUE(json, name_value, return_value) CHECK_JSON_VALUE(json, name_value); int return_value = json.p.getInt(name_value)
#define GET_UINT_JSON_VALUE(json, name_value, return_value) CHECK_JSON_VALUE(json, name_value); uint return_value = json.p.getInt(name_value)
#define GET_DOUBLE_JSON_VALUE(json, name_value, return_value) CHECK_JSON_VALUE(json, name_value); double return_value = json.p.getDouble(name_value)
#define GET_LONG_JSON_VALUE(json, name_value, return_value) CHECK_JSON_VALUE(json, name_value); long return_value = json.p.getLong(name_value)
#define GET_ULONG_JSON_VALUE(json, name_value, return_value) CHECK_JSON_VALUE(json, name_value); ulong return_value = json.p.getLong(name_value)
#define GET_STRING_JSON_VALUE(json, name_value, return_value) CHECK_JSON_VALUE(json, name_value); string return_value = json.p.getString(name_value)
#define GET_BOOL_JSON_VALUE(json, name_value, return_value) CHECK_JSON_VALUE(json, name_value); bool return_value = json.p.getBool(name_value)

//--------- Executors ----------------------------------------------------
string Execute_GetQuote()
{
   MqlTick tick;
   SymbolInfoTick(Symbol(), tick);
   
   MtQuote quote(Symbol(), tick);
   return CreateSuccessResponse(quote.CreateJson());
}

string Execute_OrderCloseAll()
{
   OrderCloseAll();
   return CreateSuccessResponse();
}

string Execute_OrderCalcMargin()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "Action", action);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_DOUBLE_JSON_VALUE(jo, "Volume", volume);
   GET_DOUBLE_JSON_VALUE(jo, "Price", price);
   
   double margin;
   bool ok = OrderCalcMargin((ENUM_ORDER_TYPE)action, symbol, volume, price, margin);
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: return value = %s", __FUNCTION__, ok ? "true" : "false");
#endif                  
   
   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", new JSONNumber(margin));

   return CreateSuccessResponse(result_value_jo);
}

string Execute_OrderCalcProfit()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "Action", action);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_DOUBLE_JSON_VALUE(jo, "Volume", volume);
   GET_DOUBLE_JSON_VALUE(jo, "PriceOpen", price_open);
   GET_DOUBLE_JSON_VALUE(jo, "PriceClose", price_close);   
   
   double profit;
   bool ok = OrderCalcProfit((ENUM_ORDER_TYPE)action, symbol, volume, price_open, price_close, profit);
            
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: return value = %s", __FUNCTION__, ok ? "true" : "false");
#endif                  
   
   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", new JSONNumber(profit));

   return CreateSuccessResponse(result_value_jo);   
}

string Execute_PositionGetTicket()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "Index", index);

#ifdef __DEBUG_LOG__
   PrintFormat("%s: index = %d", __FUNCTION__, index);
#endif

   ulong result = PositionGetTicket(index);

#ifdef __DEBUG_LOG__
   PrintFormat("%s: result = %u", __FUNCTION__, result);
#endif

   return CreateSuccessResponse(new JSONNumber(result));   
}

string Execute_PositionsTotal()
{
   int result = PositionsTotal();  
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_PositionGetSymbol()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "Index", index);
   
   string symbol = PositionGetSymbol(index);
   return CreateSuccessResponse(new JSONString(symbol));
}

string Execute_PositionSelect()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   
   bool ok = PositionSelect(symbol);
   return CreateSuccessResponse(new JSONBool(ok));
}

string Execute_PositionGetDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
      
   double result = PositionGetDouble((ENUM_POSITION_PROPERTY_DOUBLE)property_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_PositionGetInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   long result = PositionGetInteger((ENUM_POSITION_PROPERTY_INTEGER)property_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_PositionGetString()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   string result = PositionGetString((ENUM_POSITION_PROPERTY_STRING)property_id);
   return CreateSuccessResponse(new JSONString(result));   
}

string Execute_OrdersTotal()
{
   int result = OrdersTotal();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_OrderGetTicket()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "Index", index);

   ulong result = OrderGetTicket(index);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_OrderSelect()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "Ticket", ticket);

   bool result = OrderSelect(ticket);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_OrderGetDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   double result = OrderGetDouble((ENUM_ORDER_PROPERTY_DOUBLE)property_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_OrderGetInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);

   long result = OrderGetInteger((ENUM_ORDER_PROPERTY_INTEGER)property_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_OrderGetString()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);

   string result = OrderGetString((ENUM_ORDER_PROPERTY_STRING)property_id);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_HistorySelect()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "FromDate", from_date);
   GET_INT_JSON_VALUE(jo, "ToDate", to_date);  

   bool result = HistorySelect((datetime)from_date, (datetime)to_date);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_HistorySelectByPosition()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "PositionId", position_id);

   bool result = HistorySelectByPosition(position_id);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_HistoryOrderSelect()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "Ticket", ticket);
   
   bool result = HistoryOrderSelect(ticket);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_HistoryOrdersTotal()
{
   int result = HistoryOrdersTotal();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_HistoryOrderGetTicket()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "Index", index);
   
   ulong result = HistoryOrderGetTicket(index);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_HistoryOrderGetDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "TicketNumber", ticket_number);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);

   double result = HistoryOrderGetDouble(ticket_number, (ENUM_ORDER_PROPERTY_DOUBLE)property_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_HistoryOrderGetInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "TicketNumber", ticket_number);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   long result = HistoryOrderGetInteger(ticket_number, (ENUM_ORDER_PROPERTY_INTEGER)property_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_HistoryOrderGetString()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "TicketNumber", ticket_number);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   string result = HistoryOrderGetString(ticket_number, (ENUM_ORDER_PROPERTY_STRING)property_id);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_HistoryDealSelect()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "Ticket", ticket);
   
   bool result = HistoryDealSelect(ticket);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_HistoryDealsTotal()
{
   int result =  HistoryDealsTotal();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_HistoryDealGetTicket()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "Index", index);
   
   ulong result = HistoryDealGetTicket(index);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_HistoryDealGetDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "TicketNumber", ticket_number);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   double result = HistoryDealGetDouble(ticket_number, (ENUM_DEAL_PROPERTY_DOUBLE)property_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_HistoryDealGetInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "TicketNumber", ticket_number);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   long result = HistoryDealGetInteger(ticket_number, (ENUM_DEAL_PROPERTY_INTEGER)property_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_HistoryDealGetString()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "TicketNumber", ticket_number);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   string result = HistoryDealGetString(ticket_number, (ENUM_DEAL_PROPERTY_STRING)property_id);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_AccountInfoDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   double result = AccountInfoDouble((ENUM_ACCOUNT_INFO_DOUBLE)property_id);   
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_AccountInfoInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   long result = AccountInfoInteger((ENUM_ACCOUNT_INFO_INTEGER)property_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_AccountInfoString()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", property_id);
   
   string result = AccountInfoString((ENUM_ACCOUNT_INFO_STRING)property_id);
   return CreateSuccessResponse(new JSONString(result));   
}

string Execute_SeriesInfoInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   
   long result = SeriesInfoInteger(symbol, (ENUM_TIMEFRAMES)timeframe, (ENUM_SERIES_INFO_INTEGER)prop_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_Bars()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   
   int result = Bars(symbol, (ENUM_TIMEFRAMES)timeframe);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_Bars2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);
   
   int result = Bars(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_BarsCalculated()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "IndicatorHandle", indicator_handle);
   
   int result = BarsCalculated(indicator_handle);
   return CreateSuccessResponse(new JSONNumber(result));
}



string Execute_CopyBuffer()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "IndicatorHandle", indicator_handle);
   GET_INT_JSON_VALUE(jo, "BufferNum", buffer_num);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);
   
   double buffer[];
   int copied = CopyBuffer(indicator_handle, buffer_num, start_pos, count, buffer);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(buffer[i]));

   return CreateSuccessResponse(jaresult);
}

string Execute_CopyBuffer1()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "IndicatorHandle", indicator_handle);
   GET_INT_JSON_VALUE(jo, "BufferNum", buffer_num);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);
   
   double buffer[];
   int copied = CopyBuffer(indicator_handle, buffer_num, start_time, count, buffer);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(buffer[i]));

   return CreateSuccessResponse(jaresult);
}

string Execute_CopyBuffer2()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "IndicatorHandle", indicator_handle);
   GET_INT_JSON_VALUE(jo, "BufferNum", buffer_num);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);
   
   double buffer[];
   int copied = CopyBuffer(indicator_handle, buffer_num, start_time, stop_time, buffer);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(buffer[i]));

   return CreateSuccessResponse(jaresult);
}

string Execute_CopyRates()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);
   
   MqlRates rates[];
   int copied = CopyRates(symbol, (ENUM_TIMEFRAMES)timeframe, start_pos, count, rates);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, MqlRatesToJson(rates[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyRates1()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);
   
   MqlRates rates[];
   int copied = CopyRates(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, count, rates);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, MqlRatesToJson(rates[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyRates2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);

   MqlRates rates[];
   int copied = CopyRates(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time, rates);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, MqlRatesToJson(rates[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyTime()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);
   
   datetime time_array[];
   int copied = CopyTime(symbol, (ENUM_TIMEFRAMES)timeframe, start_pos, count, time_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber((long)time_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyTime1()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);
   
   datetime time_array[];
   int copied = CopyTime(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, count, time_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber((long)time_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyTime2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);
   
   datetime time_array[];
   int copied = CopyTime(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time, time_array);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber((long)time_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyOpen()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);
   
   double open_array[];
   int copied = CopyOpen(symbol, (ENUM_TIMEFRAMES)timeframe, start_pos, count, open_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(open_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyOpen1()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);
   
   double open_array[];
   int copied = CopyOpen(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, count, open_array);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(open_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyOpen2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);

   double open_array[];
   int copied = CopyOpen(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time, open_array);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(open_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyHigh()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);

   double high_array[];
   int copied = CopyHigh(symbol, (ENUM_TIMEFRAMES)timeframe, start_pos, count, high_array);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(high_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyHigh1()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);

   double high_array[];
   int copied = CopyHigh(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, count, high_array);       

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(high_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyHigh2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);

   double high_array[];
   int copied = CopyHigh(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time, high_array);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(high_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyLow()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);

   double low_array[];   
   int copied = CopyLow(symbol, (ENUM_TIMEFRAMES)timeframe, start_pos, count, low_array);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(low_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyLow1()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);

   double low_array[];
   int copied = CopyLow(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, count, low_array);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(low_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyLow2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);

   double low_array[];
   int copied = CopyLow(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time, low_array);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(low_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyClose()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);

   double close_array[];
   int copied = CopyClose(symbol, (ENUM_TIMEFRAMES)timeframe, start_pos, count, close_array);
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(close_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyClose1()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);

   double close_array[];
   int copied = CopyClose(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, count, close_array); 
   
   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(close_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyClose2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);
   
   double close_array[];
   int copied = CopyClose(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time, close_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(close_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyTickVolume()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);

   long volume_array[];
   int copied = CopyTickVolume(symbol, (ENUM_TIMEFRAMES)timeframe, start_pos, count, volume_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(volume_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyTickVolume1()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);

   long volume_array[];
   int copied = CopyTickVolume(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, count, volume_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(volume_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyTickVolume2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);

   long volume_array[];
   int copied = CopyTickVolume(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time, volume_array);       

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(volume_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyRealVolume()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);

   long volume_array[];
   int copied = CopyRealVolume(symbol, (ENUM_TIMEFRAMES)timeframe, start_pos, count, volume_array);       

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(volume_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyRealVolume1()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);

   long volume_array[];
   int copied = CopyRealVolume(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, count, volume_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(volume_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopyRealVolume2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);

   long volume_array[];
   int copied = CopyRealVolume(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time, volume_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(volume_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopySpread()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartPos", start_pos);
   GET_INT_JSON_VALUE(jo, "Count", count);

   int spread_array[];
   int copied = CopySpread(symbol, (ENUM_TIMEFRAMES)timeframe, start_pos, count, spread_array);       

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(spread_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopySpread1()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "Count", count);

   int spread_array[];
   int copied = CopySpread(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, count, spread_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(spread_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_CopySpread2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_INT_JSON_VALUE(jo, "StartTime", start_time);
   GET_INT_JSON_VALUE(jo, "StopTime", stop_time);

   int spread_array[];
   int copied = CopySpread(symbol, (ENUM_TIMEFRAMES)timeframe, (datetime)start_time, (datetime)stop_time, spread_array);

   JSONArray* jaresult = new JSONArray();
   for(int i = 0; i < copied; i++)
      jaresult.put(i, new JSONNumber(spread_array[i]));
      
   return CreateSuccessResponse(jaresult);
}

string Execute_SymbolsTotal()
{
   GET_JSON_PAYLOAD(jo);
   GET_BOOL_JSON_VALUE(jo, "Selected", selected);

   int result = SymbolsTotal(selected);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_SymbolName()
{
   GET_JSON_PAYLOAD(jo);
   GET_BOOL_JSON_VALUE(jo, "Selected", selected);
   GET_INT_JSON_VALUE(jo, "Pos", pos);
   
   string result = SymbolName(pos, selected);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_SymbolSelect()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_BOOL_JSON_VALUE(jo, "Selected", select);
   
   bool result = SymbolSelect(symbol, select);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_SymbolIsSynchronized()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   
   bool result = SymbolIsSynchronized(symbol);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_SymbolInfoDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   
   double result = SymbolInfoDouble(symbol, (ENUM_SYMBOL_INFO_DOUBLE)prop_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_SymbolInfoInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   
   long result = SymbolInfoInteger(symbol, (ENUM_SYMBOL_INFO_INTEGER)prop_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_SymbolInfoString()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   
   string result = SymbolInfoString(symbol, (ENUM_SYMBOL_INFO_STRING)prop_id);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_SymbolInfoString2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: symbol_name = %s, prop_id = %s", __FUNCTION__, symbol_name, EnumToString(prop_id));
#endif

   string string_var;
   bool ok = SymbolInfoString(symbol, SYMBOL_INDUSTRY_NAME, string_var);//(ENUM_SYMBOL_INFO_STRING)prop_id, string_var);
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: ok = %s, string_var = %s", __FUNCTION__, BoolToString(ok), string_var);
#endif

   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", new JSONString(string_var));
   
   return CreateSuccessResponse(result_value_jo);   
}

string Execute_SymbolInfoSessionQuote()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "DayOfWeek", day_of_week);
   GET_UINT_JSON_VALUE(jo, "SessionIndex", session_index);
   
   datetime from;
   datetime to;   
   bool ok = SymbolInfoSessionQuote(symbol, (ENUM_DAY_OF_WEEK)day_of_week, session_index, from, to);
      
   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   JSONObject* info_jo = new JSONObject();
   info_jo.put("From", new JSONNumber((long)from));
   info_jo.put("To", new JSONNumber((long)to));
   result_value_jo.put("Result", info_jo);
   
   return CreateSuccessResponse(result_value_jo);
}

string Execute_SymbolInfoSessionTrade()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "DayOfWeek", day_of_week);
   GET_UINT_JSON_VALUE(jo, "SessionIndex", session_index);
   
   datetime from;
   datetime to;
   bool ok = SymbolInfoSessionTrade(symbol, (ENUM_DAY_OF_WEEK)day_of_week, session_index, from, to);
   
   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   JSONObject* info_jo = new JSONObject();
   info_jo.put("From", new JSONNumber((long)from));
   info_jo.put("To", new JSONNumber((long)to));
   result_value_jo.put("Result", info_jo);
   
   return CreateSuccessResponse(result_value_jo);
}

string Execute_MarketBookAdd()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   
   bool result = MarketBookAdd(symbol);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_MarketBookRelease()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   
   bool result = MarketBookRelease(symbol);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_PositionModify()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "Ticket", ticket);
   GET_DOUBLE_JSON_VALUE(jo, "Sl", sl);
   GET_DOUBLE_JSON_VALUE(jo, "Tp", tp);
   
   CTrade trade;
   bool ok = trade.PositionModify(ticket,sl,tp);
   Print("command PositionModify: result = ", ok);
   
   return CreateSuccessResponse(new JSONBool(ok));
}

string Execute_PositionClosePartialBySymbol()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_DOUBLE_JSON_VALUE(jo, "Volume", volume);
   GET_ULONG_JSON_VALUE(jo, "Deviation", deviation);

   CTrade trade;
   bool ok = trade.PositionClosePartial(symbol, volume, deviation);
#ifdef __DEBUG_LOG__      
   Print("command PositionClosePartial (1): result = ", ok);
#endif

   return CreateSuccessResponse(new JSONBool(ok));
}

string Execute_PositionClosePartialByTicket()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "Ticket", ticket);
   GET_DOUBLE_JSON_VALUE(jo, "Volume", volume);
   GET_ULONG_JSON_VALUE(jo, "Deviation", deviation);

   CTrade trade;
   bool ok = trade.PositionClosePartial(ticket, volume, deviation);
#ifdef __DEBUG_LOG__      
   Print("command PositionClosePartial (2): result = ", ok);
#endif

   return CreateSuccessResponse(new JSONBool(ok));
}

string Execute_PositionOpen()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "OrderType", order_type);
   GET_DOUBLE_JSON_VALUE(jo, "Volume", volume);
   GET_DOUBLE_JSON_VALUE(jo, "Price", price);
   GET_DOUBLE_JSON_VALUE(jo, "Sl", sl);
   GET_DOUBLE_JSON_VALUE(jo, "Tp", tp);
   
   //Comment
   string comment;
   if (jo.p.getValue("Comment") != NULL)
   {
      comment = jo.p.getString("Comment");
   }   
   
#ifdef __DEBUG_LOG__
   PrintFormat("%s: symbol = %s, order_type = %d, volume = %f, price = %f, sl = %f, tp = %f, comment = %s", 
      __FUNCTION__, symbol, order_type, volume, price, sl, tp, comment);
#endif
   
   CTrade trade;
   bool ok = trade.PositionOpen(symbol, (ENUM_ORDER_TYPE)order_type, volume, price, sl, tp, comment);

#ifdef __DEBUG_LOG__
   Print("command PositionOpen: result = ", ok);
#endif

   return CreateSuccessResponse(new JSONBool(ok));
}

string Execute_PositionOpen2()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "OrderType", order_type);
   GET_DOUBLE_JSON_VALUE(jo, "Volume", volume);
   GET_DOUBLE_JSON_VALUE(jo, "Price", price);
   GET_DOUBLE_JSON_VALUE(jo, "Sl", sl);
   GET_DOUBLE_JSON_VALUE(jo, "Tp", tp);
   
   //Comment
   string comment;
   if (jo.p.getValue("Comment") != NULL)
   {
      comment = jo.p.getString("Comment");
   }   
   
#ifdef __DEBUG_LOG__
   PrintFormat("%s: symbol = %s, order_type = %d, volume = %f, price = %f, sl = %f, tp = %f, comment = %s", 
      __FUNCTION__, symbol, order_type, volume, price, sl, tp, comment);
#endif
   
   CTrade trade;
   bool ok = trade.PositionOpen(symbol, (ENUM_ORDER_TYPE)order_type, volume, price, sl, tp, comment);

#ifdef __DEBUG_LOG__
   Print("command PositionOpen: result = ", ok);
#endif

   MqlTradeResult trade_result={0};
   trade.Result(trade_result);

   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", MqlTradeResultToJson(trade_result));
   return CreateSuccessResponse(result_value_jo);
}

string Execute_BacktestingReady()
{
   bool retVal = false;
   if (IsTesting())
   {
      Print("Remote client is ready for backteting");
      IsRemoteReadyForTesting = true;
      retVal = true;
   }
   
   return CreateSuccessResponse(new JSONBool(retVal));
}

string Execute_IsTesting()
{
   return CreateSuccessResponse(new JSONBool(IsTesting()));
}

string Execute_Print()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "PrintMsg", printMsg);
   
   Print(printMsg);   
   return CreateSuccessResponse(new JSONBool(true));
}

string Execute_PositionSelectByTicket()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "Ticket", ticket);
   
   bool result = PositionSelectByTicket(ticket);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ObjectCreate()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_INT_JSON_VALUE(jo, "Type", type);
   GET_INT_JSON_VALUE(jo, "Nwin", nwin);
   
   CHECK_JSON_VALUE(jo, "Times");
   CHECK_JSON_VALUE(jo, "Prices");
   
   datetime times[30];
   double prices[30];
   ArrayInitialize(times, 0);
   ArrayInitialize(prices, EMPTY_VALUE);
   
   JSONArray* times_jo = jo.p.getArray("Times");
   for(int i = 0; i < times_jo.size(); i++)
      times[i] = (datetime) times_jo.getInt(i);
      
   JSONArray* prices_jo = jo.p.getArray("Prices");
   for(int i = 0; i < prices_jo.size(); i++)
      prices[i] = prices_jo.getDouble(i);
      
   bool result = ObjectCreate(chartId, name, (ENUM_OBJECT)type, nwin, 
            times[0], prices[0], times[1], prices[1], times[2], prices[2],
            times[3], prices[3], times[4], prices[4], times[5], prices[5],
            times[6], prices[6], times[7], prices[7], times[8], prices[8],
            times[9], prices[9], times[10], prices[10], times[11], prices[11],
            times[12], prices[12], times[13], prices[13], times[14], prices[14],
            times[15], prices[15], times[16], prices[16], times[17], prices[17],
            times[18], prices[18], times[19], prices[19], times[20], prices[20],
            times[21], prices[21], times[22], prices[22], times[23], prices[23],
            times[24], prices[24], times[25], prices[25], times[26], prices[26],
            times[27], prices[27], times[28], prices[28], times[29], prices[29]);

   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ObjectName()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_INT_JSON_VALUE(jo, "Pos", pos);
   GET_INT_JSON_VALUE(jo, "SubWindow", subWindow);
   GET_INT_JSON_VALUE(jo, "Type", type);
   
   string result = ObjectName(chartId, pos, subWindow, type);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_ObjectDelete()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);

   bool result = ObjectDelete(chartId, name);
   return CreateSuccessResponse(new JSONBool(result)); 
}

string Execute_ObjectsDeleteAll()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_INT_JSON_VALUE(jo, "SubWindow", subWindow);
   GET_INT_JSON_VALUE(jo, "Type", type);

   int result = ObjectsDeleteAll(chartId, subWindow, type);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ObjectFind()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);

   int result = ObjectFind(chartId, name);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ObjectGetTimeByValue()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_DOUBLE_JSON_VALUE(jo, "Value", value);
   GET_INT_JSON_VALUE(jo, "LineId", lineId);
   
   int result = (int)ObjectGetTimeByValue(chartId, name, value, lineId);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ObjectGetValueByTime()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_INT_JSON_VALUE(jo, "Time", time);
   GET_INT_JSON_VALUE(jo, "LineId", lineId);
   
   double result = ObjectGetValueByTime(chartId, name, (datetime)time, lineId);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ObjectMove()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_INT_JSON_VALUE(jo, "PointIndex", pointIndex);
   GET_INT_JSON_VALUE(jo, "Time", time);
   GET_DOUBLE_JSON_VALUE(jo, "Price", price);
   
   bool result = ObjectMove(chartId, name, pointIndex, (datetime)time, price);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ObjectsTotal()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_INT_JSON_VALUE(jo, "SubWindow", subWindow);
   GET_INT_JSON_VALUE(jo, "Type", type);
   
   int result = ObjectsTotal(chartId, subWindow, type);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ObjectGetDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_INT_JSON_VALUE(jo, "PropId", propId);

   double result = ObjectGetDouble(chartId, name, (ENUM_OBJECT_PROPERTY_DOUBLE)propId);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ObjectGetInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_INT_JSON_VALUE(jo, "PropId", propId);
   
   long result = ObjectGetInteger(chartId, name, (ENUM_OBJECT_PROPERTY_INTEGER)propId);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ObjectGetString()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_INT_JSON_VALUE(jo, "PropId", propId);
   
   string result = ObjectGetString(chartId, name, (ENUM_OBJECT_PROPERTY_STRING)propId);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_ObjectSetDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_INT_JSON_VALUE(jo, "PropId", propId);
   GET_DOUBLE_JSON_VALUE(jo, "PropValue", propValue);
   
   bool result = ObjectSetDouble(chartId, name, (ENUM_OBJECT_PROPERTY_DOUBLE)propId, propValue);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ObjectSetInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_INT_JSON_VALUE(jo, "PropId", propId);
   GET_LONG_JSON_VALUE(jo, "PropValue", propValue);

   long result = ObjectSetInteger(chartId, name, (ENUM_OBJECT_PROPERTY_INTEGER)propId, propValue);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ObjectSetString()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chartId);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_INT_JSON_VALUE(jo, "PropId", propId);
   GET_STRING_JSON_VALUE(jo, "PropValue", propValue);
   
   bool result = ObjectSetString(chartId, name, (ENUM_OBJECT_PROPERTY_STRING)propId, propValue);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_iAC()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   
   int result = iAC(symbol, (ENUM_TIMEFRAMES)period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iAD()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "AppliedVolume", applied_volume);
   
   int result = iAD(symbol, (ENUM_TIMEFRAMES)period, (ENUM_APPLIED_VOLUME)applied_volume);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iADX()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "AdxPeriod", adx_period);
   
   int result = iADX(symbol, (ENUM_TIMEFRAMES)period, adx_period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iADXWilder()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "AdxPeriod", adx_period);
   
   int result = iADXWilder(symbol, (ENUM_TIMEFRAMES)period, adx_period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iAlligator()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "JawPeriod", jaw_period);
   GET_INT_JSON_VALUE(jo, "JawShift", jaw_shift);
   GET_INT_JSON_VALUE(jo, "TeethPeriod", teeth_period);
   GET_INT_JSON_VALUE(jo, "TeethShift", teeth_shift);
   GET_INT_JSON_VALUE(jo, "LipsPeriod", lips_period);
   GET_INT_JSON_VALUE(jo, "LipsShift", lips_shift);
   GET_INT_JSON_VALUE(jo, "MaMethod", ma_method);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iAlligator(symbol, (ENUM_TIMEFRAMES)period, jaw_period, jaw_shift, teeth_period, teeth_shift, 
         lips_period, lips_shift, (ENUM_MA_METHOD)ma_method, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iAMA()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "AmaPeriod", ama_period);
   GET_INT_JSON_VALUE(jo, "FastMaPeriod", fast_ma_period);
   GET_INT_JSON_VALUE(jo, "SlowMaPeriod", slow_ma_period);
   GET_INT_JSON_VALUE(jo, "AmaShift", ama_shift);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iAMA(symbol, (ENUM_TIMEFRAMES)period, ama_period, fast_ma_period, slow_ma_period, ama_shift, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iAO()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   
   int result = iAO(symbol, (ENUM_TIMEFRAMES)period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iATR()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   
   int result = iATR(symbol, (ENUM_TIMEFRAMES)period, ma_period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iBearsPower()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   
   int result = iBearsPower(symbol, (ENUM_TIMEFRAMES)period, ma_period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iBands()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "BandsPeriod", bands_period);
   GET_INT_JSON_VALUE(jo, "BandsShift", bands_shift);
   GET_DOUBLE_JSON_VALUE(jo, "Deviation", deviation);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iBands(symbol, (ENUM_TIMEFRAMES)period, bands_period, bands_shift, deviation, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iBullsPower()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   
   int result = iBullsPower(symbol, (ENUM_TIMEFRAMES)period, ma_period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iCCI()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iCCI(symbol, (ENUM_TIMEFRAMES)period, ma_period, (ENUM_APPLIED_PRICE) applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iChaikin()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "FastMaPeriod", fast_ma_period);
   GET_INT_JSON_VALUE(jo, "SlowMaPeriod", slow_ma_period);
   GET_INT_JSON_VALUE(jo, "MaMethod", ma_method);
   GET_INT_JSON_VALUE(jo, "AppliedVolume", applied_volume);
   
   int result = iChaikin(symbol, (ENUM_TIMEFRAMES)period, fast_ma_period, slow_ma_period, (ENUM_MA_METHOD)ma_method, (ENUM_APPLIED_VOLUME) applied_volume);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iDEMA()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "MaShift", ma_shift);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iDEMA(symbol, (ENUM_TIMEFRAMES)period, ma_period, ma_shift, (ENUM_APPLIED_PRICE) applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iDeMarker()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   
   int result = iDeMarker(symbol, (ENUM_TIMEFRAMES)period, ma_period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iEnvelopes()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "MaShift", ma_shift);
   GET_INT_JSON_VALUE(jo, "MaMethod", ma_method);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   GET_DOUBLE_JSON_VALUE(jo, "Deviation", deviation);
   
   int result = iEnvelopes(symbol, (ENUM_TIMEFRAMES)period, ma_period, ma_shift, (ENUM_MA_METHOD)ma_method, (ENUM_APPLIED_PRICE)applied_price, deviation);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iForce()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "MaMethod", ma_method);
   GET_INT_JSON_VALUE(jo, "AppliedVolume", applied_volume);
   
   int result = iForce(symbol, (ENUM_TIMEFRAMES)period, ma_period, (ENUM_MA_METHOD)ma_method, (ENUM_APPLIED_VOLUME)applied_volume);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iFractals()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   
   int result = iFractals(symbol, (ENUM_TIMEFRAMES)period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iFrAMA()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "MaShift", ma_shift);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iFrAMA(symbol, (ENUM_TIMEFRAMES)period, ma_period, ma_shift, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}
   
string Execute_iGator()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "JawPeriod", jaw_period);
   GET_INT_JSON_VALUE(jo, "JawShift", jaw_shift);
   GET_INT_JSON_VALUE(jo, "TeethPeriod", teeth_period);
   GET_INT_JSON_VALUE(jo, "TeethShift", teeth_shift);
   GET_INT_JSON_VALUE(jo, "LipsPeriod", lips_period);
   GET_INT_JSON_VALUE(jo, "LipsShift", lips_shift);
   GET_INT_JSON_VALUE(jo, "MaMethod", ma_method);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iGator(symbol, (ENUM_TIMEFRAMES)period, jaw_period, jaw_shift, 
      teeth_period, teeth_shift, lips_period, lips_shift, 
      (ENUM_MA_METHOD)ma_method, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iIchimoku()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "TenkanSen", tenkan_sen);
   GET_INT_JSON_VALUE(jo, "KijunSen", kijun_sen);
   GET_INT_JSON_VALUE(jo, "SenkouSpanB", senkou_span_b);
   
   int result = iIchimoku(symbol, (ENUM_TIMEFRAMES)period, tenkan_sen, kijun_sen, senkou_span_b);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iBWMFI()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "AppliedVolume", applied_volume);

   int result = iBWMFI(symbol, (ENUM_TIMEFRAMES)period, (ENUM_APPLIED_VOLUME)applied_volume);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iMomentum()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MomPeriod", mom_period);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iMomentum(symbol, (ENUM_TIMEFRAMES)period, mom_period, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iMFI()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "AppliedVolume", applied_volume);
   
   int result = iMFI(symbol, (ENUM_TIMEFRAMES)period, ma_period, (ENUM_APPLIED_VOLUME)applied_volume);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iMA()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "MaShift", ma_shift);
   GET_INT_JSON_VALUE(jo, "MaMethod", ma_method);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iMA(symbol, (ENUM_TIMEFRAMES)period, ma_period, ma_shift, (ENUM_MA_METHOD)ma_method, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iOsMA()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "FastEmaPeriod", fast_ema_period);
   GET_INT_JSON_VALUE(jo, "SlowEmaPeriod", slow_ema_period);
   GET_INT_JSON_VALUE(jo, "SignalPeriod", signal_period);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iOsMA(symbol, (ENUM_TIMEFRAMES)period, fast_ema_period, slow_ema_period, signal_period, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iMACD()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "FastEmaPeriod", fast_ema_period);
   GET_INT_JSON_VALUE(jo, "SlowEmaPeriod", slow_ema_period);
   GET_INT_JSON_VALUE(jo, "SignalPeriod", signal_period);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iMACD(symbol, (ENUM_TIMEFRAMES)period, fast_ema_period, slow_ema_period, signal_period, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iOBV()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "AppliedVolume", applied_volume);
   
   int result = iOBV(symbol, (ENUM_TIMEFRAMES)period, (ENUM_APPLIED_VOLUME)applied_volume);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iSAR()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_DOUBLE_JSON_VALUE(jo, "Step", step);
   GET_DOUBLE_JSON_VALUE(jo, "Maximum", maximum);
   
   int result = iSAR(symbol, (ENUM_TIMEFRAMES)period, step, maximum);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iRSI()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iRSI(symbol, (ENUM_TIMEFRAMES)period, ma_period, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iRVI()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   
   int result = iRVI(symbol, (ENUM_TIMEFRAMES)period, ma_period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iStdDev()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "MaShift", ma_shift);
   GET_INT_JSON_VALUE(jo, "MaMethod", ma_method);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iStdDev(symbol, (ENUM_TIMEFRAMES)period, ma_period, ma_shift, (ENUM_MA_METHOD)ma_method, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iStochastic()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "Kperiod", Kperiod);
   GET_INT_JSON_VALUE(jo, "Dperiod", Dperiod);
   GET_INT_JSON_VALUE(jo, "Slowing", slowing);
   GET_INT_JSON_VALUE(jo, "MaMethod", ma_method);
   GET_INT_JSON_VALUE(jo, "PriceField", price_field);
   
   int result = iStochastic(symbol, (ENUM_TIMEFRAMES)period, Kperiod, Dperiod, slowing, (ENUM_MA_METHOD)slowing, (ENUM_STO_PRICE)price_field);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iTEMA()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "MaShift", ma_shift);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iTEMA(symbol, (ENUM_TIMEFRAMES)period, ma_period, ma_shift, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iTriX()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "MaPeriod", ma_period);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);
   
   int result = iTriX(symbol, (ENUM_TIMEFRAMES)period, ma_period, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iWPR()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "CalcPeriod", calc_period);
   
   int result = iWPR(symbol, (ENUM_TIMEFRAMES)period, calc_period);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iVIDyA()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "CmoPeriod", cmo_period);
   GET_INT_JSON_VALUE(jo, "EmaPeriod", ema_period);
   GET_INT_JSON_VALUE(jo, "MaShift", ma_shift);
   GET_INT_JSON_VALUE(jo, "AppliedPrice", applied_price);

   int result = iVIDyA(symbol, (ENUM_TIMEFRAMES)period, cmo_period, ema_period, ma_shift, (ENUM_APPLIED_PRICE)applied_price);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_iVolumes()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "AppliedVolume", applied_volume);
   
   int result = iVolumes(symbol, (ENUM_TIMEFRAMES)period, (ENUM_APPLIED_VOLUME)applied_volume);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_TimeCurrent()
{
   long result = TimeCurrent();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_TimeTradeServer()
{
   long result = TimeTradeServer();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_TimeLocal()
{
   long result = TimeLocal();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_TimeGMT()
{
   long result = TimeGMT();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_IndicatorRelease()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "IndicatorHandle", indicator_handle);
   
   bool result = IndicatorRelease(indicator_handle);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_GetLastError()
{
   int last_error = GetLastError();
   return CreateSuccessResponse(new JSONNumber(last_error));
}

string Execute_Alert()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Message", message);
   
   Alert(message);
   return CreateSuccessResponse();   
}

string Execute_ResetLastError()
{
   ResetLastError();
   return CreateSuccessResponse();
}

string Execute_GlobalVariableCheck()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   
   bool result = GlobalVariableCheck(name);
   return CreateSuccessResponse(new JSONBool(result));   
}

string Execute_GlobalVariableTime()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   
   datetime result = GlobalVariableTime(name);
   return CreateSuccessResponse(new JSONNumber((long)result));
}

string Execute_GlobalVariableDel()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   
   bool result = GlobalVariableDel(name);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_GlobalVariableGet()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Name", name);

   double result = GlobalVariableGet(name);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_GlobalVariableName()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "Index", index);
   
   string result = GlobalVariableName(index);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_GlobalVariableSet()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_DOUBLE_JSON_VALUE(jo, "Value", value);

   datetime result = GlobalVariableSet(name, value);
   return CreateSuccessResponse(new JSONNumber((long)result));
}

string Execute_GlobalVariablesFlush()
{
   GlobalVariablesFlush();
   return CreateSuccessResponse();
}

string Execute_ChartOpen()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   
   long result = ChartOpen(symbol, (ENUM_TIMEFRAMES)timeframe);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartFirst()
{
   long result = ChartFirst();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartNext()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   
   long result = ChartNext(chart_id);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartClose()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   
   bool result = ChartClose(chart_id);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartSymbol()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   
   string result = ChartSymbol(chart_id);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_ChartPeriod()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   
   ENUM_TIMEFRAMES result = ChartPeriod(chart_id);
   return CreateSuccessResponse(new JSONNumber((int)result));
}

string Execute_ChartSetDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   GET_DOUBLE_JSON_VALUE(jo, "Value", value);
   
   bool result = ChartSetDouble(chart_id, (ENUM_CHART_PROPERTY_DOUBLE)prop_id, value);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartSetInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   GET_LONG_JSON_VALUE(jo, "Value", value);

   bool result = ChartSetInteger(chart_id, (ENUM_CHART_PROPERTY_INTEGER)prop_id, value);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartSetString()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   GET_STRING_JSON_VALUE(jo, "Value", value);
   
   bool result = ChartSetString(chart_id, (ENUM_CHART_PROPERTY_STRING)prop_id, value);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartGetDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   GET_INT_JSON_VALUE(jo, "SubWindow", sub_window);

   double result = ChartGetDouble(chart_id, (ENUM_CHART_PROPERTY_DOUBLE)prop_id, sub_window);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartGetInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   GET_INT_JSON_VALUE(jo, "SubWindow", sub_window);
   
   long result = ChartGetInteger(chart_id, (ENUM_CHART_PROPERTY_INTEGER)prop_id, sub_window);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartGetString()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "PropId", prop_id);
   
   string result = ChartGetString(chart_id, (ENUM_CHART_PROPERTY_STRING)prop_id);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_ChartNavigate()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "Position", position);
   GET_INT_JSON_VALUE(jo, "Shift", shift);
   
   bool result = ChartNavigate(chart_id, (ENUM_CHART_POSITION)position, shift);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartIndicatorDelete()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "SubWindow", sub_window);
   GET_STRING_JSON_VALUE(jo, "IndicatorShortname", indicator_shortname);
   
#ifdef __DEBUG_LOG__
   PrintFormat("%s: chart_id = %I64d, sub_window = %d, indicator_shortname = %s", __FUNCTION__, chart_id, sub_window, indicator_shortname);
#endif

   bool result = ChartIndicatorDelete( chart_id, sub_window, indicator_shortname);
   
#ifdef __DEBUG_LOG__
   PrintFormat("%s: result = %s", __FUNCTION__, BoolToString(result));
#endif

   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartIndicatorName()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "SubWindow", sub_window);
   GET_INT_JSON_VALUE(jo, "Index", index);   
   
#ifdef __DEBUG_LOG__
   PrintFormat("%s: chart_id = %I64d, sub_window = %d, index = %d", __FUNCTION__, chart_id, sub_window, index);
#endif

   string result = ChartIndicatorName(chart_id, sub_window, index);

#ifdef __DEBUG_LOG__
   PrintFormat("%s: result = %s", __FUNCTION__, result);
#endif

   return CreateSuccessResponse(new JSONString(result));
}

string Execute_ChartIndicatorsTotal()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "SubWindow", sub_window);
   
   int result = ChartIndicatorsTotal(chart_id, sub_window);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartWindowOnDropped()
{
   int result = ChartWindowOnDropped();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartPriceOnDropped()
{
   double result = ChartPriceOnDropped();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartTimeOnDropped()
{
   datetime result = ChartTimeOnDropped();
   return CreateSuccessResponse(new JSONNumber((long)result));
}

string Execute_ChartXOnDropped()
{
   int result = ChartXOnDropped();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartYOnDropped()
{
   int result = ChartYOnDropped();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartSetSymbolPeriod()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Period", period);

   bool result = ChartSetSymbolPeriod(chart_id, symbol, (ENUM_TIMEFRAMES)period);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartScreenShot()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_STRING_JSON_VALUE(jo, "Filename", filename);
   GET_INT_JSON_VALUE(jo, "Width", width);
   GET_INT_JSON_VALUE(jo, "Height", height);
   GET_INT_JSON_VALUE(jo, "AlignMode", align_mode);
   
   bool result = ChartScreenShot(chart_id, filename, width, height, (ENUM_ALIGN_MODE)align_mode);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_WindowBarsPerChart()
{
   return CreateErrorResponse(-1, "Unsupported function");
}

string Execute_ChartIndicatorAdd()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "SubWindow", sub_window);
   GET_INT_JSON_VALUE(jo, "IndicatorHandle", indicator_handle);
   
   bool result = ChartIndicatorAdd(chart_id, sub_window, indicator_handle);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartIndicatorGet()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "SubWindow", sub_window);
   GET_STRING_JSON_VALUE(jo, "IndicatorShortname", indicator_shortname);

   int result = ChartIndicatorGet( chart_id, sub_window, indicator_shortname);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartApplyTemplate()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_STRING_JSON_VALUE(jo, "TemplateFileName", TemplateFileName);
   
   StringReplace(TemplateFileName, "\\", "\\\\");
   bool result = ChartApplyTemplate(chart_id, TemplateFileName);
   ChartRedraw(chart_id);
   
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartSaveTemplate()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_STRING_JSON_VALUE(jo, "TemplateFileName", TemplateFileName);
   
   StringReplace(TemplateFileName, "\\", "\\\\");
   bool result = ChartSaveTemplate(chart_id, TemplateFileName);
   ChartRedraw(chart_id);
   
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_ChartWindowFind()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_STRING_JSON_VALUE(jo, "IndicatorShortname", indicator_shortname);
   
   int result = ChartWindowFind(chart_id, indicator_shortname);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_TerminalInfoString()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", propertyId);
   
   string result = TerminalInfoString((ENUM_TERMINAL_INFO_STRING)propertyId);
   return CreateSuccessResponse(new JSONString(result));
}

string Execute_GlobalVariableTemp()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Name", name);

   bool result = GlobalVariableTemp(name);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_GlobalVariableSetOnCondition()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Name", name);
   GET_DOUBLE_JSON_VALUE(jo, "Value", value);
   GET_DOUBLE_JSON_VALUE(jo, "CheckValue", check_value);
   
   bool result = GlobalVariableSetOnCondition(name, value, check_value);
   return CreateSuccessResponse(new JSONBool(result));
}

string Execute_GlobalVariablesDeleteAll()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "PrefixName", prefix_name);
   GET_INT_JSON_VALUE(jo, "LimitData", limit_data);
   
   int result = GlobalVariablesDeleteAll(prefix_name, (datetime)limit_data);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_GlobalVariablesTotal()
{
   int result = GlobalVariablesTotal();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_UnlockTicks()
{
   if (!IsTesting())
   {
      Print("WARNING: function UnlockTicks can be used only for backtesting");
      return CreateErrorResponse(-1, "UnlockTicks can be used only for backtesting");
   }
   
   return CreateSuccessResponse();
}

string Execute_PositionCloseAll()
{
   int result = PositionCloseAll();
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_TesterStop()
{
   if (!IsTesting())
   {
      Print("WARNING: function TesterStop can be used only for backtesting");
      return CreateErrorResponse(-1, "TesterStop can be used only for backtesting");
   }

   TesterStop();
   return CreateSuccessResponse();
}

string Execute_TerminalInfoInteger()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", propertyId);
   
   int result = TerminalInfoInteger((ENUM_TERMINAL_INFO_INTEGER)propertyId);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_TerminalInfoDouble()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "PropertyId", propertyId);
   
   double result = TerminalInfoDouble((ENUM_TERMINAL_INFO_DOUBLE)propertyId);
   return CreateSuccessResponse(new JSONNumber(result));
}

string Execute_ChartId()
{
   long id = ChartID();
   return CreateSuccessResponse(new JSONNumber(id));
}

string Execute_ChartRedraw()
{
   GET_JSON_PAYLOAD(jo);
   GET_INT_JSON_VALUE(jo, "ChartId", chart_id);
   
   ChartRedraw(chart_id);
   return CreateSuccessResponse();
}

string Execute_CopyTicks()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_UINT_JSON_VALUE(jo, "Flags", flags);
   GET_INT_JSON_VALUE(jo, "From", from);
   GET_INT_JSON_VALUE(jo, "Count", count);
   
   MqlTick ticks[];
   int received = CopyTicks(symbol, ticks, flags, from, count); 
   if(received == -1)
      return CreateErrorResponse(GetLastError(), "CopyTicks failed");
   
   JSONArray* jaTicks = new JSONArray();
   for(int i = 0; i < received; i++)
      jaTicks.put(i, MqlTickToJson(ticks[i]));
        
   return CreateSuccessResponse(jaTicks);
}

string Execute_iCustom()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   GET_INT_JSON_VALUE(jo, "Timeframe", timeframe);
   GET_STRING_JSON_VALUE(jo, "Name", name);

   int result;
   
   if (jo.p.getValue("Params") == NULL)
   {
      result = iCustom(symbol, (ENUM_TIMEFRAMES)timeframe, name);
   }
   else
   {
      JSONArray *jaParams = jo.p.getArray("Params");
      int size = jaParams.size();

      if (size < 0 || size > 10)
         return CreateErrorResponse(-1, "Parameter's count is out of range.");

      if (jo.p.getValue("ParamsType") == NULL)
         return CreateErrorResponse(-1, "Undefinded mandatory parameter ParamsType");

      int paramsType =  jo.p.getInt("ParamsType");
      switch (paramsType)
      {
      case 0: //Int
      {
         int intParams[];
         ArrayResize(intParams, size);
         for (int i = 0; i < size; i++)
            intParams[i] = jaParams.getInt(i);
         result = iCustomT(symbol, (ENUM_TIMEFRAMES)timeframe, name, intParams, size);
      }
      break;
      case 1: //Double
      {
         double doubleParams[];
         for (int i = 0; i < size; i++)
            doubleParams[i] = jaParams.getDouble(i);
         ArrayResize(doubleParams, size);
         result = iCustomT(symbol, (ENUM_TIMEFRAMES)timeframe, name, doubleParams, size);
      }
      break;
      case 2: //String
      {
         string stringParams[];
         ArrayResize(stringParams, size);
         for (int i = 0; i < size; i++)
            stringParams[i] = jaParams.getString(i);
         result = iCustomT(symbol, (ENUM_TIMEFRAMES)timeframe, name, stringParams, size);
      }
      break;
      case 3: //Boolean
      {
         bool boolParams[];
         ArrayResize(boolParams, size);
          for (int i = 0; i < size; i++)
            boolParams[i] = jaParams.getBool(i);
         result = iCustomT(symbol, (ENUM_TIMEFRAMES)timeframe, name, boolParams, size);
      }
      break;
      default:
         return CreateErrorResponse(-1, "Unsupported type of iCustom parameters.");
      }
   }

   return CreateSuccessResponse(new JSONNumber((long)result));
}

string Execute_OrderSend()
{
   GET_JSON_PAYLOAD(jo);
   CHECK_JSON_VALUE(jo, "TradeRequest");
   JSONObject* trade_request_jo = jo.p.getObject("TradeRequest");

   MqlTradeRequest trade_request;
   bool converted = JsonToMqlTradeRequest(trade_request_jo, trade_request);
   if (converted == false)
      return CreateErrorResponse(-1, "Failed to parse parameter TradeRequest");
   
   MqlTradeResult trade_result;
   bool ok = OrderSend(trade_request, trade_result);
   
   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", MqlTradeResultToJson(trade_result));
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: return value = %s", __FUNCTION__, ok ? "true" : "false");
#endif    
      
   return CreateSuccessResponse(result_value_jo);
}

string Execute_OrderSendAsync()
{
   GET_JSON_PAYLOAD(jo);
   CHECK_JSON_VALUE(jo, "TradeRequest");
   JSONObject* trade_request_jo = jo.p.getObject("TradeRequest");
      
   MqlTradeRequest trade_request;
   bool converted = JsonToMqlTradeRequest(trade_request_jo, trade_request);
   if (converted == false)
      return CreateErrorResponse(-1, "Failed to parse parameter TradeRequest");
   
   MqlTradeResult trade_result;
   bool ok = OrderSendAsync(trade_request, trade_result);
   
   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", MqlTradeResultToJson(trade_result));
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: return value = %s", __FUNCTION__, ok ? "true" : "false");
#endif    
      
   return CreateSuccessResponse(result_value_jo);
}

string Execute_OrderCheck()
{
   GET_JSON_PAYLOAD(jo);
   CHECK_JSON_VALUE(jo, "TradeRequest");
   JSONObject* trade_request_jo = jo.p.getObject("TradeRequest");
      
   MqlTradeRequest trade_request;
   JsonToMqlTradeRequest(trade_request_jo, trade_request);
   
   MqlTradeCheckResult trade_check_result;
   bool ok = OrderCheck(trade_request, trade_check_result);
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: return value = %s", __FUNCTION__, ok ? "true" : "false");
#endif    

   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", MqlTradeCheckResultToJson(trade_check_result));
   
   return CreateSuccessResponse(result_value_jo);   
}

string Execute_MarketBookGet()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);
   
   MqlBookInfo info_array[];
   bool ok = MarketBookGet(symbol, info_array); 

#ifdef __DEBUG_LOG__   
   PrintFormat("%s: return value = %s.", __FUNCTION__, ok ? "true" : "false");
#endif
   
   if (!ok)
      return CreateErrorResponse(GetLastError(), "MarketBookGet failed");
   
   int size = ArraySize(info_array);
   JSONArray* book_ja = new JSONArray();
   for(int i = 0; i < size; i++)
      book_ja.put(i, MqlBookInfoToJson(info_array[i]));

#ifdef __DEBUG_LOG__   
   PrintFormat("%s: array size = %d.", __FUNCTION__, size);
#endif   
        
   return CreateSuccessResponse(book_ja);
}

string Execute_IndicatorCreate()
{
   GET_JSON_PAYLOAD(jo);

   //Symbol
   string symbol = NULL;
   if (jo.p.getValue("Symbol") != NULL)
      symbol = jo.p.getString("Symbol");
   
   GET_INT_JSON_VALUE(jo, "Period", period);
   GET_INT_JSON_VALUE(jo, "IndicatorType", indicator_type);
   
   int indicator_handle = -1;
   if (jo.p.getValue("Parameters") != NULL)
   {
      JSONArray parameters_ja = jo.p.getArray("Parameters");
      int size = parameters_ja.size();
      if (size > 0)
      {
         MqlParam parameters[];
         ArrayResize(parameters, size);
         
         for (int i = 0; i < size; i++)
         {
            JSONObject param_jo = parameters_ja.getObject(i);
            
            parameters[i].type = (ENUM_DATATYPE)param_jo.getInt("DataType");
            if (param_jo.getValue("IntegerValue") != NULL)
            {
               parameters[i].integer_value = param_jo.getLong("IntegerValue");
            }
            if (param_jo.getValue("DoubleValue") != NULL)
            {
               parameters[i].double_value = param_jo.getDouble("DoubleValue");
            }
            if (param_jo.getValue("StringValue") != NULL)
            {
               parameters[i].string_value = param_jo.getString("StringValue");
            }
         }
         
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: symbol = %s, period = %d, indicator_type = %d, size = %d.", __FUNCTION__, symbol, period, indicator_type, size);
#endif   
         
         indicator_handle = IndicatorCreate(symbol, (ENUM_TIMEFRAMES)period, (ENUM_INDICATOR)indicator_type, size, parameters);
      }
   }
   else
   {
#ifdef __DEBUG_LOG__   
      PrintFormat("%s: symbol = %s, period = %d, indicator_type = %d.", __FUNCTION__, symbol, period, indicator_type);
#endif
   
      indicator_handle = IndicatorCreate(symbol, (ENUM_TIMEFRAMES)period, (ENUM_INDICATOR)indicator_type);
   }
   
#ifdef __DEBUG_LOG__   
      PrintFormat("%s: result indicator handle = %d", __FUNCTION__, (ENUM_INDICATOR)indicator_handle);
#endif
   
   return CreateSuccessResponse(new JSONNumber(indicator_handle));
}

string Execute_ChartTimePriceToXY()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "SubWindow", sub_window);
   GET_INT_JSON_VALUE(jo, "Time", time);
   GET_DOUBLE_JSON_VALUE(jo, "Price", price);
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: chart_id = %d, sub_window = %d, time = %s", __FUNCTION__, chart_id, sub_window, TimeToString(time));
#endif

   int x,y;
   bool ok = ChartTimePriceToXY(chart_id, sub_window, time, price, x, y);
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: ok = %s, x = %d, y = %d", __FUNCTION__, BoolToString(ok), x, y);
#endif

   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   JSONObject* xy_jo = new JSONObject();
   xy_jo.put("X", new JSONNumber(x));
   xy_jo.put("Y", new JSONNumber(y));
   result_value_jo.put("Result", xy_jo);
   
   return CreateSuccessResponse(result_value_jo);  
}

string Execute_ChartXYToTimePrice()
{
   GET_JSON_PAYLOAD(jo);
   GET_LONG_JSON_VALUE(jo, "ChartId", chart_id);
   GET_INT_JSON_VALUE(jo, "X", x);
   GET_INT_JSON_VALUE(jo, "Y", y);
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: chart_id = %d, x = %d, y = %d", __FUNCTION__, chart_id, x, y);
#endif

   int sub_window;
   datetime time;
   double price;
   bool ok = ChartXYToTimePrice(chart_id, x, y, sub_window, time, price);
   
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: ok = %s, sub_window = %d, time = %s, price = %f", __FUNCTION__, BoolToString(ok), sub_window, TimeToString(time), price);
#endif

   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   JSONObject* time_price_jo = new JSONObject();
   time_price_jo.put("SubWindow", new JSONNumber(sub_window));
   time_price_jo.put("Time", new JSONNumber((long)time));
   time_price_jo.put("Price", new JSONNumber(price));
   result_value_jo.put("Result", time_price_jo);
   
   return CreateSuccessResponse(result_value_jo);
}

string Execute_PositionClose()
{
   GET_JSON_PAYLOAD(jo);
   GET_ULONG_JSON_VALUE(jo, "Ticket", ticket);
   GET_ULONG_JSON_VALUE(jo, "Deviation", deviation);

#ifdef __DEBUG_LOG__
   PrintFormat("%s: Ticket = %d, Deviation = %d", __FUNCTION__, ticket, deviation);
#endif

   CTrade trade;
   bool ok = trade.PositionClose(ticket, deviation);

   MqlTradeResult trade_result={0};
   trade.Result(trade_result);
   
#ifdef __DEBUG_LOG__
   Print("ExecuteRequest_PositionClose: retcode = ", trade.ResultRetcode());
#endif

   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", MqlTradeResultToJson(trade_result));

   return CreateSuccessResponse(result_value_jo);  
}

string Execute_SymbolInfoTick()
{
   GET_JSON_PAYLOAD(jo);
   GET_STRING_JSON_VALUE(jo, "Symbol", symbol);

#ifdef __DEBUG_LOG__   
   PrintFormat("%s: symbol = %s", __FUNCTION__, symbol);
#endif

   MqlTick tick={0};
   bool ok = SymbolInfoTick(symbol, tick);
     
#ifdef __DEBUG_LOG__   
   PrintFormat("%s: ok = %s", __FUNCTION__, BoolToString(ok));
#endif

   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", MqlTickToJson(tick));

   return CreateSuccessResponse(result_value_jo);      
}

string Execute_Buy()
{
   GET_JSON_PAYLOAD(jo);
   GET_DOUBLE_JSON_VALUE(jo, "Volume", volume);
   GET_DOUBLE_JSON_VALUE(jo, "Price", price);
   GET_DOUBLE_JSON_VALUE(jo, "Sl", sl);
   GET_DOUBLE_JSON_VALUE(jo, "Tp", tp);

   //Symbol
   string symbol = Symbol();
   if (jo.p.getValue("Symbol") != NULL)
      symbol = jo.p.getString("Symbol");   
     
   //Comment
   string comment = "";
   if (jo.p.getValue("Comment") != NULL)
      comment = jo.p.getString("Comment");

#ifdef __DEBUG_LOG__
   PrintFormat("%s: symbol = %s, volume = %f, price = %f, sl = %f, tp = %f, comment = %s", 
      __FUNCTION__, symbol, volume, price, sl, tp, comment);
#endif

   CTrade trade;
   bool ok = trade.Buy(volume, symbol, price, sl, tp, comment);

   MqlTradeResult trade_result={0};
   trade.Result(trade_result);

   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", MqlTradeResultToJson(trade_result));

   return CreateSuccessResponse(result_value_jo);   
}

string Execute_Sell()
{
   GET_JSON_PAYLOAD(jo);
   GET_DOUBLE_JSON_VALUE(jo, "Volume", volume);
   GET_DOUBLE_JSON_VALUE(jo, "Price", price);
   GET_DOUBLE_JSON_VALUE(jo, "Sl", sl);
   GET_DOUBLE_JSON_VALUE(jo, "Tp", tp);

   //Symbol
   string symbol=Symbol();
   if (jo.p.getValue("Symbol") != NULL)
      symbol = jo.p.getString("Symbol");   

   //Comment
   string comment="";
   if (jo.p.getValue("Comment") != NULL)
      comment = jo.p.getString("Comment");

#ifdef __DEBUG_LOG__
   PrintFormat("%s: symbol = %s, volume = %f, price = %f, sl = %f, tp = %f, comment = %s", 
      __FUNCTION__, symbol, volume, price, sl, tp, comment);
#endif

   CTrade trade;
   bool ok = trade.Sell(volume, symbol, price, sl, tp, comment);

   MqlTradeResult trade_result={0};
   trade.Result(trade_result);

   JSONObject* result_value_jo = new JSONObject();
   result_value_jo.put("RetVal", new JSONBool(ok));
   result_value_jo.put("Result", MqlTradeResultToJson(trade_result));

   return CreateSuccessResponse(result_value_jo); 
}

string Execute_GetSymbols()
{
   GET_JSON_PAYLOAD(jo);
   GET_BOOL_JSON_VALUE(jo, "Selected", selected);
   
   const int symbolsCount = SymbolsTotal(selected);
   JSONArray* jaSymbols = new JSONArray();
   int idx = 0;
   for(int idxSymbol = 0; idxSymbol < symbolsCount; idxSymbol++)
   {      
      string symbol = SymbolName(idxSymbol, selected);
      string firstChar = StringSubstr(symbol, 0, 1);
      if(firstChar != "#" && StringLen(symbol) == 6)
      {        
         jaSymbols.put(idx, new JSONString(symbol));
         idx++;
      } 
   }
   
   return CreateSuccessResponse(jaSymbols);
}

int PositionCloseAll()
{
   CTrade trade;
   int total = PositionsTotal();
   int count = 0;
   for(int i = 0; i < total; i++)
   {
      if (trade.PositionClose(PositionGetSymbol(i)))
         count++;
      Sleep(50); // Relax for 50 ms
   }
   return total;
}

bool OrderCloseAll()
{
   int count = PositionCloseAll();
   return count > 0;
}

string CreateErrorResponse(int code, string message_er)
{
   JSONValue* jsonError;
   if (code == 0)
      jsonError = new JSONString("0");
   else
      jsonError = new JSONNumber((long)code);
      
   JSONObject *joResponse = new JSONObject();
   joResponse.put("ErrorCode", jsonError);
   joResponse.put("ErrorMessage", new JSONString(message_er));
   
   string result = joResponse.toString();
   delete joResponse;
   return result;
}

string CreateSuccessResponse(JSONValue* responseBody = NULL)
{
   JSONObject joResponse;
   joResponse.put("ErrorCode", new JSONString("0"));
      
   if (responseBody != NULL)
      joResponse.put("Value", responseBody);   
   
   return joResponse.toString();
}

string CreateSuccessResponse(string responseName, JSONValue* responseBody)
{
   JSONObject joResponse;
   joResponse.put("ErrorCode", new JSONString("0"));
      
   if (responseBody != NULL)
      joResponse.put(responseName, responseBody);   
   
   return joResponse.toString();  
}


template<typename T>
int iCustomT(string symbol, ENUM_TIMEFRAMES timeframe, string name, T &p[], int count)
{
   switch(count)
   {
   case 0:
      return iCustom(symbol, timeframe, name);
   case 1:
      return iCustom(symbol, timeframe, name, p[0]);
   case 2:
      return iCustom(symbol, timeframe, name, p[0], p[1]);
   case 3:
      return iCustom(symbol, timeframe, name, p[0], p[1], p[2]);
   case 4:
      return iCustom(symbol, timeframe, name, p[0], p[1], p[2], p[3]);
   case 5:
      return iCustom(symbol, timeframe, name, p[0], p[1], p[2], p[3], p[4]);
   case 6:
      return iCustom(symbol, timeframe, name, p[0], p[1], p[2], p[3], p[4], p[5]);
   case 7:
      return iCustom(symbol, timeframe, name, p[0], p[1], p[2], p[3], p[4], p[5], p[6]);
   case 8:
      return iCustom(symbol, timeframe, name, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);
   case 9:
      return iCustom(symbol, timeframe, name, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8]);
   case 10:
      return iCustom(symbol, timeframe, name, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9]);
   default:
         return 0;
   }
}

JSONObject* MqlBookInfoToJson(MqlBookInfo& info)
{
    JSONObject *jo = new JSONObject();
    jo.put("type", new JSONNumber((int)info.type));
    jo.put("price", new JSONNumber(info.price));
    jo.put("volume", new JSONNumber(info.volume));
    jo.put("volume_real", new JSONNumber(info.volume_real));
    return jo;
}

//------------ MtProtocol -------------------------------------------------------

enum MtEventTypes
{
   ON_TRADE_TRANSACTION_EVENT = 1,
   ON_BOOK_EVENT              = 2,
   ON_TICK_EVENT              = 3,
   ON_LAST_TIME_BAR_EVENT     = 4,
   ON_LOCK_TICKS_EVENT        = 5
};

class MtObject
{
public:
   virtual JSONObject* CreateJson() const = 0;
};

class MtOnTradeTransactionEvent : public MtObject
{
public:
   MtOnTradeTransactionEvent(const MqlTradeTransaction& trans, const MqlTradeRequest& request, const MqlTradeResult& result)
   {
      _trans = trans;
      _request = request;
      _result = result;
   }
   
   virtual JSONObject* CreateJson() const
   {
      JSONObject *jo = new JSONObject();
      jo.put("Trans", MqlTradeTransactionToJson(_trans));
      jo.put("Request", MqlTradeRequestToJson(_request));
      jo.put("Result", MqlTradeResultToJson(_result));
      return jo;
   }
   
private:
   MqlTradeTransaction _trans;
   MqlTradeRequest _request;
   MqlTradeResult _result;
};

class MtOnBookEvent : public MtObject
{
public:
   MtOnBookEvent(const string& symbol)
   {
      _symbol = symbol;
   }
   
   virtual JSONObject* CreateJson() const
   {
      JSONObject *jo = new JSONObject();
      jo.put("Symbol", new JSONString(_symbol));
      return jo;
   }
   
private:
   string _symbol;
};

class MtQuote : public MtObject
{
public:
   MtQuote(string symbol, const MqlTick& tick)
   {
      _symbol = symbol;
      _tick = tick;
   }
   
   virtual JSONObject* CreateJson() const
   {
      JSONObject *jo = new JSONObject();
      jo.put("Tick", MqlTickToJson(_tick));
      jo.put("Instrument", new JSONString(_symbol));
      jo.put("ExpertHandle", new JSONNumber(ExpertHandle));
      return jo;
   }
   
private:
   string   _symbol;
   MqlTick  _tick;
};

class MtTimeBarEvent: public MtObject
{
public:
   MtTimeBarEvent(string symbol, const MqlRates& rates)
   {
      _symbol = symbol;
      _rates = rates;
   }
   
   virtual JSONObject* CreateJson() const
   {
      JSONObject *jo = new JSONObject();
      jo.put("Rates", MqlRatesToJson(_rates));
      jo.put("Instrument", new JSONString(_symbol));
      jo.put("ExpertHandle", new JSONNumber(ExpertHandle));
      return jo;
   }

private: 
   string _symbol;
   MqlRates _rates;
};

class MtLockTickEvent: public MtObject
{
public:
   MtLockTickEvent(string symbol)
   {
      _symbol = symbol;
   }
   
   virtual JSONObject* CreateJson() const
   {
      JSONObject *jo = new JSONObject();
      jo.put("Instrument", new JSONString(_symbol));
      return jo;
   }
   
private:
   string _symbol;
};

void SendMtEvent(MtEventTypes eventType, const MtObject& mtObj)
{
   JSONObject* json = mtObj.CreateJson();
   if (sendEvent(ExpertHandle, (int)eventType, json.toString(), _error))
   {
#ifdef __DEBUG_LOG__
      PrintFormat("%s: event = %s", __FUNCTION__, EnumToString(eventType));
      PrintFormat("%s: payload = %s", __FUNCTION__, json.toString());
#endif
   }
   else
   {
      PrintFormat("[ERROR] SendMtEvent: %s", _error);
   }
   
   delete json;
}

//-------------JSON converters -----------------------------------------
bool JsonToMqlTradeRequest(JSONObject *jo, MqlTradeRequest& request)
{
   //Action
   if (jo.getValue("Action") == NULL) return false;
   request.action = (ENUM_TRADE_REQUEST_ACTIONS) jo.getInt("Action");
   
   //Magic
   if (jo.getValue("Magic") == NULL) return false;
   request.magic = jo.getLong("Magic");

   //Order
   if (jo.getValue("Order") == NULL) return false;
   request.order = jo.getLong("Order");

   //Symbol
   if (jo.getValue("Symbol") != NULL)
   {
      StringInit(request.symbol, 100, 0);
      request.symbol = jo.getString("Symbol");
   }
   
   //Volume
   if (jo.getValue("Volume") == NULL) return false;
   request.volume = jo.getDouble("Volume");

   //Price
   if (jo.getValue("Price") == NULL) return false;
   request.price = jo.getDouble("Price");
   
   //Stoplimit
   if (jo.getValue("Stoplimit") == NULL) return false;
   request.stoplimit = jo.getDouble("Stoplimit");
   
   //Sl
   if (jo.getValue("Sl") == NULL) return false;
   request.sl = jo.getDouble("Sl");
   
   //Tp
   if (jo.getValue("Tp") == NULL) return false;
   request.tp = jo.getDouble("Tp");

   //Deviation
   if (jo.getValue("Deviation") == NULL) return false;
   request.deviation = jo.getLong("Deviation");

   //Type;
   if (jo.getValue("Type") == NULL) return false;
   request.type = (ENUM_ORDER_TYPE)jo.getInt("Type");
   
   //Type_filling
   if (jo.getValue("Type_filling") == NULL) return false;
   request.type_filling = (ENUM_ORDER_TYPE_FILLING)jo.getInt("Type_filling");

   //Type_time
   if (jo.getValue("Type_time") == NULL) return false;
   request.type_time = (ENUM_ORDER_TYPE_TIME)jo.getInt("Type_time");
   
   //Expiration
   if (jo.getValue("MtExpiration") == NULL) return false;
   request.expiration = (datetime)jo.getInt("MtExpiration");

   //Comment
   if (jo.getValue("Comment") != NULL)
   {
      StringInit(request.comment, 1000, 0);
      request.comment = jo.getString("Comment");
   }
   
   //Position
   if (jo.getValue("Position") == NULL) return false;
   request.position = jo.getLong("Position");

   //PositionBy
   if (jo.getValue("PositionBy") == NULL) return false;
   request.position_by = jo.getLong("PositionBy");
   
   return true;
}

JSONObject* MqlTickToJson(const MqlTick& tick)
{
   // MT5 can add some additional non-documented flags, so we need to filter required documented flags only
    int summ=0;
    if((tick.flags & TICK_FLAG_BID)==TICK_FLAG_BID)
      summ+=TICK_FLAG_BID;
    else if((tick.flags & TICK_FLAG_ASK)==TICK_FLAG_ASK)
      summ+=TICK_FLAG_ASK;
    else if((tick.flags & TICK_FLAG_LAST)==TICK_FLAG_LAST)
      summ+=TICK_FLAG_LAST;
    else if((tick.flags & TICK_FLAG_VOLUME)==TICK_FLAG_VOLUME)
      summ+=TICK_FLAG_VOLUME;
    else if((tick.flags & TICK_FLAG_BUY)==TICK_FLAG_BUY)
      summ+=TICK_FLAG_BUY;
    else if((tick.flags & TICK_FLAG_SELL)==TICK_FLAG_SELL)
      summ+=TICK_FLAG_SELL;

    JSONObject *jo = new JSONObject();
    jo.put("Time", new JSONNumber((long)tick.time));
    jo.put("Bid", new JSONNumber(tick.bid));
    jo.put("Ask", new JSONNumber(tick.ask));
    jo.put("Last", new JSONNumber(tick.last));
    jo.put("Volume", new JSONNumber(tick.volume));
    jo.put("VolumeReal", new JSONNumber(tick.volume_real));
    jo.put("Flags", new JSONNumber(summ));
    jo.put("TimeMsc", new JSONNumber(tick.time_msc));
    return jo;
}

JSONObject* MqlTradeResultToJson(const MqlTradeResult& result)
{
   JSONObject* jo = new JSONObject();
   jo.put("Retcode", new JSONNumber(result.retcode));
   jo.put("Deal", new JSONNumber(result.deal));
   jo.put("Order", new JSONNumber(result.order));
   jo.put("Volume", new JSONNumber(result.volume));
   jo.put("Price", new JSONNumber(result.price));
   jo.put("Bid", new JSONNumber(result.bid));
   jo.put("Ask", new JSONNumber(result.ask));
   jo.put("Comment", new JSONString(result.comment));
   jo.put("Request_id", new JSONNumber(result.request_id));
   return jo;
}

JSONObject* MqlTradeCheckResultToJson(const MqlTradeCheckResult& result)
{
   JSONObject* jo = new JSONObject();
   jo.put("Retcode", new JSONNumber(result.retcode));
   jo.put("Balance", new JSONNumber(result.balance));
   jo.put("Equity", new JSONNumber(result.equity));
   jo.put("Profit", new JSONNumber(result.profit));
   jo.put("Margin", new JSONNumber(result.margin));
   jo.put("Margin_free", new JSONNumber(result.margin_free));
   jo.put("Margin_level", new JSONNumber(result.margin_level));
   jo.put("Comment", new JSONString(result.comment));
   return jo;
}

JSONObject* MqlTradeTransactionToJson(const MqlTradeTransaction& trans)
{
   JSONObject *jo = new JSONObject();
   jo.put("Deal", new JSONNumber(trans.deal));
   jo.put("Order", new JSONNumber(trans.order));
   jo.put("Symbol", new JSONString(trans.symbol));
   jo.put("Type", new JSONNumber((int)trans.type));
   jo.put("OrderType", new JSONNumber((int)trans.order_type));
   jo.put("OrderState", new JSONNumber((int)trans.order_state));
   jo.put("DealType", new JSONNumber((int)trans.deal_type));
   jo.put("TimeType", new JSONNumber((int)trans.time_type));
   jo.put("MtTimeExpiration", new JSONNumber((int)trans.time_expiration));
   jo.put("Price", new JSONNumber(trans.price));
   jo.put("PriceTrigger", new JSONNumber(trans.price_trigger));
   jo.put("PriceSl", new JSONNumber(trans.price_sl));
   jo.put("PriceTp", new JSONNumber(trans.price_tp));
   jo.put("Volume", new JSONNumber(trans.volume));
   jo.put("Position", new JSONNumber(trans.position));
   jo.put("PositionBy", new JSONNumber(trans.position_by));
   return jo;
}

JSONObject* MqlTradeRequestToJson(const MqlTradeRequest& request)
{
   JSONObject *jo = new JSONObject();
   jo.put("Action", new JSONNumber((int)request.action));
   jo.put("Magic", new JSONNumber(request.magic));
   jo.put("Order", new JSONNumber(request.order));
   jo.put("Symbol", new JSONString(request.symbol));
   jo.put("Volume", new JSONNumber(request.volume));
   jo.put("Price", new JSONNumber(request.price));
   jo.put("Stoplimit", new JSONNumber(request.stoplimit));
   jo.put("Sl", new JSONNumber(request.sl));
   jo.put("Tp", new JSONNumber(request.tp));
   jo.put("Deviation", new JSONNumber(request.deviation));
   jo.put("Type", new JSONNumber((int)request.type));
   jo.put("Type_filling", new JSONNumber((int)request.type_filling));
   jo.put("Type_time", new JSONNumber((int)request.type_time));
   jo.put("MtExpiration", new JSONNumber((int)request.expiration));
   jo.put("Comment", new JSONString(request.comment));
   return jo;
}

JSONObject* MqlRatesToJson(const MqlRates& rates)
{
   JSONObject *jo = new JSONObject();
   jo.put("mt_time", new JSONNumber((int)rates.time));
   jo.put("open", new JSONNumber(rates.open));
   jo.put("high", new JSONNumber(rates.high));
   jo.put("low", new JSONNumber(rates.low));
   jo.put("close", new JSONNumber(rates.close));
   jo.put("tick_volume", new JSONNumber(rates.tick_volume));
   jo.put("spread", new JSONNumber(rates.spread));
   jo.put("real_volume", new JSONNumber(rates.real_volume));
   return jo;
}