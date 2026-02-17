# ToolTopBar

Una barra de herramientas personalizable para Windows que se integra con el sistema como AppBar, mostrando accesos directos a aplicaciones y ventanas activas con soporte para escritorios virtuales.

## 📋 Características

- **Barra de herramientas del sistema**: Se registra como AppBar de Windows reservando espacio en pantalla
- **Accesos directos personalizables**: Arrastra y suelta archivos ejecutables (.exe) o carpetas para crear accesos rápidos
- **Gestión de ventanas activas**: Muestra las aplicaciones abiertas con sus iconos en tiempo real
- **Acceso rápido a carpetas**: Agrega tus carpetas favoritas y ábrelas con un solo clic
- **Escritorios virtuales**: Navegación integrada entre escritorios virtuales de Windows 10/11
- **Multi-monitor**: Soporta configuración para mostrar en pantalla principal o en todas las pantallas
- **Altamente personalizable**: Ajusta altura, tamaño de íconos, espaciado, márgenes y más
- **Posicionamiento flexible**: Coloca la barra a la izquierda, derecha o arriba de la pantalla
- **Filtrado de procesos**: Selecciona qué aplicaciones mostrar en la barra
- **Escalado DPI**: Funciona correctamente en pantallas con diferentes escalas DPI
- **Tema oscuro**: Interfaz moderna con tema oscuro elegante

## 🖥️ Requisitos del sistema

- **Sistema operativo**: Windows 10 o superior
- **.NET Runtime**: .NET 7.0 o superior
- **Resolución mínima**: 1280x720
- **Escritorios virtuales**: Requiere Windows 10/11 para la funcionalidad de escritorios virtuales

## 🚀 Instalación

### Desde el código fuente

1. **Clonar el repositorio**:
   ```bash
   git clone https://github.com/JorgeUtello/tool-top-bar.git
   cd tool-top-bar
   ```

2. **Compilar el proyecto**:
   ```bash
   dotnet build tool-top-bar/ToolTopBar/ToolTopBar.csproj -c Release
   ```

3. **Ejecutar la aplicación**:
   ```bash
   dotnet run --project tool-top-bar/ToolTopBar/ToolTopBar.csproj
   ```

### Compilación para distribución

```bash
dotnet publish tool-top-bar/ToolTopBar/ToolTopBar.csproj -c Release -r win-x64 --self-contained
```

El ejecutable se generará en: `tool-top-bar/ToolTopBar/bin/Release/net7.0-windows/win-x64/publish/`

## 📖 Uso

### Inicio rápido

1. **Ejecutar ToolTopBar.exe**: Al iniciar, la barra aparecerá en la parte superior de la pantalla
2. **Agregar accesos directos**: Arrastra cualquier archivo `.exe` a la barra para crear un acceso directo
3. **Usar las aplicaciones**: Haz clic en los iconos para abrir aplicaciones o cambiar entre ventanas activas
4. **Configurar**: Haz clic en el ícono de engranaje (⚙️) para abrir la ventana de configuración

### Agregar accesos directos

- **Arrastrar y soltar archivos**: Arrastra un archivo `.exe` desde el Explorador de Windows directamente a la barra
- **Arrastrar y soltar carpetas**: Arrastra cualquier carpeta para tener acceso rápido a ella
- **Abrir carpetas**: Haz clic en un acceso directo de carpeta para abrirla en el Explorador de Windows
- **Eliminar**: Arrastra un acceso directo existente fuera de la barra para eliminarlo
- Los accesos directos se guardan automáticamente y persisten entre sesiones

### Escritorios virtuales

- **← (Flecha izquierda)**: Navegar al escritorio virtual anterior
- **➕ (Más)**: Crear un nuevo escritorio virtual
- **✕ (Cerrar)**: Cerrar el escritorio virtual actual
- **→ (Flecha derecha)**: Navegar al escritorio virtual siguiente

*Nota: Los botones de escritorios virtuales se pueden ocultar desde la configuración*

## ⚙️ Configuración

Accede a la configuración haciendo clic en el ícono de engranaje (⚙️) en la barra.

### Pestaña "Opciones"

| Opción | Descripción | Valor predeterminado |
|--------|-------------|---------------------|
| **Alto de barra (px)** | Altura de la barra en píxeles | 40 |
| **Alto ícono (px)** | Tamaño de los iconos en píxeles | 32 |
| **Espacio entre íconos (px)** | Separación horizontal entre iconos | 8 |
| **Margen horizontal (px)** | Distancia desde el borde izquierdo/derecho | 0 |
| **Margen vertical (px)** | Distancia desde el borde superior/inferior | 0 |
| **Posición vertical íconos (px)** | Ajuste fino vertical de los iconos | 1 |
| **Posición** | Ubicación de la barra (Izquierda/Arriba/Derecha) | Arriba |
| **Mostrar la barra en** | Solo pantalla principal o una por pantalla | Pantalla principal |
| **Ocultar botones de escritorio virtual** | Oculta los botones de navegación de VD | No |

