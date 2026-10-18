# Mercado Cercano

Base de desarrollo para una tienda ecommerce colombiana con portal de
administración. La aplicación se está construyendo por fases; esta primera base
incluye una API ASP.NET Core, una interfaz React y una comprobación de conexión.

## Requisitos

- .NET SDK 10 LTS
- Node.js 20.19 o posterior y npm

## Ejecutar en desarrollo

En una terminal, desde la raíz del repositorio:

```powershell
dotnet run --project src\MercadoCercano.Api --launch-profile http
```

En otra terminal:

```powershell
Set-Location src\MercadoCercano.Web
npm install
npm run dev
```

Abre la URL que muestre Vite. La interfaz consulta `GET /api/v1/health` a través
del proxy de desarrollo. El proxy usa `http://localhost:5136` por defecto; para
cambiarlo, copia `.env.example` a `src/MercadoCercano.Web/.env.local` y ajusta
`VITE_API_PROXY_TARGET`.

## Verificación

```powershell
dotnet build MercadoCercano.sln
Set-Location src\MercadoCercano.Web
npm run build
npm run lint
```

El documento OpenAPI está disponible en desarrollo en
`/openapi/v1.json` en el puerto de la API.

## Estructura

- `src/MercadoCercano.Api`: ASP.NET Core Web API y configuración del host.
- `src/MercadoCercano.Application`: casos de uso y contratos de aplicación.
- `src/MercadoCercano.Domain`: modelo y reglas de negocio.
- `src/MercadoCercano.Infrastructure`: persistencia e integraciones.
- `src/MercadoCercano.Web`: React, TypeScript y Vite.
- `docs/architecture.md`: límites y dirección de dependencias iniciales.

La base de datos, autenticación, catálogo, pedidos y administración pertenecen a
fases posteriores; esta base no simula esas funcionalidades.
