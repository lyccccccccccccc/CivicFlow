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
| Notifications | `GET /notifications`, `POST /notifications/{id}/read` | Authenticated |
| Reporting | `GET /dashboard` | Staff; supports date, category, priority, status and officer filters |
| CSV report | `GET /reports/cases.csv` | Manager/Admin; accepts dashboard filters |
| Category administration | `GET/POST /admin/categories`, `PUT /admin/categories/{id}`, `PUT /admin/categories/{id}/active` | Admin |
| User administration | `GET /admin/users`, `PUT /admin/users/{id}/role`, `PUT /admin/users/{id}/active` | Admin; self-demotion/disable blocked |
| Audit log | `GET /admin/audit-logs` | Admin |

Enums are serialized as readable strings. Validation and authorization failures use standard HTTP status codes and Problem Details where applicable.

`GET /cases` performs filtering, sorting and pagination in the database. Supported query parameters include `page`, `pageSize`, `search`, `priority`, `status`, `categoryId`, `officerId`, `unassigned`, `slaState`, `dueFrom`, `dueTo`, `submittedFrom`, `submittedTo`, `quickView`, `mine`, `sortBy` and `sortDirection`. It returns `{ items, page, pageSize, totalCount, totalPages }`.
