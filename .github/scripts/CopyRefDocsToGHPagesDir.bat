@SET DEPLDOC_GHPAGES_DIR=%CD%\sdk-dotnet\docs
@SET DEPLDOC_DOCFX_WORK_ROOT=%CD%\_build\DocFx
@SET DEPLDOC_DOCFX_SITE_DIR=%DEPLDOC_DOCFX_WORK_ROOT%\go.temporal.io\sdk-dotnet

@ECHO\
@ECHO  ** Deleting current contents of the GH Pages directory
@ECHO\

DEL /F /Q /S "%DEPLDOC_GHPAGES_DIR%"

@ECHO\
@ECHO  ** Copy static web site to the GH Pages directory
@ECHO\

XCOPY "%DEPLDOC_DOCFX_SITE_DIR%" "%DEPLDOC_GHPAGES_DIR%" /E /C /I /Q /R /K /Y
