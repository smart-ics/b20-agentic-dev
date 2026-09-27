# SCR-REQ-002: Create Request

## Purpose

Allow an Implementator to record a customer operational demand as a new Request in the system.

This screen captures sufficient information to create a Request in the CAPTURED state.

## Primary Actors

* Implementator

## Entry Points

* `SCR-REQ-001: Request List` → "Create Request" action button
* Global Navigation Quick Action → `+ New Request`

## Related Use Cases

* UC-REQ-001: Record Customer Request

## Related User Journeys

* UJ-REQ-001: Record Customer Request

## Information Sections

### Section: Request Information Form

Captures the core details of the operational demand.

Displays:

* Title (required) — concise summary of the demand
* Description (required) — detailed description of what is being requested
* Request Type — classification of the request nature

**Traceability:** UJ-REQ-001 step 2 (enters the request details).

### Section: Customer Association

Associates the Request with the originating Customer, if applicable.

Displays:

* Customer selector (optional) — search and select from Customer domain
* Customer Contact selector (optional) — search and select from Customer Contacts of the selected Customer

**Traceability:** UJ-REQ-001 step 2 (identifies the customer). Domain: Customer.

### Section: Operational Context

Associates the Request with relevant operational context.

Displays:

* Product selector (optional) — search and select from Product domain
* Work Package selector (optional) — search and select from Work Package domain

**Traceability:** UJ-REQ-001 step 3 (associates relevant context such as affected product or work package). Domains: Product, Work Package.

## Available Actions

### Action: Submit Request

* **Actor:** Implementator
* **Expected Outcome:** Request is created in CAPTURED state. Actor navigates to `SCR-REQ-003: Request Detail` for the newly created Request.
* **Traceability:** UJ-REQ-001 step 4 (reviews and submits the request).

### Action: Cancel

* **Actor:** Implementator
* **Expected Outcome:** No Request is created. Actor navigates back to `SCR-REQ-001: Request List`.
* **Traceability:** Standard navigation; no use case outcome.

## Navigation Destinations

* `SCR-REQ-001: Request List` (cancel, or after submission via back navigation)
* `SCR-REQ-003: Request Detail` (after successful submission)

## Layout Sketch

```
+--------------------------------------------------+
| Global Navigation Bar                            |
+--------------------------------------------------+

+--------------------------------------------------+
| Screen Title: Create Request                     |
+--------------------------------------------------+

+--------------------------------------------------+
| Request Information Form                         |
| Title:       [________________________]          |
| Description: [________________________]          |
|              [________________________]          |
| Request Type:[________________________]          |
+--------------------------------------------------+

+--------------------------------------------------+
| Customer Association                             |
| Customer:    [___________ search/select]         |
| Contact:     [___________ search/select]         |
+--------------------------------------------------+

+--------------------------------------------------+
| Operational Context                              |
| Product:     [___________ search/select]         |
| Work Package:[___________ search/select]         |
+--------------------------------------------------+

+--------------------------------------------------+
| [Cancel]                        [Submit Request] |
+--------------------------------------------------+
```
