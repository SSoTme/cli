# run the build script for creating the Windows msi installer
powershell -ExecutionPolicy Bypass -File Windows/Installer/Scripts/build.ps1

# copy the newly generated MSI(s) into the Release folder
powershell -Command "Copy-Item -Path '.\Windows\Installer\bin\main\Release\*.exe' -Destination '.\release\' -Force"
