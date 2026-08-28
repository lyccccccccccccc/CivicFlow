# API reference

Base URL: `http://localhost:5168/api`. Protected routes require `Authorization: Bearer <access-token>`.

| Area | Method and route | Access |
| --- | --- | --- |
| Authentication | `POST /auth/register`, `POST /auth/login`, `POST /auth/refresh` | Public |
| Current user | `GET /auth/me`, `POST /auth/logout` | Authenticated |
| Categories | `GET /categories` | Public |
| Requests | `POST /cases`, `GET /cases`, `GET /cases/{id}` | Authenticated; resident ownership enforced |
| Triage | `POST /cases/{id}/triage` | Staff |
| Assignment | `POST /cases/{id}/assign`, `GET /officers` | Manager/Admin |
| Workflow | `POST /cases/{id}/status` | Staff; resident may reopen own resolved case |
| Communication | `POST /cases/{id}/comments` | Authenticated; internal option for staff |
| Notifications | `GET /notifications`, `POST /notifications/{id}/read` | Authenticated |
| Reporting | `GET /dashboard` | Staff |
| CSV report | `GET /reports/cases.csv` | Manager/Admin |
| Administration | `GET /admin/users`, `PUT /admin/users/{id}/role`, `PUT /admin/users/{id}/active`, `POST /admin/categories` | Admin |

Enums are serialized as readable strings. Validation and authorization failures use standard HTTP status codes and Problem Details where applicable.
