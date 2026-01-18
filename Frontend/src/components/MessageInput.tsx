import React, { useState, useRef, useEffect } from 'react';
import { useChat, getAuthenticatedToken } from '../context/ChatContext';
import './MessageInput.css';

import iconAdd from '../assets/iconAdd.png';
import iconEmoji from '../assets/iconEmogi.png';
import iconSend from '../assets/iconSend.png';
import { useErrorQueue } from "../hooks/useErrorQueue";

import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import EmojiPicker from 'emoji-picker-react';

import { getFileIcon } from '../utils/fileUtils';
import type { Message } from "../context/ChatContext";
import { logger } from '../utils/logger';
import { getFileCategory } from '../utils/attachments';

const API_BASE = import.meta.env.VITE_API_BASE;
type UploadStatus = 'queued' | 'uploading' | 'sent' | 'error';
// --- Tipos locales para la bandeja ---
type PendingFile = {
  id: string;
  file: File;
  previewUrl: string;
  contentType: string;
  name: string;
  progress: number;                 // 0–100
  status: UploadStatus;             // estado visual
  error?: string;
  controller?: AbortController;     // para cancelar
};

const MessageInput = () => {
  const { selectedStudent, sendMessage, sendTextMessage, triggerScrollToBottom } = useChat();
  const [isSending, setIsSending] = useState(false);
  const [showEmojiPicker, setShowEmojiPicker] = useState(false);
  const MAX_FILE_MB = 4; // ⚠️ ajusta al límite de tu backend
  const { addError } = useErrorQueue();


  // 🆕 archivos en espera de envío (no subidos aún)
  const [pendingFiles, setPendingFiles] = useState<PendingFile[]>([]);

  const editor = useEditor({
    extensions: [StarterKit],
    content: '',
    onUpdate: ({ editor }) => { },
  });


  const fileInputRef = useRef<HTMLInputElement>(null);

  if (!selectedStudent) return null;

  // Helpers
  const currentHtml = () => editor?.getHTML().trim() ?? '';

  const addFiles = (files: FileList | File[]) => {
    const arr = Array.from(files);
    const toAdd: PendingFile[] = [];

    for (const f of arr) {
      const sizeMB = f.size / (1024 * 1024);
      if (sizeMB > MAX_FILE_MB) {
        addError(`El archivo "${f.name}" (${sizeMB.toFixed(2)} MB) excede el límite de ${MAX_FILE_MB} MB.`);
        logger().warn('File size exceeds limit');
        continue; // ❌ no agrega a la bandeja
      }

      toAdd.push({
        id: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
        file: f,
        previewUrl: URL.createObjectURL(f),
        contentType: f.type || 'application/octet-stream',
        name: f.name,
        progress: 0,
        status: 'queued',
      });
    }

    if (toAdd.length) setPendingFiles(prev => [...prev, ...toAdd]);
  };


  const removePending = (id: string) => {
    setPendingFiles((prev) => {
      const item = prev.find(p => p.id === id);
      if (item) URL.revokeObjectURL(item.previewUrl);
      return prev.filter(p => p.id !== id);
    });
  };
  const clearPending = () => {
    setPendingFiles(prev => {
      prev.forEach(p => URL.revokeObjectURL(p.previewUrl));
      return [];
    });
    if (fileInputRef.current) fileInputRef.current.value = '';
  };
  // Subida binaria con progreso usando XHR
  async function uploadBinaryWithProgress(
  url: string,
  file: File,
  onProgress: (pct: number) => void,
  signal?: AbortSignal,
  token?: string,
  customFormData?: FormData // ← Nuevo parámetro
): Promise<any> {
  const formData = customFormData || new FormData();
  
  // Solo agregar el archivo si no hay FormData personalizado
  if (!customFormData) {
    formData.append('file', file);
  }

  const res = await new Promise<{ status: number; body: any }>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', url);

    if (token) xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    xhr.setRequestHeader('Accept', 'application/json');

    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) {
        const pct = Math.round((e.loaded / e.total) * 100);
        onProgress(Math.min(99, pct));
      }
    };

    xhr.onreadystatechange = () => {
      if (xhr.readyState === XMLHttpRequest.DONE) {
        try {
          resolve({ status: xhr.status, body: JSON.parse(xhr.responseText) });
        } catch {
          resolve({ status: xhr.status, body: null });
        }
      }
    };
    
    xhr.onerror = () => reject(new Error('Network error'));

    if (signal) {
      signal.addEventListener('abort', () => {
        try { xhr.abort(); } catch { }
        reject(new Error('aborted'));
      });
    }

    xhr.send(formData);
  });

  if (res.status === 413) throw new Error('413');
  if (res.status < 200 || res.status >= 300) throw new Error(`HTTP ${res.status}`);
  return res.body;
}

  // Adjuntar desde input
  const handleMultipleFiles = (event: React.ChangeEvent<HTMLInputElement>) => {
    if (!event.target.files) return;
    addFiles(event.target.files);
    event.target.value = ''; // limpiar para poder volver a cargar el mismo archivo si se cancela
  };

  // Pegar imagen desde portapapeles (se agrega a la bandeja)
  const handlePaste = (event: React.ClipboardEvent) => {
    const items = event.clipboardData.items;
    for (const item of items) {
      if (item.type.indexOf('image') !== -1) {
        const file = item.getAsFile();
        if (file) addFiles([file]);
      }
    }
  };

  // --- Enviar ---
