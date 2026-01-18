import React from 'react';
import { usePreviewInfoLazy } from '../hooks/usePreviewInfoLazy';

type Props = { driveId: string; itemId: string };

const PreviewSkeleton = () => (
  <div style={{ width: 300, height: 200, borderRadius: 8, background: 'rgba(255,255,255,0.06)' }} />
);

const GenericPreview = ({ fileName }: { fileName?: string }) => (
  <div style={{ width: 300, padding: 8, border: '1px solid #444', borderRadius: 8 }}>
    <div>📁 {fileName ?? 'Archivo'}</div>
    <small>Sin previsualización disponible</small>
  </div>
);



export const FilePreview: React.FC<Props> = ({ driveId, itemId }) => {
  const { ref, data, loading, error } = usePreviewInfoLazy(driveId, itemId);

  return (
    <div ref={ref} className="file-preview">
      {loading && <PreviewSkeleton />}
      {error && <GenericPreview fileName="Archivo" />}
      {!loading && !error && data && (
        (() => {
          switch (data.previewType) {
            case 'image':
              return (
                <div className="image-preview">
                  <img
                    src={data.thumbnailUrl}
                    alt={data.fileName}
                    style={{ width: 300, height: 200, objectFit: 'cover', borderRadius: 8 }}
                    onClick={() => window.open(data.downloadUrl || data.officeOnlineUrl, '_blank')}
                  />
                  <p>{data.fileName}</p>
                </div>
              );
            case 'document':
              return (
                <div className="document-preview" style={{ width: 300 }}>
                  <div className="thumbnail-container" style={{ position: 'relative' }}>
                    <img
                      src={data.thumbnailUrl}
                      alt={data.fileName}
                      style={{ width: 300, height: 200, objectFit: 'cover', borderRadius: 8 }}
                    />
                    <button
                      aria-label="Abrir en Office"
                      style={{ position: 'absolute', inset: 0, margin: 'auto', height: 40, width: 140 }}
                      onClick={() => window.open(data.officeOnlineUrl || data.downloadUrl, '_blank')}
                    >
                      Abrir en Office
                    </button>
                  </div>
                  <p>{data.fileName}</p>
                </div>
              );
            case 'video':
              return (
                <div className="video-preview" style={{ width: 300 }}>
                  <div style={{ position: 'relative' }}>
                    <img
                      src={data.thumbnailUrl}
                      alt={`${data.fileName} thumbnail`}
                      style={{ width: 300, height: 200, objectFit: 'cover', borderRadius: 8 }}
                    />
                    <button
                      aria-label="Reproducir"
                      style={{ position: 'absolute', inset: 0, margin: 'auto', height: 48, width: 48, borderRadius: 24 }}
                      onClick={() => window.open(data.downloadUrl || data.officeOnlineUrl, '_blank')}
                    >
                      ▶️
                    </button>
                  </div>
                  <p>{data.fileName}</p>
                </div>
              );
            case 'audio':
              return (
                <div style={{ width: 300 }}>
                  <p>🎵 {data.fileName}</p>
                  <audio controls src={data.downloadUrl} style={{ width: '100%' }} />
                </div>
              );
            default:
              return <GenericPreview fileName={data.fileName} />;
          }
        })()
      )}
    </div>
  );
};

export default FilePreview;
