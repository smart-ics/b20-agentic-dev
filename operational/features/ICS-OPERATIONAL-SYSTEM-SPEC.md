# ICS Operational System — Feature Discovery Spec

**Status:** Draft for PO review  
**Owner:** COO  
**Scope:** MVP  

---

## 1. Why this system exists

Per the Operational Principle: the system exists to give leadership timely, decision-ready visibility — not to produce reports. It must continuously answer:

- What is happening?
- What changed?
- What needs attention?
- What decision is needed?
- What is the expected impact?

**Critical principle:** Operational state is the single source of truth. Dashboards, summaries, and KPIs are **views** of that state — never separately maintained.

---

## 2. Users & primary needs

| Role | Primary need |
|---|---|
| **COO** | Cross-product/customer visibility, exceptions, escalations |
| **Product Owner / Programmer** | Requests & Work Packages for their owned Product only |
| **Trainer / Project Manager** | Requests & Work Packages for their owned Customer(s) |
| **Board of Director** | Executive summary & entity-level trend, no operational detail |

---

## 3. Domain master data (foundation)

Build these first — simple list / detail / edit screens. No workflow needed here.

### Organization

- **CRUD:** Person, Team, Role, Responsibility
- **Assign:** Person → Team (Membership), Person → Role (Role Assignment)
- **Status:** Person can be Active / Inactive (history preserved, never hard-deleted)

### Customer

- **CRUD:** Customer, Customer Contacts
- **Relationship Status:** Prospect → Active → Inactive
- **Ownership:** Assign which ICS person owns the customer relationship

### Product

- **CRUD:** Product
- **Lifecycle:** Planned → Active → Deprecated → Retired
- **Ownership:** Assign Product Owner

---

## 4. Request management (core daily workspace)

### Create Request

- Title, Description, Type
- Requester: internal Person or Customer Contact
- Optional links to: Customer, Product, Work Package

### Manage Request

- **Assign / reassign** Request Owner
- **Lifecycle:** `Captured → Validated → Active → Closed`, with direct `Captured → Closed` shortcut when no active work is needed
- **Resolution capture** on close: outcome + description
- **Filter / search** by: owner, product, customer, status, type

### "My Requests" view

Every Request owner needs a daily workspace view showing only their assigned Requests.

---

## 5. Work Package management

- **Create Work Package:** name, objective, owner, optional Customer / Product
- **Add / remove Requests** to / from a Work Package
- **Lifecycle:** Draft → Active → Closed
- **Detail view:** grouped Requests filtered by Work Package (reuse the Request list component, don't build a separate UI)

---

## 6. Workflow actions

Thin actions layered on top of Request state — **not new lifecycle states:**

- **Direct Resolution** — owner resolves and documents in one step (default path)
- **Escalate** — flags a Request as needing management attention
- **Reject** — close with a rejection reason

**Important:** Assessing, Estimating, Discussing, Escalating, Waiting, Implementing, Reviewing are **workflow activities**, not lifecycle states. Don't model them as a Request status field.

---

## 7. Attention / exception engine

This is the actual point of the system. **Computed live from current state — never stored separately.**

### What triggers Attention

- Requests with no owner
- Requests with no status change in N days (threshold: TBD by PO)
- Escalated Requests awaiting a decision
- Active Work Packages with zero active Requests
- Products or Customers with no owner assigned

### Rendered as

A single **Attention feed** — this is the first screen COO and BOD see, not a list of everything.

---

## 8. Role-specific views

| Role | View |
|---|---|
| **COO** | Attention feed + all Requests/Work Packages across products, escalation queue |
| **Product Owner / Programmer** | Requests + Work Packages filtered to owned Product |
| **Trainer / PM** | Requests + Work Packages filtered to owned Customer(s) |
| **Board of Director** | Executive summary: counts by status/product/customer, open-vs-closed trend over time. No drill-in required at MVP. |

### Implementation approach

COO, PO, and Trainer/PM views share **one Request/Work Package list screen with different default filters**. Build once, apply context-specific filters. Only the BOD summary is a genuinely different, aggregated screen.

---

## 9. History & audit

- Log every status change and ownership change on Request and Work Package (who / when / what changed)
- Simple "History" tab on the detail page — no dedicated audit UI needed at MVP

---

## 10. Explicitly out of scope for MVP

### Capacity Plan & Commitment

These are projections derived from Requests + ownership + decisions. Don't build them until Request/Work Package data actually exists to derive from.

### Risk as a separate entity

Risk is derived, not authoritative. An "escalated" or "stalled" Request surfaced in the Attention feed covers this.

### Notifications / email

Useful later, not core to "what needs attention" at MVP.

### Report builder

Reports are views of state, not the product goal. One Attention feed + one filtered list + one BOD summary covers MVP.

---

## 11. Open questions for PO before scoping slices

1. **Stalled threshold:** What's the number of days with no status change that should trigger the Attention feed? (suggest 5–7 days, adjust as needed)

2. **Escalate target:** Does Escalate need to specify a target person (e.g., escalate to COO), or does it just flag "needs leadership attention" generically?

3. **BOD trend granularity:** For the executive summary — weekly or monthly trend granularity?

---

## Source

- ICS Operational Principle
- ICS Operational Workflow
- Organization / Customer / Product / Work Package / Request domain definitions
- Source: `smart-ics/b20-agentic-dev`, `operational/` folder
