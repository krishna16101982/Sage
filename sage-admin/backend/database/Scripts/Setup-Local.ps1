<#
.SYNOPSIS
  One-time local setup: creates the SageAdmin_Dev database (if missing) and applies all scripts.

.DESCRIPTION
  Run it yourself in PowerShell from backend/database/Scripts. Nothing is hardcoded:
    $env:SAGE_SUPERADMIN_EMAIL    = 'you@example.com'
    $env:SAGE_SUPERADMIN_PASSWORD = '<your own password, min 10 chars>'
    ./Setup-Local.ps1
  Default server is the local SQLEXPRESS instance with Windows authentication.
  Integration tests create and drop their own throw-away databases named SageAdmin_Test_<guid>;
  they never touch SageAdmin_Dev.
#>
param(
    [string]$Server = 'localhost\SQLEXPRESS',
    [string]$Database = 'SageAdmin_Dev'
)

$ErrorActionPreference = 'Stop'
if ($Database -notmatch '^SageAdmin_[A-Za-z0-9_]+$') { throw 'Database name must look like SageAdmin_<suffix>.' }

& sqlcmd -S $Server -E -C -b -Q "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];"
if ($LASTEXITCODE -ne 0) { throw 'Could not create the database.' }

& "$PSScriptRoot\Deploy-Database.ps1" -Server $Server -Database $Database -Environment Development

Write-Host ''
Write-Host 'Connection string for the API (store in User Secrets, not in files):'
Write-Host "  dotnet user-secrets set `"DatabaseSettings:ConnectionString`" `"Server=$Server;Database=$Database;Integrated Security=true;TrustServerCertificate=true`" --project ../../src"
