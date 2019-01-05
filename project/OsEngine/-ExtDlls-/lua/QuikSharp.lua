--~ // Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

-- is running from Quik
function is_quik()
    if getScriptPath then return true else return false end
end
script_path = "."
if is_quik() then
    script_path = getScriptPath()
	package.loadlib(getScriptPath()  .. "\\clibs\\lua51.dll", "main")
end
package.path = package.path .. ";" .. script_path .. "\\?.lua;" .. script_path .. "\\?.luac"..";"..".\\?.lua;"..".\\?.luac"
package.cpath = package.cpath .. ";" .. script_path .. '\\clibs\\?.dll'..";"..'.\\clibs\\?.dll'

local util = require("qsutils")
local qf = require("qsfunctions")
require("qscallbacks")

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
    log("Hello, QuikSharp! Running outside Quik.")
    do_main()
    logfile:close()
end

