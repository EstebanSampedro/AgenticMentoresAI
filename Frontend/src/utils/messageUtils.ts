import type { Message } from '../context/ChatContext';
import {
  normalizeAttachment,
  stripAttachmentPlaceholders,
  getFileCategory,
  type NormalizedAttachment
} from './attachments';

// helpers
const hasRealHtml = (html: string) => /<\/?[a-z][\s\S]*>/i.test(html);
const onlyWhitespaceAfterStrip = (html: string) =>
  html.replace(/\u00a0/g, ' ')     // &nbsp;
      .replace(/<[^>]+>/g, '')     // quita tags
      .replace(/\s+/g, ' ')
      .trim().length === 0;

/** SignalR/API → Message */
export const processIncomingMessage = (msg: any, chatId: string): Message => {
  const rawAttachments = Array.isArray(msg.attachments)
    ? msg.attachments
    : Array.isArray(msg.Attachments)
      ? msg.Attachments
      : [];

  const attachments = rawAttachments
    .map((att: any) =>
      att?.contentUrl && att?.name && att?.contentType
        ? { name: att.name, contentUrl: att.contentUrl, contentType: att.contentType }
        : normalizeAttachment(att)
    )
    .filter((att: any): att is NormalizedAttachment =>
      !!att && typeof att.name === 'string' &&
      typeof att.contentUrl === 'string' &&
      typeof att.contentType === 'string'
    );

  // contenido limpio
  let content = stripAttachmentPlaceholders(msg.content || msg.Content || '');

  // tipo: prioriza contentType del mensaje; si no, por attachment (MIME + nombre)
  const rawType = (msg.contentType || msg.ContentType || '').toLowerCase();
  const first = attachments[0];
  let type: Message['type'] =
    rawType
      ? (rawType as Message['type'])
      : (first ? getFileCategory(first.contentType, first.name) : 'text');

  // si vino 'html' pero no hay HTML real → degrada a 'text'
  if (type === 'html' && !hasRealHtml(content)) type = 'text';

  // si no quedó texto y hay adjuntos, evita burbuja vacía
  if (onlyWhitespaceAfterStrip(content) && attachments.length) {
    content = '';
  }

  const role = msg.senderRole || msg.SenderRole || '';
  const sender: Message['sender'] =
    role === 'Mentor' ? 'mentor' : role === 'IA' ? 'IA' : 'student';

  const result = {
    id: Number(msg.messageId || msg.MessageId || msg.Id),
    chatId: String(chatId).toLowerCase().trim(),
    sender,
    content,
    timestamp: msg.timestamp || msg.Timestamp || msg.Date,
    type,
    attachments: attachments.length ? attachments : undefined,
  };
  return result;
};



/**
 * Mapea un mensaje de la API al formato Message
 */
/** API list → Message */
export const mapApiMessageToMessage = (m: any, chatId: string): Message => {
  const attachments: NormalizedAttachment[] = Array.isArray(m.Attachments)
    ? m.Attachments.map(normalizeAttachment)
        .filter((a: NormalizedAttachment | null): a is NormalizedAttachment => a !== null)
    : [];

  let content = typeof m.MessageContent === 'string'
    ? stripAttachmentPlaceholders(m.MessageContent)
    : (m.MessageContent ?? '');

  const rawType = (m.MessageContentType || '').toLowerCase();
  const first = attachments[0];
  let type: Message['type'] =
    rawType
      ? (rawType as Message['type'])
      : (first ? getFileCategory(first.contentType, first.name) : 'text');

  if (type === 'html' && !hasRealHtml(String(content))) type = 'text';
  if (onlyWhitespaceAfterStrip(String(content)) && attachments.length) {
    content = '';
  }

  return {
    id: m.Id,
    chatId: String(chatId).toLowerCase().trim(),
    sender: m.SenderRole === 'Mentor' ? 'mentor' : m.SenderRole === 'IA' ? 'IA' : 'student',
    content,
    timestamp: m.Date,
    type,
    attachments: attachments.length ? attachments : undefined,
  };
};

