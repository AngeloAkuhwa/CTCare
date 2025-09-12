# CTCare: Sick Leave Management System

CTcare is an internal web application designed to help employees, managers, and HR teams efficiently manage sick leave. It replaces manual email and spreadsheet processes with a single source of truth fully integrated into the Microsoft 365 ecosystem.

---

## Problem We're Solving

- Employees submit sick leave requests via email.
- Managers and HR track leave balances manually using Excel.
- Doctor’s notes are scattered as attachments without centralized management.
- No real-time visibility on balances, approvals, or leave trends.

These limitations cause errors, delays, and lack of transparency in sick leave management.

---

## Our Solution

CTcare offers a streamlined, automated platform:

- **Employee self-service:** Request sick leave, view balances, and upload doctor’s notes.
- **Manager view:** Approve/reject requests, see team calendars, and identify overlaps.
- **HR dashboard:** Monitor requests globally, track balances, generate reports, and configure leave policies.
- **Automation:** Notifications via Email and Teams, calendar sync with Outlook/Teams, secure attachment handling.
- **Reporting:** Insights on usage trends, absenteeism, and entitlement breaches.

---

## Architecture

## Architectural Pattern

**Pattern:** Modular Monolith with Hexagonal Architecture (Ports & Adapters) + CQRS (commands/queries via MediatR) and Domain-Driven Design (DDD) Lite.

### Why this pattern for CTcare (MVP ==> Scale):

- **Fast to ship (single deployable):**  
  One repo, one build, one deploy. Perfect for MVP velocity while keeping code organized by domain (Leave, Employees, Reports).

- **Clear separation of concerns:**

  - **Domain:** Business rules (entitlements, validation, balances)
  - **Application:** Use cases (SubmitLeave, ApproveLeave, reporting queries)
  - **Ports:** Interfaces that define what the app needs (directory, storage, mail/Teams)
  - **Adapters:** Implement those needs (EF Core repos, Azure/Graph, Blob, SMTP/Teams)

- **Integration-friendly:**  
  Hexagonal Ports & Adapters isolate external systems (Azure AD/Graph, SharePoint, email/Teams, storage). Swap or mock them without touching business logic.

- **Scales gracefully:**  
  Start as a modular monolith; if/when a module (e.g., Reports or Notifications) outgrows the core, you can extract that module into its own service with minimal friction because boundaries (ports) already exist.

- **Testable by default:**  
  Domain and Application layers have no framework dependencies → fast unit tests. Adapters are integration-tested separately.

- **Reliable async work:**  
  Outbox pattern + Hangfire for background jobs (notifications, calendar sync) ensures jobs survive restarts and won’t be lost.

- **Read/Write clarity (CQRS):**  
  Commands mutate state, Queries read state. This keeps endpoints simple, makes validation explicit, and sets up a path to future read models (dashboards) if needed.

- **Security & tenancy ready:**  
  Boundaries make it easier to enforce authorization checks (manager vs Employee), data scoping, and auditing.

### Quick Summary

- **Backend:** .NET 8, ASP.NET Core Web API
- **Design Pattern:** Modular Monolith with Hexagonal Architecture (Ports & Adapters) and CQRS-light
- **Database:** PostgreSQL (EF Core ORM)
- **Storage:** Azure Blob Storage (for doctor’s notes and attachments)
- **Identity:** Azure AD (SSO with role-based access control)
- **Integrations:** Microsoft Graph API (Outlook, Teams, SharePoint Directory)
- **Background Jobs:** Hangfire (notifications, calendar sync, directory sync)
- **Reporting:** ClosedXML (Excel exports), QuestPDF (PDF generation)

---

## Project Structure (High-level layout)

```plaintext
src/
├── CTcare.Api             # API host: controllers, authentication, dependency injection
├── CTcare.Application     # Use cases (MediatR), validators, ports/interfaces
├── CTcare.Domain          # Core business entities, policies, domain events
├── CTcare.Infrastructure  # EF Core, repositories, external adapters, background jobs
└── CTcare.Reporting       # Export functionality (Excel, PDF)

tests/
├── CTcare.Domain.Tests
└── CTcare.Application.Tests

```

---

## Setup & Run (Development)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (to run PostgreSQL, Azurite)
- [EF Core CLI Tools](https://learn.microsoft.com/ef/core/cli/dotnet)  
  Install with:

```
dotnet tool install -g dotnet-ef
```

### Clone & Build

```
git clone https://github.com/AngeloAkuhwa/CTCare.git
cd ctcare
dotnet restore
dotnet build
```

### Run Local Dependencies

```
docker compose up -d
```

_(Starts PostgreSQL, Azurite, and optional Redis)_

### Apply Database Migrations

```
dotnet ef migrations add Init --project src/CTcare.Infrastructure --startup-project Src/CTCare.Api
dotnet ef database update --project src/CTcare.Infrastructure --startup-project Src/CTCare.Api
```

### Start API

```
dotnet run --project Src/CTCare.Api
```

### Access Endpoints

- Swagger UI: [https://localhost:5001/swagger](https://localhost:5001/swagger)
- Health Check: [https://localhost:5001/health](https://localhost:5001/health)

---

## Authentication & Roles

- **SSO** via Azure AD (Entra ID)
- **Roles:**
  - _Employee_ — Request and view sick leave
  - _Manager_ — Approve requests, view team calendar
  - _HR_ — Access reports, monitor balances, configure policies
  - _Admin_ — System and infrastructure operations (jobs, audit)

---

## Reporting Examples

- Current employees on sick leave
- Employees who have exceeded their entitlement
- Monthly sick leave usage by department
- Remaining leave balances
- Absenteeism trends over time

_Exports available as Excel, CSV, and PDF formats._

---

## Compliance

- Attachments encrypted in storage for security
- Records retained for a minimum of 2 years (or longer as required)
- Full audit logging for all user and system actions (who, what, when)

---

## Roadmap

- MVP: Employee request → Manager approval → Automatic balance update
- Secure uploads for doctor’s notes
- Advanced HR dashboards with filtering options
- Microsoft Teams and Outlook notifications + calendar integration
- Enhanced reporting and export capabilities
- Directory synchronization (Azure AD + SharePoint)

---

## Contributing Guidelines

- Maintain domain layer purity: no EF Core or third-party libraries here
- Implement new features using Commands and Queries in the Application layer
- Use Ports (interfaces) for external dependencies, implemented in the Infrastructure layer
- Write unit tests for all business logic to ensure reliability

---

## CTcare Process Flow Diagram

```
flowchart TD
Employee[Employee]
Manager[Manager]
Reports[Reports]

Employee -->|Sick Leave Request| Manager
Manager -->|Approve/Reject| Employee
Reports -->|Insights & Exports| HR
```
