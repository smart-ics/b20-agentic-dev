---
Title: Post Domain
Code: POST
Artifact: DOMAIN
Version: 1.0
LastUpdated: 2026-09-26
---

# 1. Business Overview

The Post Domain defines persistent operational communication and discussion within ICS.

A Post is an operational object used to communicate, discuss, explain, share, or preserve knowledge about something relevant to ICS operations.

A Post may be created automatically by the Operational System when a significant operational object is created or changed, or may be created directly by an authorized actor when a human-authored operational communication is required.

A Post has its own authoritative current state and lifecycle.

A Post may reference another operational object such as a Request, Work Package, Customer, Product, or Organization entity, but the referenced object does not own the Post and does not determine the Post lifecycle.

A Post remains an independent operational object even when its original reference is no longer available.

A Post may contain comments and reactions. The discussion remains part of the Post and forms a persistent record of operational communication and organizational learning.

The domain answers:

* What is this operational communication?
* Who or what created it?
* What is the current state of the Post?
* What operational object does the Post reference, if any?
* What discussion has occurred around the Post?
* What reactions have been recorded?
* Who may see the Post?
* Has the Post been hidden, archived, or otherwise removed from normal visibility?

The Post Domain exists to preserve operational communication as durable business knowledge.

It does not own the operational truth of the objects being discussed.

---

# 2. Ubiquitous Language

| Term | Meaning |
|---|---|
| Post | A persistent operational communication object containing content and optional references to operational objects. |
| Post Author | The Person or system actor responsible for creating the Post. |
| Post Reference | An optional relationship from a Post to another operational object. |
| Comment | A contribution to the discussion attached to a Post. |
| Reaction | A structured response to a Post or Comment indicating an operational meaning such as Seen, Experienced, Have Idea, Similar Issue, Duplicate, or Need Clarification. |
| Post Status | The authoritative current lifecycle state of the Post. |
| Visibility | The current visibility condition determining whether a Post is available in normal operational views. |
| Permanent Link | The stable identity and address used to reference a Post from another communication channel. |
| System-Generated Post | A Post created automatically by the system in response to an operational event or state change. |
| Human-Authored Post | A Post intentionally created by a Person for operational communication or knowledge sharing. |
| Post Discussion | The Post and its associated comments and reactions. |
| Orphaned Reference | A Post reference whose referenced operational object no longer exists or is no longer resolvable. |
| Post History | The historical record of significant Post state and content changes. |

---

# 3. Domain Capabilities

The Post Domain provides:

* Post Management
* Post Lifecycle Management
* Post Reference Management
* Post Discussion Management
* Comment Management
* Reaction Management
* Post Visibility Management
* Post History Management
* Permanent Post Identity Management

These capabilities maintain authoritative Post knowledge.

They do not manage:

* Request lifecycle;
* Work Package lifecycle;
* Customer lifecycle;
* Product lifecycle;
* Organization lifecycle;
* operational workflow for resolving Requests;
* Work Package scope;
* Customer ownership;
* Product ownership;
* organizational responsibility.

The Post Domain may record references to objects owned by those domains, but does not take ownership of their state.

---

# 4. Actors & Roles

## Post Author

The Person or system actor that creates a Post.

A Post Author may be:

* a Person from the Organization Domain; or
* the Operational System for a System-Generated Post.

The Post Domain records authorship but does not own Person identity.

---

## Comment Author

The Person who creates a Comment.

The Comment Author must reference a Person from the Organization Domain.

---

## Moderator

A Person authorized to change Post visibility or perform other moderation actions.

Moderation authority is determined by the organization's access rules.

The Post Domain does not define organizational roles or permissions.

---

## Viewer

A Person allowed to see a Post according to the Post visibility rules and system access rules.

---

# 5. Domain Objects

## Post

Represents persistent operational communication.

Attributes:

```text
PostId
Title
Content
Author
Source
Status
Visibility
CreatedAt
UpdatedAt
```

`Source` identifies whether the Post was:

```text
SYSTEM_GENERATED
HUMAN_AUTHORED
```

A Post may exist with or without a reference to another operational object.

Examples:

```text
A Request was created for a pharmacy printing problem.

A programmer documents the root cause of a recurring issue.

A trainer shares an implementation finding.

A Product Owner announces an important product behavior.

The system records that a significant Request changed state.
```

A Post is not a Request.

A Post is not a Work Package.

A Post does not become a Request merely because it describes a problem.

---

## Post Reference

