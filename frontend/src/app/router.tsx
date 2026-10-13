import CircularProgress from '@mui/material/CircularProgress'
import { createBrowserRouter } from 'react-router'
import type { RouteObject } from 'react-router'
import { paths } from '@/config/routes'
import { NotFoundRoute } from './routes/not-found'
import { RootLayout } from './routes/root-layout'

// `lazy` is a function, not an object of loaders: React Router empties a `lazy` object once it has
// loaded, so a second router built from these routes (each test builds one) would match nothing.
export const routes: RouteObject[] = [
  {
    path: paths.home,
    Component: RootLayout,
    HydrateFallback: () => <CircularProgress />,
    children: [
      {
        index: true,
        lazy: async () => ({ Component: (await import('./routes/home')).Home }),
      },
      {
        path: paths.placeholder.slice(1),
        lazy: async () => ({ Component: (await import('./routes/placeholder')).Placeholder }),
      },
      { path: '*', Component: NotFoundRoute },
    ],
  },
]

export const router = createBrowserRouter(routes)
