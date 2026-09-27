# Collaboration Use Cases

Derived from **Request Collaboration Scenarios** using the **Use‑Case Discovery Skill**.

---

## UC‑COL‑001: Record Supporting Information

**Actor:** Implementator

**Goal:** Record additional context or evidence with an existing Request.

**System Interaction:**
- Implementator selects the target Request.
- System presents an option to add supporting information.
- Implementator provides the supporting information (e.g., a Post or Comment).
- System attaches the information to the Request and persists it.

**Related Domains:** Request, Post

**Related Scenario:** SC‑COL‑001

---

## UC‑COL‑002: Search Request History

**Actor:** Implementator

**Goal:** Find previous Requests or related information.

**System Interaction:**
- Implementator initiates a search operation.
- System presents a query interface for specifying criteria (e.g., Request ID, keywords, date range).
- Implementator enters search parameters.
- System retrieves matching historical Requests and displays summaries.

**Related Domains:** Request

**Related Scenario:** SC‑COL‑002

---

## UC‑COL‑003: Track Request Progress

**Actor:** Implementator

**Goal:** Monitor the current status and steps of a Request.

**System Interaction:**
- Implementator selects the Request to track.
- System retrieves the current progress, status, and step‑history of the Request.
- System displays the status and detailed progress information to the Implementator.

**Related Domains:** Request

**Related Scenario:** SC‑COL‑003

---

## UC‑COL‑004: Track All Assigned Requests

**Actor:** Implementator

**Goal:** Monitor all Requests assigned to the Implementator.

**System Interaction:**
- Implementator navigates to the “My Assigned Requests” view.
- System queries all Requests where the owner/assignee is the Implementator.
- System displays a list of these Requests with their current statuses and key details.

**Related Domains:** Request, Organization

**Related Scenario:** SC‑COL‑004

---

*These use‑cases describe what actors accomplish through system interaction to support operational scenarios, without specifying UI, database schema, or implementation mechanics.*
