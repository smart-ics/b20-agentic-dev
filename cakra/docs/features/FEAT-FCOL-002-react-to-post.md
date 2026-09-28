# Feature

## Identity

ID: FEAT-FCOL-002
Name: React to Post
Type: Collaboration

## Purpose

Enables operational actors to express structured operational reactions to posts or comments, providing lightweight operational signals without writing textual comments.

## User Outcome

The actor signals operational context (such as having seen an update, experienced the same issue, possessing an idea, identifying a duplicate, or requesting clarification), contributing to collective awareness.

## Traceability

### Domains
- Post

### Scenarios
- SC-FCOL-002

### Use Cases
- UC-FCOL-002

### User Journeys
- UJ-FCOL-002

### Screens
- SCR-FEED-001
- SCR-POST-001

## Preconditions

- Target Post or Comment exists and is in `ACTIVE` state.
- The actor is authenticated and recognized as a Person within the Organization Domain.

## Capability

The system provides interactive reaction controls on `SCR-FEED-001: Operational Feed` and `SCR-POST-001: Post Detail` to select from standardized operational reaction types. It records the reaction against the target entity with the actor's identity, updates reaction aggregate summary badges, and allows the actor to toggle (add or remove) their reaction.

## Business Rules

- Reactions are strictly operational: `SEEN`, `EXPERIENCED`, `HAVE_IDEA`, `SIMILAR_ISSUE`, `DUPLICATE`, and `NEED_CLARIFICATION`. Generic social media reactions (Like, Love, Haha) are prohibited (Post Domain Section 5 - Reaction).
- A Person may express at most one Reaction of the same type on the same Post or Comment at the same time (Post Domain Rules 22, 23).
- Adding or removing a Reaction is an operational signal and does not change the authoritative lifecycle state or visibility of the Post or any referenced object (Post Domain Rules 24, 26).
- An actor can toggle off (remove) their previously selected reaction (UJ-FCOL-002 Alternative Paths).

## Success Result

The selected reaction is recorded or toggled off for the target Post or Comment, and the reaction badge count updates immediately for all viewers.

## Failure Conditions

- Actor selects an invalid or unsupported reaction type (validation failure).
- Target Post or Comment is archived, hidden, or deleted (state conflict).
- Actor is not authenticated or lacks Person identity (authority failure).

## Acceptance Criteria

- [ ] Controls for the 6 approved operational reaction types are available on `SCR-FEED-001` and `SCR-POST-001`.
- [ ] Users can react to both Posts and individual Comments.
- [ ] Selecting an already active reaction by the same user removes that reaction (toggle off).
- [ ] Duplicate reactions of the identical type by the same actor on the same item are prevented.
- [ ] Reaction summary counts update in real-time.
- [ ] Submitting reactions produces no mutations on Post status or referenced domain objects.

## Implementation Notes

Maintains Reaction entries within the Post Aggregate.
