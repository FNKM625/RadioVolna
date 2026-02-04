@echo off
setlocal enabledelayedexpansion
title Radio Volna - Generator APK

:: --- KONFIGURACJA ---
set "BASE_NAME=RadioVolna"
set "FRAMEWORK=net8.0-android"
set "OUTPUT_FOLDER=apk"
:: --------------------

echo ==========================================
echo      GENERATOR APK - RADIO VOLNA
echo ==========================================
echo.

:: 1. Pobieranie numeru wersji z pliku .csproj
echo [INFO] Szukam numeru wersji w pliku .csproj...
set "APP_VERSION=Unknown"

:: Używamy prostego polecenia PowerShell do wyciągnięcia liczby z tagu <ApplicationVersion>
for /f "tokens=*" %%i in ('powershell -command "Select-String -Path *.csproj -Pattern '<ApplicationVersion>(\d+)</ApplicationVersion>' | ForEach-Object { $_.Matches.Groups[1].Value }"') do set APP_VERSION=%%i

if "%APP_VERSION%"=="Unknown" (
    echo [OSTRZEZENIE] Nie udalo sie odczytac wersji. Ustawiam V1.
    set "APP_VERSION=1"
) else (
    echo [INFO] Wykryto wersje projektu: %APP_VERSION%
)

:: Tworzymy domyślną nazwę, np. RadioVolna_V3.apk
set "DEFAULT_NAME=%BASE_NAME%_V%APP_VERSION%.apk"

:: 2. Pytanie o nazwę pliku
echo.
set /p "USER_NAME=Podaj nazwe pliku (Wcisnij ENTER aby uzyc '%DEFAULT_NAME%'): "

:: Jeśli użytkownik nic nie wpisał, użyj domyślnej
if "%USER_NAME%"=="" set "USER_NAME=%DEFAULT_NAME%"

:: Sprawdź czy użytkownik dodał .apk na końcu, jak nie to dodaj
if /i not "%USER_NAME:~-4%"==".apk" set "USER_NAME=%USER_NAME%.apk"

echo.
echo [INFO] Wybrano nazwe: %USER_NAME%
echo [INFO] Folder docelowy: \%OUTPUT_FOLDER%\
echo [INFO] Rozpoczynam budowanie...
echo.

:: 3. Komenda budowania
dotnet publish -f %FRAMEWORK% -c Release -p:AndroidPackageFormat=apk

if %ERRORLEVEL% NEQ 0 (
    color 4
    echo.
    echo [BLAD] Wystapil blad podczas budowania!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [SUKCES] Zbudowano pomyslnie!
echo [INFO] Przygotowuje folder '%OUTPUT_FOLDER%'...

if not exist "%OUTPUT_FOLDER%" (
    mkdir "%OUTPUT_FOLDER%"
)

:: 4. Przenoszenie TYLKO pliku SIGNED
set "SOURCE_DIR=bin\Release\%FRAMEWORK%\publish"
set "FOUND=0"

for %%F in ("%SOURCE_DIR%\*-Signed.apk") do (
    echo [INFO] Znaleziono podpisana wersje: %%~nxF
    copy /Y "%%F" ".\%OUTPUT_FOLDER%\%USER_NAME%" >nul
    if !ERRORLEVEL! EQU 0 (
        set "FOUND=1"
        echo [SUKCES] Plik gotowy: \%OUTPUT_FOLDER%\%USER_NAME%
    )
)

:: Zabezpieczenie na wypadek braku pliku Signed
if %FOUND% EQU 0 (
    echo [INFO] Nie znaleziono wersji -Signed, szukam zwyklej...
    for %%F in ("%SOURCE_DIR%\*.apk") do (
        copy /Y "%%F" ".\%OUTPUT_FOLDER%\%USER_NAME%" >nul
        set "FOUND=1"
        echo [SUKCES] Plik gotowy (niepodpisany): \%OUTPUT_FOLDER%\%USER_NAME%
    )
)

if %FOUND% EQU 0 (
    color 6
    echo.
    echo [OSTRZEZENIE] Nie znaleziono zadnego pliku APK!
) else (
    color 2
    echo.
    echo ==========================================
    echo   GOTOWE! Sprawdz folder "apk".
    echo ==========================================
)

pause