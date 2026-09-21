@echo STARTING SITE
start redis-server.exe
start cmd /c "cd /d frontend && call run.bat"
start cmd /c "cd /d AssetValidationServiceV2 && call run.bat"
start cmd /c "cd /d Roblox && call dev.bat"
timeout /t 20 >nul
@echo STARTING RCC
start cmd /c "cd /d game-server && call start.bat"
start cmd /c "cd /d RCCService && call run.bat"