#requires -version 5.1

$ErrorActionPreference = 'Stop'

# Always run in Windows PowerShell 5.1 (WPF is most reliable there)
$WindowsPowerShellExe = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
if ($PSVersionTable.PSEdition -eq 'Core') {
    Start-Process -FilePath $WindowsPowerShellExe -ArgumentList @(
        '-NoProfile'
        '-ExecutionPolicy', 'Bypass'
        '-STA'
        '-File', $PSCommandPath
    ) | Out-Null
    exit
}

# Relaunch in STA if needed for WPF
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    Start-Process -FilePath $WindowsPowerShellExe -ArgumentList @(
        '-NoProfile'
        '-ExecutionPolicy', 'Bypass'
        '-STA'
        '-File', $PSCommandPath
    ) | Out-Null
    exit
}

try {
    $LogDir = Join-Path $env:LOCALAPPDATA 'ToolTopBar'
    $LogPath = Join-Path $LogDir 'topbar.log'
    if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }
    Add-Content -Path $LogPath -Encoding UTF8 -Value ("[{0}] START {1}" -f (Get-Date).ToString('s'), $PSCommandPath)

    Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
    Add-Type -AssemblyName System.Windows.Forms

# Native interop for window styles
if (-not ('Win32.NativeMethods' -as [type])) {
Add-Type -Namespace Win32 -Name NativeMethods -MemberDefinition @"
public const int GWL_EXSTYLE = -20;
public const int WS_EX_TOOLWINDOW = 0x00000080;
public const int WS_EX_APPWINDOW = 0x00040000;

[System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
public static extern System.IntPtr GetWindowLongPtr(System.IntPtr hWnd, int nIndex);

[System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
public static extern System.IntPtr SetWindowLongPtr(System.IntPtr hWnd, int nIndex, System.IntPtr dwNewLong);
"@
}

# Settings load/save
$SettingsDir = Join-Path $env:APPDATA 'ToolTopBar'
$SettingsPath = Join-Path $SettingsDir 'settings.json'
$Settings = [ordered]@{ BarHeight = 36.0; FontSize = 14.0; BarEdge = 'Top' }

if (Test-Path $SettingsPath) {
    try {
        $json = Get-Content $SettingsPath -Raw
        $obj = $json | ConvertFrom-Json
        if ($obj.BarHeight) { $Settings.BarHeight = [double]$obj.BarHeight }
        if ($obj.FontSize) { $Settings.FontSize = [double]$obj.FontSize }
        if ($obj.BarEdge) { $Settings.BarEdge = $obj.BarEdge }
    } catch {}
}

function Save-Settings {
    try {
        if (-not (Test-Path $SettingsDir)) { New-Item -ItemType Directory -Path $SettingsDir | Out-Null }
        $Settings | ConvertTo-Json | Set-Content -Path $SettingsPath -Encoding UTF8
    } catch {}
}

# XAML UI
$Xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="ToolTopBar"
        WindowStyle="None"
        ResizeMode="NoResize"
        Topmost="True"
        ShowInTaskbar="False"
    AllowsTransparency="False"
    Background="Transparent"
        FontFamily="Segoe UI Variable, Segoe UI"
        Focusable="False">
    <Border Padding="8,0" Background="#CC202020">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <!-- Centered title spanning both columns -->
                 <TextBlock x:Name="TitleText"
                      Grid.ColumnSpan="2"
                      Text="Hola mundo"
                      HorizontalAlignment="Center"
                       VerticalAlignment="Center"
                       Foreground="White"
                       TextOptions.TextRenderingMode="ClearType"
                       TextOptions.TextFormattingMode="Ideal"/>

            <!-- Gear button on right -->
            <Button x:Name="GearBtn"
                    Grid.Column="1"
                    Width="28" Height="28"
                    Margin="6,0,0,0"
                    VerticalAlignment="Center"
                    Background="Transparent"
                    BorderBrush="Transparent"
                    Foreground="#CCFFFFFF"
                    FontFamily="Segoe MDL2 Assets"
                    Content="&#xE713;"
                    ToolTip="Configuración"/>

            <!-- Settings popup -->
            <Popup x:Name="SettingsPopup"
                   PlacementTarget="{Binding ElementName=GearBtn}"
                   Placement="Bottom"
                   StaysOpen="False"
                   AllowsTransparency="True">
                <Border Background="#F2121212" CornerRadius="6" Padding="12">
                    <StackPanel Orientation="Vertical" MinWidth="240">
                        <TextBlock Text="Configuración" FontWeight="SemiBold" Foreground="White"/>
                        <Separator Margin="0,8,0,8"/>
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="8"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="12"/>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="12"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="Auto"/>
                                <ColumnDefinition Width="8"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="Alto (px):" Foreground="#CCFFFFFF" VerticalAlignment="Center"/>
                            <TextBox x:Name="BarHeightBox" Grid.Column="2" MinWidth="120"/>
                            <TextBlock Grid.Row="2" Text="Fuente (pt):" Foreground="#CCFFFFFF" VerticalAlignment="Center"/>
                            <TextBox x:Name="FontSizeBox" Grid.Row="2" Grid.Column="2" MinWidth="120"/>
                            <TextBlock Grid.Row="4" Text="Posición:" Foreground="#CCFFFFFF" VerticalAlignment="Center"/>
                            <ComboBox x:Name="BarEdgeCombo" Grid.Row="4" Grid.Column="2" MinWidth="120">
                                <ComboBoxItem Tag="Left">Izquierda</ComboBoxItem>
                                <ComboBoxItem Tag="Top">Arriba</ComboBoxItem>
                                <ComboBoxItem Tag="Right">Derecha</ComboBoxItem>
                            </ComboBox>
                            <StackPanel Grid.Row="6" Grid.ColumnSpan="3" Orientation="Horizontal" HorizontalAlignment="Right">
                                <Button Content="Guardar" x:Name="PopupSaveBtn" Margin="0,0,8,0"/>
                                <Button Content="Salir" x:Name="PopupExitBtn"/>
                            </StackPanel>
                        </Grid>
                    </StackPanel>
                </Border>
            </Popup>
        </Grid>
    </Border>
</Window>
"@

# Create one window per display so the bar is duplicated on all monitors
$screens = [System.Windows.Forms.Screen]::AllScreens
$windows = @()
$primaryHwnd = [IntPtr]::Zero
Add-Content -Path $LogPath -Encoding UTF8 -Value ("[{0}] Creating windows for {1} screens" -f (Get-Date).ToString('s'), $screens.Length)
for ($i = 0; $i -lt $screens.Length; $i++) {
    $screen = $screens[$i]
    $reader = New-Object System.Xml.XmlNodeReader ([xml]$Xaml)
    $w = [Windows.Markup.XamlReader]::Load($reader)

    $TitleText = $w.FindName('TitleText')
    $GearBtn = $w.FindName('GearBtn')
    $SettingsPopup = $w.FindName('SettingsPopup')
    $BarHeightBox = $w.FindName('BarHeightBox')
    $FontSizeBox = $w.FindName('FontSizeBox')
    $BarEdgeCombo = $w.FindName('BarEdgeCombo')
    $PopupSaveBtn = $w.FindName('PopupSaveBtn')
    $PopupExitBtn = $w.FindName('PopupExitBtn')

    # Position and size for this screen
    $bounds = $screen.Bounds
    $w.Left = $bounds.Left
    $w.Top = $bounds.Top
    $w.Width = $bounds.Width
    $w.Height = [double]$Settings.BarHeight
    $TitleText.FontSize = [double]$Settings.FontSize

    # Hide from Alt+Tab
    $interop = New-Object System.Windows.Interop.WindowInteropHelper $w
    $null = $interop.EnsureHandle()
    $h = $interop.Handle
    $exStyle = [Win32.NativeMethods]::GetWindowLongPtr($h, [Win32.NativeMethods]::GWL_EXSTYLE)
    $ex = $exStyle.ToInt64()
    $ex = ($ex -bor [Win32.NativeMethods]::WS_EX_TOOLWINDOW) -band (-1 -bxor [Win32.NativeMethods]::WS_EX_APPWINDOW)
    [Win32.NativeMethods]::SetWindowLongPtr($h, [Win32.NativeMethods]::GWL_EXSTYLE, [IntPtr]$ex) | Out-Null

    # Mark primary handle; DWM effects will be applied later
    if ($screen.Primary) {
        $primaryHwnd = $h
        Add-Content -Path $LogPath -Encoding UTF8 -Value ("[{0}] Primary window handle assigned" -f (Get-Date).ToString('s'))
    }

    # Wire basic events for this window (gear popup, save/exit)
    $localSettings = $Settings
    $GearBtn.Add_Click({ $SettingsPopup.IsOpen = $true })
    $PopupSaveBtn.Add_Click({
        try {
            $hVal = [double]::Parse($BarHeightBox.Text, [Globalization.CultureInfo]::InvariantCulture)
            $fVal = [double]::Parse($FontSizeBox.Text, [Globalization.CultureInfo]::InvariantCulture)
            if ($hVal -lt 16) { $hVal = 16 }
            if ($hVal -gt 200) { $hVal = 200 }
            if ($fVal -lt 8)  { $fVal = 8 }
            if ($fVal -gt 72) { $fVal = 72 }
            $Settings.BarHeight = $hVal
            $Settings.FontSize = $fVal
            $sel = ($BarEdgeCombo.SelectedItem -as [System.Windows.Controls.ComboBoxItem]).Tag
            if ($sel) { $Settings.BarEdge = $sel }
            Save-Settings
            foreach ($x in $windows) { $x.Dispatcher.Invoke({ param($s) $s.FindName('TitleText').FontSize = [double]$Settings.FontSize } , $x) }
            # AppBar adjustments skipped in duplicate-mode (windows already repositioned)
            Add-Content -Path $LogPath -Encoding UTF8 -Value ("[{0}] Popup save applied; windows updated" -f (Get-Date).ToString('s'))
            $SettingsPopup.IsOpen = $false
        } catch {
            [System.Windows.MessageBox]::Show('Valores inválidos.', 'Error', [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error) | Out-Null
        }
    })
    $PopupExitBtn.Add_Click({ foreach ($x in $windows) { try { $x.Close() } catch {} } })

    $windows += $w
}

# Register AppBar on primary window handle and set initial size
Add-Content -Path $LogPath -Encoding UTF8 -Value ("[{0}] AppBar registration skipped in duplicate-mode" -f (Get-Date).ToString('s'))

# Show all windows and keep process alive; when any closes, shutdown the dispatcher
foreach ($w in $windows) {
    $w.add_Closed({
        try { foreach ($x in $windows) { if ($x -ne $w) { $x.Close() } } } catch {}
        try { if ($primaryHwnd -ne [IntPtr]::Zero) { Unregister-AppBar -Handle $primaryHwnd } } catch {}
        [System.Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvokeShutdown([System.Windows.Threading.DispatcherPriority]::Normal) | Out-Null
    })
    $w.Show()
}

# Run the WPF dispatcher to keep the script alive until windows are closed
[System.Windows.Threading.Dispatcher]::Run()

# DWM interop: Mica (Windows 11) and fallback to Acrylic via SetWindowCompositionAttribute
if (-not ('Dwm.DwmApi' -as [type])) {
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

namespace Dwm {
    public static class DwmApi {
        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    }
}

namespace Comp {
    public enum WindowCompositionAttribute {
        WCA_ACCENT_POLICY = 19
    }
    public enum AccentState {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct AccentPolicy {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowCompositionAttributeData {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }
    public static class User32 {
        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    }
}
"@
}

# Try enabling Mica; if not available, fall back to Acrylic
function Enable-MicaOrAcrylic {
    param([IntPtr]$Handle)
    try {
        $DWMWA_SYSTEMBACKDROP_TYPE = 38
        $DWMSBT_MICA = 3
        $val = $DWMSBT_MICA
        $res = [Dwm.DwmApi]::DwmSetWindowAttribute($Handle, $DWMWA_SYSTEMBACKDROP_TYPE, [ref]$val, 4)
        if ($res -eq 0) { return }
    } catch {}

    # Acrylic fallback
    $r = 32; $g = 32; $b = 32; $a = 0xD0
    $gradient = (($a -band 0xFF) -shl 24) -bor (($b -band 0xFF) -shl 16) -bor (($g -band 0xFF) -shl 8) -bor ($r -band 0xFF)
    $accent = New-Object Comp.AccentPolicy
    $accent.AccentState = [Comp.AccentState]::ACCENT_ENABLE_ACRYLICBLURBEHIND
    $accent.AccentFlags = 2
    $accent.GradientColor = [int]$gradient
    $accent.AnimationId = 0
    $size = [Runtime.InteropServices.Marshal]::SizeOf($accent)
    $ptr = [Runtime.InteropServices.Marshal]::AllocHGlobal($size)
    [Runtime.InteropServices.Marshal]::StructureToPtr($accent, $ptr, $false)
    try {
        $data = New-Object Comp.WindowCompositionAttributeData
        $data.Attribute = [Comp.WindowCompositionAttribute]::WCA_ACCENT_POLICY
        $data.Data = $ptr
        $data.SizeOfData = $size
        [void][Comp.User32]::SetWindowCompositionAttribute($Handle, [ref]$data)
    } finally {
        [Runtime.InteropServices.Marshal]::FreeHGlobal($ptr)
    }
}

Add-Content -Path $LogPath -Encoding UTF8 -Value ("[{0}] DWM definitions loaded (effects will be applied to primary window handle later)" -f (Get-Date).ToString('s'))

# AppBar interop to reserve screen space
if (-not ('Shell.AppBar' -as [type])) {
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

namespace Shell {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    public static class AppBarConsts {
        public const int ABM_NEW = 0x00000000;
        public const int ABM_REMOVE = 0x00000001;
        public const int ABM_QUERYPOS = 0x00000002;
        public const int ABM_SETPOS = 0x00000003;
        public const int ABE_LEFT = 0;
        public const int ABE_TOP = 1;
        public const int ABE_RIGHT = 2;
    }

    public static class AppBar {
        [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
    }
}
"@
}

function Register-AppBar {
    param([System.IntPtr]$Handle)
    $abd = New-Object Shell.APPBARDATA
    $abd.cbSize = [System.Runtime.InteropServices.Marshal]::SizeOf($abd)
    $abd.hWnd = $Handle
    [void][Shell.AppBar]::SHAppBarMessage([Shell.AppBarConsts]::ABM_NEW, [ref]$abd)
}

function Unregister-AppBar {
    param([System.IntPtr]$Handle)
    $abd = New-Object Shell.APPBARDATA
    $abd.cbSize = [System.Runtime.InteropServices.Marshal]::SizeOf($abd)
    $abd.hWnd = $Handle
    [void][Shell.AppBar]::SHAppBarMessage([Shell.AppBarConsts]::ABM_REMOVE, [ref]$abd)
}

function Set-AppBarHeight {
    param([double]$Thickness, [string]$Edge = 'Top')
    $screenW = [int][System.Windows.SystemParameters]::PrimaryScreenWidth
    $screenH = [int][System.Windows.SystemParameters]::PrimaryScreenHeight
    $abd = New-Object Shell.APPBARDATA
    $abd.cbSize = [System.Runtime.InteropServices.Marshal]::SizeOf($abd)
    $abd.hWnd = $hwnd
    switch ($Edge) {
        'Left'  { $abd.uEdge = [Shell.AppBarConsts]::ABE_LEFT }
        'Right' { $abd.uEdge = [Shell.AppBarConsts]::ABE_RIGHT }
        default { $abd.uEdge = [Shell.AppBarConsts]::ABE_TOP }
    }

    # Ask system for an appropriate position first
    [void][Shell.AppBar]::SHAppBarMessage([Shell.AppBarConsts]::ABM_QUERYPOS, [ref]$abd)

    if ($abd.uEdge -eq [Shell.AppBarConsts]::ABE_TOP) {
        $abd.rc.left = 0
        $abd.rc.top = 0
        $abd.rc.right = $screenW
        $abd.rc.bottom = [int]$Thickness
    }
    elseif ($abd.uEdge -eq [Shell.AppBarConsts]::ABE_LEFT) {
        $abd.rc.left = 0
        $abd.rc.top = 0
        $abd.rc.right = [int]$Thickness
        $abd.rc.bottom = $screenH
    }
    else {
        $abd.rc.left = $screenW - [int]$Thickness
        $abd.rc.top = 0
        $abd.rc.right = $screenW
        $abd.rc.bottom = $screenH
    }

    [void][Shell.AppBar]::SHAppBarMessage([Shell.AppBarConsts]::ABM_SETPOS, [ref]$abd)

    $window.Left = $abd.rc.left
    $window.Top = $abd.rc.top
    $window.Width = $abd.rc.right - $abd.rc.left
    $window.Height = $abd.rc.bottom - $abd.rc.top
}

# AppBar registration skipped when duplicating windows; primary window positioning already applied per-screen
Add-Content -Path $LogPath -Encoding UTF8 -Value ("[{0}] Skipping AppBar registration in duplicate-mode; windows positioned per-screen bounds" -f (Get-Date).ToString('s'))

# Initialize popup defaults
$BarHeightBox.Text = [string][double]$Settings.BarHeight
$FontSizeBox.Text = [string][double]$Settings.FontSize
# Initialize BarEdge combo selection
try {
    $edge = $Settings.BarEdge
    switch ($edge) {
        'Left'  { $BarEdgeCombo.SelectedIndex = 0 }
        'Top'   { $BarEdgeCombo.SelectedIndex = 1 }
        'Right' { $BarEdgeCombo.SelectedIndex = 2 }
        default { $BarEdgeCombo.SelectedIndex = 1 }
    }
} catch {}

# Open settings
$GearBtn.Add_Click({ $SettingsPopup.IsOpen = $true })

# Save in popup
$PopupSaveBtn.Add_Click({
    try {
        $h = [double]::Parse($BarHeightBox.Text, [Globalization.CultureInfo]::InvariantCulture)
        $f = [double]::Parse($FontSizeBox.Text, [Globalization.CultureInfo]::InvariantCulture)
        if ($h -lt 16) { $h = 16 }
        if ($h -gt 200) { $h = 200 }
        if ($f -lt 8)  { $f = 8 }
        if ($f -gt 72) { $f = 72 }
        $Settings.BarHeight = $h
        $Settings.FontSize = $f
        # Read BarEdge from combo
        $sel = ($BarEdgeCombo.SelectedItem -as [System.Windows.Controls.ComboBoxItem]).Tag
        if ($sel) { $Settings.BarEdge = $sel }
        Save-Settings
        $TitleText.FontSize = [double]$Settings.FontSize
        Set-AppBarHeight -Thickness $Settings.BarHeight -Edge $Settings.BarEdge
        # Adjust title and gear alignment based on edge
        if ($Settings.BarEdge -eq 'Top') {
            $TitleText.LayoutTransform = $null
            $TitleText.HorizontalAlignment = 'Center'
            $TitleText.VerticalAlignment = 'Center'
            $TitleText.Margin = [System.Windows.Thickness]::new(0,0,0,0)
            $GearBtn.HorizontalAlignment = 'Right'
            $GearBtn.VerticalAlignment = 'Center'
            $GearBtn.Margin = [System.Windows.Thickness]::new(6,0,0,0)
        }
        elseif ($Settings.BarEdge -eq 'Left') {
            $TitleText.LayoutTransform = New-Object System.Windows.Media.RotateTransform(90)
            $TitleText.HorizontalAlignment = 'Left'
            $TitleText.VerticalAlignment = 'Center'
            $TitleText.Margin = [System.Windows.Thickness]::new(2,0,0,0)
            $GearBtn.HorizontalAlignment = 'Center'
            $GearBtn.VerticalAlignment = 'Bottom'
            $GearBtn.Margin = [System.Windows.Thickness]::new(0,0,0,6)
        }
        else {
            # Right
            $TitleText.LayoutTransform = New-Object System.Windows.Media.RotateTransform(-90)
            $TitleText.HorizontalAlignment = 'Right'
            $TitleText.VerticalAlignment = 'Center'
            $TitleText.Margin = [System.Windows.Thickness]::new(0,0,2,0)
            $GearBtn.HorizontalAlignment = 'Center'
            $GearBtn.VerticalAlignment = 'Bottom'
            $GearBtn.Margin = [System.Windows.Thickness]::new(0,0,0,6)
        }

        $SettingsPopup.IsOpen = $false
    } catch {
        [System.Windows.MessageBox]::Show('Valores inválidos.', 'Error', [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error) | Out-Null
    }
})

# Exit from popup
$PopupExitBtn.Add_Click({ $window.Close() })

# Handle display change to keep width
# React to display changes to keep width
$displayChangedSub = Register-ObjectEvent -InputObject ([Microsoft.Win32.SystemEvents]) -EventName DisplaySettingsChanged -Action {
    $window.Dispatcher.Invoke({ Set-AppBarHeight -Thickness $Settings.BarHeight -Edge $Settings.BarEdge })
}

# Ensure cleanup when window closes
$window.add_Closed({
    try { Unregister-AppBar -Handle $hwnd } catch {}
    if ($displayChangedSub) {
        Unregister-Event -SourceIdentifier $displayChangedSub.Name -ErrorAction SilentlyContinue
    }
})

$null = $window.ShowDialog()
Add-Content -Path $LogPath -Encoding UTF8 -Value ("[{0}] Window closed" -f (Get-Date).ToString('s'))

} catch {
    $details = $_ | Out-String
    try {
        $LogDir = Join-Path $env:LOCALAPPDATA 'ToolTopBar'
        $LogPath = Join-Path $LogDir 'topbar.log'
        if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }
        Add-Content -Path $LogPath -Encoding UTF8 -Value ("[{0}] ERROR\n{1}" -f (Get-Date).ToString('s'), $details)
    } catch {}
    try {
        Add-Type -AssemblyName PresentationFramework -ErrorAction SilentlyContinue
        [System.Windows.MessageBox]::Show($details, 'ToolTopBar - Error', [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Error) | Out-Null
    } catch {
        Write-Error $details
    }
    throw
}
