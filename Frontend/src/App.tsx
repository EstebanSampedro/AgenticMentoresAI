import React, { useEffect, useRef, useState } from 'react';
import { ChatProvider } from './context/ChatContext';
import { useChat } from './context/ChatContext';
import StudentList from './components/StudentList';
import ChatWindow from './components/ChatWindow';
import ChatHeader from './components/ChatHeader';
import MessageInput from './components/MessageInput';
import SummaryPanel from './components/SummaryPanel';
import ChatSearchPanel from './components/ChatSearchPanel';
import MentorAdminView from './components/MentorAdminView';
import { ErrorAlert } from "./components/ErrorAlert";
import LoadingScreen from './components/LoadingScreen';

import iconResum from './assets/iconResum.png';
import iconBack from './assets/iconBack.png';
import { initClarity } from './utils/initClarity'; //  importa esto
import './App.css';

type MobileScreen =
  | 'studentList'
  | 'chat'
  | 'chatSearch'
  | 'summary'
  | 'mentorAdmin';

const AppContent = () => {
  const [setSelectedStudent] = useState(null);

  const [showSummary, setShowSummary] = useState(false);
  const [showSearch, setShowSearch] = useState(false);
  const {
    MentorType,
    isAuthLoading,
    mobileScreen,
    setMobileScreen,
    isMobile,
    generateSummary,
    selectedStudent
  } = useChat();

  const [showAdminView, setShowAdminView] = useState(false);
  const panelRef = useRef<HTMLDivElement | null>(null);

  const isAdminView = MentorType === 'Admin' || MentorType === 'Senior' || MentorType === 'Semi Senior';
  const handleGenerateSummary = async () => {
    if (!selectedStudent) return;
    // abrir panel de Resumen
    if (isMobile) {
      setMobileScreen('summary');
    } else {
      setShowSummary(true);
      setShowSearch(false);
    }
    // disparar POST + polling
    await generateSummary(selectedStudent.chatId);
  };


  useEffect(() => {
    initClarity();
    const applySystemTheme = () => {
      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      document.body.classList.toggle('dark', prefersDark);
      document.body.classList.toggle('light', !prefersDark);
    };

    applySystemTheme();
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    mediaQuery.addEventListener('change', applySystemTheme);
    return () => mediaQuery.removeEventListener('change', applySystemTheme);
  }, []);

  useEffect(() => {
    if (selectedStudent && isMobile) {
      setMobileScreen('chat');
      setShowSummary(false);
      setShowSearch(false);
    }
  }, [selectedStudent]);

  const handleStudentClick = () => {
    setShowAdminView(false);
    if (isMobile) {
      setMobileScreen('chat');
    }
  };

  const handleShowSummary = () => {
    if (isMobile) {
      setMobileScreen('summary');
    } else {
      setShowSummary(true);
      setShowSearch(false);
    }
  };

  const handleShowSearch = () => {
    if (isMobile) {
      setMobileScreen('chatSearch');
    } else {
      setShowSearch(true);
      setShowSummary(false);
    }
  };

  const handleClosePanel = () => {
    setShowSearch(false);
    setShowSummary(false);
  };

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (panelRef.current && !panelRef.current.contains(event.target as Node)) {
        if (showSummary || showSearch) {
          handleClosePanel();
        }
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [showSummary, showSearch]);
  // ⏳  Loading
  if (isAuthLoading) return <LoadingScreen />;

  if (MentorType === null) {
    return (
      <div className="not-authorized-message">
        <h2>⛔ No eres un usuario autorizado</h2>
        <p>Por favor, contacta al administrador del sistema si crees que esto es un error.</p>
      </div>
    );
  }

  return (
    <div className="app-container">
      <ErrorAlert />

      {!isMobile && (
        <>
          <div className="sidebar">
            <StudentList
              onStudentClick={handleStudentClick}
              onAdminClick={() => {
                setShowAdminView(true);
              }}
            />
          </div>

          <div className="main-area">
            {isAdminView && showAdminView ? (
              <MentorAdminView />
            ) : (
              <>
                <button className="summary-toggle" onClick={handleShowSummary}>
                  <img src={iconResum} className="icon" alt="Resumen" />
                  <span className="label">Resumen I.A.</span>
                </button>

                <ChatHeader onBack={() => { }} onSearchClick={handleShowSearch} onGenerateSummary={handleGenerateSummary} />
                <div className="chat-summary-wrapper">
                  <div className="chat-area">
                    <ChatWindow />
                    <MessageInput />
                  </div>

                  {(showSummary || showSearch) && (
                    <div ref={panelRef} className="summary-area active">
                      {showSearch ? <ChatSearchPanel /> : <SummaryPanel />}
                    </div>
                  )}
                </div>
              </>
            )}
          </div>
        </>
      )}

      {isMobile && (
        <>
          {mobileScreen === 'studentList' && (
            <div className="sidebar foreground">
              <StudentList
                onStudentClick={handleStudentClick}
                onAdminClick={() => {
                  setMobileScreen('mentorAdmin');
                  setShowAdminView(true);
                }}
              />
            </div>
          )}

          {mobileScreen === 'chat' && (
            <div className="main-area foreground">
              <button
                className="back-button"
                onClick={() => setMobileScreen('studentList')}
              >
                <img src={iconBack} className="backicon" alt="Volver" />
                <span className="back-label">Atrás</span>
              </button>

              <ChatHeader onBack={() => setMobileScreen('studentList')} onSearchClick={handleShowSearch} onGenerateSummary={handleGenerateSummary} />
              <div className="chat-area">
                <ChatWindow />
                <MessageInput />
              </div>
            </div>
          )}

          {mobileScreen === 'chatSearch' && (
            <div className="main-area foreground">
              <button className="back-button" onClick={() => setMobileScreen('chat')}>
                ←
              </button>
              <ChatSearchPanel />
            </div>
          )}

          {mobileScreen === 'summary' && (
            <div className="main-area foreground">
              <button className="back-button" onClick={() => setMobileScreen('chat')}>
                ←
              </button>
              <SummaryPanel />
            </div>
          )}

          {mobileScreen === 'mentorAdmin' && (
            <div className="main-area foreground">
              <button className="back-button" onClick={() => setMobileScreen('studentList')}>
                ←
              </button>
              <MentorAdminView />
            </div>
          )}

          {mobileScreen === 'chat' && (
            <>
              <button className="summary-toggle" onClick={handleShowSummary}>
                <img src={iconResum} className="icon" alt="Resumen" />
                <span className="label">Resumen I.A.</span>
              </button>
            </>
          )}
        </>
      )}
    </div>
  );
};

const App = () => (
  <ChatProvider>
    <AppContent />
  </ChatProvider>
);

export default App;
