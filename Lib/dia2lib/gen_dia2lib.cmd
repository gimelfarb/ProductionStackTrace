@echo off
set VSCOMNTOOLS=%VS120COMNTOOLS%
@call "%VSCOMNTOOLS%\VsDevCmd.bat"
@call :GetWindowsSdk35ExecutablePath32

@rem Use MIDL to compile IDL to TLB
set DIA2IDL=%VSCOMNTOOLS%\..\..\DIA SDK\idl\dia2.idl
set DIA2INC=%VSCOMNTOOLS%\..\..\DIA SDK\include
midl /tlb dia2lib.tlb /I "%DIA2INC%" "%DIA2IDL%"

@rem Cleanup after MIDL
del dia2.h dia2_i.c dia2_p.c dlldata.c

@rem Use TLBIMP to convert TLB to Assembly
tlbimp /out:net40\dia2lib.dll /namespace:Dia2Lib dia2lib.tlb
"%WindowsSDK35_ExecutablePath_x86%\tlbimp.exe" /out:net20\dia2lib.dll /namespace:Dia2Lib dia2lib.tlb

@rem Cleanup - no longer need TLB
del dia2lib.tlb

@exit /B 0


@REM -----------------------------------------------------------------------
@REM        .NET 3.5 SDK
@REM -----------------------------------------------------------------------
:GetWindowsSdk35ExecutablePath32
@set WindowsSDK35_ExecutablePath_x86=
@call :GetWindowsSdk35ExePathHelper HKLM > nul 2>&1
@if errorlevel 1 call :GetWindowsSdk35ExePathHelper HKCU > nul 2>&1
@if errorlevel 1 call :GetWindowsSdk35ExePathHelperWow6432 HKLM > nul 2>&1
@if errorlevel 1 call :GetWindowsSdk35ExePathHelperWow6432 HKCU > nul 2>&1
@exit /B 0

:GetWindowsSdk35ExePathHelper
@for /F "tokens=1,2*" %%i in ('reg query "%1\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.0A\WinSDK-NetFx35Tools" /v "InstallationFolder"') DO (
	@if "%%i"=="InstallationFolder" (
		@SET "WindowsSDK35_ExecutablePath_x86=%%k"
	)
)
@if "%WindowsSDK35_ExecutablePath_x86%"=="" exit /B 1
@exit /B 0

:GetWindowsSdk35ExePathHelperWow6432
@for /F "tokens=1,2*" %%i in ('reg query "%1\SOFTWARE\Wow6432Node\Microsoft\Microsoft SDKs\Windows\v8.0A\WinSDK-NetFx35Tools" /v "InstallationFolder"') DO (
	@if "%%i"=="InstallationFolder" (
		@SET "WindowsSDK35_ExecutablePath_x86=%%k"
	)
)
@if "%WindowsSDK35_ExecutablePath_x86%"=="" exit /B 1
@exit /B 0
