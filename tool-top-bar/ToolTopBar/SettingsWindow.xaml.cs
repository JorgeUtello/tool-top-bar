using System;
using System.Globalization;
using System.Windows;

namespace ToolTopBar
{
    public partial class SettingsWindow : Window
    {
        private System.Collections.ObjectModel.ObservableCollection<ProcessEntry> _processes = new System.Collections.ObjectModel.ObservableCollection<ProcessEntry>();
        public Settings ResultSettings { get; private set; }
        private readonly SettingsService _settingsService = new SettingsService();

        public SettingsWindow(Settings current)
        {
            InitializeComponent();
            ResultSettings = new Settings
            {
                BarHeight = current.BarHeight,
                FontSize = current.FontSize,
                IconSize = current.IconSize,
                IconSpacing = current is not null ? current.IconSpacing : 16,
                EdgePaddingH = current is not null ? current.EdgePaddingH : 0,
                EdgePaddingV = current is not null ? current.EdgePaddingV : 0,
                IconTopOffset = current is not null ? current.IconTopOffset : 1,
                ShortcutPaths = current is not null ? new System.Collections.Generic.List<string>(current.ShortcutPaths) : new System.Collections.Generic.List<string>(),
                VisibleProcesses = current is not null && current.VisibleProcesses != null ? new System.Collections.Generic.List<string>(current.VisibleProcesses) : new System.Collections.Generic.List<string>(),
                HideVirtualDesktopButtons = current.HideVirtualDesktopButtons,
                BarEdge = current.BarEdge
                ,
                MultiDisplayMode = current.MultiDisplayMode
            };

            BarHeightBox.Text = ResultSettings.BarHeight.ToString(CultureInfo.InvariantCulture);
            IconSizeBox.Text = ResultSettings.IconSize.ToString(CultureInfo.InvariantCulture);
            IconSpacingBox.Text = ResultSettings.IconSpacing.ToString(CultureInfo.InvariantCulture);
            EdgePaddingHBox.Text = ResultSettings.EdgePaddingH.ToString(CultureInfo.InvariantCulture);
            EdgePaddingVBox.Text = ResultSettings.EdgePaddingV.ToString(CultureInfo.InvariantCulture);
            IconTopOffsetBox.Text = ResultSettings.IconTopOffset.ToString(CultureInfo.InvariantCulture);
            HideVDBtnsCheckBox.IsChecked = ResultSettings.HideVirtualDesktopButtons;
            // Select BarEdge in combo
            switch (ResultSettings.BarEdge)
            {
                case BarPosition.Left:
                    BarEdgeCombo.SelectedIndex = 0; break;
                case BarPosition.Top:
                    BarEdgeCombo.SelectedIndex = 1; break;
                case BarPosition.Right:
                    BarEdgeCombo.SelectedIndex = 2; break;
            }
            // Inicializar DisplayMode combo
            switch (ResultSettings.MultiDisplayMode)
            {
                case DisplayMode.PrimaryOnly:
                    DisplayModeCombo.SelectedIndex = 0; break;
                case DisplayMode.OnePerScreen:
                    DisplayModeCombo.SelectedIndex = 1; break;
            }

            // Inicializar lista de procesos en la pestaña "Hola Mundo"
            ProcessItemsControl.ItemsSource = _processes;
            LoadProcesses();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(BarHeightBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
                && double.TryParse(IconSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var icon)
                && double.TryParse(IconSpacingBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var spacing)
                && double.TryParse(EdgePaddingHBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var edgePadH)
                && double.TryParse(EdgePaddingVBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var edgePadV)
                && double.TryParse(IconTopOffsetBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var topOff))
            {
                h = Math.Clamp(h, 16, 200);
                icon = Math.Clamp(icon, 16, 96);
                spacing = Math.Max(0, spacing);
                ResultSettings.BarHeight = h;
                ResultSettings.IconSize = icon;
                ResultSettings.IconSpacing = spacing;
                ResultSettings.EdgePaddingH = edgePadH;
                ResultSettings.EdgePaddingV = edgePadV;
                ResultSettings.IconTopOffset = topOff;
                ResultSettings.HideVirtualDesktopButtons = HideVDBtnsCheckBox.IsChecked == true;
                // Read BarEdge from combo
                var sel = (BarEdgeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
                if (string.Equals(sel, "Left", StringComparison.OrdinalIgnoreCase)) ResultSettings.BarEdge = BarPosition.Left;
                else if (string.Equals(sel, "Right", StringComparison.OrdinalIgnoreCase)) ResultSettings.BarEdge = BarPosition.Right;
                else ResultSettings.BarEdge = BarPosition.Top;

                // Read MultiDisplayMode from combo
                var dm = (DisplayModeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
                if (string.Equals(dm, "OnePerScreen", StringComparison.OrdinalIgnoreCase)) ResultSettings.MultiDisplayMode = DisplayMode.OnePerScreen;
                else ResultSettings.MultiDisplayMode = DisplayMode.PrimaryOnly;

                // Save settings
                // Persist process selections from Hola Mundo tab
                PersistProcessSelections();

                _settingsService.Save(ResultSettings);

                DialogResult = true;
                Close();

                // Esperar 3 segundos y luego aplicar settings para dar tiempo a que los contenedores se generen
                var settingsToApply = ResultSettings;
                System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                        {
                            mw.ApplyExternalSettings(settingsToApply);
                        }
                    });
                });
            }
            else
            {
                System.Windows.MessageBox.Show("Valores inválidos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Apply immediately and save when any numeric box changes or arrows pressed
        private void ApplyImmediate()
        {
            if (!double.TryParse(BarHeightBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var h)) return;
            if (!double.TryParse(IconSizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var icon)) return;
            if (!double.TryParse(IconSpacingBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var spacing)) return;
            if (!double.TryParse(EdgePaddingHBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var edgePadH)) return;
            if (!double.TryParse(EdgePaddingVBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var edgePadV)) return;
            if (!double.TryParse(IconTopOffsetBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var topOff)) return;
            h = Math.Clamp(h, 16, 200);
            icon = Math.Clamp(icon, 16, 96);
            spacing = Math.Max(0, spacing);

            ResultSettings.BarHeight = h;
            ResultSettings.IconSize = icon;
            ResultSettings.IconSpacing = spacing;
            ResultSettings.EdgePaddingH = edgePadH;
            ResultSettings.EdgePaddingV = edgePadV;
            ResultSettings.IconTopOffset = topOff;
            ResultSettings.HideVirtualDesktopButtons = HideVDBtnsCheckBox.IsChecked == true;

            // Save settings and notify main window
            // Update MultiDisplayMode from combo before saving
            var dm2 = (DisplayModeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
            if (string.Equals(dm2, "OnePerScreen", StringComparison.OrdinalIgnoreCase)) ResultSettings.MultiDisplayMode = DisplayMode.OnePerScreen;
            else ResultSettings.MultiDisplayMode = DisplayMode.PrimaryOnly;

            _settingsService.Save(ResultSettings);
            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
            {
                mw.ApplyExternalSettings(ResultSettings);
            }
        }

        private void DisplayModeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var sel = (DisplayModeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
            if (string.Equals(sel, "OnePerScreen", StringComparison.OrdinalIgnoreCase)) ResultSettings.MultiDisplayMode = DisplayMode.OnePerScreen;
            else ResultSettings.MultiDisplayMode = DisplayMode.PrimaryOnly;

            // Persist and apply immediately
            _settingsService.Save(ResultSettings);
            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
            {
                mw.ApplyExternalSettings(ResultSettings);
            }
        }

        private void NumericBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down)
                {
                    if (double.TryParse(tb.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    {
                        v += (e.Key == System.Windows.Input.Key.Up) ? 1 : -1;
                        tb.Text = v.ToString(CultureInfo.InvariantCulture);
                        tb.CaretIndex = tb.Text.Length;
                        ApplyImmediate();
                    }
                    e.Handled = true;
                }
            }
        }

        private void NumericBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyImmediate();
        }

        private void IncrementButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string boxName)
            {
                var textBox = FindName(boxName) as System.Windows.Controls.TextBox;
                if (textBox != null && double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    v += 1;
                    textBox.Text = v.ToString(CultureInfo.InvariantCulture);
                    ApplyImmediate();
                }
            }
        }

        private void DecrementButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string boxName)
            {
                var textBox = FindName(boxName) as System.Windows.Controls.TextBox;
                if (textBox != null && double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    v -= 1;
                    textBox.Text = v.ToString(CultureInfo.InvariantCulture);
                    ApplyImmediate();
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void LoadProcesses()
        {
            _processes.Clear();
            try
            {
                var procs = System.Diagnostics.Process.GetProcesses()
                    .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                    .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase);
                foreach (var p in procs)
                {
                    string name = p.ProcessName;
                    string display = string.IsNullOrWhiteSpace(p.MainWindowTitle) ? name : $"{name} — {p.MainWindowTitle}";
                    bool checkedByDefault = ResultSettings.VisibleProcesses != null && ResultSettings.VisibleProcesses.Contains(name);
                    _processes.Add(new ProcessEntry { ProcessName = name, Pid = p.Id, DisplayName = display, IsChecked = checkedByDefault });
                }
            }
            catch
            {
                // ignore access errors
            }
        }

        private void RefreshProcesses_Click(object sender, RoutedEventArgs e)
        {
            LoadProcesses();
        }

        private void SelectAllProcesses_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pe in _processes) pe.IsChecked = true;
            // refresh binding
            ProcessItemsControl.Items.Refresh();
        }

        // Ensure selections are saved into ResultSettings before saving
        private void PersistProcessSelections()
        {
            ResultSettings.VisibleProcesses = _processes.Where(p => p.IsChecked).Select(p => p.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private class ProcessEntry : System.ComponentModel.INotifyPropertyChanged
        {
            public string ProcessName { get; set; }
            public int Pid { get; set; }
            public string DisplayName { get; set; }

            private bool _isChecked;
            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked != value)
                    {
                        _isChecked = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsChecked)));
                    }
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        }
    }
}
