# CivicFlow MVP product scope

## Problem

Residents need a clear way to report community issues and understand what is happening after submission. Internal teams need a consistent workflow for triage, assignment, communication, service-level monitoring and closure.

## MVP service categories

1. Roads and footpaths
2. Waste and recycling
3. Parks and trees
4. Public facilities

Each category has configurable first-response and resolution targets.

## Included capabilities

- Resident registration and authentication
- Service request creation with address and optional coordinates
- Resident request list, detail and public timeline
- Staff triage, priority and assignment
- Controlled case status transitions
- Public comments and staff-only internal notes
- Requests for additional resident information
- SLA calculation, overdue dashboard state and breach notifications
- In-application notifications
- Audit history
- Manager dashboard and CSV export
- Private JPG/PNG/PDF attachments with lifecycle authorization
- Optional map coordinates and read-only case maps

## Deliberately excluded from MVP

- Government identity providers and real government integrations
- AI classification and chatbots
- Native mobile applications
- SMS notifications
- Advanced GIS and routing
- Multi-agency transfers
- Multilingual content
- Microservices and Kubernetes
- Satisfaction surveys

## Success criteria

- A resident can complete and track a request without staff assistance.
- A manager can assign a submitted request to an officer.
- An officer can progress a case only through allowed transitions.
- Resident APIs never expose internal notes.
- SLA targets and breaches are calculated consistently.
- Core domain rules and endpoints have automated tests.
- The application can be run locally from documented steps and deployed through CI/CD.
