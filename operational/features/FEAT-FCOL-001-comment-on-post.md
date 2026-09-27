# Feature

## Identity

ID: FEAT-FCOL-001
Name: Comment on Post
Type: Collaboration

## Purpose

Enables operational actors to participate in collaborative discussions by submitting comments on active posts from the feed stream, post detail, or request detail.

## User Outcome

The actor contributes operational insights, technical findings, clarifications, or feedback to an existing post discussion, visible to all stakeholders.

## Traceability

### Domains
- Post

### Scenarios
- SC-FCOL-001

### Use Cases
- UC-FCOL-001

### User Journeys
- UJ-FCOL-001

### Screens
- SCR-FEED-001
- SCR-POST-001

## Preconditions

- Target Post exists, is in `ACTIVE` lifecycle state, and has `VISIBLE` visibility.
- The actor is authenticated and recognized as a Person within the Organization Domain.

## Capability

The system provides an input control to compose and submit comment text on `SCR-FEED-001: Operational Feed`, `SCR-POST-001: Post Detail`, and the associated feed section of `SCR-REQ-003: Request Detail`. Upon submission, the system records the comment as an element of the Post aggregate with the author's identity and timestamp, updates the post's comment count, and immediately displays the new comment in the flat discussion thread.

## Business Rules

- Comments belong to exactly one Post and cannot exist independently of that Post (Post Domain Rules 17, 18).
- Comments follow a flat discussion model; comments cannot be nested or contain child comments (Post Domain Rule 19).
- Every comment must identify its author referencing an active Person from the Organization Domain (Post Domain Rule 20).
- Submitting a comment does not alter the lifecycle state, visibility, or status of the parent Post (Post Domain Rule 25).
- Comment text must contain non-empty content (validation requirement).
- Comments inherit visibility from the parent Post unless individually hidden by administrative moderation (Post Domain Section 8).

## Success Result

A new Comment record is appended to the Post aggregate with the author's identity and timestamp, the post's comment count is incremented, and the comment becomes visible across all views displaying the Post.

## Failure Conditions

- Comment text is empty or whitespace-only (validation failure).
- Target Post does not exist, is archived, or is hidden (state conflict).
- Actor is not authenticated or not recognized as an organizational Person (authority failure).

## Acceptance Criteria

- [ ] Actor can submit a comment from `SCR-FEED-001`, `SCR-POST-001`, or `SCR-REQ-003`.
- [ ] Submitted comment appears in the flat discussion list with author name and timestamp.
- [ ] Comment count on the Post increments upon submission.
- [ ] Submitting empty comment text is rejected with a validation error.
- [ ] Comments cannot be submitted against archived or hidden posts.

## Implementation Notes

Appends a Comment entity to the Post Aggregate in accordance with the flat discussion model.
