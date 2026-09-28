# ISSUE

## Metadata

ID: CR-001
Type: CHANGE-REQUEST
Status: OPEN
Title: Revoke Direct Creation of Operational Posts (SC-FCOL-003)

## Source

Reported By: Product Owner
Reported Date: 2026-09-29

## Description

The Product Owner requested the removal and revocation of scenario SC-FCOL-003 ("Actor Creates Operational Post"). Users should have no direct mechanism to author or submit operational posts. Operational Feed entries should only originate from Request creation (and associated request lifecycle events).

## Desired Outcome

1. Direct user creation of operational posts is removed from the system.
2. Operational posts in the Operational Feed are generated solely from Request creation.
3. Scenario SC-FCOL-003 and any associated direct post-creation capabilities are decommissioned/revoked.

## Current Situation

1. Scenario SC-FCOL-003 in `cakra/docs/scenarios/feed-collaboration-scenarios.md` defines direct creation of operational posts by actors (Implementator).
2. Feature FEAT-FCOL-003 in `cakra/docs/features/FEAT-FCOL-003-create-operational-post.md` specifies the capability allowing actors to author and submit posts with `HUMAN_AUTHORED` source via `SCR-POST-002: Create Post`.
3. Navigation and screen artifacts reference direct post creation routes from the Feed and Request Detail screens.

## Evidence

- Scenario definition in [feed-collaboration-scenarios.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/scenarios/feed-collaboration-scenarios.md#L53-L76) (`SC-FCOL-003: Actor Creates Operational Post`).
- Feature specification in [FEAT-FCOL-003-create-operational-post.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/features/FEAT-FCOL-003-create-operational-post.md).
- Product Owner directive: "There is no way for user create operational post directly. Operational Feed should only come from Request Creation. Please create issue to remove this feature."

## Notes

This artifact captures the intake request in a solution-neutral manner. Detailed feasibility assessment, identification of impacted artifacts (scenarios, use cases, journeys, features, architecture, and UI screens), and remediation scope belong to the downstream analysis stage.
