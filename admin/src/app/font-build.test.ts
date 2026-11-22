// @vitest-environment node
import { mkdtemp, readFile, readdir, rm } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { build } from 'vite'
import { expect, it, vi } from 'vitest'

it('ships Red Hat Display font assets and excludes Roboto', async () => {
  const root = fileURLToPath(new URL('../../', import.meta.url))
  const outDir = await mkdtemp(join(root, 'node_modules/.font-build-'))
  try {
    vi.stubEnv('NODE_ENV', 'production')
    await build({ root, mode: 'production', logLevel: 'error', build: { outDir } })
    const files = await readdir(outDir, { recursive: true })
    expect(files).toContainEqual(expect.stringMatching(/red-hat-display-latin-ext.*\.woff2$/i))
    expect(files.filter((file) => /roboto/i.test(file))).toEqual([])
    const stylesheets = files.filter((file) => file.endsWith('.css'))
    expect(stylesheets.length).toBeGreaterThan(0)
    for (const file of stylesheets) {
      expect(await readFile(join(outDir, file), 'utf8'), file).not.toMatch(/roboto/i)
    }
  } finally {
    vi.unstubAllEnvs()
    await rm(outDir, { recursive: true, force: true })
  }
}, 120_000)
