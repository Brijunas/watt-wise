import { useEffect, useRef } from 'react'
import type { RefObject } from 'react'

// True while the component is mounted. The page checks it after an `await`: an answer that arrives
// after the user left must not show a message or refetch. The effect sets it again on StrictMode's
// second run.
const useMounted = (): RefObject<boolean> => {
  const mountedRef = useRef(true)
  useEffect(() => {
    mountedRef.current = true
    return () => {
      mountedRef.current = false
    }
  }, [])
  return mountedRef
}

export default useMounted
