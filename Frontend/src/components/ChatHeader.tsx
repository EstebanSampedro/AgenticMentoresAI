import React, { useState, useEffect } from 'react';
import { useChat } from '../context/ChatContext';
import lupaIcon from '../assets/iconLupa.png';
import './ChatHeader.css';

interface ChatHeaderProps {
  onBack?: () => void;
  onSearchClick?: () => void;
  onGenerateSummary?: () => void; // NUEVO
}

const ChatHeader: React.FC<ChatHeaderProps> = ({ onBack, onSearchClick, onGenerateSummary }) => {
  const { selectedStudent, updateStudent, updateAIState, isSummaryLoading } = useChat();
  const [isIAEnabled, setIsIAEnabled] = useState(false);

  // Actualiza el estado del IA cuando se selecciona un estudiante
  useEffect(() => {
    if (selectedStudent) {
      setIsIAEnabled(!selectedStudent.elevated); // Si está elevado a mentor, IA está desactivada
    }
  }, [selectedStudent]);

  // Maneja el cambio de estado de la IA
  const handleToggleIA = () => {
    if (!selectedStudent) return;

    const newIAState = !isIAEnabled;
    setIsIAEnabled(newIAState);

    // Actualizar el estado del estudiante en el contexto global
    updateStudent({
      ...selectedStudent,
      elevated: !newIAState,
    });

    // Actualizar el estado de la IA en el servidor
    updateAIState(selectedStudent.chatId, !newIAState, 'Mentor');
  };

  if (!selectedStudent) return null;

  return (
    <div className="chat-header">
     
      {/* Información del estudiante */}
      <div className="student-info">
        {selectedStudent.photo ? (
          <img className="avatar" src={selectedStudent.photo} alt={`${selectedStudent.name} avatar`} />
        ) : (
          <div className="avatar">
            {selectedStudent.name
              .split(' ')
              .map(n => n[0])
              .join('')
              .slice(0, 2)
              .toUpperCase()}
          </div>
        )}
        <div>
          <h3>{selectedStudent.name}</h3>
          <p><u>{selectedStudent.email}</u></p>
        </div>
      </div>

      {/* Acciones */}
      <div className="actions">
        {/* Botón de búsqueda */}
        <button className="search-button" onClick={onSearchClick} aria-label="Buscar mensajes">
          <img className="LupaIcon" src={lupaIcon} alt="Buscar" />
        </button>

        <div className="divider" />

        {/* Switch para activar/desactivar IA */}
        <div className="ia-toggle">
          <span><strong>I.A. {isIAEnabled ? 'APAGADA' : 'PRENDIDA'}</strong></span>
          <label className="switch">
            <input
              type="checkbox"
              checked={isIAEnabled}
              onChange={handleToggleIA}
              aria-label="Activar/desactivar IA"
            />
            <span className="slider round"></span>
          </label>
        </div>

        {/* Botón de resumen */}
        <button
          className="summary-button"
          aria-label="Generar resumen"
          onClick={onGenerateSummary}
          disabled={isSummaryLoading}
          title={isSummaryLoading ? 'Generando…' : 'Generar resumen'}
        >
          {isSummaryLoading ? 'Generando…' : 'Generar resumen'}
        </button>
      </div>
    </div>
  );
};

export default ChatHeader;
