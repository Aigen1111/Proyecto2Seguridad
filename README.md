# ProyectoSeguridad — ISW-1013 Calidad del Software

Aplicación web segura desarrollada en .NET 10 + PostgreSQL, implementando controles de seguridad según OWASP Top 10.

---

## Requisitos

- Docker Desktop instalado y corriendo

---

## Levantar el proyecto

Desde la carpeta raíz (donde está `docker-compose.yml`):

```bash
docker-compose up --build
```

La aplicación quedará disponible en `http://localhost:8080`.

Para detener:

```bash
docker-compose down
```

Para detener y borrar la base de datos completamente:

```bash
docker-compose down -v
```

---

## Credenciales por defecto

| Usuario | Contraseña | Rol |
|---------|------------|-----|
| admin | Admin123! | SuperAdmin |
| auditor | Auditor123! | Auditor |
| registrador | Registrador123! | Registrador |

---

## Documentación de la API

Todos los endpoints protegidos requieren autenticación JWT. El token se obtiene del endpoint de login y se envía como cookie HttpOnly automáticamente, o como header `Authorization: Bearer <token>`.

### Autenticación

**POST /api/auth/login**
No requiere autenticación. Devuelve un JWT válido por 1 hora si las credenciales son correctas. Implementa bloqueo de cuenta tras 5 intentos fallidos.
```json
// Request
{ "username": "admin", "password": "Admin123!" } 

// Response 200
{ "success": true, "token": "eyJ...", "message": "Login exitoso" }

// Response 401 — credenciales inválidas o cuenta bloqueada
{ "success": false, "token": null, "message": "Usuario o contraseña inválidos" }
```

**POST /api/auth/register**
No requiere autenticación. Crea usuario con rol Registrador.
```json
// Request
{ "username": "nuevo", "email": "nuevo@mail.com", "password": "Pass123!" }
```

**GET /api/auth/me**
Requiere autenticación. Devuelve el ID del usuario autenticado.

---

### Productos

Todos los endpoints requieren autenticación.

| Método | Ruta | Roles permitidos | Descripción |
|--------|------|-----------------|-------------|
| GET | /api/productos | Todos | Lista todos los productos |
| GET | /api/productos/{id} | Todos | Obtiene un producto por ID |
| POST | /api/productos | SuperAdmin, Registrador | Crea un producto |
| PUT | /api/productos/{id} | SuperAdmin, Registrador | Actualiza un producto |
| DELETE | /api/productos/{id} | SuperAdmin, Registrador | Elimina un producto |

```json
// Body para POST y PUT
{
  "codigo": "PRD001",
  "nombre": "Nombre del producto",
  "descripcion": "Descripción del producto",
  "precio": 9.99,
  "cantidad": 10
}
```

El campo `codigo` acepta solo letras mayúsculas y números, máximo 20 caracteres.

---

### Usuarios

Todos los endpoints requieren autenticación.

| Método | Ruta | Roles permitidos | Descripción |
|--------|------|-----------------|-------------|
| GET | /api/usuarios | Todos | Lista todos los usuarios |
| GET | /api/usuarios/{id} | SuperAdmin | Obtiene un usuario por ID |
| GET | /api/usuarios/roles | SuperAdmin | Lista los roles disponibles |
| POST | /api/usuarios | SuperAdmin | Crea un usuario |
| PUT | /api/usuarios/{id} | SuperAdmin | Actualiza un usuario |
| DELETE | /api/usuarios/{id} | SuperAdmin | Elimina un usuario |

```json
// Body para POST
{
  "username": "usuario",
  "email": "usuario@mail.com",
  "password": "Pass123!",
  "rolId": 1
}

// Body para PUT (sin contraseña)
{
  "username": "usuario",
  "email": "usuario@mail.com",
  "rolId": 2
}
```

Roles disponibles: 1 = SuperAdmin, 2 = Auditor, 3 = Registrador.

---

### Auditoría

| Método | Ruta | Roles permitidos | Descripción |
|--------|------|-----------------|-------------|
| GET | /api/auditoria | SuperAdmin | Lista logs de los últimos 7 días |
| GET | /api/auditoria/forbidden | SuperAdmin | Logs de accesos denegados (403) |
| GET | /api/auditoria/failed-logins | SuperAdmin | Intentos de login fallidos |
| GET | /api/auditoria/stats | SuperAdmin | Estadísticas generales |

---

## Arquitectura

ProyectoSeguridad/
├── Controllers/    # Endpoints REST
├── Data/           # DbContext y configuración EF Core
├── Middleware/     # Auditoría, inactividad, headers de seguridad
├── Migrations/     # Migraciones de base de datos
├── Models/         # Entidades
├── Services/       # Lógica de negocio (Auth, Auditoría)
└── Utilities/      # DTOs

---

## Controles de seguridad implementados

- Hashing de contraseñas con BCrypt (factor de costo 12)
- JWT con expiración de 1 hora, almacenado en cookie HttpOnly
- RBAC validado en el backend en cada request
- Protección contra SQL Injection mediante EF Core / LINQ
- Escape de output en el frontend contra XSS
- Headers de seguridad HTTP (CSP, X-Frame-Options, HSTS, etc.)
- Rate limiting: bloqueo de 5 minutos tras 5 intentos fallidos de login
- Invalidación de sesión por inactividad de 5 minutos
- Log de auditoría de todos los eventos sensibles con IP y timestamp

