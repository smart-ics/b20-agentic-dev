# Management Oversight Use Cases

Derived from **Management Oversight Scenarios** using the **Use‑Case Discovery Skill**.

---

## Use‑Case UC‑MGT‑001: Reassign Request Ownership
- **Actor:** Management
- **Goal:** Change the ownership of a Request to another handler.
- **System Interaction:** The system must provide a function to edit the `OwnerPersonId` of a Request (see [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/operational/domains/request-domain.md)).
- **Related Domains:** Request, Organization (Person handling the request).

---

## Use‑Case UC‑MGT‑002: Review Customer Request Progress
- **Actor:** Management
- **Goal:** Monitor the status of all Requests associated with a specific Customer.
- **System Interaction:** The system offers a filtered view/list of Requests by `CustomerId` with status summaries (see [request-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/operational/domains/request-domain.md)) and accesses Customer details (see [customer-domain.md](file:///d:/Project.Aktif/b20-agentic-dev/operational/domains/customer-domain.md)).
- **Related Domains:** Request, Customer.

---

## Use‑Case UC‑MGT‑003: Review Programmer Request Performance
- **Actor:** Management
- **Goal:** Monitor the volume and outcomes of Requests handled by a specific Programmer.
- **System Interaction:** The system aggregates Requests where `OwnerPersonId` references the Programmer (a Person in the Organization domain) and provides metrics such as count, completion rate, and outcome breakdown.
- **Related Domains:** Request, Organization (Programmer as Person).

---

## Use‑Case UC‑MGT‑004: Review Programmer Workload
- **Actor:** Management
- **Goal:** Assess the current assignment load of a Programmer.
- **System Interaction:** The system lists active Requests assigned to the Programmer (`OwnerPersonId`) and displays workload indicators (e.g., number of open Requests, priority distribution).
- **Related Domains:** Request, Organization.

---

*These use‑cases follow the discovery principle: an actor must interact with the system to achieve a goal.*
