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

export type MatchMediaFake = ((query: string) => MediaQueryList) & {
  /** Changes the width and tells the `change` listeners, like a window resize does. */
  resize: (width: number) => void
}

/**
 * A `matchMedia` fake for jsdom, which has none. Answers width and dark-mode queries; `resize`
 * moves the width afterwards and notifies the listeners.
 */
const createMatchMedia = (options: { width: number; prefersDark?: boolean }): MatchMediaFake => {
  const settings = { width: options.width, prefersDark: options.prefersDark ?? false }
  const listeners = new Set<EventListenerOrEventListenerObject>()
  const matchMedia = (query: string) => {
    const media = query.replace(/^@media\s+/, '')
    // Read on every access: `useMediaQuery` keeps one list and reads `matches` after a change.
    const list: MediaQueryList = {
      get matches() {
        return media.split(/\s+and\s+/).every((condition) => evaluate(condition, settings))
      },
      media: query,
      onchange: null,
      addEventListener: (type: string, listener: EventListenerOrEventListenerObject) => {
        if (type === 'change') {
          listeners.add(listener)
        }
      },
      removeEventListener: (type: string, listener: EventListenerOrEventListenerObject) => {
        if (type === 'change') {
          listeners.delete(listener)
        }
      },
      // eslint-disable-next-line @typescript-eslint/no-deprecated
      addListener: () => undefined,
      // eslint-disable-next-line @typescript-eslint/no-deprecated
      removeListener: () => undefined,
      dispatchEvent: () => false,
    }
    return list
  }
  return Object.assign(matchMedia, {
    resize: (width: number) => {
      settings.width = width
      for (const listener of Array.from(listeners)) {
        if (typeof listener === 'function') {
          listener(new Event('change'))
        } else {
          listener.handleEvent(new Event('change'))
        }
      }
    },
  })
}

export default createMatchMedia
