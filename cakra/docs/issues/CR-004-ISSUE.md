# ISSUE

## Metadata

ID: CR-004
Type: CHANGE-REQUEST
Status: OPEN
Title: Remove Request and Request Search from Left Sidebar Menu

## Source

Reported By: User
Reported Date: 2026-10-03

## Description

The user requested the removal of the "Request" and "Request Search" menu items from the left sidebar navigation menu.

## Desired Outcome

1. The "Request" (Request List) menu item is removed from the left sidebar navigation menu.
2. The "Request Search" menu item is removed from the left sidebar navigation menu.
3. The left sidebar menu retains other designated operational and management navigation items without displaying "Request" or "Request Search".

## Current Situation

1. The application shell navigation in `Cakra.Web` (`App.vue`) provides direct left sidebar navigation links for "Requests" (`/requests`, `SCR-REQ-001`) and "Request Search" (`/requests/search`, `SCR-REQ-004`).
2. Global navigation specifications and maps (`navigation-map.md`, `request-navigation.md`, `collaboration-navigation.md`) define `SCR-REQ-001: Request List` and `SCR-REQ-004: Request Search & History` as primary sidebar/global entry points within the Requests Area.

## Evidence

- User directive: "Please remove menu: - Request - Request Search From left side bar menu"
- Sidebar navigation component: [App.vue](file:///d:/Project.Aktif/b20-agentic-dev/cakra/src/frontend/Cakra.Web/src/App.vue#L114-L149)
- Navigation map: [navigation-map.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/navigation-map.md#L30-L38)
- Request navigation: [request-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/request-navigation.md)
- Collaboration navigation: [collaboration-navigation.md](file:///d:/Project.Aktif/b20-agentic-dev/cakra/docs/navigation/collaboration-navigation.md)

## Notes

This artifact captures the intake request in a solution-neutral manner. Detailed feasibility assessment, evaluation of alternative navigation paths/drill-downs for `SCR-REQ-001` and `SCR-REQ-004`, impact on navigation documentation, and frontend implementation updates belong to downstream analysis, architecture, and implementation stages.
