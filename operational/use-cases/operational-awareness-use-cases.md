# Operational Awareness Use Cases

Derived from **Operational Awareness Scenarios** using the **Use-Case Discovery Skill**.

---

## UC-AWR-001: Observe Operational Feed

**Actor:** All Actors

**Goal:** Discover current operational activity.

**System Interaction:** Actor views the Operational Feed; the system presents a chronological stream of Posts with content summaries, referenced operational objects, comment counts, and reaction summaries.

**Related Domains:** Post

**Related Scenario:** SC-AWR-001

---

## UC-AWR-002: Discover Request via Feed

**Actor:** Implementator

**Goal:** Become aware of a Request through its Post presence in the feed.

**System Interaction:** Actor observes a system-generated Post about a Request event in the feed; the system shows the Post content, referenced Request summary, and any ongoing discussion.

**Related Domains:** Post, Request

**Related Scenario:** SC-AWR-002

---

## UC-AWR-003: Monitor Operational Exceptions via Feed

**Actor:** Management

**Goal:** Identify operational matters requiring attention through the feed.

**System Interaction:** Management views the Operational Feed and identifies Posts indicating escalations, stalled Requests, or items requiring decision; the system surfaces these through Post content and Reaction summaries.

**Related Domains:** Post, Request

**Related Scenario:** SC-AWR-003

---

*These use-cases describe what actors accomplish through system interaction to support operational scenarios, without specifying UI, database schema, or implementation mechanics.*
