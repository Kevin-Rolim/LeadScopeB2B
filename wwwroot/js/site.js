(() => {
  'use strict';
  const $ = (s, root = document) => root.querySelector(s);
  const icon = name => `<svg class="icon" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><use href="/icons/lucide.svg#${name}"></use></svg>`;
  const confirmation = $('#confirm-dialog');
  const panel = $('#side-panel');
  let confirmAction = null, previousFocus = null, request = null;
  let dirty = false;
  const approved = new WeakSet();
  function notify(message) {
    const node = document.createElement('div');
    node.className = 'notice notice-danger'; node.setAttribute('role', 'alert');
    node.innerHTML = `${icon('circle-alert')}<span></span><button type="button" class="icon-button" data-dismiss aria-label="Fechar notificação">${icon('x')}</button>`;
    $('span', node).textContent = message;
    $('.toast-stack').append(node);
  }
  function confirm(message, action, title = 'Confirmar operação', label = 'Confirmar') {
    $('#confirm-title').textContent = title;
    $('#confirm-description').textContent = message;
    $('[data-confirm-accept]').textContent = label;
    confirmAction = action;
    confirmation.showModal();
    $('[data-confirm-cancel]').focus();
  }
  function closePanel() {
    request?.abort(); panel.close(); dirty = false;
    previousFocus?.focus();
  }
  function tryClosePanel() {
    if (dirty) confirm('Os dados preenchidos serão perdidos. Deseja descartar as alterações?', closePanel, 'Descartar alterações?', 'Descartar');
    else closePanel();
  }
  function enhance(root = document) {
    root.querySelectorAll('input[data-val-required],select[data-val-required],textarea[data-val-required]').forEach(input => {
      if (input.type !== 'hidden' && !input.disabled) input.required = true;
    });
    root.querySelectorAll('input[data-val-range-min]').forEach(input => { input.min = input.dataset.valRangeMin; input.max = input.dataset.valRangeMax; });
    root.querySelectorAll('input[type="password"]').forEach(input => {
      if (input.closest('.password-wrap')) return;
      const wrap = document.createElement('div'); wrap.className = 'password-wrap';
      input.before(wrap); wrap.append(input);
      const toggle = document.createElement('button');
      toggle.type = 'button'; toggle.className = 'icon-button password-toggle';
      toggle.setAttribute('aria-label', 'Mostrar senha'); toggle.setAttribute('aria-pressed', 'false');
      toggle.innerHTML = icon('eye');
      toggle.addEventListener('click', () => {
        const visible = input.type === 'password'; input.type = visible ? 'text' : 'password';
        toggle.setAttribute('aria-label', visible ? 'Ocultar senha' : 'Mostrar senha');
        toggle.setAttribute('aria-pressed', String(visible)); toggle.innerHTML = icon(visible ? 'eye-off' : 'eye');
      });
      wrap.append(toggle);
    });
    root.querySelectorAll('.validation-summary-errors').forEach(el => { el.setAttribute('role', 'alert'); el.tabIndex = -1; });
    root.querySelectorAll('.field-validation-error').forEach(el => {
      const name = el.dataset.valmsgFor;
      const field = [...(el.closest('form')?.elements || [])].find(input => input.name === name);
      if (field) { el.id ||= `error-${field.id || name}`; field.setAttribute('aria-describedby', el.id); field.setAttribute('aria-invalid', 'true'); }
    });
  }
  async function openPanel(link) {
    request?.abort(); request = new AbortController();
    previousFocus = link; dirty = false;
    $('#panel-title').textContent = 'Carregando…';
    $('#panel-content').innerHTML = '<div class="panel-loading" role="status" aria-label="Carregando"><div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div></div>';
    if (!panel.open) panel.showModal();
    panel.setAttribute('aria-busy', 'true');
    try {
      const response = await fetch(link.href, { credentials: 'same-origin', signal: request.signal });
      const url = new URL(response.url);
      if (url.pathname.toLowerCase().startsWith('/conta/login')) { window.location.assign(response.url); return; }
      const doc = new DOMParser().parseFromString(await response.text(), 'text/html');
      const content = $('[data-page-content]', doc);
      if (!content) throw new Error('Página indisponível');
      $('#panel-title').textContent = $('h1', content)?.textContent || 'Detalhes';
      $('#panel-content').replaceChildren(...content.childNodes);
      $('.panel-footer', panel)?.remove();
      const actions = $('.form-actions', $('#panel-content'));
      if (actions && actions.closest('form')) {
        const form = actions.closest('form'); form.id ||= 'panel-main-form';
        actions.querySelectorAll('button').forEach(button => button.setAttribute('form', form.id));
        const footer = document.createElement('footer'); footer.className = 'panel-footer';
        footer.append(actions); $('.panel-shell', panel).append(footer);
      }
      enhance($('#panel-content'));
      const hash = new URL(link.href).hash;
      if (hash) $(hash, $('#panel-content'))?.scrollIntoView({ block: 'start' });
      else $('#panel-content').scrollTop = 0;
      $('[data-panel-close]').focus();
    } catch (error) {
      if (error.name === 'AbortError') return;
      closePanel(); window.location.assign(link.href);
    } finally { panel.removeAttribute('aria-busy'); }
  }
  let menuReturn = null;
  function setMenu(open) {
    const sidebar = $('#sidebar'); if (!sidebar) return;
    sidebar.classList.toggle('is-open', open); $('.menu-backdrop').classList.toggle('is-open', open);
    $('[data-menu-open]')?.setAttribute('aria-expanded', String(open));
    if (open) { menuReturn = document.activeElement; $('a', sidebar).focus(); }
    const main = $('.app-main'); if (main) main.inert = open;
    if (open) { sidebar.setAttribute('role', 'dialog'); sidebar.setAttribute('aria-modal', 'true'); }
    else { sidebar.removeAttribute('role'); sidebar.removeAttribute('aria-modal'); menuReturn?.focus(); }
    document.body.style.overflow = open ? 'hidden' : '';
  }
  document.addEventListener('click', e => {
    if (e.target.closest('[data-menu-open]')) setMenu(true);
    if (e.target.closest('[data-menu-close]')) setMenu(false);
    if (e.target.closest('[data-dismiss]')) e.target.closest('.notice').remove();
    if (e.target.closest('[data-panel-close]')) tryClosePanel();
    if (e.target.closest('[data-confirm-cancel]')) { confirmation.close(); confirmAction = null; }
    if (e.target.closest('[data-confirm-accept]')) { const action = confirmAction; confirmAction = null; confirmation.close(); action?.(); }
    const link = e.target.closest('a');
    if (!link || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
    if (link.hasAttribute('data-panel') && !link.hasAttribute('download')) {
      e.preventDefault();
      if (dirty && panel.open) confirm('Deseja descartar as alterações e abrir outro registro?', () => openPanel(link), 'Descartar alterações?', 'Descartar');
      else openPanel(link);
    } else if (panel.open && link.closest('#side-panel') && /^(Cancelar|Voltar)$/.test(link.textContent.trim())) { e.preventDefault(); tryClosePanel(); }
  });
  document.addEventListener('input', e => {
    if (e.target.closest('#panel-content form')) dirty = true;
    const input = e.target;
    if (!input.form) return;
    input.setCustomValidity('');
    input.removeAttribute('aria-invalid'); input.classList.remove('input-validation-error');
    input.form.querySelectorAll('[data-val-equalto-other]').forEach(field => {
      const other = field.form.elements.namedItem(field.dataset.valEqualtoOther.replace('*.', ''));
      field.setCustomValidity(field.value && other && field.value !== other.value ? field.dataset.valEqualto : '');
    });
  });
  document.addEventListener('invalid', e => {
    const input = e.target;
    if (!input.form) return;
    if (input.validity.valueMissing) input.setCustomValidity(input.dataset.valRequired || 'Preencha este campo.');
    else if (input.validity.typeMismatch) input.setCustomValidity(input.type === 'email' ? 'Informe um e-mail válido.' : 'Informe um valor válido.');
    else if (input.validity.patternMismatch && input.name === 'cnae') input.setCustomValidity('Selecione um CNAE no catálogo ou informe o código de sete dígitos.');
    input.setAttribute('aria-invalid', 'true');
  }, true);
  document.addEventListener('change', e => { if (e.target.closest('#panel-content form')) dirty = true; });
  document.addEventListener('submit', e => {
    const form = e.target;
    if (form.hasAttribute('data-download')) {
      if (!$('input[name="selecionados"]:checked')) { e.preventDefault(); notify('Selecione pelo menos um lead para exportar.'); }
      return;
    }
    if (form.dataset.confirm && !approved.has(form)) {
      e.preventDefault();
      const submitter = e.submitter;
      confirm(form.dataset.confirm, () => { approved.add(form); form.requestSubmit(submitter); });
      return;
    }
    if (form.dataset.busy === 'true') { e.preventDefault(); return; }
    form.dataset.busy = 'true'; form.setAttribute('aria-busy', 'true');
    const button = e.submitter;
    if (button) { button.dataset.originalHtml = button.innerHTML; button.disabled = true; button.insertAdjacentHTML('afterbegin', '<span class="spinner" aria-hidden="true"></span>'); }
    dirty = false;
  });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && $('#sidebar')?.classList.contains('is-open')) setMenu(false);
    if (e.key === 'Tab' && $('#sidebar')?.classList.contains('is-open')) {
      const items = [...$('#sidebar').querySelectorAll('a,button')].filter(el => el.getClientRects().length);
      if (e.shiftKey && document.activeElement === items[0]) { e.preventDefault(); items.at(-1).focus(); }
      else if (!e.shiftKey && document.activeElement === items.at(-1)) { e.preventDefault(); items[0].focus(); }
    }
  });
  panel.addEventListener('cancel', e => { e.preventDefault(); tryClosePanel(); });
  panel.addEventListener('click', e => { if (e.target === panel) { const r = panel.getBoundingClientRect(); if(e.clientX < r.left || e.clientX > r.right) tryClosePanel(); } });
  confirmation.addEventListener('cancel', () => { confirmAction = null; });
  window.addEventListener('pageshow', () => {
    document.querySelectorAll('form[data-busy]').forEach(form => { delete form.dataset.busy; form.removeAttribute('aria-busy'); });
    document.querySelectorAll('button[data-original-html]').forEach(button => { button.innerHTML = button.dataset.originalHtml; button.disabled = false; delete button.dataset.originalHtml; });
  });
  enhance();
})();
