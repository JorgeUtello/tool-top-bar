using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ToolTopBar
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<AppIcon> Icons { get; } = new ObservableCollection<AppIcon>();

        public MainViewModel()
        {
            // Ejemplo de íconos (reemplazar con tus propios paths)
            Icons.Add(new AppIcon { Id = 1, Title = "App 1", Icon = null });
            Icons.Add(new AppIcon { Id = 2, Title = "App 2", Icon = null });
            Icons.Add(new AppIcon { Id = 3, Title = "App 3", Icon = null });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
