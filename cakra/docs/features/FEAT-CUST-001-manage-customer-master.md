---
Title: Manage Customer Master Data and Contacts
Code: FEAT-CUST-001
Artifact: FEATURE
Version: 2.0
LastUpdated: 2026-10-05
---

# 1. Purpose

Provides a dedicated administrative interface for creating, updating, activating, and deactivating Customer organizations, as well as managing associated Customer Contact Persons, ensuring authoritative customer organizational data is maintained within the Customer domain while remaining available for operational demand capture and tracking.

# 2. Business Outcome

Customer organizational records and representative contact persons are created and maintained by authorized Administrators through a dedicated management screen with search, metric summaries, and lifecycle controls, ensuring operational requests and historical references have accurate customer representation.

# 3. Participating Domains

## Customer
- Owns the authoritative `Customer` and `Customer Contact` aggregates and their identity/state attributes.
- Owns the Customer lifecycle state machine (`ACTIVE` <-> `INACTIVE`) and Customer Contact lifecycle (`ACTIVE` <-> `INACTIVE`).
- Provides the write commands (`CreateCustomer`, `UpdateCustomer`, `ActivateCustomer`, `DeactivateCustomer`, `CreateCustomerContact`, `UpdateCustomerContact`) and read queries consumed by the management UI.

# 4. Trigger

An authenticated Administrator navigates to the Customer Management screen (`SCR-CUST-001`) and invokes create, edit, activate/deactivate, or contact management actions.

# 5. Preconditions

- The actor is authenticated.
- The actor possesses the `Administrator` role (or `Admin`).
- The Customer domain write services and query endpoints are available.

# 6. Operational Flow

### 1. View Customer Records and Metrics
1. The Administrator opens the Customer Management screen.
2. The system retrieves all Customer records (both active and inactive) along with their status and contact associations.
3. The system displays summary KPI metric cards (Total Customers, Active Customers, Inactive Customers, Active Maintenance Contracts).
4. The system presents a high-density, searchable and filterable table displaying Customer Code, Customer Name, Active Maintenance Contract indicator, Status badge, and Management Actions.

### 2. Create New Customer Organization
1. The Administrator clicks "Add Customer" to open the customer creation modal.
2. The Administrator enters Customer Code, Customer Name, and sets the Active Maintenance Contract flag.
3. The Administrator submits the form.
4. The system validates the inputs:
   - Verifies Customer Code is non-empty, alphanumeric/uppercase, and unique.
   - Verifies Customer Name is non-empty.
5. The system persists the new Customer in `ACTIVE` status and refreshes the customer list and metrics.

### 3. Update Customer Details
1. The Administrator clicks "Edit" on a Customer row.
2. The modal displays existing customer attributes.
3. The Administrator updates Customer Name and/or Active Maintenance Contract status.
4. The Administrator submits the updates.
5. The system validates the modified fields, persists the changes, and refreshes the list view.

### 4. Manage Customer Contact Persons
1. The Administrator clicks "Contacts" on a Customer row.
2. The system displays the list of contact persons associated with that customer.
3. The Administrator can add a new contact (Name, Position, Email, Phone Number) or edit an existing contact's details and active/inactive status.
4. The system validates contact inputs (valid email format if provided, required name) and persists the contact changes linked to the parent Customer.

### 5. Deactivate Customer
1. The Administrator clicks "Deactivate" on an active Customer row.
2. The system transitions the Customer status from `ACTIVE` to `INACTIVE`.
3. The Customer is excluded from active operational selection dropdowns while preserving all historical references.

### 6. Activate Customer
1. The Administrator clicks "Activate" on an inactive Customer row.
2. The system transitions the Customer status from `INACTIVE` to `ACTIVE`.
3. The Customer becomes available again for new operational demand creation.

# 7. Domain Orchestration

- **Customer**: Owns the `Customer` and `Customer Contact` aggregates and lifecycles. Executes customer and contact mutations through its application services, enforcing code uniqueness and lifecycle state transitions. Emits domain events (`CustomerCreated`, `CustomerActivated`, `CustomerInactivated`, `CustomerContactAdded`, `CustomerContactActivated`, `CustomerContactInactivated`).

# 8. Constraints

- Only users holding the `Administrator` role may access Customer Management.
- Every `Customer` must have a unique `CustomerCode`.
- A `Customer` must not be physically removed; lifecycle transitions (`ACTIVE` <-> `INACTIVE`) preserve historical integrity.
- A `Customer Contact` must strictly belong to exactly one Customer organization.
- Maintenance contract indicator reflects current authoritative status only.

# 9. Exceptions

- **Unauthorized Access**: If a non-administrator attempts to access Customer Management, access is blocked and redirected.
- **Duplicate Customer Code**: If a customer creation request uses an existing Customer Code, the system displays a conflict error message.
- **Target Customer Not Found**: If an update or lifecycle operation targets a nonexistent Customer ID, the system returns a not-found error.
- **Invalid Contact Data**: If contact information contains malformed email or missing required fields, submission is prevented with inline validation feedback.

# 10. Acceptance Criteria

- [ ] Navigation menu displays "Customer Management" under Administration only when the authenticated user possesses the `Administrator` role.
- [ ] Direct URL navigation to `/admin/customers` by non-administrators is blocked and redirected to `/feed`.
- [ ] Administrator can view summary KPI metrics: Total Customers, Active Customers, Inactive Customers, Active Maintenance Contracts.
- [ ] Administrator can search customers by code and name, and filter by status (All, Active, Inactive).
- [ ] Administrator can open "Add Customer" modal to create a new customer with code, name, and maintenance contract flag.
- [ ] Administrator can edit existing customer name and maintenance contract flag.
- [ ] Administrator can view and manage contact persons (add contact, edit contact, toggle status) for any customer.
- [ ] Administrator can activate or deactivate a customer with one-click action and confirmation.
- [ ] All customer state changes immediately update the table and KPI metrics.
