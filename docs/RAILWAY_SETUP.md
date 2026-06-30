# 🚂 Deploy en Railway — Guía Visual

Guía paso a paso con capturas para desplegar OpenStickyMemos en Railway.

---

## 1. Fork el repositorio

![Fork button](https://docs.github.com/assets/cb-34362/images/help/repository/fork_button.png)

1. Ve a [github.com/Archebiz/OpenStickyMemos](https://github.com/Archebiz/OpenStickyMemos)
2. Click **Fork** → **Create fork**

---

## 2. Crear proyecto en Railway

1. Ve a [Railway.app](https://railway.app) y haz login
2. Click **New Project** → **Deploy from GitHub repo**
3. Selecciona tu fork → en **Settings → Root Directory** pon `src/backend`
4. Railway usará el `Dockerfile` para buildcar .NET 10
5. Luego crea un segundo servicio: **New** → **Service** → **Deploy from GitHub repo**
6. Selecciona el mismo fork y en **Settings → Root Directory** pon `src/web/open-sticky-memos`

---

## 3. Agregar PostgreSQL

1. Click en **New** dentro del proyecto
2. Selecciona **Database** → **PostgreSQL**
3. Railway lo provisiona automáticamente
4. La variable `DATABASE_URL` se inyecta sola

---

## 4. Configurar variables de entorno

Navega a tu proyecto → **Variables** y agrega:

```
JWT__Key = "tu-clave-secreta-muy-larga-cambiame!"
JWT__Issuer = "OpenStickyMemos"
JWT__Audience = "OpenStickyMemos"
ASPNETCORE_ENVIRONMENT = "Production"
```

> Railway usa `__` (doble guion bajo) para secciones anidadas.

---

## 5. Deploy automático

Railway buildca automáticamente con Nixpacks.  
No se necesita configuración adicional.

- ✅ Detecta .NET 10 automáticamente
- ✅ Detecta Node.js (Angular) automáticamente
- ✅ Las migraciones EF Core se ejecutan al iniciar
- ✅ HTTPS gestionado por Railway

---

## 6. Verificar

```bash
# Health check de tu API
curl https://tuproyecto.up.railway.app/api/health

# Respuesta esperada:
# {"status":"healthy","timestamp":"...","version":"1.0.0"}
```

---

## Recursos útiles

- [Railway Documentation](https://docs.railway.app)
- [Railway .NET Guide](https://docs.railway.app/guides/dotnet)
- [Railway Node.js Guide](https://docs.railway.app/guides/nodejs)
