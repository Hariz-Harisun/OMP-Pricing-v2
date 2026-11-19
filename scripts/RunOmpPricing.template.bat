@echo off
rem Copy this file to RunOmpPricing.bat and fill in your credentials
rem Get credentials from: https://developer.digikey.com/
set Providers__DigiKey__ClientId=YOUR_CLIENT_ID
set Providers__DigiKey__ClientSecret=YOUR_CLIENT_SECRET
set Providers__DigiKey__AccountId=YOUR_ACCOUNT_ID
set Providers__DigiKey__Enabled=true

echo Processing LT2026100009.xlsx ...
OmpPricing.App.exe --file "..\OmpPricing\tests\samples\LT2026100009.xlsx"

echo.
echo Done. Output is in %CD%\output\
pause