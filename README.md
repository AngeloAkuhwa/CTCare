# CTCare: Sick Leave Management System

CTcare is an internal web application designed to help employees, managers, and People's team efficiently manage sick leave. It replaces manual email and spreadsheet processes with a single source of truth fully integrated into the Microsoft 365 ecosystem.

---

# The CTCare Team:

-   Olamilekan Abolade
-   Mimidoo Ucheagwu
-   Dorathy Osuman
-   Prosper Ikechukwu
-   Angelo Akuhwa

## Problem We're Solving

-   Employees submit sick leave requests via email.
-   Managers and People's team track leave balances manually using Excel.
-   Doctor’s notes are scattered as attachments without centralized management.
-   No real-time visibility on balances, approvals, or leave trends.

These limitations cause errors, delays, and lack of transparency in sick leave management.

---

## Our Solution

CTcare offers a streamlined, automated platform:

-   **Employee self-service:** Request sick leave, view balances, and upload doctor’s notes.
-   **Manager view:** Approve/reject requests, see team calendars, and identify overlaps.
-   **People's team dashboard:** Monitor requests globally, track balances, generate reports, and configure leave policies.
-   **Automation:** Notifications via Email and Teams, calendar sync with Outlook/Teams, secure attachment handling.
-   **Reporting:** Insights on usage trends, absenteeism, and entitlement breaches.

---

## Architecture

## Architectural Pattern

**Pattern:** Modular Monolith with Hexagonal Architecture (Ports & Adapters) + CQRS (commands/queries via MediatR) and Domain-Driven Design (DDD) Lite.

### Why this pattern for CTcare (MVP ==> Scale):

-   **Fast to ship (single deployable):**  
    One repo, one build, one deploy. Perfect for MVP velocity while keeping code organized by domain (Leave, Employees, Reports).

-   **Clear separation of concerns:**

    -   **Domain:** Business rules (entitlements, validation, balances)
    -   **Application:** Use cases (SubmitLeave, ApproveLeave, reporting queries)
    -   **Ports:** Interfaces that define what the app needs (directory, storage, mail/Teams)
    -   **Adapters:** Implement those needs (EF Core repos, Azure/Graph, Blob, SMTP/Teams)

-   **Integration-friendly:**  
    Hexagonal Ports & Adapters isolate external systems (Azure AD/Graph, SharePoint, email/Teams, storage). Swap or mock them without touching business logic.

-   **Scales gracefully:**  
    Start as a modular monolith; if/when a module (e.g., Reports or Notifications) outgrows the core, you can extract that module into its own service with minimal friction because boundaries (ports) already exist.

-   **Testable by default:**  
    Domain and Application layers have no framework dependencies → fast unit tests. Adapters are integration-tested separately.

-   **Reliable async work:**  
    Outbox pattern + Hangfire for background jobs (notifications, calendar sync) ensures jobs survive restarts and won’t be lost.

-   **Read/Write clarity (CQRS):**  
    Commands mutate state, Queries read state. This keeps endpoints simple, makes validation explicit, and sets up a path to future read models (dashboards) if needed.

-   **Security & tenancy ready:**  
    Boundaries make it easier to enforce authorization checks (manager vs Employee), data scoping, and auditing.

### Quick Summary

-   **Backend:** .NET 8, ASP.NET Core Web API
-   **Design Pattern:** Modular Monolith with Hexagonal Architecture (Ports & Adapters) and CQRS-light
-   **Database:** PostgreSQL (EF Core ORM)
-   **Storage:** Cloudinary storage Storage (for doctor’s notes and attachments)
-   **Identity:** Azure AD (SSO with role-based access control) would be switched in later
-   **Integrations:** Microsoft Graph API (Outlook, Teams, SharePoint Directory)
-   **Background Jobs:** Hangfire (notifications, calendar sync, directory sync)
-   **Reporting:** ClosedXML (Excel exports), QuestPDF (PDF generation)

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