const handleSend = async () => {
  const text = currentHtml();
  const hasText = text && text !== '<p></p>';
  const hasFiles = pendingFiles.length > 0;
  if ((!hasText && !hasFiles) || isSending) return;

  setIsSending(true);

  try {
    // 1) ✅ Enviar SOLO texto si hay (sin archivos)
    if (hasText && !hasFiles) {
      await sendTextMessage(selectedStudent.chatId, text);
      editor?.commands.clearContent();
    }

    // 2) ✅ Subir archivos (secuencial)
    for (const pf of pendingFiles) {
      try {
        // ⚠️ Si hay texto + archivo, lo enviamos juntos en el primer file
        const contentToSend = (hasText && pendingFiles.indexOf(pf) === 0) ? text : '';
        await uploadSingleFile(pf, contentToSend);
        
        // Limpiar texto después del primer archivo
        if (hasText && pendingFiles.indexOf(pf) === 0) {
          editor?.commands.clearContent();
        }
      } catch (err) {
        logger().error('Error al subir archivo:', err);
        addError(`❌ Error al enviar ${pf.name}`);
      }
    }

    // Limpieza UI
    pendingFiles.forEach(p => URL.revokeObjectURL(p.previewUrl));
    setPendingFiles([]);
    triggerScrollToBottom();
    
  } catch (err) {
    logger().error('Error al enviar mensaje:', err);
    addError('❌ Error al enviar mensaje');
  } finally {
    clearPending();
    setIsSending(false);
  }
};