Represents an optional relationship from a Post to an operational object.

A Post Reference may point to:

```text
Request
Work Package
Customer
Product
Organization Entity
```

The Post Reference records contextual association only.

The referenced domain remains authoritative for the referenced object's current state.

A Post may lose its reference while remaining valid as a Post.

---

## Comment

Represents a contribution to the discussion of a Post.

Attributes:

```text
CommentId
PostId
AuthorPersonId
Content
Status
CreatedAt
UpdatedAt
```

Comments belong to a Post.

Comments do not form independent discussion hierarchies.

The Post Domain uses a flat discussion model:

```text
Post
├── Comment
├── Comment
└── Comment
```

A Comment cannot exist independently of its Post.

---

## Reaction

Represents a structured response to a Post or Comment.

Examples:

```text
SEEN
EXPERIENCED
HAVE_IDEA
SIMILAR_ISSUE
DUPLICATE
NEED_CLARIFICATION
```

A Reaction records that a Person expressed a defined operational response.

Generic social-media reaction concepts such as Like, Love, or Haha are not part of the domain language unless explicitly introduced as an operational need.

---

## Post History

Represents the historical record of significant changes to the Post.

Examples:

```text
Created
Content changed
Reference added
Reference removed
Visibility changed
Status changed
Archived
Restored
Comment added
Comment removed
Reaction added
Reaction removed
```

History preserves how the Post reached its current state.

It does not replace the Post's current state as authoritative.

---

# 6. Aggregates

## Post Aggregate

```text
Post
├── Post Reference(s)
├── Comment(s)
└── Reaction(s)
```

The Post Aggregate is the authoritative source of:

* Post identity;
* Post content;
* Post source;
* Post status;
* Post visibility;
* Post references;
* Post discussion;
* Post reactions.

The Post Aggregate does not own:

* Request state;
* Work Package state;
* Customer state;
* Product state;
* Organization state.

Those remain owned by their respective domains.

---

# 7. Business Rules

1. Every Post must have a unique identity.

2. Every Post must have an Author or System source.

3. Every Post must have identifiable content sufficient to understand what is being communicated.

4. Every Post has exactly one authoritative current Status.

5. Every Post has exactly one authoritative current Visibility condition.

6. A Post may be system-generated or human-authored.

7. A system-generated Post must identify the operational source event or source object that caused its creation when such information is available.

8. A Post may exist without a reference to another operational object.

9. A Post may reference one or more operational objects only through explicit Post References.

10. A Post Reference does not transfer ownership of the referenced object.

11. The referenced domain remains authoritative for the current state of the referenced object.

12. A change in the referenced object does not automatically change the Post lifecycle unless the Post itself is explicitly changed.

13. A Post may remain valid when its referenced object is deleted, archived, or otherwise becomes unavailable.

14. A Post with no resolvable reference remains an independent operational object.

15. A Post must have a permanent identity that remains stable throughout its lifecycle.

16. A Permanent Link to a Post must remain stable while the Post exists.

17. Comments belong to exactly one Post.

18. A Comment cannot exist independently of its Post.

19. Comments use a flat discussion model. A Comment does not contain child Comments.

20. Every Comment must identify its author.

21. A Reaction must identify the Person who expressed the Reaction.

22. A Person may express at most one Reaction of the same type on the same Post at the same time.

23. A Person may express at most one Reaction of the same type on the same Comment at the same time.

24. Reactions are operational signals, not authoritative changes to the Post or referenced object.

25. Adding or removing a Comment does not change the lifecycle of the Post unless explicitly defined by a separate business rule.

26. Adding or removing a Reaction does not change the lifecycle of the Post.

27. Hiding a Post changes Post visibility, not the existence of the Post.

28. Archiving a Post does not delete its discussion history.

29. A hidden or archived Post remains historically retrievable according to applicable access rules.

30. Physical deletion of a Post is exceptional and permitted only under specific governance reasons. Hiding or archiving is the preferred mechanism for removing a Post from normal visibility while preserving the historical record required by the Operational Knowledge Lifecycle.

31. Deletion of a referenced Request, Work Package, Customer, Product, or Organization entity does not require deletion of the Post.

32. A Post must not become the authoritative source for the state of another domain object.

33. The Feed is not an operational domain. Feed views are derived from Posts and other operational state.

34. Filtering or ordering Posts for a Feed does not create new Post state.

35. A Post may exist without appearing in a particular Feed view because Feed visibility and filtering are presentation concerns over Post state.

