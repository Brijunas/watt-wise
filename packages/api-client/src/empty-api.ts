import { baseQuery } from '@wattwise/core'
import { createApi } from '@reduxjs/toolkit/query/react'

// The single API slice. The endpoints are generated into ./generated/api.ts (see openapi-config.json).
export const emptyApi = createApi({
  reducerPath: 'api',
  baseQuery,
  endpoints: () => ({}),
})
