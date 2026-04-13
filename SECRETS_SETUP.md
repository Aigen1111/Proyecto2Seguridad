# Guía de Configuración por Ambiente

## Desarrollo Local

**Archivo**: `appsettings.Development.json` (NO subir a Git)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ProyectoSeguridad;Username=postgres;Password=TU_PASSWORD"
  },
  "Jwt": {
    "SecretKey": "dev-secret-key-cambiar-en-produccion"
  }
}
```

## Producción (Docker)

**Usa variables de entorno**:

```bash
export DB_HOST=postgres
export DB_USER=postgres
export DB_PASSWORD=contraseña_fuerte
export JWT_SECRET_KEY=clave_segura_256bits_minimo
```

O en docker-compose.yml:

```yaml
environment:
  ConnectionStrings__DefaultConnection: "Host=${DB_HOST};Port=5432;Database=ProyectoSeguridad;Username=${DB_USER};Password=${DB_PASSWORD}"
  Jwt__SecretKey: "${JWT_SECRET_KEY}"
```

## ⚠️ Seguridad

✅ **NUNCA** commits secrets en el código
✅ Usa `appsettings.Development.json` localmente (en .gitignore)
✅ Usa variables de entorno en producción
✅ Cambia `JWT_SECRET_KEY` - mínimo 256 bits
✅ Usa contraseñas fuertes en BD
