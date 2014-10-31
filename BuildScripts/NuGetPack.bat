@echo off
pushd ".."

set NUGET=".nuget\NuGet.exe"
set PACKAGEDIR="bin\NuGetPackages"

echo:
echo:Packing NuGet packages into %PACKAGEDIR% . . .

if exist %PACKAGEDIR% rd /s /q %PACKAGEDIR%
md %PACKAGEDIR% 

echo:
call :ParseVersion "DryIoc\Properties\AssemblyInfo.cs"
echo:DryIoc v%VER%
echo:================

%NUGET% pack "NuGet\DryIoc.nuspec" -Version %VER% -OutputDirectory %PACKAGEDIR% -NonInteractive
%NUGET% pack "NuGet\DryIoc.dll.nuspec" -Version %VER% -OutputDirectory %PACKAGEDIR% -NonInteractive 
rem -Symbols

echo:
call :ParseVersion "DryIoc.MefAttributedModel\Properties\AssemblyInfo.cs"
echo:MefAttributedModel v%VER%
echo:============================

%NUGET% pack "NuGet\DryIoc.MefAttributedModel.nuspec" -Version %VER% -OutputDirectory %PACKAGEDIR% -NonInteractive
%NUGET% pack "NuGet\DryIoc.MefAttributedModel.dll.nuspec" -Version %VER% -OutputDirectory %PACKAGEDIR% -NonInteractive
rem -Symbols 


echo:
call :ParseVersion "Extensions\DryIoc.CommonServiceLocator\Properties\AssemblyInfo.cs"
echo:DryIoc.CommonServiceLocator v%VER%
echo:============================

%NUGET% pack "NuGet\DryIoc.CommonServiceLocator.nuspec" -Version %VER% -OutputDirectory %PACKAGEDIR% -NonInteractive
%NUGET% pack "NuGet\DryIoc.CommonServiceLocator.dll.nuspec" -Version %VER% -OutputDirectory %PACKAGEDIR% -NonInteractive
rem -Symbols 

echo: 
echo:Packaging succeeded.
popd

if not "%1"=="-nopause" pause 
goto:eof

:ParseVersion
set VERFILE=%~1
for /f "usebackq tokens=2,3 delims=:() " %%A in ("%VERFILE%") do (
	if "%%A"=="AssemblyInformationalVersion" set VER=%%~B
)
exit /b