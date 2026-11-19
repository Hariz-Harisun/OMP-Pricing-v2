@echo off
rem Copy this file to RunLT2026100007.bat and fill in your credentials
rem Get credentials from: https://developer.digikey.com/
set Providers__DigiKey__ClientId=YOUR_CLIENT_ID
set Providers__DigiKey__ClientSecret=YOUR_CLIENT_SECRET
set Providers__DigiKey__AccountId=YOUR_ACCOUNT_ID
set Providers__DigiKey__Enabled=true

echo Processing all sheets in LT2026100007.xlsx ...

echo.
echo ==== Denali G2_DR8 (10k) ====
OmpPricing.App.exe --file "C:\Users\hariz\Desktop\LT2026100007.xlsx" --sheet "Denali G2_DR8 (10k)"

echo.
echo ==== Denali G2_DR8 (50k) ====
OmpPricing.App.exe --file "C:\Users\hariz\Desktop\LT2026100007.xlsx" --sheet "Denali G2_DR8 (50k)"

echo.
echo ==== Denali G2_2FR4 (10k) ====
OmpPricing.App.exe --file "C:\Users\hariz\Desktop\LT2026100007.xlsx" --sheet "Denali G2_2FR4 (10k)"

echo.
echo ==== Denali G2_2FR4 (50k) ====
OmpPricing.App.exe --file "C:\Users\hariz\Desktop\LT2026100007.xlsx" --sheet "Denali G2_2FR4 (50k)"

echo.
echo Done. Output files in %CD%\output\
pause