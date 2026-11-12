---
paths:
  - '*/src/**/*.{ts,tsx}'
---

# Async UI code

Check each of these when code awaits something.

- After every await that changes the session, check the session revision is unchanged before applying the answer.
- Check the component is still mounted before `setState` or a refetch after an await.
- Show a loader, not the sign-in form or a redirect, while the session is `restoring`.
- Tests use deferred responses (`createGate`): a late answer after navigation, unmount or sign-out changes nothing.
- RTK Query's `refetchOnMountOrArgChange` attaches to a request already pending for the same args instead of starting a new one: decide freshness by `requestId`, not by comparing `fulfilledTimeStamp` with `Date.now()`.

The full rules are in [architecture.md](../../../frontend/docs/architecture.md#session).
