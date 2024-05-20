--~ Copyright (c) 2014-2020 QUIKSharp Authors https://github.com/finsight/QUIKSharp/blob/master/AUTHORS.md. All rights reserved.
--~ Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

local json = require ("dkjson")
local qsfunctions = {}

function qsfunctions.dispatch_and_process(msg)
    if qsfunctions[msg.cmd] then
        -- dispatch a command simply by a table lookup
        -- in qsfunctions method names must match commands
        local status, result = pcall(qsfunctions[msg.cmd], msg)
        if status then
            return result
        else
            msg.cmd = "lua_error"
            msg.lua_error = "Lua error: " .. result
            return msg
        end
    else
		log(to_json(msg), 3)
		msg.lua_error = "Command not implemented in Lua qsfunctions module: " .. msg.cmd
        msg.cmd = "lua_error"
        return msg
    end
end

---------------------
-- Debug functions --
---------------------

--- Returns Pong to Ping
-- @param msg message table
-- @return same msg table
function qsfunctions.ping(msg)
    -- need to know data structure the caller gives
    msg.t = 0 -- avoid time generation. Could also leave original
    if msg.data == "Ping" then
        msg.data = "Pong"
        return msg
    else
        msg.data = msg.data .. " is not Ping"
        return msg
    end
end

--- Echoes its message
function qsfunctions.echo(msg)
    return msg
end

--- Test error handling
function qsfunctions.divide_string_by_zero(msg)
    msg.data = "asd" / 0
    return msg
end

--- Is running inside quik
function qsfunctions.is_quik(msg)
    if getScriptPath then msg.data = 1 else msg.data = 0 end
    return msg
end

-----------------------
-- Service functions --
-----------------------

--- Функция предназначена для определения состояния подключения клиентского места к
-- серверу. Возвращает «1», если клиентское место подключено и «0», если не подключено.
function qsfunctions.isConnected(msg)
    -- set time when function was called
    msg.t = timemsec()
    msg.data = isConnected()
    return msg
end

--- Функция возвращает путь, по которому находится файл info.exe, исполняющий данный
-- скрипт, без завершающего обратного слэша («\»). Например, C:\QuikFront.
function qsfunctions.getWorkingFolder(msg)
    -- set time when function was called
    msg.t = timemsec()
    msg.data = getWorkingFolder()
    return msg
end

--- Функция возвращает путь, по которому находится запускаемый скрипт, без завершающего
-- обратного слэша («\»). Например, C:\QuikFront\Scripts.
function qsfunctions.getScriptPath(msg)
    -- set time when function was called
    msg.t = timemsec()
    msg.data = getScriptPath()
    return msg
end

--- Функция возвращает значения параметров информационного окна (пункт меню
-- Связь / Информационное окно…).
function qsfunctions.getInfoParam(msg)
    -- set time when function was called
    msg.t = timemsec()
    msg.data = getInfoParam(msg.data)
    return msg
end

--- Функция отображает сообщения в терминале QUIK.
function qsfunctions.message(msg)
    log(msg.data, 1)
    msg.data = ""
    return msg
end
function qsfunctions.warning_message(msg)
    log(msg.data, 2)
    msg.data = ""
    return msg
end
function qsfunctions.error_message(msg)
    log(msg.data, 3)
    msg.data = ""
    return msg
end

--- Функция приостанавливает выполнение скрипта.
function qsfunctions.sleep(msg)
    delay(msg.data)
    msg.data = ""
    return msg
end

--- Функция для вывода отладочной информации.
function qsfunctions.PrintDbgStr(msg)
    log(msg.data, 0)
    msg.data = ""
    return msg
end

-- Выводит на график метку
function qsfunctions.addLabel(msg)
	local spl = split(msg.data, "|")
	local price, curdate, curtime, qty, path, id, algmnt, bgnd = spl[1], spl[2], spl[3], spl[4], spl[5], spl[6], spl[7], spl[8]
	label = {
			TEXT = "",
			IMAGE_PATH = path,
			ALIGNMENT = algmnt,
			YVALUE = tostring(price),
			DATE = tostring(curdate),
			TIME = tostring(curtime),
			R = 255,
			G = 255,
			B = 255,
			TRANSPARENCY = 0,
			TRANSPARENT_BACKGROUND = bgnd,
			FONT_FACE_NAME = "Arial",
			FONT_HEIGHT = "15",
			HINT = " " .. tostring(price) .. " " .. tostring(qty)
			}
	local res = AddLabel(id, label)
	msg.data = res
	return msg