// --- Subida + post de mensaje  ---
const uploadSingleFile = async (pf: PendingFile, additionalText: string = '') => {
  setPendingFiles(prev => prev.map(x =>
    x.id === pf.id ? { ...x, status: 'uploading', progress: 1, controller: new AbortController() } : x
  ));
  
  const controller = new AbortController();
  setPendingFiles(prev => prev.map(x => x.id === pf.id ? { ...x, controller } : x));

  try {
    const token = await getAuthenticatedToken();
    if (!token) {
      logger().error('No auth token');
      throw new Error('No auth token');
    }

    //  Crear FormData con metadata
    const formData = new FormData();
    formData.append('file', pf.file);
    formData.append('SenderRole', 'Mentor');
    formData.append('ContentType', getFileCategory(pf.file.type, pf.name));
    
    // Si hay texto adicional, inclúyelo
    if (additionalText) {
      formData.append('Content', additionalText);
    }
    
    // Adjuntos como JSON string
    formData.append('Attachments', JSON.stringify([
      { 
        Name: pf.name, 
        ContentType: pf.file.type 
      }
    ]));

    //  UN SOLO POST con todo incluido
    const uploadData = await uploadBinaryWithProgress(
      `${API_BASE}/api/chat/${selectedStudent!.chatId}/message`,
      pf.file,
      (pct) => setPendingFiles(prev => prev.map(x => x.id === pf.id ? { ...x, progress: pct } : x)),
      controller.signal,
      token,
      formData // ← Pasar el FormData completo
    );

    if (uploadData?.ResponseCode !== 0) throw new Error('Error al subir archivo');
    
    setPendingFiles(prev => prev.map(x => x.id === pf.id ? { ...x, progress: 100, status: 'sent' } : x));
    
  } catch (err: any) {
    logger().error('Error en uploadSingleFile:', err);
    
    setPendingFiles(prev => prev.map(x => 
      x.id === pf.id 
        ? { ...x, status: 'error', error: err.message || 'Error desconocido' } 
        : x
    ));
    
    if (err.message !== 'aborted') {
      addError(`No se pudo enviar ${pf.name}`);
    }
    
    throw err;
  }
};

  // Deshabilitar botón enviar si está enviando o no hay contenido
  const disableSend = isSending || (!pendingFiles.length && (!currentHtml() || currentHtml() === '<p></p>'));

  return (
    <div className="message-input-container" onPaste={handlePaste}>
      {/* 🆕 Bandeja de previsualizaciones, como Teams */}
      {pendingFiles.length > 0 && (
        <PendingTray items={pendingFiles} onRemove={removePending} />
      )}

      {/* Editor + acciones */}
      <div className="input-row">
        <EditorContent
          editor={editor}
          className="editor"
          onKeyDown={(e: any) => {
            // Enter = enviar, Shift+Enter = salto
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              handleSend();
            }
          }}
        />

        <div className="icons">
          <label htmlFor="file-input">
            <img src={iconAdd} alt="Adjuntar" />
          </label>
          <input
            type="file"
            id="file-input"
            style={{ display: 'none' }}
            multiple
            ref={fileInputRef}
            onChange={handleMultipleFiles}
            disabled={isSending}
            accept="image/*,video/*,audio/*,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv"
          />

          <img
            src={iconEmoji}
            alt="Emoji"
            onClick={() => setShowEmojiPicker(!showEmojiPicker)}
            style={{ cursor: 'pointer' }}
          />

          {showEmojiPicker && (
            <div className="emoji-picker-container">
              <EmojiPicker onEmojiClick={(e: any) => {
                editor?.commands.insertContent(e.emoji);
                setShowEmojiPicker(false);
              }} />
            </div>
          )}

          <div className="divider" />
          <img
            src={iconSend}
            alt="Enviar"
            onClick={handleSend}
            className={`send ${disableSend ? 'disabled' : ''}`}
            style={{ opacity: disableSend ? 0.5 : 1, pointerEvents: disableSend ? 'none' : 'auto' }}
          />
        </div>
      </div>
    </div>
  );
};

export default MessageInput;

// --- Bandeja de previsualizaciones ---
const fileIcon = (mime: string) => {
  if (mime.includes('pdf')) return '📄';
  if (mime.includes('word') || mime.endsWith('/msword') || mime.includes('officedocument.word')) return '📑';
  if (mime.includes('excel') || mime.includes('spreadsheet')) return '📊';
  if (mime.includes('powerpoint') || mime.includes('presentation')) return '🗂️';
  if (mime.startsWith('audio/')) return '🎵';
  if (mime.startsWith('video/')) return '🎞️';
  return '📁';
};

const PendingTray: React.FC<{
  items: PendingFile[];
  onRemove: (id: string) => void;
}> = ({ items, onRemove }) => {
  return (
    <div className="pending-tray">
      <div className="pending-title">Archivos por enviar</div>
      <div className="pending-grid">
        {items.map((p) => {
          const isImage = p.contentType.startsWith('image/');
          const isUploading = p.status === 'uploading';
          const isError = p.status === 'error';
          const isSent = p.status === 'sent';

          return (
            <div className={`pending-card ${p.status}`} key={p.id}>
              <div className="pending-thumb">
                {isImage ? <img src={p.previewUrl} alt={p.name} /> : <div className="pending-icon">{fileIcon(p.contentType)}</div>}
              </div>

              <div className="pending-meta">
                <div className="pending-name" title={p.name}>{p.name}</div>

                {/* Botón eliminar deshabilitado durante upload */}
                <button
                  className="pending-close"
                  aria-label="Quitar"
                  onClick={() => onRemove(p.id)}
                  disabled={isUploading}
                  title={isUploading ? 'Subiendo…' : 'Quitar'}
                >
                  ×
                </button>
              </div>

              {/* Barra de progreso / estados */}
              {isUploading && (
                <div className="progress-wrap">
                  <div className="progress-bar" style={{ width: `${p.progress}%` }} />
                  <div className="progress-label">Enviando… {p.progress}%</div>
                </div>
              )}

              {isError && (
                <div className="error-wrap">
                  <span>⚠️ {p.error || 'Error'}</span>
                </div>
              )}

              {isSent && <div className="sent-badge">Enviado</div>}
            </div>
          );
        })}
      </div>
    </div>
  );
};

