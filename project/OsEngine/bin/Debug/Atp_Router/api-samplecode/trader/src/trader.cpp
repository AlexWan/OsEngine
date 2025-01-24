#include "ThostFtdcTraderApi.h"
#include <stdlib.h>
#include <stdio.h>
#include <stdarg.h>
#include <string.h>
#include <fstream>


//my includes

#include <iostream>
#include <string>
#include <list>
#include <thread>
#include <cstdio> 
#include <cstring> 
#include <winsock2.h> 

#pragma comment(lib, "WS2_32.lib")
using namespace std;
#include <mutex> 

#ifdef _WIN32
#include <Windows.h>
#else
#include <unistd.h>
#include <time.h>
#include <sys/time.h>
#include <signal.h>    // To handle SIGPIPE
#endif

static string _tradeServerUri;
static string _brokerId;
static string _userId;
static string _userPassword;
static string _appId;
static string _authCode;
static string _investorId;
static string _isReal;

static char *TRADE_SERVER_URI = "tcp://demo9.atplatform.cn:40905";
static char* BROKER_ID = "ALEX001";//"broker_id";
static char* USER_ID = "alex1";//"user_id";
static char* USER_PASSWORD = "GS#WM3sS";//"user_password";
static char *APP_ID = "app_id";
static char *AUTH_CODE = "auth_code";
static char *INVESTOR_ID = USER_ID;  // Investor id is mapped to user id in ATP

static bool isDisconnected;

//static std::mutex mutex_array_all;
//static std::mutex mutex_array_in;
//static std::mutex mutex_array_out;

void doSleep(unsigned int millis);

void printCurrTime();

class Logger 
{
public:
    static void info(const char *format, ...) 
    {
        va_list args;
        va_start(args, format);
        printCurrTime();
        vfprintf(stdout, format, args);
        putchar('\n');
        va_end(args);
    }
};

class TestTraderClient : public CThostFtdcTraderSpi 
{

public:
    TestTraderClient(CThostFtdcTraderApi *pTraderApi)
        : tradeApi(pTraderApi),
        requestID(0),
        orderRef(0),
        logonState(UNINITIALIZED_STATE) {}
    ~TestTraderClient() {}

protected:

    virtual void OnFrontConnected() 
    {
        try
        {
            if (isDisconnected)
            {
                return;
            }

            Logger::info("[INFO] [%s:%3d]: Front connected.", __FUNCTION__, __LINE__);

            if (_isReal == "true"
                || _isReal == "True"
                || _isReal == "TRUE")
            {
                doAuthenticate();
            }
            else
            {
                doLogin();
            }
        }
        catch (...)
        {
            Logger::info("Exception. OnFrontConnected()", __FUNCTION__, __LINE__);
        }
    }

    virtual void OnFrontDisconnected(int reason)
    {
        try
        {
            isDisconnected = true;
            logonState = LOGON_ABORTED;
            Logger::info("[WARN] [%s:%3d]: Front disconnected: reasonCode=%d.", __FUNCTION__, __LINE__, reason);

            exit(0);

            //Messages.push_back("Disconnect%");
          
            //doSleep(3000);  // Better to have a delay before next retry of connecting

        }
        catch (...)
        {
            Logger::info("Exception. OnFrontDisconnected()", __FUNCTION__, __LINE__);
        }
    }

