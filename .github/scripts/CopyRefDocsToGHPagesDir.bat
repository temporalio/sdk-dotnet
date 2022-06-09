@SET DEPLDOC_GHPAGES_DIR=%CD%\sdk-dotnet\docs
@SET DEPLDOC_DOCFX_WORK_ROOT=%CD%\_build\DocFx
@SET DEPLDOC_DOCFX_SITE_DIR=%DEPLDOC_DOCFX_WORK_ROOT%\dotnet.temporal.io

@ECHO\
@ECHO  ** Deleting current contents of the GH Pages directory, except "CNAME".
@ECHO  ** DEPLDOC_GHPAGES_DIR="%DEPLDOC_GHPAGES_DIR%"
@ECHO\

@REM Delete files directly in %DEPLDOC_GHPAGES_DIR%, except "CNAME".

@FOR %%i IN ("%DEPLDOC_GHPAGES_DIR%\*") DO (
    @IF "%%~nxi" EQU "CNAME" (
        @ECHO Skiping "%%i" during deletion.
    ) ELSE (
        @ECHO Deleting "%%i".
        DEL /F /Q "%%i"
    )
)

@REM Delete subdirectories of %DEPLDOC_GHPAGES_DIR%.

@FOR /D %%i IN ("%DEPLDOC_GHPAGES_DIR%\*") DO (
    RMDIR /S /Q "%%i"
)

@ECHO\
@ECHO  ** Copying static web site to the GH Pages directory.
@ECHO\

XCOPY "%DEPLDOC_DOCFX_SITE_DIR%" "%DEPLDOC_GHPAGES_DIR%" /E /C /I /Q /R /K /Y
