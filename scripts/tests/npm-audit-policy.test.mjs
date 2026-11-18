import assert from 'node:assert/strict'
import test from 'node:test'
import { evaluateNpmAudit } from '../npm-audit-policy.mjs'

const reviewDate = new Date('2026-11-18T00:00:00Z')
const known = {
  dependency: 'picomatch',
  severity: 'high',
  url: 'https://github.com/advisories/GHSA-fc5q-39xc-vjpf',
}
const report = (...advisories) => ({
  vulnerabilities: advisories.length ? { fixture: { via: advisories } } : {},
  metadata: { vulnerabilities: { total: advisories.length } },
})

test('a clean audit has no findings', () => {
  assert.deepEqual(evaluateNpmAudit(report(), reviewDate), [])
})

test('the existing high picomatch advisory has a narrow, expiring exception', () => {
  const [finding] = evaluateNpmAudit(report(known), reviewDate)
  assert.equal(finding.allowed, true)
  assert.equal(finding.blocking, false)
  assert.equal(
    evaluateNpmAudit(report(known), new Date('2026-12-18T00:00:00Z'))[0]
      .blocking,
    true,
  )
})

test('new high and critical advisories fail even alongside an allowed advisory', () => {
  for (const severity of ['high', 'critical']) {
    const findings = evaluateNpmAudit(
      report(known, {
        ...known,
        severity,
        url: 'https://example.test/new-advisory',
      }),
      reviewDate,
    )
    assert.equal(findings.filter((finding) => finding.blocking).length, 1)
  }
})

test('the same URL cannot exempt another package or a critical finding', () => {
  for (const advisory of [
    { ...known, dependency: 'another-package' },
    { ...known, severity: 'critical' },
  ]) {
    assert.equal(
      evaluateNpmAudit(report(advisory), reviewDate)[0].blocking,
      true,
    )
  }
})

test('moderate findings remain visible without failing the high threshold', () => {
  const [finding] = evaluateNpmAudit(
    report({ ...known, severity: 'moderate' }),
    reviewDate,
  )
  assert.equal(finding.allowed, false)
  assert.equal(finding.blocking, false)
})

test('registry errors and incomplete vulnerability reports fail closed', () => {
  for (const invalid of [
    { error: { code: 'EAUDIT' } },
    {},
    { ...report(), metadata: { vulnerabilities: { total: 1 } } },
    report('missing-package'),
    report({ ...known, severity: 'unknown' }),
  ]) {
    assert.throws(() => evaluateNpmAudit(invalid, reviewDate))
  }
})
