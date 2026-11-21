import { Route, Routes } from 'react-router'
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
  return (
    <>
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
      <AudioSettings />
    </>
  )
}
