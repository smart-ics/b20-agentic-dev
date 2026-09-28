# Feed Collaboration Use Cases

Derived from **Feed Collaboration Scenarios** using the **Use-Case Discovery Skill**.

---

## UC-FCOL-001: Comment on Post

**Actor:** All Actors

**Goal:** Contribute to an operational discussion.

**System Interaction:** Actor selects a Post and submits a Comment; the system records the Comment as part of the Post discussion and makes it visible to all stakeholders.

**Related Domains:** Post

**Related Scenario:** SC-FCOL-001

---

## UC-FCOL-002: React to Post

**Actor:** All Actors

**Goal:** Express a structured operational reaction.

**System Interaction:** Actor selects a Reaction type on a Post or Comment; the system records the Reaction as an operational signal visible to all stakeholders.

**Related Domains:** Post

**Related Scenario:** SC-FCOL-002

---

## UC-FCOL-003: Create Operational Post

**Actor:** Implementator

**Goal:** Author a new operational communication.

**System Interaction:** Actor composes a Post with content and optional references to operational objects (Request, Work Package, Customer, Product); the system creates the Post and makes it visible in the Operational Feed.

**Related Domains:** Post

**Related Scenario:** SC-FCOL-003

---

## UC-FCOL-004: Navigate from Post to Request

**Actor:** All Actors

**Goal:** Access the structured Request record from a Post.

**System Interaction:** Actor follows the Post Reference link to the Request; the system navigates to the Request Detail screen.

**Related Domains:** Post, Request

**Related Scenario:** SC-FCOL-004

---

## UC-FCOL-005: Filter Operational Feed

**Actor:** All Actors

**Goal:** Focus the feed on a specific operational context.

**System Interaction:** Actor selects filter criteria such as Customer, Product, or Team; the system displays only Posts matching the selected criteria.

**Related Domains:** Post, Customer, Product, Organization

**Related Scenario:** SC-FCOL-005

---

*These use-cases describe what actors accomplish through system interaction to support operational scenarios, without specifying UI, database schema, or implementation mechanics.*
