<#
.SYNOPSIS
  Applies the version-controlled SQL scripts to a Sage Admin database (idempotent, forward-only).

.DESCRIPTION
  - DDL/ and SeedData/ scripts are tracked in dbo.SchemaVersions (name + SHA-256). A script runs once;
    if an already-applied script was edited the deploy stops (add a new numbered ALTER script instead).
  - Views/, Functions/, StoredProcedures/ use CREATE OR ALTER and are re-applied on every run.
  - Optionally creates the first Super Admin. Credentials come from environment variables, never from files:
      SAGE_SUPERADMIN_EMAIL     (required when no Super Admin exists)
      SAGE_SUPERADMIN_PASSWORD  (required when no Super Admin exists, min 10 chars)
      SAGE_SUPERADMIN_USERNAME  (optional, default 'superadmin')
  - Windows authentication by default. For SQL authentication pass -SqlUser and set $env:SQLCMDPASSWORD first.
  - Production requires -ConfirmProduction.

.EXAMPLE
  $env:SAGE_SUPERADMIN_EMAIL = 'admin@example.com'; $env:SAGE_SUPERADMIN_PASSWORD = '<choose one>'
  ./Deploy-Database.ps1 -Server 'localhost\SQLEXPRESS' -Database SageAdmin_Dev
#>
param(
    [Parameter(Mandatory)][string]$Server,
    [Parameter(Mandatory)][string]$Database,
    [ValidateSet('Development', 'Staging', 'Production')][string]$Environment = 'Development',
    [string]$SqlUser,
    [switch]$ConfirmProduction,
    [switch]$SkipSuperAdmin
)

$ErrorActionPreference = 'Stop'
if ($Environment -eq 'Production' -and -not $ConfirmProduction) {
    throw 'Refusing to deploy to Production without -ConfirmProduction.'
}

$root = Split-Path -Parent $PSScriptRoot   # backend/database
$auth = if ($SqlUser) { @('-U', $SqlUser) } else { @('-E') }
$common = @('-S', $Server, '-d', $Database) + $auth + @('-C', '-b', '-I')

function Invoke-SqlFile([string]$Path) {
    & sqlcmd @common -i $Path | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Script failed: $Path" }
}

function Invoke-SqlScalar([string]$Query) {
    $out = & sqlcmd @common -h -1 -W -Q "SET NOCOUNT ON; $Query"
    if ($LASTEXITCODE -ne 0) { throw "Query failed: $Query" }
    return ($out | Where-Object { $_ -and $_.Trim() } | Select-Object -First 1)
}

function Get-Checksum([string]$Path) { (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLower() }

Write-Host "Deploying to [$Server] / [$Database] ($Environment)"

# Bootstrap tracking table (001 is idempotent).
Invoke-SqlFile (Join-Path $root 'DDL\001_Create_SchemaVersions.sql')

function Invoke-Tracked([string]$Folder) {
    $dir = Join-Path $root $Folder
    foreach ($file in Get-ChildItem -Path $dir -Filter '*.sql' | Sort-Object Name) {
        $name = "$Folder/$($file.Name)"
        $sum = Get-Checksum $file.FullName
        $existing = Invoke-SqlScalar "SELECT Checksum FROM dbo.SchemaVersions WHERE ScriptName = N'$name'"
        if ($existing) {
            if ($existing.Trim() -ne $sum) { throw "Applied script '$name' was modified. Create a new ALTER script instead." }
            Write-Host "  skip   $name"
            continue
        }
        Write-Host "  apply  $name"
        Invoke-SqlFile $file.FullName
        $null = Invoke-SqlScalar "INSERT dbo.SchemaVersions (ScriptName, Checksum) VALUES (N'$name', '$sum')"
    }
}

function Invoke-Repeatable([string]$Folder) {
    $dir = Join-Path $root $Folder
    foreach ($file in Get-ChildItem -Path $dir -Filter '*.sql' -ErrorAction SilentlyContinue | Sort-Object Name) {
        Write-Host "  refresh $Folder/$($file.Name)"
        Invoke-SqlFile $file.FullName
    }
}

Invoke-Tracked 'DDL'
Invoke-Repeatable 'Views'
Invoke-Repeatable 'Functions'
Invoke-Repeatable 'StoredProcedures'
Invoke-Tracked 'SeedData'

if (-not $SkipSuperAdmin) {
    $has = Invoke-SqlScalar 'SELECT COUNT(*) FROM dbo.AdminUsers WHERE IsSuperAdmin = 1 AND IsDeleted = 0'
    if ([int]$has -eq 0) {
        $email = $env:SAGE_SUPERADMIN_EMAIL
        $password = $env:SAGE_SUPERADMIN_PASSWORD
        $username = if ($env:SAGE_SUPERADMIN_USERNAME) { $env:SAGE_SUPERADMIN_USERNAME } else { 'superadmin' }
        if (-not $email -or -not $password) { throw 'No Super Admin exists. Set SAGE_SUPERADMIN_EMAIL and SAGE_SUPERADMIN_PASSWORD, then re-run.' }
        if ($password.Length -lt 10) { throw 'SAGE_SUPERADMIN_PASSWORD must be at least 10 characters.' }
        if ($username -notmatch '^[A-Za-z0-9._-]{3,50}$') { throw 'Invalid SAGE_SUPERADMIN_USERNAME.' }
        if ($email -notmatch '^[^@\s'']+@[^@\s'']+\.[^@\s'']+$') { throw 'Invalid SAGE_SUPERADMIN_EMAIL.' }

        # Same format as SageCommonLib.Security.PasswordHasher: v1.{iterations}.{saltB64}.{hashB64}
        $iterations = 600000
        $salt = New-Object byte[] 16
        [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
        $kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($password, $salt, $iterations, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
        $hash = "v1.$iterations.$([Convert]::ToBase64String($salt)).$([Convert]::ToBase64String($kdf.GetBytes(32)))"

        $q = "INSERT dbo.AdminUsers (FirstName, LastName, Username, Email, PasswordHash, MustChangePassword, IsSuperAdmin, Status) " +
             "VALUES (N'Super', N'Admin', N'$username', N'$email', N'$hash', 1, 1, 'Active')"
        $null = Invoke-SqlScalar $q
        Write-Host "  created Super Admin '$username' (must change password at first login)"
    }
    else { Write-Host '  Super Admin already exists' }
}

Write-Host 'Done.'
