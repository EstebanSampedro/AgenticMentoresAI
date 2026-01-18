export const getFileIcon = (mime: string): string => {
  if (mime.includes('pdf'))        return '📄';
  if (mime.includes('word'))       return '📑';
  if (mime.includes('excel'))      return '📊';
  if (mime.includes('powerpoint')) return '📊';
  if (mime.startsWith('audio/'))   return '🎵';
  if (mime.startsWith('video/'))   return '🎞️';
  if (mime.startsWith('image/'))   return '🖼️';
  return '📁';
};