@echo off

for /f %%i in ('git rev-list HEAD --count') do set COMMIT_COUNT=%%i
for /f %%i in ('git log -1 --format^=\"%%h\"') do set COMMIT_HASH=%%i
for /f %%i in ('git status -s ^| find /C " "') do set UNCOMMITED_CHANGES=%%i
for /f %%i in ('git rev-list origin/master..HEAD --count') do set UNPUSHED_COUNT=%%i

if "%~1" == "deploy" goto :deploy
if "%~1" == "package" goto :package

:build
set MSBUILDDIR=%ProgramFiles(x86)%\MSBuild\12.0\bin
"%MSBUILDDIR%\msbuild" ProductionStackTrace.sln /t:Build /p:Configuration=Release /p:BuildPackageIfMarked=true ^
    /p:VersionBuild=%COMMIT_COUNT%;VersionBuildMeta=%COMMIT_HASH% %MSBUILDPARAMS%

exit /b

:package
if not exist "%~dp0\_deploy" mkdir "%~dp0\_deploy"
del /S /Q "%~dp0\_deploy\*.*"

set MSBUILDPARAMS=/p:WriteNuGetVersionToFile=true "/p:PackageOutputDir=%~dp0\_deploy"
call :build

exit /b

:deploy
if %UNCOMMITED_CHANGES% NEQ 0 goto :uncommited

call :package

set /p VERSION=<_deploy\.version.txt
if "%VERSION%"=="" goto :error
git tag -m "Release v%VERSION%" "v%VERSION%"
if errorlevel 1 goto :error

.nuget\NuGet.exe push _deploy\ProductionStackTrace.%VERSION%.nupkg
if errorlevel 1 goto :error
.nuget\NuGet.exe push _deploy\ProductionStackTrace.Analyze.%VERSION%.nupkg
if errorlevel 1 goto :error
.nuget\NuGet.exe push _deploy\ProductionStackTrace.Analyze.Console.%VERSION%.nupkg
if errorlevel 1 goto :error
exit /b

:uncommited
echo There are uncommited changes! Please commit first.
exit /b 1

:error
echo Something wrong, aborted
exit /b 1