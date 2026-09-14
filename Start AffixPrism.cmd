@echo off
cd /d "%~dp0"
dotnet build src\AffixPrism\AffixPrism.csproj -c Release --nologo
if errorlevel 1 (
  echo Build failed. AffixPrism requires the .NET 8 SDK or a compatible newer SDK.
  pause
  exit /b 1
)
start "" "src\AffixPrism\bin\Release\net8.0-windows\AffixPrism.exe"
