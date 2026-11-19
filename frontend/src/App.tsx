import { Route, Routes } from 'react-router'
import { HomePage } from './pages/HomePage'
import { NotFoundPage } from './pages/NotFoundPage'
import { JoinLobbyPage } from './pages/JoinLobbyPage.tsx'

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/join" element={<JoinLobbyPage />} />
      <Route path="/join/:lobbyId/:lobbyCode" element={<JoinLobbyPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
