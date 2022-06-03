@ECHO OFF
ECHO\
ECHO ----
ECHO   *** GenerateApiRefDocs ***
ECHO\
ECHO   Usage:
ECHO       GenerateApiRefDocs.bat "DocFx-WorkRoot-Dir" [DocFx-BinExe-Dir] [LogLevel]
ECHO\
ECHO   This script downloads DocFx (if it is not already present) and generates the docs.
ECHO   All the required files must be copied in place before running this script
ECHO   That is done by the build system. This script is also invoked from the build system.
ECHO ----

ECHO\

REM Constants used later: 

SET DOCFX_RELEASE_URL=https://github.com/dotnet/docfx/releases/download/v2.59.2/docfx.zip

SET DOCFX_INVOC1_TARGET=net462
SET DOCFX_INVOC2_TARGET=netcoreapp3.1
SET DOCFX_INVOC3_TARGET=net6.0

REM Read command line parameters.

IF "%1"=="" (
    ECHO Must specify the `DocFx-WorkRoot-Dir` as the first command line parameter.
    ECHO Nothing was specified.
    @ECHO ON
    @exit /b 1
) ELSE (
    ECHO `DocFx-WorkRoot-Dir` was specified on the command line.
    SET DOCFX_WORKROOT_DIR=%1
)

IF "%2"=="" (    
    SET DOCFX_BINEXE_DIR=%DOCFX_WORKROOT_DIR%\DocFx.Bin
    ECHO `DocFx-BinExe-Dir` not specified. Using default.
) ELSE (
    SET DOCFX_BINEXE_DIR=%2
    ECHO `DocFx-BinExe-Dir` was specified on the command line.
)

IF "%3"=="" (    
    SET DOCFX_LOG_LEVEL=Info
    ECHO `LogLevel` not specified. Using default.
) ELSE (
    SET DOCFX_LOG_LEVEL=%3
    ECHO `LogLevel` was specified on the command line.
)

ECHO\
ECHO `DocFx-WorkRoot-Dir`: "%DOCFX_WORKROOT_DIR%"
ECHO `DocFx-BinExe-Dir`:   "%DOCFX_BINEXE_DIR%"
ECHO `LogLevel`:           "%DOCFX_LOG_LEVEL%"
ECHO\

REM Ensure DocFx-WorkRoot-Dir exists.

CALL :EnsureDirExists %DOCFX_WORKROOT_DIR%,DocFx-WorkRoot-Dir,RC

IF %RC% NEQ 0 (
    ECHO Giving up.
    @ECHO ON
    @exit /b 1
)

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
)

REM Now we can start invoking DocFx.

ECHO\
ECHO Will invoke DocFx for each target.
ECHO\

SET DOCFX_SUMMARY_RC=0

IF "%DOCFX_INVOC1_TARGET%" NEQ "" (
    ECHO Starting to process DOCFX_INVOC1_TARGET="%DOCFX_INVOC1_TARGET%".
    CALL :InvokeDocFx %DOCFX_INVOC1_TARGET%,RC
    ECHO Finished processing DOCFX_INVOC1_TARGET="%DOCFX_INVOC1_TARGET%".
) ELSE (
    ECHO DOCFX_INVOC1_TARGET is not set. Will not process.
    SET RC=0
)

ECHO\
IF %DOCFX_SUMMARY_RC% EQU 0 (IF %RC% EQU 0 (SET DOCFX_SUMMARY_RC=0) ELSE (SET DOCFX_SUMMARY_RC=1))
ECHO So far, DOCFX_SUMMARY_RC="%DOCFX_SUMMARY_RC%".
ECHO\

IF "%DOCFX_INVOC2_TARGET%" NEQ "" (
    ECHO Starting to process DOCFX_INVOC2_TARGET="%DOCFX_INVOC2_TARGET%".
    CALL :InvokeDocFx %DOCFX_INVOC2_TARGET%,RC
    ECHO Finished processing DOCFX_INVOC2_TARGET="%DOCFX_INVOC2_TARGET%".
) ELSE (
    ECHO DOCFX_INVOC3_TARGET is not set. Will not process.
    SET RC=0
)

