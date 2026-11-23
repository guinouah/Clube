$ErrorActionPreference = 'Stop'
$securePassword = Read-Host 'Senha (minimo 12 caracteres)' -AsSecureString
$payload = @{
    setupKey = 'clubes-local-demo-only'
    cpf = Read-Host 'CPF do superadmin'
    nome = Read-Host 'Nome'
    email = Read-Host 'E-mail'
    senha = [Net.NetworkCredential]::new('', $securePassword).Password
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri 'http://localhost:5140/api/auth/bootstrap' -ContentType 'application/json' -Body $payload
$securePassword.Dispose()
