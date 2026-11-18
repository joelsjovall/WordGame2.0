// Temporary exception for the existing development-tool advisory, reviewed in docs/ci.md.
// Expiry ensures this cannot silently become a permanent vulnerability allowance.
const exceptionUrl = 'https://github.com/advisories/GHSA-fc5q-39xc-vjpf'
const exceptionExpires = new Date('2026-12-18T00:00:00Z')

export function evaluateNpmAudit(report, now = new Date()) {
  if (
    report.error ||
    !report.vulnerabilities ||
    !report.metadata?.vulnerabilities
  ) {
    throw new Error('npm audit did not return a valid vulnerability report.')
  }

  const advisories = new Map()
  for (const vulnerability of Object.values(report.vulnerabilities)) {
    for (const advisory of vulnerability.via) {
      if (typeof advisory === 'string') {
        if (!report.vulnerabilities[advisory])
          throw new Error('Unresolved npm advisory reference.')
      } else {
        if (
          !advisory?.url ||
          !advisory.dependency ||
          !['info', 'low', 'moderate', 'high', 'critical'].includes(
            advisory.severity,
          )
        )
          throw new Error('Invalid npm advisory details.')
        advisories.set(`${advisory.url}:${advisory.dependency}`, advisory)
      }
    }
  }
  if (!advisories.size && report.metadata.vulnerabilities.total > 0) {
    throw new Error('npm reported vulnerabilities without advisory details.')
  }

  return [...advisories.values()].map((advisory) => {
    const allowed =
      advisory.url === exceptionUrl &&
      advisory.dependency === 'picomatch' &&
      advisory.severity === 'high' &&
      now < exceptionExpires
    const blocking =
      ['high', 'critical'].includes(advisory.severity) && !allowed
    return { advisory, allowed, blocking }
  })
}
