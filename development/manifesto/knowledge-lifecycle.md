---
## Knowledge Classification and Lifecycle
The SDLC uses three classes of knowledge artifacts:

### 1. Permanent Knowledge

Permanent Knowledge is the authoritative knowledge base of the system.

Permanent Knowledge consists of:

* DOMAIN = capability knowledge
* FEATURE = outcome knowledge
* ARCHITECTURE = realization knowledge

Rules:

* Permanent Knowledge must remain up-to-date with the implemented system.
* Permanent Knowledge is the source of truth for business capabilities (DOMAIN), user outcomes (FEATURE), and technical realization (ARCHITECTURE).
* When the implemented system changes, the affected Permanent Knowledge artifacts must be updated accordingly.
* If implementation materially changes architectural knowledge, ARCHITECTURE must be created or updated. If it does not, no new ARCHITECTURE artifact is required.
* Agents must not treat outdated Permanent Knowledge as authoritative.
* Permanent Knowledge must not be physically deleted as part of normal workflow.
* Historical versions are preserved by Git rather than inside the artifact itself.

### 2. Working Knowledge

Working Knowledge exists to support a development activity or workflow.

Working Knowledge may include:

* ISSUE
* FEASIBILITY-ASSESSMENT
* BUG-INVESTIGATION
* IMPLEMENTATION-PLAN
* REVIEW
* TEST-EXECUTION
* DEPLOYMENT artifacts
* Other temporary artifacts required during delivery

Rules:

* Working Knowledge is not the permanent source of truth.
* Working Knowledge may contain analysis, decisions, execution records, findings, and other information required to complete a workflow.
* Once the related work has been completed and the relevant knowledge has been incorporated into Permanent Knowledge, Working Knowledge may be physically deleted or archived.
* Agents must not assume that Working Knowledge remains available permanently.
* Deleting Working Knowledge must not remove knowledge that is required to understand, implement, operate, or maintain the system. Such knowledge must first be reflected in the appropriate Permanent Knowledge artifact.
* It may be deleted only after all knowledge necessary for future understanding and maintenance has been incorporated into Permanent Knowledge.

### 3. Generated Knowledge

Generated Knowledge is derived from Permanent Knowledge for operational or communication purposes.

Generated Knowledge may include:

* SOP
* User Guide
* Training Material
* Release Notes
* Other derived documentation

Rules:

* Generated Knowledge is not an independent source of truth.
* Generated Knowledge must be derived from the current Permanent Knowledge.
* Generated Knowledge may be regenerated whenever Permanent Knowledge changes.
* Generated Knowledge may be physically deleted and regenerated without loss of authoritative system knowledge.
* Agents must not update Generated Knowledge in a way that contradicts Permanent Knowledge.

### Knowledge Lifecycle Principle

The lifecycle of knowledge is:

```text
Working Knowledge
      ↓
Delivery / Decision / Execution
      ↓
Permanent Knowledge updated
      ↓
Working Knowledge may be deleted

Permanent Knowledge
      ↓
Generation
      ↓
Generated Knowledge
```

The authoritative state of the system is always represented by the current Permanent Knowledge:

```text
DOMAIN + FEATURE + ARCHITECTURE
```

Working and Generated Knowledge exist to support or communicate that authoritative knowledge and may be removed when no longer required.