end

-- Удаляем выбранную метку
function qsfunctions.delLabel(msg)
	local spl = split(msg.data, "|")
	local tag, id = spl[1], spl[2]
	DelLabel(tag, tonumber(id))
	msg.data = ""
	return msg
end

-- Удаляем все метки с графика
function qsfunctions.delAllLabels(msg)
	local spl = split(msg.data, "|")
	local id = spl[1]
	DelAllLabels(id)
	msg.data = ""
	return msg
end

---------------------
-- Class functions --
---------------------

--- Функция предназначена для получения списка кодов классов, переданных с сервера в ходе сеанса связи.
function qsfunctions.getClassesList(msg)
    msg.data = getClassesList()
--    if  msg.data then log(msg.data) else log("getClassesList returned nil") end
    return msg
end

--- Функция предназначена для получения информации о классе.
function qsfunctions.getClassInfo(msg)
    msg.data = getClassInfo(msg.data)
--    if msg.data then log(msg.data.name) else log("getClassInfo  returned nil") end
    return msg
end

--- Функция предназначена для получения списка кодов бумаг для списка классов, заданного списком кодов.
function qsfunctions.getClassSecurities(msg)
    msg.data = getClassSecurities(msg.data)
--    if msg.data then log(msg.data) else log("getClassSecurities returned nil") end
    return msg
end

--- Функция получает информацию по указанному классу и бумаге.
function qsfunctions.getSecurityInfo(msg)
    local spl = split(msg.data, "|")
    local class_code, sec_code = spl[1], spl[2]
    msg.data = getSecurityInfo(class_code, sec_code)
    return msg
end

--- Функция берет на вход список из элементов в формате class_code|sec_code и возвращает список ответов функции getSecurityInfo. 
-- Если какая-то из бумаг не будет найдена, вместо ее значения придет null
function qsfunctions.getSecurityInfoBulk(msg)
	local result = {}
	for i=1,#msg.data do
		local spl = split(msg.data[i], "|")
		local class_code, sec_code = spl[1], spl[2]

		local status, security = pcall(getSecurityInfo, class_code, sec_code)
		if status and security then
			table.insert(result, security)
		else
			if not status then
				log("Error happened while calling getSecurityInfoBulk with ".. class_code .. "|".. sec_code .. ": ".. security)
			end
			table.insert(result, json.null)
		end
	end
	msg.data = result
	return msg
end

--- Функция предназначена для определения класса по коду инструмента из заданного списка классов.
function qsfunctions.getSecurityClass(msg)
    local spl = split(msg.data, "|")
    local classes_list, sec_code = spl[1], spl[2]

	for class_code in string.gmatch(classes_list,"([^,]+)") do
		if getSecurityInfo(class_code,sec_code) then
			msg.data = class_code
			return msg
		end
	end
	msg.data = ""
	return msg
end

--- Функция возвращает код клиента
function qsfunctions.getClientCode(msg)
	for i=0,getNumberOf("MONEY_LIMITS")-1 do
		local clientcode = getItem("MONEY_LIMITS",i).client_code
		if clientcode ~= nil then
			msg.data = clientcode
			return msg
		end
    end
	return msg
end

--- Функция возвращает торговый счет для запрашиваемого кода класса
function qsfunctions.getTradeAccount(msg)
	for i=0,getNumberOf("trade_accounts")-1 do
		local trade_account = getItem("trade_accounts",i)
		if string.find(trade_account.class_codes,'|' .. msg.data .. '|',1,1) then
			msg.data = trade_account.trdaccid
			return msg
		end
	end
	return msg
end

--- Функция возвращает торговые счета в системе, у которых указаны поддерживаемые классы инструментов.
function qsfunctions.getTradeAccounts(msg)
	local trade_accounts = {}
	for i=0,getNumberOf("trade_accounts")-1 do
		local trade_account = getItem("trade_accounts",i)
		if trade_account.class_codes ~= "" then
			table.insert(trade_accounts, trade_account)
		end
	end
	msg.data = trade_accounts
	return msg
end



---------------------------------------------------------------------
-- Order Book functions (Р¤СѓРЅРєС†РёРё РґР»СЏ СЂР°Р±РѕС‚С‹ СЃРѕ СЃС‚Р°РєР°РЅРѕРј РєРѕС‚РёСЂРѕРІРѕРє) --
---------------------------------------------------------------------

