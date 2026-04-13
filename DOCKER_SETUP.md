# Configuración y Ejecución con Docker

Este documento explica cómo ejecutar el proyecto completo (API + PostgreSQL + pgAdmin) usando Docker Compose.

## Prerequisites

- Docker Desktop instalado: https://www.docker.com/products/docker-desktop
- Docker Compose (incluido con Docker Desktop)

## Inicio Rápido

### 1. **Levantar los servicios**

```bash
cd C:\Users\Usuario\Desktop\Proyecto2Calidadd
docker-compose up -d
```

Esto levantará:
- ✅ **PostgreSQL** (puerto 5432) - Base de datos
- ✅ **pgAdmin** (puerto 5050) - Gestor de BD
- ✅ **API .NET** (puerto 5000/5001) - Tu aplicación

### 2. **Verificar que todo está corriendo**

```bash
docker-compose ps
```

Deberías ver algo como:

```
NAME                          STATUS
proyecto_seguridad_db         Up (healthy)
proyecto_seguridad_pgadmin    Up
proyecto_seguridad_api        Up
```

### 3. **Acceder a los servicios**

| Servicio | URL | Credenciales |
|----------|-----|--------------|
| **API Swagger** | http://localhost:5000/swagger | N/A |
| **Health Check** | http://localhost:5000/health | N/A |
| **pgAdmin** | http://localhost:5050 | admin@seguridad.local / admin123 |

### 4. **Conectarse a PostgreSQL desde pgAdmin**

1. Abre http://localhost:5050
2. Login con:
   - **Email**: admin@seguridad.local
   - **Contraseña**: admin123
3. Click en "Add New Server":
   - **Name**: ProyectoSeguridad (cualquier nombre)
   - **Connection tab**:
     - **Host name/address**: postgres
     - **Port**: 5432
     - **Username**: postgres
     - **Password**: postgres
     - **Save password**: ✓

## Detener los servicios

```bash
docker-compose down
```

Para eliminar también los volúmenes (limpieza completa):

```bash
docker-compose down -v
```

## Ver logs

```bash
# Logs de todo
docker-compose logs -f

# Solo de la API
docker-compose logs -f api

# Solo de PostgreSQL
docker-compose logs -f postgres
```

## Testing de la API

### Login (sin autenticación)

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Admin123!"}'
```

Respuesta esperada:

```json
{
  "success": true,
  "token": "eyJhbGc...",
  "message": "Login exitoso"
}
```

### Obtener productos (con JWT)

```bash
curl -X GET http://localhost:5000/api/productos \
  -H "Authorization: Bearer <tu_token_aqui>"
```

## Problemas Comunes

### Error: "Cannot connect to Docker daemon"
**Solución**: Asegúrate que Docker Desktop está corriendo

### Error: "Port 5432 already in use"
**Solución**: 
```bash
# Ver qué está usando el puerto
netstat -ano | findstr :5432

# O usa otro puerto en docker-compose.yml:
ports:
  - "5433:5432"  # Host:Container
```

### Error: "Connection refused" en la API
**Solución**: Espera a que PostgreSQL esté listo (healthcheck 10-15 segundos)

```bash
docker-compose logs -f postgres
```

### Las migraciones no se aplican
**Solución**: Verifica los logs de la API:

```bash
docker-compose logs -f api
```

## Configuración de Producción

Para ambiente de producción, usa `docker-compose.prod.yml`:

```bash
docker-compose -f docker-compose.prod.yml up -d
```

Variables de entorno en `.env`:
- `DB_PASSWORD` - Cambiar contraseña de PostgreSQL
- `JWT_SECRET_KEY` - Secreto seguro

## Volúmenes de Datos

Los datos persisten en:
- `postgres_data/` - Base de datos PostgreSQL
- `pgadmin_data/` - Configuración de pgAdmin

Para resetear completamente:

```bash
docker-compose down -v
docker-compose up -d
```

## Next Steps

1. ✅ Levantar Docker Compose
2. ✅ Verificar que PostgreSQL está running y sano
3. ✅ Acceder a Swagger en http://localhost:5000/swagger
4. ✅ Testear login endpoint
5. ✅ Revisar logs en pgAdmin

¡Tu aplicación segura está lista!
