// src/hooks/usePreviewInfoLazy.ts
import { useEffect, useRef, useState } from 'react';

// 👇 Usa el tipo que ya definiste en tu ChatContext o muévelo a un archivo común (recomendado)
export interface FilePreviewInfo {
  fileName: string;
  fileSize: number;
  mimeType: string;
  previewType: 'text' | 'image' | 'document' | 'voice' | 'html' | 'video' | 'audio' | 'generic';
  thumbnailUrl?: string;
  downloadUrl?: string;
  officeOnlineUrl?: string;
  width?: number;
  height?: number;
}

export function usePreviewInfoLazy(driveId: string, itemId: string) {
  const [data, setData] = useState<FilePreviewInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [shouldLoad, setShouldLoad] = useState(false);
  const ref = useRef<HTMLDivElement | null>(null);

  // 1) Observa el viewport y sólo entonces dispara la carga
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const io = new IntersectionObserver(
      (entries) => entries.forEach(e => { if (e.isIntersecting) setShouldLoad(true); }),
      { rootMargin: '200px' }
    );
    io.observe(el);
    return () => io.disconnect();
  }, []);

  // 2) Carga con retry/backoff simple
  useEffect(() => {
    if (!shouldLoad) return;
    let cancelled = false;

    (async () => {
      setLoading(true);
      setError(null);

      for (let i = 0; i < 3; i++) {
        try {
          const res = await fetch(`/api/filepreview/info/${driveId}/${itemId}`);
          if (!res.ok) throw new Error(`HTTP ${res.status}`);
          const json: FilePreviewInfo = await res.json();
          if (!cancelled) setData(json);
          break; // éxito
        } catch (err: any) {
          if (i === 2 && !cancelled) setError(err.message ?? 'Error al cargar preview');
          await new Promise(r => setTimeout(r, 600 * (i + 1))); // backoff 600/1200/1800ms
        } finally {
          if (!cancelled) setLoading(false);
        }
      }
    })();

    return () => { cancelled = true; };
  }, [shouldLoad, driveId, itemId]);

  return { ref, data, loading, error };
}