    virtual void OnRspUserLogin(CThostFtdcRspUserLoginField* login, CThostFtdcRspInfoField* status, int requestID, bool isLast)
    {
        try
        {
            if (status != NULL && status->ErrorID != 0)
            {
                Logger::info("[ERROR] [%s:%3d]: Failed to login, errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                    status->ErrorID, status->ErrorMsg);

                logonState = LOGON_FAILED;
                return;
            }

            if (login == NULL)
            {
                Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of login info.", __FUNCTION__, __LINE__);    // Should not happen
                logonState = LOGON_FAILED;
                return;
            }

            this->sessionID = login->SessionID;
            this->frontID = login->FrontID;
            this->orderRef = atoi(login->MaxOrderRef);
            Logger::info("[INFO] [%s:%3d]: Login succeed: brokerID=%s, userID=%s, sessionID=%d, tradingDay=%s.", __FUNCTION__, __LINE__,
                login->BrokerID, login->UserID, login->SessionID, login->TradingDay);
            logonState = LOGON_SUCCEED;

            Messages.push_back("Connect%");

            ofstream MyFile("Atp_Router\\Files\\ConnectTrade.txt");
            MyFile.close();
            // Do other things, e.g. add an order
            //insertOrder("CU3M-LME", true, 3900.0, 5);

        }
        catch (...)
        {
           Logger::info("Exception. OnRspUserLogin()", __FUNCTION__, __LINE__);
        }
    }

    virtual void OnRspAuthenticate(CThostFtdcRspAuthenticateField* authenticate, CThostFtdcRspInfoField* status, int requestID, bool isLast)
    {
        try
        {
            if (status != NULL && status->ErrorID != 0)
            {
                Logger::info("[ERROR] [%s:%3d]: Failed to authenticate, errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                    status->ErrorID, status->ErrorMsg);
                return;
            }

            if (authenticate == NULL)
            {
                Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of authentication info.", __FUNCTION__, __LINE__);
                return;
            }

            Logger::info("[INFO] [%s:%3d]: Authenticate succeed: brokerID=%s, userID=%s, appID=%s, appType=%c.", __FUNCTION__, __LINE__,
                authenticate->BrokerID, authenticate->UserID, authenticate->AppID, authenticate->AppType);

            doLogin();
        }
        catch (...)
        {
            Logger::info("Exception. OnRspAuthenticate()", __FUNCTION__, __LINE__);
        }
    }

    virtual void OnRtnBulletin(CThostFtdcBulletinField* pBulletin)
    {
        try
        {
            if (pBulletin != NULL)
            {
                Logger::info("[INFO] [%s:%3d]: Bulletin: exchangeID=%s, newsType=%s, newsUrgency=%c, content=%s.", __FUNCTION__, __LINE__,
                    pBulletin->ExchangeID, pBulletin->NewsType, pBulletin->NewsUrgency, pBulletin->Content);
            }
        }
        catch (...)
        {
            Logger::info("Exception. OnRtnBulletin()", __FUNCTION__, __LINE__);
        }
    }

    int counterOrderActiv = 0;
    int counterOrderFail1 = 0;

    virtual void OnRspOrderInsert(CThostFtdcInputOrderField* order, CThostFtdcRspInfoField* status, int requestID, bool isLast)
    {
        try
        {
            if (status != NULL && status->ErrorID != 0)
            {
                if (order == NULL)
                {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of input order.", __FUNCTION__, __LINE__);    // Should not happen
                    return;
                }
                Logger::info("[WARN] [%s:%3d]: Failed to add new order: OrderRef=%s, instrumentID=%s, direction=%s, volumeTotalOriginal=%d, limitPrice=%f, errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                    order->OrderRef, order->InstrumentID, order->Direction == THOST_FTDC_D_Buy ? "buy" : "sell", order->VolumeTotalOriginal, order->LimitPrice, status->ErrorID, status->ErrorMsg);

                string info = "OrderFail1@";
                info += (order->OrderRef);
                info += "@";
                info += (order->InstrumentID);
                info += "@";
                info += (order->Direction);
                info += "@";
                info += to_string(status->ErrorID);
                info += "@";
                info += (status->ErrorMsg);
                info += "%";

                //Messages.push_back(info);

                string fileName = "Atp_Router\\Files\\OrderFail1\\" + std::to_string(counterOrderFail1) + ".txt";
                ofstream MyFile(fileName);
                MyFile << info;
                MyFile.close();
                counterOrderFail1++;
            }
            else if (status == NULL)
            {
                string info = "OrderActiv@";
                info += (order->OrderRef);
                info += "@";
                info += (order->RequestID);
                info += "@";
                info += (order->InstrumentID);
                info += "@";
                info += (order->Direction);
                info += "%";

                Messages.push_back(info);

                string fileName = "Atp_Router\\Files\\OrderActiv\\" + std::to_string(counterOrderActiv) + ".txt";
                ofstream MyFile(fileName);
                MyFile << info;
                MyFile.close();
                counterOrderActiv++;
            }
        }
        catch (...)
        {
            Logger::info("Exception. OnRspOrderInsert()", __FUNCTION__, __LINE__);
        }
    }

    int counterOrderFail2 = 0;
    int counterOrderFail3 = 0;
    int counterOrderAction1 = 0;

    virtual void OnRspOrderAction(CThostFtdcInputOrderActionField* action, CThostFtdcRspInfoField* status, int requestID, bool isLast)
    {
        try
        {
            if (status != NULL && status->ErrorID != 0)
            {
                if (action == NULL)
                {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of input order action.", __FUNCTION__, __LINE__);    // Should not happen

                    string info = "OrderFail2@";
                    info += to_string(requestID);
                    info += "@";
                    info += to_string(status->ErrorID);
                    info += "@";
                    info += (status->ErrorMsg);
                    info += "%";

                    Messages.push_back(info);

                    string fileName = "Atp_Router\\Files\\OrderFail2\\" + std::to_string(counterOrderFail2) + ".txt";
                    ofstream MyFile(fileName);
                    MyFile << info;
                    MyFile.close();
                    counterOrderFail2++;

                    return;
                }

                string info = "OrderFail3@";
                info += (action->OrderRef);
                info += "@";
                info += to_string(requestID);
                info += "@";
                info += (action->RequestID);
                info += "@";
                info += (action->InstrumentID);
                info += "@";
                info += (action->OrderSysID);
                info += "@";
                info += to_string(status->ErrorID);
                info += "@";
                info += (status->ErrorMsg);

                info += "%";

                Messages.push_back(info);

                string fileName = "Atp_Router\\Files\\OrderFail3\\" + std::to_string(counterOrderFail3) + ".txt";
                ofstream MyFile(fileName);
                MyFile << info;
                MyFile.close();
                counterOrderFail3++;

                Logger::info("[WARN] [%s:%3d]: Failed to %s order: orderRef=%s, orderSysID=%s, instrumentID=%s, volumeChange=%d, limitPrice=%f, errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                    action->ActionFlag == THOST_FTDC_AF_Delete ? "delete" : "modify", action->OrderRef, action->OrderSysID, action->InstrumentID, action->VolumeChange, action->LimitPrice, status->ErrorID, status->ErrorMsg);
                return;
            }

            string info = "OrderAction1@";
            info += (action->OrderRef);
            info += "@";
            info += (action->RequestID);
            info += "@";
            info += (action->InstrumentID);
            info += "@";
            info += (action->OrderSysID);
            info += "@";
            info += (action->ActionFlag);
            info += "@";
            info += to_string(action->LimitPrice);
            info += "@";
            info += (action->UserID);
            info += "@";
            info += (action->ExchangeID);
            info += "%";

            Messages.push_back(info);

            string fileName = "Atp_Router\\Files\\OrderAction1\\" + std::to_string(counterOrderAction1) + ".txt";
            ofstream MyFile(fileName);
            MyFile << info;
            MyFile.close();
            counterOrderAction1++;

        }
        catch (...)
        {
            Logger::info("Exception. OnRspOrderAction()", __FUNCTION__, __LINE__);
        }
    }

    int counterOrderAction2 = 0;

    virtual void OnRtnOrder(CThostFtdcOrderField* order)
    {
        try
        {
            if (order != NULL)
            {
                //Logger::info("[INFO] [%s:%3d]: Order status: orderRef=%s, orderLocalID=%s, sessionID=%d, frontID=%d, instrumentID=%s, direction=%s, volumeTotalOriginal=%d, limitPrice=%f, volumeTraded=%d, orderStatus=%c.", __FUNCTION__, __LINE__,
                 //   order->OrderRef, order->OrderLocalID, order->SessionID, order->FrontID, order->InstrumentID, order->Direction == THOST_FTDC_D_Buy ? "buy" : "sell", order->VolumeTotalOriginal, order->LimitPrice, order->VolumeTraded, order->OrderStatus);

                string info = "OrderAction2@";
                info += (order->OrderRef);
                info += "@";
                info += (order->InstrumentID);
                info += "@";
                info += (order->Direction);
                info += "@";
                info += (order->OrderStatus);
                info += "@";
                info += (order->InsertDate);
                info += "@";
                info += (order->InsertTime);
                info += "@";
                info += to_string(order->LimitPrice);
                info += "@";
                info += to_string(order->VolumeTotalOriginal);
                info += "@";
                info += (order->OrderLocalID);
                info += "%";

                //Messages.push_back(info);

                string fileName = "Atp_Router\\Files\\OrderAction2\\" + std::to_string(counterOrderAction2) + ".txt";
                ofstream MyFile(fileName);
                MyFile << info;
                MyFile.close();
                counterOrderAction2++;
            }
        }
        catch (...)
        {
            Logger::info("Exception. OnRtnOrder()", __FUNCTION__, __LINE__);
        }
    }

    int counterOrderFail4 = 0;
    int counterOrderFail5 = 0;

    virtual void OnErrRtnOrderAction(CThostFtdcOrderActionField* action, CThostFtdcRspInfoField* status)
    {
        try
        {
            if (status != NULL && status->ErrorID != 0)
            {
                if (action == NULL)
                {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of order action.", __FUNCTION__, __LINE__);    // Should not happen

                    string info = "OrderFail4@";
                    info += to_string(requestID);
                    info += "@";
                    info += to_string(status->ErrorID);
                    info += "@";
                    info += (status->ErrorMsg);
                    info += "%";

                    Messages.push_back(info);

                    string fileName = "Atp_Router\\Files\\OrderFail4\\" + std::to_string(counterOrderFail4) + ".txt";
                    ofstream MyFile(fileName);
                    MyFile << info;
                    MyFile.close();
                    counterOrderFail4++;

                    return;
                }

                Logger::info("[WARN] [%s:%3d]: Failed to %s order: instrumentID=%s, orderLocalID=%s, volumeChange=%d, limitPrice=%f, errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                    action->ActionFlag == THOST_FTDC_AF_Delete ? "delete" : "modify", action->InstrumentID, action->OrderLocalID, action->VolumeChange, action->LimitPrice, status->ErrorID, status->ErrorMsg);

                string info = "OrderFail5@";
                info += (action->OrderRef);
                info += "@";
                info += to_string(requestID);
                info += "@";
                info += (action->RequestID);
                info += "@";
                info += (action->InstrumentID);
                info += "@";
                info += (action->OrderSysID);
                info += "@";
                info += to_string(status->ErrorID);
                info += "@";
                info += (status->ErrorMsg);
                info += "%";

                Messages.push_back(info);

                string fileName = "Atp_Router\\Files\\OrderFail5\\" + std::to_string(counterOrderFail5) + ".txt";
                ofstream MyFile(fileName);
                MyFile << info;
                MyFile.close();
                counterOrderFail5++;
            }
        }
        catch (...)
        {
            Logger::info("Exception. OnErrRtnOrderAction()", __FUNCTION__, __LINE__);
        }
    }

    int counterMyTrade1 = 0;

    virtual void OnRtnTrade(CThostFtdcTradeField* trade)
    {
        try
        {
            if (trade != NULL)
            {
                // Logger::info("[INFO] [%s:%3d]: Traded order: orderRef=%s, orderLocalID=%s, instrumentID=%s, direction=%s, volume=%d, price=%f, tradeDate=%s.", __FUNCTION__, __LINE__,
                 //    trade->OrderRef, trade->OrderLocalID, trade->InstrumentID, trade->Direction == THOST_FTDC_D_Buy ? "buy" : "sell", trade->Volume, trade->Price, trade->TradeDate);

                string info = "MyTrade1@";
                info += (trade->OrderRef);
                info += "@";
                info += (trade->OrderLocalID);
                info += "@";
                info += (trade->TradeDate);
                info += "@";
                info += (trade->TradeTime);
                info += "@";
                info += (trade->InstrumentID);
                info += "@";
                info += to_string(trade->Volume);
                info += "@";
                info += to_string(trade->Price);
                info += "@";
                info += (trade->Direction);
                info += "@";
                info += (trade->TradeID);
                info += "%";

                Messages.push_back(info);

                string fileName = "Atp_Router\\Files\\MyTrades\\" + std::to_string(counterMyTrade1) + ".txt";

                ofstream MyFile(fileName);
                MyFile << info;
                MyFile.close();

                counterMyTrade1++;
            }
        }
        catch (...)
        {
            Logger::info("Exception. OnRtnTrade()", __FUNCTION__, __LINE__);
        }
    }

    int counterMyTrade2 = 0;

    virtual void OnRspQryTrade(CThostFtdcTradeField* trade, CThostFtdcRspInfoField* status, int requestID, bool isLast)
    {
        try
        {
            if (status != NULL && status->ErrorID != 0)
            {
                Logger::info("[WARN] [%s:%3d]: Failed to query trade info: errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                    status->ErrorID, status->ErrorMsg);
                return;
            }

            if (trade == NULL)
            {
                if (status == NULL)
                {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of trade info.", __FUNCTION__, __LINE__);    // Should not happen
                    return;
                }

                if (!isLast)
                {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of trade info.", __FUNCTION__, __LINE__);    // Should not happen
                    return;
                }

                Logger::info("[INFO] [%s:%3d]: No matched trade info found.", __FUNCTION__, __LINE__);
                return;
            }
            else
            {
                string info = "MyTrade2@";
                info += (trade->OrderRef);
                info += "@";
                info += (trade->OrderLocalID);
                info += "@";
                info += (trade->TradeDate);
                info += "@";
                info += (trade->TradeTime);
                info += "@";
                info += (trade->InstrumentID);
                info += "@";
                info += to_string(trade->Volume);
                info += "@";
                info += to_string(trade->Price);
                info += "@";
                info += (trade->Direction);
                info += "@";
                info += (trade->TradeID);
                info += "%";

                Messages.push_back(info);

                string fileName = "Atp_Router\\Files\\MyTrades2\\" + std::to_string(counterMyTrade2) + ".txt";

                ofstream MyFile(fileName);
                MyFile << info;
                MyFile.close();

                counterMyTrade2++;
            }

            Logger::info("[INFO] [%s:%3d]: Filled order: orderLocalID=%s, instrumentID=%s, direction=%s, volume=%d, price=%f, tradeDate=%s.", __FUNCTION__, __LINE__,
                trade->OrderLocalID, trade->InstrumentID, trade->Direction == THOST_FTDC_D_Buy ? "buy" : "sell", trade->Volume, trade->Price, trade->TradeDate);

        }
        catch (...)
        {
            Logger::info("Exception. OnRspQryTrade()", __FUNCTION__, __LINE__);
        }
    }

    virtual void OnRspQryOrder(CThostFtdcOrderField* order, CThostFtdcRspInfoField* status, int requestID, bool isLast)
    {
        try
        {
            if (status != NULL && status->ErrorID != 0)
            {
                Logger::info("[WARN] [%s:%3d]: Failed to query order info: errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                    status->ErrorID, status->ErrorMsg);
                return;
            }
            if (order == NULL)
            {
                if (status == NULL)
                {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of order info.", __FUNCTION__, __LINE__);    // Should not happen
                    return;
                }
                if (!isLast) {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of order info.", __FUNCTION__, __LINE__);    // Should not happen
                    return;
                }
                Logger::info("[INFO] [%s:%3d]: No matched order info found.", __FUNCTION__, __LINE__);
                return;
            }
            Logger::info("[INFO] [%s:%3d]: Order info: orderRef=%s, orderLocalID=%s, sessionID=%d, frontID=%d, instrumentID=%s, direction=%s, volumeTotalOriginal=%d, limitPrice=%f, OrderStatus=%c.", __FUNCTION__, __LINE__,
                order->OrderRef, order->OrderLocalID, order->SessionID, order->FrontID, order->InstrumentID, order->Direction == THOST_FTDC_D_Buy ? "buy" : "sell", order->VolumeTotalOriginal, order->LimitPrice, order->OrderStatus);

        }
        catch (...)
        {
            Logger::info("Exception. OnRspQryOrder()", __FUNCTION__, __LINE__);
        }
    }

    virtual void OnRspQryInstrument(CThostFtdcInstrumentField* inst, CThostFtdcRspInfoField* status, int requestID, bool isLast)
    {
        try
        {
            if (status != NULL && status->ErrorID != 0)
            {
                Logger::info("[WARN] [%s:%3d]: Failed to query instrument info: errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                    status->ErrorID, status->ErrorMsg);
                return;
            }

            if (inst == NULL)
            {
                if (status == NULL)
                {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of instrument info.", __FUNCTION__, __LINE__);    // Should not happen
                    return;
                }

                if (!isLast) {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of instrument info.", __FUNCTION__, __LINE__);    // Should not happen
                    return;
                }
                Logger::info("[INFO] [%s:%3d]: No matched instrument info found.", __FUNCTION__, __LINE__);
                return;
            }

            Logger::info("[INFO] [%s:%3d]: Instrument info: instrumentID=%s, exchangeID=%s, isTrading=%s, expireDate=%s, volumeMultiple=%d, exchangeInstID=%s.", __FUNCTION__, __LINE__,
                inst->InstrumentID, inst->ExchangeID, inst->IsTrading != 0 ? "true" : "false", inst->ExpireDate, inst->VolumeMultiple, inst->ExchangeInstID);

        }
        catch (...)
        {
            Logger::info("Exception. OnRspQryInstrument()", __FUNCTION__, __LINE__);
        }
    }

    virtual void OnRspQryInvestorPosition(CThostFtdcInvestorPositionField* position, CThostFtdcRspInfoField* status, int requestID, bool isLast)
    {
        try
        {
            if (status != NULL && status->ErrorID != 0)
            {
                Logger::info("[WARN] [%s:%3d]: Failed to query investor position info: errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                    status->ErrorID, status->ErrorMsg);
                return;
            }

            if (position == NULL)
            {
                if (status == NULL)
                {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of investor position info.", __FUNCTION__, __LINE__);    // Should not happen
                    return;
                }
                if (!isLast)
                {
                    Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of investor position info.", __FUNCTION__, __LINE__);    // Should not happen
                    return;
                }

                Logger::info("[INFO] [%s:%3d]: No matched investor position info found.", __FUNCTION__, __LINE__);
                return;
            }

            Logger::info("[INFO] [%s:%3d]: Investor position info: instrumentID=%s, openCost=%f, positionCost=%f, position=%d, ydPosition=%d, closeProfit=%f.", __FUNCTION__, __LINE__,
                position->InstrumentID, position->OpenCost, position->PositionCost, position->Position, position->YdPosition, position->CloseProfit);

        }
        catch (...)
        {
            Logger::info("Exception. OnRspQryInvestorPosition()", __FUNCTION__, __LINE__);
        }
    }
    
    virtual void OnRspQryTradingAccount(CThostFtdcTradingAccountField* pTradingAccount,
        CThostFtdcRspInfoField* pRspInfo, int nRequestID, bool bIsLast)
    {
        try
        {
            cout << "Trading account response 1" << endl;
        }
        catch (...)
        {
            Logger::info("Exception. OnRspQryInvestorPosition()", __FUNCTION__, __LINE__);
        }
    }

    // Overwrite other api(s)
    // ...

public:

    void insertOrder(const char *instrumentID, bool isBuy, double price, int volume, int orderRef) 
    {
        ensureLogon();
        const int requestID = nextRequestID();
        CThostFtdcInputOrderField field;
        memset(&field, 0, sizeof(field));
        strcpy(field.BrokerID, BROKER_ID);
        strcpy(field.InvestorID, INVESTOR_ID);
        strcpy(field.UserID, USER_ID);
        strcpy(field.InstrumentID, instrumentID);
        sprintf(field.OrderRef, "%d", orderRef);
        field.OrderPriceType = THOST_FTDC_OPT_LimitPrice;
        field.Direction = isBuy ? THOST_FTDC_D_Buy : THOST_FTDC_D_Sell;  // Direction
        field.CombOffsetFlag[0] = THOST_FTDC_OF_Open;
        field.CombHedgeFlag[0] = THOST_FTDC_HF_Speculation;
        //field.CombHedgeFlag[1] = '9';  // Enable this line to create market maker order
        //field.CombHedgeFlag[2] = '2';  // Enable this line to set T+1 session flag for non market maker order(currently for HKEX only)
        field.LimitPrice = price;            // Price
        field.VolumeTotalOriginal = volume;  // Volume
        field.TimeCondition = THOST_FTDC_TC_GFD;
        strcpy(field.GTDDate, "");
        field.VolumeCondition = THOST_FTDC_VC_AV;
        field.MinVolume = 0;
        field.ContingentCondition = THOST_FTDC_CC_Immediately;
        field.StopPrice = 0.0;
        field.ForceCloseReason = THOST_FTDC_FCC_NotForceClose;
        field.IsAutoSuspend = 0;
        strcpy(field.BusinessUnit, "customized-data");  // Store customized data if necessary (needs to be a string)
        field.RequestID = requestID;
        field.UserForceClose = 0;
        field.IsSwapOrder = 0;
        int rtnCode = tradeApi->ReqOrderInsert(&field, requestID);

        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to add new order: instrumentID=%s, direction=%s, volume=%d, price=%f.", __FUNCTION__, __LINE__,
                instrumentID, isBuy ? "buy" : "sell", volume, price);
        }
    }

    void replaceOrder(const char *orderRef, double price, int volume) 
    {
        ensureLogon();
        CThostFtdcInputOrderActionField field;
        memset(&field, 0, sizeof(field));
        strcpy(field.BrokerID, BROKER_ID);
        strcpy(field.InvestorID, INVESTOR_ID);
        strcpy(field.UserID, USER_ID);

        // Use FrontID+SessionID+OrderRef to locate the order:
        field.FrontID = frontID;        // FrontID can be got from CThostFtdcOrderField.FrontID
        field.SessionID = sessionID;    // SessionID can be got from CThostFtdcOrderField.SessionID
        strcpy(field.OrderRef, orderRef);

        // Or use ExchangeID+OrderSysID to locate the order:
        //strcpy(field.ExchangeID, "LME");
        //strcpy(field.OrderSysID, "AAAAAAA");

        field.ActionFlag = THOST_FTDC_AF_Modify;
        field.LimitPrice = price;       // Updated price
        field.VolumeChange = volume;    // Updated remaining volume

        int rtnCode = tradeApi->ReqOrderAction(&field, nextRequestID());

        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to modify order: orderRef=%s, frontID=%d, sessionID=%d, newPrice=%f, newVolume=%d.", __FUNCTION__, __LINE__,
                field.OrderRef, field.FrontID, field.SessionID, field.LimitPrice, field.VolumeChange);
        }
    }

    void cancelOrder(const char *orderRef) 
    {
        ensureLogon();
        CThostFtdcInputOrderActionField field;
        memset(&field, 0, sizeof(field));
        strcpy(field.BrokerID, BROKER_ID);
        strcpy(field.InvestorID, INVESTOR_ID);
        strcpy(field.UserID, USER_ID);

        // Use FrontID+SessionID+OrderRef to locate the order:
        field.FrontID = frontID;        // FrontID can be got from CThostFtdcOrderField.FrontID
        field.SessionID = sessionID;    // SessionID can be got from CThostFtdcOrderField.SessionID
        strcpy(field.OrderRef, orderRef);

        // Or use ExchangeID+OrderSysID to locate the order:
        //strcpy(field.ExchangeID, "LME");
        //strcpy(field.OrderSysID, "AAAAAAA");

        field.ActionFlag = THOST_FTDC_AF_Delete;    // Delete

        int rtnCode = tradeApi->ReqOrderAction(&field, nextRequestID());

        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to cancel order: orderRef=%s, frontID=%d, sessionID=%d.", __FUNCTION__, __LINE__,
                field.OrderRef, field.FrontID, field.SessionID);
        }
    }

    void queryTrade(const char *instrumentID) 
    {
        ensureLogon();
        CThostFtdcQryTradeField field;
        memset(&field, 0, sizeof(field));
        strcpy(field.BrokerID, BROKER_ID);
        strcpy(field.InvestorID, INVESTOR_ID);
        strcpy(field.InstrumentID, instrumentID);
        strcpy(field.ExchangeID, "");
        strcpy(field.TradeID, "");
        strcpy(field.TradeTimeStart, "");    // The Format of TradeTimeStart: HH:mm:ss, e.g: 09:30:00
        strcpy(field.TradeTimeEnd, "");      // The Format of TradeTimeEnd  : HH:mm:ss, e.g: 15:00:00
        int rtnCode = tradeApi->ReqQryTrade(&field, nextRequestID());

        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to query trade info: brokerID=%s, investorID=%s, instrumentID=%s, exchangeID=%s.", __FUNCTION__, __LINE__,
                field.BrokerID, field.InvestorID, field.InstrumentID, field.ExchangeID);
        }
    }

    void queryOrder(const char *instrumentID) 
    {
        ensureLogon();
        CThostFtdcQryOrderField field;
        memset(&field, 0, sizeof(field));
        strcpy(field.BrokerID, BROKER_ID);
        strcpy(field.InvestorID, INVESTOR_ID);
        strcpy(field.InstrumentID, instrumentID);
        strcpy(field.ExchangeID, "");
        strcpy(field.OrderSysID, "");
        strcpy(field.InsertTimeStart, "");    // The Format of TradeTimeStart: HH:mm:ss, e.g: 09:30:00
        strcpy(field.InsertTimeEnd, "");      // The Format of TradeTimeEnd  : HH:mm:ss, e.g: 15:00:00

        int rtnCode = tradeApi->ReqQryOrder(&field, nextRequestID());

        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to query order info: brokerID=%s, investorID=%s, instrumentID=%s, exchangeID=%s.", __FUNCTION__, __LINE__,
                field.BrokerID, field.InvestorID, field.InstrumentID, field.ExchangeID);
        }
    }

    void queryInstrument(const char *instrumentID) 
    {
        ensureLogon();
        CThostFtdcQryInstrumentField field;
        memset(&field, 0, sizeof(field));
        strcpy(field.ExchangeID, "");
        //strcpy(field.ExchangeInstID, "EVM");  // Enable this line to get CThostFtdcInstrumentField.VolumeMultiple with original currency
        strcpy(field.InstrumentID, instrumentID);
        strcpy(field.ProductID, "");
        int rtnCode = tradeApi->ReqQryInstrument(&field, nextRequestID());

        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to query instrument info: exchangeID=%s, instrumentID=%s, productID=%s.", __FUNCTION__, __LINE__,
                field.ExchangeID, field.InstrumentID, field.ProductID);
        }
    }

    void queryInvestorPosition(const char *instrumentID) 
    {
        ensureLogon();
        CThostFtdcQryInvestorPositionField field;
        memset(&field, 0, sizeof(field));
        strcpy(field.BrokerID, BROKER_ID);
        strcpy(field.InvestorID, INVESTOR_ID);
        strcpy(field.InstrumentID, instrumentID);
        int rtnCode = tradeApi->ReqQryInvestorPosition(&field, nextRequestID());


        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to query investor position info: brokerID=%s, investorID=%s, instrumentID=%s.", __FUNCTION__, __LINE__,
                field.BrokerID, field.InvestorID, field.InstrumentID);
        }
    }

    void ensureLogon() 
    {
        const int MAX_ATTEMPT_TIMES = 100;
        int tryTimes = 0;

        while (logonState == UNINITIALIZED_STATE && tryTimes++ < MAX_ATTEMPT_TIMES) 
        {
            doSleep(200);
        }

        if (logonState != LOGON_SUCCEED) 
        {
            Logger::info("[ERROR] [%s:%3d]: Trade logon failed.", __FUNCTION__, __LINE__);
        }
    }

    list<string> Messages;

