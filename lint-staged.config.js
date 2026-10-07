import path from 'node:path'

// lint-staged hands function tasks absolute paths; `dotnet format --include` needs relative ones.
const includeArgs = (files) =>
  files.map((file) => `"${path.relative(process.cwd(), file)}"`).join(' ')

export default {
  '*.{js,mjs,cjs,jsx,ts,tsx}': ['eslint --fix --max-warnings=0', 'prettier --write'],
  '*.{json,md,yml,yaml,css,html}': 'prettier --write',
  // Fix first (lint-staged re-stages the result), then reject what could not be fixed.
  'backend/**/*.cs': (files) => [
    `dotnet format backend/WattWise.slnx --include ${includeArgs(files)}`,
    `dotnet format backend/WattWise.slnx --verify-no-changes --no-restore --include ${includeArgs(files)}`,
  ],
}
