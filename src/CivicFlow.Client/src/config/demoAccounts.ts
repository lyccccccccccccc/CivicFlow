const demoRoles = ['resident', 'officer', 'manager', 'admin'] as const
export function developmentDemoEmails(domain = import.meta.env.VITE_DEMO_ACCOUNT_DOMAIN ?? 'civicflow.local') { const safeDomain = domain?.trim().toLowerCase(); if (!safeDomain || !/^[a-z0-9.-]+$/.test(safeDomain)) return []; return demoRoles.map(role => `${role}@${safeDomain}`) }
