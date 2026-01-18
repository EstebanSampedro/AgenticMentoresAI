// src/main.tsx
import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './index.css';

import { ErrorBoundaryWithToast } from './components/ui/ErrorBoundary';
import { initClientLogger } from './utils/logger';
import { getAuthenticatedToken } from './context/ChatContext';

// Inicializa el logger (usa la base que corresponda)
initClientLogger({
  logsBaseUrl: import.meta.env.VITE_API_BASE, 
  getUserId: () => (window as any).__mentorEmail || null,
  getChatId: () => (window as any).__currentChatId || null,
  appInfo: {
    appName: 'mentores-verdes-spa',
    env: import.meta.env.MODE,
  },
});

const root = ReactDOM.createRoot(document.getElementById('root')!);
root.render(
  <React.StrictMode>
    <ErrorBoundaryWithToast>
      <App />
    </ErrorBoundaryWithToast>
  </React.StrictMode>
);