private:

    void doLogin() 
    {
        CThostFtdcReqUserLoginField field;
        memset(&field, 0, sizeof(field));
        strcpy(field.BrokerID, BROKER_ID);
        strcpy(field.UserID, USER_ID);
        strcpy(field.Password, USER_PASSWORD);

        int rtnCode = tradeApi->ReqUserLogin(&field, nextRequestID());

        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to login: brokerID=%s, userID=%s, password=%s.", __FUNCTION__, __LINE__,
                field.BrokerID, field.UserID, field.Password);
        }
    }

    void doAuthenticate() 
    {
        CThostFtdcReqAuthenticateField field;
        memset(&field, 0, sizeof(field));
        strcpy(field.BrokerID, BROKER_ID);
        strcpy(field.UserID, USER_ID);
        strcpy(field.AppID, APP_ID);
        strcpy(field.AuthCode, AUTH_CODE);

        int rtnCode = tradeApi->ReqAuthenticate(&field, nextRequestID());

        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to authenticate: brokerID=%s, userID=%s, appID=%s, authCode=%s.", __FUNCTION__, __LINE__,
                field.BrokerID, field.UserID, field.AppID, field.AuthCode);
        }
    }

    int nextRequestID() { return ++requestID; }

    int nextOrderRef() { return orderRef++; }

