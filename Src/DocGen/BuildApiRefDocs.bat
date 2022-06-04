@ECHO\
@ECHO This will generate the static API Reference Documentation website.
@ECHO\
@ECHO By default, when building the main Temporal.Sdk solution, the static
@ECHO API Reference Documentation is NOT included, because it takes forever
@ECHO to build. This command builds the solution (for only a single target
@ECHO framework) while setting the MSBuild property `BuildApiRefDocs`, which
@ECHO ensures that the API Reference Docs are generated.
@ECHO\

dotnet build -f net6.0 -c Debug "%~dp0..\Temporal.Sdk.sln" /p:BuildApiRefDocs=True
