@echo off

if "%PROCESSOR_ARCHITECTURE%"=="AMD64" (
	C:\Windows\System32\regsvr32.exe /s KuShellExtension64.dll
) else if "%PROCESSOR_ARCHITEW6432%"=="AMD64" (
	C:\Windows\System32\regsvr32.exe /s KuShellExtension64.dll
)

C:\Windows\System32\regsvr32.exe /s KuShellExtension.dll
exit
