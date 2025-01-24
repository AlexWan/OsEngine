#include "ThostFtdcMdApi.h"
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

// Trading server address：demo9.atplatform.cn:40905
// Quote server address：demo9.atplatform.cn:41213

static string _mdServerUri;
static string _brokerId;
static string _userId;
static string _userPassword;

static char* MD_SERVER_URI = "tcp://demo9.atplatform.cn:41213"; //"tcp://192.168.101.225:41213";
static char* BROKER_ID = "ALEX001";//"broker_id";
static char* USER_ID = "alex1";//"user_id";
static char* USER_PASSWORD = "GS#WM3sS";//"user_password";

static std::mutex mutex_array_all;
static std::mutex mutex_array_in;
static std::mutex mutex_array_out;

static bool isDisconnected;

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

class TestMdClient : public CThostFtdcMdSpi 

{
public:
    TestMdClient(CThostFtdcMdApi *pMdApi)
        : mdApi(pMdApi),
        requestID(1) {}
    ~TestMdClient() {}
protected:

    virtual void OnFrontConnected() 
    {
        Logger::info("[INFO] [%s:%3d]: Front connected.", __FUNCTION__, __LINE__);
        doLogin();
    }

    virtual void OnFrontDisconnected(int reason) 
    {
        try
        {
            isDisconnected = true;

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

    virtual void OnRspUserLogin(CThostFtdcRspUserLoginField *login, 
        CThostFtdcRspInfoField *status, int requestID, bool isLast) 
    {
        if (status != NULL && status->ErrorID != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Failed to login: errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                status->ErrorID, status->ErrorMsg);
            return;
        }

        if (login == NULL) 
        {
            Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of login info.", __FUNCTION__, __LINE__);    // Should not happen
            return;
        }

        Logger::info("[INFO] [%s:%3d]: Login succeed: brokerID=%s, userID=%s, sessionID=%d, tradingDay=%s.", __FUNCTION__, __LINE__,
            login->BrokerID, login->UserID, login->SessionID, login->TradingDay);

        Messages.push_back("Connect%");
        ofstream MyFile("Atp_Router\\Files\\ConnectData.txt");
        MyFile.close();

        // Do other things, e.g. subscribe contract
        //subscribeContract("CU3M-LME");
    }

    virtual void OnRspSubMarketData(CThostFtdcSpecificInstrumentField *inst, 
        CThostFtdcRspInfoField *status, int requestID, bool isLast) 
    {
        if (status != NULL && status->ErrorID != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Failed to subscribe market data: instrumentID=%s, errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                inst == NULL ? "NULL" : inst->InstrumentID, status->ErrorID, status->ErrorMsg);
            return;
        }

        if (inst == NULL) 
        {
            Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of instrument info.", __FUNCTION__, __LINE__);    // Should not happen
            return;
        }

        Logger::info("[INFO] [%s:%3d]: Subscribed market data: instrumentID=%s.", __FUNCTION__, __LINE__, inst->InstrumentID);
    }

    virtual void OnRspUnSubMarketData(CThostFtdcSpecificInstrumentField *inst, 
        CThostFtdcRspInfoField *status, int requestID, bool isLast) 
    {
        if (status != NULL && status->ErrorID != 0) {
            Logger::info("[ERROR] [%s:%3d]: Failed to unsubscribe market data: instrumentID=%s, errorID=%d, errorMsg=%s", __FUNCTION__, __LINE__,
                inst == NULL ? "NULL" : inst->InstrumentID, status->ErrorID, status->ErrorMsg);
            return;
        }

        if (inst == NULL) 
        {
            Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of instrument info.", __FUNCTION__, __LINE__);    // Should not happen
            return;
        }

        Logger::info("[INFO] [%s:%3d]: Unsubscribed market data: instrumentID=%s.", __FUNCTION__, __LINE__, inst->InstrumentID);
    }