--- Функция заказывает на сервер получение стакана по указанному классу и бумаге.
function qsfunctions.Subscribe_Level_II_Quotes(msg)
    local spl = split(msg.data, "|")
    local class_code, sec_code = spl[1], spl[2]
    msg.data = Subscribe_Level_II_Quotes(class_code, sec_code)
    return msg
end

--- Функция отменяет заказ на получение с сервера стакана по указанному классу и бумаге.
function qsfunctions.Unsubscribe_Level_II_Quotes(msg)
    local spl = split(msg.data, "|")
    local class_code, sec_code = spl[1], spl[2]
    msg.data = Unsubscribe_Level_II_Quotes(class_code, sec_code)
    return msg
end

--- Функция позволяет узнать, заказан ли с сервера стакан по указанному классу и бумаге.
function qsfunctions.IsSubscribed_Level_II_Quotes(msg)
    local spl = split(msg.data, "|")
    local class_code, sec_code = spl[1], spl[2]
    msg.data = IsSubscribed_Level_II_Quotes(class_code, sec_code)
    return msg
end

--- Функция предназначена для получения стакана по указанному классу и инструменту.
function qsfunctions.GetQuoteLevel2(msg)
    local spl = split(msg.data, "|")
    local class_code, sec_code = spl[1], spl[2]
    local server_time = getInfoParam("SERVERTIME")
    local status, ql2 = pcall(getQuoteLevel2, class_code, sec_code)
    if status then
        msg.data				= ql2
        msg.data.class_code		= class_code
        msg.data.sec_code		= sec_code
        msg.data.server_time	= server_time
        sendCallback(msg)
    else
        OnError(ql2)
    end
    return msg
end

-----------------------
-- Trading functions --
-----------------------

--- отправляет транзакцию на сервер и возвращает пустое сообщение, которое
-- будет проигноировано. Вместо него, отправитель будет ждать события
-- OnTransReply, из которого по TRANS_ID он получит результат отправленной транзакции
function qsfunctions.sendTransaction(msg)
    local res = sendTransaction(msg.data)
    if res~="" then
        -- error handling
        msg.cmd = "lua_transaction_error"
        msg.lua_error = res
        return msg
    else
        -- transaction sent
        msg.data = true
        return msg
    end
end

--- Функция заказывает получение параметров Таблицы текущих торгов. В случае успешного завершения функция возвращает «true», иначе – «false»
function qsfunctions.paramRequest(msg)
    local spl = split(msg.data, "|")
    local class_code, sec_code, param_name = spl[1], spl[2], spl[3]
    msg.data = ParamRequest(class_code, sec_code, param_name)
    return msg
end

--- Функция принимает список строк (JSON Array) в формате class_code|sec_code|param_name, вызывает функцию paramRequest для каждой строки. 
-- Возвращает список ответов в том же порядке
function qsfunctions.paramRequestBulk(msg)
	local result = {}
	for i=1,#msg.data do
		local spl = split(msg.data[i], "|")
		local class_code, sec_code, param_name = spl[1], spl[2], spl[3]
		table.insert(result, ParamRequest(class_code, sec_code, param_name))
	end
	msg.data = result
	return msg
end

--- Функция отменяет заказ на получение параметров Таблицы текущих торгов. В случае успешного завершения функция возвращает «true», иначе – «false»
function qsfunctions.cancelParamRequest(msg)
    local spl = split(msg.data, "|")
    local class_code, sec_code, param_name = spl[1], spl[2], spl[3]
    msg.data = CancelParamRequest(class_code, sec_code, param_name)
    return msg
end

--- Функция принимает список строк (JSON Array) в формате class_code|sec_code|param_name, вызывает функцию CancelParamRequest для каждой строки.
-- Возвращает список ответов в том же порядке
function qsfunctions.cancelParamRequestBulk(msg)
	local result = {}
	for i=1,#msg.data do
		local spl = split(msg.data[i], "|")
		local class_code, sec_code, param_name = spl[1], spl[2], spl[3]
		table.insert(result, CancelParamRequest(class_code, sec_code, param_name))
	end
	msg.data = result
	return msg
end

