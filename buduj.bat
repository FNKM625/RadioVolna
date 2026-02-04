@echo off
setlocal enabledelayedexpansion
title Radio Volna - Generator APK

:: --- KONFIGURACJA ---
set "DEFAULT_NAME=RadioVolna.apk"
set "FRAMEWORK=net8.0-android"
set "OUTPUT_FOLDER=apk"
:: --------------------

echo ==========================================
echo      GENERATOR APK - RADIO VOLNA
echo ==========================================
echo.

:: 1. Pytanie o nazwę pliku
set /p "USER_NAME=Podaj nazwe pliku (Wcisnij ENTER aby uzyc '%DEFAULT_NAME%'): "
if "%USER_NAME%"=="" set "USER_NAME=%DEFAULT_NAME%"
if /i not "%USER_NAME:~-4%"==".apk" set "USER_NAME=%USER_NAME%.apk"

echo.
echo [INFO] Wybrano nazwe: %USER_NAME%
echo [INFO] Folder docelowy: \%OUTPUT_FOLDER%\
echo [INFO] Rozpoczynam budowanie...
echo.

:: 2. Komenda budowania
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

:: 3. Przenoszenie TYLKO pliku SIGNED
set "SOURCE_DIR=bin\Release\%FRAMEWORK%\publish"
set "FOUND=0"

:: ZMIANA TUTAJ: Szukamy *-Signed.apk
for %%F in ("%SOURCE_DIR%\*-Signed.apk") do (
    echo [INFO] Znaleziono podpisana wersje: %%~nxF
    copy /Y "%%F" ".\%OUTPUT_FOLDER%\%USER_NAME%" >nul
    if !ERRORLEVEL! EQU 0 (
        set "FOUND=1"
        echo [SUKCES] Plik gotowy: \%OUTPUT_FOLDER%\%USER_NAME%
    )
)

if %FOUND% EQU 0 (
    :: Zabezpieczenie: jeśli nie ma Signed, spróbuj znaleźć cokolwiek (dla pewności)
    echo [INFO] Nie znaleziono wersji -Signed, szukam zwyklej...
    for %%F in ("%SOURCE_DIR%\*.apk") do (
        copy /Y "%%F" ".\%OUTPUT_FOLDER%\%USER_NAME%" >nul
        set "FOUND=1"
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