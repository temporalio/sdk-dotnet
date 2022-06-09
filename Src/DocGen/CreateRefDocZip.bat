SET PK_ZIPFILE_NAME=DotNet.Temporal.IO.zip

SET PK_ORIGIN=%CD%

CD %~dp0

7z a -tzip -mx9 %PK_ZIPFILE_NAME% dotnet.temporal.io
7z a -tzip -mx9 %PK_ZIPFILE_NAME% ApiReference.ReadMe.md
7z a -tzip -mx9 %PK_ZIPFILE_NAME% HostDocs.bat

CD %PK_ORIGIN%

MOVE /Y "%~dp0%PK_ZIPFILE_NAME%" .