    virtual void OnRtnDepthMarketData(CThostFtdcDepthMarketDataField* data)
    {
        try
        {
            if (data == NULL)
            {
                Logger::info("[ERROR] [%s:%3d]: Invalid server response, got null pointer of market data info.", __FUNCTION__, __LINE__);    // Should not happen
                return;
            }
            // Handle market data
            // Logger::info("[INFO] [%s:%3d]: MarketData: instrumentID=%s, exchangeID=%s, tradingDay=%s, lastPrice=%f, bidPrice1=%f, bidVolume1=%d, askPrice2=%f, askVolume2=%d.", __FUNCTION__, __LINE__,
            //     data->InstrumentID, data->ExchangeID, data->TradingDay, data->LastPrice, data->BidPrice1, data->BidVolume1, data->AskPrice2, data->AskVolume2);

             // const char *format =  "[INFO] [%s:%3d]: MarketData: instrumentID=%s, exchangeID=%s, tradingDay=%s, lastPrice=%f, bidPrice1=%f, bidVolume1=%d, askPrice2=%f, askVolume2=%d.", __FUNCTION__, __LINE__,
              //data->InstrumentID, data->ExchangeID, data->TradingDay, data->LastPrice, data->BidPrice1, data->BidVolume1, data->AskPrice2, data->AskVolume2;

            string info = "Md@";
            info += (data->InstrumentID);
            info += "@";
            info += (data->TradingDay);
            info += "@";
            info += (data->UpdateTime);
            info += "@";
            info += to_string(data->LastPrice);
            info += "@";
            info += to_string(data->Volume);
            info += "@";

            info += to_string(data->BidPrice1);
            info += "@";
            info += to_string(data->BidVolume1);
            info += "@";

            info += to_string(data->AskPrice1);
            info += "@";
            info += to_string(data->AskVolume1);
            info += "%";

            mutex_array_all.lock();
            Messages.push_back(info);
            mutex_array_all.unlock();
        }
        catch (exception error)
        {
            // ignore
        }
    }

public:
    void subscribeContract(char *instrumentID) 
    {
        char *instrumentIDs[] = { instrumentID };
        int rtnCode = mdApi->SubscribeMarketData(instrumentIDs, sizeof(instrumentIDs) / sizeof(char*));

        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to subscribe market data: instrumentID=%s.", __FUNCTION__, __LINE__, instrumentID);
        }
    }

    void unsubscribeContract(char *instrumentID) 
    {
        char *instrumentIDs[] = { instrumentID };
        int rtnCode = mdApi->UnSubscribeMarketData(instrumentIDs, sizeof(instrumentIDs) / sizeof(char*));
        if (rtnCode != 0) 
        {
            Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode);
        } 
        else 
        {
            Logger::info("[INFO] [%s:%3d]: Requested to unsubscribe market data: instrumentID=%s.", __FUNCTION__, __LINE__, instrumentID);
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
        int rtnCode = mdApi->ReqUserLogin(&field, nextRequestID());
        if (rtnCode != 0) { Logger::info("[ERROR] [%s:%3d]: Request failed: code=%d.", __FUNCTION__, __LINE__, rtnCode); }
    }

    int nextRequestID() 
    { 
        return requestID++; 
    }

private:
    CThostFtdcMdApi *mdApi;
    int requestID;
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

// logic

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

            if (valueIndx == 1)
            {
                _brokerId = str.substr(startV, i - startV);

                BROKER_ID = const_cast<char*>(_brokerId.c_str());
            }
            if (valueIndx == 2)
            {
                _userId = str.substr(startV, i - startV);
                USER_ID = const_cast<char*>(_userId.c_str());
            }
            if (valueIndx == 3)
            {
                _userPassword = str.substr(startV, i - startV);
                USER_PASSWORD = const_cast<char*>(_userPassword.c_str());
            }
            if (valueIndx == 6)
            {
                _mdServerUri = str.substr(startV, i - startV);
                MD_SERVER_URI = const_cast<char*>(_mdServerUri.c_str());
            }

            startV = i + 1;
        }
    }
}