--- Функция предназначена для получения значений всех параметров биржевой информации из Таблицы текущих значений параметров.
-- С помощью этой функции можно получить любое из значений Таблицы текущих значений параметров для заданных кодов класса и бумаги.
function qsfunctions.getParamEx(msg)
    local spl = split(msg.data, "|")
    local class_code, sec_code, param_name = spl[1], spl[2], spl[3]
    msg.data = getParamEx(class_code, sec_code, param_name)
    return msg
end

--- Функция предназначена для получения значении? всех параметров биржевои? информации из Таблицы текущих торгов
-- с возможностью в дальнеи?шем отказаться от получения определенных параметров, заказанных с помощью функции ParamRequest.
-- Для отказа от получения какого-либо параметра воспользуи?тесь функциеи? CancelParamRequest.
-- Функция возвращает таблицу Lua с параметрами, аналогичными параметрам, возвращаемым функциеи? getParamEx
function qsfunctions.getParamEx2(msg)
    local spl = split(msg.data, "|")
    local class_code, sec_code, param_name = spl[1], spl[2], spl[3]
    msg.data = getParamEx2(class_code, sec_code, param_name)
    return msg
end

--- Функция принимает список строк (JSON Array) в формате class_code|sec_code|param_name и возвращает результаты вызова
-- функции getParamEx2 для каждой строки запроса в виде списка в таком же порядке, как в запросе
function qsfunctions.getParamEx2Bulk(msg)
	local result = {}
	for i=1,#msg.data do
		local spl = split(msg.data[i], "|")
		local class_code, sec_code, param_name = spl[1], spl[2], spl[3]
		table.insert(result, getParamEx2(class_code, sec_code, param_name))
	end
	msg.data = result
    return msg
end

-- Функция предназначена для получения информации по бумажным лимитам.
function qsfunctions.getDepo(msg)
    local spl = split(msg.data, "|")
    local clientCode, firmId, secCode, account = spl[1], spl[2], spl[3], spl[4]
    msg.data = getDepo(clientCode, firmId, secCode, account)
    return msg
end

-- Функция предназначена для получения информации по бумажным лимитам.
function qsfunctions.getDepoEx(msg)
    local spl = split(msg.data, "|")
    local firmId, clientCode, secCode, account, limit_kind = spl[1], spl[2], spl[3], spl[4], spl[5]
    msg.data = getDepoEx(firmId, clientCode, secCode, account, tonumber(limit_kind))
    return msg
end

-- Функция для получения информации по денежным лимитам.
function qsfunctions.getMoney(msg)
    local spl = split(msg.data, "|")
    local client_code, firm_id, tag, curr_code = spl[1], spl[2], spl[3], spl[4]
    msg.data = getMoney(client_code, firm_id, tag, curr_code)
    return msg
end

-- Функция для получения информации по денежным лимитам указанного типа.
function qsfunctions.getMoneyEx(msg)
    local spl = split(msg.data, "|")
    local firm_id, client_code, tag, curr_code, limit_kind = spl[1], spl[2], spl[3], spl[4], spl[5]
    msg.data = getMoneyEx(firm_id, client_code, tag, curr_code, tonumber(limit_kind))
    return msg
end

-- Функция возвращает информацию по всем денежным лимитам.
function qsfunctions.getMoneyLimits(msg)
    local limits = {}
    for i=0,getNumberOf("money_limits")-1 do
        local limit = getItem("money_limits",i)
	    table.insert(limits, limit)
    end
     msg.data = limits
    return msg
end

-- Функция предназначена для получения информации по фьючерсным лимитам.
function qsfunctions.getFuturesLimit(msg)
    local spl = split(msg.data, "|")
    local firmId, accId, limitType, currCode = spl[1], spl[2], spl[3], spl[4]
	local result, err = getFuturesLimit(firmId, accId, limitType*1, currCode)
	if result then
		msg.data = result
	else
		log("Futures limit returns nil", 3)
		msg.data = nil
	end
    return msg
end

-- Функция возвращает информацию по фьючерсным лимитам для всех торговых счетов.
function qsfunctions.getFuturesClientLimits(msg)
    local limits = {}
    for i=0,getNumberOf("futures_client_limits")-1 do
        local limit = getItem("futures_client_limits",i)
	    table.insert(limits, limit)
    end
     msg.data = limits
    return msg
end

function qsfunctions.getFuturesHolding(msg)
    local spl = split(msg.data, "|")
    local firmId, accId, secCode, posType = spl[1], spl[2], spl[3], spl[4]
	local result, err = getFuturesHolding(firmId, accId, secCode, posType*1)
	if result then
		msg.data = result
	else
		--log("Futures holding returns nil", 3)
		msg.data = nil
	end
    return msg
