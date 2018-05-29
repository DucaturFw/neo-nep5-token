dotnet publish -o ../ico-avm
@rem cd ../neo-compiler
@rem dotnet publish -o ../ico-avm
if %errorlevel% neq 0 exit /b %errorlevel%
pushd ..\ico-avm
dotnet neon.dll NeoContractIco.dll
if %errorlevel% neq 0 exit /b %errorlevel%
popd
xcopy /Y ..\ico-avm\NeoContractIco.avm .\output\