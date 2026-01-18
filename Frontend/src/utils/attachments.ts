import type { Message } from '../context/ChatContext';

export type NormalizedAttachment = {
  name: string;
  contentUrl: string;
  contentType: string; 
};

/**
 * ✅ Detecta extensión de archivo desde URL o nombre
 */
const getFileExtension = (urlOrName: string): string => {
  try {
    const pathPart = urlOrName.split('?')[0];
    const ext = pathPart.toLowerCase().split('.').pop() || '';
    return ext;
  } catch {
    return '';
  }
};

/**
 * ✅ Convierte FileType del backend a MIME type correcto
 * FileType puede ser: "image", "video", "audio", "pdf", etc.
 */
export const fileTypeToMime = (fileType?: string): string => {
  if (!fileType) return 'application/octet-stream';
  const ft = fileType.toLowerCase();
  
  // ✅ CRÍTICO: Detectar por categoría general primero
  if (ft === 'image') return 'image/jpeg'; // default para imágenes
  if (ft === 'video') return 'video/mp4';
  if (ft === 'audio') return 'audio/mpeg';
  if (ft === 'document') return 'application/pdf';
  
  // Extensiones específicas
  if (ft === 'pdf') return 'application/pdf';
  if (ft === 'jpg' || ft === 'jpeg') return 'image/jpeg';
  if (ft === 'png') return 'image/png';
  if (ft === 'gif') return 'image/gif';
  if (ft === 'webp') return 'image/webp';
  if (ft === 'svg') return 'image/svg+xml';
  if (ft === 'mp3') return 'audio/mpeg';
  if (ft === 'wav') return 'audio/wav';
  if (ft === 'mp4') return 'video/mp4';
  if (ft === 'webm') return 'video/webm';
  
  return `application/${ft}`;
};

/**
 * ✅ MIME-type + FileType + extensión → Message["type"]
 */
export const getFileCategory = (mime?: string, name?: string, fileType?: string): Message['type'] => {
  // 1. ✅ PRIORIDAD: Si tenemos FileType del backend, usarlo
  if (fileType) {
    const ft = fileType.toLowerCase();
    if (ft === 'image') return 'image';
    if (ft === 'video') return 'video';
    if (ft === 'audio') return 'audio';
    if (ft === 'document' || ft === 'pdf') return 'document';
  }
  
  // 2. Detectar por MIME (si no es "reference")
  const m = (mime || '').toLowerCase();
  if (m !== 'reference' && m !== 'application/octet-stream') {
    if (m.startsWith('image/')) return 'image';
    if (m.startsWith('audio/')) return 'audio';
    if (m.startsWith('video/')) return 'video';
    if (m === 'application/pdf' || m.includes('pdf')) return 'document';
    if (m.includes('word') || m.includes('excel') || m.includes('powerpoint') || m.includes('document')) {
      return 'document';
    }
  }

  // 3. Fallback: detectar por extensión del nombre/URL
  const ext = getFileExtension(name || '');
  if (['png','jpg','jpeg','gif','webp','bmp','heic','heif','svg','ico'].includes(ext)) {
    return 'image';
  }
  if (['mp4','webm','mov','avi','mkv','flv','wmv'].includes(ext)) {
    return 'video';
  }
  if (['mp3','wav','ogg','m4a','aac','flac','wma'].includes(ext)) {
    return 'audio';
  }
  if (['pdf','doc','docx','ppt','pptx','xls','xlsx','txt','md'].includes(ext)) {
    return 'document';
  }

  return 'generic';
};

/**
 * ✅ Detecta si una URL apunta a una imagen
 */
export const isImageUrl = (url: string): boolean => {
  if (!url) return false;
  const ext = getFileExtension(url);
  return ['png','jpg','jpeg','gif','webp','bmp','heic','heif','svg','ico'].includes(ext);
};

/**
 * ✅ Normaliza attachments - PRIORIZA FileType sobre ContentType
 */
export const normalizeAttachment = (a: any): NormalizedAttachment | null => {
  if (!a || typeof a !== 'object') return null;

  // ✅ Caso 1: { Name, ContentUrl, ContentType, FileType }
  if (a?.Name && a?.ContentUrl) {
    // Si ContentType es "reference", usar FileType
    const mime = (a.ContentType === 'reference' || !a.ContentType)
      ? fileTypeToMime(a.FileType)
      : a.ContentType;
      
    return {
      name: a.Name,
      contentUrl: a.ContentUrl,
      contentType: mime,
    };
  }

  // ✅ Caso 2: { FileName, DownloadUrl/Url, FileType }
  if (a?.FileName && (a?.DownloadUrl || a?.Url)) {
    const mime = (a.ContentType === 'reference' || !a.ContentType)
      ? fileTypeToMime(a.FileType)
      : a.ContentType;

    return {
      name: a.FileName,
      contentUrl: a.DownloadUrl || a.Url,
      contentType: mime,
    };
  }

  // ✅ Caso 3: { fileName, downloadUrl/url, fileType } (camelCase)
  if (a?.fileName && (a?.downloadUrl || a?.url)) {
    const mime = (a.contentType === 'reference' || !a.contentType)
      ? fileTypeToMime(a.fileType)
      : a.contentType;

    return {
      name: a.fileName,
      contentUrl: a.downloadUrl || a.url,
      contentType: mime,
    };
  }

  // ✅ Caso 4: { name, contentUrl/url, contentType }
  if (a?.name && (a?.contentUrl || a?.url)) {
    const mime = (a.contentType === 'reference' || !a.contentType)
      ? fileTypeToMime(getFileExtension(a.name))
      : a.contentType;
      
    return {
      name: a.name,
      contentUrl: a.contentUrl || a.url,
      contentType: mime,
    };
  }

  return null;
};

/**
 * ✅ Limpia placeholders de attachments del HTML
 */
export const stripAttachmentPlaceholders = (html: string | undefined | null): string => {
  if (typeof html !== 'string') return '';
  return html.replace(/<attachment[^>]*><\/attachment>/gi, '');
};