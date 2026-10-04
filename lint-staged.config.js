// The C# `dotnet format` entry is added in story S2.10.
export default {
  '*.{js,mjs,cjs,jsx,ts,tsx}': ['eslint --fix --max-warnings=0', 'prettier --write'],
  '*.{json,md,yml,yaml,css,html}': 'prettier --write',
}
