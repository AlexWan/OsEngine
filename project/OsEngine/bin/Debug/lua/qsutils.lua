--~ // Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

local socket = require ("socket")
local json = require ("dkjson")
local qsutils = {}

--- Sleep that always works
function delay(msec)
    if sleep then
        pcall(sleep, msec)
    else
        -- pcall(socket.select, nil, nil, msec / 1000)
    end
end

-- high precision current time
function timemsec()
    local st, res = pcall(socket.gettime)
    if st then
        return (res) * 1000
    else
        log("unexpected error in timemsec", 3)
        error("unexpected error in timemsec")
    end
end

-- when true will show QUIK message for log(...,0)
is_debug = false

-- log files

function openLog()
    os.execute("mkdir \""..script_path.."\\logs\"")
    local lf = io.open (script_path.. "\\logs\\QUIK#_"..os.date("%Y%m%d")..".log", "a")
    if not lf then
        lf = io.open (script_path.. "\\QUIK#_"..os.date("%Y%m%d")..".log", "a")
    end
    return lf
end

-- closes log
function closeLog()
    if logfile then
        pcall(logfile:close(logfile))
    end
end

logfile = openLog()

--- Write to log file and to Quik messages
function log(msg, level)
    if not msg then msg = "" end
    if level == 1 or level == 2 or level == 3 or is_debug then
        -- only warnings and recoverable errors to Quik
        if message then
            pcall(message, msg, level)
        end
    end
    if not level then level = 0 end
    local logLine = "LOG "..level..": "..msg
    print(logLine)
    local msecs = math.floor(math.fmod(timemsec(), 1000));
    if logfile then
        pcall(logfile.write, logfile, os.date("%Y-%m-%d %H:%M:%S").."."..msecs.." "..logLine.."\n")
        pcall(logfile.flush, logfile)
    end
end


function split(inputstr, sep)
    if sep == nil then
        sep = "%s"
    end
    local t={}
    local i=1
    for str in string.gmatch(inputstr, "([^"..sep.."]+)") do
        t[i] = str
        i = i + 1
    end
    return t
end

function from_json(str)
    local status, msg= pcall(json.decode, str, 1, json.null) -- dkjson
    if status then
        return msg
    else
        return nil, msg
    end
end

function to_json(msg)
    local status, str= pcall(json.encode, msg, { indent = false }) -- dkjson
    if status then
        return str
    else
        error(str)
    end
end

-- current connection state
is_connected = false
local port = 34130
local callback_port = port + 1
-- we need two ports since callbacks and responses conflict and write to the same socket at the same time
-- I do not know how to make locking in Lua, it is just simpler to have two independent connections
-- To connect to a remote terminal - replace 'localhost' with the terminal ip-address
local response_server = socket.bind('127.0.0.1', port, 1)
local callback_server = socket.bind('127.0.0.1', callback_port, 1)
local response_client
local callback_client

--- accept client on server
local function getResponseServer()
    log('Waiting for a response client...', 0)
	if not response_server then
		log("Cannot bind to response_server, probably the port is already in use", 3)
	else
		while true do
			local status, client, err = pcall(response_server.accept, response_server )
			if status and client then
				return client
			else
				log(err, 3)
			end
		end
	end
end

local function getCallbackClient()
    log('Waiting for a callback client...', 0)
	if not callback_server then
		log("Cannot bind to callback_server, probably the port is already in use", 3)
	else
		while true do
			local status, client, err = pcall(callback_server.accept, callback_server)
			if status and client then
				return client
			else
				log(err, 3)
			end
		end
	end
end

function qsutils.connect()
    if not is_connected then
        log('QUIK# is waiting for client connection...', 1)
        if response_client then
            log("is_connected is false but the response client is not nil", 3)
            -- Quik crashes without pcall
            pcall(response_client.close, response_client)
        end
        if callback_client then
            log("is_connected is false but the callback client is not nil", 3)
            -- Quik crashes without pcall
            pcall(callback_client.close, callback_client)
        end
        response_client = getResponseServer()
        callback_client = getCallbackClient()
        if response_client and callback_client then
            is_connected = true
            log('QUIK# client connected', 1)
        end
    end
end

local function disconnected()
    is_connected = false
    log('QUIK# client disconnected', 1)
    if response_client then
        pcall(response_client.close, response_client)
        response_client = nil
    end
    if callback_client then
        pcall(callback_client.close, callback_client)
        callback_client = nil
    end
    OnQuikSharpDisconnected()
end

--- get a decoded message as a table
function receiveRequest()
    if not is_connected then
        return nil, "not conencted"
    end
    local status, requestString= pcall(response_client.receive, response_client)
    if status and requestString then
        local msg_table, err = from_json(requestString)
        if err then
            log(err, 3)
            return nil, err
        else
            return msg_table
        end
    else
        disconnected()
        return nil, err
    end
end

function sendResponse(msg_table)
    -- if not set explicitly then set CreatedTime "t" property here
    -- if not msg_table.t then msg_table.t = timemsec() end
    local responseString = to_json(msg_table)
    if is_connected then
        local status, res = pcall(response_client.send, response_client, responseString..'\n')
        if status and res then
            return true
        else
            disconnected()
            return nil, err
        end
    end
end

function sendCallback(msg_table)
    -- if not set explicitly then set CreatedTime "t" property here
    -- if not msg_table.t then msg_table.t = timemsec() end
    local callback_string = to_json(msg_table)
    if is_connected then
        local status, res = pcall(callback_client.send, callback_client, callback_string..'\n')
        if status and res then
            return true
        else
            disconnected()
            return nil, err
        end
    end
end

return qsutils
