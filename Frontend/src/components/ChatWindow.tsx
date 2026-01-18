import React, { useEffect, useState, useRef } from 'react';
import { useChat } from '../context/ChatContext';
import type { Message } from '../context/ChatContext';
import './ChatWindow.css';
import { getFileIcon } from '../utils/fileUtils';
import DOMPurify from 'dompurify';
import FilePreview from '../components/FilePreview';
import { extractDriveAndItem } from '../utils/onedrive';
import { logger } from '../utils/logger';
import ImagePreview from './ImagePreview';

const ChatWindow = () => {
  const {
    messages,
    selectedStudent,
    fetchMoreMessages,
    fetchNewerMessages,
    isFetching,
    nextPageUrl,
    prevPageUrl,
    highlightedMessageId,
    setHighlightedMessageId,
    shouldScrollToBottom,
    setShouldScrollToBottom,
  } = useChat();

  const chatRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const topSentinelRef = useRef<HTMLDivElement>(null);
  const scrollToBottom = (behavior: ScrollBehavior = 'auto') => {

    requestAnimationFrame(() => {
      if (chatRef.current) {
        chatRef.current.scrollTop = chatRef.current.scrollHeight;
      } else {
        // fallback
        bottomRef.current?.scrollIntoView({ behavior });
      }
    });
  };

  // Scroll hacia arriba para cargar más mensajes cuando el usuario llega al principio del chat
  // Cargar más mensajes cuando el sentinel (arriba del chat) es visible
  useEffect(() => {
    const el = chatRef.current;
    const sentinel = topSentinelRef.current;
    if (!el || !sentinel || !nextPageUrl) return;

    const observer = new IntersectionObserver(
      (entries) => {
        const first = entries[0];
        if (first.isIntersecting && !isFetching) {
          const prevHeight = el.scrollHeight;

          fetchMoreMessages().then(() => {
            requestAnimationFrame(() => {
              const newHeight = el.scrollHeight;
              el.scrollTop = newHeight - prevHeight;
            });
          });
        }
      },
      { root: el, threshold: 0.1 }
    );

    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [nextPageUrl, isFetching, fetchMoreMessages]);

  useEffect(() => {
    if (selectedStudent) {
      const filtered = messages.filter(m => {
        const match = m.chatId.toLowerCase() === selectedStudent.chatId.toLowerCase();
        return match;
      });
    }
  }, [messages, selectedStudent]);

  // Scroll hacia abajo cuando hay nuevos mensajes
  useEffect(() => {
    if (shouldScrollToBottom && bottomRef.current) {
      bottomRef.current.scrollIntoView({ behavior: 'smooth' });
      setShouldScrollToBottom(false); // ← Resetea la señal
    }
  }, [shouldScrollToBottom]);

  // guarda el último chatId para detectar cambio de conversación
  const lastChatIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (!selectedStudent) return;
    const newId = selectedStudent.chatId.toLowerCase();
    const isNewChat = lastChatIdRef.current !== newId;
    lastChatIdRef.current = newId;

    if (isNewChat) {
      // carga inicial: caer al final inmediatamente
      scrollToBottom('auto');
    }
  }, [selectedStudent?.chatId]);


  useEffect(() => {
    if (highlightedMessageId !== null) {
      const timer = setTimeout(() => setHighlightedMessageId(null), 2000);
      return () => clearTimeout(timer);
    }
  }, [highlightedMessageId]);

  if (!selectedStudent) {
    return (
      <div className="chat-window empty">
        <p>Selecciona un estudiante para ver la conversación</p>
      </div>
    );
  }

  const htmlHasText = (html?: string) => {
    if (!html) return false;
    // quitar etiquetas, &nbsp; y espacios
    const plain = DOMPurify.sanitize(html, { ALLOWED_TAGS: [], ALLOWED_ATTR: [] })
      .replace(/\u00a0/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();
    return plain.length > 0;
  };


  // Filtrar mensajes del estudiante seleccionado y ordenarlos por fecha
  const studentMessages = messages
    .filter(m => m.chatId.toLowerCase() === selectedStudent.chatId.toLowerCase())
    .sort((a, b) => {
      const timeDiff = new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime();
      if (timeDiff !== 0) return timeDiff;
      return a.id - b.id;
    });

  const getSenderName = (sender: string) => {
    if (sender === 'student') return selectedStudent.name;
    if (sender === 'IA') return 'I.A.';
    return 'Mentor';
  };

  const getInitials = (name: string) =>
    name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);

  const getTime = (timestamp?: string) => {
    if (!timestamp || typeof timestamp !== 'string') return 'Sin hora';

    try {
      const date = new Date(timestamp.endsWith('Z') ? timestamp : timestamp);

      const now = new Date();
      const isSameDay =
        date.getFullYear() === now.getFullYear() &&
        date.getMonth() === now.getMonth() &&
        date.getDate() === now.getDate();

      return isSameDay
        ? date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
        : date.toLocaleString(undefined, {
          day: '2-digit',
          month: 'short',
          hour: '2-digit',
          minute: '2-digit',
        });
    } catch (error) {
      logger().warn("Error parseando timestamp en ChatWindow:");
      return 'Fecha inválida';
    }
  };


  const getAvatar = (sender: string) => {
    const isStudent = sender === 'student';
    const name = isStudent ? selectedStudent.name : sender === 'IA' ? 'I.A.' : 'Mentor';


    if (isStudent && selectedStudent.photo) {
      return (
        <div className="avatar">
          <img 
          src={selectedStudent.photo} 
          alt={name} 
          className="avatar-img"
           />
        </div>
      );
    }

    return (
      <div
        className="avatar"
      >
        {isStudent && !selectedStudent.photo ? getInitials(name) : sender === 'IA' ? 'IA' : 'M'}
      </div>
    );
  };

  const renderMessageContent = (msg: Message) => {
    const main = () => {
      switch (msg.type) {
        case 'image': {
          const img = msg.attachments?.[0];
          const imgEl = img ? (
            <ImagePreview
              src={img.contentUrl}
              alt={img.name || `Imagen de ${getSenderName(msg.sender)}`}
              className="message-image"
            />
          ) : (
            <ImagePreview
              src={msg.content}
              alt={`Imagen enviada por ${getSenderName(msg.sender)}`}
              className="message-image"
            />
          );

          // texto/HTML
          const maybeText =
            htmlHasText(msg.content) ? (
              <div
                className="message-html"
                dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(msg.content) }}
              />
            ) : null;

          return (
            <>
              {imgEl}
              {maybeText}
            </>
          );
        }

        case 'html': {
          //  HTML
          return (
            <div
              className="message-html"
              dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(msg.content) }}
            />
          );
        }
        default:
          return <p>{msg.content}</p>;
      }
    };

    // ---------- Adjuntos ----------
    // Para 'image' ya mostramos el 1º arriba; para el resto, muestra todos.
    const attachmentsToRender =
      Array.isArray(msg.attachments)
        ? (msg.type === 'image' ? msg.attachments.slice(1) : msg.attachments)
        : [];

    return (
      <div>
        {main()}

        {attachmentsToRender.length > 0 && (
          <div className="message-attachments">
            {attachmentsToRender.map((a, i) => {
              if (a.contentType?.startsWith('image/')) {
                return (
                  <div key={i} className="message-attachment">
                    <ImagePreview
                      src={a.contentUrl}
                      alt={a.name}
                      className="message-image"
                    />
                  </div>
                );
              }
              const ref = extractDriveAndItem(a.contentUrl);
              if (ref) {
                return (
                  <div key={i} className="message-attachment">
                    <FilePreview driveId={ref.driveId} itemId={ref.itemId} />
                  </div>
                );
              }
              return (
                <div key={i} className="message-attachment">
                  <a href={a.contentUrl} target="_blank" rel="noreferrer">
                    {getFileIcon(a.contentType)} {a.name}
                  </a>
                </div>
              );
            })}
          </div>
        )}
      </div>
    );
  };



  return (
    <div className="chat-window" ref={chatRef}>
      <div ref={topSentinelRef} />
      {studentMessages.map(msg => {
        const isRight = msg.sender === 'mentor' || msg.sender === 'IA';
        const isHighlighted = msg.id === highlightedMessageId;

        return (

          <div
            key={msg.id}
            className={`message-row ${isRight ? 'right' : ''} ${isHighlighted ? 'highlighted' : ''}`}
            ref={isHighlighted ? (el => el?.scrollIntoView({ behavior: 'smooth', block: 'center' })) : null}
          >
            <div className="message-content">
              {!isRight && getAvatar(msg.sender)}

              <div className="message-bubble-wrapper">
                <div className="message-meta">
                  <span className="sender-name">{getSenderName(msg.sender)}</span>
                  <span className="message-time">{getTime(msg.timestamp)}</span>
                </div>
                <div className={`message-bubble ${msg.sender}`}>
                  {renderMessageContent(msg)}
                </div>
              </div>

              {isRight && getAvatar(msg.sender)}
            </div>
          </div>
        );
      })}

      <div ref={bottomRef} />
    </div>
  );
};

export default ChatWindow;
