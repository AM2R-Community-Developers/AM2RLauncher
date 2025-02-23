:: dotnet publish AM2RLauncher.Wpf -c release -r win-x64 -o "builds\win64"
:: ROBOCOPY "builds\win64" "builds\win64\lib\ " /XF *.exe *.config *.manifest /XD lib logs data /E /IS /MOVE
dotnet publish AM2RLauncher.Wpf -c release -r win-x86 -o "builds\win86"
ROBOCOPY "builds\win86" "builds\win86\lib\ " /XF *.exe *.config *.manifest /XD lib logs data /E /IS /MOVE
move .\builds\win86\AM2RLauncher.Wpf.exe .\builds\win86\AM2RLauncher.exe
move .\builds\win86\AM2RLauncher.Wpf.exe.config .\builds\win86\AM2RLauncher.exe.config
dotnet publish AM2RLauncher.Gtk -p:PublishSingleFile=true -p:DebugType=embedded -c release -r linux-x64 --no-self-contained -o "builds\linux64"
dotnet publish AM2RLauncher.Gtk -p:PublishSingleFile=true -p:DebugType=embedded -c release -r linux-x64 --self-contained -o "builds\linux64-selfContained"
