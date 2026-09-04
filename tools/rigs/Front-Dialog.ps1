<#
.SYNOPSIS
    Brings a rig's file dialog to the front, so the next keystroke lands in it.

.DESCRIPTION
    The Settings pane is WPF and lives inside the app's own window, but Export and Import open a
    Win32 common dialog - a separate top-level window of class #32770. Foregrounding the app's MAIN
    window steals focus back from that dialog, and an Enter meant for the dialog then goes nowhere.
    Front the dialog itself.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][int]$ProcessId)
$ErrorActionPreference = 'Stop'
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public class Dlg {
  delegate bool Cb(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(Cb cb, IntPtr l);
  [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public static IntPtr Find(int target) {
    IntPtr hit = IntPtr.Zero;
    EnumWindows((h, l) => {
      int p; GetWindowThreadProcessId(h, out p);
      if (p == target && IsWindowVisible(h)) {
        var sb = new StringBuilder(256); GetClassName(h, sb, 256);
        if (sb.ToString() == "#32770") { hit = h; return false; }
      }
      return true; }, IntPtr.Zero);
    return hit;
  }
}
"@
$h = [IntPtr]::Zero
for ($i = 0; $i -lt 40 -and $h -eq [IntPtr]::Zero; $i++) {
    $h = [Dlg]::Find($ProcessId)
    if ($h -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 250 }
}
if ($h -eq [IntPtr]::Zero) { throw "no file dialog open in pid $ProcessId" }
[void][Dlg]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 700
Write-Host "fronted dialog $($h.ToInt64()) in pid $ProcessId"
