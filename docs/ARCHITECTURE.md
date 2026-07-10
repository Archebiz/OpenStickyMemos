# Arquitectura de OpenStickyMemos

---

## 📐 Diagrama general

```mermaid
graph TB
    subgraph "Clientes"
        WPF[App Desktop WPF]
        WEB[App Web Angular]
    end

    subgraph "Railway (Servidor)"
        API[Backend .NET 10]
        DB[(PostgreSQL)]
        SIGNALR[SignalR Hub]
    end

    WPF -->|HTTP REST| API
    WPF -->|WebSocket| SIGNALR
    WEB -->|HTTP REST| API
    WEB -->|WebSocket| SIGNALR
    API -->|EF Core| DB
    SIGNALR -->|Broadcast| WPF
    SIGNALR -->|Broadcast| WEB
```

---

## 🏗️ Stack tecnológico

| Capa | Tecnología | Versión |
|------|-----------|---------|
| **Backend API** | ASP.NET Core | 10.0 |
| **ORM** | Entity Framework Core | 10.0 |
| **Base de datos** | PostgreSQL | 16 |
| **Tiempo real** | SignalR (WebSockets) | 10.0 |
| **Auth** | JWT Bearer + OAuth 2.0 (Google/Microsoft) | — |
| **Frontend Web** | Angular con Material | 19 |
| **Desktop** | WPF .NET + CommunityToolkit.Mvvm | 10.0 |
| **Containerización** | Docker (opcional) | — |
| **Hosting** | Railway (Nixpacks) | — |
| **CI/CD** | GitHub Actions | — |

---

## 🗄️ Modelo de datos

```mermaid
erDiagram
    User ||--o{ Project : owns
    User ||--o{ ProjectMember : member
    User ||--o{ ProjectInvitation : creates
    User ||--o{ Note : author
    Project ||--|{ ProjectMember : has
    Project ||--|{ ProjectInvitation : has
    Project ||--|{ Note : contains
    ProjectMember ||--|| User : user
    ProjectMember ||--|| Project : project
    ProjectInvitation ||--|| Project : for
    ProjectInvitation ||--|| User : created_by
    Note ||--|| Project : belongs
    Note ||--|| User : authored

    User {
        guid Id PK
        string Email UK
        string DisplayName
        string AvatarUrl
        string AuthProvider
        string ProviderId UK
        datetime CreatedAt
    }

    Project {
        guid Id PK
        string Name
        string Description
        guid OwnerId FK
        datetime CreatedAt
        datetime UpdatedAt
    }

    ProjectMember {
        guid Id PK
        guid ProjectId FK
        guid UserId FK
        enum Role
        datetime JoinedAt
    }

    ProjectInvitation {
        guid Id PK
        guid ProjectId FK
        string InvitedEmail "nullable"
        string Token UK
        guid CreatedById FK
        datetime CreatedAt
        datetime ExpiresAt
        bool IsAccepted
        guid AcceptedByUserId FK "nullable"
        datetime AcceptedAt "nullable"
    }

    Note {
        guid Id PK
        guid ProjectId FK
        guid AuthorId FK
        string Title
        text Content
        string Color
        float PositionX
        float PositionY
        float Width
        float Height
        bool IsPinned
        datetime CreatedAt
        datetime UpdatedAt
    }
```

---

## 🔀 Flujo de autenticación

```mermaid
sequenceDiagram
    participant C as Cliente (Web/WPF)
    participant P as Proveedor OAuth
    participant API as Backend
    participant DB as PostgreSQL

    C->>P: 1. Redirige a OAuth (Google/Microsoft)
    P-->>C: 2. id_token
    C->>API: 3. POST /auth/{provider} (id_token)
    API->>P: 4. Valida id_token
    API->>DB: 5. Busca/crea usuario
    API-->>C: 6. JWT + refresh token
    C->>API: 7. Peticiones con Bearer JWT
```

---

## ⚡ Flujo de tiempo real (SignalR)

```mermaid
sequenceDiagram
    participant U1 as Usuario A (Web)
    participant S as Servidor SignalR
    participant U2 as Usuario B (Desktop)

    U1->>S: 1. JoinProject(proyectoId)
    U2->>S: 2. JoinProject(proyectoId)
    U1->>S: 3. HTTP POST /notes (crear nota)
    S-->>U2: 4. NoteCreated (broadcast)
    U2->>S: 5. Mueve nota
    S-->>U1: 6. NoteUpdated (broadcast)
```

---

## 📁 Estructura del proyecto

