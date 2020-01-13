--~ // Licensed under the Apache License, Version 2.0. See LICENSE.txt in the project root for license information.

-- is running from Quik
function is_quik()
    if getScriptPath then return true else return false end
end
script_path = "."
if is_quik() then
    script_path = getScriptPath()

	-- Получаем текущюю версию Quik
	local qver = getInfoParam("VERSION")

	-- Если запрос выполнен удачно, - выделим номер версии
	if qver ~= nil then
		qver = tonumber(qver:match("%d+"))
	end

	-- Если преобразование выполнено корректно, - определяем папку хранения библиотек
	if qver == nil then
		message("QuikSharp! Не удалось определить версию QUIK", 3)
		return
	else
		libPath = "\\clibs"
	end

	-- Если версия Quik 8 и выше, добавляем к наименованию папки 64, иначе оставляем существующий путь
	if qver >= 8 then
		libPath = libPath .. "64\\"
	else
		libPath = "\\clibs\\"
	end

	-- Если версия Quik 7 будет загружена существующая WIN32 библиотека 
	-- Если версия Quik 8 будет загружена WIN64 библиотека, - которую нужно положить в папку clibs64  
	package.loadlib(getScriptPath()  .. libPath .. "lua51.dll", "main")
	--package.loadlib(getScriptPath()  .. "\\clibs\\lua51.dll", "main")
end
package.path = package.path .. ";" .. script_path .. "\\?.lua;" .. script_path .. "\\?.luac"..";"..".\\?.lua;"..".\\?.luac"
--package.cpath = package.cpath .. ";" .. script_path .. '\\clibs\\?.dll'..";"..'.\\clibs\\?.dll'
package.cpath = package.cpath .. ";" .. script_path .. libPath .. '?.dll'..";".. '.' .. libPath .. '?.dll'

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

