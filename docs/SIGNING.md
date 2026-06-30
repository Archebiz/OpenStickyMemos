# Firma de Código — Code Signing

OpenStickyMemos firma digitalmente los binarios de la aplicación de escritorio para garantizar su autenticidad y evitar advertencias de antivirus/SmartScreen.

---

## Opciones de firma (open-source)

| Opción | Costo | Descripción |
|--------|-------|-------------|
| **SignPath.io** ✅ | **Gratis** para open-source | Certificado compartido, integración GitHub Actions |
| Azure Code Signing | ~$10/mes | Integración nativa Microsoft, confianza SmartScreen |
| Certificado EV (DigiCert) | ~$300/año | Máxima confianza, proceso manual |

**Recomendación**: [SignPath.io](https://about.signpath.io/) — gratuito para proyectos open-source, firma automática en CI/CD.

---

## Cómo configurar SignPath.io

### 1. Crear cuenta en SignPath.io
- Ir a [signpath.io](https://about.signpath.io/) y registrarse como open-source
- Conectar el repositorio de GitHub
- Solicitar acceso al programa Open Source
- Crear un proyecto y una política de firma

### 2. Configurar GitHub Actions Secrets

Agregar en `Settings → Secrets and variables → Actions`:

| Secret | Descripción |
|--------|-------------|
| `SIGNPATH_API_TOKEN` | Token de API de SignPath |
| `SIGNPATH_ORG_ID` | ID de la organización en SignPath |

### 3. Descomentar el paso en `.github/workflows/release.yml`

Busca la sección `# ── Code Signing ──` en el workflow y descomenta las líneas correspondientes a SignPath, ajustando los slugs según tu configuración.

---

## Verificar la firma

### En Windows

1. Haz clic derecho en `OpenStickyMemos.Desktop.exe`
2. Selecciona **Properties** (Propiedades)
3. Ve a la pestaña **Digital Signatures** (Firmas digitales)
4. Deberías ver la entrada de SignPath o del certificado usado

### Desde PowerShell

```powershell
Get-AuthenticodeSignature -FilePath "OpenStickyMemos.Desktop.exe"
```

---

## ¿Por qué es importante?

- ✅ **SmartScreen no bloquea** la descarga
- ✅ **Antivirus no marca falso positivo**
- ✅ **Usuarios no necesitan configurar excepciones**
- ✅ **Autenticidad garantizada** del publicador
