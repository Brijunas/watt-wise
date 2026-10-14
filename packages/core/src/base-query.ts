import { fetchBaseQuery } from '@reduxjs/toolkit/query'
import type { BaseQueryFn, FetchArgs, FetchBaseQueryError } from '@reduxjs/toolkit/query'

/** The thunk extra argument of the store: configuration the store factory receives from the app. */
export interface StoreExtra {
  baseUrl: string
}

/**
 * The base query of the single API slice. It reads the API base URL from the thunk extra argument
 * (`api.extra`), never from `import.meta.env`, so the app decides the URL.
 */
const baseQuery: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = (
  args,
  api,
  extraOptions,
) => {
  const { baseUrl } = api.extra as StoreExtra
  return fetchBaseQuery({ baseUrl })(args, api, extraOptions)
}

export default baseQuery
