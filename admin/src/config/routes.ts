export const paths = {
  home: '/',
  catalogReview: '/catalog-review',
  about: '/about',
  signIn: '/sign-in',
  noAccess: '/no-access',
} as const

// Paths in the frontend app, which this app links to on its address (`env.frontendUrl`). They must
// match the frontend app's `config/routes.ts`.
export const frontendPaths = { forgotPassword: '/forgot-password' } as const
