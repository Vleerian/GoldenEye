dotnet publish --runtime win-x64 --self-contained -p:Configuration=Release /p:DebugType=None /p:DebugSymbols=false
dotnet publish --runtime osx-x64 --self-contained -p:Configuration=Release /p:DebugType=None /p:DebugSymbols=false
dotnet publish --runtime linux-x64 --self-contained -p:Configuration=Release /p:DebugType=None /p:DebugSymbols=false

rm -r builds
mkdir builds

mv ./bin/Release/net6.0/win-x64/publish/GoldenEye.exe ./builds/GoldenEye-Win64.exe
mv ./bin/Release/net6.0/osx-x64/publish/GoldenEye ./builds/GoldenEye-Osx64
mv ./bin/Release/net6.0/linux-x64/publish/GoldenEye ./builds/GoldenEye-Linux64
