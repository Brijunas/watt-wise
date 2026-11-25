// What setup prints: `setup: <step>: changed|unchanged|skipped`, one line per step, and a stop with
// `setup: <message>` on stderr. A step that can't go on throws a SetupError; setup.ts prints it and exits 1.

export class SetupError extends Error {}

export const fail = (message: string): never => {
  throw new SetupError(message)
}

export const step = (name: string, state: string): void => {
  console.log(`setup: ${name}: ${state}`)
}

export const note = (message: string): void => {
  console.log(`setup: ${message}`)
}
