using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Drawing; // For SystemIcons
using System.ComponentModel; // For PropertyChangedEventArgs and CancelEventArgs
using System.Collections.Generic;
using System.IO; // For File operations
using System; // For EventArgs
using System.Collections.ObjectModel; // For ObservableCollection
using System.Diagnostics; // For Process
using System.Threading;
using System.Threading.Tasks;

namespace ToolTopBar
{
    public partial class MainWindow : Window
    {
        private readonly SettingsService _settingsService = new SettingsService();
        private Settings _settings = Settings.Default();
        private uint _appBarCallbackMessage;
        private HwndSource? _hwndSource;
        private IntPtr _windowHandle = IntPtr.Zero;
        // Multi-screen support
        private static readonly List<MainWindow> _extraWindows = new List<MainWindow>();
        public bool IsSecondaryWindow { get; set; } = false;
        public System.Windows.Forms.Screen? AssignedScreen { get; set; }

        // --- Drag & Drop para reordenar íconos ---
        private System.Windows.Point? _dragStartPoint;
        private int _dragSourceIndex = -1;
        private bool _isDragging = false;
        private ListBoxItem? _draggedItemContainer;
        private AdornerLayer? _adornerLayer;
        private IconGhostAdorner? _ghostAdorner;
        private const double DragThreshold = 6.0; // px

        // Fuente real de íconos (shortcuts) para el ListBox
        private readonly ObservableCollection<ShortcutIconViewModel> _shortcutItems = new ObservableCollection<ShortcutIconViewModel>();

        // DependencyProperty para IconSpacing dinámico
        public static readonly DependencyProperty IconSpacingProperty =
            DependencyProperty.Register(nameof(IconSpacing), typeof(double), typeof(MainWindow),
                new PropertyMetadata(16.0));

        public double IconSpacing
        {
            get => (double)GetValue(IconSpacingProperty);
            set => SetValue(IconSpacingProperty, value);
        }

        public MainWindow()
        {
            InitializeComponent();
            IconsListBox.ItemsSource = _shortcutItems;
            Loaded += (_, __) => ApplySettingsToUI();
            Closing += MainWindow_Closing;
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        }

        // --- Drag & Drop ListBox ---
        private void IconsListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(IconsListBox);
            _draggedItemContainer = GetListBoxItemUnderMouse(e);
            _dragSourceIndex = _draggedItemContainer != null ? IconsListBox.ItemContainerGenerator.IndexFromContainer(_draggedItemContainer) : -1;
            _isDragging = false;
        }

