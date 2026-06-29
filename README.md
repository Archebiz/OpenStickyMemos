# OpenStickyMemos 🗒️

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-19-DD0031?logo=angular)](https://angular.dev/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql)](https://www.postgresql.org/)
[![Railway](https://img.shields.io/badge/Railway-ready-0B0D0E?logo=railway)](https://railway.app)

> **Notas adhesivas colaborativas en tiempo real.** Comparte notas sobre proyectos con tu equipo, actualizadas al instante desde la web o la app de escritorio.

---

## 🚀 ¿Qué es OpenStickyMemos?

Una aplicación **open-source** para crear y compartir **notas adhesivas (sticky notes)** de forma colaborativa sobre proyectos. Cada nota se actualiza en **tiempo real** gracias a SignalR.

### ✨ Funcionalidades

- 📝 Crear, editar, mover y redimensionar notas adhesivas
- 🎨 Notas con color personalizable
- 👥 Compartir proyectos e invitar colaboradores
- ⚡ Actualización en tiempo real (SignalR)
- 🪟 App de escritorio WPF (.NET 10)
- 🌐 Interfaz web Angular 19
- 🔐 Autenticación con Google y Microsoft (OAuth + JWT)
- 🐘 PostgreSQL como base de datos

---

## 🏗️ Arquitectura

```
OpenStickyMemos/
├── src/
│   ├── backend/                  → .NET 10 Web API + SignalR
│   ├── web/                      → Angular 19 SPA
│   └── desktop/                  → WPF .NET 10 Desktop App
├── docs/                         → Documentación
├── docker-compose.yml            → Desarrollo local (opcional)
└── .github/workflows/            → CI/CD
```

### Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| **Backend** | .NET 10, ASP.NET Core, SignalR, EF Core |
| **Base de datos** | PostgreSQL 16 |
| **Frontend Web** | Angular 19, Angular Material, SignalR Client |
| **Desktop** | WPF .NET 10, CommunityToolkit.Mvvm, WebView2 |
| **Tiempo real** | SignalR (WebSockets) |
| **Auth** | OAuth 2.0 (Google + Microsoft), JWT Bearer |
| **Infra** | Railway (deploy directo desde GitHub) |

---

## 🖥️ Plataformas

| Cliente | Estado |
|---------|--------|
| 🌐 Web (Angular) | ✅ Planeado |
| 🪟 WPF Desktop | ✅ Planeado |

---

## 📦 Distribución

- **Web**: Acceso directo desde tu instancia en Railway
- **Desktop**: Descargable desde [GitHub Releases](https://github.com/your-org/OpenStickyMemos/releases)
  - `OpenStickyMemos.Desktop-{version}-portable.zip` — Sin instalador, copiar y ejecutar
  - `OpenStickyMemos.Desktop-{version}-installer.exe` — Instalador firmado digitalmente

---

## 🔧 Self-hosting en Railway

Cada usuario puede levantar su propia instancia en Railway en pocos minutos:

1. **Haz fork** del repositorio en GitHub
2. **Crea una cuenta** en [Railway](https://railway.app)
3. **Conecta tu fork** a Railway (`New Project` → `Deploy from GitHub repo`)
4. **Agrega PostgreSQL** desde Railway Dashboard
5. **Configura variables de entorno** (JWT secret, OAuth keys)
6. **¡Listo!** Railway buildca y despliega automáticamente

> 📖 Guía completa en [`docs/SETUP.md`](docs/SETUP.md)

---

## 🛠️ Desarrollo local

```bash
# Requisitos
- .NET 10 SDK
- Node.js 22+
- Angular CLI 19
- PostgreSQL 16 (o Docker)

# 1. Clonar
git clone https://github.com/your-org/OpenStickyMemos.git
cd OpenStickyMemos

# 2. Backend
cd src/backend
dotnet restore
dotnet run

# 3. Frontend
cd src/web/open-sticky-memos
npm install
ng serve

# 4. Docker (alternativa a PostgreSQL local)
docker-compose up -d postgres
```

---

## 📚 Documentación

| Documento | Descripción |
|-----------|-------------|
| [`docs/SETUP.md`](docs/SETUP.md) | Self-hosting en Railway paso a paso |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Decisiones técnicas y diagramas |
| [`docs/DISTRIBUTION.md`](docs/DISTRIBUTION.md) | Cómo descargar y verificar el desktop app |
| [`docs/SIGNING.md`](docs/SIGNING.md) | Firma de código y seguridad |

---

## 📄 Licencia

**MIT** — Ver [LICENSE](LICENSE) para más detalles.

---

## 🙌 Contribuir

¿Ideas, bugs, mejoras? Abre un [issue](https://github.com/your-org/OpenStickyMemos/issues) o un [pull request](https://github.com/your-org/OpenStickyMemos/pulls).
