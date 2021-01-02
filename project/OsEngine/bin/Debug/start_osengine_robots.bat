:: Данный скрипт запускает OsEngine/Роботы, если терминал не запущен
:: This script is starting OsEngine/Robots if terminal is not running

tasklist /FI "IMAGENAME eq OsEngine.exe" 2>NUL | find /I /N "OsEngine.exe">NUL
if NOT "%ERRORLEVEL%" == "0" start "" ".\OsEngine.exe" "-robots"