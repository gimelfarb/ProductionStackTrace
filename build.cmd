@echo off
set VSCOMNTOOLS=%VS120COMNTOOLS%
@call "%VSCOMNTOOLS%\VsDevCmd.bat"

msbuild ProductionStackTrace.sln /p:Configuration=Release /t:Build
