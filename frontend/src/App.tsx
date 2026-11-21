import { useEffect, useRef } from 'react'
import { Route, Routes, useLocation } from 'react-router'
import { HomePage } from './pages/HomePage'
import { NotFoundPage } from './pages/NotFoundPage'
import { JoinLobbyPage } from './pages/JoinLobbyPage.tsx'
import { LobbyPage } from './pages/LobbyPage.tsx'
import { CreateLobbyPage } from './pages/CreateLobbyPage.tsx'
import { PartyGamePage } from './pages/PartyGamePage.tsx'
import { OnlineModePage } from './pages/OnlineModePage.tsx'
import { OnlineGamePage } from './pages/OnlineGamePage.tsx'
import { AudioSettings } from './components/AudioSettings.tsx'

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
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/online" element={<OnlineModePage />} />
          <Route path="/online/games/:gameId" element={<OnlineGamePage />} />
          <Route path="/join" element={<JoinLobbyPage />} />
          <Route path="/lobbies/new" element={<CreateLobbyPage />} />
          <Route path="/lobbies/:lobbyId" element={<LobbyPage />} />
          <Route path="/lobbies/:lobbyId/game" element={<PartyGamePage />} />
          <Route path="/join/:lobbyId/:lobbyCode" element={<JoinLobbyPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </div>
      <AudioSettings />
    </>
  )
}
