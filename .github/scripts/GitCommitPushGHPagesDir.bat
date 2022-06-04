@SET DEPLDOC_SDKREPO_ROOTDIR=%CD%\sdk-dotnet
@SET DEPLDOC_GHPAGES_DIR_RELPATH=docs
@SET DEPLDOC_ORIGIN=%CD%

@ECHO\
@ECHO ** Changing Dir to the .NET SDK repo root
@ECHO\

CD "%DEPLDOC_SDKREPO_ROOTDIR%"

@ECHO\
@ECHO ** Configuring git for the commit and push
@ECHO\

git config user.name GHActionsScript-GitCommitPushGHPagesDir
git config user.email GitCommitPushGHPagesDir@GHActionsScript.com

@ECHO\
@ECHO ** Executing git commit and push
@ECHO\

git add %DEPLDOC_GHPAGES_DIR_RELPATH%/*
git commit -m "Deploy API Reference Docs website to GH Pages."
git push

@ECHO\
@ECHO ** Changing Dir to where this script was initially started
@ECHO\

CD "%DEPLDOC_ORIGIN%"
