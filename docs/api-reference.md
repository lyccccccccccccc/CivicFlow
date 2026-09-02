# API reference

Base URL: `http://localhost:5168/api`. Protected routes require `Authorization: Bearer <access-token>`.

| Area | Method and route | Access |
| --- | --- | --- |
| Authentication | `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh` | Public |
| Current user | `GET /auth/me`, `POST /auth/logout` | Authenticated |
| Categories | `GET /categories` | Public |
| Requests | `POST /cases`, `GET /cases`, `GET /cases/{id}` | Authenticated; resident ownership enforced |
| Triage and SLA | `POST /cases/{id}/triage` | Manager/Admin |
| Assignment | `POST /cases/{id}/assign`, `GET /officers` | Manager/Admin |
| Workflow | `POST /cases/{id}/status` | Staff; resident may reopen own resolved case |
| Communication | `POST /cases/{id}/comments` | Authenticated; internal option for staff |
| Attachments | `GET/POST /cases/{id}/attachments`, `GET /cases/{id}/attachments/{attachmentId}/content`, `DELETE /cases/{id}/attachments/{attachmentId}` | Case owner or authorised staff; officer must be assigned |
| Notifications | `GET /notifications`, `POST /notifications/{id}/read` | Authenticated |
| Reporting | `GET /dashboard` | Staff; supports date, category, priority, status and officer filters |
| CSV report | `GET /reports/cases.csv` | Manager/Admin; accepts dashboard filters |
| Category administration | `GET/POST /admin/categories`, `PUT /admin/categories/{id}`, `PUT /admin/categories/{id}/active` | Admin |
| User administration | `GET /admin/users`, `PUT /admin/users/{id}/role`, `PUT /admin/users/{id}/active` | Admin; self-demotion/disable blocked |
| Audit log | `GET /admin/audit-logs`, `GET /admin/audit-users` | Manager/Admin; read-only |

Enums are serialized as stable identifiers; clients render natural-language labels. Validation and authorization failures use standard HTTP status codes and Problem Details where applicable.

## Phase 2 hardening contracts

Exports above 5000 matching cases are rejected with 400 (narrow the filters), never silently truncated. The Resolved KPI counts only Resolved, matching its linked queue filter; Closed is a separate status.

- `GET /cases/{id}` returns a role-safe business projection, not the audit entity: activity fields are `id`, `type`, `label`, `message`, `section`, `isPublic`, `createdAtUtc`, `actorName`. Sections are `conversation`, `internal`, `progress`. Resident ownership is enforced before projection; internal/admin/priority/SLA/assignment audit data is excluded by a fail-closed allowlist. Staff detail also excludes system audit. Manager/Admin use the separate audit endpoint for the complete original records.
- The resident projection collapses state-toggle bursts under five minutes with no intervening conversation. Legacy resident replies while waiting display `Resident replied. Work has resumed.` No stored history is rewritten.
- `POST /cases/{id}/comments`: send an `Idempotency-Key` header (up to 100 characters) per composed message and reuse it for retries. It is scoped to actor and case. The browser retains the key until success or an edit. Sequential replay is 204; competing writes can return 409 and must refresh. A database unique index protects concurrent replay. Without the header, each request is a distinct message.
- Status and assignment calls targeting the current value are 204 no-ops. Resolution summary, reopen reason and public rejection reason require 10–1800 trimmed characters. Reopen keeps original due dates and all history.
- A notification is keyed by business activity + recipient; SLA alerts use case + original/current target + threshold + recipient. No notification is sent to the actor. Staff public messages and Waiting/Resolved/Closed/Reopened/Rejected notify the resident; assignment/resident reply/reopen and SLA at-risk/overdue notify the officer. `POST /notifications/{id}/read` is owner-scoped and persistently idempotent. Legacy generic `Request updated` notifications are suppressed, not deleted, because they cannot reliably distinguish actor or business event.
- SLA is initialized on submission using category hours added to `SubmittedAtUtc`. Triage with omitted due dates recalculates from that same timestamp; explicit manager overrides are audited. Priority has no implicit duration multiplier: categories remain the approved default service standard. Reopen does not restart the clock. Resolved/Closed display Complete.
- `FirstResponseCompletedAtUtc` is set once, only by the first resident-visible staff `Comment`. Assignment, internal notes and audit actions cannot complete it. `FirstResponseWasBreached` is captured at completion so later SLA overrides cannot rewrite historical performance. Case DTOs expose `firstResponseSlaState`, `resolutionSlaState`, overall `slaState`, `nextSlaDueAtUtc` and `nextSlaTarget`. Completed late responses are `Breached`; unresolved expired targets are `Overdue`.
- Overall SLA evaluates both incomplete targets: any Overdue wins, otherwise any AtRisk wins, otherwise OnTrack. Resolved/Closed overall is Complete. Dashboard overdue filtering, risk table, background notifications and CSV use this same rule; `firstResponseBreached` retains the historical count and links to `firstResponseSlaState=Breached` cases.
- **Active workload** means assigned cases whose status is neither Resolved, Closed nor Rejected. `GET /dashboard` exposes `activeWorkload`, `activeWorkloadDefinition` and `officerWorkload`. All use the database `CaseQuery.ActiveWorkload` predicate and current filters. CSV uses the identical predicate for its `Active workload` column (1/0); sum this column per officer to reproduce the chart. Resolved cases remain in an unfiltered case export with value 0.

`POST /cases` validates trimmed values and returns Problem Details with field-keyed `errors`: title 5–150, description 20–2000, address/location 5–300, and an existing active category. Successful requests trim those three text values before persistence and create both SLA due dates immediately.

`POST /cases` also accepts optional `latitude` and `longitude`; they must be provided together within -90…90 and -180…180. Case detail returns coordinates only after the same owner/staff authorization used for all other case data.

Attachment upload is `multipart/form-data` with `file` and `visibility` (`Public` or `Internal`) plus an `Idempotency-Key` header. JPG/JPEG, PNG and PDF are accepted, up to 10 MB each and five active files per case. Resident uploads are Public only and are limited to editable workflow states. Attachment DTOs expose a server-calculated `canDelete` capability and intentionally omit uploader identifiers, storage keys, Blob URLs and SHA-256. `canDelete` uses the same uploader-ownership, role and lifecycle policy as the DELETE endpoint; DELETE always reauthorizes independently and returns 404 when denied. Downloads return `nosniff`, safe `Content-Disposition`, `Content-Length` and SHA-256 ETag headers; PDFs download as attachments. Unauthorized or cross-resident access returns 404. Delete bodies require `{ "reason": "10–500 characters" }` and perform audited soft deletion.

Case responses are role projections. Residents receive public request identity, content, location, workflow state and public service-target dates/states only; priority, assignment, staff identity, category SLA administration fields and audit-only data are absent. Officers receive assigned-case operational data and the business activity feed, never the full audit log or Manager/Admin account directory. Unknown authenticated roles fail closed.

`GET /cases` performs filtering, sorting and pagination in the database. Supported query parameters include `page`, `pageSize`, `search`, `priority`, `status`, `categoryId`, `officerId`, `unassigned`, `slaState`, `dueFrom`, `dueTo`, `submittedFrom`, `submittedTo`, `quickView`, `mine`, `sortBy` and `sortDirection`. It returns `{ items, page, pageSize, totalCount, totalPages }`.
