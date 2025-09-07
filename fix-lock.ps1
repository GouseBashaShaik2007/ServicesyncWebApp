# Kill ServicesyncWebApp.exe if running
Get-Process ServicesyncWebApp -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.Id -Force }

# Kill any process using port 5000
$pid = (Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue).OwningProcess
if ($pid) { Stop-Process -Id $pid -Force }

Write-Host "✅ Cleared ServicesyncWebApp.exe and port 5000. You can now run: dotnet clean; dotnet build; dotnet run"
