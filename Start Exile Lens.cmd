@echo off
cd /d "%~dp0"
dotnet build src\ExileLens\ExileLens.csproj -c Release --nologo
if errorlevel 1 (
  echo Build failed. Exile Lens requires the .NET 8 SDK or a compatible newer SDK.
  pause
  exit /b 1
)
start "" "src\ExileLens\bin\Release\net8.0-windows\ExileLens.exe"
