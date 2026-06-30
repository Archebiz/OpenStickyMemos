# Guía de Self-Hosting — OpenStickyMemos

Sigue esta guía para levantar tu propia instancia de OpenStickyMemos en Railway.  
Tendrás tu propio servidor con backend, base de datos y frontend web funcionando en menos de 30 minutos.

---

## 📋 Requisitos previos

- Una cuenta en [GitHub](https://github.com)
- Una cuenta en [Railway](https://railway.app) (plan gratuito)
- (Opcional) Cuentas de desarrollador en [Google Cloud Console](https://console.cloud.google.com) y [Azure Portal](https://portal.azure.com) para OAuth

---

## 🚀 Paso a paso

### 1. Hacer fork del repositorio

1. Ve a [github.com/Archebiz/OpenStickyMemos](https://github.com/Archebiz/OpenStickyMemos)
2. Haz clic en **Fork** → **Create a new fork**
3. Selecciona tu cuenta y crea el fork

### 2. Crear proyecto en Railway

> ⚠️ Railway requiere crear **dos servicios** separados para el monorepo:  
> **Backend** (API .NET) y **Frontend** (Angular). Sigue los pasos para cada uno.

### 2a. Crear servicio Backend

1. Inicia sesión en [Railway](https://railway.app)
2. Haz clic en **New Project** → **Deploy from GitHub repo**
3. Elige tu fork de `OpenStickyMemos`
4. En **Settings** → **Root Directory**, ingresa: `src/backend`
5. Railway usará el `railway.json` de esa carpeta con Docker para buildcar .NET 10

### 2b. Crear servicio Frontend

1. Dentro del mismo proyecto, haz clic en **New** → **Service** → **Deploy from GitHub repo**
2. Selecciona el mismo fork
3. En **Settings** → **Root Directory**, ingresa: `src/web/open-sticky-memos`
4. Railway detectará Angular con Nixpacks automáticamente

### 3. Agregar PostgreSQL

1. Dentro del proyecto en Railway, haz clic en **New**
2. Selecciona **Database** → **PostgreSQL**
3. Railway lo provisionará automáticamente
4. La variable `DATABASE_URL` se inyectará automáticamente y la app la usará sin configuración adicional

### 4. Configurar variables de entorno

En Railway, ve a tu proyecto → **Variables**. Configura las siguientes en cada servicio:

#### Backend (servicio en `src/backend`)

| Variable | Descripción | Ejemplo |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Entorno de ejecución | `Production` |
| `ConnectionStrings__DefaultConnection` | Cadena de conexión PostgreSQL | *(Railway la inyecta automáticamente)* |
| `JWT__Key` | Clave secreta JWT (mínimo 32 caracteres) | `TuClaveSecretaSuperSeguraCambiaEsto!` |
| `JWT__Issuer` | Emisor del JWT | `OpenStickyMemos` |
| `JWT__Audience` | Audiencia del JWT | `OpenStickyMemos` |
| `OAuth__Google__ClientId` | Client ID de Google OAuth | *(de Google Cloud Console)* |
| `OAuth__Google__ClientSecret` | Client Secret de Google | *(de Google Cloud Console)* |
| `OAuth__Microsoft__ClientId` | Client ID de Microsoft | *(de Azure Portal)* |
| `OAuth__Microsoft__ClientSecret` | Client Secret de Microsoft | *(de Azure Portal)* |
| `OAuth__Microsoft__TenantId` | Tenant ID de Microsoft | `common` |
| `ALLOWED_ORIGINS` | Orígenes CORS permitidos | `https://tudominio.railway.app` |

#### Frontend (servicio en `src/web/open-sticky-memos`)

| Variable | Descripción | Ejemplo |
|----------|-------------|---------|
| `API_URL` | URL del backend (sin / al final) | `https://tudominio-backend.up.railway.app` |

> ⚠️ Railway usa `__` (doble underscore) para representar secciones anidadas.  
> Ejemplo: `JWT__Key` representa `Jwt:Key` en `appsettings.json`.

> ⚡ El frontend genera su `config.json` automáticamente al iniciar usando la variable `API_URL`. No necesita rebuild.

### 5. Configurar OAuth (Google)

1. Ve a [Google Cloud Console](https://console.cloud.google.com)
2. Crea un proyecto nuevo o selecciona uno existente
3. Ve a **APIs & Services** → **Credentials**
4. Crea una **OAuth 2.0 Client ID** (tipo Web application)
5. Agrega como **Authorized redirect URIs**:
   - App desktop: `http://localhost`
   - App web: `https://tudominio.railway.app/auth/callback`
6. Copia el **Client ID** y **Client Secret** a las variables de entorno en Railway

### 6. Configurar OAuth (Microsoft)

1. Ve a [Azure Portal](https://portal.azure.com) → **App registrations**
2. Crea un nuevo registro
3. Agrega como **Redirect URI**:
   - App desktop: `http://localhost`
   - App web: `https://tudominio.railway.app/auth/callback`
4. Ve a **Certificates & Secrets** y crea un **Client Secret**
5. Copia el **Application (client) ID** y el **Client Secret** a Railway

### 7. Deploy automático

Railway se encarga de todo:

1. Cada push a `main` en tu fork dispara un deploy automático
2. Railway buildca con Nixpacks (detecta .NET y Node.js)
3. Las migraciones de base de datos se ejecutan automáticamente al iniciar
4. Tu backend estará disponible en `https://tuproyecto.up.railway.app`

### 8. Acceder a la app web

- **API**: `https://tuproyecto.up.railway.app/api/health`
- **Web**: `https://tuproyecto.up.railway.app`
- **OpenAPI**: `https://tuproyecto.up.railway.app/openapi/v1.json`

---

## 🖥️ Configurar la app desktop

1. Descarga la última versión desde [GitHub Releases](https://github.com/Archebiz/OpenStickyMemos/releases)
2. Extrae el archivo `.zip`
3. Edita `appsettings.json` (junto al .exe):

```json
{
  "ApiUrl": "https://tuproyecto.up.railway.app",
  "SignalRUrl": "https://tuproyecto.up.railway.app/hubs/notes"
}
```

4. Ejecuta `OpenStickyMemos.Desktop.exe`

---

## 🔄 Actualizar tu instancia

```bash
# En tu fork local
git pull upstream main
git push origin main

# Railway detecta el cambio y deploya automáticamente
```

---

## ❓ Troubleshooting

| Problema | Solución |
|----------|----------|
| **502 Bad Gateway** | Espera 30s a que termine el build initial |
| **Base de datos no conecta** | Verifica `DATABASE_URL` en Railway |
| **OAuth no funciona** | Verifica Client ID / Secret y Redirect URIs |
| **CORS errors** | Agrega `ALLOWED_ORIGINS` con tu dominio |
| **JWT inválido** | Regenera `JWT__Key` (mínimo 32 caracteres) |
