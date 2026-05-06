dotnet publish MapBoard.MAUI -f net10.0-android -r android-arm64 -c Release -p:AndroidKeyStore=true 
Invoke-Item MapBoard.MAUI\bin\Release\net10.0-android\android-arm64\publish
pause