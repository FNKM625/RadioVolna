@echo off
setlocal enabledelayedexpansion
title Radio Volna - Generator APK

REM --- KONFIGURACJA ---
set "PROJECT_FILE=RadioVolna.csproj"
set "VERSION_FILE=version.json"
set "BASE_NAME=RadioVolna"
set "FRAMEWORK=net8.0-android"
set "OUTPUT_FOLDER=apk"

echo ==========================================
echo      GENERATOR APK - RADIO VOLNA
echo ==========================================
echo.

REM 1. Pobieranie numerow wersji z pliku .csproj
echo [INFO] Szukam numeru wersji w pliku %PROJECT_FILE%...
set "APP_DISPLAY_VERSION=Unknown"
set "APP_BUILD_VERSION=Unknown"

for /f "tokens=*" %%i in ('powershell -command "$xml=[xml](Get-Content '%PROJECT_FILE%'); foreach($pg in $xml.Project.PropertyGroup) { if($pg.Condition -match 'Release\|net8\.0-android') { Write-Output $pg.ApplicationDisplayVersion } }"') do set APP_DISPLAY_VERSION=%%i

for /f "tokens=*" %%i in ('powershell -command "$xml=[xml](Get-Content '%PROJECT_FILE%'); foreach($pg in $xml.Project.PropertyGroup) { if($pg.Condition -match 'Release\|net8\.0-android') { Write-Output $pg.ApplicationVersion } }"') do set APP_BUILD_VERSION=%%i

if "%APP_DISPLAY_VERSION%"=="Unknown" (
    echo [OSTRZEZENIE] Nie udalo sie odczytac wersji. Ustawiam V1.
    set "APP_DISPLAY_VERSION=1"
    set "APP_BUILD_VERSION=1"
) else (
    echo [INFO] Wykryto wersje: %APP_DISPLAY_VERSION% ^(Build: %APP_BUILD_VERSION%^)
)

REM 2. Aktualizacja pliku version.json
echo [INFO] Aktualizuje plik %VERSION_FILE%...
powershell -command "$file='%VERSION_FILE%'; if(Test-Path $file) { $json=Get-Content $file -Raw | ConvertFrom-Json; $json.latestVersion='%APP_DISPLAY_VERSION%'; $json.latestBuild='%APP_BUILD_VERSION%'; $json | ConvertTo-Json | Set-Content $file -Encoding UTF8; Write-Output '       [SUKCES] Zapisano nowe wersje do JSON.' } else { Write-Output '       [OSTRZEZENIE] Brak pliku %VERSION_FILE% - pomijam aktualizacje.' }"

set "DEFAULT_NAME=%BASE_NAME%_V%APP_DISPLAY_VERSION%.apk"

REM 3. Pytanie o nazwe pliku
echo.
set /p "USER_NAME=Podaj nazwe pliku (Wcisnij ENTER aby uzyc '%DEFAULT_NAME%'): "

if "%USER_NAME%"=="" set "USER_NAME=%DEFAULT_NAME%"
if /i not "%USER_NAME:~-4%"==".apk" set "USER_NAME=%USER_NAME%.apk"

echo.
echo [INFO] Wybrano nazwe: %USER_NAME%
echo [INFO] Folder docelowy: \%OUTPUT_FOLDER%\
echo [INFO] Rozpoczynam budowanie...
echo.

REM 4. Komenda budowania
dotnet publish "%PROJECT_FILE%" -f %FRAMEWORK% -c Release -p:AndroidPackageFormat=apk

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

REM 5. Przenoszenie pliku
set "SOURCE_DIR=bin\Release\%FRAMEWORK%\publish"

for %%F in ("%SOURCE_DIR%\*-Signed.apk") do (
    echo [INFO] Znaleziono podpisana wersje: %%~nxF
    copy /Y "%%F" ".\%OUTPUT_FOLDER%\%USER_NAME%" >nul
    if !ERRORLEVEL! EQU 0 (
        echo [SUKCES] Plik gotowy: \%OUTPUT_FOLDER%\%USER_NAME%
    )
)

REM --- AUTOMATYCZNE WGRYWANIE NA GOOGLE DRIVE ---
set "DRIVE_FILE_ID=1rV71ArRDhjOIr_YMqvDDZx8TSAqs7iEt"

echo.
echo [INFO] Rozpoczynam wysylanie nowej wersji na Dysk Google...

gdrive.exe files update %DRIVE_FILE_ID% ".\%OUTPUT_FOLDER%\%USER_NAME%"

if %ERRORLEVEL% EQU 0 (
    echo [SUKCES] Plik zostal podmieniony na Dysku Google jako nowa wersja.
    echo [INFO] Stary link nadal dziala i pobiera najnowszy plik!
) else (
    echo [BLAD] Cos poszlo nie tak z wysylaniem na Dysk. 
    echo [INFO] Sprawdz czy gdrive.exe jest w folderze i czy masz internet.
)

pause