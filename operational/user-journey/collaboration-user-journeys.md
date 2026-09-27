# Collaboration User Journeys

Derived from **Collaboration Use Cases** (`operational/use-cases/collaboration-use-cases.md`) using the **User Journey Generation Skill** (`operational/user-journey/user-journey-generation-skill.md`).

Canonical actors follow the **ICS Operational Actor Model** (`operational/actors/actor-model.md`):
* **Implementator:** Organizational Role responsible for handling operational demands, documenting progress, collaborating on requests, and managing requests for which they are responsible.

---

## UJ-COL-001: Record Supporting Information

**Use Case Reference:** UC-COL-001

### Actor
Implementator

### Goal
Record additional context, clarification, or evidence with an existing Request.

### Trigger
Additional context, customer communication, error logs, or evidence need to be recorded with an existing Request.

### Main Journey
1. Implementator locates and accesses the target Request.
2. Implementator reviews existing context and previous discussions to verify what information is currently available.
3. Implementator enters the supporting details (such as observation notes, customer clarification, or technical findings).
4. Implementator attaches relevant supporting evidence or reference materials if available.
5. Implementator submits the supporting information to make it part of the Request record.

### Alternative Paths
* **Targeted note vs. general update:** Implementator designates whether the supporting entry is an internal note or a general collaborative update.
* **Referencing related work:** Implementator includes references to related Requests or Work Packages to provide broader operational context.

### Success Outcome
The supporting information and any attached evidence are preserved with the Request and made visible to all collaborators.

### Information Needed
* Request identifier and demand context
* Supporting observations, comments, or technical notes
* Supporting files, documents, or evidence attachments (if applicable)

---

## UJ-COL-002: Search Request History

**Use Case Reference:** UC-COL-002

### Actor
Implementator

### Goal
Find previous Requests or related historical information to guide current operational activities.

### Trigger
Implementator needs to reference past resolutions, check for duplicate issues, or discover historical precedent.

### Main Journey
1. Implementator accesses the request search facility.
2. Implementator specifies search criteria, such as keywords, customer name, affected product, or date range.
3. Implementator initiates the search and reviews the matching historical results.
4. Implementator selects a relevant historical Request to examine its demand details, progress history, and final resolution.

### Alternative Paths
* **Refining broad results:** Implementator applies additional criteria (e.g., status, resolution type, date range) when initial results are too broad.
* **No results found:** Implementator adjusts search terms or removes constraints to locate related precedent.

### Success Outcome
Implementator retrieves and reviews relevant past Requests and leverages prior knowledge for current tasks.

### Information Needed
* Search keywords, customer name, product, or timeframe
* Summary attributes of search results (status, date, customer, summary, resolution outcome)

---

## UJ-COL-003: Track Request Progress

**Use Case Reference:** UC-COL-003

### Actor
Implementator

### Goal
Monitor the current progress, status, and milestone history of a Request.

### Trigger
Implementator needs to inspect the current state of a Request to determine next steps or answer stakeholder inquiries.

### Main Journey
1. Implementator locates the Request whose progress needs to be checked.
2. Implementator views the current operational status, assigned owner, and milestone state.
3. Implementator inspects the chronological progression of steps and recorded activities.
4. Implementator determines whether the Request is advancing according to expectations.

### Alternative Paths
* **Stalled progress identified:** Implementator identifies a blocker or stalled step and decides to record a follow-up inquiry or notify the assigned owner.
* **Inspecting detailed discussions:** Implementator reviews chronological comments and notes to understand the rationale behind a status change.

### Success Outcome
Implementator gains full visibility into the current standing, ownership, and history of the Request.

### Information Needed
* Request identifier
* Current status, assigned owner, and recent updates
* Step history and milestone progress timeline

---

## UJ-COL-004: Review Assigned Requests

**Use Case Reference:** UC-COL-004

### Actor
Implementator

### Goal
Review Requests for which they are currently responsible to organize and prioritize work.

### Trigger
Implementator begins an operational session or needs an updated overview of Requests for which they are currently responsible.

### Main Journey
1. Implementator navigates to the overview of Requests for which they are responsible.
2. Implementator reviews the list of Requests for which they are currently responsible along with current statuses, priorities, and deadlines.
3. Implementator assesses which Requests require immediate action, evaluation, or follow-up.
4. Implementator selects a specific Request to begin or continue active work.

### Alternative Paths
* **Filtering and grouping workload:** Implementator sorts or filters Requests by urgency, customer, product, or lifecycle stage.
* **No active Requests:** Implementator finds no active Requests for which they are responsible and proceeds to check unassigned queues or other operational duties.

### Success Outcome
Implementator possesses a clear understanding of the Requests for which they are currently responsible and selects the next priority Request to address.

### Information Needed
* List of Requests for which the Implementator is responsible (with identifier, customer, summary, status, and priority)
* Operational responsibility context