private:
    enum LogonState 
    {
        LOGON_SUCCEED = 0,
        LOGON_FAILED = 1,
        LOGON_ABORTED = 2,
        UNINITIALIZED_STATE = 3
    };

    volatile LogonState logonState;

    CThostFtdcTraderApi *tradeApi;
    int requestID;
    int frontID;
    int sessionID;
    int orderRef;
};

void doSleep(unsigned int millis) 
{
#ifdef _WIN32
    Sleep(millis);
#else
    usleep(millis*1000);
#endif
}

void printCurrTime() 
{
#ifdef _WIN32
    SYSTEMTIME localTime;
    GetLocalTime(&localTime);
    printf("[%d-%02d-%02d %02d:%02d:%02d.%03d] ", localTime.wYear, localTime.wMonth, localTime.wDay, localTime.wHour, localTime.wMinute, localTime.wSecond, localTime.wMilliseconds);
#else
    struct timeval timeVal;
    gettimeofday(&timeVal, NULL);
    struct tm* time;
    time = localtime(&timeVal.tv_sec);
    printf("[%d-%02d-%02d %02d:%02d:%02d.%03d] ", (time->tm_year + 1900), (time->tm_mon + 1), time->tm_mday, time->tm_hour, time->tm_min, time->tm_sec, timeVal.tv_usec / 1000);
#endif
}


