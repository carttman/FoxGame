# Ask Codex CLI to draw a game image in the FoxGame art style (see the image section of AGENTS.md).
# Usage:
#   powershell -ExecutionPolicy Bypass -File Art/codex-draw.ps1 -Name title_bg -Prompt "title screen background ..."
#   options: -Ref FoxGame/Captures/step4_jump.png (reference image), -Size "1:1" (aspect), -Model <model>
# Result: Art/<Name>.png (or <Name>_2.png ... if it already exists)
# (ASCII only: Windows PowerShell 5.1 misreads UTF-8 scripts without BOM. The prompt itself may be Korean.)
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][string]$Prompt,
    [string]$Ref = "FoxGame/Captures/step5_start.png",
    [string]$Size = "16:9",
    [string]$Model = ""
)

$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$codex = Get-Command codex.cmd -ErrorAction SilentlyContinue
if (-not $codex) {
    Write-Error "codex.cmd not found. Install: npm install -g @openai/codex (Node.js required), then 'codex.cmd login'."
    exit 1
}

# Pick a file name that does not overwrite an existing image
$out = "Art/$Name.png"
$n = 2
while (Test-Path (Join-Path $repo $out)) { $out = "Art/${Name}_$n.png"; $n++ }

$full = @"
Generate an image with your image generation tool and save it as $out in this repository.
Follow the art style guide and file rules in the image-making section of AGENTS.md. Do not modify any other files.
Aspect ratio: $Size.
Request: $Prompt
When done, reply with the saved path, the pixel size, and one sentence describing the image.
"@

$codexArgs = @("exec", "-C", $repo, "-s", "workspace-write")
if ($Ref -and (Test-Path (Join-Path $repo $Ref))) { $codexArgs += @("-i", (Join-Path $repo $Ref)) }
if ($Model) { $codexArgs += @("-m", $Model) }
# The prompt goes through stdin ("-"), not as an argument:
#  - "-i" takes several files, so a trailing prompt argument would be swallowed as an image path
#  - multi-line / Korean text is not safe through the codex.cmd batch shim
$codexArgs += "-"
$OutputEncoding = New-Object System.Text.UTF8Encoding $false

Write-Host "Codex is drawing -> $out (reference: $Ref)"
$full | & $codex.Source @codexArgs
if (Test-Path (Join-Path $repo $out)) { Write-Host "Saved: $out" } else { Write-Warning "Codex finished but $out was not created." ; exit 1 }
