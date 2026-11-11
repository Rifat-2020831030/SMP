import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router'
import { createQueryClient } from '@/lib/query-client'
import { StudentIdProvider } from '@/lib/student-id'
import './index.css'
import App from './App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={createQueryClient()}>
      <StudentIdProvider>
        <BrowserRouter><App /></BrowserRouter>
      </StudentIdProvider>
    </QueryClientProvider>
  </StrictMode>,
)