#pragma region Server

list<string> MessagesIn;
list<string> MessagesOut;

void SetConnectSettings(string str)
{
    int startV = 2;
    int endV = 2;
    int valueIndx = 0;

    for (int i = 2; i < str.length(); i++)
    {

        if (str[i] == '@')
        {
            valueIndx++;
            endV = i - 2;

           /* static string _tradeServerUri;
            connectionStr += BrokerId + "@";        1 
            connectionStr += UserId + "@";          2
            connectionStr += UserPassword + "@";    3
            connectionStr += AppId + "@";           4
            connectionStr += AufCode + "@";         5
            connectionStr += DataServerUrl + "@";   6
            connectionStr += TradeServerUrl + "@";  7
            connectionStr += IsReal + "@";          8

            static const char* TRADE_SERVER_URI = "tcp://192.168.101.225:40905";
            static const char* BROKER_ID = "NHSG001";//"broker_id";
            static const char* USER_ID = "nhsguser2";//"user_id";
            static const char* USER_PASSWORD = "%wsF(YmQ";//"user_password";
            static const char* APP_ID = "app_id";
            static const char* AUTH_CODE = "auth_code";
            static const char* INVESTOR_ID = USER_ID;  // Investor id is mapped to user id in ATP*/

            if (valueIndx == 1)
            {
                _brokerId = str.substr(startV, i - startV);

                BROKER_ID = const_cast<char*>(_brokerId.c_str());
            }
            if (valueIndx == 2)
            {
                _userId = str.substr(startV, i - startV);
                USER_ID = const_cast<char*>(_userId.c_str());
                INVESTOR_ID = const_cast<char*>(_userId.c_str());
            }
            if (valueIndx == 3)
            {
                _userPassword = str.substr(startV, i - startV);
                USER_PASSWORD = const_cast<char*>(_userPassword.c_str());
            }
            if (valueIndx == 4)
            {
                _appId = str.substr(startV, i - startV);
                APP_ID = const_cast<char*>(_appId.c_str());
            }
            if (valueIndx == 5)
            {
                _authCode = str.substr(startV, i - startV);
                AUTH_CODE = const_cast<char*>(_authCode.c_str());
            }
            if (valueIndx == 6)
            {
                //_authCode = str.substr(startV, i - startV);
                //AUTH_CODE = const_cast<char*>(_authCode.c_str());
            }

            if (valueIndx == 7)
            {
                _tradeServerUri = str.substr(startV, i - startV);
                TRADE_SERVER_URI = const_cast<char*>(_tradeServerUri.c_str());
            }

            if (valueIndx == 8)
            {
                _isReal = str.substr(startV, i - startV);
            }

            startV = i + 1;
        }
    }
}

