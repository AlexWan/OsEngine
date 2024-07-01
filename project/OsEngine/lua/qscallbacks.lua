--~ Copyright (c) 2014-2020 QUIKSharp Authors https://github.com/finsight/QUIKSharp/blob/master/AUTHORS.md. All rights reserved.
--~ Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

package.path = package.path..";"..".\\?.lua;"..".\\?.luac"

local qscallbacks = {}

local function CleanUp()
    closeLog()
end

function OnQuikSharpDisconnected()
    -- TODO any recovery or risk management logic here
end

function OnError(message)
	if is_connected then
		local msg = {}
		msg.t = timemsec()
		msg.cmd = "lua_error"
		msg.data = "Lua error: " .. message
		sendCallback(msg)
	end
end

function OnDisconnected()
    local msg = {}
    msg.cmd = "OnDisconnected"
    msg.t = timemsec()
    msg.data = ""
    sendCallback(msg)
end

function OnConnected()
    local msg = {}
    msg.cmd = "OnConnected"
    msg.t = timemsec()
    msg.data = ""
    sendCallback(msg)
end

function OnAllTrade(alltrade)
    if is_connected then
        local msg = {}
        msg.t = timemsec()
        msg.cmd = "OnAllTrade"
        msg.data = alltrade
        sendCallback(msg)
    end
end

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

function OnInit(script_path)
    if is_connected then
        local msg = {}
        msg.cmd = "OnInit"
        msg.t = timemsec()
        msg.data = script_path
        sendCallback(msg)
    end
    log("QUIK# is initialized from "..script_path, 0)
end

function OnOrder(order)
    local msg = {}
    msg.t = timemsec()
    msg.id = nil
    msg.data = order
    msg.cmd = "OnOrder"
    sendCallback(msg)
end

function OnQuote(class_code, sec_code)
    if is_connected then
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

function OnStop(s)
    is_started = false

    if is_connected then
        local msg = {}
        msg.cmd = "OnStop"
        msg.t = timemsec()
        msg.data = s
        sendCallback(msg)
    end
    log("QUIK# stopped. You could keep script running when closing QUIK and the script will start automatically the next time you start QUIK", 1)
    CleanUp()
    --	send disconnect
    return 1000
end

function OnTrade(trade)
    local msg = {}
    msg.t = timemsec()
    msg.id = nil
    msg.data = trade
    msg.cmd = "OnTrade"
    sendCallback(msg)
end

function OnTransReply(trans_reply)
    local msg = {}
    msg.t = timemsec()
    msg.id = nil
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

function OnAccountBalance(acc_bal)
    local msg = {}
    msg.t = timemsec()
    msg.data = acc_bal
    msg.cmd = "OnAccountBalance"
    sendCallback(msg)
end

function OnAccountPosition(acc_pos)
    local msg = {}
    msg.t = timemsec()
    msg.data = acc_pos
    msg.cmd = "OnAccountPosition"
    sendCallback(msg)
end

function OnDepoLimit(dlimit)
    local msg = {}
    msg.t = timemsec()
    msg.data = dlimit
    msg.cmd = "OnDepoLimit"
    sendCallback(msg)
end

function OnDepoLimitDelete(dlimit_del)
    local msg = {}
    msg.t = timemsec()
    msg.data = dlimit_del
    msg.cmd = "OnDepoLimitDelete"
    sendCallback(msg)
end

function OnFirm(firm)
    local msg = {}
    msg.t = timemsec()
    msg.data = firm
    msg.cmd = "OnFirm"
    sendCallback(msg)
end

function OnFuturesClientHolding(fut_pos)
    local msg = {}
    msg.t = timemsec()
    msg.data = fut_pos
    msg.cmd = "OnFuturesClientHolding"
    sendCallback(msg)
end

function OnFuturesLimitChange(fut_limit)
    local msg = {}
    msg.t = timemsec()
    msg.data = fut_limit
    msg.cmd = "OnFuturesLimitChange"
    sendCallback(msg)
end

function OnFuturesLimitDelete(lim_del)
    local msg = {}
    msg.t = timemsec()
    msg.data = lim_del
    msg.cmd = "OnFuturesLimitDelete"
    sendCallback(msg)
end

function OnMoneyLimit(mlimit)
    local msg = {}
    msg.t = timemsec()
    msg.data = mlimit
    msg.cmd = "OnMoneyLimit"
    sendCallback(msg)
end

function OnMoneyLimitDelete(mlimit_del)
    local msg = {}
    msg.t = timemsec()
    msg.data = mlimit_del
    msg.cmd = "OnMoneyLimitDelete"
    sendCallback(msg)
end

return qscallbacks
