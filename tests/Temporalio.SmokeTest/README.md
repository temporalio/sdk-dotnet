## Smoke Test

This is a simple program that references `Temporalio` library and runs a simple client call. This intentionally does
not have the `Temporalio` dependency already present. Rather, it can be added from a folder with the nupkg inside via:

    dotnet add tests/Temporalio.SmokeTest package Temporalio -s "/path/to/nupkg/folder;https://api.nuget.org/v3/index.json" --prerelease