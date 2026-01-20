# Test script for MCP server
# This script sends JSON-RPC messages to the MCP server via stdio

$serverPath = Join-Path $PSScriptRoot "..\src\Apmas.Server\bin\Debug\net8.0\Apmas.Server.exe"

if (-not (Test-Path $serverPath)) {
    Write-Error "Server executable not found at $serverPath"
    exit 1
}

Write-Host "Starting MCP server..." -ForegroundColor Cyan

# Start the server process
$processInfo = New-Object System.Diagnostics.ProcessStartInfo
$processInfo.FileName = $serverPath
$processInfo.UseShellExecute = $false
$processInfo.RedirectStandardInput = $true
$processInfo.RedirectStandardOutput = $true
$processInfo.RedirectStandardError = $true
$processInfo.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $processInfo
$process.Start() | Out-Null

# Helper function to send a JSON-RPC message
function Send-McpMessage {
    param(
        [Parameter(Mandatory)]
        [string]$Json
    )

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Json)
    $headers = "Content-Length: $($bytes.Length)`r`nContent-Type: application/json`r`n`r`n"

    Write-Host "`n>>> Sending:" -ForegroundColor Yellow
    Write-Host $Json

    $process.StandardInput.Write($headers)
    $process.StandardInput.Write($Json)
    $process.StandardInput.Flush()
}

# Helper function to read a JSON-RPC response
function Read-McpResponse {
    $headers = @{}

    # Read headers
    while ($true) {
        $line = $process.StandardOutput.ReadLine()
        if ([string]::IsNullOrWhiteSpace($line)) {
            break
        }

        $colonIndex = $line.IndexOf(':')
        if ($colonIndex -gt 0) {
            $key = $line.Substring(0, $colonIndex).Trim()
            $value = $line.Substring($colonIndex + 1).Trim()
            $headers[$key] = $value
        }
    }

    # Read body
    $contentLength = [int]$headers['Content-Length']
    $buffer = New-Object char[] $contentLength
    $process.StandardOutput.Read($buffer, 0, $contentLength) | Out-Null
    $json = -join $buffer

    Write-Host "`n<<< Received:" -ForegroundColor Green
    $formatted = $json | ConvertFrom-Json | ConvertTo-Json -Depth 10
    Write-Host $formatted

    return $json | ConvertFrom-Json
}

try {
    # Give the server a moment to start
    Start-Sleep -Milliseconds 500

    # Test 1: Initialize
    Write-Host "`n=== Test 1: Initialize ===" -ForegroundColor Magenta
    $initRequest = @{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = @{
            protocolVersion = "2025-11-25"
            capabilities = @{
                roots = @{}
            }
            clientInfo = @{
                name = "test-client"
                version = "1.0.0"
            }
        }
    } | ConvertTo-Json -Compress

    Send-McpMessage -Json $initRequest
    $initResponse = Read-McpResponse

    if ($initResponse.result.serverInfo.name -eq "APMAS") {
        Write-Host "`nTest 1: PASSED" -ForegroundColor Green
    } else {
        Write-Host "`nTest 1: FAILED" -ForegroundColor Red
    }

    # Test 2: List tools
    Write-Host "`n=== Test 2: List Tools ===" -ForegroundColor Magenta
    $toolsRequest = @{
        jsonrpc = "2.0"
        id = 2
        method = "tools/list"
        params = @{}
    } | ConvertTo-Json -Compress

    Send-McpMessage -Json $toolsRequest
    $toolsResponse = Read-McpResponse

    $toolCount = $toolsResponse.result.tools.Count
    Write-Host "`nFound $toolCount tools"

    if ($toolsResponse.result.tools -is [array]) {
        Write-Host "`nTest 2: PASSED (even with 0 tools, structure is correct)" -ForegroundColor Green
    } else {
        Write-Host "`nTest 2: FAILED" -ForegroundColor Red
    }

    # Test 3: Invalid method
    Write-Host "`n=== Test 3: Invalid Method ===" -ForegroundColor Magenta
    $invalidRequest = @{
        jsonrpc = "2.0"
        id = 3
        method = "invalid/method"
        params = @{}
    } | ConvertTo-Json -Compress

    Send-McpMessage -Json $invalidRequest
    $errorResponse = Read-McpResponse

    if ($errorResponse.error.code -eq -32601) {
        Write-Host "`nTest 3: PASSED (proper error handling)" -ForegroundColor Green
    } else {
        Write-Host "`nTest 3: FAILED" -ForegroundColor Red
    }

    Write-Host "`n=== All Tests Complete ===" -ForegroundColor Cyan

} catch {
    Write-Host "`nError: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
} finally {
    # Clean up
    Write-Host "`nShutting down server..." -ForegroundColor Cyan
    if (-not $process.HasExited) {
        $process.Kill()
    }
    $process.Dispose()
}
