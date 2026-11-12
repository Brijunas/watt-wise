/**
 * A deferred answer: a handler awaits `gate.wait` and the test calls `gate.open()` when the
 * response may go out, so it can assert the in-flight state first.
 *
 * ```ts
 * const gate = createGate()
 * server.use(http.post(authUrl('/sign-in'), async () => {
 *   await gate.wait
 *   return HttpResponse.json(signedInResponse)
 * }))
 * // ... assert the busy state, then:
 * gate.open()
 * ```
 */
const createGate = () => {
  let open: () => void = () => undefined
  const wait = new Promise<void>((resolve) => {
    open = resolve
  })
  return { wait, open }
}

export default createGate
