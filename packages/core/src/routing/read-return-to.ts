const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null

const text = (value: unknown): string => (typeof value === 'string' ? value : '')

// The page a guard sent the user away from, as the router state's `from` location: path, query and
// hash. It is only ever a location in this app: a path that starts with a single slash, so a
// crafted state can't send the user to another site.
const readReturnTo = (state: unknown): string | undefined => {
  const from = isRecord(state) && isRecord(state.from) ? state.from : undefined
  const pathname = text(from?.pathname)
  if (!pathname.startsWith('/') || pathname.startsWith('//')) return undefined
  return `${pathname}${text(from?.search)}${text(from?.hash)}`
}

export default readReturnTo
