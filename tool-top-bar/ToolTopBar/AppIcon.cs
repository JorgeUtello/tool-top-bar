using System.Windows.Media;

namespace ToolTopBar
{
    public class AppIcon
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public ImageSource Icon { get; set; }
        public System.Windows.Input.ICommand Command { get; set; }
    }
}