        private void IconsListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_dragStartPoint == null || _dragSourceIndex < 0)
                return;
            if (_dragSourceIndex >= _shortcutItems.Count)
                return;
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            var pos = e.GetPosition(IconsListBox);
            var delta = (pos - _dragStartPoint.Value);

            if (!_isDragging && (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold))
            {
                // Iniciar drag visual
                _isDragging = true;
                _draggedItemContainer = (ListBoxItem)IconsListBox.ItemContainerGenerator.ContainerFromIndex(_dragSourceIndex);
                if (_draggedItemContainer != null)
                {
                    _draggedItemContainer.Opacity = 0.4;
                    // Ghost opcional
                    _adornerLayer ??= AdornerLayer.GetAdornerLayer(IconsListBox);
                    if (_adornerLayer != null)
                    {
                        _ghostAdorner = new IconGhostAdorner(_draggedItemContainer, _adornerLayer, e.GetPosition(IconsListBox));
                        _adornerLayer.Add(_ghostAdorner);
                    }
                    Mouse.Capture(IconsListBox);
                }
            }

            if (_isDragging && _ghostAdorner != null)
            {
                _ghostAdorner.UpdatePosition(e.GetPosition(IconsListBox));
            }

            if (_isDragging)
            {
                // Reordenar en caliente
                int targetIndex = GetItemIndexUnderMouse(e);
                if (targetIndex >= 0 && targetIndex != _dragSourceIndex)
                {
                    var icons = _shortcutItems;
                    if (targetIndex < icons.Count)
                    {
                        icons.Move(_dragSourceIndex, targetIndex);
                        _dragSourceIndex = targetIndex;
                    }
                }
            }
        }

        private void IconsListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                if (_draggedItemContainer != null)
                    _draggedItemContainer.Opacity = 1.0;

                if (_adornerLayer != null && _ghostAdorner != null)
                {
                    _adornerLayer.Remove(_ghostAdorner);
                    _ghostAdorner = null;
                }

                Mouse.Capture(null);
            }

            _isDragging = false;
            _dragStartPoint = null;
            _dragSourceIndex = -1;
            _draggedItemContainer = null;

            // Persist ordering changes back to settings when drag finishes
            try
            {
                _settings.ShortcutPaths.Clear();
                foreach (var vm in _shortcutItems)
                {
                    _settings.ShortcutPaths.Add(vm.Path);
                }
                _settingsService.Save(_settings);
            }
            catch { }

            // If this was a click (not a drag), launch the clicked shortcut immediately (single-click behavior)
            try
            {
                if (!_isDragging)
                {
                    var pos = e.GetPosition(IconsListBox);
                    var element = IconsListBox.InputHitTest(pos) as DependencyObject;
                    while (element != null && element is not ListBoxItem)
                        element = VisualTreeHelper.GetParent(element);
                    if (element is ListBoxItem item && item.DataContext is ShortcutIconViewModel svm && !svm.IsSeparator)
                    {
                        try
                        {
                            IconsListBox.SelectedItem = svm;
                        }
                        catch { }
                        _ = LaunchOrActivateAsync(svm.Path);
                    }
                }
            }
            catch { }
        }

        // Helpers para hit test
        private ListBoxItem? GetListBoxItemUnderMouse(System.Windows.Input.MouseEventArgs e)
        {
            var point = e.GetPosition(IconsListBox);
            var element = IconsListBox.InputHitTest(point) as DependencyObject;
            while (element != null && element is not ListBoxItem)
                element = VisualTreeHelper.GetParent(element);
            return element as ListBoxItem;
        }

        private int GetItemIndexUnderMouse(System.Windows.Input.MouseEventArgs e)
        {
            var item = GetListBoxItemUnderMouse(e);
            return item != null ? IconsListBox.ItemContainerGenerator.IndexFromContainer(item) : -1;
        }

        // --- Adorner para ghost visual ---
        public class IconGhostAdorner : Adorner
        {
            private readonly VisualBrush _brush;
            private System.Windows.Point _offset;
            private readonly double _width;
            private readonly double _height;

            public IconGhostAdorner(UIElement adornedElement, AdornerLayer layer, System.Windows.Point startPoint) : base(layer)
            {
                _brush = new VisualBrush(adornedElement) { Opacity = 0.7 };
                _width = ((FrameworkElement)adornedElement).ActualWidth;
                _height = ((FrameworkElement)adornedElement).ActualHeight;
                _offset = startPoint;
                IsHitTestVisible = false;
            }

            public void UpdatePosition(System.Windows.Point p)
            {
                _offset = p;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext dc)
            {
                dc.DrawRectangle(_brush, null, new Rect(_offset.X - _width / 2, _offset.Y - _height / 2, _width, _height));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _settings = _settingsService.Load();
            ApplySettingsToUI();
            UpdateShortcutUI();
            // Register appbar and set height only after settings are loaded
            try
            {
                if (_windowHandle != IntPtr.Zero)
                {
                    RegisterAppBar(_windowHandle);
                    SetAppBarHeight(_settings.BarHeight);
                }
            }
            catch { }

            // Show the UI now that settings have been applied
            try { MainBorder.Visibility = Visibility.Visible; } catch { }
            // Assign this window to primary screen by default if none assigned
            AssignedScreen ??= Screen.PrimaryScreen;
            PositionTopBar();

            // If master window and user wants one per screen, create additional windows
            if (!IsSecondaryWindow && _settings.MultiDisplayMode == DisplayMode.OnePerScreen)
            {
                EnsureMultiScreenInstances();
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            _windowHandle = handle;
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource.AddHook(WndProc);
            NativeMethods.HideFromAltTab(handle);
        }

        private void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.PrimaryScreenWidth))
            {
                PositionTopBar();
            }
        }

        private void PositionTopBar()
        {
            SetAppBarHeight(_settings.BarHeight);
        }

        private void EnsureMultiScreenInstances()
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                // For each non-primary screen, ensure we have an extra window
                foreach (var sc in screens)
                {
                    if (sc.Primary) continue; // primary handled by main window
                    var existing = _extraWindows.Find(w => w.AssignedScreen?.DeviceName == sc.DeviceName);
                    if (existing != null)
                    {
                        // Update settings on existing
                        existing._settings = this._settings;
                        existing.ApplySettingsToUI();
                        existing.PositionTopBar();
                        continue;
                    }

                    // Create new window for this screen
                    var win = new MainWindow();
                    win.IsSecondaryWindow = true;
                    win.AssignedScreen = sc;
                    // Share settings instance so user changes propagate; keep separate service
                    win._settings = this._settings;
                    _extraWindows.Add(win);
                    win.Show();
                }

                // Close any extra windows whose screen disappeared
                for (int i = _extraWindows.Count - 1; i >= 0; i--)
                {
                    var w = _extraWindows[i];
                    if (Array.FindIndex(screens, s => s.DeviceName == w.AssignedScreen?.DeviceName) < 0)
                    {
                        try { w.Close(); } catch { }
                        _extraWindows.RemoveAt(i);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Error creating multi-screen windows", ex);
            }
        }

        private void CloseExtraWindows()
        {
            foreach (var w in _extraWindows.ToArray())
            {
                try { w.Close(); } catch { }
            }
            _extraWindows.Clear();
        }

        public void ApplySettingsToUI()
        {
            // Only set window Height directly for top edge; left/right windows are sized in SetAppBarHeight
            if (_settings.BarEdge == BarPosition.Top)
            {
                Height = _settings.BarHeight;
            }
            
            // Adjust content margin based on EdgePadding setting.
            // Use Margin on the inner MainGrid instead of Padding on MainBorder,
            // because negative Padding breaks WPF layout while negative Margin works fine.
            var epH = _settings.EdgePaddingH;
            var epV = _settings.EdgePaddingV;
            MainBorder.Padding = new Thickness(0);
            MainGrid.Margin = new Thickness(epH, epV, epH, epV);

            // Apply vertical offset to shortcut icons and virtual desktop buttons
            var topOff = _settings.IconTopOffset;
            
            // Adjust layout orientation and ItemsPanel based on edge
            if (_settings.BarEdge == BarPosition.Top)
            {
                // Ensure the window occupies full width (SetAppBarHeight handles Left/Width)
                // Place the three containers into the top row across three columns
                Grid.SetRow(DropAreaBorder, 0);
                Grid.SetColumn(DropAreaBorder, 0);

                Grid.SetRow(BarStackPanel, 0);
                Grid.SetColumn(BarStackPanel, 1);

                Grid.SetRow(GearBtn, 0);
                Grid.SetColumn(GearBtn, 2);

                // Horizontal layout across the top
                BarStackPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
                // Center the middle container so the second section sits in the middle of the screen
                BarStackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                BarStackPanel.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                // Remove top margin so icons align flush with the bar top
                BarStackPanel.Margin = new Thickness(0, topOff, 0, 0);
                var factoryH = new System.Windows.FrameworkElementFactory(typeof(StackPanel));
                factoryH.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Horizontal);
                IconsListBox.ItemsPanel = new ItemsPanelTemplate(factoryH);

                // Drop area: small pill on the left column
                DropAreaBorder.Width = double.NaN; // allow Auto/stretched but constrained by column
                DropAreaBorder.Height = double.NaN; // Auto
                DropAreaBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                DropAreaBorder.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                DropAreaBorder.Margin = new Thickness(2,0,8,0);
                // drop indicator removed

                // Gear button at the right column: align center vertically
                GearBtn.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                GearBtn.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                GearBtn.Margin = new Thickness(0, 0, 6, 0);

                // Shortcuts: center column should stretch; icons centered inside it
                // Remove top margin so icons sit flush vertically
                IconsListBox.Margin = new Thickness(0, topOff, 0, 0);
                IconsListBox.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                IconsListBox.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            }
            else
            {
                BarStackPanel.Orientation = System.Windows.Controls.Orientation.Vertical;
                BarStackPanel.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                var factoryV = new System.Windows.FrameworkElementFactory(typeof(StackPanel));
                factoryV.SetValue(StackPanel.OrientationProperty, System.Windows.Controls.Orientation.Vertical);
                IconsListBox.ItemsPanel = new ItemsPanelTemplate(factoryV);
                // For left/right edges: make the drop area a horizontal strip at the top
                DropAreaBorder.Width = double.NaN; // Auto / stretch
                DropAreaBorder.Height = 100;
                DropAreaBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                DropAreaBorder.VerticalAlignment = System.Windows.VerticalAlignment.Top;
                // Reduce any extra spacing so icons sit closer to the drop area
                DropAreaBorder.Margin = new Thickness(0,0,0,4);
                // drop indicator removed
                
                // Shortcuts: align to edge depending on left/right with 2px margin
                if (_settings.BarEdge == BarPosition.Left)
                {
                    // Stack elements into the Grid rows: drop (0), content (1), gear (2)
                    Grid.SetRow(DropAreaBorder, 0);
                    Grid.SetColumn(DropAreaBorder, 0);

                    Grid.SetRow(BarStackPanel, 1);
                    Grid.SetColumn(BarStackPanel, 0);

                    Grid.SetRow(GearBtn, 2);
                    Grid.SetColumn(GearBtn, 0);

                    // All elements aligned to left with minimal left padding (closer to screen edge)
                    BarStackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    BarStackPanel.Margin = new Thickness(0, 0, 0, 0);
                    IconsListBox.Margin = new Thickness(0, 0, 0, 0);
                    IconsListBox.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    // Gear button at bottom left with minimal left offset
                    GearBtn.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                    GearBtn.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    GearBtn.Margin = new Thickness(0, 0, 0, 6);
                }
                else // Right
                {
                    // Stack elements into the Grid rows but align to right column for mirror
                    Grid.SetRow(DropAreaBorder, 0);
                    Grid.SetColumn(DropAreaBorder, 2);

                    Grid.SetRow(BarStackPanel, 1);
                    Grid.SetColumn(BarStackPanel, 2);

                    Grid.SetRow(GearBtn, 2);
                    Grid.SetColumn(GearBtn, 2);

                    // All elements aligned to right with 2px margin
                    BarStackPanel.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    BarStackPanel.Margin = new Thickness(0, 0, 2, 0);
                    IconsListBox.Margin = new Thickness(0, 0, 2, 0);
                    IconsListBox.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    // Gear button at bottom right
                    GearBtn.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
                    GearBtn.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                    GearBtn.Margin = new Thickness(0, 0, 2, 6);
                }
                // Make icons align to the start (top) of the vertical bar
                IconsListBox.VerticalAlignment = VerticalAlignment.Top;
            }
            // Compute effective icon size based on bar thickness and user preference.
            // Reserve 4px for padding (2px top + 2px bottom); ensure icons fit inside the bar.
            var available = Math.Max(16.0, _settings.BarHeight - 4.0);
            var iconSize = (int)Math.Clamp(Math.Min(_settings.IconSize, available), 16, 96);
            var btnMargin = _settings.BarEdge == BarPosition.Top ? new Thickness(0, 0, 10, 0) : new Thickness(0, 0, 0, 10);
            MoveLeftBtn.Width = iconSize;
            MoveLeftBtn.Height = iconSize;
            MoveLeftBtn.Margin = btnMargin;
            AddDesktopBtn.Width = iconSize;
            AddDesktopBtn.Height = iconSize;
            AddDesktopBtn.Margin = btnMargin;
            CloseBtn.Width = iconSize;
            CloseBtn.Height = iconSize;
            CloseBtn.Margin = btnMargin;
            MoveRightBtn.Width = iconSize;
            MoveRightBtn.Height = iconSize;
            MoveRightBtn.Margin = btnMargin;
            GearBtn.Width = iconSize;
            GearBtn.Height = iconSize;
            // Mostrar/ocultar botones de escritorios virtual según la configuración
            // Use Hidden instead of Collapsed so layout spacing is preserved and UI doesn't break
            var vdVisibility = _settings.HideVirtualDesktopButtons ? Visibility.Hidden : Visibility.Visible;
            MoveLeftBtn.Visibility = vdVisibility;
            AddDesktopBtn.Visibility = vdVisibility;
            CloseBtn.Visibility = vdVisibility;
            MoveRightBtn.Visibility = vdVisibility;

            // Aplicar spacing horizontal configurable: simplemente actualizar la DependencyProperty
            // El binding en el XAML se encargará de actualizar los márgenes automáticamente
            IconSpacing = Math.Clamp(_settings.IconSpacing, 0.0, 200.0);

        }

        private void GearBtn_Click(object sender, RoutedEventArgs e)
        {
            // Open the dedicated SettingsWindow centered on screen.
            // Settings application is now handled by the delayed task in SettingsWindow.Save_Click
            var sw = new SettingsWindow(_settings);
            var result = sw.ShowDialog();
            // The settings will be applied after a 3-second delay by SettingsWindow
        }

        private void MainBorder_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            // Forward to existing handler so drag visual/accept logic is reused
            try { BarStackPanel_PreviewDragOver(sender, e); } catch { }
        }

        private void MainBorder_Drop(object sender, System.Windows.DragEventArgs e)
        {
            // Forward to existing handler so drop logic is reused
            try { BarStackPanel_Drop(sender, e); } catch { }
        }

        private void PopupSaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(BarHeightBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h)
                && double.TryParse(FontSizeBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f)
                && double.TryParse(IconSizeBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var icon))
            {
                h = Math.Clamp(h, 16, 200);
                f = Math.Clamp(f, 8, 72);
                icon = Math.Clamp(icon, 16, 96);

                // Make sure icons fit inside the bar.
                if (icon + 4 > h)
                {
                    h = Math.Clamp(icon + 8, 16, 200);
                }

                _settings.BarHeight = h;
                _settings.FontSize = f;
                _settings.IconSize = icon;
                _settings.HideVirtualDesktopButtons = HideVDBtnsCheckBox.IsChecked == true;
                _settingsService.Save(_settings);
                ApplySettingsToUI();
                UpdateShortcutUI();
                PositionTopBar();
                SettingsPopup.IsOpen = false;
            }
        }

        private void PopupExitBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "¿Salir y cerrar la barra?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddPopup.IsOpen = true;
            }
            catch { }
        }

        private void AddSeparatorBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Insert a unique separator token at end
                var token = SeparatorToken + ":" + System.Guid.NewGuid().ToString("N");
                _settings.ShortcutPaths.Add(token);
                _settingsService.Save(_settings);
                UpdateShortcutUI();
                AddPopup.IsOpen = false;
            }
            catch (Exception ex)
            {
                LogError("Error adding separator", ex);
            }
        }

        // Helper static class for icon conversion
        public static class IconHelper
        {
            public static ImageSource ToImageSource(System.Drawing.Icon icon, int size)
            {
                var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(size, size));
                bmp.Freeze();
                return bmp;
            }
        }

        // Helper to activate existing process window for a given file/executable
        private static class WindowActivator
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(System.IntPtr hWnd);
            [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);
            private const int SW_RESTORE = 9;
            private const int SW_MAXIMIZE = 3;

            public static bool TryActivateProcessForPath(string path)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path)) return false;
                    var ext = Path.GetExtension(path).ToLowerInvariant();
                    // If it's an executable, try match by process name
                    if (ext == ".exe" || ext == ".appref-ms")
                    {
                        var name = Path.GetFileNameWithoutExtension(path);
                        foreach (var p in Process.GetProcessesByName(name))
                        {
                            try
                            {
                                if (p.Id == Process.GetCurrentProcess().Id) continue;
                                var h = p.MainWindowHandle;
                                if (h == System.IntPtr.Zero) continue;
                                // Restore and maximize to ensure the app is visible in fullscreen
                                ShowWindow(h, SW_MAXIMIZE);
                                SetForegroundWindow(h);
                                return true;
                            }
                            catch { }
                        }
                    }

                    // Fallback: try to find a process whose main window title contains the file name
                    var fileName = Path.GetFileName(path);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        foreach (var p in Process.GetProcesses())
                        {
                            try
                            {
                                if (p.Id == Process.GetCurrentProcess().Id) continue;
                                var h = p.MainWindowHandle;
                                if (h == System.IntPtr.Zero) continue;
                                var title = p.MainWindowTitle;
                                if (!string.IsNullOrWhiteSpace(title) && title.IndexOf(fileName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    ShowWindow(h, SW_RESTORE);
                                    SetForegroundWindow(h);
                                    return true;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
                return false;
            }

            public static void ActivateWindow(IntPtr h)
            {
                try
                {
                    // Maximize the target window and bring to foreground
                    ShowWindow(h, SW_MAXIMIZE);
                    SetForegroundWindow(h);
                }
                catch { }
            }
        }
        
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            _settingsService.Save(_settings);
            var handle = new WindowInteropHelper(this).Handle;
            UnregisterAppBar(handle);
            if (_hwndSource is not null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
            // If primary window is closing, ensure extras are closed
            if (!IsSecondaryWindow)
            {
                CloseExtraWindows();
            }
        }

        private void RegisterAppBar(IntPtr hwnd)
        {
            _appBarCallbackMessage = NativeMethods.RegisterWindowMessage("ToolTopBar.AppBarMessage");
            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = hwnd,
                uCallbackMessage = _appBarCallbackMessage
            };
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref abd);
        }

        private void UnregisterAppBar(IntPtr hwnd)
        {
            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = hwnd
            };
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref abd);
        }

        // Called by SettingsWindow when settings saved/applied externally
        public void ApplyExternalSettings(Settings s)
        {
            if (s == null) return;

            // Keep existing settings object and only update changed values
            _settings.BarHeight = s.BarHeight;
            _settings.FontSize = s.FontSize;
            _settings.IconSize = s.IconSize;
            _settings.IconSpacing = s.IconSpacing;
            _settings.EdgePaddingH = s.EdgePaddingH;
            _settings.EdgePaddingV = s.EdgePaddingV;
            _settings.IconTopOffset = s.IconTopOffset;
            _settings.HideVirtualDesktopButtons = s.HideVirtualDesktopButtons;
            _settings.BarEdge = s.BarEdge;
            _settings.MultiDisplayMode = s.MultiDisplayMode;

            // Apply visible processes filter
            _settings.VisibleProcesses = s.VisibleProcesses ?? new List<string>();

            _settingsService.Save(_settings);
            ApplySettingsToUI();
            UpdateShortcutUI();
            PositionTopBar();
            // Recreate or close extra windows depending on new mode
            if (!IsSecondaryWindow)
            {
                if (_settings.MultiDisplayMode == DisplayMode.OnePerScreen)
                    EnsureMultiScreenInstances();
                else
                    CloseExtraWindows();
            }
        }

        private void SetAppBarHeight(double height)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            // Get DPI scale to convert logical height to physical pixels for the AppBar API.
            double dpiScale = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                dpiScale = source.CompositionTarget.TransformToDevice.M22;
            }

            // Use assigned screen bounds if this window is tied to a specific monitor
            var screenBounds = AssignedScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, (int)(SystemParameters.PrimaryScreenWidth * dpiScale), (int)(SystemParameters.PrimaryScreenHeight * dpiScale));
            var screenW = screenBounds.Width;
            var screenH = screenBounds.Height;
            // height is in WPF logical units; convert to physical pixels for the shell
            var thickness = (int)(height * dpiScale);

            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = hwnd,
                uEdge = (uint)(_settings.BarEdge == BarPosition.Left ? 0 : (_settings.BarEdge == BarPosition.Top ? 1 : 2)),
                rc = new NativeMethods.RECT()
            };

            // Ask the system for an appropriate position first
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref abd);

            if (_settings.BarEdge == BarPosition.Top)
            {
                abd.rc.left = screenBounds.Left;
                abd.rc.top = screenBounds.Top;
                abd.rc.right = screenBounds.Left + screenW;
                abd.rc.bottom = abd.rc.top + thickness;
            }
            else if (_settings.BarEdge == BarPosition.Left)
            {
                abd.rc.left = screenBounds.Left;
                abd.rc.top = screenBounds.Top;
                abd.rc.right = abd.rc.left + thickness;
                abd.rc.bottom = screenBounds.Top + screenH;
            }
            else // Right
            {
                abd.rc.left = screenBounds.Left + screenW - thickness;
                abd.rc.top = screenBounds.Top;
                abd.rc.right = screenBounds.Left + screenW;
                abd.rc.bottom = screenBounds.Top + screenH;
            }

            NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref abd);

            // Convert from physical pixels to WPF logical units using the DPI scaling factor.
            // Screen.Bounds and SHAppBarMessage work in physical pixels, but WPF
            // Left/Top/Width/Height are in device-independent units (96 DPI-based).
            // Reuse dpiScale obtained above; also get X axis scale.
            double dpiScaleX = dpiScale; // assume uniform scaling; refine if needed
            if (source?.CompositionTarget != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            }

            Left = abd.rc.left / dpiScaleX;
            Top = abd.rc.top / dpiScale;
            Width = (abd.rc.right - abd.rc.left) / dpiScaleX;
            Height = (abd.rc.bottom - abd.rc.top) / dpiScale;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_appBarCallbackMessage != 0 && msg == _appBarCallbackMessage)
            {
                // Windows is telling us the appbar position/workarea changed.
                var notification = wParam.ToInt32();
                if (notification == NativeMethods.ABN_POSCHANGED)
                {
                    SetAppBarHeight(_settings.BarHeight);
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private void LogError(string message, Exception ex)
        {
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            string logMessage = $"[{DateTime.Now}] {message}: {ex.Message}\n{ex.StackTrace}\n";
            File.AppendAllText(logFilePath, logMessage);
        }

        private void MoveLeftBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NativeMethods.SwitchVirtualDesktopLeft();
            }
            catch (Exception ex)
            {
                LogError("Error al mover a la izquierda", ex);
                System.Windows.MessageBox.Show("Error al mover a la izquierda: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MoveRightBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NativeMethods.SwitchVirtualDesktopRight();
            }
            catch (Exception ex)
            {
                LogError("Error al mover a la derecha", ex);
                System.Windows.MessageBox.Show("Error al mover a la derecha: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddDesktopBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NativeMethods.CreateVirtualDesktop();
            }
            catch (Exception ex)
            {
                LogError("Error al agregar escritorio virtual", ex);
                System.Windows.MessageBox.Show("Error al agregar escritorio virtual: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "¿Cerrar el escritorio virtual actual?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    NativeMethods.CloseVirtualDesktop();
                }
                catch (Exception ex)
                {
                    LogError("Error al cerrar escritorio virtual", ex);
                    System.Windows.MessageBox.Show("Error al cerrar escritorio virtual: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void BarStackPanel_Drop(object sender, System.Windows.DragEventArgs e)
        {
            try
            {
                Console.WriteLine($"Drop event triggered. DataPresent: {e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)}");
                if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                {
                    Console.WriteLine("No file drop detected.");
                    return;
                }
                var files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files == null || files.Length == 0)
                {
                    Console.WriteLine("No files found in drop.");
                    return;
                }
                var allowed = new[] { ".lnk", ".exe", ".bat", ".cmd", ".url", ".appref-ms" };
                bool added = false;
                foreach (var file in files)
                {
                    Console.WriteLine($"Processing file: {file}");
                    
                    // Verificar si es una carpeta
                    if (Directory.Exists(file))
                    {
                        if (!_settings.ShortcutPaths.Contains(file))
                        {
                            _settings.ShortcutPaths.Add(file);
                            added = true;
                            Console.WriteLine($"Folder added: {file}");
                        }
                        continue;
                    }
                    
                    if (!File.Exists(file))
                    {
                        Console.WriteLine($"File does not exist: {file}");
                        continue;
                    }
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (!allowed.Contains(ext))
                    {
                        Console.WriteLine($"Extension not allowed: {ext}");
                        continue;
                    }
                    if (!_settings.ShortcutPaths.Contains(file))
                    {
                        _settings.ShortcutPaths.Add(file);
                        added = true;
                        Console.WriteLine($"Shortcut added: {file}");
                    }
                }
                if (added)
                {
                    _settingsService.Save(_settings);
                    UpdateShortcutUI();
                    Console.WriteLine("Shortcuts updated and saved.");
                }
                else
                {
                    Console.WriteLine("No shortcuts added.");
                }
            }
            catch (Exception ex)
            {
                LogError("Error al asignar acceso directo", ex);
                Console.WriteLine($"Error: {ex.Message}\n{ex.StackTrace}");
                System.Windows.MessageBox.Show("Error al asignar acceso directo: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private async void ShortcutIcon_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Image img && img.DataContext is ShortcutIconViewModel vm)
            {
                try
                {
                    // Ensure the clicked item becomes selected/focused so keyboard actions (Delete) work
                    try { IconsListBox.SelectedItem = vm; } catch { }
                    if (vm.IsSeparator) return; // separators are not launchable
                    var path = vm.Path;
                    if (string.IsNullOrWhiteSpace(path))
                        return;
                    if (!File.Exists(path) && !Directory.Exists(path))
                    {
                        System.Windows.MessageBox.Show("El acceso directo configurado ya no existe. Volvé a arrastrarlo.", "Acceso directo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _settings.ShortcutPaths.Remove(path);
                        _settingsService.Save(_settings);
                        UpdateShortcutUI();
                        return;
                    }
                    // Try to activate an existing instance that owns a main window for this target
                    var target = GetBestIconPath(path);
                    await LaunchOrActivateAsync(path);
                }
                catch (Exception ex)
                {
                    LogError("Error al ejecutar acceso directo", ex);
                    System.Windows.MessageBox.Show("Error al ejecutar acceso directo: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void IconsListBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                var vm = IconsListBox.SelectedItem as ShortcutIconViewModel;
                if (vm == null)
                    return;

                var display = vm.IsSeparator ? "separador" : $"acceso directo '{System.IO.Path.GetFileName(vm.Path)}'";
                var result = System.Windows.MessageBox.Show($"¿Eliminar {display}?", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Remove first matching path/token from settings and persist
                        var idx = _settings.ShortcutPaths.IndexOf(vm.Path);
                        if (idx >= 0) _settings.ShortcutPaths.RemoveAt(idx);
                        _settingsService.Save(_settings);
                        UpdateShortcutUI();

                        // Move selection to nearby item if possible
                        if (IconsListBox.Items.Count > 0)
                        {
                            IconsListBox.SelectedIndex = Math.Min(Math.Max(0, idx), IconsListBox.Items.Count - 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("Error al eliminar acceso directo/separador", ex);
                        System.Windows.MessageBox.Show("Error al eliminar: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                e.Handled = true;
            }
        }


        private const string SeparatorToken = "__SEP__";

        // Shared helper: try to activate quickly, otherwise start the process immediately.
        private async Task LaunchOrActivateAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            
            // Si es una carpeta, abrirla directamente en el explorador
            if (Directory.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    LogError($"Error al abrir carpeta: {path}", ex);
                }
                return;
            }
            
            if (!File.Exists(path)) return;
            var target = GetBestIconPath(path);
            try
            {
                var activationTask = Task.Run(() => WindowActivator.TryActivateProcessForPath(target));
                var finished = await Task.WhenAny(activationTask, Task.Delay(150));
                if (finished == activationTask && activationTask.IsCompleted && activationTask.Result)
                {
                    return; // quickly activated an existing instance
                }
            }
            catch { }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process? proc = null;
                try { proc = Process.Start(psi); } catch { proc = null; }

                // If we have a process object, wait briefly for its main window and maximize it.
                IntPtr hwnd = IntPtr.Zero;
                if (proc != null)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < 2000)
                    {
                        try
                        {
                            proc.Refresh();
                            hwnd = proc.MainWindowHandle;
                            if (hwnd != IntPtr.Zero) break;
                        }
                        catch { }
                        await Task.Delay(100);
                    }
                }

                // Fallback: find by process name if we couldn't obtain handle from returned Process
                if (hwnd == IntPtr.Zero)
                {
                    try
                    {
                        var name = Path.GetFileNameWithoutExtension(path);
                        foreach (var p in Process.GetProcessesByName(name))
                        {
                            if (p.Id == Process.GetCurrentProcess().Id) continue;
                            try { if (p.MainWindowHandle != IntPtr.Zero) { hwnd = p.MainWindowHandle; break; } } catch { }
                        }
                    }
                    catch { }
                }

                if (hwnd != IntPtr.Zero)
                {
                    try { WindowActivator.ActivateWindow(hwnd); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogError("Error starting process", ex);
            }
        }

        private void UpdateShortcutUI()
        {
            // Normalize any legacy bare separator tokens to unique tokens so each separator is independent
            try
            {
                bool changed = false;
                for (int i = 0; i < _settings.ShortcutPaths.Count; i++)
                {
                    var p = _settings.ShortcutPaths[i];
                    if (string.Equals(p, SeparatorToken, StringComparison.OrdinalIgnoreCase))
                    {
                        _settings.ShortcutPaths[i] = SeparatorToken + ":" + Guid.NewGuid().ToString("N");
                        changed = true;
                    }
                }
                if (changed)
                {
                    try { _settingsService.Save(_settings); } catch { }
                }
            }
            catch { }

            // Build a list of icon sources for the ItemsControl
            var available = Math.Max(16.0, _settings.BarHeight - 4.0);
            var iconSize = (int)Math.Clamp(Math.Min(_settings.IconSize, available), 16, 96);
            _shortcutItems.Clear();
            foreach (var path in _settings.ShortcutPaths.ToList())
            {
                if (!string.IsNullOrEmpty(path) && path.StartsWith(SeparatorToken, StringComparison.OrdinalIgnoreCase))
                {
                    // Separator item (any token that starts with the separator prefix)
                    _shortcutItems.Add(new ShortcutIconViewModel { Path = path, Icon = IconHelper.ToImageSource(SystemIcons.Application, iconSize), IconSize = iconSize, IsSeparator = true });
                    continue;
                }
                
                // Verificar si es una carpeta
                if (Directory.Exists(path))
                {
                    var src = TryGetIconImageSource(path, iconSize);
                    if (src == null)
                    {
                        // Usar icono de carpeta del sistema
                        src = IconHelper.ToImageSource(SystemIcons.Shield, iconSize);
                    }
                    _shortcutItems.Add(new ShortcutIconViewModel { Path = path, Icon = src, IconSize = iconSize });
                    continue;
                }
                
                if (!File.Exists(path))
                    continue;
                var iconPath = GetBestIconPath(path);
                var src2 = TryGetIconImageSource(iconPath, iconSize);
                if (src2 == null)
                {
                    // Fallback: use a default icon (e.g. a built-in system icon)
                    src2 = IconHelper.ToImageSource(SystemIcons.Application, iconSize);
                }
                _shortcutItems.Add(new ShortcutIconViewModel { Path = path, Icon = src2, IconSize = iconSize });
            }
            if (IconsListBox.ItemsSource != _shortcutItems)
            {
                IconsListBox.ItemsSource = _shortcutItems;
            }
        }

        public class ShortcutIconViewModel
        {
            public string Path { get; set; } = string.Empty;
            public ImageSource Icon { get; set; } = null!;
            public double IconSize { get; set; }
            public bool IsSeparator { get; set; } = false;
        }

        // Simple view model for enumerated top-level windows
        public class WindowInfo : INotifyPropertyChanged
        {
            public IntPtr Hwnd { get; set; }
            public string ProcessName { get; set; } = string.Empty;

            private string _appName = string.Empty;
            // Friendly application name (FileDescription) e.g. "Visual Studio Code"
            public string AppName
            {
                get => _appName;
                set { _appName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppName))); }
            }

            // Short project/name extracted from window title (e.g. "tool-top-bar")
            public string ProjectName { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;

            private ImageSource? _icon;
            public ImageSource? Icon
            {
                get => _icon;
                set { _icon = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon))); }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        private readonly ObservableCollection<WindowInfo> _windowItems = new ObservableCollection<WindowInfo>();
        // Cancellation for background window enumeration
        private System.Threading.CancellationTokenSource? _windowEnumerationCts;

        // P/Invoke for enumerating top-level windows
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        

        private void WindowsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Cancel any previous enumeration in progress
                try { _windowEnumerationCts?.Cancel(); } catch { }
                _windowEnumerationCts = new System.Threading.CancellationTokenSource();

                // Open popup immediately with empty list for snappy response
                _windowItems.Clear();
                WindowListBox.ItemsSource = _windowItems;
                WindowsPopup.IsOpen = true;

                var cts = _windowEnumerationCts;
                // Perform enumeration on a background thread and add items incrementally
                System.Threading.Tasks.Task.Run(() =>
                {
                    var shell = GetShellWindow();
                    EnumWindows((hwnd, lParam) =>
                    {
                        try
                        {
                            if (cts is null || cts.IsCancellationRequested) return false; // stop enumeration
                            if (hwnd == shell) return true;
                            if (!IsWindowVisible(hwnd)) return true;
                            int len = GetWindowTextLength(hwnd);
                            if (len == 0) return true;
                            var sb = new System.Text.StringBuilder(len + 1);
                            GetWindowText(hwnd, sb, sb.Capacity);
                            var title = sb.ToString();
                            if (string.IsNullOrWhiteSpace(title)) return true;
                            GetWindowThreadProcessId(hwnd, out uint pid);

                            string procName = string.Empty;
                            try
                            {
                                var p = Process.GetProcessById((int)pid);
                                procName = p.ProcessName;
                            }
                            catch { }

                            // Quick filter by process name (if configured)
                            bool include = true;
                            try
                            {
                                if (_settings.VisibleProcesses != null && _settings.VisibleProcesses.Count > 0)
                                {
                                    include = _settings.VisibleProcesses.Any(v => string.Equals(v, procName, StringComparison.OrdinalIgnoreCase));
                                }
                            }
                            catch { }

                            if (!include) return true;

                            // Lightweight initial info: avoid heavy calls (MainModule, icons, FileVersionInfo) here
                            string appName = procName;
                            string projectName = title;
                            try
                            {
                                var parts = title.Split(new string[] { " - " }, StringSplitOptions.None);
                                if (parts.Length >= 3)
                                {
                                    projectName = parts[parts.Length - 2];
                                }
                                else if (parts.Length == 2)
                                {
                                    projectName = parts[0];
                                }
                            }
                            catch { }

                            var win = new WindowInfo { Hwnd = hwnd, ProcessName = procName, AppName = appName, ProjectName = projectName, Title = title, Icon = null };
                            // Add to UI collection on UI thread
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { _windowItems.Add(win); } catch { }
                            }));
                        }
                        catch { }
                        return true;
                    }, IntPtr.Zero);

                    // Optionally, after fast enumeration, kick off background tasks to enrich items (icons/descriptions)
                    // We'll load icons/descriptions for visible items but do it in background with cancellation
                    try
                    {
                        if (cts != null && !cts.IsCancellationRequested)
                        {
                            var itemsSnapshot = System.Linq.Enumerable.ToList(_windowItems);
                            var sem = new System.Threading.SemaphoreSlim(3);
                            var enrichTasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
                            foreach (var item in itemsSnapshot)
                            {
                                if (cts.IsCancellationRequested) break;
                                enrichTasks.Add(System.Threading.Tasks.Task.Run(async () =>
                                {
                                    await sem.WaitAsync(cts.Token).ConfigureAwait(false);
                                    try
                                    {
                                        // Try to resolve exe path and icon; tolerate failures
                                        try
                                        {
                                            GetWindowThreadProcessId(item.Hwnd, out uint pid2);
                                            var p2 = Process.GetProcessById((int)pid2);
                                            var exe = p2.MainModule?.FileName;
                                            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
                                            {
                                                var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
                                                var friendly = fvi.FileDescription;
                                                if (!string.IsNullOrWhiteSpace(friendly))
                                                {
                                                    var name = friendly;
                                                    Dispatcher.BeginInvoke(new Action(() => { item.AppName = name; }));
                                                }
                                                var icon = TryGetIconImageSource(exe, 24);
                                                if (icon != null)
                                                {
                                                    icon.Freeze(); // Make cross-thread safe
                                                    Dispatcher.BeginInvoke(new Action(() => { item.Icon = icon; }));
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                    finally { sem.Release(); }
                                }, cts.Token));
                            }
                            System.Threading.Tasks.Task.WhenAll(enrichTasks).ContinueWith(_ => { }, System.Threading.Tasks.TaskScheduler.Default);
                        }
                    }
                    catch { }
                }, cts?.Token ?? System.Threading.CancellationToken.None);
                // Auto-cancel enumeration after a short timeout so popup is responsive
                System.Threading.Tasks.Task.Delay(800).ContinueWith(_ => { try { _windowEnumerationCts?.Cancel(); } catch { } });
            }
            catch (Exception ex)
            {
                LogError("Error enumerating windows", ex);
            }
        }

        private void WindowListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (WindowListBox.SelectedItem is WindowInfo info)
            {
                try
                {
                    WindowActivator.ActivateWindow(info.Hwnd);
                    WindowsPopup.IsOpen = false;
                }
                catch (Exception ex)
                {
                    LogError("Error activating window", ex);
                }
            }
        }

        private void WindowListBox_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var pos = e.GetPosition(WindowListBox);
                var element = WindowListBox.InputHitTest(pos) as DependencyObject;
                while (element != null && element is not ListBoxItem)
                    element = VisualTreeHelper.GetParent(element);
                if (element is ListBoxItem item && item.DataContext is WindowInfo info)
                {
                    try
                    {
                        WindowActivator.ActivateWindow(info.Hwnd);
                        WindowsPopup.IsOpen = false;
                        e.Handled = true;
                    }
                    catch (Exception ex)
                    {
                        LogError("Error activating window (single click)", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Error handling single-click on WindowListBox", ex);
            }
        }

        private static string GetBestIconPath(string shortcutOrExePath)
        {
            var ext = Path.GetExtension(shortcutOrExePath).ToLowerInvariant();
            if (ext == ".lnk")
            {
                var resolved = TryResolveLnkTarget(shortcutOrExePath);
                if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                {
                    return resolved;
                }
            }
            return shortcutOrExePath;
        }

        private static string? TryResolveLnkTarget(string lnkPath)
        {
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                if (t is null) return null;
                dynamic shell = Activator.CreateInstance(t)!;
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                string? target = shortcut.TargetPath as string;
                return string.IsNullOrWhiteSpace(target) ? null : target;
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource? TryGetIconImageSource(string filePath, int desiredSize)
        {
            try
            {
                // If it's a directory, try to get the system folder icon via SHGetFileInfo
                if (Directory.Exists(filePath))
                {
                    var hIcon = NativeMethods.GetSystemIconHandle(filePath, true, desiredSize <= 16);
                    if (hIcon != IntPtr.Zero)
                    {
                        var bmpDir = Imaging.CreateBitmapSourceFromHIcon(
                            hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromWidthAndHeight(desiredSize, desiredSize));
                        NativeMethods.DestroyIcon(hIcon);
                        bmpDir.Freeze();
                        return bmpDir;
                    }
                }

                // Fallback for files: Extract associated icon
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
                if (icon is null) return null;

                var bmp = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(desiredSize, desiredSize));

                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
        private void BarStackPanel_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

    }
}

