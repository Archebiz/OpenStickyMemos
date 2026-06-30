# Distribución — Desktop App

La aplicación de escritorio **OpenStickyMemos Desktop** se distribuye a través de [GitHub Releases](https://github.com/Archebiz/OpenStickyMemos/releases).

---

## Formato actual

### 📦 Portable (.zip)

Actualmente solo está disponible el formato portable (sin instalador).

```
OpenStickyMemos.Desktop-{version}-portable.zip
```

- **Peso aproximado**: ~80-100 MB (self-contained .NET 10)
- **Requiere**: Windows 10 u 11 (64-bit)
- **Uso**: Descargar, extraer y ejecutar `OpenStickyMemos.Desktop.exe`
- **Ventaja**: No requiere instalación, no modifica el registro
- **Desventaja**: Mayor tamaño por incluir .NET runtime

---

## Cómo descargar

### Opción 1: Desde GitHub Releases (recomendado)

1. Ve a [GitHub Releases](https://github.com/Archebiz/OpenStickyMemos/releases)
2. Busca la última versión (latest release)
3. Descarga `OpenStickyMemos.Desktop-{version}-portable.zip`
4. Extrae el contenido en una carpeta
5. Ejecuta `OpenStickyMemos.Desktop.exe`

### Opción 2: Compilar desde código fuente

```bash
# Requisitos: .NET 10 SDK
git clone https://github.com/Archebiz/OpenStickyMemos.git
cd OpenStickyMemos/src/desktop/OpenStickyMemos.Desktop

# Publicar como portable
dotnet publish -c Release -r win-x64 --self-contained ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o ./publish
```

> ⚠️ **PowerShell**: Si usas PowerShell, usa `` ` `` (backtick) para continuar líneas en vez de `^`.  
> O ejecuta todo en una sola línea:
> ```powershell
> dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
> ```

---

## Verificar la firma digital (pendiente)

> ⏳ **Próximamente**: Se planea integrar [SignPath](https://about.signpath.io/) para firmar digitalmente el ejecutable.  
> Mientras tanto, tu antivirus podría bloquear la app por no estar firmada.  
> Agrega la carpeta de la app a las exclusiones de tu antivirus o usa **Windows Defender**:
> 1. Ve a **Seguridad de Windows** → **Protección contra virus y amenazas**
> 2. **Administrar configuración** → **Exclusiones**
> 3. Agrega la carpeta donde extrajiste `OpenStickyMemos.Desktop.exe`

Cuando esté disponible, la verificación se hará así:

```powershell
# Verificar firma
Get-AuthenticodeSignature -FilePath "ruta\OpenStickyMemos.Desktop.exe"

# El resultado debe mostrar:
#   SignerCertificate: CN=... (SignPath o el certificado usado)
#   Status: Valid
```

También puedes verificar en Windows:
1. Clic derecho → Properties → Digital Signatures

---

## Configurar el servidor

Al abrir la app por primera vez, se conectará a `http://localhost:5000` por defecto.

### Usar tu propia instancia

Edita el archivo `appsettings.json` (junto al .exe):

```json
{
  "ApiUrl": "https://tu-instancia.railway.app",
  "SignalRUrl": "https://tu-instancia.railway.app/api/hubs/notes"
}
```

### Variables de entorno

Alternativamente, puedes configurar:

| Variable | Descripción |
|----------|-------------|
| `API_URL` | URL base de la API |
| `SIGNALR_URL` | URL del hub de SignalR | `https://tu-instancia.railway.app/api/hubs/notes` |

---

## Requisitos del sistema

| Componente | Requisito |
|------------|-----------|
| **Sistema Operativo** | Windows 10 / 11 (64-bit) |
| **Arquitectura** | x64 |
| **.NET Runtime** | Incluido (self-contained) |
| **WebView2** | Incluido en Windows 11 / actualizable en Windows 10 |
| **RAM** | Mínimo 2 GB |
| **Espacio** | ~150 MB libres |
