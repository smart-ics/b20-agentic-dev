# Operational Awareness Scenarios

## SC-AWR-001

```text id="sc-awr-001"
Scenario ID:
SC-AWR-001

Scenario Name:
Actor Observes Operational Feed

Primary Actor:
All Actors

Goal:
Become aware of current operational activity by observing the shared feed.

Related Domains:
Post

Trigger:
Actor begins an operational session or periodically checks for operational activity.

Expected Outcome:
Actor gains awareness of new and ongoing operational activity visible in the feed.
```

## SC-AWR-002

```text id="sc-awr-002"
Scenario ID:
SC-AWR-002

Scenario Name:
Actor Discovers Request Through Feed

Primary Actor:
Implementator

Goal:
Discover a new or changed Request through its Post presence in the feed.

Related Domains:
Post
Request

Trigger:
A system-generated Post about a Request event appears in the feed.

Expected Outcome:
Actor becomes aware of the Request context through the Post without navigating to Request Detail.
```

## SC-AWR-003

```text id="sc-awr-003"
Scenario ID:
SC-AWR-003

Scenario Name:
Management Observes Exceptions via Feed

Primary Actor:
Management

Goal:
Identify operational exceptions, escalations, or items requiring attention through the feed.

Related Domains:
Post
Request

Trigger:
Management monitors the feed for operational signals.

Expected Outcome:
Management identifies operational matters requiring decision or intervention.
```
