# Feed Navigation Specification

This document details the destinations and movement paths for the **Operational Feed** area, derived from:
* **Scenarios:** `SC-AWR-001` through `SC-AWR-003` (`operational/scenarios/operational-awareness-scenarios.md`) and `SC-FCOL-001` through `SC-FCOL-005` (`operational/scenarios/feed-collaboration-scenarios.md`)
* **Use Cases:** `UC-AWR-001` through `UC-AWR-003` (`operational/use-cases/operational-awareness-use-cases.md`) and `UC-FCOL-001` through `UC-FCOL-005` (`operational/use-cases/feed-collaboration-use-cases.md`)
* **User Journeys:** `UJ-AWR-001` through `UJ-AWR-003` (`operational/user-journey/operational-awareness-user-journeys.md`) and `UJ-FCOL-001` through `UJ-FCOL-005` (`operational/user-journey/feed-collaboration-user-journeys.md`)

---

## 1. Navigation Hierarchy

```text
Operational Feed Area
├── SCR-FEED-001: Operational Feed
│   ├── Post Selection ──────────────► SCR-POST-001: Post Detail
│   ├── Create Post Action ──────────► SCR-POST-002: Create Post
│   └── Post Reference Link ────────► SCR-REQ-003: Request Detail
│
├── SCR-POST-001: Post Detail
│   ├── Post Reference Link ────────► SCR-REQ-003: Request Detail
│   └── Back / Return ──────────────► SCR-FEED-001: Operational Feed
│
└── SCR-POST-002: Create Post
    └── After Submission ───────────► SCR-FEED-001: Operational Feed
                                     └── SCR-POST-001: Post Detail
```

---

## 2. Journey Movement Paths

### UJ-AWR-001: Observe Operational Feed
* **Primary Actor:** Implementator, Management, Request Owner
* **Entry Point:** `SCR-FEED-001: Operational Feed`
* **Destination:** `SCR-FEED-001: Operational Feed`
* **Exit / Destination:** `SCR-POST-001: Post Detail` or `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor arrives at `SCR-FEED-001: Operational Feed`.
  2. Actor observes the feed.
  3. Actor may select a post to navigate to `SCR-POST-001` or a request reference to navigate to `SCR-REQ-003`.

### UJ-AWR-002: Discover Request via Feed
* **Primary Actor:** Implementator, Management, Request Owner
* **Entry Point:** `SCR-FEED-001: Operational Feed`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor arrives at `SCR-FEED-001: Operational Feed`.
  2. Actor identifies a request reference in the feed.
  3. Actor navigates to `SCR-REQ-003: Request Detail`.

### UJ-AWR-003: Monitor Operational Exceptions via Feed
* **Primary Actor:** Implementator, Management, Request Owner
* **Entry Point:** `SCR-FEED-001: Operational Feed`
* **Destination:** `SCR-FEED-001: Operational Feed`
* **Exit / Destination:** `SCR-POST-001: Post Detail` or `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor arrives at `SCR-FEED-001: Operational Feed`.
  2. Actor monitors exceptions.
  3. Actor may select an exception post to navigate to `SCR-POST-001` or `SCR-REQ-003`.

### UJ-FCOL-001: Comment on Post
* **Primary Actor:** Implementator, Management, Request Owner
* **Entry Point:** `SCR-FEED-001: Operational Feed` or `SCR-POST-001: Post Detail`
* **Destination:** `SCR-POST-001: Post Detail`
* **Exit / Destination:** `SCR-POST-001: Post Detail`
* **Movement Path:**
  1. Actor arrives at `SCR-FEED-001` or `SCR-POST-001`.
  2. Actor adds a comment on the post.
  3. Actor remains on or arrives at `SCR-POST-001: Post Detail`.

### UJ-FCOL-002: React to Post
* **Primary Actor:** Implementator, Management, Request Owner
* **Entry Point:** `SCR-FEED-001: Operational Feed` or `SCR-POST-001: Post Detail`
* **Destination:** Originating Screen
* **Exit / Destination:** Originating Screen
* **Movement Path:**
  1. Actor arrives at `SCR-FEED-001` or `SCR-POST-001`.
  2. Actor reacts to the post.
  3. Actor remains on the originating screen.

### UJ-FCOL-003: Create Operational Post
* **Primary Actor:** Implementator
* **Entry Point:** `SCR-FEED-001: Operational Feed` or `SCR-REQ-003: Request Detail`
* **Destination:** `SCR-POST-002: Create Post`
* **Exit / Destination:** `SCR-FEED-001: Operational Feed` or `SCR-POST-001: Post Detail`
* **Movement Path:**
  1. Actor navigates to `SCR-POST-002: Create Post` from `SCR-FEED-001` or `SCR-REQ-003`.
  2. Actor creates a post.
  3. Actor is directed to `SCR-FEED-001` or `SCR-POST-001` upon submission.

### UJ-FCOL-004: Navigate from Post to Request
* **Primary Actor:** Implementator, Management, Request Owner
* **Entry Point:** `SCR-FEED-001: Operational Feed` or `SCR-POST-001: Post Detail`
* **Destination:** `SCR-REQ-003: Request Detail`
* **Exit / Destination:** `SCR-REQ-003: Request Detail`
* **Movement Path:**
  1. Actor views a post with a request reference on `SCR-FEED-001` or `SCR-POST-001`.
  2. Actor selects the request reference.
  3. Actor navigates to `SCR-REQ-003: Request Detail`.

### UJ-FCOL-005: Filter Feed by Context
* **Primary Actor:** Implementator, Management, Request Owner
* **Entry Point:** `SCR-FEED-001: Operational Feed`
* **Destination:** `SCR-FEED-001: Operational Feed`
* **Exit / Destination:** `SCR-FEED-001: Operational Feed`
* **Movement Path:**
  1. Actor arrives at `SCR-FEED-001: Operational Feed`.
  2. Actor applies filters to the feed.
  3. Actor remains on `SCR-FEED-001: Operational Feed`.