-   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
-   [Docker Desktop](https://www.docker.com/products/docker-desktop) (to run PostgreSQL, Azurite)
-   [EF Core CLI Tools](https://learn.microsoft.com/ef/core/cli/dotnet)  
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

-   Swagger UI: [https://localhost:5001/swagger](https://localhost:5001/swagger)
-   Health Check: [https://localhost:5001/health](https://localhost:5001/health)
-   Hangfire Dashboard Access: [https://localhost:5001/hangfire](https://localhost:5001/hangfire)

---

## Authentication & Roles

-   **SSO** via Azure AD (Entra ID) => Undone (username, password auth available atm)
-   **Roles:**
    -   _Employee_ — Request and view sick leave
    -   _Manager_ — Approve requests, view team calendar
    -   _HR_ — Access reports, monitor balances, configure policies
    -   _Admin_ — System and infrastructure operations (jobs, audit)

---

## Reporting Examples

-   Current employees on sick leave
-   Employees who have exceeded their entitlement
-   Monthly sick leave usage by department
-   Remaining leave balances
-   Absenteeism trends over time

_Exports available as Excel, CSV, and PDF formats._

---

## Compliance

-   Attachments encrypted in storage for security
-   Records retained for a minimum of 2 years (or longer as required)
-   Full audit logging for all user and system actions (who, what, when)

---

## Roadmap

-   MVP: Employee request → Manager approval → Automatic balance update
-   Secure uploads for doctor’s notes
-   Advanced People's team dashboards with filtering options
-   Microsoft Teams and Outlook notifications + calendar integration
-   Enhanced reporting and export capabilities
-   Directory synchronization (Azure AD + SharePoint)

---

## Contributing Guidelines

-   Maintain domain layer purity: no EF Core or third-party libraries here
-   Implement new features using Commands and Queries in the Application layer
-   Use Ports (interfaces) for external dependencies, implemented in the Infrastructure layer
-   Write unit tests for all business logic to ensure reliability

---

## CTcare Process Flow Diagram

```
flowchart TD
Employee[Employee]
Manager[Manager]
Reports[Reports]

Employee -->|Sick Leave Request| Manager
Manager -->|Approve/Reject| Employee
Reports -->|Insights & Exports| People's team
```

## Planned Enhancements & Deferred Items (Post-MVP)

This release focuses on the core leave flow. Below are items intentionally deferred for a complete, production-grade solution.

---

### Identity & Access

-   Switch to SSO (Azure AD / Entra ID) for all users; retire local passwords.

-   Role/Policy hardening: granular scopes (PeopleTeam.Read, PeopleTeam.Write, Approvals.Manage, Reports.Export).

-   Just-in-time role sync from Azure AD groups (nightly + on login).

-   Session management: device list, remote revoke, idle timeout, and refresh-token rotation improvement.

### Notifications (Email, SMS, Teams)

-   Unified notification service with templates (Razor) and per-channel fallbacks.

-   Teams adaptive cards for approve/return actions inline.

-   SMS (Twilio/Your provider) for critical events (returns, escalations).

-   Digest emails (daily/weekly summaries to managers/People's team).

-   Bounce/Undeliverable processing & retry with dead-letter queue.

### Background Jobs (Hangfire)

-   Already present: Annual entitlement provisioner.

## To add:

-   Balance reconciliation (detect drift between balances and requests).

-   Directory sync (employees, departments, managers from Entra ID).

-   Calendar sync (push approved leave to Outlook calendars).

-   Overdue doctor’s note reminder (and auto-return/auto-cancel after grace period).

-   Pending-approval escalation (SLA timers => escalate to next manager/People's team).

-   Stale submitted auto-cancel (configurable window).

-   Attachment antivirus scan (e.g., ClamAV) & quarantine workflow.

-   Cloudinary clean-up (orphaned files, retention rules).

-   Cache warmers for high-traffic dashboards.

-   Report schedulers (monthly absenteeism/department usage exports).

-   Dead-letter reprocessor for failed jobs.

### Product UI (Next)

-   Manager approval screen: team calendar view, clash highlighters, bulk actions, inline returns with comments.

-   People's team dashboards: multi-filter reports, export to Excel/PDF.

-   Admin console: leave rules, entitlement policies, public holidays, content templates, feature flags.

### API & Platform Hardening

-   PATCH semantics for partial updates; consistent ETag/RowVersion usage for optimistic concurrency.

-   Search/filter/paging standardization across list endpoints.

-   Outbox pattern for reliable eventing to jobs/integrations.

-   Idempotency keys for POSTs to avoid duplicates on retries.

-   Rate-limit & API key rotation policies; secrets management across environments.

### Data & Ops

-   Seeders & provisioning flows for new employees/teams/leave types.

-   Backfill tools to import legacy balances/requests.

### Testing & Quality

-   Integration tests (EF Core, Cloudinary, mail, Teams via test doubles).

-   Contract tests for public APIs (OpenAPI + smoke).

-   Load & soak testing for peak months.

-   UI e2e (Playwright/Cypress) for critical flows.
