import { generatedApi } from './generated/api'

export type { AboutResponse, GetAboutApiResponse } from './generated/api'

// Cache tags are part of the API contract, so both apps share them. Add a tag type here, then
// `providesTags` on the query and `invalidatesTags` on the mutations that change it.
export const api = generatedApi.enhanceEndpoints({
  addTagTypes: ['About'],
  endpoints: {
    getAbout: { providesTags: ['About'] },
  },
})

export const { useGetAboutQuery } = api
