import { readFile, readdir } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../frontend/dist/', import.meta.url))
const sensitivePatterns = [
  /ConnectionStrings__Statistics/i,
  /postgres(?:ql)?:\/\/[^\s"'`]+/i,
  /(?:Host|Server)=[^;\s]+;[^\n]*Password=/i,
  /(?:Password|Pwd)=[^;\s"'`]+;/i,
  /[\w.-]+\.neon\.tech/i,
]

async function scan(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const file = join(directory, entry.name)
    if (entry.isDirectory()) {
      await scan(file)
    } else if (entry.isFile() && /\.(?:js|css|html|json|map|txt)$/i.test(entry.name)) {
      const content = await readFile(file, 'utf8')
      if (sensitivePatterns.some((pattern) => pattern.test(content))) {
        throw new Error(`Potential backend secret in frontend build: ${entry.name}`)
      }
    }
  }
}

await scan(root)
console.log('Frontend build contains no known backend secret markers.')
