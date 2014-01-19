@echo off
set VSCOMNTOOLS=%VS120COMNTOOLS%
@call "%VSCOMNTOOLS%\VsDevCmd.bat"

@rem Use MIDL to compile IDL to TLB
set DIA2IDL=%VSCOMNTOOLS%\..\..\DIA SDK\idl\dia2.idl
set DIA2INC=%VSCOMNTOOLS%\..\..\DIA SDK\include
midl /tlb dia2lib.tlb /I "%DIA2INC%" "%DIA2IDL%"

@rem Cleanup after MIDL
del dia2.h dia2_i.c dia2_p.c dlldata.c

@rem Use TLBIMP to convert TLB to Assembly
tlbimp /out:dia2lib.dll /namespace:Dia2Lib dia2lib.tlb

@rem Cleanup - no longer need TLB
del dia2lib.tlb
