function evaluate(condition: string, options: { width: number; prefersDark: boolean }): boolean {
  const normalized = condition.trim().replace(/\s+/g, ' ')
  const width = /^\(\s*(min|max)-width\s*:\s*(\d+(?:\.\d+)?)px\s*\)$/.exec(normalized)
  if (width) {
    const limit = Number(width[2])
    return width[1] === 'min' ? options.width >= limit : options.width <= limit
  }
  if (/^\(\s*prefers-color-scheme\s*:\s*dark\s*\)$/.test(normalized)) {
    return options.prefersDark
  }
  return false
}

/** A `matchMedia` fake for jsdom, which has none. Answers width and dark-mode queries. */
export function createMatchMedia(options: {
  width: number
  prefersDark?: boolean
}): (query: string) => MediaQueryList {
  const settings = { width: options.width, prefersDark: options.prefersDark ?? false }
  return (query) => {
    const media = query.replace(/^@media\s+/, '')
    const matches = media.split(/\s+and\s+/).every((condition) => evaluate(condition, settings))
    const list: MediaQueryList = {
      matches,
      media: query,
      onchange: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      addListener: () => undefined,
      removeListener: () => undefined,
      dispatchEvent: () => false,
    }
    return list
  }
}
