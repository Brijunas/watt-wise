import { HttpResponse } from 'msw'

export interface ProblemOptions {
  /** The HTTP status. */
  status: number
  /** The machine-readable ProblemDetails `code`, such as `Accounts.LockedOut`. */
  code: string
  /** Field errors of a validation problem: camelCase field path to messages. */
  errors?: Record<string, string[]>
  /** Sent as the `Retry-After` header, in seconds. */
  retryAfterSeconds?: number
}

/** A ProblemDetails answer like the Api's: `status`, `code`, and `errors` for a validation problem. */
const problemResponse = ({ status, code, errors, retryAfterSeconds }: ProblemOptions) =>
  HttpResponse.json(
    { type: 'about:blank', title: code, status, code, ...(errors && { errors }) },
    {
      status,
      headers: retryAfterSeconds === undefined ? {} : { 'Retry-After': String(retryAfterSeconds) },
    },
  )

export default problemResponse
