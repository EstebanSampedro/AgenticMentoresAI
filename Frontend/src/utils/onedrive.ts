export type ItemRef = { driveId: string; itemId: string } | null;

// Intenta parsear URLs estilo Graph/SharePoint
export const extractDriveAndItem = (url: string): ItemRef => {
  if (!url) return null;

  // /drives/{driveId}/items/{itemId}
  const m1 = url.match(/\/drives\/([^/]+)\/items\/([^/?#]+)/i);
  if (m1) return { driveId: m1[1], itemId: m1[2] };

  // /_api/v2.0/drives/{driveId}/items/{itemId}
  const m2 = url.match(/\/_api\/v2\.0\/drives\/([^/]+)\/items\/([^/?#]+)/i);
  if (m2) return { driveId: m2[1], itemId: m2[2] };

  // Si tu API devuelve query params ?driveId=...&itemId=...
  try {
    const u = new URL(url, window.location.origin);
    const driveId = u.searchParams.get('driveId');
    const itemId = u.searchParams.get('itemId');
    if (driveId && itemId) return { driveId, itemId };
  } catch { /* ignore */ }

  return null;
};