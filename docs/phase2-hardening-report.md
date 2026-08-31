# Phase 2 hardening 验收报告

日期：2026-08-31。分支：`fix/phase-2-hardening`。基线：`ea96846`（开始时工作区干净）。

## 已完成

- 后端按角色返回业务 Activity DTO，Resident 不获得内部备注、priority/SLA/assignment 或系统管理审计；Officer 详情也不返回系统 Audit。
- Resident 分为 Request progress / Conversation；Staff 分为 Conversation / Internal notes / Case activity。Manager/Admin 使用独立完整 Audit Log。
- 历史审计保持原文。Resident 的短时状态切换合并为业务里程碑，旧回复补出 “Resident replied. Work has resumed.”。
- 按角色限制通知类型，排除操作者本人；业务事件/接收人唯一键、消息重试键、同状态/同分配 no-op、乐观并发 409 和持久化已读。
- Resolution / Reopen / Rejected 的公开 summary 或原因要求 10–1800 字符；Reopen 有确认对话框，保留历史与原 SLA。
- SLA 从 SubmittedAt 起算；Manager Recalculate 使用原始提交时间。采用分类默认小时数，不新增未定义的 Priority 倍率。显式 due date 覆盖仍允许并审计。
- Active workload 排除 Resolved/Closed/Rejected，数据库查询、Dashboard、CSV 使用同一 predicate。Resolved KPI 与点击后的队列一致；大于 5000 条的 CSV 拒绝导出并要求缩小筛选，不返回误导性的部分结果。
- 启动仅在新数据库首次创建时 seed，既有数据库不会被重新赋予默认用户角色或分类。

## 增量数据库升级

原项目没有 EF migration baseline，采用 `Phase2Upgrade` 幂等事务升级，并用 SQL application lock 串行执行：

1. 增加 nullable `CaseActivities.OperationKey` / `UserNotifications.EventKey`。
2. 增加 ActorId+OperationKey 和 UserId+EventKey 的 filtered unique indexes。
3. 仅对两个 SLA target 都为空的历史记录，按原 SubmittedAt 补默认目标并追加 Audit；已有 target 不修改。
4. 不删除表、记录或数据卷，不重新 seed；没有生成“重建数据库”迁移。

原验收工单 `CF-20260831-36657C`（ID `83bf5143-ba4e-4e06-a372-81867f1f34c1`）保持 InProgress / Medium；SubmittedAt `2026-08-31T12:18:53.737212+10:00`、ResolutionDue `2026-09-05T12:34:00+10:00` 均与修改前一致。此工单只做读取验收。

真实 SQL 测试新增并保留了带 Phase2 hardening / Workload / Integration category / integration test 标识的测试记录；没有清空现有 E2E 数据。浏览器写操作使用本轮生成的工单（包括 CF-20260831-741429、CF-20260831-30CA1B），没有修改原验收工单。

## 自动化与安全检查

| 检查 | 结果 |
| --- | --- |
| dotnet restore | 通过 |
| dotnet build | Debug / Release 均通过，0 warning / 0 error |
| dotnet test | 9 单元 + 20 集成测试，通过 |
| 真实 SQL Server 集成 suite | 20/20 通过（包含真实唯一约束、DB 查询、工作流与权限） |
| npm install | 按要求跳过：依赖未改变且 node_modules 存在 |
| npm run lint | 通过 |
| npm run build | 通过 |
| NuGet vulnerable + transitive | 所有项目未发现已知漏洞 |
| npm audit --json | 0 vulnerabilities |
| git diff --check | 通过 |

新回归包含：角色投影 fail-closed、历史技术状态降噪、Manager/Admin 完整 audit、Officer audit 403、内部备注保密、resident reply 自动恢复、无 self notification、重试去重、no-op 无新增 audit/notification、Mark read 持久化、所有终止状态 workload 排除、SLA SubmittedAt 基准、summary/reopen 校验、audit append-only 与 SQL unique index。

SQL 与 API 实时检查：Active workload API=11、CSV sum=11；Resolved API=10、CSV rows=10（该快照在后续增量测试前取得，测试记录增加时绝对数量会变化，统计口径不变）。

## 浏览器验收

- Resident：登录、请求列表、原工单安全投影、公开 resolution summary、Complete、必填 reopen reason / 二次确认、通知 Mark read 后刷新保持已读。
- Officer：默认 `/cases?mine=true`；三个业务分区；公开消息/内部备注；Reopen 通知 Open 指向正确工单；Start work、最小 summary 限制、Resolve。
- Manager：Dashboard 自然语言与 Active workload；独立 Audit 入口、按原工单筛选可见 PriorityChanged / InternalNote；详情 Recalculate 按原提交时间计算（API 二次核对成功）。
- Admin：分类/状态/SLA 列表、自我 role/status 控件禁用、完整 Audit 与 SlaChanged 筛选。
- 四角色验收期间 browser console error 日志为空。SQL Server healthy、API /health=Healthy、前端 HTTP 200；服务保持运行。

