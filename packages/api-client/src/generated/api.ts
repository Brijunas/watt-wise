import { emptyApi as api } from '../empty-api'
const injectedRtkApi = api.injectEndpoints({
  endpoints: (build) => ({
    getAbout: build.query<GetAboutApiResponse, GetAboutApiArg>({
      query: () => ({ url: `/api/v1/about` }),
    }),
    register: build.mutation<RegisterApiResponse, RegisterApiArg>({
      query: (queryArg) => ({ url: `/api/v1/auth/register`, method: 'POST', body: queryArg.body }),
    }),
    resendConfirmation: build.mutation<ResendConfirmationApiResponse, ResendConfirmationApiArg>({
      query: (queryArg) => ({
        url: `/api/v1/auth/resend-confirmation`,
        method: 'POST',
        body: queryArg.body,
      }),
    }),
    confirmEmail: build.mutation<ConfirmEmailApiResponse, ConfirmEmailApiArg>({
      query: (queryArg) => ({
        url: `/api/v1/auth/confirm-email`,
        method: 'POST',
        body: queryArg.body,
      }),
    }),
  }),
  overrideExisting: false,
})
export { injectedRtkApi as generatedApi }
export type GetAboutApiResponse = /** status 200 OK */ AboutResponse
export type GetAboutApiArg = void
export type RegisterApiResponse = unknown
export type RegisterApiArg = {
  body: (null | null) | RegisterRequest
}
export type ResendConfirmationApiResponse = unknown
export type ResendConfirmationApiArg = {
  body: (null | null) | ResendConfirmationRequest
}
export type ConfirmEmailApiResponse = unknown
export type ConfirmEmailApiArg = {
  body: (null | null) | ConfirmEmailRequest
}
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
export type RegisterRequest = {
  email: string | null
  password: string | null
  language: string | null
}
export type ResendConfirmationRequest = {
  email: string | null
}
export type ProblemDetails = {
  type?: string | null
  title?: string | null
  status?: (number | string | (null | null)) | null
  detail?: string | null
  instance?: string | null
}
export type ConfirmEmailRequest = {
  id: string | null
  token: string | null
}
export const {
  useGetAboutQuery,
  useRegisterMutation,
  useResendConfirmationMutation,
  useConfirmEmailMutation,
} = injectedRtkApi
