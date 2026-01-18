// src/components/ImagePreview.tsx
import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import './ImagePreview.css';

interface ImagePreviewProps {
  src: string;
  alt: string;
  className?: string;
}

const ImagePreview: React.FC<ImagePreviewProps> = ({ src, alt, className = '' }) => {
  const [imageSrc, setImageSrc] = useState<string>('');
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);

  useEffect(() => {
    let revoked = false;

    const loadImage = async () => {
      try {
        setIsLoading(true);
        setError(false);
        setImageSrc(src);
        setIsLoading(false);
      } catch (err) {
        setError(true);
        setIsLoading(false);
      }
    };

    if (src) loadImage();

    return () => {
      if (!revoked && imageSrc?.startsWith('blob:')) {
        URL.revokeObjectURL(imageSrc);
        revoked = true;
      }
    };
  }, [src]);

  // Bloquear scroll del body cuando el modal está abierto
  useEffect(() => {
    if (!isModalOpen) return;
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setIsModalOpen(false);
    window.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = prev;
      window.removeEventListener('keydown', onKey);
    };
  }, [isModalOpen]);

  if (isLoading) {
    return (
      <div className={`image-preview-loading ${className}`}>
        <div className="spinner" />
        <span>Cargando imagen...</span>
      </div>
    );
  }

  if (error || !imageSrc) {
    return (
      <div className={`image-preview-error ${className}`}>
        <span>❌ No se pudo cargar la imagen</span>
        <small>{src}</small>
      </div>
    );
  }

  const overlay = (
    <div className="image-modal-overlay" onClick={() => setIsModalOpen(false)} role="dialog" aria-modal="true">
      <div className="image-modal-content" onClick={(e) => e.stopPropagation()}>
        <button className="image-modal-close" onClick={() => setIsModalOpen(false)} aria-label="Cerrar">✕</button>
        <img src={imageSrc} alt={alt} className="image-modal-full" />
      </div>
    </div>
  );

  return (
    <>
      <img
        src={imageSrc}
        alt={alt}
        className={`${className} image-preview-clickable`}
        onClick={() => setIsModalOpen(true)}
      />
      {isModalOpen ? createPortal(overlay, document.body) : null}
    </>
  );
};

export default ImagePreview;
