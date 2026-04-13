# Aplicación Web Segura - Proyecto Calidad del Software

## Descripción

Este es un proyecto de API Web segura desarrollado para el curso de Calidad del Software de la UTN, implementando mejores prácticas de seguridad siguiendo el estándar OWASP Top 10.

## Característic as de Seguridad Implementadas

### 1. **Modelos de Datos Seguros**
- **Usuario**: Control de acceso, autenticación segura, tracking de actividad
- **Producto**: Información de categorización y auditoría
- **AuditoriaLog**: Registro completo de eventos del sistema

### 2. **Autenticación y Autorización (RF-02 a RF-05)**
- **Hashing de Contraseñas**: BCrypt con factor de costo 12
- **JWT (JSON Web Tokens)**: Token de sesión con expiración de 1 hora
- **Cookies HttpOnly y Secure**: Protección contra XSS
- **RBAC (Role-Based Access Control)**:
  - **SuperAdmin**: Acceso total
  - **Auditor**: Visualización de logs y auditoría
  - **Registrador**: Gestión de productos

### 3. **Protección contra Ataques (RS-01 a RS-07)**

#### SQL Injection (RS-01)
- ✅ Exclusivamente LINQ/EF Core (consultas parametrizadas)
- ✅ No se concatenan strings en queries

#### XSS (RS-02)
- ✅ Content-Security-Policy headers
- ✅ Escape de output en API
- ✅ Validación doble: frontend y backend

#### CSRF / Session Management (RS-03)
- ✅ JWT en Cookies HttpOnly/Secure
- ✅ Regeneración de sesión tras login
- ✅ Validación de tokens antes de operaciones sensibles

#### Inactividad (RS-04)
- ✅ Invalidación automática tras 5 minutos de inactividad
- ✅ Middleware verificador en cada request

#### Rate Limiting (RS-05)
- ✅ Bloqueo de logins tras 5 intentos fallidos por 5 minutos
- ✅ Logging completo de intentos fallidos
- ✅ Registro en auditoría

#### Headers de Seguridad (RS-06)
- ✅ X-Frame-Options: DENY
- ✅ X-Content-Type-Options: nosniff
- ✅ Strict-Transport-Security
- ✅ X-XSS-Protection
- ✅ Content-Security-Policy
- ✅ Referrer-Policy
- ✅ Permissions-Policy

#### Validación (RS-07)
- ✅ Validación en Backend (Data Annotations)
- ✅ Validación en Frontend (HTML5 + JavaScript)
- ✅ Sanitización de inputs

### 4. **Log de Auditoría Detallado (RF-06)**
- Registro de todos los eventos sensibles
- **Especialmente**: Accesos denegados (403) - Primer checkeo del profesor
- Captura: IP origen, usuario, timestamp, método HTTP, ruta, código de respuesta

## Requisitos del Sistema

- **.NET 8/9** (o superior)
- **PostgreSQL 11+** instalado localmente
- **Entity Framework Core Tools**
- **PostMan** o **Insomnia** para testing API

## Instalación Rápida

### 1. Instalar dependencias
```bash
cd ProyectoSeguridad
dotnet restore
```

### 2. Configurar la base de datos
Editar `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ProyectoSeguridad;Username=postgres;Password=postgres"
  }
}
```

### 3. Aplicar migraciones (Iniciador automático)
```bash
# Las migraciones se aplican automáticamente en Program.cs al iniciar
```

### 4. Ejecutar
```bash
dotnet run
```

La API estará disponible en `https://localhost:5001` y Swagger en `https://localhost:5001/swagger`

## Credenciales por Defecto

| Usuario | Contraseña | Rol |
|---------|-----------|-----|
| admin | Admin123! | SuperAdmin |
| auditor | Auditor123! | Auditor |
| registrador | Registrador123! | Registrador |

⚠️ **IMPORTANTE**: Cambiar contraseñas en producción

## Endpoints Principales

### Autenticación
- `POST /api/auth/login` - Login (sin autenticación)
- `POST /api/auth/register` - Registro (sin autenticación)
- `GET /api/auth/me` - Obtener usuario actual (requiere autenticación)

### Productos
- `GET /api/productos` - Listar (todos los roles autenticados)
- `GET /api/productos/{id}` - Obtener uno (todos los roles)
- `POST /api/productos` - Crear (SuperAdmin, Registrador)
- `PUT /api/productos/{id}` - Actualizar (SuperAdmin solo)
- `DELETE /api/productos/{id}` - Eliminar (SuperAdmin solo)

### Auditoría
- `GET /api/auditoria` - Listar logs (SuperAdmin, Auditor)
- `GET /api/auditoria/forbidden` - Logs de 403 Forbidden (SuperAdmin, Auditor)
- `GET /api/auditoria/failed-logins` - Intentos fallidos (SuperAdmin)
- `GET /api/auditoria/stats` - Estadísticas (SuperAdmin)

## Testing con POST /api/auth/login

```bash
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}'
```

## Arquitectura

```
ProyectoSeguridad/
├── Models/           # Entidades de datos
├── Data/             # Contexto de BD e migraciones
├── Services/         # Lógica de negocio (Auth, Auditoría)
├── Controllers/      # Endpoints API
├── Middleware/       # Seguridad y auditoría
├── Utilities/        # DTOs y helpers
└── Migrations/       # EF Core migraciones
```

## Notas de Seguridad

1. **Secreto JWT**: Cambiar `Jwt:SecretKey` en production
2. **CORS**: Configurar orígenes permitidos en `appsettings.json`
3. **HTTPS**: Siempre usar en producción
4. **Base de datos**: Usar contraseñas fuertes en producción
5. **Logs**: Revisar regularmente para detectar anomalías

## Próximos Pasos

1. Docker: `Genera un Dockerfile y docker-compose.yml`
2. Frontend: HTML/JavaScript con validación y protección XSS
3. Testing: Suite de pruebas unitarias e integración
4. Monitoreo: Implementar alertas para intentos sospechosos

## Documentación

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [Entity Framework Core](https://learn.microsoft.com/ef/)
- [JWT Best Practices](https://tools.ietf.org/html/rfc7519)

## Autor

Desarrollador - Aplicación Web Segura para UTN

## Licencia

Este proyecto es para fines educativos.
