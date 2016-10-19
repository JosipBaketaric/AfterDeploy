@REM HINT: SET SECOND ARGUMENT TO /NOPAUSE WHEN AUTOMATING THE BUILD.

CALL "%VS140COMNTOOLS%VsDevCmd.bat" || ECHO ERROR: Cannot find Visual Studio 2015, missing VS140COMNTOOLS variable. && GOTO Error0
@ECHO ON

PUSHD "%~dp0" || GOTO Error0
CALL ChangeVersions.bat || GOTO Error1
IF EXIST msbuild.log DEL msbuild.log || GOTO Error1

REM NuGet Automatic Package Restore requires "NuGet.exe restore" to be executed before the command-line build.
WHERE /Q NuGet.exe || ECHO ERROR: Please download the NuGet.exe command line tool. && GOTO Error1

NuGet.exe restore Rhetos.AfterDeploy.sln -NonInteractive || GOTO Error1
MSBuild.exe "Rhetos.AfterDeploy.sln" /target:rebuild /p:Configuration=Debug /verbosity:minimal /fileLogger || GOTO Error1
NuGet.exe pack -o .. || GOTO Error1

CALL ChangeVersions.bat /RESTORE || GOTO Error1
POPD

@REM ================================================

@ECHO.
@ECHO %~nx0 SUCCESSFULLY COMPLETED.
@EXIT /B 0

:Error1
@POPD
:Error0
@ECHO.
@ECHO %~nx0 FAILED.
@IF /I [%2] NEQ [/NOPAUSE] @PAUSE
@EXIT /B 1