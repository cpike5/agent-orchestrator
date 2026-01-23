# APMAS Test Projects

Test projects for validating the APMAS orchestration system. Projects range from simple CLI apps to Blazor applications with SQLite backends. Each is scoped to be complex enough for meaningful stress testing while staying within reasonable token limits.

---

## 1. Task Timer CLI

**Complexity:** Low
**Type:** Console Application
**Agents:** 2-3 (Architect, Developer, Tester)

A simple Pomodoro-style timer with session tracking.

**Features:**
- Start/stop/pause timer via commands
- Configurable work/break durations
- Session history saved to JSON file
- Daily/weekly summary statistics

**Why it's good for testing:**
- Minimal dependencies
- Clear separation of concerns (timer logic, persistence, UI)
- Fast iteration cycles

---

## 2. Markdown Note Organizer

**Complexity:** Low-Medium
**Type:** Console Application
**Agents:** 3 (Architect, Developer, Tester)

CLI tool for managing a folder of markdown notes.

**Features:**
- Create/edit/delete notes
- Tag-based organization
- Full-text search across notes
- Export to single HTML file
- SQLite index for fast searching

**Why it's good for testing:**
- File I/O + database operations
- Search indexing logic
- Multiple output formats

---

## 3. Expense Tracker API

**Complexity:** Medium
**Type:** ASP.NET Core Minimal API
**Agents:** 3-4 (Architect, Backend Dev, Tester, maybe Frontend)

REST API for tracking personal expenses.

**Features:**
- CRUD for expenses and categories
- Monthly budget limits with alerts
- SQLite persistence with EF Core
- Basic JWT authentication
- OpenAPI/Swagger documentation

**Why it's good for testing:**
- Standard web API patterns
- Database migrations
- Authentication flow
- API documentation generation

---

## 4. Link Shortener Service

**Complexity:** Medium
**Type:** ASP.NET Core + Razor Pages
**Agents:** 3-4 (Architect, Backend Dev, Frontend Dev, Tester)

URL shortening service with click analytics.

**Features:**
- Generate short codes for URLs
- Redirect with click tracking
- Simple dashboard showing click stats
- Optional expiration dates
- SQLite backend

**Why it's good for testing:**
- Mixed API + UI concerns
- Analytics/aggregation queries
- Redirect handling

---

## 5. Recipe Book

**Complexity:** Medium
**Type:** Blazor Server
**Agents:** 4 (Architect, Backend Dev, Frontend Dev, Tester)

Personal recipe management application.

**Features:**
- Add/edit/delete recipes with ingredients
- Category and tag organization
- Ingredient-based search
- Shopping list generator from selected recipes
- SQLite with EF Core

**Why it's good for testing:**
- Blazor Server component patterns
- Related entity management (recipes ↔ ingredients)
- List generation logic

---

## 6. Habit Tracker

**Complexity:** Medium
**Type:** Blazor Server
**Agents:** 4 (Architect, Backend Dev, Frontend Dev, Tester)

Daily habit tracking with streak visualization.

**Features:**
- Define habits with frequency goals
- Daily check-in interface
- Streak tracking and calendar heatmap
- Weekly/monthly progress charts
- SQLite persistence

**Why it's good for testing:**
- Date-based logic and calculations
- Data visualization components
- Recurring data patterns

---

## 7. Inventory Manager

**Complexity:** Medium-High
**Type:** Blazor Server + Background Service
**Agents:** 4-5 (Architect, Backend Dev, Frontend Dev, Tester, DevOps)

Small business inventory tracking system.

**Features:**
- Products with categories and locations
- Stock level tracking with low-stock alerts
- Simple barcode lookup (manual entry)
- Transaction history (in/out)
- Background service for alert checking
- SQLite backend

**Why it's good for testing:**
- Background service coordination
- Alert/notification patterns
- Transaction logging

---

## 8. Event Scheduler

**Complexity:** Medium-High
**Type:** Blazor WASM + Minimal API
**Agents:** 4-5 (Architect, Backend Dev, Frontend Dev, Tester)

Personal event and appointment scheduler.

**Features:**
- Calendar view (month/week/day)
- Recurring event support
- Reminder notifications (in-app)
- iCal export
- SQLite API backend

**Why it's good for testing:**
- Blazor WASM + API separation
- Complex date/recurrence logic
- Calendar UI components

---

## 9. Reading List Tracker

**Complexity:** Medium
**Type:** Blazor WASM + Minimal API
**Agents:** 4 (Architect, Backend Dev, Frontend Dev, Tester)

Track books with reading progress and reviews.

**Features:**
- Add books (manual entry or ISBN lookup via Open Library API)
- Reading status (want to read, reading, finished)
- Progress tracking with page counts
- Star ratings and notes
- Statistics dashboard
- SQLite backend

**Why it's good for testing:**
- External API integration
- Progress calculations
- Dashboard aggregations

---

## 10. Team Standup Logger

**Complexity:** High
**Type:** Blazor Server + SignalR
**Agents:** 5 (Architect, Backend Dev, Frontend Dev, Tester, DevOps)

Async standup tool for small teams.

**Features:**
- Create teams and invite members (simple code-based join)
- Daily standup form (yesterday, today, blockers)
- Real-time updates when team members post
- Weekly digest generation
- Slack-style threaded comments on blockers
- SQLite with EF Core

**Why it's good for testing:**
- Real-time SignalR communication
- Multi-user scenarios
- Threaded data structures
- Digest/summary generation

---

## Project Selection Guide

| Complexity | Projects | Best For |
|------------|----------|----------|
| **Low** | 1, 2 | Initial APMAS validation, quick iterations |
| **Medium** | 3, 4, 5, 6 | Core orchestration testing, typical workflows |
| **Medium-High** | 7, 8, 9 | Multi-agent coordination, background services |
| **High** | 10 | Full stress test, real-time features |

## Recommended Test Sequence

1. **Task Timer CLI** - Validate basic agent spawning and completion
2. **Expense Tracker API** - Test API development workflow
3. **Recipe Book** - Test Blazor Server patterns
4. **Event Scheduler** - Test WASM + API split architecture
5. **Team Standup Logger** - Full stress test with all agent types
