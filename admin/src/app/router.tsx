import { createBrowserRouter } from 'react-router'
import type { RouteObject } from 'react-router'
import { PageLoading } from '@wattwise/ui'
import { paths } from '@/config/routes'
import NotFoundRoute from './routes/not-found'
import RootLayout from './routes/root-layout'

// `lazy` is a function, not an object of loaders: React Router empties a `lazy` object once it has
// loaded, so a second router built from these routes (each test builds one) would match nothing.
// `HydrateFallback` sits on the lazy pages, not on the root: React Router renders the fallback in
// place of the route that has it, so on the root it would replace the whole shell on first load.
export const routes: RouteObject[] = [
  {
    path: paths.home,
    Component: RootLayout,
    children: [
      {
        index: true,
        HydrateFallback: PageLoading,
        lazy: async () => ({ Component: (await import('./routes/home')).default }),
      },
      {
        path: paths.catalogReview,
        HydrateFallback: PageLoading,
        lazy: async () => ({ Component: (await import('./routes/catalog-review')).default }),
      },
      {
        path: paths.about,
        HydrateFallback: PageLoading,
        lazy: async () => ({ Component: (await import('./routes/about')).default }),
      },
      { path: '*', Component: NotFoundRoute },
    ],
  },
]

export const router = createBrowserRouter(routes)
