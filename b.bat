@echo off
setlocal EnableDelayedExpansion

dotnet clean -v:m
dotnet build -c:Release -v:m -p:DevMode=false;NoLegacy=true
if %ERRORLEVEL% neq 0 goto :error

echo:
echo:## Finished: RESTORE and BUILD
echo: 

echo:
echo:## Starting: TestRunner... ##
echo:

dotnet run --no-build -c Release --project test/DryIoc.TestRunner/DryIoc.TestRunner.csproj

if %ERRORLEVEL% neq 0 goto :error
echo:## Finished: TestRunner ##

echo:
echo:## Starting: TESTS...
echo: 

dotnet test --no-build -c:Release -p:DevMode=false;NoLegacy=true

if %ERRORLEVEL% neq 0 goto :error

echo: 
echo:## Finished: TESTS

echo: 
echo:## Starting: MARKDOWN DOCS GENERATION

dotnet msbuild -target:MdGenerate docs\DryIoc.Docs\DryIoc.Docs.csproj

echo:
echo:## Finished: MARKDOWN DOCS GENERATION
echo: 

echo:
echo:## Finished: ALL Successful ##
exit /b 0

:error
echo:
echo:## Build is failed :-(
exit /b 1
