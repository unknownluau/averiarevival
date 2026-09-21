@echo off
:loop
echo "Starting Catalog Render RCC"
RCCService.exe -console -verbose -port 4621
echo Restarting this RCC, Control+C to cancel restart!
timeout 10
echo (%time%) Restarting RCC!
goto loop