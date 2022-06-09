@ECHO OFF
ECHO\
ECHO ----
ECHO   *** HostDocs ***
ECHO\
ECHO   Usage:
ECHO       HostDocs.bat
ECHO\
ECHO   This script downloads DocFx (if it is not already present) and starts its localhost function.
ECHO   The API Referece Docs will be accessible under http://localhost:8080/
ECHO   Of course, if you prefer, you can use any other means of hosting the docs site in this archive.
ECHO ----

ECHO\

REM Constants used later: 

SET DOCFX_RELEASE_URL=https://github.com/dotnet/docfx/releases/download/v2.59.2/docfx.zip
SET DOCFX_BINEXE_DIR=DocFx.Bin
SET DOCFX_SITE_DIR_PATH=dotnet.temporal.io

REM Switch to the script's folder.

CD "%~dp0"

ECHO Change Dir to "%~dp0"
ECHO\

REM Ensure DocFx-BinExe-Dir exists. 

CALL :EnsureDirExists %DOCFX_BINEXE_DIR%,DocFx-BinExe-Dir,RC

IF %RC% NEQ 0 (
    ECHO Giving up.
    @ECHO ON
    @exit /b 1
)

ECHO\

REM Ensure DocFx-Bin-Exe exists.

SET DOCFX_BIN_EXE=%DOCFX_BINEXE_DIR%\docfx.exe
SET DOCFX_RELEASE_ZIP_PATH=%DOCFX_BINEXE_DIR%\docfx.zip

IF exist "%DOCFX_BIN_EXE%" (
    ECHO 'DocFx-Bin-Exe' exists ["%DOCFX_BIN_EXE%"].
) ELSE (
    ECHO 'DocFx-Bin-Exe' does not exist ["%DOCFX_BIN_EXE%"].
    ECHO Must download and install DocFx.

    ECHO\
    ECHO Downloading...
    ECHO   - Source: "%DOCFX_RELEASE_URL%"
    ECHO   - Destination: "%DOCFX_RELEASE_ZIP_PATH%"    
    ECHO\

    :: curl --url "%DOCFX_RELEASE_URL%" --output "%DOCFX_RELEASE_ZIP_PATH%" --verbose --location
    curl --url "%DOCFX_RELEASE_URL%" --output "%DOCFX_RELEASE_ZIP_PATH%" --location

    ECHO\
    ECHO Downloaded file [note the downloaded file size; a very small file may indicate 'NotFound']:
    ECHO -----------

    dir "%DOCFX_RELEASE_ZIP_PATH%"

    ECHO -----------
    ECHO\

    ECHO Unpacking...
    ECHO -----------

    :: tar -xvf "%DOCFX_RELEASE_ZIP_PATH%"
    tar -xf "%DOCFX_RELEASE_ZIP_PATH%" -C "%DOCFX_BINEXE_DIR%"

    ECHO -----------
    ECHO ...finished unpacking.
    ECHO\

    IF exist "%DOCFX_BIN_EXE%" (
        ECHO 'DocFx-Bin-Exe' now exists ["%DOCFX_BIN_EXE%"].
    ) ELSE (
        ECHO Could not install 'DocFx-Bin-Exe' ["%DOCFX_BIN_EXE%"].
        ECHO Giving up.
        @ECHO ON
        @exit /b 1
    )
    
    ECHO Deleting downloaded archive.
    DEL /F "%DOCFX_RELEASE_ZIP_PATH%"
    ECHO\
)

REM Now we can start DocFx.

ECHO\
ECHO Will start DocFx in 'serve' mode.
ECHO\

SET DOCFX_INVOC_CMD="%DOCFX_BIN_EXE%" serve .\%DOCFX_SITE_DIR_PATH%

ECHO\
ECHO DocFx invocation command:
ECHO %DOCFX_INVOC_CMD%
ECHO\

ECHO Opening browser. You need to refresh after 2-3 seconds.
ECHO\

start http://localhost:8080/

ECHO\
ECHO Invoking DocFx.
ECHO -----------

%DOCFX_INVOC_CMD%
SET DOCFX_RESULTCODE=%ERRORLEVEL%

ECHO -----------
ECHO DocFx finished. ResultCode="%DOCFX_RC%"
ECHO\

ECHO ----
ECHO   *** GenerateApiRefDocs ***
ECHO   Finished. DOCFX_RESULTCODE="%DOCFX_RESULTCODE%". Good bye.
ECHO ----
ECHO\

@ECHO ON
@exit /b %DOCFX_RESULTCODE%


:: ----------- FUNCTION DEFINITIONS ----------- 

:: EnsureDirExists (DirPath, DirMoniker, ResultCode)
:EnsureDirExists 

rem ECHO :EnsureDirExists("%~1", "%~2", "%~3") {

IF exist "%~1"\ (    
    ECHO '%~2' exists ["%~1"].
    SET %~3=0
) ELSE (    
    ECHO '%~2' does not exist. Creating.
    mkdir "%~1"

    IF exist "%~1"\ (
        ECHO Created. '%~2' now exists ["%~1"].
        SET %~3=0
    ) ELSE (
        ECHO Cannot create '%~2' ["%~1"].
        SET $~3=1
    )
)

rem ECHO }

exit /b 0