36. A Post is independent from the workflow of the object it references.

37. Creating a Post does not create a Request, Work Package, Commitment, Action, Risk, or other operational domain object unless an explicit business operation creates that object separately.

38. A Post is not a Commitment.

39. A Post is not an approval.

40. A Post is not a decision unless the content explicitly records a decision made through an appropriate operational authority.

---

# 8. State Machines & Lifecycles

## Post Lifecycle

The Post lifecycle defines the existence condition of the Post.

```text
ACTIVE
   ↓
ARCHIVED
```

---

## Visibility

Visibility is not part of the Post lifecycle. It is an independent property of the current Post state.

```text
VISIBLE
   ↕
HIDDEN
```

A Post may be visible or hidden regardless of its lifecycle state, subject to the rules below.

---

## ACTIVE

The Post exists as current operational knowledge.

An Active Post may:

* appear in normal Feed views;
* receive Comments;
* receive Reactions;
* maintain references;
* remain available through its Permanent Link.

An Active Post may be visible or hidden.

---

## HIDDEN

HIDDEN is a visibility condition, not a lifecycle state.

The Post remains part of the authoritative operational record but is excluded from normal visibility.

A hidden Post:

* continues to exist;
* retains its identity;
* retains its discussion;
* retains its history;
* remains retrievable according to access rules.

Hidden is a visibility condition, not destruction.

A hidden Post may become visible again.

---

## ARCHIVED

The Post is retained as historical operational knowledge but is no longer part of normal active communication.

An archived Post:

* remains identifiable;
* retains its Permanent Link;
* retains its discussion and history;
* is excluded from normal active Feed views;
* remains available for historical reference and analysis where appropriate.

Archiving does not delete the Post.

---

## Post State vs Visibility

Post lifecycle and visibility must remain distinct.

```text
Post Status (lifecycle)
ACTIVE / ARCHIVED

Visibility (independent property)
VISIBLE / HIDDEN
```

This allows states such as:

```text
ACTIVE + VISIBLE
ACTIVE + HIDDEN
ARCHIVED + VISIBLE
ARCHIVED + HIDDEN
```

An archived Post should not normally be visible in the active Feed regardless of its prior visibility.

Visibility is not part of the Post lifecycle; it is an independent property of the current Post state.

---

## Post Content Changes

Editing Post content is an update operation.

It does not create a new lifecycle state.

The current content remains authoritative while the history preserves significant previous versions or changes.

---

## Post Reference Changes

Adding, changing, or removing a Post Reference is an update operation.

It does not change the Post lifecycle.

A Post may therefore evolve from:

```text
Post
Reference → Request-123
```

to:

```text
Post
Reference → none
```

without losing the Post itself.

---

## Comment Lifecycle

Comments follow a simpler lifecycle:

```text
ACTIVE
   ↓
HIDDEN
```

HIDDEN is a visibility condition, not a lifecycle state for Comments.

A hidden Comment remains part of the Post discussion history but is excluded from normal discussion display.

The Comment lifecycle does not change the Post lifecycle.

---

# 9. Domain Events

Examples:

```text
PostCreated
PostContentChanged

PostReferenceAdded
PostReferenceChanged
PostReferenceRemoved

PostHidden
PostShown

PostArchived
PostRestored

CommentAdded
CommentChanged
CommentHidden

ReactionAdded
ReactionRemoved
```

For System-Generated Posts, creation may be initiated by an operational event from another domain, for example:

```text
RequestCreated
RequestAssigned
RequestEscalated
RequestResolved
WorkPackageCreated
WorkPackageClosed
```

Those source events remain owned by their originating domains.

The Post Domain only owns the resulting Post and its lifecycle.

The domain events of other domains must not become Post state.

---

## Domain Boundary Summary

```text
Organization
    owns
    → People, roles, responsibilities

Customer
    owns
    → Customer identity and state

Product
    owns
    → Product identity and state

Request
    owns
    → Request identity, ownership, state, resolution

Work Package
    owns
    → Work Package identity, objective, ownership, state,
      Request grouping

Post
    owns
    → Communication, discussion, reactions, references,
      visibility, lifecycle, history
```

The relationship is:

```text
Request / Work Package / Customer / Product / Organization
                         │
                         │ optional reference
                         ▼
                        Post
                         │
              ┌──────────┼──────────┐
              ▼          ▼          ▼
          Comments    Reactions   History
```

Post does not become the owner of any referenced operational object.

The Feed is derived from Post state and is therefore a capability/view, not a domain.