### Pestaña "Programas Activos"

- **Lista de procesos**: Muestra todos los procesos con ventana principal (GUI)
- **Seleccionar/Deseleccionar**: Marca los programas que quieres ver en la barra
- **Refrescar**: Actualiza la lista de procesos en ejecución
- **Seleccionar todo**: Marca todos los procesos disponibles

### Persistencia de configuración

La configuración se guarda automáticamente en:
```
%APPDATA%\ToolTopBar\settings.json
```

## 🎨 Personalización

### Márgenes negativos

Puedes usar valores negativos en los márgenes horizontal y vertical para que los iconos se extiendan más allá del borde de la barra, creando efectos visuales únicos.

### Ajuste fino de posición

El parámetro "Posición vertical íconos" permite ajustes de 1 píxel para alinear perfectamente los iconos según tus preferencias visuales.

### Escalado DPI

La aplicación detecta automáticamente la escala DPI de tu pantalla y ajusta la altura de la barra en consecuencia, garantizando una apariencia consistente en diferentes configuraciones de pantalla.

## 🏗️ Arquitectura técnica

### Componentes principales

- **MainWindow.xaml/.cs**: Ventana principal de la barra, gestión de AppBar, iconos y eventos
- **SettingsWindow.xaml/.cs**: Ventana de configuración con validación de entrada
- **Settings.cs**: Modelo de datos para la configuración
- **SettingsService.cs**: Servicio de persistencia JSON
- **NativeMethods.cs**: Interoperabilidad P/Invoke con Win32 API
- **AppIcon.cs**: Gestión de extracción de iconos de ejecutables
- **MainViewModel.cs**: ViewModel para binding de datos
- **WeatherService.cs**: Servicio de clima (si está implementado)

### APIs de Windows utilizadas

- **SHAppBarMessage**: Registro de AppBar y reserva de espacio en pantalla
- **GetDpiForWindow**: Detección de escala DPI
- **IVirtualDesktopManager**: Gestión de escritorios virtuales (COM)
- **ExtractIconEx**: Extracción de iconos de archivos ejecutables

### Binding de datos

La aplicación utiliza `INotifyPropertyChanged` para actualizar la UI automáticamente cuando cambian:
- Iconos de ventanas activas
- Nombres de aplicaciones
- Estados de configuración

## 🐛 Solución de problemas

### La barra no aparece

- Verifica que no haya otra instancia en ejecución
- Comprueba que .NET 7.0 está instalado correctamente
- Revisa el Administrador de tareas para ver si el proceso está activo

### Los iconos no se muestran correctamente

- Algunos ejecutables pueden no tener iconos integrados
- Verifica los permisos de acceso a los archivos
- Intenta arrastrar el acceso directo nuevamente

### La barra se superpone con ventanas maximizadas

- Esto puede ocurrir en configuraciones DPI no estándar
- Ajusta el "Alto de barra" en la configuración
- Reinicia la aplicación después de cambiar la configuración

### Los escritorios virtuales no funcionan

- Asegúrate de estar en Windows 10 o superior
- Verifica que los escritorios virtuales estén habilitados en Windows
- Comprueba que tienes permisos para usar la API de escritorios virtuales

## 🤝 Contribuciones

Las contribuciones son bienvenidas. Por favor:

1. Haz fork del repositorio
2. Crea una rama para tu característica (`git checkout -b feature/NuevaCaracteristica`)
3. Commit tus cambios (`git commit -m 'feat: Agregar nueva característica'`)
4. Push a la rama (`git push origin feature/NuevaCaracteristica`)
5. Abre un Pull Request

### Convenciones de commits

Utilizamos commits convencionales:
- `feat:` Nueva característica
- `fix:` Corrección de errores
- `docs:` Cambios en documentación
- `style:` Cambios de formato/estilo
- `refactor:` Refactorización de código
- `test:` Agregar o modificar tests
- `chore:` Tareas de mantenimiento

## 📝 Roadmap

- [ ] Temas personalizables (claro/oscuro/personalizado)
- [ ] Soporte para plugins
- [ ] Atajos de teclado globales
- [ ] Animaciones de transición
- [ ] Widget de clima integrado
- [ ] Notificaciones del sistema
- [ ] Modo compacto
- [ ] Exportar/importar configuración
- [ ] Instalador MSI/MSIX

## 📄 Licencia

Este proyecto está bajo la licencia MIT. Consulta el archivo `LICENSE` para más detalles.

## 👤 Autor

**Jorge Utello** - [@JorgeUtello](https://github.com/JorgeUtello)

## 🙏 Agradecimientos

- Comunidad de .NET por las excelentes herramientas y documentación
- Contribuidores de Stack Overflow por soluciones a problemas de Win32 API
- Microsoft por la documentación de AppBar y escritorios virtuales

---

⭐ Si este proyecto te resulta útil, considera darle una estrella en GitHub
