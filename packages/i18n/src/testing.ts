const pluralSuffix = /_(?:zero|one|two|few|many|other)$/

const collectKeys = (value: object, prefix: string): string[] => {
  return Object.entries(value).flatMap(([key, child]: [string, unknown]) => {
    const path = prefix === '' ? key : `${prefix}.${key}`
    return typeof child === 'object' && child !== null ? collectKeys(child, path) : [path]
  })
}

// The leaf keys of a locale file. i18next plural forms (`count_one`, `count_few`, ...) reduce to
// their base key and repeats collapse, so languages with different plural categories compare equal.
// Any leaf ending in a plural suffix (e.g. `status_other`) is treated as a plural form.
const flattenKeys = (value: object): string[] => {
  return [...new Set(collectKeys(value, '').map((key) => key.replace(pluralSuffix, '')))]
}

export default flattenKeys