DWORD WINAPI serverReceive(LPVOID lpParam)
{ //Получение данных от клиента
    char buffer[1024] = { 0 }; //Буфер для данных

    SOCKET client = *(SOCKET*)lpParam; //Сокет для клиента

    MessagesIn.clear();
    MessagesOut.clear();

    while (true)
    { //Цикл работы сервера
        try
        {
            if (isDisconnected)
            {
                return 1;
            }
            if (recv(client, buffer, sizeof(buffer), 0) == SOCKET_ERROR)
            {
                //Если не удалось получить данные буфера, сообщить об ошибке и выйти
                cout << "recv function failed with error " << WSAGetLastError() << endl;
                return -1;
            }

            if (strcmp(buffer, "exit\n") == 0 ||
                buffer[0] == 'D')
            { //Если клиент отсоединился
                cout << "Client Disconnected." << endl;
                return -1;
            }

            if (buffer[0] == '\0')
            {
                //buffer[0] = 'P';
                send(client, buffer, sizeof(buffer), 0); // отправляем ответ
                memset(buffer, 0, sizeof(buffer)); //Очистить буфер
                continue;
            }

            if (buffer[0] != 'P')
            {
                cout << "Cl: " << buffer << endl; //Иначе вывести сообщение от клиента из буфера
            }

            if (buffer[0] == 'O'
                || buffer[0] == 'R')
            { // управление ордерами
                MessagesIn.push_back(buffer);
            }

            if (buffer[0] == 'Z')
            {// запросить состояние портфеля

                MessagesIn.push_back(buffer);
            }

            if (buffer[0] == 'C')
            {// установить соединение
                string buff = buffer;

                SetConnectSettings(buff);

                MessagesIn.push_back(buffer);
            }
            if (buffer[0] == 'D')
            {// разорвать соединение
                MessagesIn.push_back("Delete");
            }

            if (buffer[0] == 'S')
            {// подписка на бумагу
                MessagesIn.push_back(buffer);
            }

            if (buffer[0] == 'P'
                && MessagesOut.size() != 0)
            {
                list <string>::iterator it;

                bool sendMessage = false;

                //mutex_array_out.lock();

                for (it = MessagesOut.begin(); it != MessagesOut.end(); it++)
                {
                    string str = *it;
                    send(client, str.c_str(), sizeof(buffer), 0); // отправляем ответ
                    MessagesOut.erase(it);
                    sendMessage = true;
                    //cout << "send to OsEngine: " << (str) << " \n";

                    break;
                }

                // mutex_array_out.unlock();

                if (sendMessage == false)
                {
                    send(client, buffer, sizeof(buffer), 0); // отправляем ответ
                }

                memset(buffer, 0, sizeof(buffer)); //Очистить буфер
                continue;
            }
            else
            {
                memset(buffer, 0, sizeof(buffer)); //Очистить буфер
                send(client, buffer, sizeof(buffer), 0); // отправляем ответ
            }
        }
        catch (...)
        {
            // ignore
        }
    }
    return 1;
}

