SET PK_ZIPFILE_NAME=Go.Temporal.IO_Sdk-DotNet.zip

SET PK_ORIGIN=%CD%

CD %~dp0

7z a -tzip -mx9 %PK_ZIPFILE_NAME% go.temporal.io\sdk-dotnet
7z a -tzip -mx9 %PK_ZIPFILE_NAME% ApiReference.ReadMe.md
7z a -tzip -mx9 %PK_ZIPFILE_NAME% HostDocs.bat

CD %PK_ORIGIN%

MOVE /Y "%~dp0%PK_ZIPFILE_NAME%" .