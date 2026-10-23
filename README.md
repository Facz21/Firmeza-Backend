# Firmeza.Backend — Enterprise Asset, Inventory & Purchase Approval API (.NET 10)

API RESTful empresarial desarrollada en **.NET 10** siguiendo los principios de **Clean Architecture**, **CQRS (MediatR)** y **Domain-Driven Design (DDD)** para la gestión de activos físicos, control de inventario, gobierno de identidades con **ASP.NET Core Identity (RBAC)** y flujo de **Carrito de Compras con Aprobación Administrativa**.

---

## Tabla de Contenidos

1. [Características Principales](#1-características-principales)
2. [Arquitectura y Estructura del Proyecto](#2-arquitectura-y-estructura-del-proyecto)
3. [Stack Tecnológico](#3-stack-tecnológico)
4. [Credenciales de Prueba (Seed Users)](#4-credenciales-de-prueba-seed-users)
5. [Módulos del Sistema](#5-módulos-del-sistema)
6. [Catálogo Completo de Endpoints API](#6-catálogo-completo-de-endpoints-api)
7. [Modelo de Datos (PostgreSQL)](#7-modelo-de-datos-postgresql)
8. [Configuración y Variables de Entorno](#8-configuración-y-variables-de-entorno)
9. [Ejecución con Docker y Local](#9-ejecución-con-docker-y-local)
10. [Pruebas Unitarias](#10-pruebas-unitarias)

---

## 1. Características Principales

- **Clean Architecture & CQRS**: Separación estricta de responsabilidades en cuatro capas (`Domain`, `Application`, `Infrastructure`, `WebApi`) orquestadas con **MediatR** y tuberías de validación con **FluentValidation**.
- **ASP.NET Core Identity Completo & RBAC**:
  - Autenticación mediante **JWT Bearer (`HS256`)** con claims de identidad y roles (`Administrator`, `Auditor`, `StandardUser`).
  - Políticas de seguridad de contraseñas (mínimo 8 caracteres, mayúsculas, minúsculas, dígitos y símbolos).
  - Protección contra fuerza bruta (**Account Lockout**: bloqueo automático por 15 minutos tras 5 intentos fallidos consecutivos).
  - Flujo de recuperación de cuenta mediante tokens criptográficos de Identity (`GeneratePasswordResetTokenAsync` / `ResetPasswordAsync`) con desbloqueo automático.
  - Panel de administración de usuarios: creación de operadores, cambio de rol RBAC, bloqueo/desbloqueo manual, restablecimiento forzado de contraseña y eliminación de cuentas.
- **Gestión de Activos e Inventario**:
  - CRUD transaccional de productos (`SKU`, nombre, especificación técnica, categoría, precio unitario, stock y estado de ciclo de vida).
  - Consulta paginada con filtros dinámicos por término de búsqueda, categoría y estado (`Draft`, `Active`, `OutOfStock`, `Discontinued`).
  - **Exportación a Excel (`.xlsx`)** con formato corporativo mediante **ClosedXML**.
  - **Importación masiva desde Excel (`.xlsx`)** con validación fila por fila y reporte de inserciones/errores.
  - **Certificados Técnicos en PDF** generados dinámicamente con **QuestPDF**.
- **Sistema de Carrito de Compras Aprobado (`Purchase Orders`)**:
  - Flujo de solicitud de compra empresarial **sin pasarela de pago en línea**.
  - Validación de punto de entrega (`DeliveryPoint`), método de pago (`CashOnDelivery`, `BankTransfer`, `CorporateCredit`, `CardOnDelivery`), estado activo del producto y disponibilidad de stock.
  - **Aprobación Administrativa Transaccional**: Al aprobar un carrito, el sistema verifica existencias en tiempo real, **descuenta automáticamente las unidades del inventario**, cambia el estado del producto a `OutOfStock` si llega a `0` y registra al administrador revisor y la fecha de aprobación.
  - **Rechazo Administrativo**: Permite rechazar solicitudes dejando observaciones para el solicitante sin afectar el stock físico.
- **Manejo Centralizado de Excepciones (`ProblemDetails` RFC 7807)**: Middleware global (`ExceptionHandlingMiddleware`) y traductor de códigos de Identity a mensajes claros y amigables en español.

---

## 2. Arquitectura y Estructura del Proyecto

```text
Firmeza.Backend/
├── Dockerfile                                  # Imagen multi-etapa (.NET 10 SDK -> ASP.NET 10 Runtime)
├── EnterpriseSystem.slnx                       # Solución .NET 10
├── src/
│   ├── Core/
│   │   ├── EnterpriseSystem.Domain/            # Capa de Dominio (Cero dependencias externas)
│   │   │   ├── Common/                         # BaseAuditableEntity (CreatedAt, CreatedBy, LastModified*)
│   │   │   ├── Entities/                       # Product, Category, PurchaseOrder, PurchaseOrderItem
│   │   │   ├── Enums/                          # ProductStatus, Roles, PurchaseOrderStatus, PaymentMethod
│   │   │   └── Exceptions/                     # ValidationException, NotFoundException, ForbiddenAccessException
│   │   │
│   │   └── EnterpriseSystem.Application/       # Capa de Aplicación (Casos de Uso, CQRS, Interfaces)
│   │       ├── Common/
│   │       │   ├── Behaviors/                  # ValidationBehavior (Pipeline de MediatR)
│   │       │   ├── Interfaces/                 # IApplicationDbContext, IIdentityService, IExcelService, IPdfService
│   │       │   └── Models/                     # AuthResult, IdentityModels, PaginatedList, Result
│   │       └── Features/
│   │           ├── Auth/                       # LoginCommand, RegisterCommand, GetCurrentUserQuery
│   │           ├── Categories/                 # CreateCategoryCommand, GetCategoriesQuery
│   │           └── Products/                   # Create/Update/Delete Commands, Queries, Excel/PDF Handlers
│   │
│   ├── Infrastructure/
│   │   └── EnterpriseSystem.Infrastructure/    # Capa de Infraestructura (Persistencia, Identity, Archivos)
│   │       ├── Identity/                       # ApplicationUser, ApplicationRole, IdentityService
│   │       ├── Persistence/                    # ApplicationDbContext, Configurations, Migrations, Initialiser
│   │       ├── Services/                       # ExcelService (ClosedXML), PdfService (QuestPDF)
│   │       └── DependencyInjection.cs          # Registro de EF Core PostgreSQL, Identity y Servicios
│   │
│   └── Presentation/
│       └── EnterpriseSystem.WebApi/            # Capa de Presentación (Controladores REST, Middleware, Scalar)
│           ├── Controllers/
│           │   ├── ApiControllerBase.cs        # Ruta base: /api/v1/[controller]
│           │   ├── AuthController.cs           # Login, Registro, Perfil, Cambio y Reset de Contraseña
│           │   ├── UsersController.cs          # Gobernanza RBAC de Usuarios y Roles (Solo Admin)
│           │   ├── ProductsController.cs       # CRUD de Activos, Importación/Exportación Excel y PDF
│           │   ├── CategoriesController.cs     # Catálogo de Categorías
│           │   └── OrdersController.cs         # Solicitudes de Carrito y Aprobación/Rechazo de Compras
│           ├── Middleware/
│           │   └── ExceptionHandlingMiddleware.cs
│           ├── Services/
│           │   └── CurrentUserService.cs       # Extracción de UserId y Email desde Claims JWT
│           ├── appsettings.json
│           └── Program.cs                      # Configuración de Pipeline, CORS, JWT, Scalar y Auto-Seed
│
└── tests/
    └── EnterpriseSystem.UnitTests/             # Pruebas unitarias con xUnit, Moq y FluentAssertions
```

---

## 3. Stack Tecnológico

| Componente | Tecnología | Versión | Propósito |
| :--- | :--- | :--- | :--- |
| **Framework Base** | .NET / ASP.NET Core Web API | `10.0` | Runtime y servidor HTTP de alto rendimiento |
| **ORM & Base de Datos** | Entity Framework Core + Npgsql | `10.0.12` | Persistencia relacional sobre **PostgreSQL 16** |
| **Seguridad e Identidad** | ASP.NET Core Identity + JWT Bearer | `10.0.12` | Gestión de usuarios, roles, bloqueos y tokens JWT |
| **Arquitectura CQRS** | MediatR | `12.4.1` | Desacoplamiento de comandos, consultas y pipelines |
| **Validación** | FluentValidation | `11.11.0` | Validaciones declarativas de reglas de negocio |
| **Reportes Excel** | ClosedXML | `0.104.2` | Generación e importación de libros `.xlsx` |
| **Reportes PDF** | QuestPDF | `2025.1.1` | Generación de certificados técnicos de activos en PDF |
| **Documentación API** | Scalar.AspNetCore + OpenAPI | `2.17.11` | Portal interactivo de documentación en `/scalar/v1` |
| **Testing** | xUnit + Moq + FluentAssertions | `2.9.2` | Suite de pruebas unitarias de dominio y aplicación |

---

## 4. Credenciales de Prueba (Seed Users)

Al iniciar la aplicación, `ApplicationDbContextInitialiser` ejecuta las migraciones y siembra automáticamente las categorías iniciales, 12 activos de hardware empresarial y 3 cuentas operativas:

| Rol (`RBAC`) | Nombre Completo | Correo Electrónico | Contraseña | Permisos en la Plataforma |
| :--- | :--- | :--- | :--- | :--- |
| **`Administrator`** | System Administrator | `admin@enterprise.com` | `Admin@123456` | **Control total**: CRUD de activos, importación/exportación `.xlsx`, reportes PDF, aprobación/rechazo de carritos de compra y administración de usuarios/roles. |
| **`Auditor`** | Compliance Auditor | `auditor@enterprise.com` | `Auditor@123456` | **Auditoría y supervisión**: Consulta de inventario, vista global de órdenes de compra, exportación Excel y descarga de certificados PDF. |
| **`StandardUser`** | Standard Operator | `user@enterprise.com` | `User@123456` | **Operador estándar** (rol por defecto en nuevos registros): Consulta de catálogo, armado y envío de carritos de compra, seguimiento de pedidos y descarga de fichas PDF/Excel. |

---

## 5. Módulos del Sistema

### 5.1. Flujo de Carrito de Compras y Aprobación (`/api/v1/orders`)
1. **Creación de Solicitud (`POST /api/v1/orders`)**:
   - El usuario envía los ítems (`ProductId`, `Quantity`), el **Punto de Entrega (`DeliveryPoint`)**, el **Método de Pago (`PaymentMethod`)** y notas opcionales.
   - El backend verifica que cada producto exista, esté `Active` y cuente con stock suficiente (`StockQuantity >= Quantity`).
   - Se genera la orden con folio único (`ORD-YYYYMMDD-XXXXXX`) en estado `PendingApproval (0)`.
2. **Aprobación (`PUT /api/v1/orders/{id}/approve`)**:
   - Exclusivo para el rol `Administrator`.
   - Verifica nuevamente que todos los productos del carrito tengan stock disponible.
   - **Descuenta automáticamente** las cantidades del `StockQuantity` de cada producto (cambiando el estado del producto a `OutOfStock` si su stock llega a `0`).
   - Actualiza el estado de la orden a `Approved (1)` registrando `ReviewedBy`, `ReviewedAt` y `AdminComments`.
3. **Rechazo (`PUT /api/v1/orders/{id}/reject`)**:
   - Exclusivo para el rol `Administrator`.
   - Cambia el estado de la orden a `Rejected (2)` sin descontar unidades del inventario y guarda las observaciones del administrador.

---

## 6. Catálogo Completo de Endpoints API

Ruta base: `http://localhost:5193/api/v1`  
Documentación interactiva Scalar: `http://localhost:5193/scalar/v1`

### Autenticación y Perfil (`/api/v1/auth`)
| Método | Endpoint | Autorización | Descripción |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/auth/login` | Público | Autentica credenciales, valida bloqueo de cuenta y emite token JWT. |
| `POST` | `/api/v1/auth/register` | Público | Registra un nuevo operador (asigna rol `StandardUser` por defecto). |
| `GET` | `/api/v1/auth/me` | Autenticado | Devuelve la sesión activa del usuario autenticado. |
| `GET` | `/api/v1/auth/profile` | Autenticado | Obtiene el perfil completo de Identity (teléfono, roles, estado de bloqueo). |
| `PUT` | `/api/v1/auth/profile` | Autenticado | Actualiza nombre completo y teléfono de contacto. |
| `POST` | `/api/v1/auth/change-password` | Autenticado | Cambia la contraseña validando la contraseña actual. |
| `POST` | `/api/v1/auth/forgot-password` | Público | Genera un token criptográfico de recuperación de contraseña. |
| `POST` | `/api/v1/auth/reset-password` | Público | Restablece la contraseña con el token y limpia bloqueos de seguridad. |

### Gobernanza de Usuarios y Roles (`/api/v1/users`)
| Método | Endpoint | Autorización | Descripción |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/users` | `Administrator` | Lista todos los usuarios registrados en Identity y su estado de seguridad. |
| `GET` | `/api/v1/users/roles` | `Administrator` | Lista los roles del sistema y el conteo de operadores por rol. |
| `POST` | `/api/v1/users` | `Administrator` | Crea un nuevo usuario asignándole un rol específico. |
| `PUT` | `/api/v1/users/{id}/role` | `Administrator` | Actualiza el rol RBAC de un usuario. |
| `PUT` | `/api/v1/users/{id}/lockout` | `Administrator` | Bloquea o desbloquea manualmente la cuenta de un usuario. |
| `POST` | `/api/v1/users/{id}/reset-password` | `Administrator` | Restablece administrativamente la contraseña de un usuario. |
| `DELETE` | `/api/v1/users/{id}` | `Administrator` | Elimina permanentemente una cuenta de usuario. |

### Productos e Inventario (`/api/v1/products`)
| Método | Endpoint | Autorización | Descripción |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/products` | Autenticado | Lista paginada con filtros (`pageNumber`, `pageSize`, `searchTerm`, `categoryId`, `status`). |
| `GET` | `/api/v1/products/{id}` | Autenticado | Obtiene el detalle técnico completo de un activo por su UUID. |
| `POST` | `/api/v1/products` | `Administrator` | Registra un nuevo activo SKU en el inventario. |
| `PUT` | `/api/v1/products/{id}` | `Administrator` | Actualiza especificaciones, precio, stock o estado de un activo. |
| `DELETE` | `/api/v1/products/{id}` | `Administrator` | Elimina un activo del registro. |
| `GET` | `/api/v1/products/export/excel` | Autenticado | Exporta el inventario filtrado a un archivo Excel (`.xlsx`). |
| `GET` | `/api/v1/products/{id}/export/pdf` | Autenticado | Genera y descarga el certificado técnico en PDF de un activo. |
| `POST` | `/api/v1/products/import/excel` | `Administrator` | Importa activos masivamente desde una hoja de cálculo `.xlsx`. |

### Categorías (`/api/v1/categories`)
| Método | Endpoint | Autorización | Descripción |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/categories` | Autenticado | Lista todas las categorías de activos disponibles. |
| `POST` | `/api/v1/categories` | `Administrator` | Crea una nueva categoría de clasificación. |

### Carrito de Compras y Aprobaciones (`/api/v1/orders`)
| Método | Endpoint | Autorización | Descripción |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/orders` | Autenticado | Envía un carrito de compra con punto de entrega y método de pago para aprobación. |
| `GET` | `/api/v1/orders/my` | Autenticado | Lista el historial y estado de los carritos enviados por el usuario actual. |
| `GET` | `/api/v1/orders` | `Administrator`, `Auditor` | Lista todas las solicitudes de compra de todos los usuarios (filtrable por `status`). |
| `PUT` | `/api/v1/orders/{id}/approve` | `Administrator` | Aprueba la compra, descuenta el stock automáticamente y registra comentarios. |
| `PUT` | `/api/v1/orders/{id}/reject` | `Administrator` | Rechaza la solicitud de compra registrando el motivo. |

---

## 7. Modelo de Datos (PostgreSQL)

Tablas principales en la base de datos `EnterpriseDb`:
- **`AspNetUsers` / `AspNetRoles` / `AspNetUserRoles`**: Esquema completo de ASP.NET Core Identity con extensión `FullName` y `CreatedAt`.
- **`categories`**: Clasificación de activos (`id`, `name`, `description`, campos de auditoría).
- **`products`**: Registro maestro de activos (`id`, `sku` único, `name`, `description`, `price`, `stock_quantity`, `status`, `category_id`, campos de auditoría).
- **`purchase_orders`**: Cabecera de carritos de compra (`id`, `order_number` único, `user_id`, `user_email`, `user_full_name`, `delivery_point`, `payment_method`, `status`, `total_amount`, `notes`, `admin_comments`, `reviewed_by`, `reviewed_at`, campos de auditoría).
- **`purchase_order_items`**: Líneas de detalle de cada carrito (`id`, `purchase_order_id`, `product_id`, `product_sku`, `product_name`, `unit_price`, `quantity`, `line_total`).

---

## 8. Configuración y Variables de Entorno

Archivo [`appsettings.json`](src/Presentation/EnterpriseSystem.WebApi/appsettings.json):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=EnterpriseDb;Username=postgres;Password=postgres;"
  },
  "Jwt": {
    "SecretKey": "EnterpriseSystem_SuperSecretSecureKey_2026_DotNet10_DefaultSigningKey!",
    "Issuer": "EnterpriseSystem",
    "Audience": "EnterpriseSystemClient",
    "ExpiryMinutes": 1440
  }
}
```

---

## 9. Ejecución con Docker y Local

### Opción A: Clonar Ambos Repositorios y Levantar Todo con Docker Compose (DB + Backend + Frontend)
En cualquier computador nuevo con Docker instalado, clona ambos repositorios uno al lado del otro y ejecuta `docker compose` desde `Firmeza.Backend`:

```bash
# 1. Crear carpeta del proyecto y clonar ambos repositorios
mkdir FirmezaProject && cd FirmezaProject
git clone https://github.com/Facz21/Firmeza-Backend.git Firmeza.Backend
git clone https://github.com/Facz21/Firmeza-Fronted.git Firmeza.Frontend

# 2. Entrar al Backend y levantar PostgreSQL + Backend + Frontend con un solo comando
cd Firmeza.Backend
docker compose up --build -d
```

> Si solo clonaste `Firmeza.Backend` y quieres levantar únicamente **PostgreSQL + Backend API** sin el Frontend:
> ```bash
> docker compose up --build -d db backend
> ```

### Opción B: Ejecución Local con .NET CLI
1. Asegúrate de tener una instancia de PostgreSQL activa en `localhost:5432` (`Username=postgres`, `Password=postgres`).
2. Compila y ejecuta el proyecto WebApi:
   ```bash
   dotnet build src/Presentation/EnterpriseSystem.WebApi/EnterpriseSystem.WebApi.csproj
   dotnet run --project src/Presentation/EnterpriseSystem.WebApi/EnterpriseSystem.WebApi.csproj
   ```
3. Abre `http://localhost:5193/scalar/v1` en tu navegador.

---

## 10. Pruebas Unitarias

Para ejecutar la suite de pruebas unitarias (`xUnit`):

```bash
dotnet test tests/EnterpriseSystem.UnitTests/EnterpriseSystem.UnitTests.csproj
```
