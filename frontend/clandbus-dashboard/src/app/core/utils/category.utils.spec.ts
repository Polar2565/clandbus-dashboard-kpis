import { categoryVisual } from './category.utils';

describe('categoryVisual', () => {
  it('mantiene un color estable para cada categoría conocida', () => {
    expect(categoryVisual('Certificaciones').key).toBe('certification');
    expect(categoryVisual('Migraciones').key).toBe('migration');
    expect(categoryVisual('Soporte').key).toBe('support');
    expect(categoryVisual('Desarrollo').key).toBe('development');
  });

  it('usa una identidad neutral cuando la categoría no está reconocida', () => {
    expect(categoryVisual('Categoría nueva').key).toBe('other');
    expect(categoryVisual('').key).toBe('other');
  });
});
