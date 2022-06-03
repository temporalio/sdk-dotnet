SET PK_ORIGIN=%CD%

CD %~dp0

7z a -tzip -mx9 TemporalIO.GitHub.IO_Sdk-DotNet.zip temporalio.github.io\sdk-dotnet
7z a -tzip -mx9 TemporalIO.GitHub.IO_Sdk-DotNet.zip ApiReference.ReadMe.md
7z a -tzip -mx9 TemporalIO.GitHub.IO_Sdk-DotNet.zip HostDocs.bat

CD %PK_ORIGIN%