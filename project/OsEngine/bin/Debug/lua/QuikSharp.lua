--~ // Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

-- is running from Quik
function is_quik()
    if getScriptPath then return true else return false end
end

quikVersion = nil

script_path = "."

if is_quik() then
    script_path = getScriptPath()
    
	quikVersion = getInfoParam("VERSION")

	if quikVersion ~= nil then
		quikVersion = tonumber(quikVersion:match("%d+%.%d+"))
	end

	if quikVersion == nil then
		message("QUIK# cannot detect QUIK version", 3)
		return
	else
		libPath = "\\clibs"
	end
    
    -- MD dynamic, requires MSVCRT
    -- MT static, MSVCRT is linked statically with luasocket
    -- package.cpath contains info.exe working directory, which has MSVCRT, so MT should not be needed in theory, 
    -- but in one issue someone said it doesn't work on machines that do not have Visual Studio. 
    local linkage = "MD"
    
	if quikVersion >= 8.5 then
        libPath = libPath .. "64\\53_"..linkage.."\\"
	elseif quikVersion >= 8 then
        libPath = libPath .. "64\\5.1_"..linkage.."\\"
	else
		libPath = "\\clibs\\5.1_"..linkage.."\\"
	end
end
package.path = package.path .. ";" .. script_path .. "\\?.lua;" .. script_path .. "\\?.luac"..";"..".\\?.lua;"..".\\?.luac"
package.cpath = package.cpath .. ";" .. script_path .. libPath .. '?.dll'..";".. '.' .. libPath .. '?.dll'

local util = require("qsutils")
local qf = require("qsfunctions")
require("qscallbacks")

log("Detected Quik version: ".. quikVersion .." and using cpath: "..package.cpath  , 0)

local is_started = true

function do_main()
    log("Entered main function", 0)
    while is_started do
        -- if not connected, connect
        util.connect()
        -- when connected, process queue
        -- receive message,
        local requestMsg = receiveRequest()
        if requestMsg then
            -- if ok, process message
            -- dispatch_and_process never throws, it returns lua errors wrapped as a message
            local responseMsg, err = qf.dispatch_and_process(requestMsg)
            if responseMsg then
                -- send message
                local res = sendResponse(responseMsg)
            else
                log("Could not dispatch and process request: " .. err, 3)
            end
        else
            delay(1)
        end
    end
end

--- catch errors
function main()
    local status, err = pcall(do_main)
    if status then
        log("finished")
    else
        log(err, 3)
    end
end

if not is_quik() then
    log("Hello, QUIK#! Running outside Quik.")
    do_main()
    logfile:close()
end

