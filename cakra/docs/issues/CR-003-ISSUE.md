# ISSUE

## Metadata

ID: CR-003
Type: CHANGE-REQUEST
Status: OPEN
Title: Create New Request via Operational Feed

## Source

Reported By: User
Reported Date: 2026-10-03

## Description

The user requested that creating a new Request shall be done via the Operational Feed. Operational actors should be able to initiate and record a new Request directly from within the Operational Feed interface/workspace.

## Desired Outcome

1. Operational actors can create a new Request directly via the Operational Feed (`SCR-FEED-001`).
2. The Operational Feed serves as an active entry point / mechanism for Request intake.
3. Submitting a new Request from the Operational Feed records the Request and makes its corresponding post appear in the feed stream.

## Current Situation

1. Request creation is currently defined as being performed via a separate dedicated screen `SCR-REQ-002: Create Request`, accessible primarily from `SCR-REQ-001: Request List` or global actions (as defined in `FEAT-REQ-001` and `14-scr-req-002.md`).
2. The Operational Feed (`SCR-FEED-001: Operational Feed` in `10-scr-feed-001.md`) currently functions solely as an awareness projection with actions limited to filter, comment, and reaction, with no direct capability or entry point to create new Requests from the feed workspace.
3. Prior change request `CR-001` revoked direct creation of operational posts (`SC-FCOL-003`), establishing that feed items originate from Request creation and request lifecycle events. However, users currently have no mechanism to initiate Request creation directly from the Operational Feed.

## Evidence

- User directive: "Create New Request shall be done via Operational Feed"
- Operational Feed UI Layout: [10-scr-feed-001.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/ui-layout/10-scr-feed-001.md)
- Request Creation Feature: [FEAT-REQ-001-record-customer-request.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-REQ-001-record-customer-request.md)
- Feed Navigation: [feed-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/feed-navigation.md)
- Change Request CR-001: [CR-001-ISSUE.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/issues/CR-001-ISSUE.md)

## Notes

This artifact captures the intake request in a solution-neutral manner. Detailed feasibility assessment, UI interaction pattern (e.g., inline feed composer vs. feed-triggered modal/drawer vs. navigation), impacted feature and screen specifications, and architecture decisions belong to downstream analysis and architecture stages.
