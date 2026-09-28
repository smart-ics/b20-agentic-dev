# Feature

## Identity

ID: FEAT-FCOL-003
Name: Create Operational Post
Type: Command

## Purpose

Enables authorized actors to compose and publish a new human-authored operational post, optionally associating it with an existing Request, Work Package, Customer, or Product to preserve and communicate operational knowledge.

## User Outcome

The actor publishes operational findings, root cause explanations, progress updates, or announcements to the entire organization, creating a durable discussion record anchored to relevant business entities.

## Traceability

### Domains
- Post
- Request
- Customer
- Product
- Organization

### Scenarios
- SC-FCOL-003

### Use Cases
- UC-FCOL-003

### User Journeys
- UJ-FCOL-003

### Screens
- SCR-POST-002

## Preconditions

- The actor is authenticated and authorized to author operational communications (Implementator, Request Owner, Management).
- Any referenced operational entity (Request, Work Package, Customer, Product) must exist in its respective domain.

## Capability

The system provides an authoring form on `SCR-POST-002: Create Post` (accessible from `SCR-FEED-001` or `SCR-REQ-003`) capturing Post Title, Post Content, and optional references to operational objects (Request, Work Package, Customer, Product). Upon submission, the system creates a new Post aggregate with `HUMAN_AUTHORED` source, sets initial lifecycle status to `ACTIVE` and visibility to `VISIBLE`, assigns a permanent identity/link, and surfaces the post in the Operational Feed.

## Business Rules

- Every Post must have a unique identity, an author referencing an active Person from the Organization Domain, and non-empty title and content (Post Domain Rules 1, 2, 3).
- The Post source is set to `HUMAN_AUTHORED` (Post Domain Section 5).
- Initial Post lifecycle status is `ACTIVE` and visibility is `VISIBLE` (Post Domain Rules 4, 5, Section 8).
- A Post may exist with or without references to other operational objects (Post Domain Rule 8).
- A Post Reference only records contextual association; it does not transfer ownership or change the lifecycle state of the referenced object (Post Domain Rules 10, 11, 37).
- Creating a Post does not create a Request, Work Package, Commitment, or Decision (Post Domain Rules 37, 38, 40).
- When initiated from `SCR-REQ-003: Request Detail`, the Post Reference is pre-populated with the current Request (UJ-FCOL-003 Step 1; screen-inventory.md line 352).
- Upon submission, navigation redirects to `SCR-FEED-001: Operational Feed` or `SCR-POST-001: Post Detail` (screen-inventory.md line 355).

## Success Result

A new Post entity is created in authoritative state `ACTIVE` / `VISIBLE` with `HUMAN_AUTHORED` source, a permanent link, and optional contextual references, appearing immediately at the top of the Operational Feed.

## Failure Conditions

- Post Title or Content is blank or missing (validation failure).
- Selected reference ID does not exist in the referenced domain (referential failure).
- Actor lacks authorization to author operational communications (authority failure).

## Acceptance Criteria

- [ ] Creation screen is accessible at `SCR-POST-002` via actions on `SCR-FEED-001` and `SCR-REQ-003`.
- [ ] Form validates that Title and Content are non-empty before submission.
- [ ] Users can optionally attach references to valid Requests, Work Packages, Customers, or Products.
- [ ] Opening the form from `SCR-REQ-003` pre-populates the current Request reference.
- [ ] Submitted post receives a unique ID, status `ACTIVE`, visibility `VISIBLE`, and appears in `SCR-FEED-001`.
- [ ] Creating a post does not alter the lifecycle or ownership of any referenced entity.

## Implementation Notes

Creates an independent Post Aggregate while establishing optional Post Reference links to upstream domain entities.
