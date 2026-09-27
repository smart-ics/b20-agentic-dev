# SCR-REQ-004: Request Search & History

## Purpose

Allow an Implementator to search and retrieve historical Request records to reference past resolutions, check for duplicates, or discover operational precedent.

## Primary Actors

* Implementator

## Entry Points

* Global Navigation → `Requests > Search & History`
* `SCR-REQ-001: Request List` (navigation link)

## Related Use Cases

* UC-COL-002: Search Request History

## Related User Journeys

* UJ-COL-002: Search Request History

## Information Sections

### Section: Search Criteria

Allows the Implementator to specify search parameters.

Displays:

* Keyword search (free text — searches title, description)
* Customer filter (search/select from Customer domain)
* Product filter (search/select from Product domain)
* Request Status filter (all statuses including CLOSED)
* Request Type filter
* Date range filter (created date, closed date)
* Request Owner filter

**Traceability:** UJ-COL-002 step 2 (specifies search criteria such as keywords, customer name, affected product, or date range). UJ-COL-002 alternative path (refining broad results with additional criteria).

### Section: Search Results

Displays matching historical Requests.

Displays:

* Request ID
* Title
* Request Status
* Customer name
* Product name
* Request Owner
* Created date
* Closed date (if closed)
* Resolution outcome (if resolved)
* Summary / description preview

**Traceability:** UJ-COL-002 step 3 (reviews matching historical results). Information Needed: summary attributes of search results (status, date, customer, summary, resolution outcome).

### Section: Result Count

Provides awareness of search scope.

Displays:

* Total number of matching results

**Traceability:** UJ-COL-002 alternative path (Implementator adjusts terms when no results found).

## Available Actions

### Action: Execute Search

* **Actor:** Implementator
* **Expected Outcome:** Search results are populated based on specified criteria.
* **Traceability:** UJ-COL-002 step 3.

### Action: Refine Search

* **Actor:** Implementator
* **Expected Outcome:** Search criteria are adjusted and search re-executed.
* **Traceability:** UJ-COL-002 alternative path (refining broad results).

### Action: Select Result

* **Actor:** Implementator
* **Expected Outcome:** Actor navigates to `SCR-REQ-003: Request Detail` for the selected historical Request.
* **Traceability:** UJ-COL-002 step 4 (selects a relevant historical Request to examine).

## Navigation Destinations

* `SCR-REQ-003: Request Detail` (select a search result)

## Layout Sketch

```
+--------------------------------------------------+
| Global Navigation Bar                            |
+--------------------------------------------------+

+--------------------------------------------------+
| Screen Title: Request Search & History           |
+--------------------------------------------------+

+--------------------------------------------------+
| Search Criteria                                  |
| Keywords:  [________________________]            |
| Customer:  [____________] Product: [____________]|
| Status:    [____________] Type:    [____________]|
| Owner:     [____________]                        |
| Date Range:[____] to [____]       [Search]       |
+--------------------------------------------------+

+--------------------------------------------------+
| Result Count: N results found                    |
+--------------------------------------------------+

+--------------------------------------------------+
| Search Results                                   |
| ID | Title | Status | Customer | Product | Owner |
|    | Created | Closed | Resolution                |
| ------------------------------------------------ |
| Row 1                                            |
| Row 2                                            |
| Row 3                                            |
| ...                                              |
+--------------------------------------------------+

+--------------------------------------------------+
| Pagination                                       |
+--------------------------------------------------+
```