end

-- Функция возвращает таблицу заявок (всю или по заданному инструменту)
function qsfunctions.get_orders(msg)
	if msg.data ~= "" then
		local spl = split(msg.data, "|")
		class_code, sec_code = spl[1], spl[2]
	end

	local orders = {}
	for i = 0, getNumberOf("orders") - 1 do
		local order = getItem("orders", i)
		if msg.data == "" or (order.class_code == class_code and order.sec_code == sec_code) then
			table.insert(orders, order)
		end
	end
	msg.data = orders
	return msg
end

-- Функция возвращает заявку по заданному инструменту и ID-транзакции
function qsfunctions.getOrder_by_ID(msg)
	if msg.data ~= "" then
		local spl = split(msg.data, "|")
		class_code, sec_code, trans_id = spl[1], spl[2], spl[3]
	end

	local order_num = 0
	local res
	for i = 0, getNumberOf("orders") - 1 do
		local order = getItem("orders", i)
		if order.class_code == class_code and order.sec_code == sec_code and order.trans_id == tonumber(trans_id) and order.order_num > order_num then
			order_num = order.order_num
			res = order
		end
	end
	msg.data = res
	return msg
end

---- Функция возвращает заявку по номеру
function qsfunctions.getOrder_by_Number(msg)
	for i=0,getNumberOf("orders")-1 do
		local order = getItem("orders",i)
		if order.order_num == tonumber(msg.data) then
			msg.data = order
			return msg
		end
	end
	return msg
end

--- Возвращает заявку по её номеру и классу инструмента ---
--- На основе http://help.qlua.org/ch4_5_1_1.htm ---
function qsfunctions.get_order_by_number(msg)
	local spl = split(msg.data, "|")
	local class_code = spl[1]
	local order_id = tonumber(spl[2])
	msg.data = getOrderByNumber(class_code, order_id)
	return msg
end

--- Возвращает список записей из таблицы 'Лимиты по бумагам'
--- На основе http://help.qlua.org/ch4_6_11.htm и http://help.qlua.org/ch4_5_3.htm
function qsfunctions.get_depo_limits(msg)
	local sec_code = msg.data
	local count = getNumberOf("depo_limits")
	local depo_limits = {}
	for i = 0, count - 1 do
		local depo_limit = getItem("depo_limits", i)
		if msg.data == "" or depo_limit.sec_code == sec_code then
			table.insert(depo_limits, depo_limit)
		end
	end
	msg.data = depo_limits
	return msg
end

-- Функция возвращает таблицу сделок (всю или по заданному инструменту)
function qsfunctions.get_trades(msg)
	if msg.data ~= "" then
		local spl = split(msg.data, "|")
		class_code, sec_code = spl[1], spl[2]
	end

	local trades = {}
	for i = 0, getNumberOf("trades") - 1 do
		local trade = getItem("trades", i)
		if msg.data == "" or (trade.class_code == class_code and trade.sec_code == sec_code) then
			table.insert(trades, trade)
		end
	end
	msg.data = trades
	return msg
end

-- Функция возвращает таблицу сделок по номеру заявки
function qsfunctions.get_Trades_by_OrderNumber(msg)
	local order_num = tonumber(msg.data)

	local trades = {}
	for i = 0, getNumberOf("trades") - 1 do
		local trade = getItem("trades", i)
		if trade.order_num == order_num then
			table.insert(trades, trade)
		end
	end
	msg.data = trades
	return msg
end

-- Функция предназначена для получения значений параметров таблицы «Клиентский портфель», соответствующих идентификатору участника торгов «firmid» и коду клиента «client_code».
function qsfunctions.getPortfolioInfo(msg)
    local spl = split(msg.data, "|")
    local firmId, clientCode = spl[1], spl[2]
    msg.data = getPortfolioInfo(firmId, clientCode)
    return msg
end

-- Функция предназначена для получения значений параметров таблицы «Клиентский портфель», соответствующих идентификатору участника торгов «firmid», коду клиента «client_code» и виду лимита «limit_kind».
function qsfunctions.getPortfolioInfoEx(msg)
    local spl = split(msg.data, "|")
    local firmId, clientCode, limit_kind = spl[1], spl[2], spl[3]
    msg.data = getPortfolioInfoEx(firmId, clientCode, tonumber(limit_kind))
    return msg
