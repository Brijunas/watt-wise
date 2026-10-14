const flattenKeys = (value: object, prefix = ''): string[] => {
  return Object.entries(value).flatMap(([key, child]: [string, unknown]) => {
    const path = prefix === '' ? key : `${prefix}.${key}`
    return typeof child === 'object' && child !== null ? flattenKeys(child, path) : [path]
  })
}

export default flattenKeys
