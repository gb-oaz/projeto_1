import { beforeEach, describe, expect, it } from 'vitest';

// Cada app sobe pelo main.ts e monta a raiz em #app (RN-149).
describe.each(['landing_page', 'hub', 'quest'])('app %s', (app) => {
  beforeEach(() => {
    document.body.innerHTML = '<div id="app"></div>';
  });

  it('monta a raiz em #app', async () => {
    await import(`../../apps/${app}/main.ts`);

    expect(document.querySelector(`#app [data-app="${app}"]`)).not.toBeNull();
  });
});
