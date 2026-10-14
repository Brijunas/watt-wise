import { emptyApi as api } from '../empty-api'
const injectedRtkApi = api.injectEndpoints({
  endpoints: (build) => ({
    getAbout: build.query<GetAboutApiResponse, GetAboutApiArg>({
      query: () => ({ url: `/api/v1/about` }),
    }),
  }),
  overrideExisting: false,
})
export { injectedRtkApi as generatedApi }
export type GetAboutApiResponse = /** status 200 OK */ AboutResponse
export type GetAboutApiArg = void
export type AboutResponse = {
  name: string
  version: string
}
export type HttpValidationProblemDetails = {
  type?: string | null
  title?: string | null
  status?: (number | string | (null | null)) | null
  detail?: string | null
  instance?: string | null
  errors?: {
    [key: string]: string[]
  }
}
export const { useGetAboutQuery } = injectedRtkApi
