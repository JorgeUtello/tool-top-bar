# ToolTopBar

Barra superior minimalista para Windows 11, nativa y ligera.

- Siempre arriba, sin bordes, oculta en Alt+Tab y en la barra de tareas.
- Muestra "Hola mundo" centrado.
- Configurable: alto de la barra y tamaño de fuente.

## Opción recomendada (sin instalar nada)
PowerShell + WPF (incluido en Windows 11)

```powershell
# Ejecutar en PowerShell (preferiblemente desde PowerShell, no cmd)
cd "c:\Users\JorgeUtello\OneDrive - LARTIRIGOYEN Y CIA. S.A\Documentos\JAU\tool-top-bar"
powershell -NoProfile -ExecutionPolicy Bypass -STA -File .\topbar.ps1
```

- Clic derecho sobre la barra → "Configurar..." para cambiar alto y tamaño de fuente.
- Los ajustes se guardan en %AppData%/ToolTopBar/settings.json.

## Opción alternativa (WPF .NET 8)
Si prefieres compilar el proyecto WPF (requiere instalar .NET SDK 8):

```powershell
cd "c:\Users\JorgeUtello\OneDrive - LARTIRIGOYEN Y CIA. S.A\Documentos\JAU\tool-top-bar\ToolTopBar"
dotnet run -c Release
```

> Nota: La opción de PowerShell es más nativa en el sentido de no requerir instalaciones adicionales. La versión .NET ofrece base para evolucionar a un ejecutable.
