@echo off
:loop
echo "Starting Player Render RCC"
RCCService.exe -console -verbose -port 1621
echo Restarting this RCC, Control+C to cancel restart!
timeout 10
echo (%time%) Restarting RCC!
goto loop