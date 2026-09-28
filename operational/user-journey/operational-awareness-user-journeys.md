# Operational Awareness User Journeys

Derived from **Operational Awareness Use Cases** (`operational/use-cases/operational-awareness-use-cases.md`) using the **User Journey Generation Skill** (`operational/user-journey/user-journey-generation-skill.md`).

Canonical actors follow the **CAKRA - ICS Operational Actor Model** (`operational/actors/actor-model.md`):
* **Implementator:** Organizational Role responsible for handling operational demands, documenting progress, collaborating on requests, and managing requests for which they are responsible.
* **Management:** Organizational Role responsible for defining organizational structure, monitoring operational health, assessing workload and performance, and intervening in assignments or operational decisions.

---

## UJ-AWR-001: Observe Operational Feed

**Use Case Reference:** UC-AWR-001

### Actor
All Actors (Implementator, Management, Request Owner)

### Goal
Gain awareness of current operational activity.

### Trigger
Actor opens the system or begins an operational session.

### Main Journey
1. Actor navigates to the Operational Feed.
2. Actor scans the chronological stream of Posts for recent operational activity.
3. Actor observes summaries of new Requests, status changes, discussions, and operational communications.
4. Actor identifies Posts that are relevant to their operational context.

### Alternative Paths
* **Filtered entry:** Actor arrives at the feed already filtered by a specific Customer, Product, or Team.
* **No new activity:** Actor confirms no new operational activity since their last session.

### Success Outcome
Actor possesses current awareness of organizational operational activity.

### Information Needed
* Chronological stream of Posts with author, content summary, referenced operational objects, comment count, and reaction summary
* Timestamps indicating recency

---

## UJ-AWR-002: Discover Request via Feed

**Use Case Reference:** UC-AWR-002

### Actor
Implementator

### Goal
Become aware of a new or changed Request through its Post presence in the feed.

### Trigger
A system-generated Post about a Request event appears in the feed.

### Main Journey
1. Actor observes a system-generated Post indicating a Request event (creation, assignment, escalation, resolution).
2. Actor reads the Post content to understand the Request context.
3. Actor reviews any existing Comments or Reactions on the Post.
4. Actor determines whether the Request requires their attention or participation.

### Alternative Paths
* **Immediate action:** Actor decides to comment or react directly on the Post.
* **Deeper investigation:** Actor navigates to the Request Detail via the Post Reference.

### Success Outcome
Actor understands the Request context and has determined whether action is needed.

### Information Needed
* Post content and source event type
* Referenced Request summary (title, status, owner, customer)
* Existing Comments and Reactions on the Post

---

## UJ-AWR-003: Monitor Operational Exceptions via Feed

**Use Case Reference:** UC-AWR-003

### Actor
Management

### Goal
Identify operational matters requiring decision or intervention.

### Trigger
Management reviews the feed for operational health monitoring.

### Main Journey
1. Management navigates to the Operational Feed.
2. Management scans for Posts indicating escalations, stalled progress, or items requiring attention.
3. Management reviews Reactions on Posts to identify items where team members have signaled issues (NEED_CLARIFICATION, SIMILAR_ISSUE, etc.).
4. Management determines which operational matters require intervention.

### Alternative Paths
* **Filtered review:** Management filters the feed by a specific Customer or Product to focus attention.
* **Delegation:** Management comments on a Post to provide direction or delegate action.

### Success Outcome
Management has identified operational exceptions and determined appropriate responses.

### Information Needed
* Posts with operational signals (escalations, stalled items)
* Reaction summaries indicating team member signals
* Referenced Request status and ownership
