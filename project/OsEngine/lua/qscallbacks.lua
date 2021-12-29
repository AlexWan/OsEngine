--~ // Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

package.path = package.path..";"..".\\?.lua;"..".\\?.luac"
package.cpath = package.cpath..";"..'.\\clibs\\?.dll'

local util = require("qsutils")

local qscallbacks = {}

--- Мы сохраняем пропущенные значения только если скрипт работает, но соединение прервалось
-- Если скрипт останавливается, то мы удаляем накопленные пропущенные значения
-- QuikSharp должен работать пока работает Квик, он не рассчитан на остановку внутри Квика.
-- При этом клиент может подключаться и отключаться сколько угодно и всегда получит пропущенные
-- сообщения после переподключения (если хватит места на диске)
local function CleanUp()
    -- close log
    closeLog()
    -- discard missed values if any
    discardMissedValues()
end

--- Функция вызывается когда соединение с QuikSharp клиентом обрывается
function OnQuikSharpDisconnected()
    -- TODO any recovery or risk management logic here
end

--- Функция вызывается когда скрипт ловит ошибку в функциях обратного вызова
function OnError(message)
	if is_connected then
		local msg = {}
		msg.t = timemsec()
		msg.cmd = "lua_error"
		msg.data = "Lua error: " .. message
		sendCallback(msg)
	end
end


--- Функция вызывается терминалом QUIK при установлении связи с сервером QUIK.
function OnConnected()
    if is_connected then
        local msg = {}
        msg.t = timemsec()
        msg.cmd = "OnConnected"
        msg.data = ""
        sendCallback(msg)
    end
end

--- Функция вызывается терминалом QUIK при установлении связи с сервером QUIK.
function OnDisconnected()
    if is_connected then
        local msg = {}
        msg.t = timemsec()
        msg.cmd = "OnDisconnected"
        msg.data = ""
        sendCallback(msg)
    end
end

--- Функция вызывается терминалом QUIK при получении обезличенной сделки.
function OnAllTrade(alltrade)
    if is_connected then
        local msg = {}
        msg.t = timemsec()
        msg.cmd = "OnAllTrade"
        msg.data = alltrade
        sendCallback(msg)
    end
end

--- Функция вызывается перед закрытием терминала QUIK.
function OnClose()
    if is_connected then
        local msg = {}
        msg.cmd = "OnClose"
        msg.t = timemsec()
        msg.data = ""
        sendCallback(msg)
    end
    CleanUp()
end

--- Функция вызывается терминалом QUIK перед вызовом функции main().
-- В качестве параметра принимает значение полного пути к запускаемому скрипту.
function OnInit(script_path)
    if is_connected then
        local msg = {}
        msg.cmd = "OnInit"
        msg.t = timemsec()
        msg.data = script_path
        sendCallback(msg)
    end
    log("Hello, QuikSharp! Running inside Quik from the path: "..getScriptPath(), 1)
end

--- Функция вызывается терминалом QUIK при в таблице заявок.
function OnOrder(order)
    local msg = {}
    msg.t = timemsec()
    msg.id = nil -- значение в order.trans_id
    msg.data = order
    msg.cmd = "OnOrder"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при получении изменения стакана котировок.
function OnQuote(class_code, sec_code)
    if true then -- is_connected
        local msg = {}
        msg.cmd = "OnQuote"
        msg.t = timemsec()
        local server_time = getInfoParam("SERVERTIME")
        local status, ql2 = pcall(getQuoteLevel2, class_code, sec_code)
        if status then
            msg.data = ql2
            msg.data.class_code = class_code
            msg.data.sec_code = sec_code
            msg.data.server_time = server_time
            sendCallback(msg)
        else
            OnError(ql2)
        end
    end
end

