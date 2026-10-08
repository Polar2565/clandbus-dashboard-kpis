export interface CategoryVisual {
  key: string;
  color: string;
  soft: string;
}

const CATEGORY_VISUALS: Record<string, CategoryVisual> = {
  certification: { key: 'certification', color: '#6f5bd3', soft: '#f0edff' },
  migration: { key: 'migration', color: '#218f70', soft: '#e7f6f0' },
  support: { key: 'support', color: '#c5801d', soft: '#fff3df' },
  development: { key: 'development', color: '#2478b5', soft: '#e8f3fb' },
  content: { key: 'content', color: '#bd5a7b', soft: '#fbeaf1' },
  sales: { key: 'sales', color: '#d36c2c', soft: '#fff0e6' },
  customerSuccess: { key: 'customer-success', color: '#4d8b48', soft: '#edf6e9' },
  other: { key: 'other', color: '#718295', soft: '#edf2f5' },
};

export function categoryVisual(value: unknown): CategoryVisual {
  const category = `${value ?? ''}`.trim().toLocaleLowerCase('es-MX');
  if (/cert|curso|capacit/.test(category)) return CATEGORY_VISUALS['certification'];
  if (/migr|implement|config/.test(category)) return CATEGORY_VISUALS['migration'];
  if (/soporte|support|mesa de ayuda/.test(category)) return CATEGORY_VISUALS['support'];
  if (/desarroll|developer|program|software/.test(category)) return CATEGORY_VISUALS['development'];
  if (/contenido|content|webinar|publica/.test(category)) return CATEGORY_VISUALS['content'];
  if (/venta|sales|comercial/.test(category)) return CATEGORY_VISUALS['sales'];
  if (/éxito|exito|success|cliente/.test(category)) return CATEGORY_VISUALS['customerSuccess'];
  return CATEGORY_VISUALS['other'];
}
