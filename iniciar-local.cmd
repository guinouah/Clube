@echo off
set "PROJECT_ROOT=%~dp0"
start "Clubes API" powershell -NoExit -ExecutionPolicy Bypass -Command "$env:Bootstrap__SetupKey='clubes-local-demo-only'; Set-Location -LiteralPath '%PROJECT_ROOT%'; dotnet run --project src/Clubes.Api --launch-profile http"
start "Clubes Web" powershell -NoExit -ExecutionPolicy Bypass -Command "$env:Bootstrap__SetupKey='clubes-local-demo-only'; Set-Location -LiteralPath '%PROJECT_ROOT%'; dotnet run --project src/Clubes.Web --launch-profile http"
echo API: http://localhost:5140
echo Painel: http://localhost:5229
echo SQLite: src\Clubes.Api\App_Data\clubes-v2.db