## 已知限制 / 未解决项

- Vite 主 bundle 557.50 kB，仍有 >500 kB 的非阻断性能提示；本次未引入 Phase 3 或做无关代码拆分。
- 旧版 generic “Request updated” 通知缺少可靠业务事件和操作者关联，保留数据库原记录但不再投递显示；不伪造旧通知幂等关系。
- 原先已设置的 SLA 不做追溯覆盖（包括历史上基于分诊时间的目标），以保留既有承诺与审计真实性；今后的提交和 Recalculate 使用新规则。
- 附件、地图、邮件、短信等 Phase 3 功能未开发。
- 本次列出的 Phase 2 hardening 功能无已知阻断问题；真实测试新增记录按“不清理现有数据”的要求保留。

## 修改文件（完整清单）

- [docs/api-reference.md](docs/api-reference.md)
- [docs/architecture.md](docs/architecture.md)
- [src/CivicFlow.Api/Background/SlaMonitorWorker.cs](src/CivicFlow.Api/Background/SlaMonitorWorker.cs)
- [src/CivicFlow.Api/Common/ApiExceptionHandler.cs](src/CivicFlow.Api/Common/ApiExceptionHandler.cs)
- [src/CivicFlow.Api/Common/CaseQuery.cs](src/CivicFlow.Api/Common/CaseQuery.cs)
- [src/CivicFlow.Api/Controllers/AdminController.cs](src/CivicFlow.Api/Controllers/AdminController.cs)
- [src/CivicFlow.Api/Controllers/CasesController.cs](src/CivicFlow.Api/Controllers/CasesController.cs)
- [src/CivicFlow.Api/Controllers/OperationsController.cs](src/CivicFlow.Api/Controllers/OperationsController.cs)
- [src/CivicFlow.Api/Program.cs](src/CivicFlow.Api/Program.cs)
- [src/CivicFlow.Client/src/App.tsx](src/CivicFlow.Client/src/App.tsx)
- [src/CivicFlow.Client/src/api/client.ts](src/CivicFlow.Client/src/api/client.ts)
- [src/CivicFlow.Client/src/components/AppShell.tsx](src/CivicFlow.Client/src/components/AppShell.tsx)
- [src/CivicFlow.Client/src/components/ProtectedRoute.tsx](src/CivicFlow.Client/src/components/ProtectedRoute.tsx)
- [src/CivicFlow.Client/src/pages/AuditLogPage.tsx](src/CivicFlow.Client/src/pages/AuditLogPage.tsx)
- [src/CivicFlow.Client/src/pages/CaseDetailPage.tsx](src/CivicFlow.Client/src/pages/CaseDetailPage.tsx)
- [src/CivicFlow.Client/src/pages/CasesPage.tsx](src/CivicFlow.Client/src/pages/CasesPage.tsx)
- [src/CivicFlow.Client/src/pages/DashboardPage.tsx](src/CivicFlow.Client/src/pages/DashboardPage.tsx)
- [src/CivicFlow.Client/src/pages/NotificationsPage.tsx](src/CivicFlow.Client/src/pages/NotificationsPage.tsx)
- [src/CivicFlow.Domain/Entities/CaseActivity.cs](src/CivicFlow.Domain/Entities/CaseActivity.cs)
- [src/CivicFlow.Domain/Entities/ServiceRequest.cs](src/CivicFlow.Domain/Entities/ServiceRequest.cs)
- [src/CivicFlow.Domain/Entities/UserNotification.cs](src/CivicFlow.Domain/Entities/UserNotification.cs)
- [src/CivicFlow.Infrastructure/Persistence/ApplicationDbContext.cs](src/CivicFlow.Infrastructure/Persistence/ApplicationDbContext.cs)
- [src/CivicFlow.Infrastructure/Persistence/DatabaseSeeder.cs](src/CivicFlow.Infrastructure/Persistence/DatabaseSeeder.cs)
- [tests/CivicFlow.IntegrationTests/ApiSmokeTests.cs](tests/CivicFlow.IntegrationTests/ApiSmokeTests.cs)
- [tests/CivicFlow.IntegrationTests/StaffOperationsTests.cs](tests/CivicFlow.IntegrationTests/StaffOperationsTests.cs)
- [src/CivicFlow.Api/Common/ActivityFeed.cs](src/CivicFlow.Api/Common/ActivityFeed.cs)
- [src/CivicFlow.Infrastructure/Persistence/Phase2Upgrade.cs](src/CivicFlow.Infrastructure/Persistence/Phase2Upgrade.cs)
- [tests/CivicFlow.IntegrationTests/Phase2HardeningTests.cs](tests/CivicFlow.IntegrationTests/Phase2HardeningTests.cs)
- [docs/phase2-hardening-report.md](docs/phase2-hardening-report.md)
