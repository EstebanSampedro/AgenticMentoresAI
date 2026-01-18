import React, { useEffect, useMemo,useRef, useState } from 'react';
import { useChat } from '../context/ChatContext';
import './SummaryPanel.css';
import { logger } from '../utils/logger';

type SummaryFilter = 'All' | 'Mentor' | 'IA' | 'Diario' | 'Bajo Demanda';
const FILTERS: SummaryFilter[] = ['All', 'Mentor', 'IA', 'Diario', 'Bajo Demanda'];

const normalize = (s?: string) =>
  (s ?? '')
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .trim();

const SummaryPanel = () => {
  const {
    selectedStudent,
    resumes,
    fetchResumesFromApi,
    isSummaryLoading
  } = useChat();

  const [filter, setFilter] = useState<SummaryFilter>('All');
  const fetchedByChat = useRef<Record<string, boolean>>({});
  // Carga al abrir panel o cambiar estudiante
  useEffect(() => {
    const id = selectedStudent?.chatId;
    if (!id) return;

    // solo la primera vez por chatId:
    if (!fetchedByChat.current[id]) {
      fetchedByChat.current[id] = true;    // marca antes de fetch para evitar carreras
      setFilter('All');
      fetchResumesFromApi(id);
    }
  // 👇 dependen solo del chatId, no de funciones
  }, [selectedStudent?.chatId]);

  // Normalizados para comparar/contar
  const normalizedResumes = useMemo(
    () => resumes.map(r => ({ ...r, _t: normalize(r.summaryType) })),
    [resumes]
  );

  const counts = useMemo(() => {
    const base: Record<SummaryFilter, number> = {
      All: normalizedResumes.length,
      Mentor: 0,
      IA: 0,
      Diario: 0,
      'Bajo Demanda': 0
    };
    for (const r of normalizedResumes) {
      if (r._t === 'mentor') base.Mentor++;
      else if (r._t === 'ia') base.IA++;
      else if (r._t === 'diario') base.Diario++;
      else if (r._t === 'bajo demanda') base['Bajo Demanda']++;
    }
    return base;
  }, [normalizedResumes]);

  const filteredResumes = useMemo(() => {
    if (filter === 'All') return normalizedResumes;
    const target = normalize(filter);
    return normalizedResumes.filter(r => r._t === target);
  }, [normalizedResumes, filter]);

  if (!selectedStudent) return null;

 

  return (
    <div className="summary-panel">
      <div className="summary-header">
        <h3>Resumen de {selectedStudent.name}</h3>
        
      </div>

      {/* Filtros */}
      <div className="summary-filters" role="tablist" aria-label="Filtros de resúmenes">
        {FILTERS.map(f => (
          <button
            key={f}
            role="tab"
            aria-selected={filter === f}
            className={`chip filter ${filter === f ? 'active' : ''}`}
            onClick={() => setFilter(f)}
            disabled={isSummaryLoading}
            title={`Filtrar por ${f}`}
          >
            {f} <span className="count">{counts[f]}</span>
          </button>
        ))}
      </div>

      {isSummaryLoading && <div className="summary-loading">Generando resumen…</div>}

      {!isSummaryLoading && filteredResumes.length === 0 && (
        <p className="summary-empty">No hay resúmenes para este filtro.</p>
      )}

      {!isSummaryLoading &&
        filteredResumes.length > 0 &&
        filteredResumes.map(r => (
          <div key={r.id} className="summary-item fade-in">
            <div className="summary-meta">
              <span className="chip">{r.summaryType}</span>
              <span className="date">{formatSummaryDate(r.createdAt)}</span>
              {r.escalated && <span className="chip warn">Escalado</span>}
            </div>

            {r.escalated && r.escalationReason && (
              <p className="escalation">Motivo: {r.escalationReason}</p>
            )}

            <p className="summary-text">Resumen: {r.summary}</p>

            {r.keyPoints?.length > 0 && (
              <>
                <strong>Puntos clave:</strong>
                <ul className="kp-list">
                  {r.keyPoints.map((kp, i) => (
                    <li key={i}>{kp}</li>
                  ))}
                </ul>
              </>
            )}
          </div>
        ))}
    </div>
  );
};

const formatSummaryDate = (iso: string) => {
  try {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) {
      logger().warn('Fecha inválida en resumen', { iso } as any);
      return 'Fecha inválida';
    }
    return new Intl.DateTimeFormat('es-EC', {
      dateStyle: 'medium',
      timeStyle: 'short',
      hour12: true,
    }).format(d);
  } catch (err) {
    logger().warn('Error parseando fecha de resumen', { iso, err } as any);
    return 'Fecha inválida';
  }
};

export default SummaryPanel;
