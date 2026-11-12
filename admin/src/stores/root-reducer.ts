import { combineSlices } from '@reduxjs/toolkit'
import { api } from '@wattwise/api-client'
import { sessionSlice } from '@wattwise/core'

// A feature extends this with `declare module` and injects its slice (see "Store" in
// frontend/docs/structure.md), so the root reducer never imports a feature.
// eslint-disable-next-line @typescript-eslint/no-empty-object-type
export interface LazyLoadedSlices {}

export const rootReducer = combineSlices(api, sessionSlice).withLazyLoadedSlices<LazyLoadedSlices>()

export type RootState = ReturnType<typeof rootReducer>