int SocketWorkPlace()
{
    WSADATA WSAData; //Данные 
    SOCKET server, client; //Сокеты сервера и клиента
    SOCKADDR_IN serverAddr, clientAddr; //Адреса сокетов

    WSAStartup(MAKEWORD(2, 0), &WSAData);

    server = socket(AF_INET, SOCK_STREAM, 0); //Создали сервер
    if (server == INVALID_SOCKET) {
        cout << "Socket creation failed with error:" << WSAGetLastError() << endl;
        return -1;
    }
    serverAddr.sin_addr.s_addr = INADDR_ANY;
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(5556);

    if (bind(server, (SOCKADDR*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
        cout << "Bind function failed with error: " << WSAGetLastError() << endl;
        return -1;
    }

    if (listen(server, 0) == SOCKET_ERROR) { //Если не удалось получить запрос
        cout << "Listen function failed with error:" << WSAGetLastError() << endl;
        return -1;
    }

    cout << "Listening for incoming connections...." << endl;

    char buffer[1024]; //Создать буфер для данных
    int clientAddrSize = sizeof(clientAddr); //Инициализировать адерс клиента

    if ((client = accept(server, (SOCKADDR*)&clientAddr, &clientAddrSize)) != INVALID_SOCKET) {
        //Если соединение установлено
        cout << "Client connected!" << endl;

        DWORD tid; //Идентификатор

        HANDLE t1 = CreateThread(NULL, 0, serverReceive, &client, 0, &tid); //Создание потока для получения данных
        if (t1 == NULL) { //Ошибка создания потока
            cout << "Thread Creation Error: " << WSAGetLastError() << endl;
        }

        WaitForSingleObject(t1, INFINITE);

        closesocket(client); //Закрыть сокет

        if (closesocket(server) == SOCKET_ERROR) { //Ошибка закрытия сокета
            cout << "Close socket failed with error: " << WSAGetLastError() << endl;
            return -1;
        }

        cout << "Client disconnected!" << endl;

        WSACleanup();
    }
    return -1;
}

void ThreadWorkerPlace()
{
    while (true)
    {
        try
        {
            SocketWorkPlace();
        }
        catch (...)
        {
           // ignore
        }
    }
}

#pragma endregion


int main() 
{
#ifndef _WIN32
    // To ignore SIGPIPE
    struct sigaction sa;
    sa.sa_handler = SIG_IGN;
    sigaction(SIGPIPE, &sa, 0);
#endif
    const char* flowPath = "trader-0";
    
    std::thread thread(ThreadWorkerPlace);

    /* doSleep(1000);
 tradeClient->queryInstrument("CU3M-LME");
 doSleep(1000);
 tradeClient->insertOrder("CU3M-LME", true, 3900.0, 3);
 doSleep(1000);
 tradeClient->queryOrder("CU3M-LME");
 doSleep(1000);
 tradeClient->replaceOrder("1", 3950.0, 5);
 doSleep(1000);
 tradeClient->cancelOrder("1");
 doSleep(1000);
 tradeClient->queryTrade("CU3M-LME");
 doSleep(1000);
 tradeClient->queryInvestorPosition("");
 doSleep(1000);
 */

    CThostFtdcTraderApi* pTraderApi;;  // Distinct flow path is needed for each api instance
    TestTraderClient* tradeClient;

    //готовая читалка данных из сообщений сокетов

    std::chrono::milliseconds timespan(1000);

    bool isClosed = false;

    bool isStarted = false;

    while (true)
    {
        try
        {
            std::this_thread::sleep_for(timespan);

            if (isClosed)
            {
                break;
            }

            if (isDisconnected)
            {
                return 0;
            }

            // подача сообщений В ШЛЮЗ

            list <string>::iterator it;


            for (it = MessagesIn.begin(); it != MessagesIn.end(); it++)
            {
                string str = *it;

                if (str[0] == 'C') // Connect
                { // запрос на подключение к Шлюзу
                    pTraderApi = CThostFtdcTraderApi::CreateFtdcTraderApi(flowPath);
                    tradeClient = new TestTraderClient(pTraderApi);

                    pTraderApi->RegisterSpi(tradeClient);
                    pTraderApi->RegisterFront((char*)TRADE_SERVER_URI);

                    pTraderApi->SubscribePrivateTopic(THOST_TERT_RESTART);
                    pTraderApi->SubscribePublicTopic(THOST_TERT_RESTART);

                    Logger::info("[INFO] [%s:%3d]: Initial client: serverUri=%s, brokerID=%s, userID=%s, version=%s.", __FUNCTION__, __LINE__,
                        TRADE_SERVER_URI, BROKER_ID, USER_ID, CThostFtdcTraderApi::GetApiVersion());

                    pTraderApi->Init();  // Start connecting

                    isStarted = true;

                    cout << "do connect" << " \n";
                }
                else if (str[0] == 'D') // Disconnect
                { // запрос на выключение
                    isClosed = true;
                }
                else if (str[0] == 'S') // запрос на чтение информации по инструменту
                {
                    string buff = str;

                    int endV = 2;

                    for (int i = 2; i < buff.length(); i++)
                    {
                        if (buff[i] == '@')
                        {
                            endV = i - 2;
                            break;
                        }
                    }

                    string const secName = buff.substr(2, endV);
                    char* cstr2 = const_cast<char*>(secName.c_str());

                    tradeClient->queryInstrument(cstr2);
                }
                else if (str[0] == 'Z') // PositionOnBoard
                { // запрос на получение позиций по портфелю
                    string buff = str;

                    int endV = 2;

                    for (int i = 2; i < buff.length(); i++)
                    {
                        if (buff[i] == '@')
                        {
                            endV = i - 2;
                            break;
                        }
                    }

                    string const secName = buff.substr(2, endV);
                    char* cstr2 = const_cast<char*>(secName.c_str());

                    tradeClient->queryInvestorPosition(cstr2);
                }
                else if (str[0] == 'O') // Order
                { // запрос на выставление ордера
                    //string orderToTcp = "O@";
                    //orderToTcp += order.SecurityNameCode + "@";
                    //orderToTcp += isBuy + "@";
                    //orderToTcp += order.Price.ToString().Replace(",", ".") + "@";
                    //orderToTcp += order.Volume.ToString().Replace(",", ".") + "@";
                    //orderToTcp += order.NumberUser + "@";

                    int startV = 2;
                    int endV = 2;
                    int valueIndx = 0;

                    string seсurityName = "";
                    string isBuy;
                    string orderPrice = "";
                    string orderVolume = "";
                    string numberUser = "";

                    for (int i = 2; i < str.length(); i++)
                    {
                        if (str[i] == '@')
                        {
                            valueIndx++;
                            endV = i - 2;

                            if (valueIndx == 1)
                            {
                                seсurityName = str.substr(startV, i - startV);
                            }
                            if (valueIndx == 2)
                            {
                                isBuy = str.substr(startV, i - startV);
                            }
                            if (valueIndx == 3)
                            {
                                orderPrice = str.substr(startV, i - startV);
                            }
                            if (valueIndx == 4)
                            {
                                orderVolume = str.substr(startV, i - startV);
                            }
                            if (valueIndx == 5)
                            {
                                numberUser = str.substr(startV, i - startV);
                                break;
                            }

                            startV = i + 1;
                        }
                    }

                    bool isBuyBool = false;

                    if (isBuy == "true" ||
                        isBuy == "True")
                    {
                        isBuyBool = true;
                    }

                    double priceDouble = stod(orderPrice);
                    int volumeInt = stoi(orderVolume);
                    int numberUserInt = stoi(numberUser);

                    tradeClient->insertOrder(seсurityName.c_str(), isBuyBool, priceDouble, volumeInt, numberUserInt);
                }
                else if (str[0] == 'R') // Return
                { // запрос на отзыв ордера
                    int startV = 2;
                    int endV = 2;
                    int valueIndx = 0;

                    string numberUser = "";

                    for (int i = 2; i < str.length(); i++)
                    {
                        if (str[i] == '@')
                        {
                            valueIndx++;
                            endV = i - 2;

                            if (valueIndx == 1)
                            {
                                numberUser = str.substr(startV, i - startV);
                                break;
                            }

                            startV = i + 1;
                        }
                    }
                    //string orderToTcp = "R@";
                    //orderToTcp += order.NumberUser + "@";
                        //const_cast<char*>(_userId.c_str())
                    tradeClient->cancelOrder(numberUser.c_str());
                }

                MessagesIn.erase(it);
                break;
            }

            // чтение сообщений ИЗ ШЛЮЗА

            if (isStarted == false)
            {
                continue;
            }

            list <string>::iterator itt;
            for (itt = tradeClient->Messages.begin(); itt != tradeClient->Messages.end(); itt++)
            {
                string str = *itt;
                //cout << "send to messagesOut " << (str) << " \n";

                MessagesOut.push_back(str);
                tradeClient->Messages.erase(itt);
                break;
            }
        }
        catch (...)
        {
            Logger::info("Exception. Main. Socket closed", __FUNCTION__, __LINE__);
            return 0;
        }
    }

    // Destroy the instance and release resources
    pTraderApi->RegisterSpi(NULL);
    pTraderApi->Release();

    delete tradeClient;

    return 0;
}