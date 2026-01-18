import React, { useState, useRef, useEffect } from 'react';
import { useChat, getAuthenticatedToken } from '../context/ChatContext';
import './ChatSearchPanel.css';
import imagenBusqueda from '../assets/imagenBusqueda.png';
import DOMPurify from 'dompurify';
import { logger } from '../utils/logger';

const API_BASE = import.meta.env.VITE_API_BASE;

interface SearchResult {
  Id: number;
  SenderRole: string;
  ChatId: string;
  MessageContent: string;
  MessageContentType: string;
  Date: string;
}

const ChatSearchPanel = () => {
  const { selectedStudent, fetchMessageContext } = useChat();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<number | null>(null);

  const sanitizeInput = (input: string): string =>
    input.replace(/[<>{}[\]\\`"'()\/]/g, '').trim();

  const escapeRegExp = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

  const highlightHtml = (html: string, kw: string) => {
    if (!kw.trim()) return DOMPurify.sanitize(html);
    const clean = DOMPurify.sanitize(html);
    const container = document.createElement('div');
    container.innerHTML = clean;

    const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT);
    const re = new RegExp(`(${escapeRegExp(kw)})`, 'gi');
    while (walker.nextNode()) {
      const node = walker.currentNode as Text;
      const val = node.nodeValue;
      if (!val) continue;
      if (re.test(val)) {
        const span = document.createElement('span');
        span.innerHTML = val.replace(re, '<mark>$1</mark>');
        node.parentNode?.replaceChild(span, node);
      }
    }
    return container.innerHTML;
  };

  const authorizedFetch = async (url: string) => {
    const token = await getAuthenticatedToken();
    if (!token) throw new Error('No auth token');
    return fetch(url, {
      headers: {
        Accept: 'application/json',
        Authorization: `Bearer ${token}`,
      },
    });
  };

  const runSearch = async (raw: string) => {
    const cleanQuery = sanitizeInput(raw);
    if (!selectedStudent || cleanQuery.length < 3) {
      setResults([]);
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const url = `${API_BASE}/api/chat/${encodeURIComponent(
        selectedStudent.chatId
      )}/messages?query=${encodeURIComponent(cleanQuery)}`;
      const response = await authorizedFetch(url);
      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const data: {
        ResponseCode: number;
        ResponseMessage?: string;
        ResponseData?: {
          Messages?: any[];
          messages?: any[]; 
        };
      } = await response.json();
      // Soporta Messages/messages y también el caso sin envoltorio
      const responseData = (data && (data.ResponseData as any)) ?? (data as any);
      const msgs: any[] = responseData?.Messages ?? responseData?.messages ?? [];

      if (!Array.isArray(msgs)) {
        setResults([]);
      } else {
        const enriched = msgs.map((msg: any) => ({
          ...msg,
          ChatId: selectedStudent.chatId,
        })) as SearchResult[];
        setResults(enriched);
      }

      logger().info('ChatSearch OK', {
        component: 'ChatSearchPanel',
        count: Array.isArray(msgs) ? msgs.length : 0,
      } as any);

    } catch (error) {
      logger().error('ChatSearch failed', error, { component: 'ChatSearchPanel' } as any);
      setResults([]);
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = () => runSearch(query);

  // Debounce al escribir
  useEffect(() => {
    if (debounceRef.current) window.clearTimeout(debounceRef.current);
    if (!query.trim()) {
      setResults([]);
      setLoading(false);
      return;
    }
    debounceRef.current = window.setTimeout(() => runSearch(query), 400);
    return () => {
      if (debounceRef.current) window.clearTimeout(debounceRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query, selectedStudent?.chatId]);

  const handleClear = () => {
    setQuery('');
    setResults([]);
    inputRef.current?.focus();
  };

  const handleResultClick = (r: SearchResult) => {
    fetchMessageContext(r.ChatId, r.Id);
  };

  return (
    <div className="chat-search-panel">
      <h2>Buscar en el chat</h2>

      <div className="search-header">
        <input
          ref={inputRef}
          type="text"
          value={query}
          placeholder="Escribe una palabra clave de búsqueda"
          maxLength={100}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
        />
        {query && (
          <button className="clear-button" onClick={handleClear}>
            Borrar
          </button>
        )}
      </div>

      {loading && <div className="search-loading">Buscando...</div>}

      {!loading && results.length === 0 && !query && (
        <div className="search-placeholder">
          <img className="ImgLupagrande" src={imagenBusqueda} alt="Buscar en este chat" />
          <p>
            <strong>Buscar en este chat</strong>
          </p>
          <p>Busca mensajes, archivos y direcciones URL compartidos en este chat.</p>
        </div>
      )}

      {!loading && results.length > 0 && (
        <div className="search-results">
          <h4>Resultados</h4>
          {results.map((r) => (
            <div key={r.Id} className="search-result" onClick={() => handleResultClick(r)}>
              <p>
                <strong>{r.SenderRole}</strong>:{' '}
                <span
                  dangerouslySetInnerHTML={{
                    __html: highlightHtml(r.MessageContent || '', query),
                  }}
                />
              </p>
              <small>{new Date(r.Date).toLocaleDateString()}</small>
              <br />
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default ChatSearchPanel;
