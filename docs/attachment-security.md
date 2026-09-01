# Attachment security and operations

- The `case-attachments` container is private. Local development uses Azurite; Azure production uses managed identity and RBAC, not an account key in configuration.
- API authorization is re-evaluated for list, upload, download and delete. Residents see Public files on their own cases only. Case Officers see only assigned cases. Managers/Admins can access authorised staff views. Cross-owner and hidden-resource requests return 404.
- Uploads allow JPG/JPEG, PNG and PDF only. Extension, declared content type, magic signature, 10 MB size, image decodability and a 40-megapixel ceiling are checked. Original names are Unicode-normalized and stripped of path/control/dangerous characters. Blob keys are server-generated GUID paths.
- SHA-256 supports integrity, idempotency and ETags; it and the storage key never leave the API or enter application logs. PDFs use attachment disposition and all downloads use `X-Content-Type-Options: nosniff`.
- Delete is an audited soft delete with a required reason. The blob remains private for 30 days, after which the maintenance worker physically removes it. Reconciliation also removes old unreferenced blobs left by abnormal termination.
- **Phase 3A does not scan for malware. A production launch must add asynchronous malware scanning/quarantine, block download until a clean verdict, define incident handling and monitor scanner failures.**
- Logs must contain case/attachment IDs only, never connection strings, blob URLs, storage keys, tokens or original file content.
