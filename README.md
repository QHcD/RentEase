# Property Leasing & Maintenance Platform
## IT8118 Advanced Programming — Brief B — Semester B 2025-2026

---

## System Architecture

```
PropertyLeasing.sln
├── PropertyLeasing.API        ← Web API + EF Core Models + SignalR Hub
├── PropertyLeasing.MVC        ← Main Web App (references API project)
└── PropertyLeasing.Reporting  ← Reporting App (HttpClient only, no DB access)
```

## Demo Credentials

| Role             | Email                        | Password     |
|------------------|------------------------------|--------------|
| Property Manager | manager@propleasing.com      | Manager@123  |
| Tenant           | tenant1@example.com          | Tenant@123   |
| Maintenance Staff| staff1@propleasing.com       | Staff@123    |

---

## Setup Instructions

### Step 1 — Database

Run the SQL scripts in order in SQL Server Management Studio (SSMS):

```
SQL/01_PropertyLeasingDB_Schema.sql   ← Creates all tables
SQL/02_PropertyLeasingDB_SeedData.sql ← Inserts test data
```

The Identity database is auto-created on first run via EF Migrations.

### Step 1b — Fresh demo business data (optional)

The MVC **Applications** tab lists only applications that are still part of the leasing pipeline (Pending, Screening, etc.). Approved applications that already have an active lease appear under **Leases** only.

When you use **local development** (`RentEase.API/appsettings.Development.json`), `Seed:ResetBusinessDataOnStartup` defaults to **true**, so each API start clears business tables (units, applications, leases, maintenance seed rows, etc.) and reapplies the built-in EF seed. Set it to **false** once you want to keep data between runs.

For Azure or shared servers, keep `Seed:ResetBusinessDataOnStartup` **false** in `appsettings.json` unless you intentionally want a one-time wipe.

### Step 2 — Connection Strings

Update `appsettings.json` in both `PropertyLeasing.API` and `PropertyLeasing.MVC`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=PropertyLeasingDB;Trusted_Connection=True;",
  "IdentityConnection": "Server=YOUR_SERVER;Database=PropertyLeasingIdentityDB;Trusted_Connection=True;"
}
```

### Step 3 — Run Order

Run all three projects simultaneously in Visual Studio:
1. Right-click Solution → Properties → Multiple startup projects
2. Set all three to **Start**
3. Make sure `PropertyLeasing.API` runs on port **7001**

Default ports:
- API:       https://localhost:7001
- MVC:       https://localhost:7002
- Reporting: https://localhost:7003

### Step 4 — Verify API base URL

In `PropertyLeasing.MVC/appsettings.json` and `PropertyLeasing.Reporting/appsettings.json`:

```json
"ApiSettings": {
  "BaseUrl": "https://localhost:7001"
}
```

---

## Key Features

### Web API (PropertyLeasing.API)
- JWT Authentication (POST /api/auth/login, /api/auth/register)
- Public Maintenance Lookup endpoint (no auth needed)
- Maintenance CRUD endpoints (JWT protected)
- Report endpoints: occupancy, maintenance, payments, applications
- SignalR Hub at `/hubs/maintenance` for real-time updates
- Swagger UI at `/swagger`

### MVC Application (PropertyLeasing.MVC)
- Role-based access: Property Manager, Tenant, Maintenance Staff
- Browse properties and units
- Submit lease applications → approval workflow → active lease
- Submit & track maintenance requests (with ticket number)
- Public maintenance lookup page (calls API via HttpClient)
- Real-time dashboard with SignalR maintenance board
- Notification system
- Payment tracking

### Reporting Application (PropertyLeasing.Reporting)
- Login via JWT (Property Manager only)
- All data via HttpClient calls to the API (no direct DB access)
- Reports: Occupancy, Maintenance backlog, Payments, Applications

---

## API Endpoints Summary

| Method | Route                              | Auth           | Purpose                        |
|--------|------------------------------------|----------------|--------------------------------|
| POST   | /api/auth/login                    | None           | Login, returns JWT             |
| POST   | /api/auth/register                 | None           | Register as Tenant             |
| GET    | /api/units                         | None           | List available units           |
| GET    | /api/units/{id}                    | None           | Unit details                   |
| GET    | /api/maintenance/lookup            | None ★         | Public ticket lookup           |
| GET    | /api/maintenance                   | JWT (Manager)  | All maintenance requests       |
| POST   | /api/maintenance                   | JWT (Tenant)   | Submit new request             |
| PUT    | /api/maintenance/{id}/status       | JWT (Manager)  | Update request status          |
| GET    | /api/reports/occupancy             | JWT (Manager)  | Occupancy report               |
| GET    | /api/reports/maintenance           | JWT (Manager)  | Maintenance report             |
| GET    | /api/reports/payments              | JWT (Manager)  | Payment report                 |
| GET    | /api/reports/applications          | JWT (Manager)  | Applications report            |

★ Used by MVC Public Lookup page via HttpClient

---

## SignalR

Hub URL: `/hubs/maintenance`

Events:
- `NewRequest` — fired when a tenant submits a new maintenance request
- `StatusUpdated` — fired when a manager/staff updates request status

The Dashboard page connects to SignalR and updates the maintenance board in real time.

---

## NuGet Packages

| Package                                           | Purpose                    |
|---------------------------------------------------|----------------------------|
| Microsoft.AspNetCore.Authentication.JwtBearer     | JWT auth for Web API       |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | ASP.NET Identity           |
| Microsoft.EntityFrameworkCore.SqlServer           | EF Core SQL Server         |
| Microsoft.AspNetCore.SignalR                      | Real-time SignalR          |
| Swashbuckle.AspNetCore                            | Swagger UI                 |
| System.IdentityModel.Tokens.Jwt                   | JWT token generation       |
| Microsoft.AspNetCore.SignalR.Client               | SignalR client (MVC)       |
| Microsoft.AspNetCore.Authentication.Cookies       | Cookie auth (Reporting)    |