DWORD WINAPI serverReceive(LPVOID lpParam)
{ //Получение данных от клиента
    char buffer[1024] = {0}; //Буфер для данных

    SOCKET client = *(SOCKET*)lpParam; //Сокет для клиента

    MessagesIn.clear();
    MessagesOut.clear();

    while (true)
    { //Цикл работы сервера
        try
        {
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

            if (buffer[0] == 'C')
            {// установить соединение
                string buff = buffer;
                SetConnectSettings(buff);
                mutex_array_in.lock();
                MessagesIn.push_back(buffer);
                mutex_array_in.unlock();
            }

            if (buffer[0] == 'D')
            {// разорвать соединение
                mutex_array_in.lock();
                MessagesIn.push_back("Delete");
                mutex_array_in.unlock();
            }

            if (buffer[0] == 'S')
            {// подписка на бумагу
                mutex_array_in.lock();
                MessagesIn.push_back(buffer);
                mutex_array_in.unlock();
            }

            mutex_array_out.lock();

            if (buffer[0] == 'P'
                && MessagesOut.size() != 0)
            {
                list <string>::iterator it;

                bool sendMessage = false;

                for (it = MessagesOut.begin(); it != MessagesOut.end(); it++)
                {
                    string str = *it;
                    send(client, str.c_str(), sizeof(buffer), 0); // отправляем ответ
                    MessagesOut.erase(it);

                    if (MessagesOut.size() > 100)
                    {
                        MessagesOut.clear();
                    }

                    sendMessage = true;
                    break;
                }

                if (sendMessage == false)
                {
                    send(client, buffer, sizeof(buffer), 0); // отправляем ответ
                }

                memset(buffer, 0, sizeof(buffer)); //Очистить буфер
                mutex_array_out.unlock();
                continue;
            }
            else
            {
                send(client, buffer, sizeof(buffer), 0); // отправляем ответ
                memset(buffer, 0, sizeof(buffer)); //Очистить буфер
                mutex_array_out.unlock();
                continue;
            }
        }
        catch(exception error)
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
    serverAddr.sin_port = htons(5555);

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
        SocketWorkPlace();
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

    std::thread thread(ThreadWorkerPlace);

    CThostFtdcMdApi* pMdApi;
    TestMdClient* mdClient;

    //готовая читалка данных из сообщений сокетов

    std::chrono::milliseconds timespan(10);


    bool isStarted = false;

    while (true)
    {
        try
        {
            std::this_thread::sleep_for(timespan);


            if (isDisconnected)
            {
                return -1;
            }

            // подача сообщений В ШЛЮЗ

            list <string>::iterator it;

            mutex_array_in.lock();

            for (it = MessagesIn.begin(); it != MessagesIn.end(); it++)
            {
                string str = *it;

                if (str[0] == 'C')
                { // запрос на подключение к Шлюзу
                    pMdApi = CThostFtdcMdApi::CreateFtdcMdApi();
                    mdClient = new TestMdClient(pMdApi);
                    pMdApi->RegisterSpi(mdClient);
                    pMdApi->RegisterFront((char*)MD_SERVER_URI);
                    pMdApi->Init();  // Start connecting

                    isStarted = true;
                }
                else if (str[0] == 'D')
                { // запрос на выключение

                }
                else if (str[0] == 'S')
                { // запрос на подключение бумаги

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

                std:string const secName = buff.substr(2, endV);
                    char* cstr2 = const_cast<char*>(secName.c_str());

                    mdClient->subscribeContract(cstr2);
                }

                MessagesIn.erase(it);
                break;
            }

            mutex_array_in.unlock();

            // чтение сообщений ИЗ ШЛЮЗА

            if (isStarted == false)
            {
                continue;
            }

            mutex_array_all.lock();

            list <string>::iterator itt;
            for (itt = mdClient->Messages.begin(); itt != mdClient->Messages.end(); itt++)
            {
                string str = *itt;
                //cout << (str) << " \n";

                mutex_array_out.lock();
                MessagesOut.push_back(str);
                mutex_array_out.unlock();

                //mdClient->Messages.erase(itt);
                //break;
            }
            mdClient->Messages.clear();

            mutex_array_all.unlock();
        }
        catch (exception e)
        {
            // ignore
        }
    }

    cout << "do exit by exit from cycle" << " \n";

    // Destroy the instance and release resources
    pMdApi->RegisterSpi(NULL);
    pMdApi->Release();
    delete mdClient;

    return 0;
}