end


--------------------------
-- OptionBoard functions --
--------------------------
function qsfunctions.getOptionBoard(msg)
    local spl = split(msg.data, "|")
    local classCode, secCode = spl[1], spl[2]
	local result, err = getOptions(classCode, secCode)
	if result then
		msg.data = result
	else
		log("Option board returns nil", 3)
		msg.data = nil
	end
    return msg
end

function getOptions(classCode,secCode)
	--classCode = "SPBOPT"
--BaseSecList="RIZ6"
local SecList = getClassSecurities(classCode) --все сразу
local t={}
local p={}
for sec in string.gmatch(SecList, "([^,]+)") do --перебираем опционы по очереди.
            local Optionbase=getParamEx(classCode,sec,"optionbase").param_image
            local Optiontype=getParamEx(classCode,sec,"optiontype").param_image
            if (string.find(secCode,Optionbase)~=nil) then


                p={
                    ["code"]=getParamEx(classCode,sec,"code").param_image,
					["Name"]=getSecurityInfo(classCode,sec).name,
					["DAYS_TO_MAT_DATE"]=getParamEx(classCode,sec,"DAYS_TO_MAT_DATE").param_value+0,
					["BID"]=getParamEx(classCode,sec,"BID").param_value+0,
					["OFFER"]=getParamEx(classCode,sec,"OFFER").param_value+0,
					["OPTIONBASE"]=getParamEx(classCode,sec,"optionbase").param_image,
					["OPTIONTYPE"]=getParamEx(classCode,sec,"optiontype").param_image,
					["Longname"]=getParamEx(classCode,sec,"longname").param_image,
					["shortname"]=getParamEx(classCode,sec,"shortname").param_image,
					["Volatility"]=getParamEx(classCode,sec,"volatility").param_value+0,
					["Strike"]=getParamEx(classCode,sec,"strike").param_value+0
                    }



                        table.insert( t, p )
            end

end
return t
end

--------------------------
-- Stop order functions --
--------------------------

--- Возвращает список стоп-заявок
function qsfunctions.get_stop_orders(msg)
	if msg.data ~= "" then
		local spl = split(msg.data, "|")
		class_code, sec_code = spl[1], spl[2]
	end

	local count = getNumberOf("stop_orders")
	local stop_orders = {}
	for i = 0, count - 1 do
		local stop_order = getItem("stop_orders", i)
		if msg.data == "" or (stop_order.class_code == class_code and stop_order.sec_code == sec_code) then
			table.insert(stop_orders, stop_order)
		end
	end
	msg.data = stop_orders
	return msg
end

-------------------------
--- Candles functions ---
-------------------------

--- Возвращаем количество свечей по тегу
function qsfunctions.get_num_candles(msg)
	log("Called get_num_candles" .. msg.data, 2)
	local spl = split(msg.data, "|")
	local tag = spl[1]

	msg.data = getNumCandles(tag) * 1
	return msg
end


--- Возвращаем все свечи по идентификатору графика. График должен быть открыт
function qsfunctions.get_candles(msg)
	log("Called get_candles" .. msg.data, 2)
	local spl = split(msg.data, "|")
	local tag = spl[1]
	local line = tonumber(spl[2])
	local first_candle = tonumber(spl[3])
	local count = tonumber(spl[4])
	if count == 0 then
		count = getNumCandles(tag) * 1
	end
	log("Count: " .. count, 2)
	local t,n,l = getCandlesByIndex(tag, line, first_candle, count)
	log("Candles table size: " .. n, 2)
	log("Label: " .. l, 2)
	local candles = {}
	for i = 0, count - 1 do
		table.insert(candles, t[i])
	end
	msg.data = candles
	return msg
end

--- Возвращаем все свечи по заданному инструменту и интервалу
function qsfunctions.get_candles_from_data_source(msg)
	local ds, is_error = create_data_source(msg)
	if not is_error then
		--- датасорс изначально приходит пустой, нужно некоторое время подождать пока он заполниться данными
		repeat sleep(1) until ds:Size() > 0

		local count = tonumber(split(msg.data, "|")[4]) --- возвращаем последние count свечей. Если равен 0, то возвращаем все доступные свечи.
		local class, sec, interval = get_candles_param(msg)
		local candles = {}
		local start_i = count == 0 and 1 or math.max(1, ds:Size() - count + 1)
		for i = start_i, ds:Size() do
			local candle = fetch_candle(ds, i)
			candle.sec = sec
			candle.class = class
			candle.interval = interval
			table.insert(candles, candle)
		end
		ds:Close()
		msg.data = candles
	end
	return msg