```
OpenStickyMemos/
├── src/
│   ├── OpenStickyMemos.slnx
│   ├── backend/
│   │   └── OpenStickyMemos.Api/
│   │       ├── Controllers/     ← Health, Auth, Projects, Notes, Invitations
│   │       ├── Models/          ← User, Project, ProjectMember, ProjectInvitation, Note
│   │       ├── Data/            ← AppDbContext + Migrations
│   │       ├── Services/        ← Jwt, Project, Note, Invitation services
│   │       ├── Hubs/            ← NotesHub (SignalR)
│   │       └── DTOs/            ← Request/Response + Validators
│   ├── web/
│   │   └── open-sticky-memos/   ← Angular 19 SPA
│   │       ├── core/            ← Auth, SignalR, API, Interceptor
│   │       └── pages/           ← Login, Dashboard, Board
│   └── desktop/
│       └── OpenStickyMemos.Desktop/ ← WPF .NET 10
│           ├── Services/        ← API, Auth, SignalR, Settings
│           ├── ViewModels/      ← MVVM ViewModels
│           └── Views/           ← MainWindow, Login, Dashboard, Board
├── .github/workflows/           ← CI/CD pipelines
├── docs/                        ← Documentación
├── docker-compose.yml           ← (Opcional) Desarrollo local
└── README.md
```

---

## � Flujo de invitaciones por link

```mermaid
sequenceDiagram
    participant O as Owner
    participant API as Backend
    participant I as Invitado
    participant DB as PostgreSQL

    O->>API: POST /projects/{id}/invitations
    API->>DB: Crea ProjectInvitation con token único
    API-->>O: InvitationResponse { invitationLink, token, expiresAt }
    O-->>I: Comparte link (web/desktop/whatsapp/etc)
    I->>API: GET /api/invitations/{token}
    API-->>I: InvitationPublicResponse { projectName, createdByName, isExpired }
    alt No está logueado
        I->>API: Se redirige a /login?returnUrl=/invite/{token}
        I->>API: POST /auth/login → JWT
        API-->>I: AuthResponse
    end
    I->>API: POST /api/invitations/{token}/accept (Bearer JWT)
    API->>DB: Crea ProjectMember + marca IsAccepted=true
    API-->>I: InvitationResponse { isAccepted=true }
    API-->>Grupo SignalR: MemberAdded (broadcast a miembros del proyecto)
    I->>API: GET /projects/{id}/notes
    API-->>I: Notes del proyecto
```

---

## �🔐 Decisiones técnicas

| Decisión | Opción | Motivo |
|----------|--------|--------|
| **Tiempo real** | SignalR | Reconexión automática, grupos, integrado en .NET |
| **Resolución conflictos** | LWW (Last Writer Wins) | Simple, predecible |
| **Auth Desktop** | WebView2 + OAuth PKCE | Reutiliza flujo estándar, cada app maneja su propio login |
| **Persistencia tokens** | SQLite + DataProtection | Cifrado local, auto-login sin fricción |
| **Configuración** | appsettings.json + env vars | Portable, Railway-ready |
| **ORM** | EF Core + Migrations automáticas | Migraciones al iniciar, sin scripts manuales |
| **Frontend** | Standalone Components | Angular 19, sin NgModules, lazy loading |
| **Deploy Railway** | Nixpacks (sin Docker) | Build directo desde GitHub, más simple |
| **Distribución Desktop** | GitHub Releases (portable) | Sin store, descarga directa |
| **Code Signing** | SignPath.io | Gratuito para open-source |

---

## 📊 Endpoints de la API

| Método | Ruta | Auth | Descripción |
|--------|------|:----:|-------------|
| `GET` | `/api/health` | — | Health check |
| `POST` | `/api/auth/google` | — | Login con Google |
| `POST` | `/api/auth/microsoft` | — | Login con Microsoft |
| `POST` | `/api/auth/refresh` | — | Refresh JWT |
| `GET` | `/api/projects` | 🔐 | Listar proyectos |
| `POST` | `/api/projects` | 🔐 | Crear proyecto |
| `GET` | `/api/projects/{id}` | 🔐 | Detalle proyecto |
| `PUT` | `/api/projects/{id}` | 🔐 | Actualizar proyecto |
| `DELETE` | `/api/projects/{id}` | 🔐 | Eliminar proyecto |
| `POST` | `/api/projects/{id}/members` | 🔐 | Invitar miembro por email |
| `DELETE` | `/api/projects/{id}/members/{userId}` | 🔐 | Remover miembro |
| `POST` | `/api/projects/{id}/invitations` | 🔐 | Generar link de invitación (owner) |
| `GET` | `/api/projects/{id}/invitations` | 🔐 | Listar invitaciones activas (owner) |
| `DELETE` | `/api/projects/{id}/invitations/{iid}` | 🔐 | Revocar invitación (owner) |
| `GET` | `/api/invitations/{token}` | — | Info pública de invitación |
| `POST` | `/api/invitations/{token}/accept` | 🔐 | Aceptar invitación |
| `GET` | `/api/projects/{id}/notes` | 🔐 | Notas del proyecto |
| `POST` | `/api/projects/{id}/notes` | 🔐 | Crear nota |
| `PUT` | `/api/projects/{id}/notes/{nid}` | 🔐 | Actualizar nota |
| `PATCH` | `/api/projects/{id}/notes/{nid}/position` | 🔐 | Mover nota |
| `DELETE` | `/api/projects/{id}/notes/{nid}` | 🔐 | Eliminar nota |
| `WS` | `/hubs/notes` | 🔐 | SignalR Hub |
