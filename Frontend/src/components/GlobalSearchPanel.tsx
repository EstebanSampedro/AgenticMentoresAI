import React, { useState, useEffect, useRef } from 'react';
import lupaIcon from '../assets/iconLupa.png';
import './GlobalSearchPanel.css';
import { useChat, getAuthenticatedToken } from '../context/ChatContext'; // 👈 import del token helper
import DOMPurify from 'dompurify';
import { logger } from '../utils/logger';

const API_BASE = import.meta.env.VITE_API_BASE;

interface MessageClaveTotal {
  Id: number;
  StudentFullName: string;
  ChatId: string | null;
  SenderRole: string;
  Content: string;
  ContentType: string;
  Date: string;
}

export const GlobalSearchPanel = () => {
  const [keyword, setKeyword] = useState('');
  const [messages, setMessages] = useState<MessageClaveTotal[]>([]);
  const [loading, setLoading] = useState(false);
  const { mentorEmail, fetchMessageContext } = useChat();
  const containerRef = useRef<HTMLDivElement>(null);
  const debounceRef = useRef<number | null>(null);

  // --- Helpers ---
  const sanitizeInput = (input: string): string =>
    input.replace(/[<>{}[\]\\`"'()\/]/g, '').trim();

  const authorizedFetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const token = await getAuthenticatedToken();
    if (!token) throw new Error('No auth token');
    const headers = new Headers(init?.headers || {});
    headers.set('Authorization', `Bearer ${token}`);
    headers.set('Accept', 'application/json');
    // Content-Type no es necesario para GET
    return fetch(input, { ...init, headers });
  };

  const highlightHtml = (htmlString: string, kw: string) => {
    if (!kw.trim()) return DOMPurify.sanitize(htmlString);

    const cleanHTML = DOMPurify.sanitize(htmlString);
    const container = document.createElement('div');
    container.innerHTML = cleanHTML;

    // Resalta texto en nodos de texto
    const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT);
    const regex = new RegExp(`(${escapeRegExp(kw)})`, 'gi');

    while (walker.nextNode()) {
      const node = walker.currentNode as Text;
      const value = node.nodeValue;
      if (!value) continue;

      if (regex.test(value)) {
        const span = document.createElement('span');
        span.innerHTML = value.replace(regex, '<mark>$1</mark>');
        node.parentNode?.replaceChild(span, node);
      }
    }
    return container.innerHTML;
  };

  const escapeRegExp = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

  // --- Buscar (con debounce) ---
  const runSearch = async (q: string) => {
    const cleanKeyword = sanitizeInput(q);
    if (!mentorEmail || cleanKeyword.length < 3) {
      setMessages([]);
      return;
    }

    setLoading(true);
    try {
      const url = `${API_BASE}/api/mentor/${encodeURIComponent(
        mentorEmail
      )}/messages?query=${encodeURIComponent(cleanKeyword)}`;

      const response = await authorizedFetch(url);
      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const json = await response.json();
      setMessages(Array.isArray(json.ResponseData.Results) ? json.ResponseData.Results : []);

      logger().info('Global search OK', {
        component: 'GlobalSearchPanel',
        count: Array.isArray(json.ResponseData.Results) ? json.ResponseData.Results.length : 0,
      } as any);
    } catch (error) {
      logger().error('Global search failed', error, { component: 'GlobalSearchPanel' } as any);
      setMessages([]);
    } finally {
      setLoading(false);
    }
  };

  // Debounce al teclear
  useEffect(() => {
    if (debounceRef.current) window.clearTimeout(debounceRef.current);
    // si input vacío, limpia inmediato
    if (keyword.trim() === '') {
      setMessages([]);
      setLoading(false);
      return;
    }
    debounceRef.current = window.setTimeout(() => runSearch(keyword), 400);
    return () => {
      if (debounceRef.current) window.clearTimeout(debounceRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [keyword, mentorEmail]);

  // Cerrar resultados al click fuera
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setMessages([]);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <div className="global-search-panel" ref={containerRef}>
      <div className="search-bar">
        <img src={lupaIcon} alt="Buscar" />
        <input
          type="text"
          placeholder="Buscar…"
          maxLength={100}
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && runSearch(keyword)}
        />
      </div>

      {loading && <div>Cargando…</div>}

      {!loading && messages.length > 0 && (
        <div className="result-block">
          <h4>Mensajes</h4>
          <div
            style={{
              maxHeight: '300px',
              overflowY: 'auto',
              paddingRight: '5px',
            }}
          >
            {messages.map((m) => (
              <div
                key={m.Id}
                className="result-entry"
                onClick={() => m.ChatId && fetchMessageContext(m.ChatId, m.Id)}
                style={{ cursor: 'pointer', marginBottom: '10px' }}
              >
                <span>
                  <strong>{m.SenderRole}</strong>:{' '}
                  <span
                    dangerouslySetInnerHTML={{
                      __html: highlightHtml(m.Content || '', keyword),
                    }}
                  />
                </span>
                <br />
                <small>{new Date(m.Date).toLocaleString()}</small>
                <br />
                <small>
                  <em>{m.StudentFullName}</em>
                </small>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

export default GlobalSearchPanel;