end

function create_data_source(msg)
	local class, sec, interval = get_candles_param(msg)
	local ds, error_descr = CreateDataSource(class, sec, interval)
	local is_error = false
	if(error_descr ~= nil) then
		msg.cmd = "lua_create_data_source_error"
		msg.lua_error = error_descr
		is_error = true
	elseif ds == nil then
		msg.cmd = "lua_create_data_source_error"
		msg.lua_error = "Can't create data source for " .. class .. ", " .. sec .. ", " .. tostring(interval)
		is_error = true
	end
	return ds, is_error
end

function fetch_candle(data_source, index)
	local candle = {}
	candle.low   = data_source:L(index)
	candle.close = data_source:C(index)
	candle.high = data_source:H(index)
	candle.open = data_source:O(index)
	candle.volume = data_source:V(index)
	candle.datetime = data_source:T(index)
	return candle
end

--- Словарь открытых подписок (datasources) на свечи
data_sources = {}
last_indexes = {}

--- Подписаться на получения свечей по заданному инструмент и интервалу
function qsfunctions.subscribe_to_candles(msg)
	local ds, is_error = create_data_source(msg)
	if not is_error then
		local class, sec, interval = get_candles_param(msg)
		local key = get_key(class, sec, interval)
		data_sources[key] = ds
		last_indexes[key] = ds:Size()
		ds:SetUpdateCallback(
			function(index)
				data_source_callback(index, class, sec, interval)
			end)
	end
	return msg
end

function data_source_callback(index, class, sec, interval)
	local key = get_key(class, sec, interval)
	if index ~= last_indexes[key] then
		last_indexes[key] = index

		local candle = fetch_candle(data_sources[key], index - 1)
		candle.sec = sec
		candle.class = class
		candle.interval = interval

		local msg = {}
        msg.t = timemsec()
        msg.cmd = "NewCandle"
        msg.data = candle
        sendCallback(msg)
	end
end

--- Отписать от получения свечей по заданному инструменту и интервалу
function qsfunctions.unsubscribe_from_candles(msg)
	local class, sec, interval = get_candles_param(msg)
	local key = get_key(class, sec, interval)
	data_sources[key]:Close()
	data_sources[key] = nil
	last_indexes[key] = nil
	return msg
end

--- Проверить открыта ли подписка на заданный инструмент и интервал
function qsfunctions.is_subscribed(msg)
	local class, sec, interval = get_candles_param(msg)
	local key = get_key(class, sec, interval)
	for k, v in pairs(data_sources) do
		if key == k then
			msg.data = true;
			return  msg
		end
	end
	msg.data = false
	return msg
end

--- Возвращает из msg информацию о инструменте на который подписываемся и интервале
function get_candles_param(msg)
	local spl = split(msg.data, "|")
	return spl[1], spl[2], tonumber(spl[3])
end

--- Возвращает уникальный ключ для инструмента на который подписываемся и инетрвала
function get_key(class, sec, interval)
	return class .. "|" .. sec .. "|" .. tostring(interval)
end

-------------------------
--- UCP functions ---
-------------------------

--- Функция возвращает торговый счет срочного рынка, соответствующий коду клиента фондового рынка с единой денежной позицией
function qsfunctions.GetTrdAccByClientCode(msg)
    local spl = split(msg.data, "|")
    local firmId, clientCode = spl[1], spl[2]
    msg.data = getTrdAccByClientCode(firmId, clientCode)
    return msg
end

--- Функция возвращает код клиента фондового рынка с единой денежной позицией, соответствующий торговому счету срочного рынка
function qsfunctions.GetClientCodeByTrdAcc(msg)
    local spl = split(msg.data, "|")
    local firmId, trdAccId = spl[1], spl[2]
    msg.data = getClientCodeByTrdAcc(firmId, trdAccId)
    return msg
end

--- Функция предназначена для получения признака, указывающего имеет ли клиент единую денежную позицию
function qsfunctions.IsUcpClient(msg)
    local spl = split(msg.data, "|")
    local firmId, client = spl[1], spl[2]
    msg.data = isUcpClient(firmId, client)
    return msg
end

return qsfunctions
