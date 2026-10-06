# ISSUE

## Metadata

ID: CR-019
Type: CHANGE-REQUEST
Status: OPEN
Title: Bulk Multi-line Quick Capture for Work Package Requests in SCR-WP-001

## Source

Reported By: User
Reported Date: 2026-10-06

## Description

When creating a Work Package (`SCR-WP-001: Work Package Screen`), users currently must create the Work Package container first, navigate away to create requests/tasks one by one, and then return to associate them individually into the Work Package. This multi-step process introduces cognitive fatigue and inefficiency when planning or brainstorming operational work packages. Users require an integrated quick-capture mechanism allowing them to draft or paste a multi-line list of tasks directly within the Work Package interface and have them automatically converted into requests linked to the Work Package.

## Desired Outcome

1. Users can capture multiple minimal requests simultaneously via a bulk multi-line text input both during initial Work Package creation (Create Work Package modal) and within the Work Package Detail panel (Scope Management section).
2. The input supports pasting text directly from notes, meeting minutes, or task lists. Line prefixes such as bullets (`-`, `*`, `+`), numbered lists (`1.`, `1)`, `(1)`), and markdown checkboxes (`[ ]`, `[x]`) are stripped automatically, and empty/blank lines are ignored.
3. A "Parse & Preview" interaction parses lines into individual item cards/chips with removal actions (`✕`) before committing, allowing users to verify and prune entries prior to submission.
4. Minimal requests created via quick capture inherit operational context (Customer, Product, and Work Package ID) from the Work Package, default to standard attributes (e.g. `RequestType: GENERAL`, `Priority: NORMAL`, `Status: CAPTURED`), and leave the Request Owner unassigned with an empty Description to be enriched later via Request Detail.
5. In the Create Work Package workflow, the Work Package container is created first, followed by the creation of each parsed request associated with the new Work Package.
6. In case of partial failure during request recording, the created Work Package is preserved and a descriptive warning alerts the user to any tasks that failed to create so they can be re-tried in Scope Management.

## Current Situation

1. In `SCR-WP-001` (`WorkPackageView.vue`), the "Create Work Package" modal only captures container metadata (`name`, `objective`, `ownerPersonId`, `customerId`, `productId`) with no provision to define initial requests.
2. In the Work Package Detail panel Scope Management section, adding requests is limited to single-item selection or manual ID input (`addRequestForm.selectedRequestId`, `manualRequestId`), requiring requests to already exist beforehand.
3. Operational users must repeatedly navigate back and forth between Request capture screens and the Work Package screen to assemble a package scope.

## Evidence

- Screen component: [WorkPackageView.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/views/WorkPackageView.vue#L174-L202)
- Work Package API controller: [WorkPackagesController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/WorkPackagesController.cs#L131-L165)
- Request recording endpoint: [RequestsController.cs](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/backend/Cakra.Api/Controllers/RequestsController.cs#L248-L285)
- Intake interview alignment: Intake interview on 2026-10-06 confirming multi-line text entry, bullet/number stripping, parse & preview verification step, attribute inheritance, and availability in both creation and scope management views.

## Notes

- Intake interview confirmed:
  - Input mode: Bulk Multi-line Text Box with Parse & Preview list.
  - Normalization: Auto-strip bullets/numbers/checkboxes and skip blank lines.
  - Attribute inheritance: Customer and Product inherited from Work Package; Owner unassigned; Type defaults to GENERAL; Priority defaults to NORMAL; Description blank for later enrichment.
  - Dual placement: Available in Create Work Package modal as well as Work Package Detail Scope Management.
  - Resilience: Partial success warning preserves the created container and lists any unrecorded tasks.
- Downstream workflow routing:
  - Stage: Discovery & Feasibility Assessment
  - Owning Role: `ica-analyst`
