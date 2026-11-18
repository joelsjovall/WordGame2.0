import { readFile, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import openapiTS, { astToString } from 'openapi-typescript'
import prettier from 'prettier'

const args = process.argv.slice(2)
const source =
  args.find((arg) => arg !== '--check') ??
  'http://localhost:5000/openapi/v1.json'
const output = new URL('../src/services/api/generated.ts', import.meta.url)
const config = await prettier.resolveConfig(
  fileURLToPath(new URL('../.prettierrc.json', import.meta.url)),
)
const types = astToString(await openapiTS(new URL(source)))
const formatted = await prettier.format(
  '// Generated from backend OpenAPI. Do not edit by hand.\n' + types,
  { ...config, parser: 'typescript' },
)

if (args.includes('--check')) {
  if ((await readFile(output, 'utf8')) !== formatted) {
    throw new Error(
      'API types are stale. Run npm run api:generate against the current backend.',
    )
  }
  process.stdout.write('API types are up to date.\n')
} else {
  await writeFile(output, formatted)
  process.stdout.write(
    'Generated src/services/api/generated.ts from OpenAPI.\n',
  )
}
