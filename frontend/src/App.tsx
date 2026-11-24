import { lazy, Suspense, useEffect, useRef } from 'react'
import { Route, Routes, useLocation } from 'react-router'
import { HomePage } from './pages/HomePage'
import { NotFoundPage } from './pages/NotFoundPage'
import { AudioSettings } from './components/AudioSettings.tsx'
import { LoadingStatus } from './components/ui.tsx'

const JoinLobbyPage = lazy(async () => ({
  default: (await import('./pages/JoinLobbyPage.tsx')).JoinLobbyPage,
}))
const LobbyPage = lazy(async () => ({
  default: (await import('./pages/LobbyPage.tsx')).LobbyPage,
}))
const CreateLobbyPage = lazy(async () => ({
  default: (await import('./pages/CreateLobbyPage.tsx')).CreateLobbyPage,
}))
const PartyGamePage = lazy(async () => ({
  default: (await import('./pages/PartyGamePage.tsx')).PartyGamePage,
}))
const OnlineModePage = lazy(async () => ({
  default: (await import('./pages/OnlineModePage.tsx')).OnlineModePage,
}))
const OnlineGamePage = lazy(async () => ({
  default: (await import('./pages/OnlineGamePage.tsx')).OnlineGamePage,
}))

export default function App() {
  const { pathname } = useLocation()
  const content = useRef<HTMLDivElement>(null)
  const previousPath = useRef(pathname)
  useEffect(() => {
    if (previousPath.current !== pathname) content.current?.focus()
    previousPath.current = pathname
  }, [pathname])
  return (
    <>
      <a className="skip-link" href="#main-content" tabIndex={0}>
        Skip to main content
      </a>
      <div id="main-content" ref={content} tabIndex={-1}>
        <Suspense
          fallback={
            <main className="page">
              <LoadingStatus>Loading page...</LoadingStatus>
            </main>
          }
        >
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/online" element={<OnlineModePage />} />
            <Route path="/online/games/:gameId" element={<OnlineGamePage />} />
            <Route path="/join" element={<JoinLobbyPage />} />
            <Route path="/lobbies/new" element={<CreateLobbyPage />} />
            <Route path="/lobbies/:lobbyId" element={<LobbyPage />} />
            <Route path="/lobbies/:lobbyId/game" element={<PartyGamePage />} />
            <Route
              path="/join/:lobbyId/:lobbyCode"
              element={<JoinLobbyPage />}
            />
            <Route path="*" element={<NotFoundPage />} />
          </Routes>
        </Suspense>
      </div>
      <AudioSettings />
    </>
  )
}
