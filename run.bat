@echo off
echo Starting server: http://localhost:5080
echo To stop: close this window or press Ctrl+C
start "" http://localhost:5080
dotnet run
pause
