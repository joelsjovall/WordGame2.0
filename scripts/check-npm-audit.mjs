import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { evaluateNpmAudit } from './npm-audit-policy.mjs'

const auditOptions = {
  cwd: fileURLToPath(new URL('../frontend/', import.meta.url)),
  encoding: 'utf8',
}
const audit =
  process.platform === 'win32'
    ? spawnSync('cmd.exe', ['/d', '/s', '/c', 'npm audit --json'], auditOptions)
    : spawnSync('npm', ['audit', '--json'], auditOptions)

if (audit.error || ![0, 1].includes(audit.status)) {
  throw new Error('npm audit could not run successfully.', {
    cause: audit.error,
  })
}

const report = JSON.parse(audit.stdout)
const findings = evaluateNpmAudit(report)

let failures = 0
for (const { advisory, allowed, blocking } of findings) {
  console.log(
    `${blocking ? 'FAIL' : allowed ? 'KNOWN EXCEPTION' : 'NOTICE'}: ${advisory.dependency} (${advisory.severity}) ${advisory.url}`,
  )
  if (blocking) failures++
}

console.log('npm vulnerability totals:', report.metadata.vulnerabilities)
if (failures) process.exitCode = 1
