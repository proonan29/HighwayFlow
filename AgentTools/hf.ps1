# Helper: run a Unity CLI command against the HighwayFlow editor.
# Usage: .\AgentTools\hf.ps1 <command> [args...]
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$env:Path += ";C:\Users\proon\AppData\Local\Unity\bin"
Set-Location D:\UnityAIProjs\DigitalTwin\HighwayFlow
& unity command @args --no-banner 2>&1
