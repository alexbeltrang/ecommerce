# Arquitectura inicial

La solución se divide en una API ASP.NET Core y una interfaz React, con
proyectos separados para el dominio, la aplicación y la infraestructura. La
separación establece límites para crecer sin introducir microservicios ni
dependencias prematuras.

## Dirección de dependencias

- `Domain` no depende de otros proyectos de la solución.
- `Application` depende de `Domain`.
- `Infrastructure` depende de `Application` y `Domain`.
- `Api` compone la aplicación y la infraestructura.
- `Web` consume la API REST; en desarrollo usa el proxy de Vite.

## Estado de la base

`GET /api/v1/health` comprueba que la API responde. OpenAPI se expone únicamente
en desarrollo. Aún no hay persistencia, autenticación, endpoints comerciales ni
datos semilla; se incorporarán en sus fases correspondientes.