--- Функция вызывается терминалом QUIK при остановке скрипта из диалога управления и при закрытии терминала QUIK.
function OnStop(s)
    is_started = false

    if is_connected then
        local msg = {}
        msg.cmd = "OnStop"
        msg.t = timemsec()
        msg.data = s
        sendCallback(msg)
    end
    log("Bye, QuikSharp!")
    CleanUp()
    --	send disconnect
    return 1000
end

--- Функция вызывается терминалом QUIK при получении сделки.
function OnTrade(trade)
    local msg = {}
    msg.t = timemsec()
    msg.id = nil -- значение в OnTrade.trans_id
    msg.data = trade
    msg.cmd = "OnTrade"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при получении ответа на транзакцию пользователя.
function OnTransReply(trans_reply)
    local msg = {}
    msg.t = timemsec()
    msg.id = nil -- значение в trans_reply.trans_id
    msg.data = trans_reply
    msg.cmd = "OnTransReply"
    sendCallback(msg)
end

function OnStopOrder(stop_order)
	local msg = {}
    msg.t = timemsec()
    msg.data = stop_order
    msg.cmd = "OnStopOrder"
    sendCallback(msg)
end

function OnParam(class_code, sec_code)
    local msg = {}
    msg.cmd = "OnParam"
    msg.t = timemsec()
	local dat = {}
	dat.class_code = class_code
	dat.sec_code = sec_code
    msg.data = dat
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при отключении от сервера QUIK.
function OnDisconnected()
    local msg = {}
    msg.cmd = "OnDisconnected"
    msg.t = timemsec()
    msg.data = ""
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при установлении связи с сервером QUIK.
function OnConnected()
    local msg = {}
    msg.cmd = "OnConnected"
    msg.t = timemsec()
    msg.data = ""
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при получении изменений текущей позиции по счету.
function OnAccountBalance(acc_bal)
    local msg = {}
    msg.t = timemsec()
    msg.data = acc_bal
    msg.cmd = "OnAccountBalance"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при изменении денежной позиции по счету.
function OnAccountPosition(acc_pos)
    local msg = {}
    msg.t = timemsec()
    msg.data = acc_pos
    msg.cmd = "OnAccountPosition"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при получении изменений лимита по бумагам.
function OnDepoLimit(dlimit)
    local msg = {}
    msg.t = timemsec()
    msg.data = dlimit
    msg.cmd = "OnDepoLimit"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при удалении клиентского лимита по бумагам.
function OnDepoLimitDelete(dlimit_del)
    local msg = {}
    msg.t = timemsec()
    msg.data = dlimit_del
    msg.cmd = "OnDepoLimitDelete"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при получении описания новой фирмы от сервера.
function OnFirm(firm)
    local msg = {}
    msg.t = timemsec()
    msg.data = firm
    msg.cmd = "OnFirm"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при изменении позиции по срочному рынку.
function OnFuturesClientHolding(fut_pos)
    local msg = {}
    msg.t = timemsec()
    msg.data = fut_pos
    msg.cmd = "OnFuturesClientHolding"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при получении изменений ограничений по срочному рынку.
function OnFuturesLimitChange(fut_limit)
    local msg = {}
    msg.t = timemsec()
    msg.data = fut_limit
    msg.cmd = "OnFuturesLimitChange"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при удалении лимита по срочному рынку.
function OnFuturesLimitDelete(lim_del)
    local msg = {}
    msg.t = timemsec()
    msg.data = lim_del
    msg.cmd = "OnFuturesLimitDelete"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при получении изменений по денежному лимиту клиента.
function OnMoneyLimit(mlimit)
    local msg = {}
    msg.t = timemsec()
    msg.data = mlimit
    msg.cmd = "OnMoneyLimit"
    sendCallback(msg)
end

--- Функция вызывается терминалом QUIK при удалении денежного лимита.
function OnMoneyLimitDelete(mlimit_del)
    local msg = {}
    msg.t = timemsec()
    msg.data = mlimit_del
    msg.cmd = "OnMoneyLimitDelete"
    sendCallback(msg)
end

return qscallbacks