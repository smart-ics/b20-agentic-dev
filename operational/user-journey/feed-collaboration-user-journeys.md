# Feed Collaboration User Journeys

Derived from **Feed Collaboration Use Cases** (`operational/use-cases/feed-collaboration-use-cases.md`) using the **User Journey Generation Skill** (`operational/user-journey/user-journey-generation-skill.md`).

Canonical actors follow the **CAKRA - ICS Operational Actor Model** (`operational/actors/actor-model.md`):
* **Implementator:** Organizational Role responsible for handling operational demands, documenting progress, collaborating on requests, and managing requests for which they are responsible.
* **Management:** Organizational Role responsible for defining organizational structure, monitoring operational health, assessing workload and performance, and intervening in assignments or operational decisions.

---

## UJ-FCOL-001: Comment on Post

**Use Case Reference:** UC-FCOL-001

### Actor
All Actors

### Goal
Contribute a comment to an operational discussion.

### Trigger
Actor observes a Post and has information, clarification, or perspective to contribute.

### Main Journey
1. Actor locates the Post in the Operational Feed or Post Detail view.
2. Actor reviews existing Comments to understand the current discussion and avoid duplication.
3. Actor composes and submits a Comment.

### Alternative Paths
* **Comment from feed:** Actor adds a Comment directly from the feed view without opening Post Detail.
* **Comment from Request Detail:** Actor adds a Comment via the related Posts section on Request Detail.

### Success Outcome
Comment is recorded as part of the Post discussion and visible to all stakeholders.

### Information Needed
* Post content and existing Comments

---

## UJ-FCOL-002: React to Post

**Use Case Reference:** UC-FCOL-002

### Actor
All Actors

### Goal
Express a structured operational reaction to a Post or Comment.

### Trigger
Actor observes a Post or Comment that warrants an operational signal.

### Main Journey
1. Actor locates the Post or Comment in the Operational Feed or Post Detail view.
2. Actor selects the appropriate Reaction type (Seen, Experienced, Have Idea, Similar Issue, Duplicate, Need Clarification).
3. Actor confirms the Reaction.

### Alternative Paths
* **Remove Reaction:** Actor changes their mind and removes a previously expressed Reaction.

### Success Outcome
Reaction is recorded and visible as an operational signal to all stakeholders.

### Information Needed
* Available Reaction types and their operational meanings

---

## UJ-FCOL-003: Create Operational Post

**Use Case Reference:** UC-FCOL-003

### Actor
Implementator

### Goal
Author a new operational communication.

### Trigger
Actor needs to communicate, explain, share, or preserve operational knowledge.

### Main Journey
1. Actor initiates Post creation from the Operational Feed or from a Request Detail.
2. Actor enters the Post title and content.
3. Actor optionally associates the Post with a referenced operational object (Request, Work Package, Customer, Product).
4. Actor reviews and submits the Post.

### Alternative Paths
* **Post without reference:** Actor creates a standalone Post not referencing any operational object.
* **Post from Request Detail:** Actor creates a Post pre-populated with a reference to the current Request.

### Success Outcome
Post is created and visible in the Operational Feed.

### Information Needed
* Post content (title, body)
* Available operational objects for reference (Requests, Work Packages, Customers, Products)

---

## UJ-FCOL-004: Navigate from Post to Request

**Use Case Reference:** UC-FCOL-004

### Actor
All Actors

### Goal
Access the structured Request record referenced by a Post.

### Trigger
Actor needs structured Request data beyond what the Post provides.

### Main Journey
1. Actor locates a Post that references a Request in the Operational Feed or Post Detail view.
2. Actor follows the Post Reference link to the Request.
3. Actor arrives at Request Detail with full structured operational context.

### Alternative Paths
* **Return to feed:** Actor returns to the Operational Feed after reviewing the Request Detail.
* **Return to Post Detail:** Actor returns to the Post discussion after reviewing the Request.

### Success Outcome
Actor views the structured Request record with full operational context.

### Information Needed
* Post Reference identifying the Request

---

## UJ-FCOL-005: Filter Feed by Context

**Use Case Reference:** UC-FCOL-005

### Actor
All Actors

### Goal
Focus the feed on a specific operational context.

### Trigger
Actor needs to view operational activity for a specific Customer, Product, or Team.

### Main Journey
1. Actor accesses filter controls on the Operational Feed.
2. Actor selects filter criteria (Customer, Product, Team, or other operational context).
3. Actor reviews the filtered stream of Posts.

### Alternative Paths
* **Clear filter:** Actor removes filters to return to the full feed.
* **Combined filters:** Actor applies multiple filter criteria simultaneously.

### Success Outcome
Feed displays only Posts matching the selected operational context.

### Information Needed
* Available filter criteria (Customers, Products, Teams)
