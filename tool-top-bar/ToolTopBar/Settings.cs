namespace ToolTopBar
{
    public class Settings
    {
        public double BarHeight { get; set; } = 48; // px
        public double FontSize { get; set; } = 14; // pt
        public double IconSize { get; set; } = 40; // px (alto/ancho de los botones)

        // Espacio horizontal entre iconos (px)
        public double IconSpacing { get; set; } = 16;

        // Distancia horizontal (px) entre los íconos y el borde de la barra
        public double EdgePaddingH { get; set; } = 0;

        // Distancia vertical (px) entre los íconos y el borde de la barra
        public double EdgePaddingV { get; set; } = 0;

        // Posición vertical (top offset en px) de íconos y botones de escritorio virtual
        public double IconTopOffset { get; set; } = 1;


        // Lista de accesos directos configurables por drag&drop (.exe/.lnk/etc.)
        public List<string> ShortcutPaths { get; set; } = new List<string>();

        // Si es true, ocultar los botones relacionados con los escritorios virtuales
        public bool HideVirtualDesktopButtons { get; set; } = false;

        // Posición de la barra en el escritorio
        // Posición del appbar: Left, Top, Right
        public BarPosition BarEdge { get; set; } = BarPosition.Top;

        // Mostrar la barra en: solo pantalla principal, o una por cada pantalla activa
        public DisplayMode MultiDisplayMode { get; set; } = DisplayMode.PrimaryOnly;

        // Lista de procesos/nombrés de programas permitidos para mostrar en la ventana activa
        public List<string> VisibleProcesses { get; set; } = new List<string>();

        public static Settings Default() => new Settings();
    }

    public enum BarPosition
    {
        Left = 0,
        Top = 1,
        Right = 2
    }

    public enum DisplayMode
    {
        PrimaryOnly = 0,
        OnePerScreen = 1
    }
}
