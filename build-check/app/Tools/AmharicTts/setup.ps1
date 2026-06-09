param(
    [string]$Python = "py",
    [string]$PythonVersion = "-3.12"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$venv = Join-Path $root ".venv"
$modelDir = Join-Path $root "model"
$pythonExe = Join-Path $venv "Scripts\python.exe"

if (-not (Test-Path $pythonExe)) {
    & $Python $PythonVersion -m venv $venv
}

& $pythonExe -m pip install --upgrade pip
& $pythonExe -m pip install --index-url https://download.pytorch.org/whl/cpu torch
& $pythonExe -m pip install "transformers==4.41.2" "huggingface-hub==0.23.5" accelerate scipy safetensors

New-Item -ItemType Directory -Force -Path $modelDir | Out-Null
$env:HF_HOME = Join-Path $modelDir "hf-cache"

$sample = Join-Path $root "sample.wav"
& $pythonExe (Join-Path $root "synthesize_ticket.py") `
    --text "ibakwo kutir em and wede memezgebiya kotari hulet yihidu" `
    --output $sample `
    --model-dir $modelDir
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $sample)) {
    throw "Natural Amharic voice setup failed before sample audio was generated."
}

Write-Host "Natural Amharic voice is ready."
Write-Host "Sample: $sample"