ECHO\
IF %DOCFX_SUMMARY_RC% EQU 0 (IF %RC% EQU 0 (SET DOCFX_SUMMARY_RC=0) ELSE (SET DOCFX_SUMMARY_RC=1))
ECHO So far, DOCFX_SUMMARY_RC="%DOCFX_SUMMARY_RC%".
ECHO\

IF "%DOCFX_INVOC3_TARGET%" NEQ "" (
    ECHO Starting to process DOCFX_INVOC3_TARGET="%DOCFX_INVOC3_TARGET%".
    CALL :InvokeDocFx %DOCFX_INVOC3_TARGET%,RC
    ECHO Finished processing DOCFX_INVOC3_TARGET="%DOCFX_INVOC3_TARGET%".
) ELSE (
    ECHO DOCFX_INVOC3_TARGET is not set. Will not process.
    SET RC=0
)

ECHO\
IF %DOCFX_SUMMARY_RC% EQU 0 (IF %RC% EQU 0 (SET DOCFX_SUMMARY_RC=0) ELSE (SET DOCFX_SUMMARY_RC=1))
ECHO So far, DOCFX_SUMMARY_RC="%DOCFX_SUMMARY_RC%".
ECHO\

IF "%DOCFX_INVOC4_TARGET%" NEQ "" (
    ECHO Starting to process DOCFX_INVOC4_TARGET="%DOCFX_INVOC4_TARGET%".
    CALL :InvokeDocFx %DOCFX_INVOC4_TARGET%,RC
    ECHO Finished processing DOCFX_INVOC4_TARGET="%DOCFX_INVOC4_TARGET%".
) ELSE (
    ECHO DOCFX_INVOC4_TARGET is not set. Will not process.
    SET RC=0
)

ECHO\
IF %DOCFX_SUMMARY_RC% EQU 0 (IF %RC% EQU 0 (SET DOCFX_SUMMARY_RC=0) ELSE (SET DOCFX_SUMMARY_RC=1))
ECHO So far, DOCFX_SUMMARY_RC="%DOCFX_SUMMARY_RC%".
ECHO\

ECHO ----
ECHO   *** GenerateApiRefDocs ***
ECHO   Finished. DOCFX_SUMMARY_RC="%DOCFX_SUMMARY_RC%". Good bye.
ECHO ----
ECHO\

@ECHO ON
@exit /b %DOCFX_SUMMARY_RC%


:: ----------- FUNCTION DEFINITIONS ----------- 

:: InvokeDocFx(InvocTarget, ResultCode)
:InvokeDocFx

SET DOCFX_INVOC_TARGET=%~1

ECHO\
ECHO :InvokeDocFx(%DOCFX_INVOC_TARGET%) { ----------- -----------
ECHO\

SET DOCFX_PROJECT_JSON_PATH=%DOCFX_WORKROOT_DIR%\DocFx-%DOCFX_INVOC_TARGET%.json
ECHO DOCFX_PROJECT_JSON_PATH="%DOCFX_PROJECT_JSON_PATH%"

FOR /F %%i IN ('powershell -c "get-date -format yyMMdd-HHmmss"') DO (
    SET DOCFX_TIMESTAMP=%%i
)

SET DOCFX_LOG_PATH=%DOCFX_WORKROOT_DIR%\DocFxLog_%DOCFX_TIMESTAMP%_%DOCFX_INVOC_TARGET%.log
ECHO DOCFX_LOG_PATH="%DOCFX_LOG_PATH%"

SET DOCFX_INVOC_CMD="%DOCFX_BIN_EXE%" "%DOCFX_PROJECT_JSON_PATH%" -l "%DOCFX_LOG_PATH%" --logLevel "%DOCFX_LOG_LEVEL%"

ECHO\
ECHO DocFx invocation command:
ECHO %DOCFX_INVOC_CMD%

ECHO\
ECHO Invoking DocFx.
ECHO -----------

%DOCFX_INVOC_CMD%
SET DOCFX_RC=%ERRORLEVEL%

ECHO -----------
ECHO DocFx finished. ResultCode="%DOCFX_RC%"

SET %~2=%DOCFX_RC%

ECHO\
ECHO ----------- ----------- } :InvokeDocFx(%DOCFX_INVOC_TARGET%)

exit /b 0


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