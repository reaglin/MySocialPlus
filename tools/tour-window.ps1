# Launch MySocial+ on the sample, select tree nodes through UI Automation, and photograph the
# window after each with PrintWindow (never a screen grab, never synthetic input).
#   .\tools\tour-window.ps1 -Exe src\MySocialPlus.App\bin\Debug\net10.0-windows\MySocialPlus.exe -OutDir shots
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$OutDir = "shots",
    [string[]]$Nodes = @("Bluesky", "Sarah Lee", "Colleagues", "Jim Halloran", "My Friends"),
    [int]$WaitMs = 3500
)

Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Win {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static IntPtr MainWindow(uint pid) {
        IntPtr best = IntPtr.Zero; int bestArea = 0;
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p != pid || !IsWindowVisible(h)) return true;
            RECT r; if (!GetWindowRect(h, out r)) return true;
            int area = (r.Right - r.Left) * (r.Bottom - r.Top);
            if (area > bestArea) { best = h; bestArea = area; }
            return true;
        }, IntPtr.Zero);
        return best;
    }
}
"@

function Shoot([IntPtr]$h, [string]$path) {
    $r = New-Object Win+RECT
    [Win]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [Win]::PrintWindow($h, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Output "saved $path"
}

New-Item -ItemType Directory -Force $OutDir | Out-Null
$proc = Start-Process -FilePath $Exe -ArgumentList "--sample" -PassThru
Start-Sleep -Milliseconds $WaitMs
$h = [Win]::MainWindow([uint32]$proc.Id)
if ($h -eq [IntPtr]::Zero) { Write-Output "NO WINDOW"; if (-not $proc.HasExited) { $proc.Kill() }; exit 1 }

$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$treeItem = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::TreeItem)

$i = 0
Shoot $h (Join-Path $OutDir ("{0:D2}-start.png" -f $i++))
foreach ($name in $Nodes) {
    # Names carry the badge ("Sarah Lee, 2 new"), so match the start.
    $items = $root.FindAll($TS::Descendants, $treeItem)
    $hit = $null
    foreach ($it in $items) { $n = $it.Current.Name; if ($n -eq $name -or $n.StartsWith("$name,")) { $hit = $it; break } }
    if ($null -eq $hit) { Write-Output "not found: $name"; continue }
    $sel = $hit.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $sel.Select()
    Start-Sleep -Milliseconds 1500
    $safe = ($name -replace '[^A-Za-z0-9]+', '-').ToLower()
    Shoot $h (Join-Path $OutDir ("{0:D2}-{1}.png" -f $i++, $safe))
    $after = @(); foreach ($it in $root.FindAll($TS::Descendants, $treeItem)) { $after += $it.Current.Name }
    Write-Output ("  tree now: " + ($after -join " | "))
}
if (-not $proc.HasExited) { $proc.Kill() }
